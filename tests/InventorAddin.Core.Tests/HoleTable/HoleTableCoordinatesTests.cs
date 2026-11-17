using InventorAddin.Core.HoleTable;
using InventorAddin.Core.Holes;

namespace InventorAddin.Core.Tests.HoleTable;

public class HoleTableCoordinatesTests
{
    private static HoleTableHole At(double x, double y) => new(
        "F#k", 1, "F", HoleUnits.Metric, HoleTableType.Plain, false, 0.5, null, null, false, null,
        null, null, null, null, null, null, false, null, x, y);

    private static readonly SheetPoint Origin = new(10, 20);

    [Fact]
    public void No_origin_stops()
    {
        var r = HoleTableOriginResult.From(Array.Empty<SheetPoint>());
        Assert.True(r.IsStop);
        Assert.Equal(HoleTableMessages.NoOrdinateOrigin, r.StopMessage);
        Assert.Null(r.Origin);
    }

    [Fact]
    public void Two_different_origins_stop()
    {
        var r = HoleTableOriginResult.From(new[] { new SheetPoint(1, 1), new SheetPoint(1.01, 1) });
        Assert.True(r.IsStop);
        Assert.Equal(HoleTableMessages.MoreThanOneOrdinateOrigin, r.StopMessage);
    }

    [Fact]
    public void Origins_at_one_point_give_that_origin()
    {
        var r = HoleTableOriginResult.From(new[] { new SheetPoint(1, 1), new SheetPoint(1.0005, 1) });
        Assert.False(r.IsStop);
        Assert.Null(r.StopMessage);
        Assert.Equal(new SheetPoint(1, 1), r.Origin);
    }

    [Fact]
    public void One_origin_is_returned()
    {
        var r = HoleTableOriginResult.From(new[] { Origin });
        Assert.Equal(Origin, r.Origin);
    }

    [Fact]
    public void Half_scale_clearance_table_in_mm()
    {
        var c = HoleTableCoordinates.For(At(15, 22.5), Origin, 0.5, HoleUnits.Metric, false);
        Assert.Equal("100.0", c.XText);
        Assert.Equal("50.0", c.YText);
        Assert.Equal(100.0, c.X);
    }

    [Fact]
    public void Precision_table_uses_two_decimals()
    {
        var c = HoleTableCoordinates.For(At(15, 22.5), Origin, 0.5, HoleUnits.Metric, true);
        Assert.Equal("100.00", c.XText);
        Assert.Equal("50.00", c.YText);
    }

    [Fact]
    public void Inches_convert_and_use_their_decimals()
    {
        var clearance = HoleTableCoordinates.For(At(12.54, 20), Origin, 1, HoleUnits.Imperial, false);
        Assert.Equal("1.00", clearance.XText);
        Assert.Equal("0.00", clearance.YText);
        var precision = HoleTableCoordinates.For(At(12.54, 20), Origin, 1, HoleUnits.Imperial, true);
        Assert.Equal("1.000", precision.XText);
    }

    [Fact]
    public void Left_and_below_are_negative()
    {
        var c = HoleTableCoordinates.For(At(8, 19), Origin, 1, HoleUnits.Metric, false);
        Assert.Equal("-20.0", c.XText);
        Assert.Equal("-10.0", c.YText);
    }

    [Fact]
    public void Tiny_negative_shows_zero_not_minus_zero()
    {
        var c = HoleTableCoordinates.For(At(9.9999, 20), Origin, 1, HoleUnits.Metric, false);
        Assert.Equal("0.0", c.XText);
        Assert.Equal(0.0, c.X);
        Assert.False(double.IsNegative(c.X));
    }

    [Theory]
    [InlineData(10.125, "1.3")]
    [InlineData(9.875, "-1.3")]
    [InlineData(10.625, "6.3")]
    public void Rounds_half_away_from_zero(double sheetX, string expected)
    {
        var c = HoleTableCoordinates.For(At(sheetX, 20), Origin, 1, HoleUnits.Metric, false);
        Assert.Equal(expected, c.XText);
    }

    [Fact]
    public void Floating_point_noise_does_not_defeat_the_half()
    {
        var c = HoleTableCoordinates.For(At(10.0125, 20), Origin, 1, HoleUnits.Metric, true);
        Assert.Equal("0.13", c.XText);
    }

    [Fact]
    public void Origins_are_compared_by_spread_not_only_to_the_first()
    {
        var r = HoleTableOriginResult.From(new[] { new SheetPoint(1, 1), new SheetPoint(1.0008, 1), new SheetPoint(1.0016, 1) });
        Assert.True(r.IsStop);
    }

    [Fact]
    public void Messages_word_singular_and_plural()
    {
        Assert.Equal("1 hole was left out because it is not seen end on in this view.", HoleTableMessages.HolesLeftOut(1));
        Assert.Equal("3 holes were left out because they are not seen end on in this view.", HoleTableMessages.HolesLeftOut(3));
    }

    [Fact]
    public void Zero_scale_throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => HoleTableCoordinates.For(At(1, 1), Origin, 0, HoleUnits.Metric, false));
    }
}
