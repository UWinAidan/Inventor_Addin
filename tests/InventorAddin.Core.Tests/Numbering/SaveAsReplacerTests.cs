using InventorAddin.Core.Logging;
using InventorAddin.Core.Models;
using InventorAddin.Core.Numbering;
using InventorAddin.Core.PartProperties;

namespace InventorAddin.Core.Tests.Numbering;

public sealed class SaveAsReplacerTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 10, 14, 3, 0, TimeSpan.Zero);

    private readonly string _root;
    private readonly string _shared;
    private readonly string _local;
    private readonly string _cad;
    private readonly string _component;
    private readonly string _subAssembly;
    private readonly RecordingLog _log = new();
    private readonly FakeTarget _target = new();

    public SaveAsReplacerTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "InventorAddinTests", Guid.NewGuid().ToString("N"));
        _shared = Path.Combine(_root, "nas");
        _local = Path.Combine(_root, "appdata", "register");
        _cad = Path.Combine(_root, "cad");
        Directory.CreateDirectory(_shared);
        Directory.CreateDirectory(_cad);
        _component = Path.Combine(_cad, "test_bracket,AWB-10001.ipt");
        File.WriteAllText(_component, "original");
        _subAssembly = Path.Combine(_cad, "test_frame,AWB-10002.iam");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private string SharedPath => Path.Combine(_shared, RegisterStore.SharedFileName);

    private string CopyPath => Path.Combine(_cad, "TEST_BRACKET_COPY,AWB-10001.ipt");

    private RegisterStore Store() => new(_shared, _local, new FakeEnvironment(), _log, TimeSpan.FromMilliseconds(200));

    private ReplaceSelection TopLevel(
        DocumentKind kind = DocumentKind.Part,
        EditBlock component = EditBlock.None,
        EditBlock assembly = EditBlock.None) =>
        new(_component, kind, component, assembly);

    private ReplaceSelection Nested(EditBlock subAssembly = EditBlock.None, string? subFile = null, string? occurrence = "Frame:1") =>
        new(_component, DocumentKind.Part, EditBlock.None, subAssembly,
            SubAssemblyFullFileName: subFile ?? _subAssembly, SubAssemblyOccurrenceName: occurrence);

    private SaveAsReplacer Replacer(ReplaceSelection selection) =>
        new(Store(), _target, selection, "Test Designer", _log);

    private SaveAsCopyRequest Request() => new(_component, DocumentKind.Part, "TEST BRACKET COPY", "M");

    private static List<RegisterEntry> Read(string path) => RegisterCsv.Read(File.ReadAllText(path)).Entries.ToList();

    // Check

    [Fact]
    public void Check_ASavedPartOrAssembly_MayRun()
    {
        Assert.Null(SaveAsReplacer.Check(TopLevel()));
        Assert.Null(SaveAsReplacer.Check(TopLevel(DocumentKind.SheetMetalPart)));
        Assert.Null(SaveAsReplacer.Check(TopLevel(DocumentKind.Assembly)));
        Assert.Null(SaveAsReplacer.Check(TopLevel(DocumentKind.WeldmentAssembly)));
        Assert.Null(SaveAsReplacer.Check(Nested()));
    }

    [Fact]
    public void Check_VirtualComponent_Stops()
    {
        var selection = new ReplaceSelection("", DocumentKind.Unknown, EditBlock.None, EditBlock.None, IsVirtual: true);

        Assert.Equal(SaveAsReplacer.VirtualComponentMessage, SaveAsReplacer.Check(selection));
    }

    [Fact]
    public void Check_SuppressedComponent_Stops()
    {
        var selection = new ReplaceSelection("", DocumentKind.Unknown, EditBlock.None, EditBlock.None, IsSuppressed: true);

        Assert.Equal(SaveAsReplacer.SuppressedComponentMessage, SaveAsReplacer.Check(selection));
        Assert.Equal(SaveAsReplacer.SuppressedComponentMessage, SaveAsReplacer.Check(TopLevel() with { IsSuppressed = true }));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Check_UnsavedComponent_Stops(string file)
    {
        var selection = new ReplaceSelection(file, DocumentKind.Part, EditBlock.None, EditBlock.None);

        Assert.Equal(SaveAsReplacer.UnsavedComponentMessage, SaveAsReplacer.Check(selection));
    }

    [Theory]
    [InlineData(DocumentKind.Drawing)]
    [InlineData(DocumentKind.Unknown)]
    public void Check_WrongKind_Stops(DocumentKind kind)
    {
        Assert.Equal(SaveAsReplacer.WrongComponentMessage, SaveAsReplacer.Check(TopLevel(kind)));
    }

    [Theory]
    [InlineData(EditBlock.ReadOnlyFile, SaveAsReplacer.ReadOnlyComponentMessage)]
    [InlineData(EditBlock.NotModifiable, SaveAsReplacer.NotModifiableComponentMessage)]
    public void Check_ReadOnlyOrLibraryComponent_Stops(EditBlock block, string message)
    {
        Assert.Equal(message, SaveAsReplacer.Check(TopLevel(component: block)));
        Assert.Equal(message, SaveAsReplacer.Check(Nested() with { ComponentBlock = block }));
    }

    [Theory]
    [InlineData(EditBlock.ReadOnlyFile, SaveAsReplacer.ReadOnlyAssemblyMessage)]
    [InlineData(EditBlock.NotModifiable, SaveAsReplacer.NotModifiableAssemblyMessage)]
    public void Check_TopLevel_AssemblyThatCannotBeEdited_Stops(EditBlock block, string message)
    {
        Assert.Equal(message, SaveAsReplacer.Check(TopLevel(assembly: block)));
    }

    [Fact]
    public void Check_ReadOnlySubAssembly_StopsNamingIt()
    {
        Assert.Equal(
            SaveAsReplacer.ReadOnlySubAssemblyMessage("test_frame,AWB-10002.iam (Frame:1)"),
            SaveAsReplacer.Check(Nested(EditBlock.ReadOnlyFile)));
    }

    [Fact]
    public void Check_NotModifiableSubAssembly_StopsNamingIt()
    {
        Assert.Equal(
            SaveAsReplacer.NotModifiableSubAssemblyMessage("test_frame,AWB-10002.iam (Frame:1)"),
            SaveAsReplacer.Check(Nested(EditBlock.NotModifiable)));
    }

    [Fact]
    public void Check_UnsavedSubAssembly_Stops()
    {
        Assert.Equal(
            SaveAsReplacer.UnsavedSubAssemblyMessage("Frame:1"),
            SaveAsReplacer.Check(Nested(subFile: "")));
    }

    [Fact]
    public void Check_ComponentProblemComesBeforeTheSubAssemblys()
    {
        var selection = Nested(EditBlock.ReadOnlyFile) with { ComponentBlock = EditBlock.ReadOnlyFile };

        Assert.Equal(SaveAsReplacer.ReadOnlyComponentMessage, SaveAsReplacer.Check(selection));
    }

    // Naming and the warning

    [Theory]
    [InlineData(@"C:\Work\frame,AWB-10002.iam", "Frame:1", "frame,AWB-10002.iam (Frame:1)")]
    [InlineData("/home/user/cad/frame.iam", null, "frame.iam")]
    [InlineData("/home/user/cad/frame.iam", "  ", "frame.iam")]
    [InlineData("", "Frame:1", "Frame:1")]
    [InlineData("", null, "(unnamed)")]
    public void SubAssemblyName_IsItsFileThenItsOccurrence(string file, string? occurrence, string expected)
    {
        var selection = new ReplaceSelection(_component, DocumentKind.Part, EditBlock.None, EditBlock.None, false, file, occurrence);

        Assert.Equal(expected, SaveAsReplacer.SubAssemblyName(selection));
    }

    [Fact]
    public void TopLevel_HasNoSubAssemblyName_AndNoWarning()
    {
        Assert.False(TopLevel().InSubAssembly);
        Assert.Equal(string.Empty, SaveAsReplacer.SubAssemblyName(TopLevel()));
        Assert.Null(SaveAsReplacer.SubAssemblyWarning(TopLevel()));
    }

    [Fact]
    public void Nested_WarningNamesTheSubAssembly_SaysItIsSaved_AndUndoCannotPutItBack()
    {
        var warning = SaveAsReplacer.SubAssemblyWarning(Nested());

        Assert.True(Nested().InSubAssembly);
        Assert.NotNull(warning);
        Assert.Contains("test_frame,AWB-10002.iam (Frame:1)", warning, StringComparison.Ordinal);
        Assert.Contains("is saved", warning, StringComparison.Ordinal);
        Assert.Contains("every assembly that uses", warning, StringComparison.Ordinal);
        Assert.Contains("Undo cannot put it back", warning, StringComparison.Ordinal);
    }

    // Replace: top level

    [Fact]
    public void Replace_TopLevel_CopiesAsSaveAsCopy_ClosesTheCopy_ThenReplacesTheOccurrence_AndSavesNothingElse()
    {
        var result = Replacer(TopLevel()).Replace(Request());

        Assert.Equal("AWB-10001", result.PartNumber);
        Assert.Equal(CopyPath, result.FullPath);
        Assert.Equal(
            [$"copy {CopyPath}", $"open {CopyPath}", "write", "save", "close", $"replace {CopyPath}"],
            _target.Calls);
        Assert.Equal([10001], Read(SharedPath).Select(e => e.Number.Value));
        Assert.Equal("original", File.ReadAllText(_component));
        Assert.True(File.Exists(CopyPath));
    }

    [Fact]
    public void Replace_CopyThatWillNotClose_IsLogged_AndTheReplaceStillRuns()
    {
        _target.CloseCopyFailure = new InvalidOperationException("Cannot close.");

        var result = Replacer(TopLevel()).Replace(Request());

        Assert.Equal("AWB-10001", result.PartNumber);
        Assert.Contains($"replace {CopyPath}", _target.Calls);
        Assert.Contains(_log.Warnings, w => w.Contains("Cannot close.", StringComparison.Ordinal));
    }

    [Fact]
    public void Replace_FailsAfterTheCopy_KeepsTheCopyAndItsNumber_AndSaysToReplaceByHand()
    {
        var failure = new InvalidOperationException("Inventor said no.");
        _target.ReplaceFailure = failure;

        var ex = Assert.Throws<NewPartException>(() => Replacer(TopLevel()).Replace(Request()));

        Assert.Equal(SaveAsReplacer.ReplaceFailedMessage("AWB-10001", CopyPath, "Inventor said no."), ex.Message);
        Assert.Equal("AWB-10001", ex.AssignedNumber);
        Assert.Same(failure, ex.InnerException);
        Assert.True(File.Exists(CopyPath));
        Assert.Equal([10001], Read(SharedPath).Select(e => e.Number.Value));
        Assert.Contains(_log.Errors, e => ReferenceEquals(e.Ex, failure));
        Assert.DoesNotContain("save sub-assembly", _target.Calls);
    }

    [Fact]
    public void Replace_CopyFails_IsSaveAsCopysFailure_AndNothingIsReplaced()
    {
        _target.FailAt = "write";
        _target.Failure = new InvalidOperationException("Inventor said no.");

        var ex = Assert.Throws<NewPartException>(() => Replacer(TopLevel()).Replace(Request()));

        Assert.Equal(SaveAsCopyCreator.FailedAfterAssignMessage("AWB-10001", "Inventor said no."), ex.Message);
        Assert.False(File.Exists(CopyPath));
        Assert.DoesNotContain(_target.Calls, c => c.StartsWith("replace", StringComparison.Ordinal));
    }

    [Fact]
    public void Replace_SelectionThatFailsCheck_StopsBeforeAnything()
    {
        var ex = Assert.Throws<NewPartException>(
            () => Replacer(TopLevel(component: EditBlock.ReadOnlyFile)).Replace(Request()));

        Assert.Equal(SaveAsReplacer.ReadOnlyComponentMessage, ex.Message);
        Assert.Null(ex.AssignedNumber);
        Assert.Empty(_target.Calls);
        Assert.False(File.Exists(SharedPath) && Read(SharedPath).Count > 0);
    }

    // Replace: inside a sub-assembly

    [Fact]
    public void Replace_Nested_ReplacesThenSavesTheSubAssembly()
    {
        var result = Replacer(Nested()).Replace(Request());

        Assert.Equal("AWB-10001", result.PartNumber);
        Assert.Equal(
            [$"copy {CopyPath}", $"open {CopyPath}", "write", "save", "close", $"replace {CopyPath}", "save sub-assembly"],
            _target.Calls);
    }

    [Fact]
    public void Replace_Nested_ReadOnlySubAssembly_StopsBeforeTheCopy()
    {
        var ex = Assert.Throws<NewPartException>(() => Replacer(Nested(EditBlock.ReadOnlyFile)).Replace(Request()));

        Assert.Equal(SaveAsReplacer.ReadOnlySubAssemblyMessage("test_frame,AWB-10002.iam (Frame:1)"), ex.Message);
        Assert.Empty(_target.Calls);
        Assert.False(File.Exists(CopyPath));
    }

    [Fact]
    public void Replace_Nested_SubAssemblyWillNotSave_SaysSoNamingIt()
    {
        var failure = new InvalidOperationException("Disk full.");
        _target.SaveSubAssemblyFailure = failure;

        var ex = Assert.Throws<NewPartException>(() => Replacer(Nested()).Replace(Request()));

        Assert.Equal(
            SaveAsReplacer.SubAssemblySaveFailedMessage("AWB-10001", "test_frame,AWB-10002.iam (Frame:1)", "Disk full."),
            ex.Message);
        Assert.Equal("AWB-10001", ex.AssignedNumber);
        Assert.Same(failure, ex.InnerException);
        Assert.True(File.Exists(CopyPath));
    }

    [Fact]
    public void Replace_Nested_ReplaceFails_DoesNotSaveTheSubAssembly()
    {
        _target.ReplaceFailure = new InvalidOperationException("Inventor said no.");

        Assert.Throws<NewPartException>(() => Replacer(Nested()).Replace(Request()));

        Assert.DoesNotContain("save sub-assembly", _target.Calls);
    }

    [Fact]
    public void Constructor_RejectsNulls()
    {
        Assert.Throws<ArgumentNullException>(() => new SaveAsReplacer(null!, _target, TopLevel(), null));
        Assert.Throws<ArgumentNullException>(() => new SaveAsReplacer(Store(), null!, TopLevel(), null));
        Assert.Throws<ArgumentNullException>(() => new SaveAsReplacer(Store(), _target, null!, null));
        Assert.Throws<ArgumentNullException>(() => Replacer(TopLevel()).Replace(null!));
        Assert.Throws<ArgumentNullException>(() => SaveAsReplacer.Check(null!));
    }

    private sealed class FakeTarget : ISaveAsReplaceTarget
    {
        public List<string> Calls { get; } = [];
        public string? FailAt { get; set; }
        public Exception? Failure { get; set; }
        public Exception? CloseCopyFailure { get; set; }
        public Exception? ReplaceFailure { get; set; }
        public Exception? SaveSubAssemblyFailure { get; set; }

        public void SaveCopyAs(string fullPath)
        {
            Calls.Add($"copy {fullPath}");
            File.WriteAllText(fullPath, "copy");
        }

        public ICopiedDocument OpenCopy(string fullPath)
        {
            Calls.Add($"open {fullPath}");
            return new FakeCopy(this);
        }

        public void CloseCopy()
        {
            Calls.Add("close");
            if (CloseCopyFailure is not null)
                throw CloseCopyFailure;
        }

        public void ReplaceOccurrence(string copyFullPath)
        {
            Calls.Add($"replace {copyFullPath}");
            if (ReplaceFailure is not null)
                throw ReplaceFailure;
        }

        public void SaveSubAssembly()
        {
            Calls.Add("save sub-assembly");
            if (SaveSubAssemblyFailure is not null)
                throw SaveSubAssemblyFailure;
        }
    }

    private sealed class FakeCopy : ICopiedDocument
    {
        private readonly FakeTarget _owner;

        public FakeCopy(FakeTarget owner) => _owner = owner;

        public void WriteProperties(IReadOnlyList<PropertyWrite> writes)
        {
            _owner.Calls.Add("write");
            if (_owner.FailAt == "write")
                throw _owner.Failure!;
        }

        public void Save() => _owner.Calls.Add("save");

        public void CloseWithoutSaving() => _owner.Calls.Add("close without saving");
    }

    private sealed class FakeEnvironment : IRegisterEnvironment
    {
        public DateTimeOffset Now => SaveAsReplacerTests.Now;
        public string AssignedBy => "testuser";
        public string ComputerName => "TESTPC";
        public bool ProbeFolder(string folder, TimeSpan timeout) => Directory.Exists(folder);
    }

    private sealed class RecordingLog : ILog
    {
        public List<(string Message, Exception? Ex)> Errors { get; } = [];
        public List<string> Warnings { get; } = [];
        public void Info(string message) { }
        public void Warn(string message) => Warnings.Add(message);
        public void Error(string message, Exception? ex = null) => Errors.Add((message, ex));
    }
}
