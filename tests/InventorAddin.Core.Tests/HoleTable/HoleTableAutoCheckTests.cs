using InventorAddin.Core.HoleTable;
using InventorAddin.Core.Holes;
using InventorAddin.Core.ViewModels;

namespace InventorAddin.Core.Tests.HoleTable;

/// <summary>A source with one sheet and one view that holds stored tables. Any write method throws, so a check that writes fails.</summary>
internal sealed class StoredFakeSource : IHoleTableSource
{
    public List<string> Calls { get; } = [];

    public List<HoleTableHole> Holes { get; set; } =
    [
        new("P#1", 1, "F", HoleUnits.Metric, HoleTableType.Plain, true, 0.6, HoleTermination.ThroughAll, null, false, null,
            null, null, null, null, null, null, false, null, 1, 1),
        new("P#2", 1, "F", HoleUnits.Metric, HoleTableType.Plain, true, 0.6, HoleTermination.ThroughAll, null, false, null,
            null, null, null, null, null, null, false, null, 2, 1),
    ];

    public HoleTableState? Stored { get; set; }

    public string? AnalyzeFailure { get; set; }

    public bool UpdateThrows { get; set; }

    public string DrawingName => "Plate.idw";

    public StoredFakeSource(bool store = true)
    {
        if (store)
        {
            Stored = StateFor(Holes);
        }
    }

    public static HoleTableState StateFor(IEnumerable<HoleTableHole> holes) =>
        HoleTableState.From("Sheet:1", "VIEW1", HoleTableEdits.None,
            HoleTablePlan.Build(holes, null, new SheetPoint(0, 0), 1, HoleUnits.Metric));

    public IReadOnlyList<HoleTableSheetInfo> GetSheets() => throw new InvalidOperationException("write");

    public IReadOnlyList<HoleTableViewInfo> GetViews(string sheetKey) => throw new InvalidOperationException("write");

    public IReadOnlyList<HoleTableViewInfo> GetKeyedViews(string sheetKey)
    {
        Calls.Add("getkeyedviews " + sheetKey);
        return [new("v1", "VIEW1")];
    }

    public HoleTableViewAnalysis Analyze(string sheetKey, string viewKey)
    {
        Calls.Add("analyze");
        return AnalyzeFailure is null
            ? new(Holes, [new SheetPoint(0, 0)], 1, HoleUnits.Metric, 0)
            : HoleTableViewAnalysis.Failed(AnalyzeFailure);
    }

    public HoleTableState? ReadState(string sheetKey, string viewKey)
    {
        Calls.Add("readstate");
        return Stored;
    }

    public string GenerateTable(string sheetKey, string viewKey, HoleTableKind kind, HoleTablePlan plan, HoleTableState state) =>
        throw new InvalidOperationException("write");

    public string CreateTables(string sheetKey, string viewKey, HoleTablePlan plan, HoleTableState state) =>
        throw new InvalidOperationException("write");

    public string UpdateTables(string sheetKey, string viewKey, HoleTablePlan plan, HoleTableState state)
    {
        Calls.Add("update");
        if (UpdateThrows) throw new InvalidOperationException("boom");
        Stored = state;
        return "Updated.";
    }

    public string AlignTables(string sheetKey, string viewKey) => throw new InvalidOperationException("write");

    public void Move(string id, double x) =>
        Holes = Holes.Select(h => h.Id == id ? h with { SheetXCm = x } : h).ToList();
}

public class HoleTableStoredCheckTests
{
    private static readonly HoleTableSheetInfo[] Sheets = [new("s1", "Sheet:1", true)];

    [Fact]
    public void Run_UpToDate_ListsNothing_AndCallsOnlyReads()
    {
        var source = new StoredFakeSource();
        var result = HoleTableStoredCheck.Run(source, Sheets);
        Assert.Empty(result.OutOfDate);
        Assert.Empty(result.CouldNotCheck);
        Assert.NotEmpty(source.Calls);
        Assert.All(source.Calls, c => Assert.True(c.StartsWith("getkeyedviews") || c == "readstate" || c == "analyze", c));
    }

    [Fact]
    public void Run_MovedHole_IsOutOfDate()
    {
        var source = new StoredFakeSource();
        source.Move("P#1", 3);
        var item = Assert.Single(HoleTableStoredCheck.Run(source, Sheets).OutOfDate);
        Assert.Equal("VIEW1 on Sheet:1: 1 hole moved.", item.Text);
    }

    [Fact]
    public void Run_ViewWithNoStoredState_IsSkipped()
    {
        var result = HoleTableStoredCheck.Run(new StoredFakeSource(store: false), Sheets);
        Assert.Empty(result.OutOfDate);
        Assert.Empty(result.CouldNotCheck);
    }

