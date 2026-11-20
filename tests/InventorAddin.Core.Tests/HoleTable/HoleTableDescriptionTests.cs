using InventorAddin.Core.HoleTable;
using InventorAddin.Core.Holes;

namespace InventorAddin.Core.Tests.HoleTable;

public class HoleTableDescriptionTests
{
    private const string D = "⌀";
    private const string CB = "⌴";
    private const string CS = "⌵";
    private const string Dp = "↧";

    private static HoleTableHole Hole(
        HoleTableType type = HoleTableType.Plain,
        HoleUnits units = HoleUnits.Metric,
        double? drillCm = 0.66,
        HoleTermination? termination = HoleTermination.ThroughAll,
        double? depthCm = null) => new(
        "F#k", 1, "F", units, type, false, drillCm, termination, depthCm, false, null,
        null, null, null, null, null, null, false, null, 0, 0);

    private static string Text(HoleTableHole h) => HoleTableDescriptions.For(h).PlainText;

    [Fact]
    public void Plain_through() => Assert.Equal($"{D}6.6 THRU", Text(Hole()));

    [Fact]
    public void Plain_blind() =>
        Assert.Equal($"{D}6.6 {Dp}12.0", Text(Hole(termination: HoleTermination.Distance, depthCm: 1.2)));

    [Fact]
    public void Termination_to_face() =>
        Assert.Equal($"{D}6.6 TO FACE", Text(Hole(termination: HoleTermination.ToFace)));

    [Fact]
    public void Dowel_h7_through_stacks_deviations()
    {
        var h = Hole(drillCm: 0.6) with
        {
            IsPrecision = true,
            Tolerance = new HoleTolerance("H7", "H7", 0.0012, 0),
        };
        var d = HoleTableDescriptions.For(h);
        Assert.Equal($"{D}6.00 H7 [+0.012/+0.000] THRU", d.PlainText);
        Assert.Equal("+0.012", d.Tolerance!.Upper);
        Assert.Equal("+0.000", d.Tolerance.Lower);
        Assert.Equal(3, d.FirstLineBeforeStack.Count);
        Assert.Equal(" THRU", d.FirstLineAfterStack.Single().Text);
    }

    [Fact]
    public void Custom_tolerance_has_no_code_and_blind_depth()
    {
        var h = Hole(drillCm: 0.6, termination: HoleTermination.Distance, depthCm: 1.0) with
        {
            IsPrecision = true,
            Tolerance = new HoleTolerance("Custom", null, 0.0005, -0.0003),
        };
        Assert.Equal($"{D}6.00 [+0.005/-0.003] {Dp}10.00", Text(h));
    }

    [Fact]
    public void Counterbore_line()
    {
        var h = Hole(HoleTableType.Counterbore) with { SeatDiameterCm = 1.1, SeatDepthCm = 0.64 };
        var lines = HoleTableDescriptions.For(h).PlainLines;
        Assert.Equal([$"{D}6.6 THRU", $"{CB}{D}11.0 {Dp}6.4"], lines);
    }

    [Fact]
    public void Countersink_line_has_whole_degrees()
    {
        var h = Hole(HoleTableType.Countersink) with { SeatDiameterCm = 1.24, SeatAngleDeg = 90 };
        Assert.Equal([$"{D}6.6 THRU", $"{CS}{D}12.4 X 90°"], HoleTableDescriptions.For(h).PlainLines);
    }

    [Fact]
    public void Spotface_line()
    {
        var h = Hole(HoleTableType.Spotface) with { SeatDiameterCm = 1.4, SeatDepthCm = 0.1 };
        Assert.Equal([$"{D}6.6 THRU", $"SF {D}14.0 {Dp}1.0"], HoleTableDescriptions.For(h).PlainLines);
    }

    [Fact]
    public void Tapped_blind()
    {
        var h = Hole(HoleTableType.Tapped, drillCm: 0.5, termination: HoleTermination.Distance, depthCm: 1.2) with
        {
            ThreadDesignation = "M6x1",
            ThreadClass = "6H",
            ThreadDepthCm = 1.0,
        };
        Assert.Equal([$"{D}5.0 {Dp}12.0", $"M6x1 - 6H {Dp}10.0"], HoleTableDescriptions.For(h).PlainLines);
    }

    [Fact]
    public void Tapped_through_full_depth()
    {
        var h = Hole(HoleTableType.Tapped, drillCm: 0.5) with { ThreadDesignation = "M6x1", ThreadClass = "6H" };
        Assert.Equal([$"{D}5.0 THRU", "M6x1 - 6H THRU"], HoleTableDescriptions.For(h).PlainLines);
    }

    [Fact]
    public void Tapped_through_with_thread_depth()
    {
        var h = Hole(HoleTableType.Tapped, drillCm: 0.5) with
        {
            ThreadDesignation = "M6x1",
            ThreadClass = "6H",
            ThreadDepthCm = 0.8,
        };
        Assert.Equal([$"{D}5.0 THRU", $"M6x1 - 6H {Dp}8.0"], HoleTableDescriptions.For(h).PlainLines);
    }

