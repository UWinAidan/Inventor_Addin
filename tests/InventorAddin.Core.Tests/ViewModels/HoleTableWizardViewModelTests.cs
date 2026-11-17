using InventorAddin.Core.HoleTable;
using InventorAddin.Core.Holes;
using InventorAddin.Core.ViewModels;

namespace InventorAddin.Core.Tests.ViewModels;

public class HoleTableWizardViewModelTests
{
    private static HoleTableHole Plain(string id, double dia, double x = 0, double y = 0, bool precision = false) => new(
        id, 1, "F", HoleUnits.Metric, HoleTableType.Plain, precision, dia, HoleTermination.ThroughAll, null, false, null,
        null, null, null, null, null, null, false, null, x, y);

    private sealed class FakeSource : IHoleTableSource
    {
        public List<HoleTableSheetInfo> SheetList { get; } = [new("s1", "Sheet:1", false), new("s2", "Sheet:2", true)];

        public Dictionary<string, HoleTableViewInfo[]> ViewsBySheet { get; } = new()
        {
            ["s1"] = [new("v1", "VIEW1")],
            ["s2"] = [new("v2", "VIEW2"), new("v3", "VIEW3")],
        };

        public HoleTableViewAnalysis Analysis { get; set; } = new(
            [
                Plain("P#1", 0.6, 1, 1, true), Plain("P#2", 0.6, 2, 1, true),
                Plain("C#1", 0.5, 1, 2), Plain("C#2", 0.5, 2, 2), Plain("D#1", 0.9, 5, 5),
            ],
            [new SheetPoint(0, 0)], 1, HoleUnits.Metric, 0);

        public List<string> Calls { get; } = [];

        public bool Throw { get; set; }

        public string DrawingName => "Plate.idw";

        public IReadOnlyList<HoleTableSheetInfo> GetSheets() => SheetList;

        public IReadOnlyList<HoleTableViewInfo> GetViews(string sheetKey) => ViewsBySheet[sheetKey];

        public HoleTableViewAnalysis Analyze(string sheetKey, string viewKey)
        {
            Calls.Add($"analyze {sheetKey}/{viewKey}");
            if (Throw) throw new InvalidOperationException("boom");
            return Analysis;
        }

        public string GenerateTable(string sheetKey, string viewKey, HoleTableKind kind, HoleTablePlan plan)
        {
            Calls.Add($"generate {kind} {sheetKey}/{viewKey}");
            if (Throw) throw new InvalidOperationException("boom");
            return "Generated " + kind;
        }

        public string CreateTables(string sheetKey, string viewKey, HoleTablePlan plan)
        {
            Calls.Add($"create {sheetKey}/{viewKey}");
            if (Throw) throw new InvalidOperationException("boom");
            return "Created";
        }

        public string AlignTables(string sheetKey, string viewKey)
        {
            Calls.Add($"align {sheetKey}/{viewKey}");
            if (Throw) throw new InvalidOperationException("boom");
            return "Aligned";
        }
    }

    private static (HoleTableWizardViewModel Vm, FakeSource Source) Analyzed()
    {
        var source = new FakeSource();
        var vm = new HoleTableWizardViewModel(source);
        vm.AnalyzeCommand.Execute(null);
        return (vm, source);
    }

    private static string[] Tags(IEnumerable<HoleTableWizardRow> rows) => rows.Select(r => r.Tag).ToArray();

    [Fact]
    public void Header_selects_the_active_sheet_and_lists_its_views()
    {
        var vm = new HoleTableWizardViewModel(new FakeSource());
        Assert.Equal("Plate.idw", vm.DrawingName);
        Assert.Equal("s2", vm.SelectedSheet);
        Assert.Equal(new[] { "VIEW2", "VIEW3" }, vm.Views.Select(v => v.Name));
        Assert.Equal("v2", vm.SelectedView);

        vm.SelectedSheet = "s1";
        Assert.Equal(new[] { "VIEW1" }, vm.Views.Select(v => v.Name));
        Assert.Equal("v1", vm.SelectedView);
    }

    [Fact]
    public void Analyze_fills_both_lists_and_reports_found_and_left_out()
    {
        var source = new FakeSource();
        source.Analysis = source.Analysis with { LeftOutCount = 2 };
        var vm = new HoleTableWizardViewModel(source);
        vm.AnalyzeCommand.Execute(null);

        Assert.Equal(new[] { "A1", "A2" }, Tags(vm.PrecisionRows));
        Assert.Equal(new[] { "B1", "B2", "C1" }, Tags(vm.ClearanceRows));
        Assert.Equal("Found 5 holes. " + HoleTableMessages.HolesLeftOut(2), vm.Status);
        Assert.Contains("analyze s2/v2", source.Calls);
        Assert.False(string.IsNullOrEmpty(vm.PrecisionRows[0].Description));
    }

