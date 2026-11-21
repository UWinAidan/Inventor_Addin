using InventorAddin.Core.HoleTable;
using InventorAddin.Core.Holes;

namespace InventorAddin.Core.Tests.HoleTable;

public class HoleTablePlanTests
{
    private static readonly SheetPoint Origin = new(0, 0);

    private static HoleTableHole Plain(string id, double dia, double x = 0, double y = 0, bool precision = false, HoleTolerance? tol = null) => new(
        id, 1, "F", HoleUnits.Metric, HoleTableType.Plain, precision, dia, HoleTermination.ThroughAll, null, false, null,
        null, null, null, null, null, null, false, tol, x, y);

    private static HoleTableHole Tapped(string id, double x = 0, double y = 0) => new(
        id, 1, "T", HoleUnits.Metric, HoleTableType.Tapped, false, 0.5, HoleTermination.ThroughAll, null, false, null,
        null, null, null, "M6x1", "Metric", null, false, null, x, y);

    private static HoleTablePlan Build(IEnumerable<HoleTableHole> holes, HoleTableEdits? edits = null) =>
        HoleTablePlan.Build(holes, edits, Origin, 1, HoleUnits.Metric);

    private static List<HoleTableHole> Plate(int bCount = 6)
    {
        var tol = new HoleTolerance("H7", "H7", 0.001, 0);
        var list = new List<HoleTableHole>
        {
            Plain("P#1", 0.6, 1, 1, true, tol),
            Plain("P#2", 0.6, 2, 1, true, tol),
            Plain("C#1", 0.9, 5, 5),
            Tapped("T#1", 1, 3), Tapped("T#2", 2, 3), Tapped("T#3", 3, 3), Tapped("T#4", 4, 3),
        };
        for (var i = 1; i <= bCount; i++)
        {
            list.Add(Plain($"B#{i}", 0.5, i, 8));
        }

        return list;
    }

    private static string[] Tags(HoleTablePlan p) => p.AllGroups.SelectMany(g => g.Rows).Select(r => r.Tag).ToArray();

    [Fact]
    public void Reference_plate_tags()
    {
        var p = Build(Plate());
        Assert.Equal(
            new[] { "A1", "A2", "B1", "B2", "B3", "B4", "B5", "B6", "C1", "D1", "D2", "D3", "D4" },
            Tags(p));
        Assert.Single(p.Precision);
        Assert.Equal(3, p.Clearance.Count);
        Assert.Empty(p.StaleIds);
        Assert.All(p.Precision, g => Assert.Equal(HoleTableKind.Precision, g.Table));
        Assert.Equal("-", p.Precision[0].NotesText);
    }

    [Fact]
    public void Result_does_not_depend_on_input_order()
    {
        var reversed = Plate();
        reversed.Reverse();
        var ids1 = Build(Plate()).AllGroups.SelectMany(g => g.Rows).Select(r => r.Tag + r.Hole.Id);
        var ids2 = Build(reversed).AllGroups.SelectMany(g => g.Rows).Select(r => r.Tag + r.Hole.Id);
        Assert.Equal(ids1, ids2);
    }

    [Fact]
    public void Rows_order_by_y_then_x_and_show_coordinates()
    {
        var p = Build(new[] { Plain("a", 0.5, 3, 2), Plain("b", 0.5, 1, 2), Plain("c", 0.5, 9, 1) });
        var rows = p.Clearance[0].Rows;
        Assert.Equal(new[] { "c", "b", "a" }, rows.Select(r => r.Hole.Id));
        Assert.Equal("90.0", rows[0].XText);
        Assert.Equal("10.0", rows[0].YText);
    }

