using InventorAddin.Core.HoleTable;
using InventorAddin.Core.Holes;
using Xunit;

namespace InventorAddin.Core.Tests.HoleTable;

public class HoleTableTests
{
    private static InventorHoleFacts Facts(
        HoleSeat? seat = HoleSeat.None, bool tapped = false, bool tapered = false, double? diameter = 0.65,
        string? iso = null) =>
        new(seat, tapped, tapped ? "M8x1.25" : null, tapped ? "Metric" : null, null, diameter, HoleTermination.ThroughAll,
            null, 1.2, 0.8, 1.4, 90, false, null, iso, null, null, false)
        {
            TaperedThread = tapered,
            SpotfaceDiameterCm = 1.3,
            SpotfaceDepthCm = 0.1,
        };

    private static readonly HoleTableCentre[] Centres =
        [new("keyA", 1, 1.0, 2.0), new("keyB", 2, 3.0, 4.0)];

    private static IReadOnlyList<HoleTableHole> Build(InventorHoleFacts f, HoleSpec? spec = null, TaperTappedRow? row = null) =>
        HoleTableHoles.From("Plate Hole 3", f, spec, HoleUnits.Metric, Centres, row);

    [Fact]
    public void OneInstancePerCentreWithOwnIdAndPosition()
    {
        var holes = Build(Facts());
        Assert.Equal(2, holes.Count);
        Assert.Equal("Plate Hole 3#keyA", holes[0].Id);
        Assert.Equal("Plate Hole 3#keyB", holes[1].Id);
        Assert.Equal(2, holes[1].CentreIndex);
        Assert.Equal(3.0, holes[1].SheetXCm);
        Assert.Equal(4.0, holes[1].SheetYCm);
        Assert.Equal(0.65, holes[0].DrillDiameterCm);
        Assert.Equal("Plate Hole 3", holes[0].FeatureName);
    }

    [Fact]
    public void TappedWithCounterboreIsTapped()
    {
        Assert.Equal(HoleTableType.Tapped, Build(Facts(HoleSeat.Counterbore, tapped: true))[0].Type);
    }

    [Fact]
    public void TaperTappedWithCountersinkIsTaperTapped()
    {
        Assert.Equal(HoleTableType.TaperTapped, Build(Facts(HoleSeat.Countersink, tapped: true, tapered: true))[0].Type);
    }

    [Theory]
    [InlineData(HoleSeat.None, HoleTableType.Plain)]
    [InlineData(null, HoleTableType.Plain)]
    [InlineData(HoleSeat.Counterbore, HoleTableType.Counterbore)]
    [InlineData(HoleSeat.Countersink, HoleTableType.Countersink)]
    [InlineData(HoleSeat.Spotface, HoleTableType.Spotface)]
    public void SeatDecidesTypeWithoutThread(HoleSeat? seat, HoleTableType expected)
    {
        Assert.Equal(expected, Build(Facts(seat))[0].Type);
    }

    [Fact]
    public void SeatedDowelIsItsSeatTypeAndPrecision()
    {
        var dowel = HoleSpec.ClearanceHole(HoleUnits.Metric, "M6") with { Kind = HoleKind.Dowel };
        var hole = Build(Facts(HoleSeat.Counterbore), dowel)[0];
        Assert.Equal(HoleTableType.Counterbore, hole.Type);
        Assert.True(hole.IsPrecision);
        Assert.Equal(1.2, hole.SeatDiameterCm);
        Assert.Equal(0.8, hole.SeatDepthCm);
    }

    [Fact]
    public void PlainDowelIsPlainAndPrecision()
    {
        var dowel = HoleSpec.ClearanceHole(HoleUnits.Metric, "M6") with { Kind = HoleKind.Dowel };
        var hole = Build(Facts(), dowel)[0];
        Assert.Equal(HoleTableType.Plain, hole.Type);
        Assert.True(hole.IsPrecision);
    }

