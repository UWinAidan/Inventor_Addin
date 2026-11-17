using System.Text;
using InventorAddin.Core.HoleTable;
using InventorAddin.Core.Holes;

namespace InventorAddin.Core.Tests.HoleTable;

public class HoleTableRefreshTests
{
    private static readonly SheetPoint Origin = new(0, 0);

    private static HoleTableHole Plain(string id, double dia, double x = 0, double y = 0, bool precision = false) => new(
        id, 1, "F", HoleUnits.Metric, HoleTableType.Plain, precision, dia, HoleTermination.ThroughAll, null, false, null,
        null, null, null, null, null, null, false, null, x, y);

    private static HoleTablePlan Build(IEnumerable<HoleTableHole> holes, HoleTableEdits? edits = null) =>
        HoleTablePlan.Build(holes, edits, Origin, 1, HoleUnits.Metric);

    private static HoleTableRefresh Refresh(IEnumerable<HoleTableHole> holes, HoleTablePlan before, HoleTableEdits? edits = null) =>
        HoleTablePlan.Refresh(holes, edits, before.ToStoredTags(), Origin, 1, HoleUnits.Metric);

    private static string[] Tags(HoleTablePlan p) => p.AllGroups.SelectMany(g => g.Rows).Select(r => r.Tag).ToArray();

    private static string TagOf(HoleTablePlan p, string id) =>
        p.AllGroups.SelectMany(g => g.Rows).Single(r => r.Hole.Id == id).Tag;

    // A: two precision 0.6; B: six clearance 0.5 in a row; C: one clearance 0.9.
    private static List<HoleTableHole> Plate()
    {
        var list = new List<HoleTableHole> { Plain("P#1", 0.6, 1, 1, true), Plain("P#2", 0.6, 2, 1, true), Plain("C#1", 0.9, 5, 5) };
        for (var i = 1; i <= 6; i++)
        {
            list.Add(Plain($"B#{i}", 0.5, i, 8));
        }

        return list;
    }

    [Fact]
    public void Refresh_with_no_previous_tags_equals_Build()
    {
        var holes = Plate();
        var r = HoleTablePlan.Refresh(holes, null, null, Origin, 1, HoleUnits.Metric);
        Assert.Equal(Tags(Build(holes)), Tags(r.Plan));
        Assert.Equal(holes.Count, r.AddedIds.Count);
        Assert.Empty(r.GoneIds);
    }

    [Fact]
    public void Refresh_with_no_previous_tags_equals_Build_with_edits()
    {
        var holes = Plate();
        var edits = HoleTableEdits.None.WithSwapped("P#2").WithSplit("B#3").WithSplit("C#1").WithDeleted("B#5");
        var built = Build(holes, edits);
        var r = HoleTablePlan.Refresh(holes, edits, null, Origin, 1, HoleUnits.Metric);
        Assert.Equal(Tags(built), Tags(r.Plan));
        Assert.Equal(
            built.AllGroups.Select(g => (g.Letter, g.Table, g.SplitHoleId)),
            r.Plan.AllGroups.Select(g => (g.Letter, g.Table, g.SplitHoleId)));
    }

    [Fact]
    public void Unchanged_model_keeps_every_tag_and_reports_nothing()
    {
        var holes = Plate();
        var before = Build(holes);
        var r = Refresh(holes, before);
        Assert.Equal(Tags(before), Tags(r.Plan));
        Assert.Empty(r.AddedIds);
        Assert.Empty(r.GoneIds);
        Assert.Empty(r.DescriptionChanges);
        Assert.Equal("0 holes added, 0 holes gone, 0 descriptions changed.", r.Summary);
    }

    [Fact]
    public void A_hole_that_moved_keeps_its_tag()
    {
        var holes = Plate();
        var before = Build(holes);
        var moved = holes.Select(h => h.Id == "B#2" ? h with { SheetXCm = 40, SheetYCm = -3 } : h).ToList();
        var r = Refresh(moved, before);
        Assert.Equal(TagOf(before, "B#2"), TagOf(r.Plan, "B#2"));
        Assert.Equal(Tags(before).OrderBy(t => t), Tags(r.Plan).OrderBy(t => t));
        Assert.Equal(new[] { "B1", "B2", "B3", "B4", "B5", "B6" }, r.Plan.Clearance[0].Rows.Select(x => x.Tag));
    }