    [Fact]
    public void Tapped_with_counterbore_puts_seat_between_drill_and_thread()
    {
        var h = Hole(HoleTableType.Tapped, drillCm: 0.5) with
        {
            ThreadDesignation = "M6x1",
            ThreadClass = "6H",
            Seat = HoleSeat.Counterbore,
            SeatDiameterCm = 1.1,
            SeatDepthCm = 0.64,
        };
        Assert.Equal([$"{D}5.0 THRU", $"{CB}{D}11.0 {Dp}6.4", "M6x1 - 6H THRU"], HoleTableDescriptions.For(h).PlainLines);
    }

    [Fact]
    public void Taper_tapped_with_drill_and_engagement()
    {
        var h = Hole(HoleTableType.TaperTapped, drillCm: 0.82, termination: HoleTermination.Distance, depthCm: 1.0) with
        {
            ThreadDesignation = "Rc 1/8",
            TaperedThread = true,
            TaperEngagementCm = 0.65,
        };
        Assert.Equal([$"{D}8.2 {Dp}10.0", $"Rc 1/8 {Dp}6.5"], HoleTableDescriptions.For(h).PlainLines);
    }

    [Theory]
    [InlineData(HoleTermination.ThroughAll)]
    [InlineData(HoleTermination.Distance)]
    public void Taper_tapped_without_drill_or_engagement_is_the_designation_alone(HoleTermination termination)
    {
        var h = Hole(HoleTableType.TaperTapped, drillCm: null, termination: termination, depthCm: 1.0) with
        {
            ThreadDesignation = "Rc 1/8",
            TaperedThread = true,
        };
        Assert.Equal(["Rc 1/8"], HoleTableDescriptions.For(h).PlainLines);
    }

    [Fact]
    public void Tapped_blind_with_full_depth_thread_is_the_designation_alone()
    {
        var h = Hole(HoleTableType.Tapped, drillCm: 0.5, termination: HoleTermination.Distance, depthCm: 1.2) with
        {
            ThreadDesignation = "M6x1",
            ThreadClass = "6H",
        };
        Assert.Equal([$"{D}5.0 {Dp}12.0", "M6x1 - 6H"], HoleTableDescriptions.For(h).PlainLines);
    }

    [Fact]
    public void Tapped_spotface_is_described_as_a_spotface()
    {
        var h = Hole(HoleTableType.Tapped, drillCm: 0.5) with
        {
            ThreadDesignation = "M6x1",
            Seat = HoleSeat.Spotface,
            SeatDiameterCm = 1.4,
            SeatDepthCm = 0.1,
        };
        Assert.Equal([$"{D}5.0 THRU", $"SF {D}14.0 {Dp}1.0", "M6x1 THRU"], HoleTableDescriptions.For(h).PlainLines);
    }

    [Fact]
    public void Tapped_countersink_is_described_as_a_countersink()
    {
        var h = Hole(HoleTableType.Tapped, drillCm: 0.5) with
        {
            ThreadDesignation = "M6x1",
            Seat = HoleSeat.Countersink,
            SeatDiameterCm = 1.24,
            SeatAngleDeg = 90,
        };
        Assert.Equal([$"{D}5.0 THRU", $"{CS}{D}12.4 X 90°", "M6x1 THRU"], HoleTableDescriptions.For(h).PlainLines);
    }

    [Fact]
    public void Tapped_counterbore_and_spotface_at_the_same_sizes_have_different_keys()
    {
        var h = Hole(HoleTableType.Tapped, drillCm: 0.5) with
        {
            ThreadDesignation = "M6x1",
            SeatDiameterCm = 1.4,
            SeatDepthCm = 0.1,
        };
        Assert.NotEqual(
            HoleTableDescriptions.For(h with { Seat = HoleSeat.Counterbore }).Key,
            HoleTableDescriptions.For(h with { Seat = HoleSeat.Spotface }).Key);
    }

    [Fact]
    public void Inch_plain_and_decimals()
    {
        var h = Hole(units: HoleUnits.Imperial, drillCm: 0.25 * 2.54, termination: HoleTermination.Distance, depthCm: 0.5 * 2.54);
        Assert.Equal($"{D}0.25 {Dp}0.50", Text(h));
    }

    [Fact]
    public void Inch_precision_has_three_decimals_and_four_decimal_deviations()
    {
        var h = Hole(units: HoleUnits.Imperial, drillCm: 0.25 * 2.54) with
        {
            IsPrecision = true,
            Tolerance = new HoleTolerance("Custom", null, 0.0005 * 2.54, -0.0002 * 2.54),
        };
        Assert.Equal($"{D}0.250 [+0.0005/-0.0002] THRU", Text(h));
    }

