using InventorAddin.Core.HoleTable;
using InventorAddin.Core.Holes;

namespace InventorAddin.Core.Tests.HoleTable;

public class HoleTableStalenessTests
{
    private static readonly SheetPoint Origin = new(0, 0);

    private static HoleTableHole Plain(string id, double dia, double x = 0, double y = 0, bool precision = false) => new(
        id, 1, "F", HoleUnits.Metric, HoleTableType.Plain, precision, dia, HoleTermination.ThroughAll, null, false, null,
        null, null, null, null, null, null, false, null, x, y);

    private static List<HoleTableHole> Plate() =>
        [Plain("P#1", 0.6, 1, 1, true), Plain("P#2", 0.6, 2, 1, true), Plain("C#1", 0.5, 1, 2), Plain("C#2", 0.5, 2, 2)];

    private static HoleTableViewAnalysis Analysis(IEnumerable<HoleTableHole> holes, int leftOut = 0) =>
        new(holes.ToList(), [Origin], 1, HoleUnits.Metric, leftOut);

    private static HoleTableState Stored(IEnumerable<HoleTableHole> holes, HoleTableEdits? edits = null)
    {
        var plan = HoleTablePlan.Build(holes, edits, Origin, 1, HoleUnits.Metric);
        return HoleTableState.From("Sheet:1", "VIEW1", edits ?? HoleTableEdits.None, plan);
    }

    [Fact]
    public void An_unchanged_view_is_up_to_date()
    {
        var r = HoleTableStaleness.Check(Stored(Plate()), Analysis(Plate()));
        Assert.Equal(HoleTableStalenessKind.UpToDate, r.Kind);
        Assert.False(r.MovesNotChecked);
    }

    [Fact]
    public void A_moved_hole_is_reported_but_a_move_below_the_precision_is_not()
    {
        var moved = Plate();
        moved[0] = Plain("P#1", 0.6, 1.5, 1, true);
        var r = HoleTableStaleness.Check(Stored(Plate()), Analysis(moved));
        Assert.True(r.IsOutOfDate);
        Assert.Equal(1, r.Moved);
        Assert.Equal("1 hole moved.", r.Text);

        // 0.0001 cm is 0.001 mm: under any table's precision.
        var nudged = Plate();
        nudged[0] = Plain("P#1", 0.6, 1.0001, 1, true);
        Assert.Equal(HoleTableStalenessKind.UpToDate, HoleTableStaleness.Check(Stored(Plate()), Analysis(nudged)).Kind);
    }

    [Fact]
    public void An_added_hole_is_reported()
    {
        var more = Plate();
        more.Add(Plain("C#3", 0.5, 3, 2));
        var r = HoleTableStaleness.Check(Stored(Plate()), Analysis(more));
        Assert.True(r.IsOutOfDate);
        Assert.Equal(1, r.Added);
    }

    [Fact]
    public void A_removed_hole_is_reported()
    {
        var fewer = Plate().Where(h => h.Id != "C#2").ToList();
        var r = HoleTableStaleness.Check(Stored(Plate()), Analysis(fewer));
        Assert.Equal(1, r.Removed);
        Assert.Equal("1 hole removed.", r.Text);
    }

    [Fact]
    public void A_hole_left_out_because_it_is_not_end_on_says_so()
    {
        var fewer = Plate().Where(h => h.Id != "C#2").ToList();
        var r = HoleTableStaleness.Check(Stored(Plate()), Analysis(fewer, leftOut: 1));
        Assert.Equal(1, r.Removed);
        Assert.Equal("1 hole removed. " + HoleTableMessages.HolesLeftOut(1), r.Text);
    }

    [Fact]
    public void A_changed_description_is_reported()
    {
        var wider = Plate().Select(h => h.Id.StartsWith('C') ? Plain(h.Id, 0.55, h.SheetXCm, h.SheetYCm) : h).ToList();
        var r = HoleTableStaleness.Check(Stored(Plate()), Analysis(wider));
        Assert.True(r.IsOutOfDate);
        Assert.True(r.DescriptionChanged > 0);
        Assert.Contains("description", r.Text);
    }

    [Fact]
    public void Several_reasons_are_listed_together()
    {
        var holes = Plate().Where(h => h.Id != "C#2").ToList();
        holes[0] = Plain("P#1", 0.6, 1.5, 1, true);
        holes.Add(Plain("C#9", 0.5, 9, 9));
        Assert.Equal("1 hole moved, 1 hole added, 1 hole removed.", HoleTableStaleness.Check(Stored(Plate()), Analysis(holes)).Text);
    }

    [Fact]
    public void A_version_1_state_is_checked_without_moves()
    {
        var v1 = Stored(Plate()) with { Version = 1, Drawn = null };
        var moved = Plate();
        moved[0] = Plain("P#1", 0.6, 1.5, 1, true);
        var r = HoleTableStaleness.Check(v1, Analysis(moved));
        Assert.Equal(HoleTableStalenessKind.UpToDate, r.Kind);
        Assert.True(r.MovesNotChecked);

        var added = Plate();
        added.Add(Plain("C#3", 0.5, 3, 2));
        var r2 = HoleTableStaleness.Check(v1, Analysis(added));
        Assert.Equal(1, r2.Added);
        Assert.Contains("older version", r2.Text);
    }