    [Fact]
    public void PrecisionFollowsAwbDataThenInventorTolerance()
    {
        var toleranced = HoleSpec.ClearanceHole(HoleUnits.Metric, "M6") with { Toleranced = true };
        var plain = HoleSpec.ClearanceHole(HoleUnits.Metric, "M6");
        Assert.True(Build(Facts(), toleranced)[0].IsPrecision);
        Assert.False(Build(Facts(), plain)[0].IsPrecision);
        // AWB data decides: an untoleranced AWB hole whose Inventor diameter carries a tolerance is not precision.
        Assert.False(Build(Facts(iso: "H7"), plain)[0].IsPrecision);
        // No AWB data: Inventor's tolerance decides.
        Assert.True(Build(Facts(iso: "H7"))[0].IsPrecision);
        Assert.False(Build(Facts())[0].IsPrecision);
    }

    [Fact]
    public void TaperTappedTakesAwbTapDrillInCm()
    {
        var row = new TaperTappedRow(2, HoleUnits.Metric, "1/8", "Rc", 8.4m, 10m, 12m, false);
        var hole = Build(Facts(tapped: true, tapered: true, diameter: null), null, row)[0];
        Assert.Equal(0.84, hole.DrillDiameterCm!.Value, 6);

        var imperial = row with { Units = HoleUnits.Imperial, TapDrill = 0.5m };
        Assert.Equal(1.27, Build(Facts(tapped: true, tapered: true, diameter: null), null, imperial)[0].DrillDiameterCm!.Value, 6);

        Assert.Null(Build(Facts(tapped: true, tapered: true, diameter: null))[0].DrillDiameterCm);
    }

    private static HoleTableGroupKey Key(HoleTableType t, double? d, string tie = "") => new(t, d, tie);

    [Fact]
    public void PrecisionGroupsOrderByDiameterIgnoringType()
    {
        var keys = new[] { Key(HoleTableType.Tapped, 0.8), Key(HoleTableType.Plain, 1.0), Key(HoleTableType.Counterbore, 0.5) };
        var sorted = keys.OrderBy(k => k, HoleTableOrder.Groups(true)).Select(k => k.DiameterCm).ToArray();
        Assert.Equal(new double?[] { 0.5, 0.8, 1.0 }, sorted);
    }

    [Fact]
    public void ClearanceGroupsOrderByTypeThenDiameter()
    {
        var keys = new[]
        {
            Key(HoleTableType.Tapped, 0.3), Key(HoleTableType.Plain, 1.0), Key(HoleTableType.Plain, 0.5),
            Key(HoleTableType.Counterbore, 0.2), Key(HoleTableType.Spotface, 0.1), Key(HoleTableType.Countersink, 0.4),
            Key(HoleTableType.TaperTapped, 0.1),
        };
        var sorted = keys.OrderBy(k => k, HoleTableOrder.Groups(false)).ToArray();
        Assert.Equal(
            new[]
            {
                HoleTableType.Plain, HoleTableType.Plain, HoleTableType.Counterbore, HoleTableType.Countersink,
                HoleTableType.Spotface, HoleTableType.Tapped, HoleTableType.TaperTapped,
            },
            sorted.Select(k => k.Type).ToArray());
        Assert.Equal(0.5, sorted[0].DiameterCm);
    }

    [Fact]
    public void TiesUseTieBreakAndNullDiameterSortsLast()
    {
        var keys = new[]
        {
            Key(HoleTableType.Plain, null, "a"), Key(HoleTableType.Plain, 0.5, "b"), Key(HoleTableType.Plain, 0.5, "a"),
            Key(HoleTableType.Plain, 0.9, "a"),
        };
        var sorted = keys.OrderBy(k => k, HoleTableOrder.Groups(false)).ToArray();
        Assert.Equal("a", sorted[0].TieBreak);
        Assert.Equal(0.5, sorted[0].DiameterCm);
        Assert.Equal("b", sorted[1].TieBreak);
        Assert.Equal(0.9, sorted[2].DiameterCm);
        Assert.Null(sorted[3].DiameterCm);
    }

    [Fact]
    public void InstancesOrderYThenXWithTolerance()
    {
        var holes = HoleTableHoles.From("H", Facts(), null, HoleUnits.Metric,
        [
            new("k1", 1, 5.0, 2.0),
            new("k2", 2, 1.0, 2.0002), // same row within tolerance, further left
            new("k3", 3, 9.0, 1.0),
            new("k4", 4, 0.0, 3.0),
        ]);
        var ids = HoleTableOrder.SortInstances(holes).Select(h => h.Id).ToArray();
        Assert.Equal(new[] { "H#k3", "H#k2", "H#k1", "H#k4" }, ids);
    }