    [Fact]
    public void A_new_hole_in_group_B_takes_B7()
    {
        var holes = Plate();
        var before = Build(holes);
        holes.Add(Plain("B#7", 0.5, 0, 0));
        var r = Refresh(holes, before);
        Assert.Equal("B7", TagOf(r.Plan, "B#7"));
        Assert.Equal(new[] { "B#7" }, r.AddedIds);
        Assert.Equal(Tags(before).Append("B7").OrderBy(t => t), Tags(r.Plan).OrderBy(t => t));
        Assert.Equal("B6", r.Plan.Clearance[0].Rows[^2].Tag);
        Assert.Equal("B7", r.Plan.Clearance[0].Rows[^1].Tag);
    }

    [Fact]
    public void A_new_group_takes_the_next_unused_letter_even_if_it_sorts_earlier()
    {
        var holes = Plate();
        var before = Build(holes);
        holes.Add(Plain("N#1", 0.2, 3, 3)); // clearance; Build would sort it before B
        var r = Refresh(holes, before);
        Assert.Equal("D1", TagOf(r.Plan, "N#1"));
        Assert.Equal(new[] { "B", "C", "D" }, r.Plan.Clearance.Select(g => g.Letter));
        Assert.Equal(TagOf(before, "C#1"), TagOf(r.Plan, "C#1"));
        Assert.Equal(TagOf(before, "B#1"), TagOf(r.Plan, "B#1"));
    }

    [Fact]
    public void A_removed_hole_leaves_a_gap_and_other_tags_stay()
    {
        var holes = Plate();
        var before = Build(holes);
        var r = Refresh(holes.Where(h => h.Id != "B#3"), before);
        Assert.Equal(new[] { "B1", "B2", "B4", "B5", "B6" }, r.Plan.Clearance[0].Rows.Select(x => x.Tag));
        Assert.Equal(new[] { "B#3" }, r.GoneIds);
        Assert.Contains("1 hole gone", r.Summary);
        Assert.Equal(TagOf(before, "C#1"), TagOf(r.Plan, "C#1"));
    }

    [Fact]
    public void A_whole_group_removed_never_frees_its_letter_for_a_new_group()
    {
        var holes = Plate();
        var before = Build(holes);
        holes.RemoveAll(h => h.Id == "C#1");
        holes.Add(Plain("N#1", 0.7, 3, 3));
        var r = Refresh(holes, before);
        Assert.Equal("D1", TagOf(r.Plan, "N#1"));
        Assert.Equal("B1", TagOf(r.Plan, "B#1"));
    }

    [Fact]
    public void A_whole_group_changing_description_keeps_its_letter()
    {
        var holes = Plate();
        var before = Build(holes);
        var changed = holes.Select(h => h.Id.StartsWith("B#") ? h with { DrillDiameterCm = 0.55 } : h).ToList();
        var r = Refresh(changed, before);
        Assert.Equal(Tags(before).OrderBy(t => t), Tags(r.Plan).OrderBy(t => t));
        var change = Assert.Single(r.DescriptionChanges);
        Assert.Equal("B", change.Letter);
        Assert.Empty(r.AddedIds);
    }

    [Fact]
    public void One_hole_changing_description_leaves_its_group_and_is_placed_as_new()
    {
        var holes = Plate();
        var before = Build(holes);
        var changed = holes.Select(h => h.Id == "B#6" ? h with { DrillDiameterCm = 0.55 } : h).ToList();
        var r = Refresh(changed, before);
        Assert.Equal("B1", TagOf(r.Plan, "B#1"));
        Assert.Equal("D1", TagOf(r.Plan, "B#6"));
        Assert.Empty(r.AddedIds);
        Assert.Empty(r.GoneIds);
    }

