using InventorAddin.Core.Logging;
using InventorAddin.Core.Models;
using InventorAddin.Core.Numbering;

namespace InventorAddin.Core.Tests.Numbering;

public sealed class RegisterStoreTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 10, 14, 3, 0, TimeSpan.Zero);

    private readonly string _root;
    private readonly string _shared;
    private readonly string _local;
    private readonly FakeEnvironment _environment = new();
    private readonly RecordingLog _log = new();

    public RegisterStoreTests()
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
    private string LocalPath => Path.Combine(_local, RegisterStore.LocalFileName);
    private string PendingPath => Path.Combine(_local, RegisterStore.PendingFileName);

    private RegisterStore Store(TimeSpan? lockWait = null) =>
        new(_shared, _local, _environment, _log, lockWait ?? TimeSpan.FromMilliseconds(300));

    private static RegisterRequest Request(string name = "test plate") =>
        new(name, "P", DocumentKind.Part, $@"C:\Work\{name}.ipt");

    private static RegisterEntry Entry(int value, string computer = "OTHERPC") =>
        new(
            new PartNumber(value),
            "other part",
            "",
            DocumentKind.Assembly,
            $@"C:\Other\AWB-{value},other_part.iam",
            new DateTimeOffset(2026, 9, 1, 8, 0, 0, TimeSpan.Zero).AddMinutes(value - 10000),
            "otheruser",
            computer);

    private static List<int> Numbers(string path) =>
        RegisterCsv.Read(File.ReadAllText(path)).Entries.Select(e => e.Number.Value).ToList();

    private void WriteShared(params int[] values) =>
        File.WriteAllText(SharedPath, RegisterCsv.Write(values.Select(v => Entry(v))));

    private void WriteLocal(params int[] values)
    {
        Directory.CreateDirectory(_local);
        File.WriteAllText(LocalPath, RegisterCsv.Write(values.Select(v => Entry(v))));
    }

    private void AssertOnlyRegisterFilesLeft()
    {
        Assert.Empty(Directory.GetFiles(_shared, "*.tmp"));
        Assert.False(File.Exists(LockPath));
        if (Directory.Exists(_local))
            Assert.Empty(Directory.GetFiles(_local, "*.tmp"));
    }

    // Construction and files

    [Fact]
    public void FileNamesAreTheDefaultsFromTheSpec()
    {
        Assert.Equal("part-register.csv", RegisterStore.SharedFileName);
        Assert.Equal("part-register.lock", RegisterStore.LockFileName);
        Assert.Equal("part-register.csv", RegisterStore.LocalFileName);
        Assert.Equal("pending.csv", RegisterStore.PendingFileName);
        Assert.Equal(TimeSpan.FromSeconds(5), RegisterStore.DefaultLockWait);
        Assert.Equal(TimeSpan.FromMinutes(2), RegisterStore.StaleLockAge);
        Assert.Equal(TimeSpan.FromSeconds(3), RegisterStore.ProbeTimeout);
    }

    [Fact]
    public void LockWaitDefaultsToFiveSeconds()
    {
        var store = new RegisterStore(_shared, _local, _environment);

        Assert.Equal(RegisterStore.DefaultLockWait, store.LockWait);
    }

    [Fact]
    public void BlankLocalFolderIsRefused()
    {
        Assert.Throws<ArgumentException>(() => new RegisterStore(_shared, " ", _environment));
    }

    [Fact]
    public void MissingSharedFolderIsNeverCreated()
    {
        var missing = Path.Combine(_root, "unplugged");
        _environment.Reachable = false;
        var store = new RegisterStore(missing, _local, _environment, _log);

        store.Assign(Request());
        store.Sync();

        Assert.False(Directory.Exists(missing));
    }

    [Fact]
    public void ProbeIsAskedAboutTheSharedFolderWithTheProbeTimeout()
    {
        Store().Sync();

        Assert.Equal([(_shared, RegisterStore.ProbeTimeout)], _environment.Probes);
    }

    [Fact]
    public void BlankSharedFolderIsAlwaysOfflineWithoutProbing()
    {
        var store = new RegisterStore("", _local, _environment, _log);

        var assignment = store.Assign(Request());
        var sync = store.Sync();

        Assert.True(assignment.Offline);
        Assert.False(sync.Reached);
        Assert.Empty(_environment.Probes);
        Assert.Null(store.SharedFilePath);
    }

    // Reading

    [Fact]
    public void ReadLocal_MissingFilesGiveAnEmptyRegister()
    {
        Assert.Empty(Store().ReadLocal().Entries);
        Assert.False(Directory.Exists(_local));
    }

    [Fact]
    public void ReadLocal_IsTheLocalCopyThenThePendingEntries_AndNeverTouchesTheSharedFolder()
    {
        WriteLocal(10001, 10002);
        File.WriteAllText(PendingPath, RegisterCsv.Write([Entry(10003)]));
        WriteShared(10001, 10002, 10003, 10004);

        var register = Store().ReadLocal();

        Assert.Equal([10001, 10002, 10003], register.Entries.Select(e => e.Number.Value));
        Assert.Empty(_environment.Probes);
    }

    [Fact]
    public void ReadLocal_UnreadableFileGivesEmptyAndAWarning()
    {
        Directory.CreateDirectory(_local);
        File.WriteAllText(LocalPath, "this is not a register\r\nAWB-10001\r\n");

        var register = Store().ReadLocal();

        Assert.Empty(register.Entries);
        Assert.Contains(_log.Warnings, w => w.Contains(LocalPath, StringComparison.Ordinal));
    }

    [Fact]
    public void ReadLocal_SkippedRowIsLoggedAndTheRestRead()
    {
        Directory.CreateDirectory(_local);
        File.WriteAllText(LocalPath, RegisterCsv.Write([Entry(10001)]) + "AWB-1,x,,Part,,,,\r\n");

        var register = Store().ReadLocal();

        Assert.Equal([10001], register.Entries.Select(e => e.Number.Value));
        Assert.Contains(_log.Warnings, w => w.Contains("Line 3", StringComparison.Ordinal));
    }

    [Fact]
    public void HasNumber_UsesTheLocalCopyAndPending()
    {
        WriteLocal(10001);
        File.WriteAllText(PendingPath, RegisterCsv.Write([Entry(10002)]));
        WriteShared(10001, 10002, 10003);
        var store = Store();

        Assert.True(store.HasNumber("AWB-10001"));
        Assert.True(store.HasNumber("awb-10002"));
        Assert.False(store.HasNumber("AWB-10003"));
        Assert.False(store.HasNumber(null));
        Assert.False(store.HasNumber("plate"));
    }

    // Assigning, reachable

    [Fact]
    public void FirstAssignmentInAnEmptyFolderIs10001_AndWritesBothFiles_ThenTheNextIs10002()
    {
        var store = Store();

        var first = store.Assign(Request("first plate"));

        Assert.Equal("AWB-10001", first.PartNumber.ToString());
        Assert.False(first.Offline);
        Assert.Empty(first.Duplicates);
        Assert.Equal([10001], Numbers(SharedPath));
        Assert.Equal([10001], Numbers(LocalPath));

        var second = store.Assign(Request("second plate"));

        Assert.Equal("AWB-10002", second.PartNumber.ToString());
        Assert.Equal([10001, 10002], Numbers(SharedPath));
        Assert.Equal([10001, 10002], Numbers(LocalPath));
        AssertOnlyRegisterFilesLeft();
    }

    [Fact]
    public void AssignedEntryCarriesTheRequestAndTheEnvironment()
    {
        var request = new RegisterRequest("base, plate", "SM", DocumentKind.SheetMetalPart, @"C:\Work\base plate.ipt");

        Store().Assign(request);

        var entry = Assert.Single(RegisterCsv.Read(File.ReadAllText(SharedPath)).Entries);
        Assert.Equal("base, plate", entry.Name);
        Assert.Equal("SM", entry.PartType);
        Assert.Equal(DocumentKind.SheetMetalPart, entry.Kind);
        Assert.Equal(@"C:\Work\base plate.ipt", entry.FilePath);
        Assert.Equal(Now, entry.AssignedAt);
        Assert.Equal("testuser", entry.AssignedBy);
        Assert.Equal("TESTPC", entry.Computer);
    }

    [Fact]
    public void AssignedByIsTheEnvironmentsPerson_OnlineAndOffline()
    {
        _environment.AssignedBy = "A. Person";

        Store().Assign(Request());
        _environment.Reachable = false;
        Store().Assign(Request());

        Assert.Equal("A. Person", Assert.Single(RegisterCsv.Read(File.ReadAllText(SharedPath)).Entries).AssignedBy);
        Assert.Equal("A. Person", Assert.Single(RegisterCsv.Read(File.ReadAllText(PendingPath)).Entries).AssignedBy);
    }

    [Fact]
    public void AssignAfterTheHighestInTheSharedRegister_NotTheLocalCopy()
    {
        WriteShared(10001, 10007);
        WriteLocal(10001);

        var assignment = Store().Assign(Request());

        Assert.Equal(10008, assignment.PartNumber.Value);
        Assert.Equal([10001, 10007, 10008], Numbers(LocalPath));
    }

    [Fact]
    public void ExistingRowsAreKeptInOrder()
    {
        WriteShared(10003, 10001, 10002);

        Store().Assign(Request());

        Assert.Equal([10003, 10001, 10002, 10004], Numbers(SharedPath));
    }

    // Assigning with the request built from the number

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AssignWithRequestFor_GetsTheNumberAboutToBeAssigned_AndRecordsItsRequest(bool reachable)
    {
        WriteShared(10001);
        WriteLocal(10001);
        _environment.Reachable = reachable;
        var seen = new List<PartNumber>();

        var result = Store().Assign(number =>
        {
            seen.Add(number);
            return new RegisterRequest("test plate", "P", DocumentKind.Part, $@"C:\Work\{number},test_plate.ipt");
        });

        Assert.Equal(10002, result.PartNumber.Value);
        Assert.Equal([new PartNumber(10002)], seen);
        var written = RegisterCsv.Read(File.ReadAllText(reachable ? SharedPath : PendingPath)).Entries.Last();
        Assert.Equal(@"C:\Work\AWB-10002,test_plate.ipt", written.FilePath);
        AssertOnlyRegisterFilesLeft();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AssignWithRequestFor_ThatThrows_AssignsNothing_AndChangesNoFile(bool reachable)
    {
        WriteShared(10001);
        WriteLocal(10001);
        _environment.Reachable = reachable;
        var sharedBefore = File.ReadAllBytes(SharedPath);
        var localBefore = File.ReadAllBytes(LocalPath);
        var store = Store();

        var ex = Assert.Throws<InvalidDataException>(() => store.Assign(_ => throw new InvalidDataException("taken")));

        Assert.Equal("taken", ex.Message);
        Assert.Equal(sharedBefore, File.ReadAllBytes(SharedPath));
        Assert.Equal(localBefore, File.ReadAllBytes(LocalPath));
        Assert.False(File.Exists(PendingPath));
        AssertOnlyRegisterFilesLeft();
        Assert.Equal(10002, store.Assign(Request()).PartNumber.Value);
    }

    [Fact]
    public void AssignWithRequestFor_RejectsNull()
    {
        Assert.Throws<ArgumentNullException>(() => Store().Assign((Func<PartNumber, RegisterRequest>)null!));
    }

    // Assigning, offline

    [Fact]
    public void OfflineAssignmentsUseLocalCopyAndPending_AndAreConsecutive()
    {
        WriteShared(10001, 10002);
        WriteLocal(10001, 10002);
        _environment.Reachable = false;
        var store = Store();
        var sharedBefore = File.ReadAllBytes(SharedPath);

        var first = store.Assign(Request("first"));
        var second = store.Assign(Request("second"));

        Assert.True(first.Offline);
        Assert.True(second.Offline);
        Assert.Equal(10003, first.PartNumber.Value);
        Assert.Equal(10004, second.PartNumber.Value);
        Assert.Equal([10003, 10004], Numbers(PendingPath));
        Assert.Equal([10001, 10002], Numbers(LocalPath));
        Assert.Equal(sharedBefore, File.ReadAllBytes(SharedPath));
        Assert.True(store.HasNumber("AWB-10004"));
    }

    [Fact]
    public void OfflineAssignmentCreatesTheLocalFolder()
    {
        _environment.Reachable = false;

        var assignment = Store().Assign(Request());

        Assert.Equal(10001, assignment.PartNumber.Value);
        Assert.True(File.Exists(PendingPath));
    }

    [Fact]
    public void OfflineAppendKeepsAPendingRowThisPcCannotRead()
    {
        Directory.CreateDirectory(_local);
        File.WriteAllText(PendingPath, RegisterCsv.Write([Entry(10001)]) + "AWB-1,x,,Part,,,,");
        _environment.Reachable = false;

        Store().Assign(Request());

        var text = File.ReadAllText(PendingPath);
        Assert.Contains("AWB-1,x,,Part,,,,\r\n", text, StringComparison.Ordinal);
        Assert.Equal([10001, 10002], RegisterCsv.Read(text).Entries.Select(e => e.Number.Value));
    }

    [Fact]
    public void LaterReachableAssignSyncsThePendingEntriesFirstThenAssignsAfterTheHighest()
    {
        WriteShared(10001, 10002);
        WriteLocal(10001, 10002);
        _environment.Reachable = false;
        var store = Store();
        store.Assign(Request("first"));
        store.Assign(Request("second"));

        _environment.Reachable = true;
        var assignment = store.Assign(Request("third"));

        Assert.False(assignment.Offline);
        Assert.Empty(assignment.Duplicates);
        Assert.Equal(10005, assignment.PartNumber.Value);
        Assert.Equal([10001, 10002, 10003, 10004, 10005], Numbers(SharedPath));
        Assert.Equal([10001, 10002, 10003, 10004, 10005], Numbers(LocalPath));
        Assert.False(File.Exists(PendingPath));
        AssertOnlyRegisterFilesLeft();
    }

    [Fact]
    public void PendingNumberAnotherPcAlsoUsedIsReportedAsADuplicate_AndBothRowsAreKept()
    {
        WriteLocal(10001);
        _environment.Reachable = false;
        var store = Store();
        store.Assign(Request("offline plate"));

        // Meanwhile another PC took AWB-10002.
        WriteShared(10001, 10002);
        _environment.Reachable = true;
        var assignment = store.Assign(Request("next plate"));

        var duplicate = Assert.Single(assignment.Duplicates);
        Assert.Equal(10002, duplicate.Number.Value);
        Assert.Equal("OTHERPC", duplicate.Existing.Computer);
        Assert.Equal(@"C:\Work\offline plate.ipt", duplicate.Added.FilePath);
        Assert.Equal(10003, assignment.PartNumber.Value);
        Assert.Equal([10001, 10002, 10002, 10003], Numbers(SharedPath));
        Assert.Contains(_log.Warnings, w => w.Contains("AWB-10002", StringComparison.Ordinal));
    }

    // Syncing

    [Fact]
    public void Sync_Unreachable_DoesNothingAndSaysSo()
    {
        WriteShared(10001);
        _environment.Reachable = false;
        var sharedBefore = File.ReadAllBytes(SharedPath);

        var result = Store().Sync();

        Assert.False(result.Reached);
        Assert.Empty(result.Duplicates);
        Assert.Equal(sharedBefore, File.ReadAllBytes(SharedPath));
        Assert.False(File.Exists(LocalPath));
    }

    [Fact]
    public void Sync_MergesPending_RefreshesLocal_AndAddsNoEntry()
    {
        WriteShared(10001, 10002);
        WriteLocal(10001);
        File.WriteAllText(PendingPath, RegisterCsv.Write([Entry(10002, "TESTPC") with { FilePath = @"C:\Work\mine.ipt" }]));

        var result = Store().Sync();

        Assert.True(result.Reached);
        var duplicate = Assert.Single(result.Duplicates);
        Assert.Equal(10002, duplicate.Number.Value);
        Assert.Equal([10001, 10002, 10002], Numbers(SharedPath));
        Assert.Equal([10001, 10002, 10002], Numbers(LocalPath));
        Assert.False(File.Exists(PendingPath));
        AssertOnlyRegisterFilesLeft();
    }

    [Fact]
    public void Sync_WithNothingPending_LeavesTheSharedFileAlone_AndRefreshesLocal()
    {
        WriteShared(10001, 10002);
        var sharedBefore = File.ReadAllBytes(SharedPath);

        var result = Store().Sync();

        Assert.True(result.Reached);
        Assert.Equal(sharedBefore, File.ReadAllBytes(SharedPath));
        Assert.Equal([10001, 10002], Numbers(LocalPath));
    }

    [Fact]
    public void Sync_PendingAlreadyInTheSharedRegisterExactlyIsNotAddedAgain()
    {
        WriteShared(10001, 10002);
        Directory.CreateDirectory(_local);
        File.WriteAllText(PendingPath, RegisterCsv.Write([Entry(10002)]));

        var result = Store().Sync();

        Assert.Empty(result.Duplicates);
        Assert.Equal([10001, 10002], Numbers(SharedPath));
        Assert.False(File.Exists(PendingPath));
    }

    // Lock

    [Fact]
    public void StaleLockIsClearedAndLogged()
    {
        File.WriteAllText(LockPath, "crashed");
        File.SetLastWriteTimeUtc(LockPath, Now.UtcDateTime - TimeSpan.FromMinutes(5));

        var assignment = Store().Assign(Request());

        Assert.Equal(10001, assignment.PartNumber.Value);
        Assert.Contains(_log.Warnings, w => w.Contains(LockPath, StringComparison.Ordinal));
        AssertOnlyRegisterFilesLeft();
    }

    [Fact]
    public void FreshLockHeldByAnotherPcThrowsBusy_AndNothingIsAssigned()
    {
        WriteShared(10001);
        var sharedBefore = File.ReadAllBytes(SharedPath);
        File.WriteAllText(LockPath, "other PC");
        File.SetLastWriteTimeUtc(LockPath, Now.UtcDateTime - TimeSpan.FromSeconds(30));

        var ex = Assert.Throws<RegisterBusyException>(() => Store(TimeSpan.FromMilliseconds(250)).Assign(Request()));

        Assert.Contains("Another PC is writing the part register", ex.Message, StringComparison.Ordinal);
        Assert.Equal(LockPath, ex.LockFilePath);
        Assert.Equal(sharedBefore, File.ReadAllBytes(SharedPath));
        Assert.Equal("other PC", File.ReadAllText(LockPath));
        Assert.False(File.Exists(PendingPath));
        Assert.False(File.Exists(LocalPath));
        Assert.Empty(Directory.GetFiles(_shared, "*.tmp"));
    }

    [Fact]
    public void FreshLockMakesSyncThrowBusyToo()
    {
        File.WriteAllText(LockPath, "other PC");
        File.SetLastWriteTimeUtc(LockPath, Now.UtcDateTime);

        Assert.Throws<RegisterBusyException>(() => Store(TimeSpan.FromMilliseconds(150)).Sync());
    }

    // Unreadable shared register

    [Fact]
    public void CorruptSharedFileThrowsAndIsLeftUnchanged()
    {
        File.WriteAllText(SharedPath, "this is not a register\r\nAWB-10001,plate\r\n");
        var before = File.ReadAllBytes(SharedPath);

        var ex = Assert.Throws<RegisterUnreadableException>(() => Store().Assign(Request()));

        Assert.Equal(SharedPath, ex.FilePath);
        Assert.Contains(SharedPath, ex.Message, StringComparison.Ordinal);
        Assert.Equal(before, File.ReadAllBytes(SharedPath));
        Assert.False(File.Exists(LocalPath));
        AssertOnlyRegisterFilesLeft();
    }

    [Fact]
    public void SharedFileWithOneBadRowThrowsNamingTheLine_AndIsLeftUnchangedByteForByte()
    {
        var text = RegisterCsv.Write([Entry(10001), Entry(10002)])
            + "AWB-10003,bad date,,Part,C:\\x.ipt,last tuesday,someone,PC\r\n";
        File.WriteAllText(SharedPath, text);
        var before = File.ReadAllBytes(SharedPath);

        var ex = Assert.Throws<RegisterUnreadableException>(() => Store().Assign(Request()));

        Assert.Equal(SharedPath, ex.FilePath);
        var problem = Assert.Single(ex.Problems);
        Assert.Contains("Line 4", problem, StringComparison.Ordinal);
        Assert.Contains("Line 4", ex.Message, StringComparison.Ordinal);
        Assert.Equal(before, File.ReadAllBytes(SharedPath));
        AssertOnlyRegisterFilesLeft();
    }

    [Fact]
    public void SharedFileWithTheAssignedAtColumnDeletedThrows_AndIsLeftUnchangedByteForByte()
    {
        var text =
            "Number,Name,PartType,Kind,FilePath,AssignedBy,Computer\r\n" +
            "AWB-10001,plate,P,Part,C:\\Work\\plate.ipt,someone,PC\r\n";
        File.WriteAllText(SharedPath, text);
        var before = File.ReadAllBytes(SharedPath);

        var ex = Assert.Throws<RegisterUnreadableException>(() => Store().Assign(Request()));

        Assert.Contains(ex.Problems, p => p.Contains(RegisterCsv.AssignedAtColumn, StringComparison.Ordinal));
        Assert.Equal(before, File.ReadAllBytes(SharedPath));
        AssertOnlyRegisterFilesLeft();
    }

    [Fact]
    public void SyncRefusesASharedFileWithABadRowToo_AndKeepsThePendingEntries()
    {
        var text = RegisterCsv.Write([Entry(10001)]) + "not-a-number,x,,Part,,,,\r\n";
        File.WriteAllText(SharedPath, text);
        var before = File.ReadAllBytes(SharedPath);
        Directory.CreateDirectory(_local);
        File.WriteAllText(PendingPath, RegisterCsv.Write([Entry(10002, "TESTPC")]));

        Assert.Throws<RegisterUnreadableException>(() => Store().Sync());

        Assert.Equal(before, File.ReadAllBytes(SharedPath));
        Assert.Equal([10002], Numbers(PendingPath));
        AssertOnlyRegisterFilesLeft();
    }

    [Fact]
    public void PendingFileWithABadRowIsRefusedRatherThanDeleted()
    {
        WriteShared(10001);
        var sharedBefore = File.ReadAllBytes(SharedPath);
        Directory.CreateDirectory(_local);
        var pending = RegisterCsv.Write([Entry(10002, "TESTPC")]) + "AWB-1,x,,Part,,,,\r\n";
        File.WriteAllText(PendingPath, pending);

        var ex = Assert.Throws<RegisterUnreadableException>(() => Store().Assign(Request()));

        Assert.Equal(PendingPath, ex.FilePath);
        Assert.Equal(pending, File.ReadAllText(PendingPath));
        Assert.Equal(sharedBefore, File.ReadAllBytes(SharedPath));
        AssertOnlyRegisterFilesLeft();
    }

    // Registering a number a file already carries (Mass Update, task 048)

    [Fact]
    public void RegisterExisting_Reachable_AppendsTheRowWithThatNumber_InBothFiles()
    {
        WriteShared(10001, 10003);

        var result = Store().RegisterExisting(new PartNumber(10900), Request("old plate"));

        Assert.True(result.Added);
        Assert.False(result.Offline);
        Assert.Empty(result.Duplicates);
        Assert.Equal([10001, 10003, 10900], Numbers(SharedPath));
        Assert.Equal([10001, 10003, 10900], Numbers(LocalPath));
        var entry = RegisterCsv.Read(File.ReadAllText(SharedPath)).Entries[^1];
        Assert.Equal("old plate", entry.Name);
        Assert.Equal(@"C:\Work\old plate.ipt", entry.FilePath);
        Assert.Equal(Now, entry.AssignedAt);
        Assert.Equal("testuser", entry.AssignedBy);
        AssertOnlyRegisterFilesLeft();
    }

    [Fact]
    public void RegisterExisting_NumberAlreadyHeld_AddsNothing_AndSaysSo()
    {
        WriteShared(10001, 10003);
        var before = File.ReadAllBytes(SharedPath);

        var result = Store().RegisterExisting(new PartNumber(10003), Request());

        Assert.False(result.Added);
        Assert.False(result.Offline);
        Assert.Equal(before, File.ReadAllBytes(SharedPath));
        Assert.Contains(_log.Warnings, w => w.Contains("AWB-10003", StringComparison.Ordinal));
    }

    [Fact]
    public void RegisterExisting_ThenAssign_GivesOneMoreThanTheRegisteredNumber()
    {
        WriteShared(10001);
        var store = Store();

        store.RegisterExisting(new PartNumber(10900), Request("old plate"));
        var assigned = store.Assign(Request("new plate"));

        Assert.Equal(new PartNumber(10901), assigned.PartNumber);
    }

    [Fact]
    public void RegisterExisting_Offline_GoesToPending_UnlessThisPcHoldsIt()
    {
        WriteLocal(10001);
        _environment.Reachable = false;
        var store = Store();

        var added = store.RegisterExisting(new PartNumber(10900), Request());
        var again = store.RegisterExisting(new PartNumber(10900), Request());
        var held = store.RegisterExisting(new PartNumber(10001), Request());

        Assert.Equal((true, true), (added.Added, added.Offline));
        Assert.Equal((false, true), (again.Added, again.Offline));
        Assert.Equal((false, true), (held.Added, held.Offline));
        Assert.Equal([10900], Numbers(PendingPath));
        Assert.True(store.ReadLocal().Contains(new PartNumber(10900)));
    }

    [Fact]
    public void RegisterExisting_SyncsPendingFirst_AndReportsTheirDuplicates()
    {
        WriteShared(10001, 10002);
        Directory.CreateDirectory(_local);
        File.WriteAllText(PendingPath, RegisterCsv.Write([Entry(10002, "TESTPC") with { FilePath = @"C:\Work\offline.ipt" }]));

        var result = Store().RegisterExisting(new PartNumber(10900), Request());

        Assert.True(result.Added);
        Assert.Equal(new PartNumber(10002), Assert.Single(result.Duplicates).Number);
        Assert.Equal([10001, 10002, 10002, 10900], Numbers(SharedPath));
        Assert.False(File.Exists(PendingPath));
    }

    [Fact]
    public void RegisterExisting_BusyLockThrows_AndChangesNothing()
    {
        WriteShared(10001);
        File.WriteAllText(LockPath, "someone else");
        File.SetLastWriteTimeUtc(LockPath, Now.UtcDateTime);
        var before = File.ReadAllBytes(SharedPath);

        Assert.Throws<RegisterBusyException>(() => Store().RegisterExisting(new PartNumber(10900), Request()));

        Assert.Equal(before, File.ReadAllBytes(SharedPath));
        Assert.False(File.Exists(PendingPath));
    }

    [Fact]
    public void RegisterExisting_RejectsNull()
    {
        Assert.Throws<ArgumentNullException>(() => Store().RegisterExisting(new PartNumber(10900), null!));
    }

    [Fact]
    public void WrittenRegisterHasAByteOrderMarkForExcel()
    {
        Store().Assign(Request());

        var bytes = File.ReadAllBytes(SharedPath);
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]);
    }

    // Updating an entry (task 047, Rename)

    private string PendingUpdatesPath => Path.Combine(_local, RegisterStore.PendingUpdatesFileName);

    private static List<RegisterEntry> Entries(string path) => RegisterCsv.Read(File.ReadAllText(path)).Entries.ToList();

    [Fact]
    public void PendingUpdatesFileNameIsTheDefault()
    {
        Assert.Equal("pending-updates.json", RegisterStore.PendingUpdatesFileName);
    }

    [Fact]
    public void UpdateEntry_Reachable_RewritesPathAndNameOnly_InPlace_InBothFiles()
    {
        WriteShared(10001, 10002, 10003);

        var result = Store().UpdateEntry(new PartNumber(10002), @"C:\Work\AWB-10002,base_plate.iam", "base plate");

        Assert.True(result.Found);
        Assert.False(result.Offline);
        Assert.Empty(result.Duplicates);
        foreach (var path in new[] { SharedPath, LocalPath })
        {
            var entries = Entries(path);
            Assert.Equal([10001, 10002, 10003], entries.Select(e => e.Number.Value));
            Assert.Equal(Entry(10001), entries[0]);
            Assert.Equal(Entry(10003), entries[2]);
            Assert.Equal(
                Entry(10002) with { FilePath = @"C:\Work\AWB-10002,base_plate.iam", Name = "base plate" },
                entries[1]);
        }

        AssertOnlyRegisterFilesLeft();
    }

    [Fact]
    public void UpdateEntry_NumberNotInTheRegister_ChangesNothing_AndSaysSo()
    {
        WriteShared(10001);
        var before = File.ReadAllBytes(SharedPath);

        var result = Store().UpdateEntry(new PartNumber(10009), @"C:\Work\x.ipt", "x");

        Assert.False(result.Found);
        Assert.Equal(before, File.ReadAllBytes(SharedPath));
        Assert.Contains(_log.Warnings, w => w.Contains("AWB-10009", StringComparison.Ordinal));
        AssertOnlyRegisterFilesLeft();
    }

    [Fact]
    public void UpdateEntry_NumberHeldTwice_ChangesTheRowWithTheOldPath()
    {
        var first = Entry(10002) with { FilePath = @"C:\A\one.ipt" };
        var second = Entry(10002, "TESTPC") with { FilePath = @"C:\B\two.ipt" };
        File.WriteAllText(SharedPath, RegisterCsv.Write([Entry(10001), first, second]));

        Store().UpdateEntry(new PartNumber(10002), @"C:\B\renamed.ipt", "renamed", oldPath: @"c:\b\TWO.ipt");

        var entries = Entries(SharedPath);
        Assert.Equal(first, entries[1]);
        Assert.Equal(@"C:\B\renamed.ipt", entries[2].FilePath);
    }

    [Fact]
    public void UpdateEntry_Reachable_SyncsPendingEntriesToo()
    {
        WriteShared(10001);
        WriteLocal(10001);
        _environment.Reachable = false;
        var store = Store();
        store.Assign(Request("offline plate"));

        _environment.Reachable = true;
        var result = store.UpdateEntry(new PartNumber(10002), @"C:\Work\AWB-10002,renamed.ipt", "renamed");

        Assert.True(result.Found);
        Assert.Equal([10001, 10002], Numbers(SharedPath));
        Assert.Equal(@"C:\Work\AWB-10002,renamed.ipt", Entries(SharedPath)[1].FilePath);
        Assert.False(File.Exists(PendingPath));
    }

    [Fact]
    public void UpdateEntry_Offline_WaitsInThePendingUpdates_ShowsInReadLocal_AndIsAppliedAtTheNextSync()
    {
        WriteShared(10001, 10002);
        WriteLocal(10001, 10002);
        var sharedBefore = File.ReadAllBytes(SharedPath);
        _environment.Reachable = false;
        var store = Store();

        var result = store.UpdateEntry(new PartNumber(10002), @"C:\Work\AWB-10002,renamed.iam", "renamed", Entry(10002).FilePath);

        Assert.True(result.Found);
        Assert.True(result.Offline);
        Assert.True(File.Exists(PendingUpdatesPath));
        Assert.Equal(sharedBefore, File.ReadAllBytes(SharedPath));
        Assert.Equal(@"C:\Work\AWB-10002,renamed.iam", store.ReadLocal().Entries[1].FilePath);
        Assert.Equal("renamed", store.ReadLocal().Entries[1].Name);

        _environment.Reachable = true;
        var sync = store.Sync();

        Assert.True(sync.Reached);
        var shared = Entries(SharedPath);
        Assert.Equal([10001, 10002], shared.Select(e => e.Number.Value));
        Assert.Equal(Entry(10002) with { FilePath = @"C:\Work\AWB-10002,renamed.iam", Name = "renamed" }, shared[1]);
        Assert.Equal(shared, Entries(LocalPath));
        Assert.False(File.Exists(PendingUpdatesPath));
        AssertOnlyRegisterFilesLeft();
    }

    [Fact]
    public void UpdateEntry_Offline_TwoUpdatesAreAppliedInOrder()
    {
        WriteShared(10001);
        WriteLocal(10001);
        _environment.Reachable = false;
        var store = Store();

        store.UpdateEntry(new PartNumber(10001), @"C:\Work\first.iam", "first");
        store.UpdateEntry(new PartNumber(10001), @"C:\Work\second.iam", "second", @"C:\Work\first.iam");
        _environment.Reachable = true;
        store.Sync();

        var entry = Assert.Single(Entries(SharedPath));
        Assert.Equal(@"C:\Work\second.iam", entry.FilePath);
        Assert.Equal("second", entry.Name);
    }

    [Fact]
    public void UpdateEntry_Offline_OfAPendingNumber_IsAppliedAfterThePendingEntryIsSynced()
    {
        WriteShared(10001);
        WriteLocal(10001);
        _environment.Reachable = false;
        var store = Store();
        store.Assign(Request("offline plate"));
        store.UpdateEntry(new PartNumber(10002), @"C:\Work\AWB-10002,renamed.ipt", "renamed");

        _environment.Reachable = true;
        var assignment = store.Assign(Request("next plate"));

        Assert.Equal(10003, assignment.PartNumber.Value);
        var shared = Entries(SharedPath);
        Assert.Equal([10001, 10002, 10003], shared.Select(e => e.Number.Value));
        Assert.Equal(@"C:\Work\AWB-10002,renamed.ipt", shared[1].FilePath);
        Assert.False(File.Exists(PendingUpdatesPath));
    }

    [Fact]
    public void UpdateEntry_Offline_NumberThisPcDoesNotHold_IsNotQueued()
    {
        WriteLocal(10001);
        _environment.Reachable = false;

        var result = Store().UpdateEntry(new PartNumber(10005), @"C:\Work\x.ipt", "x");

        Assert.False(result.Found);
        Assert.True(result.Offline);
        Assert.False(File.Exists(PendingUpdatesPath));
    }

    [Fact]
    public void PendingUpdateForANumberTheSharedRegisterDoesNotHold_IsDroppedWithAWarning()
    {
        WriteLocal(10001, 10002);
        _environment.Reachable = false;
        var store = Store();
        store.UpdateEntry(new PartNumber(10002), @"C:\Work\x.ipt", "x");

        // The shared register never had AWB-10002 (for example the local copy came from elsewhere).
        WriteShared(10001);
        var sharedBefore = File.ReadAllBytes(SharedPath);
        _environment.Reachable = true;
        store.Sync();

        Assert.Equal(sharedBefore, File.ReadAllBytes(SharedPath));
        Assert.False(File.Exists(PendingUpdatesPath));
        Assert.Contains(_log.Warnings, w => w.Contains("dropped", StringComparison.Ordinal));
    }

    [Fact]
    public void UpdateEntry_LockBusy_IsQueued_ThenAppliedAtTheNextSync()
    {
        WriteShared(10001, 10002);
        WriteLocal(10001, 10002);
        var before = File.ReadAllBytes(SharedPath);
        File.WriteAllText(LockPath, "other PC");
        File.SetLastWriteTimeUtc(LockPath, Now.UtcDateTime);
        var store = Store(TimeSpan.FromMilliseconds(150));

        var result = store.UpdateEntry(new PartNumber(10002), @"C:\Work\AWB-10002,renamed.ipt", "renamed", Entry(10002).FilePath);

        Assert.True(result.Found);
        Assert.True(result.Offline);
        Assert.NotNull(result.QueuedReason);
        Assert.Equal(before, File.ReadAllBytes(SharedPath));
        Assert.True(File.Exists(PendingUpdatesPath));
        Assert.Equal(@"C:\Work\AWB-10002,renamed.ipt", store.ReadLocal().Entries[1].FilePath);

        File.Delete(LockPath);
        store.Sync();

        Assert.Equal(Entry(10002) with { FilePath = @"C:\Work\AWB-10002,renamed.ipt", Name = "renamed" }, Entries(SharedPath)[1]);
        Assert.False(File.Exists(PendingUpdatesPath));
        AssertOnlyRegisterFilesLeft();
    }

    [Fact]
    public void UpdateEntry_SharedFileWithABadRow_IsQueued_AndTheSharedFileIsLeftUnchanged()
    {
        WriteLocal(10001);
        var text = RegisterCsv.Write([Entry(10001)]) + "AWB-10002,bad date,,Part,C:\\x.ipt,last tuesday,someone,PC\r\n";
        File.WriteAllText(SharedPath, text);
        var before = File.ReadAllBytes(SharedPath);

        var result = Store().UpdateEntry(new PartNumber(10001), @"C:\x.ipt", "x");

        Assert.True(result.Found);
        Assert.Contains("Line 3", result.QueuedReason, StringComparison.Ordinal);
        Assert.Equal(before, File.ReadAllBytes(SharedPath));
        Assert.True(File.Exists(PendingUpdatesPath));
        AssertOnlyRegisterFilesLeft();
    }

    [Fact]
    public void UpdateEntry_WriteFailsAndThePendingUpdatesFileIsUnreadable_Throws_AndChangesNothing()
    {
        WriteShared(10001);
        WriteLocal(10001);
        File.WriteAllText(PendingUpdatesPath, "this is not json");
        var before = File.ReadAllBytes(SharedPath);

        var ex = Assert.Throws<RegisterUnreadableException>(
            () => Store().UpdateEntry(new PartNumber(10001), @"C:\x.ipt", "x"));

        Assert.Equal(PendingUpdatesPath, ex.FilePath);
        Assert.Equal(before, File.ReadAllBytes(SharedPath));
        Assert.Equal("this is not json", File.ReadAllText(PendingUpdatesPath));
    }

    [Fact]
    public void TheSameUpdateAppliedTwice_ChangesNothingMore_EvenWithANumberHeldTwice()
    {
        var first = Entry(10002) with { FilePath = @"C:\A\one.ipt" };
        var second = Entry(10002, "TESTPC") with { FilePath = @"C:\B\two.ipt" };
        File.WriteAllText(SharedPath, RegisterCsv.Write([first, second]));
        WriteLocal();
        File.WriteAllText(LocalPath, RegisterCsv.Write([first, second]));
        var store = Store();

        store.UpdateEntry(new PartNumber(10002), @"C:\B\renamed.ipt", "renamed", @"C:\B\two.ipt");
        var once = File.ReadAllBytes(SharedPath);

        // As after a write that got part way and was queued: the same update arrives again.
        _environment.Reachable = false;
        store.UpdateEntry(new PartNumber(10002), @"C:\B\renamed.ipt", "renamed", @"C:\B\two.ipt");
        _environment.Reachable = true;
        store.Sync();

        Assert.Equal(once, File.ReadAllBytes(SharedPath));
        Assert.Equal(first, Entries(SharedPath)[0]);
    }

    [Fact]
    public void UnreadablePendingUpdatesFile_IsRefusedRatherThanDeleted_AndIgnoredByReadLocal()
    {
        WriteShared(10001);
        WriteLocal(10001);
        File.WriteAllText(PendingUpdatesPath, "this is not json");
        var sharedBefore = File.ReadAllBytes(SharedPath);

        var ex = Assert.Throws<RegisterUnreadableException>(() => Store().Sync());

        Assert.Equal(PendingUpdatesPath, ex.FilePath);
        Assert.Equal("this is not json", File.ReadAllText(PendingUpdatesPath));
        Assert.Equal(sharedBefore, File.ReadAllBytes(SharedPath));
        Assert.Equal([10001], Store().ReadLocal().Entries.Select(e => e.Number.Value));
        Assert.Contains(_log.Warnings, w => w.Contains(PendingUpdatesPath, StringComparison.Ordinal));
        AssertOnlyRegisterFilesLeft();
    }

    [Fact]
    public void UnreadablePendingUpdatesFile_IsNotOverwrittenByAnOfflineUpdate()
    {
        WriteLocal(10001);
        File.WriteAllText(PendingUpdatesPath, "[ { \"number\": \"not a number\" } ]");
        _environment.Reachable = false;

        Assert.Throws<RegisterUnreadableException>(
            () => Store().UpdateEntry(new PartNumber(10001), @"C:\x.ipt", "x"));

        Assert.Equal("[ { \"number\": \"not a number\" } ]", File.ReadAllText(PendingUpdatesPath));
    }

    [Fact]
    public void UpdateEntry_RejectsABlankPath()
    {
        Assert.Throws<ArgumentException>(() => Store().UpdateEntry(new PartNumber(10001), " ", "x"));
    }

    // RegisterEnvironment

    [Fact]
    public void RealEnvironment_ProbeFindsAnExistingFolderAndNotAMissingOne()
    {
        var environment = new RegisterEnvironment("A. Person");

        Assert.Equal("A. Person", environment.AssignedBy);
        Assert.True(environment.ProbeFolder(_shared, TimeSpan.FromSeconds(3)));
        Assert.False(environment.ProbeFolder(Path.Combine(_root, "missing"), TimeSpan.FromSeconds(3)));
        Assert.False(environment.ProbeFolder("", TimeSpan.FromSeconds(3)));
        Assert.Equal(TimeSpan.Zero, environment.Now.Offset);
        Assert.False(string.IsNullOrEmpty(environment.ComputerName));
    }

    [Fact]
    public void RealEnvironment_NullAssignedByReadsAsBlank()
    {
        Assert.Equal("", new RegisterEnvironment(null).AssignedBy);
    }

    private sealed class FakeEnvironment : IRegisterEnvironment
    {
        public bool Reachable { get; set; } = true;

        public List<(string Folder, TimeSpan Timeout)> Probes { get; } = [];

        public DateTimeOffset Now => RegisterStoreTests.Now;

        public string AssignedBy { get; set; } = "testuser";

        public string ComputerName => "TESTPC";

        public bool ProbeFolder(string folder, TimeSpan timeout)
        {
            Probes.Add((folder, timeout));
            return Reachable && Directory.Exists(folder);
        }
    }

    private sealed class RecordingLog : ILog
    {
        public List<string> Warnings { get; } = [];

        public void Info(string message) { }

        public void Warn(string message) => Warnings.Add(message);

        public void Error(string message, Exception? ex = null) => Warnings.Add(message);
    }
}
