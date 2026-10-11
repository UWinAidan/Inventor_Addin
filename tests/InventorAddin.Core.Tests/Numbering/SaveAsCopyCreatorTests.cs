using InventorAddin.Core.Logging;
using InventorAddin.Core.Models;
using InventorAddin.Core.Numbering;
using InventorAddin.Core.PartProperties;

namespace InventorAddin.Core.Tests.Numbering;

public sealed class SaveAsCopyCreatorTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 10, 14, 3, 0, TimeSpan.Zero);

    private readonly string _root;
    private readonly string _shared;
    private readonly string _local;
    private readonly string _cad;
    private readonly string _original;
    private readonly FakeEnvironment _environment = new();
    private readonly RecordingLog _log = new();
    private readonly FakeSource _source = new();

    public SaveAsCopyCreatorTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "InventorAddinTests", Guid.NewGuid().ToString("N"));
        _shared = Path.Combine(_root, "nas");
        _local = Path.Combine(_root, "appdata", "register");
        _cad = Path.Combine(_root, "cad");
        Directory.CreateDirectory(_shared);
        Directory.CreateDirectory(_cad);
        _original = Path.Combine(_cad, "test_bracket,AWB-10001.ipt");
        File.WriteAllText(_original, "original");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private string SharedPath => Path.Combine(_shared, RegisterStore.SharedFileName);
    private string PendingPath => Path.Combine(_local, RegisterStore.PendingFileName);

    private RegisterStore Store() => new(_shared, _local, _environment, _log, TimeSpan.FromMilliseconds(200));

    private SaveAsCopyCreator Creator(
        string? designer = "Test Designer",
        Func<string, bool>? folderExists = null,
        Action<string>? deleteFile = null) =>
        new(Store(), _source, designer, _log, folderExists, deleteFile);

    private SaveAsCopyRequest Request(
        string? original = null,
        DocumentKind kind = DocumentKind.Part,
        string name = "TEST BRACKET COPY",
        string partType = "M") =>
        new(original ?? _original, kind, name, partType);

    private static List<RegisterEntry> Read(string path) => RegisterCsv.Read(File.ReadAllText(path)).Entries.ToList();

    private void WriteShared(params int[] values) =>
        File.WriteAllText(SharedPath, RegisterCsv.Write(values.Select(v => new RegisterEntry(
            new PartNumber(v), "other part", "", DocumentKind.Part, $@"C:\Other\AWB-{v},other_part.ipt",
            Now.AddDays(-1), "otheruser", "OTHERPC"))));

    // Helpers

    [Theory]
    [InlineData(@"C:\Work\plate,AWB-10001.ipt", ".ipt")]
    [InlineData("/home/user/cad/Assembly.IAM", ".IAM")]
    [InlineData(@"C:\Work.v2\plate", "")]
    [InlineData(@"C:\Work\plate.", "")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void ExtensionOf(string? path, string expected)
    {
        Assert.Equal(expected, SaveAsCopyCreator.ExtensionOf(path));
    }

    [Theory]
    [InlineData(DocumentKind.Part, true)]
    [InlineData(DocumentKind.SheetMetalPart, true)]
    [InlineData(DocumentKind.Assembly, true)]
    [InlineData(DocumentKind.WeldmentAssembly, true)]
    [InlineData(DocumentKind.Drawing, false)]
    [InlineData(DocumentKind.Presentation, false)]
    [InlineData(DocumentKind.Unknown, false)]
    public void CanCopy_PartsAndAssembliesOnly(DocumentKind kind, bool expected)
    {
        Assert.Equal(expected, SaveAsCopyCreator.CanCopy(kind));
    }

    [Fact]
    public void Messages()
    {
        Assert.Equal(
            "Part number AWB-10002 was assigned, but the copy could not be made: Boom. The number stays in the register.",
            SaveAsCopyCreator.FailedAfterAssignMessage("AWB-10002", "Boom."));
    }

    // Success

    [Fact]
    public void Copy_SavesACopyInTheOriginalsFolder_OpensIt_WritesAndSaves_InOrder()
    {
        WriteShared(10001);

        var result = Creator().Copy(Request());

        var expectedPath = Path.Combine(_cad, "TEST_BRACKET_COPY,AWB-10002.ipt");
        Assert.Equal("AWB-10002", result.PartNumber);
        Assert.Equal(expectedPath, result.FullPath);
        Assert.False(result.Offline);
        Assert.Empty(result.DuplicateMessages);

        Assert.Equal([$"copy {expectedPath}", $"open {expectedPath}", "write", "save"], _source.Calls);

        var copy = Assert.Single(_source.Opened);
        Assert.Equal(CreationProperties.Build(new PartNumber(10002), "TEST BRACKET COPY", "M", "Test Designer"), copy.Writes);
        Assert.False(copy.Closed);
        Assert.Equal("original", File.ReadAllText(_original));

        var entry = Read(SharedPath).Last();
        Assert.Equal(10002, entry.Number.Value);
        Assert.Equal("TEST BRACKET COPY", entry.Name);
        Assert.Equal("M", entry.PartType);
        Assert.Equal(DocumentKind.Part, entry.Kind);
        Assert.Equal(expectedPath, entry.FilePath);
    }

    [Theory]
    [InlineData(DocumentKind.Assembly, "Plate.iam", "NEW_PLATE,AWB-10001.iam")]
    [InlineData(DocumentKind.WeldmentAssembly, "Frame.iam", "NEW_PLATE,AWB-10001.iam")]
    [InlineData(DocumentKind.SheetMetalPart, "old_plate.ipt", "NEW_PLATE,AWB-10001.ipt")]
    public void Copy_KeepsTheOriginalsExtension_AndRegistersItsKind(DocumentKind kind, string originalName, string copyName)
    {
        var original = Path.Combine(_cad, originalName);
        File.WriteAllText(original, "original");

        var result = Creator().Copy(Request(original: original, kind: kind, name: "new plate", partType: "A"));

        Assert.Equal(Path.Combine(_cad, copyName), result.FullPath);
        Assert.Equal(kind, Read(SharedPath).Single().Kind);
    }

    [Fact]
    public void Copy_NameIsNormalised_AndACommaIsAccepted()
    {
        var result = Creator().Copy(Request(name: "  left_bracket, long  "));

        Assert.Equal(Path.Combine(_cad, "LEFT_BRACKET,_LONG,AWB-10001.ipt"), result.FullPath);
        Assert.Equal("LEFT BRACKET, LONG", Read(SharedPath).Single().Name);
        Assert.Equal(
            CreationProperties.Build(new PartNumber(10001), "LEFT BRACKET, LONG", "M", "Test Designer"),
            Assert.Single(_source.Opened).Writes);
    }

    [Fact]
    public void Copy_BlankDesigner_WritesNoDesignerOrDetailer()
    {
        Creator(designer: " ").Copy(Request(partType: ""));

        var writes = Assert.Single(_source.Opened).Writes!;
        Assert.Equal(CreationProperties.Build(new PartNumber(10001), "TEST BRACKET COPY", "", null), writes);
        Assert.Equal(2, writes.Count);
    }

    [Fact]
    public void Copy_Offline_SaysSo_AndTheRowWaitsInPending()
    {
        _environment.Reachable = false;

        var result = Creator().Copy(Request());

        Assert.True(result.Offline);
        Assert.Equal(result.FullPath, Read(PendingPath).Single().FilePath);
        Assert.Equal(
            "Part number AWB-10001 was assigned offline; it will be added to the shared register at the next sync.",
            NewPartCreator.Notice(result));
    }

    [Fact]
    public void Copy_ReportsDuplicatesFoundBySync()
    {
        WriteShared(10001);
        Directory.CreateDirectory(_local);
        File.WriteAllText(PendingPath, RegisterCsv.Write([new RegisterEntry(
            new PartNumber(10001), "mine", "", DocumentKind.Part, @"C:\Mine\mine,AWB-10001.ipt", Now, "testuser", "TESTPC")]));

        var result = Creator().Copy(Request());

        Assert.Contains("AWB-10001", Assert.Single(result.DuplicateMessages), StringComparison.Ordinal);
    }

    // Stops before a number is assigned

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("Part1")]
    public void Unsaved_StopsBeforeAssigning(string original)
    {
        var ex = Assert.Throws<NewPartException>(() => Creator().Copy(Request(original: original)));

        Assert.Equal(SaveAsCopyCreator.UnsavedMessage, ex.Message);
        AssertNothingAssignedOrCopied();
    }

    [Theory]
    [InlineData(DocumentKind.Drawing)]
    [InlineData(DocumentKind.Presentation)]
    [InlineData(DocumentKind.Unknown)]
    public void WrongKind_StopsBeforeAssigning(DocumentKind kind)
    {
        var ex = Assert.Throws<NewPartException>(() => Creator().Copy(Request(kind: kind)));

        Assert.Equal(SaveAsCopyCreator.WrongDocumentMessage, ex.Message);
        AssertNothingAssignedOrCopied();
    }

    [Theory]
    [InlineData("", "Enter a part name.")]
    [InlineData("a?b", "A part name cannot contain the character '?'.")]
    public void BadName_StopsBeforeAssigning(string name, string problem)
    {
        var ex = Assert.Throws<NewPartException>(() => Creator().Copy(Request(name: name)));

        Assert.Equal(problem + " Nothing was assigned.", ex.Message);
        AssertNothingAssignedOrCopied();
    }

    [Fact]
    public void OriginalsFolderUnreachable_StopsBeforeAssigning()
    {
        var ex = Assert.Throws<NewPartException>(() => Creator(folderExists: _ => false).Copy(Request()));

        Assert.Equal(NewPartCreator.FolderMissingMessage(_cad), ex.Message);
        AssertNothingAssignedOrCopied();
    }

    [Fact]
    public void OriginalNotAFullPath_StopsBeforeAssigning()
    {
        var ex = Assert.Throws<NewPartException>(() => Creator().Copy(Request(original: "relative/plate,AWB-10001.ipt")));

        Assert.Equal(NewPartCreator.NotFullPathMessage, ex.Message);
        AssertNothingAssignedOrCopied();
    }

    [Fact]
    public void FileWithTheNextNumberAlreadyThere_StopsWithoutTakingTheNumber()
    {
        WriteShared(10001);
        var existing = Path.Combine(_cad, "TEST_BRACKET_COPY,AWB-10002.ipt");
        File.WriteAllText(existing, "not a part");

        var ex = Assert.Throws<NewPartException>(() => Creator().Copy(Request()));

        Assert.Equal(NewPartCreator.ExistsBeforeAssignMessage(existing), ex.Message);
        Assert.Null(ex.AssignedNumber);
        Assert.Equal([10001], Read(SharedPath).Select(e => e.Number.Value));
        Assert.Empty(_source.Calls);
        Assert.Equal("not a part", File.ReadAllText(existing));
    }

    [Fact]
    public void PathTooLong_StopsWithoutTakingTheNumber()
    {
        var ex = Assert.Throws<NewPartException>(() => Creator().Copy(Request(name: new string('x', 300))));

        Assert.Contains("too long", ex.Message, StringComparison.Ordinal);
        AssertNothingAssignedOrCopied();
    }

    [Fact]
    public void RegisterBusy_StopsWithItsMessage_NothingAssigned()
    {
        File.WriteAllText(Path.Combine(_shared, RegisterStore.LockFileName), "other");

        var ex = Assert.Throws<NewPartException>(() => Creator().Copy(Request()));

        Assert.IsType<RegisterBusyException>(ex.InnerException);
        Assert.Null(ex.AssignedNumber);
        Assert.Empty(_source.Calls);
    }

    // Fails after the number was assigned

    [Fact]
    public void FileAppearsAfterAssigning_StopsNamingTheNumber_AndLeavesThatFileAlone()
    {
        var path = Path.Combine(_cad, "TEST_BRACKET_COPY,AWB-10001.ipt");
        _log.OnInfo = message =>
        {
            if (message.StartsWith("Part number AWB-10001 assigned", StringComparison.Ordinal))
                File.WriteAllText(path, "another file");
        };

        var ex = Assert.Throws<NewPartException>(() => Creator().Copy(Request()));

        Assert.Equal(NewPartCreator.ExistsAfterAssignMessage("AWB-10001", path), ex.Message);
        Assert.Equal("AWB-10001", ex.AssignedNumber);
        Assert.Empty(_source.Calls);
        Assert.Equal("another file", File.ReadAllText(path));
    }

    [Theory]
    [InlineData("copy", false)]
    [InlineData("open", false)]
    [InlineData("write", true)]
    [InlineData("save", true)]
    public void FailureAfterAssigning_SaysWhichNumber_ClosesAndRemovesTheCopy_AndTheRowStays(string failingStep, bool opened)
    {
        var failure = new InvalidOperationException("Inventor said no.");
        _source.FailAt = failingStep;
        _source.Failure = failure;
        var path = Path.Combine(_cad, "TEST_BRACKET_COPY,AWB-10001.ipt");

        var ex = Assert.Throws<NewPartException>(() => Creator().Copy(Request()));

        Assert.Equal(SaveAsCopyCreator.FailedAfterAssignMessage("AWB-10001", "Inventor said no."), ex.Message);
        Assert.Equal("AWB-10001", ex.AssignedNumber);
        Assert.Same(failure, ex.InnerException);
        Assert.Equal([10001], Read(SharedPath).Select(e => e.Number.Value));
        Assert.Contains(_log.Errors, e => ReferenceEquals(e.Ex, failure) && e.Message.Contains("AWB-10001", StringComparison.Ordinal));
        Assert.False(File.Exists(path));
        Assert.Equal("original", File.ReadAllText(_original));

        if (opened)
            Assert.True(Assert.Single(_source.Opened).Closed);
        else
            Assert.Empty(_source.Opened);
    }

    [Fact]
    public void FailureAfterAssigning_ACopyThatCannotBeRemoved_IsNamedInTheMessage()
    {
        _source.FailAt = "write";
        _source.Failure = new InvalidOperationException("Inventor said no.");
        _source.CloseFailure = new InvalidOperationException("Cannot close.");
        var path = Path.Combine(_cad, "TEST_BRACKET_COPY,AWB-10001.ipt");

        var ex = Assert.Throws<NewPartException>(
            () => Creator(deleteFile: _ => throw new IOException("In use.")).Copy(Request()));

        Assert.Equal(
            SaveAsCopyCreator.FailedAfterAssignMessage("AWB-10001", "Inventor said no.") + " " +
            SaveAsCopyCreator.NotRemovedMessage(path),
            ex.Message);
        Assert.True(File.Exists(path));
        Assert.Contains(_log.Errors, e => e.Ex is InvalidOperationException { Message: "Cannot close." });
        Assert.Contains(_log.Errors, e => e.Ex is IOException { Message: "In use." });
    }

    [Fact]
    public void Constructor_RejectsNulls()
    {
        Assert.Throws<ArgumentNullException>(() => new SaveAsCopyCreator(null!, _source, null));
        Assert.Throws<ArgumentNullException>(() => new SaveAsCopyCreator(Store(), null!, null));
        Assert.Throws<ArgumentNullException>(() => Creator().Copy(null!));
    }

    private void AssertNothingAssignedOrCopied()
    {
        Assert.False(File.Exists(SharedPath) && Read(SharedPath).Count > 0);
        Assert.False(File.Exists(PendingPath));
        Assert.Empty(_source.Calls);
    }

    private sealed class FakeSource : ISaveAsCopySource
    {
        public List<string> Calls { get; } = [];
        public List<FakeCopy> Opened { get; } = [];
        public string? FailAt { get; set; }
        public Exception? Failure { get; set; }
        public Exception? CloseFailure { get; set; }

        public void SaveCopyAs(string fullPath)
        {
            Calls.Add($"copy {fullPath}");
            File.WriteAllText(fullPath, "copy");
            if (FailAt == "copy")
                throw Failure!;
        }

        public ICopiedDocument OpenCopy(string fullPath)
        {
            Calls.Add($"open {fullPath}");
            if (FailAt == "open")
                throw Failure!;
            var copy = new FakeCopy(this);
            Opened.Add(copy);
            return copy;
        }
    }

    private sealed class FakeCopy : ICopiedDocument
    {
        private readonly FakeSource _owner;

        public FakeCopy(FakeSource owner) => _owner = owner;

        public IReadOnlyList<PropertyWrite>? Writes { get; private set; }
        public bool Closed { get; private set; }

        public void WriteProperties(IReadOnlyList<PropertyWrite> writes)
        {
            _owner.Calls.Add("write");
            if (_owner.FailAt == "write")
                throw _owner.Failure!;
            Writes = writes;
        }

        public void Save()
        {
            _owner.Calls.Add("save");
            if (_owner.FailAt == "save")
                throw _owner.Failure!;
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
        public DateTimeOffset Now => SaveAsCopyCreatorTests.Now;
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
