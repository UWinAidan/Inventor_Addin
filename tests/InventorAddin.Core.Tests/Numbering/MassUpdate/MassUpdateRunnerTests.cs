using InventorAddin.Core.Logging;
using InventorAddin.Core.Models;
using InventorAddin.Core.Numbering;
using InventorAddin.Core.Numbering.MassUpdate;
using InventorAddin.Core.PartProperties;
using InventorAddin.Core.ViewModels;

namespace InventorAddin.Core.Tests.Numbering.MassUpdate;

public sealed class MassUpdateRunnerTests : IDisposable
{
    private const string User = "testlogin";
    private const string Designer = "Test Designer";
    private static readonly DateTimeOffset Now = new(2026, 10, 10, 14, 3, 0, TimeSpan.Zero);

    private readonly string _root;
    private readonly string _shared;
    private readonly string _local;
    private readonly FakeEnvironment _environment = new();
    private readonly RecordingLog _log = new();
    private readonly FakeFiles _files = new();

    public MassUpdateRunnerTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "InventorAddinTests", Guid.NewGuid().ToString("N"));
        _shared = Path.Combine(_root, "nas");
        _local = Path.Combine(_root, "appdata", "register");
        Directory.CreateDirectory(_shared);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private string SharedPath => Path.Combine(_shared, RegisterStore.SharedFileName);
    private string LockPath => Path.Combine(_shared, RegisterStore.LockFileName);
    private string PendingPath => Path.Combine(_local, RegisterStore.PendingFileName);

    private RegisterStore Store() => new(_shared, _local, _environment, _log, TimeSpan.FromMilliseconds(200));

    private MassUpdateRunner Runner() => new(Store(), _files, _log);

    private void WriteShared(params int[] values) =>
        System.IO.File.WriteAllText(SharedPath, RegisterCsv.Write(values.Select(v => new RegisterEntry(
            new PartNumber(v), "other part", "", DocumentKind.Part, $@"C:\Other\AWB-{v},other_part.ipt",
            Now.AddDays(-1), "otheruser", "OTHERPC"))));

    private List<RegisterEntry> Shared() => RegisterCsv.Read(System.IO.File.ReadAllText(SharedPath)).Entries.ToList();

    private static ScannedFile File(string name, string partNumber = "", string designer = "Alex Example", string detailer = "Alex Example") =>
        new($@"C:\Work\{name}", partNumber, designer, detailer, "M", DocumentKind.Part);

    private MassUpdatePlan Plan(params (ScannedFile File, MassUpdateActions Ticked)[] files)
    {
        var register = Store().ReadLocal();
        if (System.IO.File.Exists(SharedPath))
            register = new PartRegister(Shared());
        return MassUpdatePlan.Build(files.Select(f => (ScannedFileState.For(f.File, register, [User]), f.Ticked)), Designer);
    }

    // Apply

    [Fact]
    public void RegisterOnly_AddsTheRow_AndNeverOpensTheFile()
    {
        WriteShared(10001);
        var plan = Plan((File("old.ipt", "AWB-10900"), MassUpdateActions.Register));

        var result = Runner().Apply(plan);

        Assert.Equal([new FileUpdateResult(@"C:\Work\old.ipt")], result.Results);
        Assert.Null(result.StopMessage);
        Assert.Empty(_files.Calls);
        var entry = Shared()[^1];
        Assert.Equal(new PartNumber(10900), entry.Number);
        Assert.Equal("old", entry.Name);
        Assert.Equal(@"C:\Work\old.ipt", entry.FilePath);
    }

    [Fact]
    public void Assign_OpensFirst_ThenAssigns_WritesThePartNumber_SavesAndCloses()
    {
        WriteShared(10001);
        var plan = Plan((File("old.ipt"), MassUpdateActions.Assign));
        _files.OnOpen = _ => Assert.Single(Shared());

        var result = Runner().Apply(plan);

        Assert.Equal([new FileUpdateResult(@"C:\Work\old.ipt")], result.Results);
        Assert.Equal([@"open C:\Work\old.ipt", "write", "save", "close"], _files.Calls);
        Assert.Equal([PropertyWrite.SetText(PartPropertyStorage.PartNumber, "AWB-10002")], _files.Documents.Single().Writes);
        Assert.Equal(new PartNumber(10002), Shared()[^1].Number);
        Assert.Equal(@"C:\Work\old.ipt", Shared()[^1].FilePath);
    }

    [Fact]
    public void ReplaceNames_WritesDesignerAndDetailer_AndAddsNoRow()
    {
        WriteShared(10001);
        var plan = Plan((File("a.ipt", "AWB-10001", User, User), MassUpdateActions.ReplaceNames));

        var result = Runner().Apply(plan);

        Assert.Equal([new FileUpdateResult(@"C:\Work\a.ipt")], result.Results);
        Assert.Equal(
            [
                PropertyWrite.SetText(PartPropertyStorage.Designer, Designer),
                PropertyWrite.SetText(PartPropertyStorage.Detailer, Designer),
            ],
            _files.Documents.Single().Writes);
        Assert.Single(Shared());
    }

    [Fact]
    public void AssignAndNames_AreOneWriteBeforeOneSave()
    {
        WriteShared(10001);
        var plan = Plan((File("a.ipt", "", User), MassUpdateActions.Assign | MassUpdateActions.ReplaceNames));

        Runner().Apply(plan);

        Assert.Equal([@"open C:\Work\a.ipt", "write", "save", "close"], _files.Calls);
        Assert.Equal(
            [
                PropertyWrite.SetText(PartPropertyStorage.PartNumber, "AWB-10002"),
                PropertyWrite.SetText(PartPropertyStorage.Designer, Designer),
            ],
            _files.Documents.Single().Writes);
    }

    [Fact]
    public void RegisterExistingRunsBeforeAssign_SoAnAssignedNumberNeverTakesACarriedOne()
    {
        WriteShared(10001);
        var plan = Plan(
            (File("a.ipt"), MassUpdateActions.Assign),
            (File("b.ipt", "AWB-10002"), MassUpdateActions.Register));

        var result = Runner().Apply(plan);

        Assert.Equal([10001, 10002, 10003], Shared().Select(e => e.Number.Value));
        Assert.Equal(@"C:\Work\b.ipt", Shared()[1].FilePath);
        Assert.Equal(@"C:\Work\a.ipt", Shared()[2].FilePath);
        Assert.Equal([@"C:\Work\a.ipt", @"C:\Work\b.ipt"], result.Results.Select(r => r.FullPath));
        Assert.All(result.Results, r => Assert.Null(r.Error));
    }

    [Fact]
    public void OneFileFailing_DoesNotStopTheOthers_AndIsLogged()
    {
        WriteShared(10001);
        var plan = Plan(
            (File("a.ipt", "AWB-10001", User), MassUpdateActions.ReplaceNames),
            (File("b.ipt", "AWB-10001", User), MassUpdateActions.ReplaceNames),
            (File("c.ipt", "AWB-10001", User), MassUpdateActions.ReplaceNames));
        _files.FailSaveOn = @"C:\Work\b.ipt";

        var result = Runner().Apply(plan);

        Assert.Null(result.Results[0].Error);
        Assert.Equal("The file is locked.", result.Results[1].Error);
        Assert.Null(result.Results[2].Error);
        Assert.Equal(3, _files.Documents.Count(d => d.Closed));
        Assert.Contains(_log.Errors, e => e.Contains(@"C:\Work\b.ipt", StringComparison.Ordinal));
        Assert.Equal("2 files updated, 1 not.", MassUpdateRunner.SummaryText(result));
    }

    [Fact]
    public void AFileThatCannotBeOpened_CostsNoNumber()
    {
        WriteShared(10001);
        var plan = Plan((File("a.ipt"), MassUpdateActions.Assign), (File("b.ipt"), MassUpdateActions.Assign));
        _files.FailOpenOn = @"C:\Work\a.ipt";

        var result = Runner().Apply(plan);

        Assert.Equal(ScannedFileState.OpenInInventorText, result.Results[0].Error);
        Assert.Null(result.Results[1].Error);
        Assert.Equal([10001, 10002], Shared().Select(e => e.Number.Value));
        Assert.Equal(@"C:\Work\b.ipt", Shared()[^1].FilePath);
    }

    [Fact]
    public void ASaveThatFailsAfterAssigning_SaysTheNumberStays()
    {
        WriteShared(10001);
        var plan = Plan((File("a.ipt"), MassUpdateActions.Assign));
        _files.FailSaveOn = @"C:\Work\a.ipt";

        var result = Runner().Apply(plan);

        Assert.Equal(MassUpdateRunner.FailedAfterAssignText(new PartNumber(10002), "The file is locked."), result.Results[0].Error);
        Assert.Equal(new PartNumber(10002), Shared()[^1].Number);
        Assert.True(_files.Documents.Single().Closed);
    }

    [Fact]
    public void ABusyRegister_StopsBeforeAnyFileChanges()
    {
        WriteShared(10001);
        var plan = Plan((File("a.ipt"), MassUpdateActions.Assign), (File("b.ipt", "AWB-10001", User), MassUpdateActions.ReplaceNames));
        System.IO.File.WriteAllText(LockPath, "other PC");
        System.IO.File.SetLastWriteTimeUtc(LockPath, Now.UtcDateTime);

        var result = Runner().Apply(plan);

        Assert.Empty(result.Results);
        Assert.Contains("Another PC is writing the part register", result.StopMessage, StringComparison.Ordinal);
        Assert.Empty(_files.Calls);
    }

    [Fact]
    public void AnUnreadableRegister_StopsBeforeAnyFileChanges()
    {
        WriteShared(10001);
        var plan = Plan((File("b.ipt", "AWB-10001", User), MassUpdateActions.ReplaceNames));
        System.IO.File.AppendAllText(SharedPath, "AWB-10002,x,,Part,C:\\x.ipt,not a date,u,c\r\n");

        var result = Runner().Apply(plan);

        Assert.Empty(result.Results);
        Assert.Contains("cannot be read in full", result.StopMessage, StringComparison.Ordinal);
        Assert.Empty(_files.Calls);
    }

    [Fact]
    public void ARegisterFailurePartWay_StopsTheRest_AndSaysWhichWereNotChanged()
    {
        WriteShared(10001);
        var plan = Plan(
            (File("a.ipt", "AWB-10001", User), MassUpdateActions.ReplaceNames),
            (File("b.ipt"), MassUpdateActions.Assign),
            (File("c.ipt", "AWB-10001", User), MassUpdateActions.ReplaceNames));
        _files.OnOpen = path =>
        {
            if (path.EndsWith("b.ipt", StringComparison.Ordinal))
            {
                System.IO.File.WriteAllText(LockPath, "other PC");
                System.IO.File.SetLastWriteTimeUtc(LockPath, Now.UtcDateTime);
            }
        };

        var result = Runner().Apply(plan);

        Assert.Null(result.Results[0].Error);
        Assert.Contains("Another PC is writing the part register", result.Results[1].Error, StringComparison.Ordinal);
        Assert.Equal(MassUpdateRunner.StoppedText, result.Results[2].Error);
        Assert.NotNull(result.StopMessage);
        Assert.DoesNotContain(@"open C:\Work\c.ipt", _files.Calls);
        Assert.True(_files.Documents.Single(d => d.Path.EndsWith("b.ipt", StringComparison.Ordinal)).Closed);
        Assert.Null(_files.Documents.Single(d => d.Path.EndsWith("b.ipt", StringComparison.Ordinal)).Writes);
    }

    [Fact]
    public void ANumberRegisteredMeanwhile_IsNotAddedAgain_AndTheRowSaysSo()
    {
        WriteShared(10001);
        var plan = Plan((File("a.ipt", "AWB-10900", User), MassUpdateActions.Register | MassUpdateActions.ReplaceNames));
        WriteShared(10001, 10900);

        var result = Runner().Apply(plan);

        Assert.Equal(
            $"{MassUpdateRunner.AlreadyRegisteredText(new PartNumber(10900))} {MassUpdateRunner.NamesReplacedText}",
            result.Results[0].Error);
        Assert.Equal(2, Shared().Count);
        Assert.NotNull(_files.Documents.Single().Writes);
    }

    [Fact]
    public void Offline_NumbersGoToPending_AndTheNoticeSaysSo()
    {
        _environment.Reachable = false;
        var plan = Plan((File("a.ipt"), MassUpdateActions.Assign), (File("b.ipt", "AWB-10900"), MassUpdateActions.Register));

        var result = Runner().Apply(plan);

        Assert.All(result.Results, r => Assert.Null(r.Error));
        Assert.Equal(MassUpdateRunner.OfflineNotice(2), result.Notice);
        Assert.Equal([10900, 10901], RegisterCsv.Read(System.IO.File.ReadAllText(PendingPath)).Entries.Select(e => e.Number.Value));
    }

    [Fact]
    public void DuplicatesFoundBySyncAreInTheNotice()
    {
        WriteShared(10001, 10002);
        Directory.CreateDirectory(_local);
        System.IO.File.WriteAllText(PendingPath, RegisterCsv.Write([new RegisterEntry(
            new PartNumber(10002), "offline part", "", DocumentKind.Part, @"C:\Work\offline.ipt", Now, "testuser", "TESTPC")]));
        var plan = Plan((File("a.ipt", "AWB-10001", User), MassUpdateActions.ReplaceNames));

        var result = Runner().Apply(plan);

        Assert.StartsWith(PartPropertiesViewModel.DuplicatesHeading, result.Notice, StringComparison.Ordinal);
        Assert.Contains("AWB-10002", result.Notice, StringComparison.Ordinal);
    }

    [Fact]
    public void ProgressCountsTheFiles()
    {
        WriteShared(10001);
        var plan = Plan((File("a.ipt", "AWB-10901"), MassUpdateActions.Register), (File("b.ipt"), MassUpdateActions.Assign));
        var seen = new List<(int, int)>();

        Runner().Apply(plan, (n, total) => seen.Add((n, total)));

        Assert.Equal([(1, 2), (2, 2)], seen);
    }

    [Fact]
    public void AnEmptyPlanDoesNothing()
    {
        var result = Runner().Apply(new MassUpdatePlan([], []));

        Assert.Empty(result.Results);
        Assert.Null(result.StopMessage);
        Assert.False(System.IO.File.Exists(SharedPath));
    }

    [Theory]
    [InlineData(3, 0, "3 files updated.")]
    [InlineData(1, 0, "1 file updated.")]
    [InlineData(1, 2, "1 file updated, 2 not.")]
    public void Summary(int done, int failed, string expected)
    {
        var results = Enumerable.Range(0, done).Select(i => new FileUpdateResult($"d{i}"))
            .Concat(Enumerable.Range(0, failed).Select(i => new FileUpdateResult($"f{i}", "x")))
            .ToList();

        Assert.Equal(expected, MassUpdateRunner.SummaryText(new MassUpdateApplyResult(results, null, null)));
    }

    // Scan

    [Fact]
    public void Scan_SyncsThenReadsEachListedFileInOrder_WithProgress()
    {
        WriteShared(10001);
        var seen = new List<(int, int)>();
        var scanner = new MassUpdateScanner(Store(), _files, _log, _ => [@"C:\Work\a.ipt", @"C:\Work\b.iam"]);

        var result = scanner.Scan(@"C:\Work", (n, total) => seen.Add((n, total)));

        Assert.Equal([@"C:\Work\a.ipt", @"C:\Work\b.iam"], result.Files.Select(f => f.FullPath));
        Assert.Equal([@"read C:\Work\a.ipt", @"read C:\Work\b.iam"], _files.Calls);
        Assert.Equal([(0, 0), (1, 2), (2, 2)], seen);
        Assert.Equal(2, result.Total);
        Assert.False(result.Cancelled);
        Assert.True(result.Register.Contains(new PartNumber(10001)));
        Assert.Null(result.Notice);
    }

    [Fact]
    public void Scan_StopsBetweenFilesWhenCancelled()
    {
        var reads = 0;
        _files.OnRead = _ => reads++;
        var scanner = new MassUpdateScanner(Store(), _files, _log, _ => ["a.ipt", "b.ipt", "c.ipt", "d.ipt"]);

        var result = scanner.Scan("C:\\Work", isCancelled: () => reads >= 2);

        Assert.Equal(2, result.Files.Count);
        Assert.Equal(4, result.Total);
        Assert.True(result.Cancelled);
    }

    [Fact]
    public void Scan_ACancelAfterTheLastFileIsNotACancel()
    {
        var scanner = new MassUpdateScanner(Store(), _files, _log, _ => ["a.ipt"]);

        var result = scanner.Scan("C:\\Work", isCancelled: () => true);

        Assert.Single(result.Files);
        Assert.False(result.Cancelled);
    }

    [Fact]
    public void Scan_AMissingFolderStopsWithAMessage()
    {
        var missing = Path.Combine(_root, "missing");
        var scanner = new MassUpdateScanner(Store(), _files, _log);

        var ex = Assert.Throws<MassUpdateException>(() => scanner.Scan(missing));

        Assert.Equal(MassUpdateScanner.FolderNotFoundMessage(missing), ex.Message);
    }

    [Fact]
    public void Scan_ListsWithScanFolderByDefault()
    {
        var cad = Path.Combine(_root, "cad");
        Directory.CreateDirectory(Path.Combine(cad, "OldVersions"));
        System.IO.File.WriteAllText(Path.Combine(cad, "a.ipt"), "");
        System.IO.File.WriteAllText(Path.Combine(cad, "OldVersions", "a.0001.ipt"), "");

        var result = new MassUpdateScanner(Store(), _files, _log).Scan(cad);

        Assert.Equal([Path.Combine(cad, "a.ipt")], result.Files.Select(f => f.FullPath));
    }

    [Fact]
    public void Scan_AListingThatTakesTooLongStops()
    {
        using var release = new ManualResetEventSlim();
        var scanner = new MassUpdateScanner(Store(), _files, _log, _ => { release.Wait(); return []; }, TimeSpan.FromMilliseconds(50));

        var ex = Assert.Throws<MassUpdateException>(() => scanner.Scan(@"C:\Work"));
        release.Set();

        Assert.Contains("took longer than", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Scan_AListingThatFailsStopsWithTheReason()
    {
        var scanner = new MassUpdateScanner(Store(), _files, _log, _ => throw new UnauthorizedAccessException("Access is denied."));

        var ex = Assert.Throws<MassUpdateException>(() => scanner.Scan(@"C:\Work"));

        Assert.Equal(MassUpdateScanner.ListingFailedMessage(@"C:\Work", "Access is denied."), ex.Message);
    }

    [Fact]
    public void Scan_ARegisterThatCannotBeSynced_StillScans_WithALocalCopyAndANotice()
    {
        WriteShared(10001);
        System.IO.File.WriteAllText(LockPath, "other PC");
        System.IO.File.SetLastWriteTimeUtc(LockPath, Now.UtcDateTime);
        var scanner = new MassUpdateScanner(Store(), _files, _log, _ => ["a.ipt"]);

        var result = scanner.Scan(@"C:\Work");

        Assert.Single(result.Files);
        Assert.StartsWith("The register could not be synced", result.Notice, StringComparison.Ordinal);
    }

    [Fact]
    public void Scan_Offline_SaysTheLocalCopyIsUsed()
    {
        _environment.Reachable = false;
        var scanner = new MassUpdateScanner(Store(), _files, _log, _ => []);

        var result = scanner.Scan(@"C:\Work");

        Assert.Equal(MassUpdateScanner.OfflineNotice, result.Notice);
        Assert.Empty(result.Files);
    }

    [Fact]
    public void Scan_AReaderThatThrows_StillGivesTheFileARow()
    {
        _files.OnRead = _ => throw new InvalidOperationException("Boom.");
        var scanner = new MassUpdateScanner(Store(), _files, _log, _ => ["a.ipt"]);

        var file = Assert.Single(scanner.Scan(@"C:\Work").Files);

        Assert.Equal("Boom.", file.ReadError);
    }

    // Fakes

    private sealed class FakeFiles : IMassUpdateFiles
    {
        public List<string> Calls { get; } = [];
        public List<FakeDocument> Documents { get; } = [];
        public string? FailOpenOn { get; set; }
        public string? FailSaveOn { get; set; }
        public Action<string>? OnOpen { get; set; }
        public Action<string>? OnRead { get; set; }

        public ScannedFile Read(string fullPath)
        {
            Calls.Add($"read {fullPath}");
            OnRead?.Invoke(fullPath);
            return new ScannedFile(fullPath, "", "", "", "", DocumentKind.Part);
        }

        public IMassUpdateDocument Open(string fullPath)
        {
            if (fullPath == FailOpenOn)
                throw new InvalidOperationException(ScannedFileState.OpenInInventorText);
            Calls.Add($"open {fullPath}");
            OnOpen?.Invoke(fullPath);
            var document = new FakeDocument(this, fullPath);
            Documents.Add(document);
            return document;
        }
    }

    private sealed class FakeDocument : IMassUpdateDocument
    {
        private readonly FakeFiles _owner;

        public FakeDocument(FakeFiles owner, string path)
        {
            _owner = owner;
            Path = path;
        }

        public string Path { get; }
        public IReadOnlyList<PropertyWrite>? Writes { get; private set; }
        public bool Closed { get; private set; }

        public void WriteProperties(IReadOnlyList<PropertyWrite> writes)
        {
            _owner.Calls.Add("write");
            Writes = writes;
        }

        public void Save()
        {
            _owner.Calls.Add("save");
            if (Path == _owner.FailSaveOn)
                throw new InvalidOperationException("The file is locked.");
        }

        public void CloseWithoutSaving()
        {
            _owner.Calls.Add("close");
            Closed = true;
        }
    }

    private sealed class FakeEnvironment : IRegisterEnvironment
    {
        public bool Reachable { get; set; } = true;
        public DateTimeOffset Now => MassUpdateRunnerTests.Now;
        public string AssignedBy => "testuser";
        public string ComputerName => "TESTPC";
        public bool ProbeFolder(string folder, TimeSpan timeout) => Reachable && Directory.Exists(folder);
    }

    private sealed class RecordingLog : ILog
    {
        public List<string> Errors { get; } = [];
        public void Info(string message) { }
        public void Warn(string message) { }
        public void Error(string message, Exception? ex = null) => Errors.Add(message);
    }
}