    [Fact]
    public void Run_UnreadableView_IsCouldNotCheck()
    {
        var source = new StoredFakeSource { AnalyzeFailure = "No origin." };
        var result = HoleTableStoredCheck.Run(source, Sheets);
        Assert.Empty(result.OutOfDate);
        Assert.Single(result.CouldNotCheck);
    }

    [Fact]
    public void GetKeyedSheets_DefaultsToGetSheets()
    {
        IHoleTableSource source = new DefaultSheets();
        Assert.Single(source.GetKeyedSheets());
    }

    private sealed class DefaultSheets : IHoleTableSource
    {
        public string DrawingName => "";
        public IReadOnlyList<HoleTableSheetInfo> GetSheets() => [new("a", "A", true)];
        public IReadOnlyList<HoleTableViewInfo> GetViews(string sheetKey) => [];
        public HoleTableViewAnalysis Analyze(string sheetKey, string viewKey) => throw new NotSupportedException();
        public string GenerateTable(string sheetKey, string viewKey, HoleTableKind kind, HoleTablePlan plan, HoleTableState state) => throw new NotSupportedException();
        public string CreateTables(string sheetKey, string viewKey, HoleTablePlan plan, HoleTableState state) => throw new NotSupportedException();
        public string UpdateTables(string sheetKey, string viewKey, HoleTablePlan plan, HoleTableState state) => throw new NotSupportedException();
        public HoleTableState? ReadState(string sheetKey, string viewKey) => null;
        public string AlignTables(string sheetKey, string viewKey) => throw new NotSupportedException();
    }
}

public class HoleTableStoredUpdateTests
{
    [Fact]
    public void Run_UpdatesAMovedView()
    {
        var source = new StoredFakeSource();
        source.Move("P#1", 3);
        Assert.True(HoleTableStoredUpdate.Run(source, "s1", "v1", "Sheet:1", "VIEW1", out var text));
        Assert.StartsWith("Updated.", text);
        Assert.Contains("update", source.Calls);
    }

    [Fact]
    public void Run_NoStoredState_FailsWithTheText()
    {
        Assert.False(HoleTableStoredUpdate.Run(new StoredFakeSource(false), "s1", "v1", "S", "V", out var text));
        Assert.Equal(HoleTableMessages.NoStoredTables, text);
    }

    [Fact]
    public void Run_AnalysisFailure_ReturnsItsMessage()
    {
        var source = new StoredFakeSource { AnalyzeFailure = "No origin." };
        Assert.False(HoleTableStoredUpdate.Run(source, "s1", "v1", "S", "V", out var text));
        Assert.Equal("No origin.", text);
    }

    [Fact]
    public void Run_SourceThrows_IsCaught()
    {
        var source = new StoredFakeSource { UpdateThrows = true };
        source.Move("P#1", 3);
        Assert.False(HoleTableStoredUpdate.Run(source, "s1", "v1", "S", "V", out var text));
        Assert.Equal("Could not update the tables: boom", text);
    }
}

public class HoleTableAutoCheckTests
{
    private static readonly DateTime T0 = new(2026, 1, 1, 9, 0, 0);
    private static readonly TimeSpan Quiet = HoleTableAutoCheck.QuietTime;
    private const string A = @"C:\Work\A.idw";
    private const string B = @"C:\Work\B.idw";

    private static HoleTableAutoCheckContext Ready(string? active = A) => new(active, true, false, false);

    private static HoleTableStoredViewCheck View(string key, string reason) => new("s1", "Sheet:1", key, key.ToUpperInvariant(), reason);

    private static HoleTableStoredCheckResult Out(params HoleTableStoredViewCheck[] views) => new(views, []);

    [Fact]
    public void Activation_IsDueAfterTheQuietTime_NotBefore()
    {
        var check = new HoleTableAutoCheck();
        check.DrawingActivated(A, T0);
        Assert.Null(check.TakeDue(Ready(), T0));
        Assert.Null(check.TakeDue(Ready(), T0 + Quiet - TimeSpan.FromMilliseconds(1)));
        Assert.Equal(A, check.TakeDue(Ready(), T0 + Quiet));
    }

    [Fact]
    public void ATakenCheck_IsNotDueAgain()
    {
        var check = new HoleTableAutoCheck();
        check.DrawingActivated(A, T0);
        Assert.Equal(A, check.TakeDue(Ready(), T0 + Quiet));
        Assert.Null(check.TakeDue(Ready(), T0 + Quiet + Quiet));
        Assert.False(check.IsPending(A));
    }