    [Fact]
    public void Analyze_with_no_origin_stops_and_leaves_lists_empty()
    {
        var source = new FakeSource();
        source.Analysis = source.Analysis with { Origins = [] };
        var vm = new HoleTableWizardViewModel(source);
        vm.AnalyzeCommand.Execute(null);

        Assert.Equal(HoleTableMessages.NoOrdinateOrigin, vm.Status);
        Assert.Empty(vm.PrecisionRows);
        Assert.Empty(vm.ClearanceRows);
        Assert.False(vm.CreateTablesCommand.CanExecute(null));
    }

    [Fact]
    public void Analyze_with_several_origins_stops()
    {
        var source = new FakeSource();
        source.Analysis = source.Analysis with { Origins = [new SheetPoint(0, 0), new SheetPoint(5, 5)] };
        var vm = new HoleTableWizardViewModel(source);
        vm.AnalyzeCommand.Execute(null);

        Assert.Equal(HoleTableMessages.MoreThanOneOrdinateOrigin, vm.Status);
        Assert.Empty(vm.PrecisionRows);
        Assert.Empty(vm.ClearanceRows);
    }

    [Fact]
    public void Analyze_failure_shows_the_message()
    {
        var source = new FakeSource();
        source.Analysis = HoleTableViewAnalysis.Failed("The view could not be read.");
        var vm = new HoleTableWizardViewModel(source);
        vm.AnalyzeCommand.Execute(null);
        Assert.Equal("The view could not be read.", vm.Status);
        Assert.Empty(vm.PrecisionRows);
    }

    [Fact]
    public void Analyze_needs_a_view()
    {
        var source = new FakeSource();
        source.ViewsBySheet["s2"] = [];
        var vm = new HoleTableWizardViewModel(source);
        Assert.False(vm.AnalyzeCommand.CanExecute(null));
    }

    [Fact]
    public void Swap_moves_the_hole_and_the_selection_follows()
    {
        var (vm, _) = Analyzed();
        vm.SelectedClearanceRow = vm.ClearanceRows.Single(r => r.HoleId == "D#1");
        vm.SwapCommand.Execute(null);

        Assert.Equal(3, vm.PrecisionRows.Count);
        Assert.Equal(2, vm.ClearanceRows.Count);
        Assert.Null(vm.SelectedClearanceRow);
        Assert.Equal("D#1", vm.SelectedPrecisionRow?.HoleId);

        vm.SwapCommand.Execute(null);
        Assert.Equal(2, vm.PrecisionRows.Count);
        Assert.Equal("D#1", vm.SelectedClearanceRow?.HoleId);
    }

    [Fact]
    public void Delete_removes_the_hole_and_renumbers()
    {
        var (vm, _) = Analyzed();
        vm.SelectedClearanceRow = vm.ClearanceRows.Single(r => r.HoleId == "C#1");
        vm.DeleteCommand.Execute(null);

        Assert.Equal(new[] { "B1", "C1" }, Tags(vm.ClearanceRows));
        Assert.Null(vm.SelectedRow);
        Assert.False(vm.DeleteCommand.CanExecute(null));
    }

    [Fact]
    public void Split_gives_the_hole_its_own_group_and_keeps_the_selection()
    {
        var (vm, _) = Analyzed();
        vm.SelectedClearanceRow = vm.ClearanceRows.Single(r => r.HoleId == "C#2");
        vm.SelectedClearanceRow!.IsSplit = true;

        Assert.Equal(3, vm.ClearanceRows.Count);
        var split = vm.ClearanceRows.Single(r => r.HoleId == "C#2");
        Assert.True(split.IsSplit);
        Assert.Equal(1, vm.ClearanceRows.Count(r => r.IsSplit));
        Assert.Same(split, vm.SelectedClearanceRow);
        Assert.EndsWith("1", split.Tag);
    }

    [Fact]
    public void Renumber_rebuilds_without_changing_the_result()
    {
        var (vm, _) = Analyzed();
        var before = Tags(vm.ClearanceRows);
        vm.SelectedClearanceRow = vm.ClearanceRows[0];
        vm.RenumberCommand.Execute(null);
        Assert.Equal(before, Tags(vm.ClearanceRows));
        Assert.Equal(before[0], vm.SelectedClearanceRow?.Tag);
    }

    [Fact]
    public void Notes_survive_a_swap_and_a_split_elsewhere()
    {
        var (vm, _) = Analyzed();
        vm.PrecisionRows[0].Notes = "Ream in place";
        Assert.All(vm.PrecisionRows, r => Assert.Equal("Ream in place", r.Notes));

        vm.SelectedClearanceRow = vm.ClearanceRows.Single(r => r.HoleId == "D#1");
        vm.SwapCommand.Execute(null);
        vm.ClearanceRows.First(r => r.HoleId == "C#1").IsSplit = true;

        var precisionGroupRows = vm.PrecisionRows.Where(r => r.HoleId.StartsWith("P#")).ToList();
        Assert.All(precisionGroupRows, r => Assert.Equal("Ream in place", r.Notes));
        Assert.Equal("", vm.ClearanceRows.First(r => r.HoleId == "C#2").Notes);
    }

