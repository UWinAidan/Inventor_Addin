using InventorAddin.Core.Logging;
using InventorAddin.Core.Models;
using InventorAddin.Core.Numbering;
using InventorAddin.Core.PartProperties;

namespace InventorAddin.Core.Tests.PartProperties;

public sealed class RegisterPartNumberAssignerTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 10, 14, 3, 0, TimeSpan.Zero);

    private readonly string _root;
    private readonly string _shared;
    private readonly string _local;
    private readonly FakeEnvironment _environment = new();
    private readonly RecordingLog _log = new();
    private readonly FakeTarget _target = new();

    public RegisterPartNumberAssignerTests()
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
    private string LocalPath => Path.Combine(_local, RegisterStore.LocalFileName);
    private string PendingPath => Path.Combine(_local, RegisterStore.PendingFileName);
    private string LockPath => Path.Combine(_shared, RegisterStore.LockFileName);

    private RegisterStore Store() =>
        new(_shared, _local, _environment, _log, TimeSpan.FromMilliseconds(200));

    private static PartPropertiesSnapshot Snapshot(string partNumber = "test_plate") => new()
    {
        FullFileName = @"C:\Work\test_plate.ipt",
        DocumentKind = DocumentKind.SheetMetalPart,
        PartNumber = partNumber,
        PartType = " pm ",
    };

    private RegisterPartNumberAssigner Assigner(PartPropertiesSnapshot? snapshot = null, RegisterStore? store = null) =>
        new(store ?? Store(), snapshot ?? Snapshot(), _target, _log);

    private static RegisterEntry Entry(int value, string path) =>
        new(new PartNumber(value), "other part", "", DocumentKind.Part, path, Now.AddDays(-1), "otheruser", "OTHERPC");

    private static List<RegisterEntry> Read(string path) => RegisterCsv.Read(File.ReadAllText(path)).Entries.ToList();

    private void WriteShared(params RegisterEntry[] entries) =>
        File.WriteAllText(SharedPath, RegisterCsv.Write(entries));

    private void WritePending(params RegisterEntry[] entries)
    {
        Directory.CreateDirectory(_local);
        File.WriteAllText(PendingPath, RegisterCsv.Write(entries));
    }

    [Fact]
    public void Messages()
    {
        Assert.Equal("Save the file before giving it a part number.", RegisterPartNumberAssigner.NotSavedMessage);
        Assert.Equal("This file already has a registered number.", RegisterPartNumberAssigner.AlreadyRegisteredMessage);
    }

    [Fact]
    public void Constructor_RejectsNulls()
    {
        Assert.Throws<ArgumentNullException>(() => new RegisterPartNumberAssigner(null!, Snapshot(), _target));
        Assert.Throws<ArgumentNullException>(() => new RegisterPartNumberAssigner(Store(), null!, _target));
        Assert.Throws<ArgumentNullException>(() => new RegisterPartNumberAssigner(Store(), Snapshot(), null!));
    }

    [Fact]
    public void RequestFor_UsesTheNameFromTheFileTheListedTypeTheKindAndThePath()
    {
        var request = RegisterPartNumberAssigner.RequestFor(Snapshot() with
        {
            FullFileName = @"C:\Work\mounting_bracket,AWB-10500.ipt",
        });

        Assert.Equal("mounting bracket", request.Name);
        Assert.Equal("PM", request.PartType);
        Assert.Equal(DocumentKind.SheetMetalPart, request.Kind);
        Assert.Equal(@"C:\Work\mounting_bracket,AWB-10500.ipt", request.FilePath);
    }

    [Fact]
    public void RequestFor_KeepsAnUnlistedTypeAndBlank()
    {
        Assert.Equal("XQ", RegisterPartNumberAssigner.RequestFor(Snapshot() with { PartType = " XQ " }).PartType);
        Assert.Equal("", RegisterPartNumberAssigner.RequestFor(Snapshot() with { PartType = null }).PartType);
    }

    [Fact]
    public void Assign_Online_RegistersThenWritesThePartNumber()
    {
        WriteShared(Entry(10001, @"C:\Other\a.ipt"));

        var result = Assigner().Assign();

        Assert.Equal("AWB-10002", result.PartNumber);
        Assert.False(result.Offline);
        Assert.Empty(result.DuplicateMessages);

        var shared = Read(SharedPath);
        Assert.Equal(2, shared.Count);
        Assert.Equal("AWB-10002", shared[1].Number.ToString());
        Assert.Equal("test plate", shared[1].Name);
        Assert.Equal(@"C:\Work\test_plate.ipt", shared[1].FilePath);
        Assert.Equal(Read(SharedPath), Read(LocalPath));

        var writes = Assert.Single(_target.Calls);
        var write = Assert.Single(writes);
        Assert.Equal(PartPropertyStorage.PartNumber, write.Location);
        Assert.Equal(PropertyWriteAction.Set, write.Action);
        Assert.Equal("AWB-10002", write.TextValue);
    }

    [Fact]
    public void Assign_RegistersBeforeWriting()
    {
        var store = Store();
        _target.OnApply = () => Assert.True(store.HasNumber("AWB-10001"));

        Assigner(store: store).Assign();

        Assert.Single(_target.Calls);
    }

    [Fact]
    public void Assign_Offline_SaysSoAndUsesThePendingFile()
    {
        _environment.Reachable = false;

        var result = Assigner().Assign();

        Assert.Equal("AWB-10001", result.PartNumber);
        Assert.True(result.Offline);
        Assert.False(File.Exists(SharedPath));
        Assert.Single(Read(PendingPath));
        Assert.Single(_target.Calls);
    }

    [Fact]
    public void Assign_NoSharedFolder_IsOffline()
    {
        var store = new RegisterStore(null, _local, _environment, _log);

        var result = Assigner(store: store).Assign();

        Assert.True(result.Offline);
        Assert.Equal("AWB-10001", result.PartNumber);
    }

    [Fact]
    public void Assign_SyncsFirst_AndReportsDuplicates()
    {
        WriteShared(Entry(10001, @"C:\Other\a.ipt"));
        WritePending(Entry(10001, @"C:\Mine\b.ipt") with { AssignedAt = Now.AddHours(-2), Computer = "TESTPC" });

        var result = Assigner().Assign();

        Assert.Equal("AWB-10002", result.PartNumber);
        var message = Assert.Single(result.DuplicateMessages);
        Assert.Equal(@"AWB-10001 was given to two files: 'C:\Other\a.ipt' and 'C:\Mine\b.ipt'.", message);
        Assert.Equal(3, Read(SharedPath).Count);
        Assert.False(File.Exists(PendingPath));
    }

    [Fact]
    public void Assign_RefusesWhenTheSyncShowsTheNumberIsRegistered()
    {
        // The window opened with AWB-10500 not in this PC's copy; another PC has since registered it.
        WriteShared(Entry(10500, @"C:\Other\test_plate.ipt"));
        var store = Store();
        Assert.False(store.HasNumber("AWB-10500"));

        var ex = Assert.Throws<InvalidOperationException>(() => Assigner(Snapshot("AWB-10500"), store).Assign());

        Assert.Equal("This file already has a registered number.", ex.Message);
        Assert.Single(Read(SharedPath));
        Assert.Empty(_target.Calls);
    }

    [Fact]
    public void Assign_AnUnregisteredNumber_GetsANewOne()
    {
        WriteShared(Entry(10001, @"C:\Other\a.ipt"));

        var result = Assigner(Snapshot("AWB-10500")).Assign();

        Assert.Equal("AWB-10002", result.PartNumber);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Assign_RefusesANeverSavedFile(string fullFileName)
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => Assigner(Snapshot() with { FullFileName = fullFileName }).Assign());

        Assert.Equal(RegisterPartNumberAssigner.NotSavedMessage, ex.Message);
        Assert.False(File.Exists(SharedPath));
        Assert.Empty(_target.Calls);
    }

    [Fact]
    public void Assign_Busy_PassesTheRegisterMessageOn_AndWritesNothing()
    {
        File.WriteAllText(LockPath, "someone else");
        File.SetLastWriteTimeUtc(LockPath, Now.UtcDateTime);

        var ex = Assert.Throws<RegisterBusyException>(() => Assigner().Assign());

        Assert.Contains("Nothing was assigned", ex.Message);
        Assert.Empty(_target.Calls);
        Assert.False(File.Exists(SharedPath));
    }

    [Fact]
    public void Assign_UnreadableRegister_PassesTheRegisterMessageOn_AndWritesNothing()
    {
        File.WriteAllText(SharedPath, RegisterCsv.Header + RegisterCsv.NewLine + "AWB-10001,a,,Part,C:\\a.ipt,not a date,u,PC" + RegisterCsv.NewLine);

        var ex = Assert.Throws<RegisterUnreadableException>(() => Assigner().Assign());

        Assert.Contains("Nothing was assigned", ex.Message);
        Assert.Empty(_target.Calls);
    }

    [Fact]
    public void Assign_WriteFails_TheNumberStaysInTheRegister_AndTheMessageAndLogSaySo()
    {
        _target.Throw = new InvalidOperationException("Could not set the property 'Part Number'.");

        var ex = Assert.Throws<InvalidOperationException>(() => Assigner().Assign());

        Assert.Equal(
            "Part number AWB-10001 was assigned in the register but could not be written to the file: " +
            "Could not set the property 'Part Number'. The number stays in the register.",
            ex.Message);
        Assert.Same(_target.Throw, ex.InnerException);
        Assert.Single(Read(SharedPath));
        Assert.Contains(_log.Errors, e => e.Message.Contains("AWB-10001") && e.Message.Contains("register entry stays"));
    }

    [Fact]
    public void DescribeDuplicate_NamesTheNumberAndBothFiles()
    {
        var duplicate = new RegisterDuplicate(
            new PartNumber(10005), Entry(10005, @"C:\A\one.ipt"), Entry(10005, @"C:\B\two.ipt"));

        Assert.Equal(
            @"AWB-10005 was given to two files: 'C:\A\one.ipt' and 'C:\B\two.ipt'.",
            PartNumberAssignment.DescribeDuplicate(duplicate));
    }

    private sealed class FakeTarget : IPropertyWriteTarget
    {
        public List<IReadOnlyList<PropertyWrite>> Calls { get; } = [];
        public Exception? Throw { get; set; }
        public Action? OnApply { get; set; }

        public void Apply(IReadOnlyList<PropertyWrite> writes)
        {
            Calls.Add(writes);
            OnApply?.Invoke();
            if (Throw is not null)
                throw Throw;
        }
    }

    private sealed class FakeEnvironment : IRegisterEnvironment
    {
        public bool Reachable { get; set; } = true;

        public DateTimeOffset Now => RegisterPartNumberAssignerTests.Now;

        public string AssignedBy => "testuser";

        public string ComputerName => "TESTPC";

        public bool ProbeFolder(string folder, TimeSpan timeout) => Reachable && Directory.Exists(folder);
    }

    private sealed class RecordingLog : ILog
    {
        public List<string> Warnings { get; } = [];
        public List<(string Message, Exception? Ex)> Errors { get; } = [];

        public void Info(string message) { }

        public void Warn(string message) => Warnings.Add(message);

        public void Error(string message, Exception? ex = null) => Errors.Add((message, ex));
    }
}