    [Fact]
    public void Chained_y_near_ties_are_stable_whatever_the_input_order()
    {
        // 0.0004 apart in a chain: pairwise comparison with a tolerance is not transitive; the plan must be.
        var holes = new[]
        {
            Plain("a", 0.5, 3, 1.0000), Plain("b", 0.5, 2, 1.0004), Plain("c", 0.5, 1, 1.0008),
        };
        var expected = Build(holes).Clearance[0].Rows.Select(r => r.Hole.Id).ToList();
        Assert.Equal(new[] { "c", "b", "a" }, expected); // one row band, so X ascending
        foreach (var perm in new[] { new[] { 0, 1, 2 }, new[] { 2, 1, 0 }, new[] { 1, 0, 2 }, new[] { 1, 2, 0 }, new[] { 0, 2, 1 }, new[] { 2, 0, 1 } })
        {
            Assert.Equal(expected, Build(perm.Select(i => holes[i])).Clearance[0].Rows.Select(r => r.Hole.Id));
        }
    }

    [Fact]
    public void Clearance_groups_follow_type_then_diameter()
    {
        HoleTableHole Typed(string id, HoleTableType type, double dia, HoleSeat seat = HoleSeat.None, bool tapped = false) => new(
            id, 1, "F", HoleUnits.Metric, type, false, dia, HoleTermination.ThroughAll, null, false, null,
            seat == HoleSeat.None ? null : 1.0, seat == HoleSeat.Countersink ? null : 0.2, seat == HoleSeat.Countersink ? 90 : null,
            tapped ? "M5" : null, tapped ? "Metric" : null, null, type == HoleTableType.TaperTapped, null, 0, 0)
        { Seat = seat };

        var p = Build(new[]
        {
            Typed("t", HoleTableType.Tapped, 0.4, tapped: true),
            Typed("sf", HoleTableType.Spotface, 0.4, HoleSeat.Spotface),
            Typed("cs", HoleTableType.Countersink, 0.4, HoleSeat.Countersink),
            Typed("cb", HoleTableType.Counterbore, 0.4, HoleSeat.Counterbore),
            Typed("p2", HoleTableType.Plain, 0.6),
            Typed("p1", HoleTableType.Plain, 0.3),
            Typed("tt", HoleTableType.TaperTapped, 0.4, tapped: true),
        });
        Assert.Equal(
            new[] { "p1", "p2", "cb", "cs", "sf", "t", "tt" },
            p.Clearance.Select(g => g.Rows[0].Hole.Id));
        Assert.Equal(new[] { "A", "B", "C", "D", "E", "F", "G" }, p.Clearance.Select(g => g.Letter));
    }

    [Fact]
    public void Letters_run_past_Z()
    {
        Assert.Equal("A", HoleTablePlan.Letter(0));
        Assert.Equal("Z", HoleTablePlan.Letter(25));
        Assert.Equal("AA", HoleTablePlan.Letter(26));
        Assert.Equal("AB", HoleTablePlan.Letter(27));
        Assert.Equal("AZ", HoleTablePlan.Letter(51));
        Assert.Equal("BA", HoleTablePlan.Letter(52));
        var many = Enumerable.Range(0, 28).Select(i => Plain("h" + i, 0.1 + i * 0.01));
        Assert.Equal("AB", Build(many).Clearance[^1].Letter);
    }

    [Fact]
    public void Swap_moves_a_hole_to_the_other_table_and_groups_by_description()
    {
        var holes = Plate();
        var p = Build(holes, HoleTableEdits.None.WithSwapped("B#1"));
        // B#1 is now in precision; the 0.5 diameter sorts before the 0.6 precision group.
        Assert.Equal(2, p.Precision.Count);
        Assert.Equal("B#1", p.Precision[0].Rows[0].Hole.Id);
        Assert.Equal("A", p.Precision[0].Letter);
        Assert.Equal("A1", Tags(p)[0]);
        Assert.Equal("P#1", p.Precision[1].Rows[0].Hole.Id);
        // Swap back by toggling off.
        Assert.Equal(Tags(Build(holes)), Tags(Build(holes, HoleTableEdits.None.WithSwapped("B#1").WithSwapped("B#1", false))));
    }

    [Fact]
    public void Swapped_holes_with_one_description_share_a_group()
    {
        var p = Build(Plate(), HoleTableEdits.None.WithSwapped("B#1").WithSwapped("B#2"));
        Assert.Equal(2, p.Precision[0].Rows.Count);
    }

