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

        /// <summary>GetViews and ReadState calls, kept apart from the calls that draw or analyse.</summary>
        public List<string> ReadCalls { get; } = [];

        public bool Throw { get; set; }

        public string DrawingName => "Plate.idw";

        public IReadOnlyList<HoleTableSheetInfo> GetSheets() => SheetList;

        public IReadOnlyList<HoleTableViewInfo> GetViews(string sheetKey)
        {
            ReadCalls.Add($"getviews {sheetKey}");
            return ViewsBySheet[sheetKey];
        }

        public HoleTableViewAnalysis Analyze(string sheetKey, string viewKey)
        {
            Calls.Add($"analyze {sheetKey}/{viewKey}");
            if (Throw) throw new InvalidOperationException("boom");
            return Analysis;
        }

        public string GenerateTable(string sheetKey, string viewKey, HoleTableKind kind, HoleTablePlan plan, HoleTableState state)
        {
            Calls.Add($"generate {kind} {sheetKey}/{viewKey}");
            Stored[(sheetKey, viewKey)] = state;
            if (Throw) throw new InvalidOperationException("boom");
            return "Generated " + kind;
        }

        public string CreateTables(string sheetKey, string viewKey, HoleTablePlan plan, HoleTableState state)
        {
            Calls.Add($"create {sheetKey}/{viewKey}");
            CreatedState = state;
            Stored[(sheetKey, viewKey)] = state;
            if (Throw) throw new InvalidOperationException("boom");
            return "Created";
        }

        public Dictionary<(string, string), HoleTableState> Stored { get; } = [];

        public HoleTablePlan? UpdatedPlan { get; private set; }

        public HoleTableState? UpdatedState { get; private set; }

        public HoleTableState? CreatedState { get; private set; }

        public string UpdateTables(string sheetKey, string viewKey, HoleTablePlan plan, HoleTableState state)
        {
            Calls.Add($"update {sheetKey}/{viewKey}");
            if (Throw) throw new InvalidOperationException("boom");
            UpdatedPlan = plan;
            UpdatedState = state;
            Stored[(sheetKey, viewKey)] = state;
            return "Updated.";
        }

        public HoleTableState? ReadState(string sheetKey, string viewKey)
        {
            ReadCalls.Add($"readstate {sheetKey}/{viewKey}");
            return Stored.GetValueOrDefault((sheetKey, viewKey));
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
    public void Analyze_with_an_unreadable_origin_says_it_could_not_be_read()
    {
        var source = new FakeSource();
        source.Analysis = source.Analysis with { Origins = [], OriginUnreadable = true };
        var vm = new HoleTableWizardViewModel(source);
        vm.AnalyzeCommand.Execute(null);

        Assert.Equal(HoleTableMessages.OrdinateOriginUnreadable, vm.Status);
        Assert.Empty(vm.PrecisionRows);
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

    // Task 162: stored state, Update and the Edit Table tab.

    private static string[] TagList(HoleTableWizardViewModel vm) =>
        vm.PrecisionRows.Concat(vm.ClearanceRows).Select(r => r.Tag).ToArray();

    private static Dictionary<string, string> TagMap(HoleTableWizardViewModel vm) =>
        vm.PrecisionRows.Concat(vm.ClearanceRows).ToDictionary(r => r.HoleId, r => r.Tag);

    private static HoleTableWizardViewModel OpenStored(FakeSource source)
    {
        var vm = new HoleTableWizardViewModel(source);
        vm.SelectedStoredView = vm.StoredViews[0];
        vm.OpenStoredViewCommand.Execute(null);
        return vm;
    }

    // Analyses, swaps the first clearance hole, adds a note, creates; returns the source holding the stored state.
    private static FakeSource StoredSource()
    {
        var (vm, source) = Analyzed();
        vm.SelectedClearanceRow = vm.ClearanceRows[0];
        vm.SwapCommand.Execute(null);
        vm.Groups.First(g => g.Table == HoleTableKind.Clearance).Notes = "Deburr";
        vm.CreateTablesCommand.Execute(null);
        return source;
    }

    [Fact]
    public void Create_stores_the_state_with_names_tags_and_edits()
    {
        var (vm, source) = Analyzed();
        vm.SelectedClearanceRow = vm.ClearanceRows[2];
        vm.SwapCommand.Execute(null);
        vm.CreateTablesCommand.Execute(null);

        var state = source.CreatedState!;
        Assert.Equal("Sheet:2", state.SheetName);
        Assert.Equal("VIEW2", state.ViewName);
        Assert.Equal(5, state.Tags.ByHole.Count);
        Assert.Single(state.Edits.SwappedIds);
        Assert.Contains(vm.StoredViews, v => v.Key == "v2");
    }

    [Fact]
    public void Generate_one_table_also_stores_the_state()
    {
        var (vm, source) = Analyzed();
        vm.GeneratePrecisionCommand.Execute(null);
        Assert.Contains("generate Precision s2/v2", source.Calls);
    }

    [Fact]
    public void Edit_tab_lists_only_views_with_stored_tables()
    {
        var source = new FakeSource();
        source.Stored[("s2", "v3")] = HoleTableState.From("Sheet:2", "VIEW3", HoleTableEdits.None, HoleTablePlan.Build([], null, new SheetPoint(0, 0), 1, HoleUnits.Metric));
        var vm = new HoleTableWizardViewModel(source);
        Assert.Equal(new[] { "v3" }, vm.StoredViews.Select(v => v.Key));
    }

    [Fact]
    public void Reopening_a_stored_view_shows_the_stored_tags_and_notes()
    {
        var source = StoredSource();
        var created = source.CreatedState!;
        var vm = OpenStored(source);

        Assert.Equal(created.Tags.ByHole.ToDictionary(p => p.Key, p => p.Value.Text), TagMap(vm));
        Assert.Contains(vm.Groups, g => g.Notes == "Deburr");
        Assert.Contains(vm.ClearanceRows.Concat(vm.PrecisionRows), r => r.Notes == "Deburr");
        Assert.Contains("0 holes added", vm.Status);
    }

    [Fact]
    public void Edits_after_opening_keep_the_tags_of_the_other_holes()
    {
        var vm = OpenStored(StoredSource());
        var before = TagMap(vm);

        var target = vm.ClearanceRows[^1];
        vm.SelectedClearanceRow = target;
        vm.DeleteCommand.Execute(null);
        var after = TagMap(vm);
        Assert.DoesNotContain(target.HoleId, after.Keys);
        foreach (var (id, tag) in after)
        {
            Assert.Equal(before[id], tag);
        }
    }

    [Fact]
    public void Renumber_after_opening_closes_the_gaps()
    {
        var vm = OpenStored(StoredSource());
        Assert.Equal(new[] { "A1", "B1", "B2" }, TagList(vm).Take(3));
        vm.SelectedPrecisionRow = vm.PrecisionRows[1];
        vm.DeleteCommand.Execute(null);
        var gapped = vm.Plan!.AllGroups.Any(g => !g.Rows.Select(r => r.Number).SequenceEqual(Enumerable.Range(1, g.Rows.Count)));
        Assert.True(gapped);
        vm.RenumberCommand.Execute(null);

        Assert.All(vm.Plan.AllGroups, g => Assert.Equal(Enumerable.Range(1, g.Rows.Count), g.Rows.Select(r => r.Number)));
        Assert.Equal(new[] { "A1", "B1" }, vm.PrecisionRows.Select(r => r.Tag));
    }

    [Fact]
    public void Update_calls_the_source_with_the_refreshed_plan_and_keeps_tags()
    {
        var source = StoredSource();
        var oldTags = source.CreatedState!.Tags.ByHole.ToDictionary(p => p.Key, p => p.Value.Text);

        // A hole is added to the model and one is removed.
        var holes = source.Analysis.Holes.Where(h => h.Id != "D#1").Append(Plain("C#3", 0.5, 3, 2)).ToList();
        source.Analysis = source.Analysis with { Holes = holes };
        var vm = new HoleTableWizardViewModel(source);
        vm.UpdateCommand.Execute(null);

        Assert.Contains("update s2/v2", source.Calls);
        var tags = source.UpdatedPlan!.AllGroups.SelectMany(g => g.Rows).ToDictionary(r => r.Hole.Id, r => r.Tag);
        Assert.DoesNotContain("D#1", tags.Keys);
        foreach (var (id, tag) in tags.Where(t => t.Key != "C#3"))
        {
            Assert.Equal(oldTags[id], tag);
        }

        Assert.Equal(tags["C#3"], TagMap(vm)["C#3"]);
        Assert.StartsWith("Updated.", vm.Status);
        Assert.Contains("1 hole added, 1 hole gone, 0 descriptions changed.", vm.Status);
        Assert.Equal(tags.Count, source.UpdatedState!.Tags.ByHole.Count);
    }

    [Fact]
    public void Update_on_a_view_with_no_stored_tables_says_so_and_does_not_write()
    {
        var source = new FakeSource();
        var vm = new HoleTableWizardViewModel(source);
        vm.UpdateCommand.Execute(null);
        Assert.Equal(HoleTableWizardViewModel.NoStoredTablesText, vm.Status);
        Assert.DoesNotContain(source.Calls, c => c.StartsWith("update"));
    }

    [Fact]
    public void Update_after_editing_in_the_window_uses_those_edits()
    {
        var source = StoredSource();
        var vm = OpenStored(source);
        vm.SelectedClearanceRow = vm.ClearanceRows[^1];
        var id = vm.SelectedClearanceRow.HoleId;
        vm.DeleteCommand.Execute(null);
        vm.UpdateCommand.Execute(null);

        Assert.Contains(id, source.UpdatedState!.Edits.DeletedIds);
        Assert.DoesNotContain(source.UpdatedPlan!.AllGroups.SelectMany(g => g.Rows), r => r.Hole.Id == id);
    }

    [Fact]
    public void A_stale_id_in_the_stored_edits_is_reported_and_dropped()
    {
        var source = StoredSource();
        var stored = source.Stored[("s2", "v2")];
        source.Stored[("s2", "v2")] = stored with { Edits = stored.Edits.WithDeleted("Gone#9") };
        var vm = OpenStored(source);

        Assert.Equal(new[] { "Gone#9" }, vm.Plan!.StaleIds);
        Assert.Contains(HoleTableMessages.StaleIdsDropped(1), vm.Status);
        vm.UpdateCommand.Execute(null);
        Assert.DoesNotContain("Gone#9", source.UpdatedState!.Edits.DeletedIds);
    }

    [Fact]
    public void Group_notes_edit_per_group()
    {
        var (vm, _) = Analyzed();
        Assert.Equal(vm.Plan!.AllGroups.Count(), vm.Groups.Count);
        var group = vm.Groups.First(g => g.Table == HoleTableKind.Clearance);
        group.Notes = "Check fit";
        Assert.Equal("Check fit", vm.Groups.First(g => g.Table == HoleTableKind.Clearance).Notes);
        Assert.Equal("Check fit", vm.ClearanceRows[0].Notes);
    }

    [Fact]
    public void Update_reports_a_failing_source()
    {
        var source = StoredSource();
        var vm = OpenStored(source);
        source.Throw = true;
        vm.UpdateCommand.Execute(null);
        Assert.Contains("boom", vm.Status);
    }

    [Fact]
    public void Swapping_a_row_and_swapping_it_back_leaves_every_tag_unchanged()
    {
        var vm = OpenStored(StoredSource());
        var before = TagMap(vm);
        var id = vm.PrecisionRows[^1].HoleId;
        vm.SelectedPrecisionRow = vm.PrecisionRows[^1];
        vm.SwapCommand.Execute(null);
        Assert.NotEqual(before[id], TagMap(vm)[id]);
        vm.SwapCommand.Execute(null);
        Assert.Equal(before, TagMap(vm));
    }

    [Fact]
    public void Splitting_a_row_and_unsplitting_it_leaves_every_tag_unchanged()
    {
        var vm = OpenStored(StoredSource());
        var before = TagMap(vm);
        var row = vm.PrecisionRows[^1];
        row.IsSplit = true;
        Assert.NotEqual(before[row.HoleId], TagMap(vm)[row.HoleId]);
        vm.PrecisionRows.Single(r => r.HoleId == row.HoleId).IsSplit = false;
        Assert.Equal(before, TagMap(vm));
    }

    [Fact]
    public void Update_after_Analyze_on_a_stored_view_keeps_the_stored_edits()
    {
        var source = StoredSource();
        var vm = new HoleTableWizardViewModel(source);
        vm.AnalyzeCommand.Execute(null);
        Assert.Contains("Opened the stored tables", vm.Status);
        Assert.Contains(vm.Groups, g => g.Notes == "Deburr");

        vm.UpdateCommand.Execute(null);
        Assert.Single(source.UpdatedState!.Edits.SwappedIds);
        Assert.Equal(source.CreatedState!.Edits.SwappedIds, source.UpdatedState.Edits.SwappedIds);
        Assert.Single(source.UpdatedState.Edits.NotesByDescription);
        Assert.Equal(source.CreatedState.Tags.ByHole.ToDictionary(p => p.Key, p => p.Value.Text), TagMap(vm));
    }

    [Fact]
    public void A_failed_create_does_not_replace_the_tags()
    {
        var (vm, source) = Analyzed();
        source.Throw = true;
        vm.CreateTablesCommand.Execute(null);
        Assert.Contains("boom", vm.Status);
        source.Throw = false;
        vm.SelectedClearanceRow = vm.ClearanceRows[0];
        vm.SwapCommand.Execute(null);
        // Still the Create tab's renumbering behaviour: no tags were stored.
        Assert.Equal(HoleTablePlan.Build(source.Analysis.Holes, HoleTableEdits.None.WithSwapped(vm.SelectedRow!.HoleId), new SheetPoint(0, 0), 1, HoleUnits.Metric).AllGroups.SelectMany(g => g.Rows).Select(r => r.Tag), TagList(vm));
    }

    [Fact]
    public void Generate_stores_the_tags_and_lists_the_view()
    {
        var (vm, source) = Analyzed();
        vm.GeneratePrecisionCommand.Execute(null);
        Assert.Contains(vm.StoredViews, v => v.Key == "v2");
        Assert.Equal(5, source.Stored[("s2", "v2")].Tags.ByHole.Count);
    }

    // ---- Task 171: the on-open check of stored tables ----

    private static (FakeSource Source, HoleTableWizardViewModel Vm) Stored()
    {
        var (vm, source) = Analyzed();
        vm.CreateTablesCommand.Execute(null);
        source.Calls.Clear();
        // A second window over the same drawing: nothing is open in it.
        return (source, new HoleTableWizardViewModel(source));
    }

    private static void Move(FakeSource source, string id, double x)
    {
        source.Analysis = source.Analysis with
        {
            Holes = source.Analysis.Holes.Select(h => h.Id == id ? Plain(id, h.DrillDiameterCm ?? 0.6, x, h.SheetYCm, h.IsPrecision) : h).ToList(),
        };
    }

    [Fact]
    public void Check_with_no_stored_tables_lists_nothing()
    {
        var vm = new HoleTableWizardViewModel(new FakeSource());
        vm.CheckStoredTables();
        Assert.Empty(vm.OutOfDateViews);
        Assert.Empty(vm.CouldNotCheckViews);
        Assert.False(vm.HasStoredCheckMessage);
    }

    [Fact]
    public void Check_of_an_unchanged_view_lists_nothing()
    {
        var (_, vm) = Stored();
        vm.CheckStoredTables();
        Assert.Empty(vm.OutOfDateViews);
        Assert.Empty(vm.CouldNotCheckViews);
    }

    [Fact]
    public void Check_lists_a_moved_hole_and_writes_nothing()
    {
        var (source, vm) = Stored();
        Move(source, "P#1", 3);
        vm.CheckStoredTables();

        var item = Assert.Single(vm.OutOfDateViews);
        Assert.Equal("VIEW2 on Sheet:2: 1 hole moved.", item.Text);
        Assert.Contains(HoleTableMessages.OutOfDateHeading, vm.StoredCheckMessage);
        Assert.Contains("VIEW2 on Sheet:2: 1 hole moved.", vm.StoredCheckMessage);
        Assert.All(source.Calls, c => Assert.StartsWith("analyze", c));
        Assert.All(source.ReadCalls, c => Assert.True(c.StartsWith("getviews") || c.StartsWith("readstate"), c));
        Assert.NotEmpty(source.ReadCalls);
        Assert.Null(source.UpdatedState);
    }

    [Fact]
    public void Check_lists_added_and_removed_holes()
    {
        var (source, vm) = Stored();
        source.Analysis = source.Analysis with
        {
            Holes = source.Analysis.Holes.Where(h => h.Id != "C#2").Append(Plain("N#1", 0.5, 9, 9)).ToList(),
            LeftOutCount = 1,
        };
        vm.CheckStoredTables();
        Assert.Equal("VIEW2 on Sheet:2: 1 hole added, 1 hole removed. " + HoleTableMessages.HolesLeftOut(1), Assert.Single(vm.OutOfDateViews).Text);
    }

    [Fact]
    public void Check_covers_every_sheet_not_only_the_selected_one()
    {
        var (source, vm) = Stored();
        source.Stored[("s1", "v1")] = source.Stored[("s2", "v2")] with { SheetName = "Sheet:1", ViewName = "VIEW1" };
        Move(source, "P#2", 4);
        vm.CheckStoredTables();
        Assert.Equal(new[] { "VIEW1", "VIEW2" }, vm.OutOfDateViews.Select(v => v.ViewName).OrderBy(n => n));
        Assert.Contains("analyze s1/v1", source.Calls);
    }

    [Fact]
    public void Check_of_a_version_1_state_skips_moves_without_crashing()
    {
        var (source, vm) = Stored();
        source.Stored[("s2", "v2")] = source.Stored[("s2", "v2")] with { Version = 1, Drawn = null };
        Move(source, "P#1", 3);
        vm.CheckStoredTables();
        Assert.Empty(vm.OutOfDateViews);

        source.Analysis = source.Analysis with { Holes = source.Analysis.Holes.Append(Plain("N#1", 0.5, 9, 9)).ToList() };
        vm.CheckStoredTables();
        Assert.Contains("1 hole added", Assert.Single(vm.OutOfDateViews).Reason);
    }

    [Fact]
    public void Check_lists_a_failed_analysis_or_a_missing_origin_as_could_not_check()
    {
        var (source, vm) = Stored();
        source.Analysis = HoleTableViewAnalysis.Failed("view gone");
        vm.CheckStoredTables();
        Assert.Empty(vm.OutOfDateViews);
        Assert.Equal("VIEW2 on Sheet:2: view gone", Assert.Single(vm.CouldNotCheckViews).Text);
        Assert.Contains(HoleTableMessages.CouldNotCheckHeading, vm.StoredCheckMessage);

        source.Analysis = new([], [], 1, HoleUnits.Metric, 0);
        vm.CheckStoredTables();
        Assert.Contains(HoleTableMessages.NoOrdinateOrigin, Assert.Single(vm.CouldNotCheckViews).Reason);

        source.Analysis = new([], [], 1, HoleUnits.Metric, 0, null, true);
        vm.CheckStoredTables();
        Assert.Contains(HoleTableMessages.OrdinateOriginUnreadable, Assert.Single(vm.CouldNotCheckViews).Reason);
    }

    [Fact]
    public void Check_lists_a_view_whose_analysis_throws_as_could_not_check()
    {
        var (source, vm) = Stored();
        source.Throw = true;
        vm.CheckStoredTables();
        Assert.Empty(vm.OutOfDateViews);
        Assert.Contains("boom", Assert.Single(vm.CouldNotCheckViews).Reason);
    }

    [Fact]
    public void UpdateOutOfDate_updates_each_view_and_drops_it_from_the_list()
    {
        var (source, vm) = Stored();
        Move(source, "P#1", 3);
        vm.CheckStoredTables();
        Assert.True(vm.UpdateOutOfDateCommand.CanExecute(null));
        vm.UpdateOutOfDateCommand.Execute(null);

        Assert.Contains("update s2/v2", source.Calls);
        Assert.Empty(vm.OutOfDateViews);
        Assert.StartsWith("VIEW2 on Sheet:2: Updated.", vm.Status);
        Assert.Equal(30.0, double.Parse(source.UpdatedState!.Drawn!["P#1"].XText, System.Globalization.CultureInfo.InvariantCulture));

        // What Update stored is what the check calls up to date.
        vm.CheckStoredTables();
        Assert.Empty(vm.OutOfDateViews);
        Assert.False(vm.UpdateOutOfDateCommand.CanExecute(null));
    }

    [Fact]
    public void UpdateOutOfDate_on_the_open_view_uses_the_windows_own_Update()
    {
        var (vm, source) = Analyzed();
        vm.CreateTablesCommand.Execute(null);
        Move(source, "P#1", 3);
        vm.CheckStoredTables();
        Assert.Single(vm.OutOfDateViews);
        vm.UpdateOutOfDateCommand.Execute(null);
        Assert.Empty(vm.OutOfDateViews);
        Assert.NotNull(source.UpdatedState);
    }

    [Fact]
    public void UpdateOutOfDate_writes_the_current_sheet_and_view_names()
    {
        var (source, vm) = Stored();
        source.Stored[("s2", "v2")] = source.Stored[("s2", "v2")] with { SheetName = "Old sheet", ViewName = "Old view" };
        Move(source, "P#1", 3);
        vm.CheckStoredTables();
        vm.UpdateOutOfDateCommand.Execute(null);
        Assert.Equal("Sheet:2", source.UpdatedState!.SheetName);
        Assert.Equal("VIEW2", source.UpdatedState.ViewName);
    }

    [Fact]
    public void A_failed_Update_keeps_the_view_listed_with_the_reason()
    {
        var (source, vm) = Stored();
        Move(source, "P#1", 3);
        vm.CheckStoredTables();
        source.Throw = true;
        vm.UpdateOutOfDateCommand.Execute(null);

        var item = Assert.Single(vm.OutOfDateViews);
        Assert.Contains("Update failed", item.Reason);
        Assert.Contains("boom", item.Reason);
        Assert.Contains("Update failed", vm.Status);
    }

    [Fact]
    public void Dismiss_clears_the_message_without_updating()
    {
        var (source, vm) = Stored();
        Move(source, "P#1", 3);
        vm.CheckStoredTables();
        Assert.True(vm.HasStoredCheckMessage);
        vm.DismissStoredCheckCommand.Execute(null);
        Assert.False(vm.HasStoredCheckMessage);
        Assert.Empty(vm.OutOfDateViews);
        Assert.DoesNotContain(source.Calls, c => c.StartsWith("update"));
    }
}