    [Fact]
    public void A_new_split_takes_the_next_unused_letter()
    {
        var holes = Plate();
        var before = Build(holes);
        var r = Refresh(holes, before, HoleTableEdits.None.WithSplit("B#2"));
        Assert.Equal("D1", TagOf(r.Plan, "B#2"));
        Assert.Equal("B1", TagOf(r.Plan, "B#1"));
        Assert.Equal("B3", TagOf(r.Plan, "B#3"));
        Assert.Equal("D", r.Plan.Clearance.Single(g => g.SplitHoleId == "B#2").Letter);
    }

    [Fact]
    public void Removing_a_split_returns_the_hole_to_its_group_after_the_highest_number()
    {
        var holes = Plate();
        var split = Build(holes, HoleTableEdits.None.WithSplit("B#2"));
        var r = Refresh(holes, split);
        Assert.Equal("B6", TagOf(r.Plan, "B#2"));
        Assert.Equal(6, r.Plan.Clearance.First(g => g.Rows.Any(x => x.Hole.Id == "B#2")).Rows.Count);
    }

    [Fact]
    public void A_swapped_hole_moves_table_with_the_next_letter()
    {
        var holes = Plate();
        var before = Build(holes);
        var r = Refresh(holes, before, HoleTableEdits.None.WithSwapped("P#2"));
        Assert.Equal("A1", TagOf(r.Plan, "P#1"));
        Assert.Equal("D1", TagOf(r.Plan, "P#2"));
        Assert.Contains(r.Plan.Clearance, g => g.Rows.Any(x => x.Hole.Id == "P#2"));
    }

    [Fact]
    public void A_stale_id_in_the_edits_is_dropped_and_reported()
    {
        var holes = Plate();
        var before = Build(holes);
        var edits = HoleTableEdits.None.WithSwapped("gone#1").WithSplitNote("gone#2", "x").WithSplit("B#1");
        var r = Refresh(holes, before, edits);
        Assert.Equal(new[] { "gone#1" }, r.Plan.StaleIds);
        Assert.DoesNotContain("gone#1", r.Edits.SwappedIds);
        Assert.Empty(r.Edits.NotesBySplitId);
        Assert.Contains("B#1", r.Edits.SplitIds);
    }

    [Fact]
    public void Notes_follow_the_group()
    {
        var holes = Plate();
        var before = Build(holes);
        var key = before.Clearance[0].Description.Key;
        var r = Refresh(holes, before, HoleTableEdits.None.WithNote(HoleTableKind.Clearance, key, "Deburr"));
        Assert.Equal("Deburr", r.Plan.Clearance[0].Note);
    }


    [Fact]
    public void A_hole_removed_and_one_added_in_the_same_update_does_not_reuse_the_tag()
    {
        var holes = Plate();
        var before = Build(holes);
        holes.RemoveAll(h => h.Id == "B#6");
        holes.Add(Plain("B#7", 0.5, 0, 0));
        var r = Refresh(holes, before);
        Assert.Equal("B7", TagOf(r.Plan, "B#7"));
    }

    [Fact]
    public void A_tag_retired_in_one_update_is_not_reused_in_a_later_one()
    {
        var holes = Plate();
        var before = Build(holes);
        holes.RemoveAll(h => h.Id == "B#6");
        var r1 = Refresh(holes, before);
        Assert.Equal(6, r1.Plan.ToStoredTags().HighestNumber("B"));
        holes.Add(Plain("B#7", 0.5, 0, 0));
        var r2 = Refresh(holes, r1.Plan);
        Assert.Equal("B7", TagOf(r2.Plan, "B#7"));
    }

    [Fact]
    public void High_water_marks_survive_the_stored_text()
    {
        var holes = Plate();
        var before = Build(holes);
        holes.RemoveAll(h => h.Id is "B#6" or "C#1");
        var r1 = Refresh(holes, before);
        var state = HoleTableState.From("S", "V", r1.Edits, r1.Plan);
        var read = HoleTableStateJson.Read(HoleTableStateJson.Write(state)).State!;
        Assert.Equal(state, read);
        holes.Add(Plain("B#7", 0.5, 0, 0));
        holes.Add(Plain("N#1", 0.7, 3, 3));
        var r2 = HoleTablePlan.Refresh(holes, r1.Edits, read.Tags, Origin, 1, HoleUnits.Metric);
        Assert.Equal("B7", TagOf(r2.Plan, "B#7"));
        Assert.Equal("D1", TagOf(r2.Plan, "N#1"));
    }