    [Fact]
    public void Delete_leaves_the_hole_out_and_renumbers()
    {
        var p = Build(Plate(), HoleTableEdits.None.WithDeleted("B#2"));
        var b = p.Clearance.First(g => g.Rows.Any(r => r.Hole.Id == "B#1"));
        Assert.Equal(new[] { "B1", "B2", "B3", "B4", "B5" }, b.Rows.Select(r => r.Tag));
        Assert.DoesNotContain(p.AllGroups.SelectMany(g => g.Rows), r => r.Hole.Id == "B#2");
    }

    [Fact]
    public void Deleting_a_whole_group_closes_the_letter_gap()
    {
        var p = Build(Plate(), HoleTableEdits.None.WithDeleted("C#1"));
        Assert.Equal(new[] { "A", "B", "C" }, p.AllGroups.Select(g => g.Letter));
        Assert.Equal("C", p.Clearance[1].Letter);
    }

    [Fact]
    public void Split_gives_the_hole_its_own_group_straight_after_its_source()
    {
        var p = Build(Plate(), HoleTableEdits.None.WithSplit("B#3"));
        Assert.Equal(4, p.Clearance.Count);
        Assert.Equal(new[] { "A", "B", "C", "D", "E" }, p.AllGroups.Select(g => g.Letter));
        Assert.Equal(5, p.Clearance[0].Rows.Count);
        Assert.Equal("C1", p.Clearance[1].Rows[0].Tag);
        Assert.Equal("B#3", p.Clearance[1].SplitHoleId);
        Assert.Single(p.Clearance[1].Rows);
        // The next source group moves down a letter.
        Assert.Equal("D1", p.Clearance[2].Rows[0].Tag);
    }

    [Fact]
    public void Splitting_every_hole_of_a_group_still_orders_them_in_place()
    {
        var p = Build(Plate(), HoleTableEdits.None.WithSplit("P#2").WithSplit("P#1"));
        Assert.Equal(2, p.Precision.Count);
        Assert.Equal(new[] { "P#1", "P#2" }, p.Precision.Select(g => g.Rows[0].Hole.Id));
        Assert.All(p.Precision, g => Assert.NotNull(g.SplitHoleId));
    }

    [Fact]
    public void Edits_combine_split_swap_and_delete()
    {
        var edits = HoleTableEdits.None.WithSplit("B#1").WithSwapped("B#1").WithDeleted("B#2");
        var p = Build(Plate(), edits);
        var split = p.Precision.Single(g => g.SplitHoleId == "B#1");
        Assert.Single(split.Rows);
        Assert.DoesNotContain(p.AllGroups.SelectMany(g => g.Rows), r => r.Hole.Id == "B#2");
        Assert.Equal(4, p.Clearance.First(g => g.Rows.Any(r => r.Hole.Id == "B#3")).Rows.Count);

        // Delete wins over split and swap.
        var deleted = Build(Plate(), edits.WithDeleted("B#1"));
        Assert.DoesNotContain(deleted.AllGroups.SelectMany(g => g.Rows), r => r.Hole.Id == "B#1");
    }

    [Fact]
    public void Stale_ids_are_ignored_and_reported()
    {
        var edits = HoleTableEdits.None.WithSwapped("gone#1").WithDeleted("gone#2").WithSplit("gone#1").WithSplit("B#1");
        var p = Build(Plate(), edits);
        Assert.Equal(new[] { "gone#1", "gone#2" }, p.StaleIds);
        Assert.Equal(Tags(Build(Plate(), HoleTableEdits.None.WithSplit("B#1"))), Tags(p));
    }

    private static List<HoleTableHole> PlateOf(int b, int c, int d)
    {
        var tol = new HoleTolerance("H7", "H7", 0.001, 0);
        var list = new List<HoleTableHole> { Plain("P#1", 0.6, 1, 1, true, tol), Plain("P#2", 0.6, 2, 1, true, tol) };
        list.AddRange(Enumerable.Range(1, b).Select(i => Plain($"B#{i}", 0.5, i, 8)));
        list.AddRange(Enumerable.Range(1, c).Select(i => Plain($"C#{i}", 0.9, i, 5)));
        list.AddRange(Enumerable.Range(1, d).Select(i => Tapped($"T#{i}", i, 3)));
        return list;
    }