    [Fact]
    public void Inch_tapped()
    {
        var h = Hole(HoleTableType.Tapped, HoleUnits.Imperial, drillCm: 0.159 * 2.54) with
        {
            ThreadDesignation = "10-32 UNF",
            ThreadClass = "2B",
            ThreadDepthCm = 0.5 * 2.54,
        };
        Assert.Equal([$"{D}0.159 THRU", $"10-32 UNF - 2B {Dp}0.50"], HoleTableDescriptions.For(h).PlainLines);
    }

    [Theory]
    [InlineData(0.225, "2.25")]
    [InlineData(0.5, "5.0")]
    [InlineData(0.66, "6.6")]
    [InlineData(0.22501, "2.25")]
    [InlineData(0.225000000001, "2.25")]
    [InlineData(0.2250001, "2.25")]
    public void Clearance_mm_keeps_extra_decimals(double drillCm, string expected) =>
        Assert.Equal($"{D}{expected} THRU", Text(Hole(drillCm: drillCm)));

    [Fact]
    public void Mm_depth_of_6_5_shows_6_5() =>
        Assert.Equal($"{D}6.6 {Dp}6.5", Text(Hole(termination: HoleTermination.Distance, depthCm: 0.65)));

    [Theory]
    [InlineData(0.32, "3.20")]
    [InlineData(0.3205, "3.205")]
    public void Precision_mm_keeps_its_two_decimals_and_a_third(double drillCm, string expected) =>
        Assert.Equal($"{D}{expected} THRU", HoleTableDescriptions.For(Hole(drillCm: drillCm), 2).PlainText);

    [Theory]
    [InlineData(0.25, "0.25")]
    [InlineData(0.1875, "0.1875")]
    [InlineData(0.18755, "0.1876")]
    public void Inch_clearance_keeps_up_to_four_decimals(double inches, string expected) =>
        Assert.Equal($"{D}{expected} THRU", Text(Hole(units: HoleUnits.Imperial, drillCm: inches * 2.54)));

    [Fact]
    public void Explicit_decimals_are_used()
    {
        Assert.Equal($"{D}6.60 THRU", HoleTableDescriptions.For(Hole(), 2).PlainText);
    }

    [Fact]
    public void Keys_ignore_position_and_id()
    {
        var a = Hole();
        var b = a with { Id = "G#z", SheetXCm = 5, SheetYCm = 9, CentreIndex = 4 };
        Assert.Equal(HoleTableDescriptions.For(a).Key, HoleTableDescriptions.For(b).Key);
    }

    public static IEnumerable<object[]> Differences()
    {
        yield return [(Func<HoleTableHole, HoleTableHole>)(h => h with { Termination = HoleTermination.Distance, DepthCm = 1.0 })];
        yield return [(Func<HoleTableHole, HoleTableHole>)(h => h with { DrillDiameterCm = 0.7 })];
        yield return [(Func<HoleTableHole, HoleTableHole>)(h => h with { Type = HoleTableType.Counterbore, SeatDiameterCm = 1.1, SeatDepthCm = 0.6 })];
        yield return [(Func<HoleTableHole, HoleTableHole>)(h => h with { Type = HoleTableType.Counterbore, SeatDiameterCm = 1.2, SeatDepthCm = 0.6 })];
        yield return [(Func<HoleTableHole, HoleTableHole>)(h => h with { Type = HoleTableType.Tapped, ThreadDesignation = "M6x1", ThreadClass = "6H" })];
        yield return [(Func<HoleTableHole, HoleTableHole>)(h => h with { Type = HoleTableType.Tapped, ThreadDesignation = "M6x1.25", ThreadClass = "6H" })];
        yield return [(Func<HoleTableHole, HoleTableHole>)(h => h with { Tolerance = new HoleTolerance("H7", "H7", 0.0012, 0) })];
        yield return [(Func<HoleTableHole, HoleTableHole>)(h => h with { Tolerance = new HoleTolerance("H8", "H8", 0.0012, 0) })];
        yield return [(Func<HoleTableHole, HoleTableHole>)(h => h with { Tolerance = new HoleTolerance("Custom", null, 0.0012, 0) })];
        yield return [(Func<HoleTableHole, HoleTableHole>)(h => h with { Tolerance = new HoleTolerance("Custom", null, 0.0012, -0.0004) })];
    }

    [Theory]
    [MemberData(nameof(Differences))]
    public void Real_differences_change_the_key(Func<HoleTableHole, HoleTableHole> change)
    {
        var plain = Hole();
        Assert.NotEqual(HoleTableDescriptions.For(plain).Key, HoleTableDescriptions.For(change(plain)).Key);
    }

    [Fact]
    public void Tolerance_codes_and_deviations_differ_between_each_other()
    {
        var keys = new[]
        {
            new HoleTolerance("H7", "H7", 0.0012, 0),
            new HoleTolerance("H8", "H8", 0.0012, 0),
            new HoleTolerance("Custom", null, 0.0012, 0),
            new HoleTolerance("Custom", null, 0.0012, -0.0004),
        }.Select(t => HoleTableDescriptions.For(Hole() with { Tolerance = t }).Key).ToList();
        Assert.Equal(keys.Count, keys.Distinct().Count());
    }
}