    [Theory]
    [InlineData(true, HoleUnits.Metric, 2)]
    [InlineData(true, HoleUnits.Imperial, 3)]
    [InlineData(false, HoleUnits.Metric, 1)]
    [InlineData(false, HoleUnits.Imperial, 2)]
    public void Decimals(bool precision, HoleUnits units, int expected)
    {
        Assert.Equal(expected, HoleTableDecimals.For(precision, units));
    }

    private static readonly TappedRow M8Row = new(4, HoleUnits.Metric, "M8", 8m, 1.25m, 6.8m, "6H", 12m, 16m, false);
    private static readonly TappedRow M6Row = new(3, HoleUnits.Metric, "M6", 6m, 1m, 5.0m, "6H", 9m, 12m, false);

    private static InventorHoleFacts TappedFacts(double? diameter) => Facts(HoleSeat.None, tapped: true, diameter: diameter);

    private static double? TappedDrill(double? facts, double? stored, TappedRow? row) =>
        HoleTableHoles.From("H", TappedFacts(facts), null, HoleUnits.Metric, Centres, null, null, stored, row)[0].DrillDiameterCm;

    // Task 182 replaces task 179's "AWB value wins when both differ" (Tapped_StoredDiameterWinsOverInventors): Inventor's wins.
    [Fact]
    public void Tapped_InventorsDiameterDiffersFromStored_InventorsWins() => Assert.Equal(0.68, TappedDrill(0.68, 0.5, M6Row));

    [Fact]
    public void Tapped_InventorsDiameterAgreesWithStoredWithinNoise_IsExactlyTheStoredValue() =>
        Assert.Equal(0.5, TappedDrill(0.5 + 1e-7, 0.5, M6Row));

    [Fact]
    public void Tapped_NoInventorsDiameter_StoredFitsTheThread_TakesTheStoredOne() => Assert.Equal(0.5, TappedDrill(null, 0.5, M6Row));

    [Fact]
    public void Tapped_NoInventorsDiameter_StoredDoesNotFitTheThread_TakesTheRowsTapDrill() =>
        Assert.Equal(0.68, TappedDrill(null, 0.5, M8Row)!.Value, 6); // stored M6 drill, hole changed to M8

    [Fact]
    public void Tapped_NoFactsDiameter_TakesTheStoredOne() => Assert.Equal(0.5, TappedDrill(null, 0.5, null));

    [Fact]
    public void Tapped_NoStoredData_TakesInventorsDiameter() => Assert.Equal(0.52, TappedDrill(0.52, null, M6Row));

    [Fact]
    public void Tapped_NeitherDiameter_TakesTheRowsTapDrill() => Assert.Equal(0.5, TappedDrill(null, null, M6Row)!.Value, 6);

    [Fact]
    public void Tapped_NoneOfTheThree_HasNoDrill() => Assert.Null(TappedDrill(null, null, null));

    [Fact]
    public void Tapped_StoredDrillGivesTheDescriptionsDrillLine()
    {
        var f = TappedFacts(null) with { ThreadDesignation = "M6x1", DepthCm = 1.2, Termination = HoleTermination.Distance };
        var h = HoleTableHoles.From("H", f, null, HoleUnits.Metric, Centres, null, null, 0.5, null)[0];
        Assert.StartsWith("⌀" + "5.0", HoleTableDescriptions.For(h).PlainLines[0]);
    }

    [Fact]
    public void Tapped_WholeDrillLine_HasTheDiameterAndTheDepth()
    {
        var f = TappedFacts(0.5) with { ThreadDesignation = "M6x1", DepthCm = 1.2, Termination = HoleTermination.Distance };
        var h = HoleTableHoles.From("H", f, null, HoleUnits.Metric, Centres, null, null, 0.5, M6Row)[0];
        Assert.Equal("⌀5.0 ↧12.0", HoleTableDescriptions.For(h).PlainLines[0]);
    }

    [Fact]
    public void NotTapped_IgnoresStoredDiameterAndRow() =>
        Assert.Equal(0.65, HoleTableHoles.From("H", Facts(), null, HoleUnits.Metric, Centres, null, null, 0.4, M6Row)[0].DrillDiameterCm);
}