    [Fact]
    public void A_failed_analysis_could_not_be_checked()
    {
        var r = HoleTableStaleness.Check(Stored(Plate()), HoleTableViewAnalysis.Failed("no view"));
        Assert.Equal(HoleTableStalenessKind.CouldNotCheck, r.Kind);
        Assert.Equal("no view", r.Text);
    }

    [Fact]
    public void An_origin_stop_could_not_be_checked()
    {
        var none = HoleTableStaleness.Check(Stored(Plate()), Analysis(Plate()) with { Origins = [] });
        Assert.Equal(HoleTableStalenessKind.CouldNotCheck, none.Kind);
        Assert.Equal(HoleTableMessages.NoOrdinateOrigin, none.Reason);

        var unreadable = HoleTableStaleness.Check(Stored(Plate()), Analysis(Plate()) with { Origins = [], OriginUnreadable = true });
        Assert.Equal(HoleTableMessages.OrdinateOriginUnreadable, unreadable.Reason);

        var several = HoleTableStaleness.Check(Stored(Plate()), Analysis(Plate()) with { Origins = [new(0, 0), new(5, 5)] });
        Assert.Equal(HoleTableMessages.MoreThanOneOrdinateOrigin, several.Reason);
    }

    private static IEnumerable<List<HoleTableHole>> Variants()
    {
        yield return Plate();
        var moved = Plate();
        moved[2] = Plain("C#1", 0.5, 1, 2.5);
        yield return moved;
        var added = Plate();
        added.Add(Plain("C#3", 0.5, 3, 2));
        yield return added;
        yield return Plate().Where(h => h.Id != "C#2").ToList();
        // Whole group changes description.
        yield return Plate().Select(h => h.Id.StartsWith('C') ? Plain(h.Id, 0.55, h.SheetXCm, h.SheetYCm) : h).ToList();
        // One hole of a group changes description: it is retagged.
        yield return Plate().Select(h => h.Id == "C#2" ? Plain(h.Id, 0.55, h.SheetXCm, h.SheetYCm) : h).ToList();
        // Retagged and moved.
        yield return Plate().Select(h => h.Id == "C#2" ? Plain(h.Id, 0.55, 7, 7) : h).ToList();
    }

    [Fact]
    public void The_check_agrees_with_Update_on_what_changed()
    {
        var state = Stored(Plate());
        foreach (var holes in Variants())
        {
            var check = HoleTableStaleness.Check(state, Analysis(holes));
            var refresh = HoleTablePlan.Refresh(holes, state.Edits, state.Tags, Origin, 1, HoleUnits.Metric);
            var after = HoleTableState.From("Sheet:1", "VIEW1", refresh.Edits, refresh.Plan);
            Assert.Equal(!check.IsOutOfDate, after.Equals(state));
        }
    }

    [Fact]
    public void A_retagged_hole_that_also_moved_counts_as_moved_and_as_a_description_change()
    {
        var holes = Plate().Select(h => h.Id == "C#2" ? Plain(h.Id, 0.55, 7, 7) : h).ToList();
        var r = HoleTableStaleness.Check(Stored(Plate()), Analysis(holes));
        Assert.Equal(1, r.Moved);
        Assert.Equal(1, r.DescriptionChanged);
    }

    [Fact]
    public void State_from_a_Refresh_plan_with_gaps_a_split_and_a_swap_round_trips()
    {
        var edits = HoleTableEdits.None.WithSplit("C#2").WithSwapped("P#2");
        var first = Stored(Plate(), edits);
        var holes = Plate().Where(h => h.Id != "P#1").ToList();
        holes.Add(Plain("C#3", 0.5, 3, 2));
        var refresh = HoleTablePlan.Refresh(holes, first.Edits, first.Tags, Origin, 1, HoleUnits.Metric);
        var state = HoleTableState.From("Sheet:1", "VIEW1", refresh.Edits, refresh.Plan);

        var read = HoleTableStateJson.Read(HoleTableStateJson.Write(state));
        Assert.Null(read.Reason);
        Assert.Equal(state, read.State);
        Assert.Equal(state.Drawn, read.State!.Drawn);
        Assert.Contains(state.Tags.Groups, g => g.SplitHoleId is not null);
    }

    [Fact]
    public void State_round_trips_with_positions_and_a_version_1_text_still_reads()
    {
        var state = Stored(Plate());
        var read = HoleTableStateJson.Read(HoleTableStateJson.Write(state));
        Assert.Null(read.Reason);
        Assert.Equal(2, read.State!.Version);
        Assert.Equal(state.Drawn, read.State.Drawn);

        var v1 = HoleTableStateJson.Write(state with { Version = 1, Drawn = null });
        var readV1 = HoleTableStateJson.Read(v1);
        Assert.Null(readV1.Reason);
        Assert.Equal(1, readV1.State!.Version);
        Assert.Null(readV1.State.Drawn);
    }
}