    [Fact]
    public void Text_without_high_water_marks_falls_back_to_the_stored_tags()
    {
        var holes = Plate();
        var before = Build(holes);
        var json = HoleTableStateJson.Write(HoleTableState.From("S", "V", HoleTableEdits.None, before));
        var node = System.Text.Json.Nodes.JsonNode.Parse(json)!.AsObject();
        node.Remove("nl");
        node.Remove("hn");
        var read = HoleTableStateJson.Read(node.ToJsonString());
        Assert.Null(read.Reason);
        Assert.Equal(before.ToStoredTags().NextLetterIndex, read.State!.Tags.NextLetterIndex);
        Assert.Equal(6, read.State.Tags.HighestNumber("B"));
    }

    [Fact]
    public void A_refresh_that_left_gaps_then_Build_equals_a_fresh_Build()
    {
        var holes = Plate();
        var before = Build(holes);
        holes.RemoveAll(h => h.Id is "B#2" or "C#1");
        holes.Add(Plain("N#1", 0.2, 3, 3));
        var r = Refresh(holes, before, HoleTableEdits.None.WithDeleted("gone#1").WithSplit("B#4"));
        var rebuilt = Build(holes, r.Edits);
        var fresh = Build(holes, HoleTableEdits.None.WithSplit("B#4"));
        Assert.Equal(Tags(fresh), Tags(rebuilt));
        Assert.NotEqual(Tags(fresh), Tags(r.Plan));
    }
    [Fact]
    public void Refresh_is_stable_when_run_again_and_Build_renumbers()
    {
        var holes = Plate();
        var before = Build(holes);
        holes.RemoveAll(h => h.Id == "B#1");
        holes.Add(Plain("N#1", 0.2, 3, 3));
        var r1 = Refresh(holes, before);
        var r2 = Refresh(holes, r1.Plan);
        Assert.Equal(Tags(r1.Plan), Tags(r2.Plan));
        Assert.NotEqual(Tags(Build(holes)), Tags(r1.Plan));
        Assert.Equal(Tags(Build(holes)), Tags(HoleTablePlan.Refresh(holes, null, null, Origin, 1, HoleUnits.Metric).Plan));
    }

    [Fact]
    public void LetterIndex_inverts_Letter()
    {
        foreach (var i in new[] { 0, 1, 25, 26, 27, 51, 52, 701, 702 })
        {
            Assert.Equal(i, HoleTablePlan.LetterIndex(HoleTablePlan.Letter(i)));
        }

        Assert.Equal(-1, HoleTablePlan.LetterIndex(""));
        Assert.Equal(-1, HoleTablePlan.LetterIndex("a1"));
    }

    [Fact]
    public void Summary_uses_singular_and_plural()
    {
        var holes = Plate();
        var before = Build(holes);
        holes.RemoveAll(h => h.Id is "B#1" or "B#2");
        holes.Add(Plain("N#1", 0.5, 9, 9));
        var r = Refresh(holes, before);
        Assert.Equal("1 hole added, 2 holes gone, 0 descriptions changed.", r.Summary);
    }

    [Fact]
    public void Stored_state_for_200_holes_is_under_32_KB()
    {
        var holes = new List<HoleTableHole>();
        for (var i = 0; i < 200; i++)
        {
            var dia = 0.4 + (i % 8) * 0.05;
            holes.Add(Plain($"Plate Hole {i / 8 + 1}#AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8gISIjJCUm{i:D4}", dia, i % 20, i / 20));
        }

        var plan = Build(holes);
        var edits = HoleTableEdits.None.WithSwapped(holes[0].Id).WithSplit(holes[1].Id).WithSplitNote(holes[1].Id, "Reamed");
        var text = HoleTableStateJson.Write(HoleTableState.From("Sheet:1", "VIEW1", edits, plan));
        var bytes = Encoding.UTF8.GetByteCount(text);
        Assert.True(bytes < 32 * 1024, bytes.ToString());
    }
}