    [Fact]
    public void Split_group_notes_are_kept_for_that_hole()
    {
        var (vm, _) = Analyzed();
        vm.ClearanceRows.First(r => r.HoleId == "C#1").IsSplit = true;
        vm.ClearanceRows.First(r => r.HoleId == "C#1").Notes = "Deburr";
        Assert.Equal("Deburr", vm.ClearanceRows.First(r => r.HoleId == "C#1").Notes);
        Assert.Equal("", vm.ClearanceRows.First(r => r.HoleId == "C#2").Notes);
    }

    [Fact]
    public void Enabled_states_follow_the_selection_and_the_lists()
    {
        var source = new FakeSource();
        var vm = new HoleTableWizardViewModel(source);
        var raised = new List<string>();
        vm.SwapCommand.CanExecuteChanged += (_, _) => raised.Add("swap");
        vm.CreateTablesCommand.CanExecuteChanged += (_, _) => raised.Add("create");

        Assert.False(vm.SwapCommand.CanExecute(null));
        Assert.False(vm.DeleteCommand.CanExecute(null));
        Assert.False(vm.GeneratePrecisionCommand.CanExecute(null));
        Assert.False(vm.GenerateClearanceCommand.CanExecute(null));
        Assert.False(vm.CreateTablesCommand.CanExecute(null));

        vm.AnalyzeCommand.Execute(null);
        Assert.True(vm.GeneratePrecisionCommand.CanExecute(null));
        Assert.True(vm.GenerateClearanceCommand.CanExecute(null));
        Assert.True(vm.CreateTablesCommand.CanExecute(null));
        Assert.False(vm.SwapCommand.CanExecute(null));
        Assert.Contains("create", raised);

        raised.Clear();
        vm.SelectedPrecisionRow = vm.PrecisionRows[0];
        Assert.True(vm.SwapCommand.CanExecute(null));
        Assert.True(vm.DeleteCommand.CanExecute(null));
        Assert.Contains("swap", raised);

        // Delete both precision holes: Generate Precision turns off, Clearance stays on.
        vm.DeleteCommand.Execute(null);
        vm.SelectedPrecisionRow = vm.PrecisionRows[0];
        vm.DeleteCommand.Execute(null);
        Assert.False(vm.GeneratePrecisionCommand.CanExecute(null));
        Assert.True(vm.GenerateClearanceCommand.CanExecute(null));
        Assert.True(vm.CreateTablesCommand.CanExecute(null));
    }

    [Fact]
    public void Generate_create_and_align_call_the_source_and_show_its_result()
    {
        var (vm, source) = Analyzed();
        vm.GeneratePrecisionCommand.Execute(null);
        Assert.Equal("Generated Precision", vm.Status);
        vm.GenerateClearanceCommand.Execute(null);
        vm.CreateTablesCommand.Execute(null);
        Assert.Equal("Created", vm.Status);
        vm.AlignCommand.Execute(null);
        Assert.Equal("Aligned", vm.Status);
        Assert.Equal(new[] { "generate Precision s2/v2", "generate Clearance s2/v2", "create s2/v2", "align s2/v2" }, source.Calls.Skip(1));
    }

    [Fact]
    public void Changing_the_view_clears_the_lists()
    {
        var (vm, _) = Analyzed();
        vm.SelectedView = "v3";
        Assert.Empty(vm.PrecisionRows);
        Assert.Empty(vm.ClearanceRows);
        Assert.False(vm.CreateTablesCommand.CanExecute(null));
    }

    [Fact]
    public void Cancel_raises_close_requested_and_changes_nothing()
    {
        var (vm, source) = Analyzed();
        var closed = 0;
        vm.CancelRequested += (_, _) => closed++;
        vm.CancelCommand.Execute(null);
        Assert.Equal(1, closed);
        Assert.Single(source.Calls);
    }

    [Fact]
    public void Align_needs_a_view()
    {
        var source = new FakeSource();
        source.ViewsBySheet["s2"] = [];
        var vm = new HoleTableWizardViewModel(source);
        Assert.False(vm.AlignCommand.CanExecute(null));
        Assert.True(new HoleTableWizardViewModel(new FakeSource()).AlignCommand.CanExecute(null));
    }

    [Fact]
    public void A_throwing_source_gives_a_status_message_and_keeps_state()
    {
        var source = new FakeSource();
        var vm = new HoleTableWizardViewModel(source);
        source.Throw = true;
        vm.AnalyzeCommand.Execute(null);
        Assert.Contains("boom", vm.Status);
        Assert.Empty(vm.PrecisionRows);

        source.Throw = false;
        vm.AnalyzeCommand.Execute(null);
        Assert.Equal(2, vm.PrecisionRows.Count);

        source.Throw = true;
        vm.GeneratePrecisionCommand.Execute(null);
        Assert.Contains("boom", vm.Status);
        vm.CreateTablesCommand.Execute(null);
        Assert.Contains("boom", vm.Status);
        vm.AlignCommand.Execute(null);
        Assert.Contains("boom", vm.Status);
        Assert.Equal(2, vm.PrecisionRows.Count);
        Assert.NotNull(vm.Plan);
    }
}