    private static string[] LetterToFirstHole(HoleTablePlan p) =>
        p.AllGroups.Select(g => g.Letter + ":" + g.Rows[0].Hole.Id).ToArray();

    private static string[] Numbers(HoleTablePlan p, string letter) =>
        p.AllGroups.Single(g => g.Letter == letter).Rows.Select(r => r.Tag).ToArray();

    [Fact]
    public void Adding_instances_never_changes_other_groups_letters_even_when_counts_pass_each_other()
    {
        // B has 6, C has 1, D (tapped) has 4: A precision, B, C, D clearance.
        var before = Build(PlateOf(6, 1, 4));
        // D grows to 7 (more than B's 6) and C to 5 (more than D's old 4).
        var after = Build(PlateOf(6, 5, 7));
        Assert.Equal(LetterToFirstHole(before), LetterToFirstHole(after));
        Assert.Equal(Numbers(before, "A"), Numbers(after, "A"));
        Assert.Equal(Numbers(before, "B"), Numbers(after, "B"));
        Assert.Equal(new[] { "C1", "C2", "C3", "C4", "C5" }, Numbers(after, "C"));
        Assert.Equal(7, Numbers(after, "D").Length);
        Assert.Equal("D7", Numbers(after, "D")[6]);
    }

    [Fact]
    public void Removing_instances_never_changes_other_groups_letters_even_when_counts_pass_each_other()
    {
        var before = Build(PlateOf(6, 1, 4));
        // Delete B#2..B#6 so B (1) has fewer than D (4).
        var edits = HoleTableEdits.None;
        for (var i = 2; i <= 6; i++)
        {
            edits = edits.WithDeleted($"B#{i}");
        }

        var after = Build(PlateOf(6, 1, 4), edits);
        Assert.Equal(LetterToFirstHole(before), LetterToFirstHole(after));
        Assert.Equal(new[] { "B1" }, Numbers(after, "B"));
        Assert.Equal(Numbers(before, "C"), Numbers(after, "C"));
        Assert.Equal(Numbers(before, "D"), Numbers(after, "D"));
    }

    [Fact]
    public void Notes_are_kept_per_table_when_two_descriptions_share_a_key()
    {
        // No drill diameter and no termination: an empty description, the same Key in both tables.
        HoleTableHole Bare(string id, bool precision) => new(
            id, 1, "F", HoleUnits.Metric, HoleTableType.Plain, precision, null, null, null, false, null,
            null, null, null, null, null, null, false, null, 0, 0);
        var holes = new[] { Bare("p", true), Bare("c", false) };
        var edits = HoleTableEdits.None.WithNote(HoleTableKind.Precision, "", "PRECISION NOTE");
        var p = Build(holes, edits);
        Assert.Equal("PRECISION NOTE", p.Precision[0].NotesText);
        Assert.Equal("-", p.Clearance[0].NotesText);
    }

    [Fact]
    public void Notes_survive_a_rebuild_and_missing_notes_show_a_dash()
    {
        var holes = Plate();
        var key = Build(holes).Clearance[0].Description.Key;
        var edits = HoleTableEdits.None.WithNote(HoleTableKind.Clearance, key, "DRILL FIRST").WithSplitNote("B#3", "SPLIT NOTE").WithSplit("B#3");
        var p = Build(holes, edits);
        Assert.Equal("DRILL FIRST", p.Clearance[0].NotesText);
        Assert.Equal("SPLIT NOTE", p.Clearance[1].NotesText);
        Assert.Equal("-", p.Clearance[2].NotesText);
        Assert.Equal("-", p.Precision[0].NotesText);

        var again = Build(holes, edits);
        Assert.Equal("DRILL FIRST", again.Clearance[0].NotesText);

        Assert.Equal("-", Build(holes, edits.WithNote(HoleTableKind.Clearance, key, "  ")).Clearance[0].NotesText);
    }
}