    [Fact]
    public void ASecondEvent_RestartsTheQuietTime()
    {
        var check = new HoleTableAutoCheck();
        check.DrawingActivated(A, T0);
        check.DrawingUpdated(A, T0 + TimeSpan.FromSeconds(1));
        Assert.Null(check.TakeDue(Ready(), T0 + Quiet));
        Assert.Equal(A, check.TakeDue(Ready(), T0 + TimeSpan.FromSeconds(1) + Quiet));
    }

    [Fact]
    public void SettingOff_NeverDue_AndDropsPendingChecks()
    {
        var check = new HoleTableAutoCheck(enabled: false);
        check.DrawingActivated(A, T0);
        Assert.Null(check.TakeDue(Ready(), T0 + Quiet));
        Assert.False(check.IsPending(A));

        check.Enabled = true;
        check.DrawingActivated(B, T0);
        check.Enabled = false;
        check.Enabled = true;
        Assert.Null(check.TakeDue(Ready(B), T0 + Quiet));
    }

    [Theory]
    [InlineData(false, true, false, false)] // not active
    [InlineData(true, false, false, false)] // editing something else
    [InlineData(true, true, true, false)] // a command is running
    [InlineData(true, true, false, true)] // an AWB window is open
    public void ADueCheck_StaysDueUntilTheConditionClears(bool isActive, bool editingItself, bool command, bool window)
    {
        var check = new HoleTableAutoCheck();
        check.DrawingActivated(A, T0);
        var blocked = new HoleTableAutoCheckContext(isActive ? A : B, editingItself, command, window);
        Assert.Null(check.TakeDue(blocked, T0 + Quiet));
        Assert.Null(check.TakeDue(blocked, T0 + Quiet * 5));
        Assert.True(check.IsPending(A));
        Assert.Equal(A, check.TakeDue(Ready(), T0 + Quiet * 6));
    }

    [Fact]
    public void NoActiveDrawing_NothingRuns()
    {
        var check = new HoleTableAutoCheck();
        check.DrawingActivated(A, T0);
        Assert.Null(check.TakeDue(new HoleTableAutoCheckContext(null, true, false, false), T0 + Quiet));
    }

    [Fact]
    public void AnUpdateWhileNotActive_RunsOnTheNextActivation()
    {
        var check = new HoleTableAutoCheck();
        check.DrawingUpdated(A, T0);
        Assert.Null(check.TakeDue(Ready(B), T0 + Quiet * 3));
        check.DrawingActivated(A, T0 + Quiet * 4);
        Assert.Null(check.TakeDue(Ready(A), T0 + Quiet * 4));
        Assert.Equal(A, check.TakeDue(Ready(A), T0 + Quiet * 5));
    }

    [Fact]
    public void ADrawingClosed_IsForgotten()
    {
        var check = new HoleTableAutoCheck();
        check.DrawingActivated(A, T0);
        check.Later(A, [View("v1", "x")]);
        check.DrawingClosed(A);
        Assert.Null(check.TakeDue(Ready(), T0 + Quiet));
        Assert.True(check.ShouldPrompt(A, Out(View("v1", "x"))));
    }

    [Fact]
    public void DrawingNames_CompareIgnoringCase()
    {
        var check = new HoleTableAutoCheck();
        check.DrawingActivated(A, T0);
        Assert.Equal(A, check.TakeDue(Ready(A.ToUpperInvariant()), T0 + Quiet), ignoreCase: true);
        check.Later(A, [View("v1", "x")]);
        Assert.False(check.ShouldPrompt(A.ToLowerInvariant(), Out(View("v1", "x"))));
    }

    [Fact]
    public void Prompt_OnlyWhenSomethingIsOutOfDate()
    {
        var check = new HoleTableAutoCheck();
        Assert.False(check.ShouldPrompt(A, new([], [])));
        Assert.False(check.ShouldPrompt(A, new([], [View("v1", "could not")])));
        Assert.True(check.ShouldPrompt(A, Out(View("v1", "1 hole moved."))));
    }

    [Fact]
    public void Later_ThenTheSameResult_DoesNotPrompt_EvenInAnotherOrder()
    {
        var check = new HoleTableAutoCheck();
        var one = View("v1", "1 hole moved.");
        var two = View("v2", "1 hole added.");
        check.Later(A, [one, two]);
        Assert.False(check.ShouldPrompt(A, Out(two, one)));
    }

    [Fact]
    public void Later_ThenADifferentReasonOrView_Prompts()
    {
        var check = new HoleTableAutoCheck();
        check.Later(A, [View("v1", "1 hole moved.")]);
        Assert.True(check.ShouldPrompt(A, Out(View("v1", "2 holes moved."))));
        Assert.True(check.ShouldPrompt(A, Out(View("v1", "1 hole moved."), View("v2", "1 hole added."))));
        Assert.True(check.ShouldPrompt(A, Out(View("v2", "1 hole moved."))));
    }

