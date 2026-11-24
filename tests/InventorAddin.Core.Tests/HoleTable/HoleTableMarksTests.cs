using InventorAddin.Core.HoleTable;

namespace InventorAddin.Core.Tests.HoleTable;

public class HoleTableMarksTests
{
    [Fact]
    public void Names_carry_the_brand()
    {
        Assert.Equal("AWB Centre Mark", HoleTableMarks.CentreMarkStyleName);
        Assert.Equal("AWB_HoleTag", HoleTableMarks.TagDefinitionName);
        Assert.Equal("AWB_PrecisionMark", HoleTableMarks.PrecisionMarkDefinitionName);
        Assert.Equal("AWB_HoleMark", HoleTableMarks.AttributeSetName);
    }

    [Fact]
    public void The_centre_mark_runs_two_millimetres_past_the_circle_with_no_gap()
    {
        Assert.Equal(0.2, HoleTableMarks.CentreMarkOvershootCm);
        Assert.Equal(0.2, HoleTableMarks.CentreMarkExtensionLengthCm);
        Assert.Equal(0.01, HoleTableMarks.CentreMarkSizeCm);
        Assert.True(HoleTableMarks.CentreMarkGapCm > 0, "Inventor refuses a gap of 0");
        Assert.True(HoleTableMarks.CentreMarkGapCm < 0.001);
    }

    [Fact]
    public void Only_a_hole_in_the_precision_table_gets_the_precision_mark()
    {
        Assert.True(HoleTableMarks.HasPrecisionMark(HoleTableKind.Precision));
        Assert.False(HoleTableMarks.HasPrecisionMark(HoleTableKind.Clearance));
    }

    [Fact]
    public void The_outermost_circle_is_the_largest_thread_edge_included()
    {
        var hole = new HoleCircle(0.3, false);
        var thread = new HoleCircle(0.4, true);
        var seat = new HoleCircle(0.6, false);
        Assert.Equal(2, HoleTableMarks.OutermostIndex([hole, thread, seat]));
        Assert.Equal(1, HoleTableMarks.OutermostIndex([hole, thread]));
        Assert.Equal(0, HoleTableMarks.OutermostIndex([hole]));
    }

    [Fact]
    public void No_circles_give_minus_one()
    {
        Assert.Equal(-1, HoleTableMarks.OutermostIndex([]));
        Assert.Equal(-1, HoleTableMarks.HoleIndex([]));
    }

    [Fact]
    public void The_hole_circle_is_the_smallest_that_is_not_a_thread_edge()
    {
        Assert.Equal(0, HoleTableMarks.HoleIndex([new HoleCircle(0.3, false), new HoleCircle(0.4, true), new HoleCircle(0.6, false)]));
        Assert.Equal(1, HoleTableMarks.HoleIndex([new HoleCircle(0.2, true), new HoleCircle(0.5, false)]));
        Assert.Equal(0, HoleTableMarks.HoleIndex([new HoleCircle(0.2, true), new HoleCircle(0.4, true)]));
    }

    [Fact]
    public void The_precision_mark_counts_when_it_is_larger_than_the_outermost_circle()
    {
        Assert.Equal(0.6, HoleTableMarks.ClearRadiusCm(0.6, true, 0.3));
        Assert.Equal(0.5, HoleTableMarks.ClearRadiusCm(0.3, true, 0.5));
        Assert.Equal(0.3, HoleTableMarks.ClearRadiusCm(0.3, false, 0.5));
    }

    [Fact]
    public void The_tag_sits_up_and_right_at_45_degrees_half_a_text_height_clear()
    {
        var centre = new SheetPoint(10, 20);
        const double clear = 0.5, text = 0.35, width = 0.8;
        var p = HoleTableMarks.TagPoint(centre, clear, text, width);

        // The symbol is centred on the point, so the tag's lower left corner is the point less half its size.
        var cornerX = p.XCm - width / 2 - centre.XCm;
        var cornerY = p.YCm - text / 2 - centre.YCm;
        Assert.Equal(cornerX, cornerY, 9);
        Assert.Equal(clear + 0.5 * text, Math.Sqrt(cornerX * cornerX + cornerY * cornerY), 9);
        Assert.True(cornerX > 0);
    }

    [Fact]
    public void A_bigger_clear_radius_moves_the_tag_further_out()
    {
        var centre = new SheetPoint(0, 0);
        var near = HoleTableMarks.TagPoint(centre, 0.3, 0.35, 0.8);
        var far = HoleTableMarks.TagPoint(centre, 0.9, 0.35, 0.8);
        Assert.True(far.XCm > near.XCm);
        Assert.True(far.YCm > near.YCm);
    }

    [Fact]
    public void The_summary_is_empty_when_nothing_was_marked()
    {
        Assert.Equal("", HoleTableMarks.Summary(0, 0));
        Assert.Equal("Marked 1 hole on the view.", HoleTableMarks.Summary(1, 0));
        Assert.Equal("Marked 3 holes on the view.", HoleTableMarks.Summary(3, 0));
        Assert.Contains("2 holes could not be marked", HoleTableMarks.Summary(3, 2));
        Assert.Contains("1 hole could not be marked", HoleTableMarks.Summary(0, 1));
    }

    private static readonly SheetPoint Origin = new(0, 0);

    [Fact]
    public void The_tag_gap_is_the_distance_from_the_box_to_the_hole_edge()
    {
        // Box 1 cm to the right of a hole of radius 0.25 cm: gap 0.75.
        Assert.Equal(0.75, HoleTableMarks.TagGapCm(new SheetPoint(1, -0.2), new SheetPoint(2, 0.2), Origin, 0.25), 9);
        // Diagonal: the corner (0.6, 0.8) is 1 cm from the centre.
        Assert.Equal(0.75, HoleTableMarks.TagGapCm(new SheetPoint(0.6, 0.8), new SheetPoint(2, 2), Origin, 0.25), 9);
    }

    [Fact]
    public void A_tag_beside_its_hole_has_no_leader()
    {
        Assert.False(HoleTableMarks.TagLeaderVisible(new SheetPoint(0.3, 0.3), new SheetPoint(0.8, 0.5), Origin, 0.25));
    }

    [Fact]
    public void A_tag_exactly_at_the_distance_has_no_leader_and_just_past_it_has()
    {
        // Radius 0.25 and box edge at 0.75: the gap is exactly 0.5 cm (5 mm).
        Assert.False(HoleTableMarks.TagLeaderVisible(new SheetPoint(0.75, 0), new SheetPoint(1.5, 0.4), Origin, 0.25));
        Assert.True(HoleTableMarks.TagLeaderVisible(new SheetPoint(0.76, 0), new SheetPoint(1.5, 0.4), Origin, 0.25));
    }

    [Fact]
    public void A_tag_twenty_millimetres_away_has_a_leader()
    {
        Assert.True(HoleTableMarks.TagLeaderVisible(new SheetPoint(2.2, 0), new SheetPoint(2.8, 0.4), Origin, 0.25));
    }

    [Fact]
    public void A_tag_over_its_hole_has_no_leader_and_a_negative_gap()
    {
        Assert.False(HoleTableMarks.TagLeaderVisible(new SheetPoint(-0.1, -0.1), new SheetPoint(0.4, 0.2), Origin, 0.25));
        Assert.True(HoleTableMarks.TagGapCm(new SheetPoint(0.1, 0.1), new SheetPoint(0.4, 0.2), Origin, 0.25) < 0);
        Assert.Equal(-0.25, HoleTableMarks.TagGapCm(new SheetPoint(-1, -1), new SheetPoint(1, 1), Origin, 0.25), 9);
    }
}
