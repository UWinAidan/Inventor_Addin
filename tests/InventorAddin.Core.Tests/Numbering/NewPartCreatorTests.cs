using InventorAddin.Core.Logging;
using InventorAddin.Core.Models;
using InventorAddin.Core.Numbering;
using InventorAddin.Core.PartProperties;
using InventorAddin.Core.Settings;
using InventorAddin.Core.ViewModels;

namespace InventorAddin.Core.Tests.Numbering;

public sealed class NewPartCreatorTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 10, 14, 3, 0, TimeSpan.Zero);

    private readonly string _root;
    private readonly string _shared;
    private readonly string _local;
    private readonly string _cad;
    private readonly FakeEnvironment _environment = new();
    private readonly RecordingLog _log = new();
    private readonly FakeDocuments _documents = new();

    public NewPartCreatorTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "InventorAddinTests", Guid.NewGuid().ToString("N"));
        _shared = Path.Combine(_root, "nas");
        _local = Path.Combine(_root, "appdata", "register");
        _cad = Path.Combine(_root, "cad");
        Directory.CreateDirectory(_shared);
        Directory.CreateDirectory(_cad);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private string SharedPath => Path.Combine(_shared, RegisterStore.SharedFileName);
    private string PendingPath => Path.Combine(_local, RegisterStore.PendingFileName);

    private RegisterStore Store() => new(_shared, _local, _environment, _log, TimeSpan.FromMilliseconds(200));

    private NewPartCreator Creator(string? designer = "Test Designer", Func<string, bool>? folderExists = null) =>
        new(Store(), _documents, designer, _log, folderExists);

    private NewPartRequest Request(
        NewFileKind kind = NewFileKind.Part,
        string name = "test bracket",
        string partType = "M",
        UnitsSystem units = UnitsSystem.Metric,
        string? folder = null) =>
        new(kind, name, partType, units, folder ?? _cad);

    private static List<RegisterEntry> Read(string path) => RegisterCsv.Read(File.ReadAllText(path)).Entries.ToList();

    private void WriteShared(params int[] values) =>
        File.WriteAllText(SharedPath, RegisterCsv.Write(values.Select(v => new RegisterEntry(
            new PartNumber(v), "other part", "", DocumentKind.Part, $@"C:\Other\AWB-{v},other_part.ipt",
            Now.AddDays(-1), "otheruser", "OTHERPC"))));

    // The folder the dialog starts in

    [Theory]
    [InlineData(@"C:\Work\Parts\plate,AWB-10001.ipt", @"D:\Cad", @"C:\Work\Parts")]
    [InlineData(@"C:\plate,AWB-10001.ipt", @"D:\Cad", @"C:\")]
    [InlineData("/home/user/cad/plate.ipt", @"D:\Cad", "/home/user/cad")]
    [InlineData("", @" D:\Cad ", @"D:\Cad")]
    [InlineData(null, @"D:\Cad", @"D:\Cad")]
    [InlineData("  ", @"D:\Cad", @"D:\Cad")]
    [InlineData(null, "", "")]
    [InlineData(null, null, "")]
    public void DefaultFolder_IsTheActiveDocumentsFolder_ElseTheWorkFolder(string? active, string? work, string expected)
    {
        Assert.Equal(expected, NewPartCreator.DefaultFolder(active, work));
    }

    // Messages

    [Fact]
    public void Messages()
    {
        Assert.Equal(
            "Inventor has no default sheet metal part template for imperial units. Nothing was assigned.",
            NewPartCreator.MissingTemplateMessage(NewFileKind.SheetMetalPart, UnitsSystem.Imperial));
        Assert.Equal(
            "Inventor has no default assembly template for metric units. Nothing was assigned.",
            NewPartCreator.MissingTemplateMessage(NewFileKind.Assembly, UnitsSystem.Metric));
        Assert.Equal(
            @"Part number AWB-10001 was assigned, but the part could not be created: Boom. The number stays in the register.",
            NewPartCreator.FailedAfterAssignMessage("AWB-10001", NewFileKind.Part, "Boom."));
    }

    // Success

    [Fact]
    public void Create_AssignsWritesAndSaves_InOrder()
    {
        WriteShared(10001);

        var result = Creator().Create(Request());

        var expectedPath = Path.Combine(_cad, "test_bracket,AWB-10002.ipt");
        Assert.Equal("AWB-10002", result.PartNumber);
        Assert.Equal(expectedPath, result.FullPath);
        Assert.False(result.Offline);
        Assert.Empty(result.DuplicateMessages);

        Assert.Equal(
            [$"find {NewFileKind.Part} {UnitsSystem.Metric}", $"create {NewFileKind.Part} template.ipt", "write", $"save {expectedPath}"],
            _documents.Calls);

        var document = Assert.Single(_documents.Created);
        Assert.Equal(CreationProperties.Build(new PartNumber(10002), "test bracket", "M", "Test Designer"), document.Writes);
        Assert.False(document.Closed);

        var entry = Read(SharedPath).Last();
        Assert.Equal(10002, entry.Number.Value);
        Assert.Equal("test bracket", entry.Name);
        Assert.Equal("M", entry.PartType);
        Assert.Equal(DocumentKind.Part, entry.Kind);
        Assert.Equal(expectedPath, entry.FilePath);
    }

    [Theory]
    [InlineData(NewFileKind.SheetMetalPart, UnitsSystem.Imperial, "test_bracket,AWB-10001.ipt", DocumentKind.SheetMetalPart)]
    [InlineData(NewFileKind.Assembly, UnitsSystem.Metric, "test_bracket,AWB-10001.iam", DocumentKind.Assembly)]
    public void Create_PassesKindAndUnits_AndUsesTheKindsExtension(
        NewFileKind kind, UnitsSystem units, string fileName, DocumentKind registered)
    {
        var result = Creator().Create(Request(kind: kind, units: units, partType: "A"));

        Assert.Equal(Path.Combine(_cad, fileName), result.FullPath);
        Assert.Equal($"find {kind} {units}", _documents.Calls[0]);
        Assert.Equal($"create {kind} template.ipt", _documents.Calls[1]);
        Assert.Equal(registered, Read(SharedPath).Single().Kind);
    }

    [Fact]
    public void Create_Offline_SaysSo_AndTheRowWaitsInPending()
    {
        _environment.Reachable = false;

        var result = Creator().Create(Request());

        Assert.True(result.Offline);
        Assert.Equal("AWB-10001", result.PartNumber);
        Assert.Equal(result.FullPath, Read(PendingPath).Single().FilePath);
        Assert.Equal(
            "Part number AWB-10001 was assigned offline; it will be added to the shared register at the next sync.",
            NewPartCreator.Notice(result));
    }

    [Fact]
    public void Create_ReportsDuplicatesFoundBySync()
    {
        WriteShared(10001);
        Directory.CreateDirectory(_local);
        File.WriteAllText(PendingPath, RegisterCsv.Write([new RegisterEntry(
            new PartNumber(10001), "mine", "", DocumentKind.Part, @"C:\Mine\mine,AWB-10001.ipt", Now, "testuser", "TESTPC")]));

        var result = Creator().Create(Request());

        var line = Assert.Single(result.DuplicateMessages);
        Assert.Contains("AWB-10001", line, StringComparison.Ordinal);
        Assert.Equal(PartPropertiesViewModel.FormatDuplicates(result.DuplicateMessages), NewPartCreator.Notice(result));
    }

    [Fact]
    public void Notice_IsNullWhenOnlineWithoutDuplicates()
    {
        Assert.Null(NewPartCreator.Notice(new NewPartResult("AWB-10001", "x", false, [])));
    }

    [Fact]
    public void Notice_OfflineAndDuplicates_BothShown()
    {
        var text = NewPartCreator.Notice(new NewPartResult("AWB-10001", "x", true, ["dup line"]));

        Assert.NotNull(text);
        Assert.StartsWith("Part number AWB-10001 was assigned offline", text, StringComparison.Ordinal);
        Assert.Contains("- dup line", text, StringComparison.Ordinal);
    }

    // Stops before a number is assigned

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void MissingTemplate_StopsBeforeAssigning(string? template)
    {
        _documents.Template = template;

        var ex = Assert.Throws<NewPartException>(() => Creator().Create(Request(kind: NewFileKind.SheetMetalPart, units: UnitsSystem.Imperial)));

        Assert.Equal(NewPartCreator.MissingTemplateMessage(NewFileKind.SheetMetalPart, UnitsSystem.Imperial), ex.Message);
        Assert.Null(ex.AssignedNumber);
        AssertNothingAssignedOrCreated();
    }

    [Fact]
    public void MissingTemplate_IsCheckedBeforeTheFolder()
    {
        _documents.Template = "";
        var probed = false;

        Assert.Throws<NewPartException>(() => Creator(folderExists: _ => probed = true).Create(Request()));

        Assert.False(probed);
    }

    [Theory]
    [InlineData("")]
    [InlineData("relative/folder")]
    public void FolderNotAFullPath_StopsBeforeAssigning(string folder)
    {
        var ex = Assert.Throws<NewPartException>(() => Creator().Create(Request(folder: folder)));

        Assert.Equal(NewPartCreator.NotFullPathMessage, ex.Message);
        AssertNothingAssignedOrCreated();
    }

    [Fact]
    public void FolderMissing_StopsBeforeAssigning()
    {
        var missing = Path.Combine(_root, "no such folder");

        var ex = Assert.Throws<NewPartException>(() => Creator().Create(Request(folder: missing)));

        Assert.Equal(NewPartCreator.FolderMissingMessage(missing), ex.Message);
        AssertNothingAssignedOrCreated();
    }

    [Fact]
    public void FolderUnreachable_ByTheProbe_StopsBeforeAssigning()
    {
        var ex = Assert.Throws<NewPartException>(() => Creator(folderExists: _ => false).Create(Request()));

        Assert.Equal(NewPartCreator.FolderMissingMessage(_cad), ex.Message);
        AssertNothingAssignedOrCreated();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void FileWithTheNextNumberAlreadyThere_StopsWithoutTakingTheNumber(bool reachable)
    {
        WriteShared(10001);
        Directory.CreateDirectory(_local);
        File.WriteAllText(Path.Combine(_local, RegisterStore.LocalFileName), File.ReadAllText(SharedPath));
        _environment.Reachable = reachable;
        var existing = Path.Combine(_cad, "test_bracket,AWB-10002.ipt");
        File.WriteAllText(existing, "not a part");

        var ex = Assert.Throws<NewPartException>(() => Creator().Create(Request()));

        Assert.Equal(NewPartCreator.ExistsBeforeAssignMessage(existing), ex.Message);
        Assert.Null(ex.AssignedNumber);
        Assert.Equal([10001], Read(SharedPath).Select(e => e.Number.Value));
        Assert.False(File.Exists(PendingPath));
        Assert.Empty(_documents.Created);
    }

    [Fact]
    public void PathTooLong_StopsWithoutTakingTheNumber()
    {
        var ex = Assert.Throws<NewPartException>(() => Creator().Create(Request(name: new string('x', 300))));

        Assert.Contains("too long", ex.Message, StringComparison.Ordinal);
        AssertNothingAssignedOrCreated();
    }

    [Fact]
    public void RegisterBusy_StopsWithItsMessage_NothingAssigned()
    {
        File.WriteAllText(Path.Combine(_shared, RegisterStore.LockFileName), "other");

        var ex = Assert.Throws<NewPartException>(() => Creator().Create(Request()));

        Assert.IsType<RegisterBusyException>(ex.InnerException);
        Assert.Equal(ex.InnerException!.Message, ex.Message);
        Assert.Null(ex.AssignedNumber);
        Assert.Empty(_documents.Created);
    }

    [Fact]
    public void SequenceFull_StopsWithItsMessage()
    {
        WriteShared(99999);

        var ex = Assert.Throws<NewPartException>(() => Creator().Create(Request()));

        Assert.Contains("sequence is full", ex.Message, StringComparison.Ordinal);
        Assert.Null(ex.AssignedNumber);
        Assert.Empty(_documents.Created);
    }

    // Fails after the number was assigned

    [Fact]
    public void FileAppearsAfterAssigning_StopsNamingTheNumber_AndTheRowStays()
    {
        var path = Path.Combine(_cad, "test_bracket,AWB-10001.ipt");

        // The store logs the assignment once the row is written; the file appears then, after the name was checked.
        _log.OnInfo = message =>
        {
            if (message.StartsWith("Part number AWB-10001 assigned", StringComparison.Ordinal))
                File.WriteAllText(path, "another file");
        };

        var ex = Assert.Throws<NewPartException>(() => Creator().Create(Request()));

        Assert.Equal(NewPartCreator.ExistsAfterAssignMessage("AWB-10001", path), ex.Message);
        Assert.Equal("AWB-10001", ex.AssignedNumber);
        Assert.Equal([10001], Read(SharedPath).Select(e => e.Number.Value));
        Assert.Empty(_documents.Created);
        Assert.Equal("another file", File.ReadAllText(path));
    }

    [Theory]
    [InlineData("create")]
    [InlineData("write")]
    [InlineData("save")]
    public void FailureAfterAssigning_SaysWhichNumber_LogsIt_ClosesTheDocument_AndTheRowStays(string failingStep)
    {
        var failure = new InvalidOperationException("Inventor said no.");
        _documents.FailAt = failingStep;
        _documents.Failure = failure;

        var ex = Assert.Throws<NewPartException>(() => Creator().Create(Request()));

        Assert.Equal(NewPartCreator.FailedAfterAssignMessage("AWB-10001", NewFileKind.Part, "Inventor said no."), ex.Message);
        Assert.Equal("AWB-10001", ex.AssignedNumber);
        Assert.Same(failure, ex.InnerException);
        Assert.Equal([10001], Read(SharedPath).Select(e => e.Number.Value));
        Assert.Contains(_log.Errors, e => ReferenceEquals(e.Ex, failure) && e.Message.Contains("AWB-10001", StringComparison.Ordinal));

        if (failingStep == "create")
        {
            Assert.Empty(_documents.Created);
        }
        else
        {
            Assert.True(Assert.Single(_documents.Created).Closed);
        }
    }

    [Fact]
    public void FailureAfterAssigning_ACloseThatFailsIsLogged_AndTheOriginalMessageIsKept()
    {
        _documents.FailAt = "save";
        _documents.Failure = new IOException("Disk full.");
        _documents.CloseFailure = new InvalidOperationException("Cannot close.");

        var ex = Assert.Throws<NewPartException>(() => Creator().Create(Request()));

        Assert.Contains("Disk full.", ex.Message, StringComparison.Ordinal);
        Assert.Contains(_log.Errors, e => e.Ex is InvalidOperationException { Message: "Cannot close." });
    }

    [Fact]
    public void Constructor_RejectsNulls()
    {
        Assert.Throws<ArgumentNullException>(() => new NewPartCreator(null!, _documents, null));
        Assert.Throws<ArgumentNullException>(() => new NewPartCreator(Store(), null!, null));
        Assert.Throws<ArgumentNullException>(() => Creator().Create(null!));
    }

    private void AssertNothingAssignedOrCreated()
    {
        Assert.False(File.Exists(SharedPath) && Read(SharedPath).Count > 0);
        Assert.False(File.Exists(PendingPath));
        Assert.Empty(_documents.Created);
    }

    private sealed class FakeDocuments : INewPartDocuments
    {
        public string? Template { get; set; } = "template.ipt";
        public List<string> Calls { get; } = [];
        public List<FakeDocument> Created { get; } = [];
        public string? FailAt { get; set; }
        public Exception? Failure { get; set; }
        public Exception? CloseFailure { get; set; }

        public string? FindTemplate(NewFileKind kind, UnitsSystem units)
        {
            Calls.Add($"find {kind} {units}");
            return Template;
        }

        public INewPartDocument Create(NewFileKind kind, string templatePath)
        {
            Calls.Add($"create {kind} {templatePath}");
            if (FailAt == "create")
                throw Failure!;
            var document = new FakeDocument(this);
            Created.Add(document);
            return document;
        }
    }

    private sealed class FakeDocument : INewPartDocument
    {
        private readonly FakeDocuments _owner;

        public FakeDocument(FakeDocuments owner) => _owner = owner;

        public IReadOnlyList<PropertyWrite>? Writes { get; private set; }
        public bool Closed { get; private set; }

        public void WriteProperties(IReadOnlyList<PropertyWrite> writes)
        {
            _owner.Calls.Add("write");
            if (_owner.FailAt == "write")
                throw _owner.Failure!;
            Writes = writes;
        }

        public void SaveAs(string fullPath)
        {
            _owner.Calls.Add($"save {fullPath}");
            if (_owner.FailAt == "save")
                throw _owner.Failure!;
            File.WriteAllText(fullPath, "saved");
        }

        public void CloseWithoutSaving()
        {
            Closed = true;
            if (_owner.CloseFailure is not null)
                throw _owner.CloseFailure;
        }
    }

    private sealed class FakeEnvironment : IRegisterEnvironment
    {
        public bool Reachable { get; set; } = true;
        public DateTimeOffset Now => NewPartCreatorTests.Now;
        public string AssignedBy => "testuser";
        public string ComputerName => "TESTPC";
        public bool ProbeFolder(string folder, TimeSpan timeout) => Reachable && Directory.Exists(folder);
    }

    private sealed class RecordingLog : ILog
    {
        public List<(string Message, Exception? Ex)> Errors { get; } = [];
        public Action<string>? OnInfo { get; set; }
        public void Info(string message) => OnInfo?.Invoke(message);
        public void Warn(string message) { }
        public void Error(string message, Exception? ex = null) => Errors.Add((message, ex));
    }
}