    [Fact]
    public void Later_IsPerDrawing()
    {
        var check = new HoleTableAutoCheck();
        check.Later(A, [View("v1", "x")]);
        Assert.True(check.ShouldPrompt(B, Out(View("v1", "x"))));
    }

    [Fact]
    public void UpdateNow_ThenTheSameResultAgain_Prompts()
    {
        var check = new HoleTableAutoCheck();
        check.Later(A, [View("v1", "x")]);
        check.UpdatedNow(A);
        Assert.True(check.ShouldPrompt(A, Out(View("v1", "x"))));
    }

    [Fact]
    public void ACheckThatFindsNothing_ForgetsLater()
    {
        var check = new HoleTableAutoCheck();
        check.Later(A, [View("v1", "x")]);
        Assert.False(check.ShouldPrompt(A, new([], [])));
        Assert.True(check.ShouldPrompt(A, Out(View("v1", "x"))));
    }

    [Fact]
    public void WizardClosed_CountsAsLater()
    {
        var check = new HoleTableAutoCheck();
        var result = Out(View("v1", "1 hole moved."));
        check.WizardClosed(A, result);
        Assert.False(check.ShouldPrompt(A, result));
        Assert.True(check.ShouldPrompt(A, Out(View("v1", "2 holes moved."))));
    }

    [Fact]
    public void WizardClosed_WithNothingOutOfDate_ForgetsLater()
    {
        var check = new HoleTableAutoCheck();
        check.Later(A, [View("v1", "x")]);
        check.WizardClosed(A, new([], []));
        Assert.True(check.ShouldPrompt(A, Out(View("v1", "x"))));
    }
}

public class HoleTableAutoCheckPendingTests
{
    private static readonly DateTime T0 = new(2026, 1, 1, 12, 0, 0);

    [Fact]
    public void AnyPending_follows_the_due_list()
    {
        var policy = new HoleTableAutoCheck();
        Assert.False(policy.AnyPending);

        policy.DrawingActivated("a.idw", T0);
        Assert.True(policy.AnyPending);

        var context = new HoleTableAutoCheckContext("a.idw", true, false, false);
        Assert.Equal("a.idw", policy.TakeDue(context, T0 + HoleTableAutoCheck.QuietTime));
        Assert.False(policy.AnyPending);
    }

    [Fact]
    public void AnyPending_is_cleared_by_close_and_by_switching_the_setting_off()
    {
        var policy = new HoleTableAutoCheck();
        policy.DrawingUpdated("a.idw", T0);
        policy.DrawingUpdated("b.idw", T0);
        policy.DrawingClosed("a.idw");
        Assert.True(policy.AnyPending);

        policy.Enabled = false;
        Assert.False(policy.AnyPending);
    }
}

public class HoleTableAutoCheckLogLineTests
{
    private static HoleTableStoredViewCheck View(string reason) => new("s", "Sheet:1", "v", "VIEW1", reason);

    [Fact]
    public void One_view_out_of_date_names_it_and_the_time()
    {
        var result = new HoleTableStoredCheckResult([View("2 holes moved.")], []);
        Assert.Equal(
            "Hole table check: Plate.idw, 1 view out of date (VIEW1 on Sheet:1: 2 holes moved.), 0.4 s",
            HoleTableMessages.AutoCheckLogLine("Plate.idw", result, 0.4));
    }

    [Fact]
    public void Nothing_out_of_date_says_so()
    {
        var result = new HoleTableStoredCheckResult([], []);
        Assert.Equal("Hole table check: Plate.idw, no views out of date, 0.0 s", HoleTableMessages.AutoCheckLogLine("Plate.idw", result, 0.01));
    }

    [Fact]
    public void Several_views_and_unreadable_views_are_listed()
    {
        var result = new HoleTableStoredCheckResult(
            [View("1 hole added."), new("s", "Sheet:2", "w", "VIEW2", "1 hole removed.")],
            [new("s", "Sheet:1", "x", "VIEW3", "Could not check the stored tables: x")]);
        Assert.Equal(
            "Hole table check: Plate.idw, 2 views out of date (VIEW1 on Sheet:1: 1 hole added.; VIEW2 on Sheet:2: 1 hole removed.), " +
            "1 view could not be checked (VIEW3 on Sheet:1: Could not check the stored tables: x), 1.2 s",
            HoleTableMessages.AutoCheckLogLine("Plate.idw", result, 1.23));
    }
}
