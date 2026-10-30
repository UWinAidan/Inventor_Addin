using InventorAddin.Core.Holes;
using static InventorAddin.Core.Tests.Holes.HoleTestWorkbook;

namespace InventorAddin.Core.Tests.Holes;

public sealed class HoleReadBackTests
{
    private static readonly HoleWorkbook Workbook = Create();

    /// <summary>What the add-in would read from the hole it built from <paramref name="geometry"/>.</summary>
    private static InventorHoleFacts FactsOf(HoleGeometry geometry) => new(
        geometry.Seat,
        geometry.Thread is not null,
        geometry.Thread?.Designation,
        geometry.Thread?.ThreadType,
        geometry.Thread is { FullDepth: false } thread ? thread.ThreadDepthCm : null,
        geometry.HoleDiameterCm,
        geometry.Termination,
        geometry.DepthCm,
        geometry.CounterboreDiameterCm,
        geometry.CounterboreDepthCm,
        geometry.CountersinkDiameterCm,
        geometry.CountersinkAngleDeg,
        geometry.DrillPoint?.IsFlat ?? false,
        geometry.DrillPoint?.AngleDeg,
        geometry.Tolerance?.IsoCode,
        geometry.Tolerance?.UpperCm,
        geometry.Tolerance?.LowerCm,
        geometry.Flip);

    private static HoleBuildResult Build(HoleSpec spec)
    {
        var result = HoleRules.Build(spec, Workbook, 1);
        Assert.True(result.Succeeded, string.Join(" | ", result.Problems));
        return result;
    }

    private static InventorHoleFacts PlainFacts(double diameterCm) => new(
        HoleSeat.None, false, null, null, null, diameterCm, HoleTermination.ThroughAll, null,
        null, null, null, null, false, null, null, null, null, false);

    public static TheoryData<HoleSpec> EveryKind() => new()
    {
        Clearance(),
        Clearance() with { ClearanceFit = ClearanceFit.Loose, Termination = HoleTermination.Distance, Depth = 12.5m, Flip = true },
        Clearance() with { ClearanceFit = ClearanceFit.Close, Termination = HoleTermination.ToFace },
        Clearance() with { Seat = HoleSeat.Counterbore, Fastener = "TLOW" },
        Clearance() with { Seat = HoleSeat.Counterbore, Termination = HoleTermination.Distance, Depth = 20m, DrillPoint = "Flat" },
        Clearance() with { Seat = HoleSeat.Countersink },
        HoleSpec.ClearanceHole(HoleUnits.Imperial, "#7") with { Seat = HoleSeat.Countersink, Termination = HoleTermination.Distance, Depth = 0.375m },
        Tapped(),
        Tapped() with { ThreadDepth = 6.25m, DrillDepth = 8.1m, DrillPoint = "118 deg" },
        Tapped() with { Termination = HoleTermination.ThroughAll },
        Tapped("#7-36 UNF") with { Units = HoleUnits.Imperial },
        Dowel(),
        Dowel() with { Tolerance = "Test bore", Termination = HoleTermination.Distance, Depth = 14m },
        Dowel() with { PrecisionFit = PrecisionFit.Press, Flip = true },
        Dowel("1/7\"") with { Units = HoleUnits.Imperial, PrecisionFit = PrecisionFit.Slip },
        Tapped() with { Seat = HoleSeat.Counterbore, Fastener = "TLOW" },
        Tapped() with { Seat = HoleSeat.Countersink, Termination = HoleTermination.ThroughAll },
        Tapped("#7-36 UNF") with { Units = HoleUnits.Imperial, Seat = HoleSeat.Counterbore },
        CboredDowel() with { Termination = HoleTermination.Distance, Depth = 14m },
        CboredDowel() with { CboreDiameter = 7.25m, CboreDepth = 1.5m },
        CsunkDowel() with { PrecisionFit = PrecisionFit.Press },
        CsunkDowel() with { CsinkDiameter = 6.1m, CsinkAngleDeg = 120m },
    };

    [Theory]
    [MemberData(nameof(EveryKind))]
    public void FromGeometry_GivesTheSpecThatBuiltIt(HoleSpec spec)
    {
        var built = Build(spec);

        var result = HoleReadBack.Read(null, FactsOf(built.Geometry!), Workbook, HoleUnits.Metric);

        Assert.Equal(built.Resolved, result.Spec);
        Assert.Equal(HoleSource.Inventor, result.Source);
        Assert.True(result.IsInventorHole);
        Assert.Null(result.Stored);
        Assert.Empty(result.Notes);
    }

    [Theory]
    [MemberData(nameof(EveryKind))]
    public void FromGeometry_RebuildsTheSameGeometry(HoleSpec spec)
    {
        var built = Build(spec);

        var result = HoleReadBack.Read(null, FactsOf(built.Geometry!), Workbook, HoleUnits.Imperial);

        Assert.Equal(built.Geometry, Build(result.Spec).Geometry);
    }

    [Fact]
    public void StoredData_IsUsedAsItIs()
    {
        var built = Build(Dowel() with { PrecisionFit = PrecisionFit.Press });
        var stored = new HoleStoredData(built.Resolved, HoleAppliedState.From(built.Geometry!));
        var read = HoleStoredData.Read(stored.ToAttributes());

        // Facts that would read back as something else: the stored data wins.
        var result = HoleReadBack.Read(read, PlainFacts(0.55), Workbook, HoleUnits.Metric);

        Assert.Equal(HoleSource.Awb, result.Source);
        Assert.False(result.IsInventorHole);
        Assert.Equal(built.Resolved, result.Spec);
        Assert.Equal(stored, result.Stored);
        Assert.Empty(result.Notes);
    }

    [Fact]
    public void UnreadableStoredData_FallsBackToGeometry_WithItsMessage()
    {
        var read = HoleStoredData.Read([new HoleAttribute(HoleStoredData.VersionName, 99)]);

        var result = HoleReadBack.Read(read, PlainFacts(0.55), Workbook, HoleUnits.Metric);

        Assert.Equal(HoleSource.Inventor, result.Source);
        Assert.Equal("T5", result.Spec.Size);
        Assert.Equal(ClearanceFit.Normal, result.Spec.ClearanceFit);
        Assert.Equal(read.Message, Assert.Single(result.Notes));
    }

    [Fact]
    public void UnknownDiameter_TakesTheClosestClearance_InTheDefaultUnits()
    {
        var result = HoleReadBack.Read(null, PlainFacts(0.57), Workbook, HoleUnits.Metric);

        Assert.Equal(HoleKind.Clearance, result.Spec.Kind);
        Assert.Equal("T5", result.Spec.Size);
        Assert.Equal(ClearanceFit.Loose, result.Spec.ClearanceFit);
        Assert.Equal("No Clearance row has a 5.7 mm hole; the closest, T5 Loose, is picked.", Assert.Single(result.Notes));
    }

    [Fact]
    public void EmptyWorkbook_GivesABlankSize_WithANote()
    {
        var result = HoleReadBack.Read(null, PlainFacts(0.57), HoleWorkbook.Empty, HoleUnits.Imperial);

        Assert.Equal(HoleUnits.Imperial, result.Spec.Units);
        Assert.Equal("", result.Spec.Size);
        Assert.Equal("No Clearance row has a 5.7 mm hole; pick a size.", Assert.Single(result.Notes));
    }

    [Fact]
    public void DefaultUnits_AreTriedFirst()
    {
        // 5.5 mm is T5 Normal in metric and, in this workbook, #X Normal in imperial.
        var workbook = Workbook with
        {
            Clearance = [.. Workbook.Clearance, new ClearanceRow(9, HoleUnits.Imperial, "#X", 0.2m, 0.2m, 0.55m / 2.54m, 0.3m, false)],
        };
        var facts = PlainFacts(0.55);

        var metric = HoleReadBack.Read(null, facts, workbook, HoleUnits.Metric).Spec;
        var imperial = HoleReadBack.Read(null, facts, workbook, HoleUnits.Imperial).Spec;

        Assert.Equal((HoleUnits.Metric, "T5"), (metric.Units, metric.Size));
        Assert.Equal((HoleUnits.Imperial, "#X"), (imperial.Units, imperial.Size));
    }

    [Fact]
    public void DiameterWithAToleranceNotInTheWorkbook_IsADowel_WithTheDefaultTolerance()
    {
        var facts = PlainFacts(0.7) with { ToleranceIsoCode = "K6", ToleranceUpperCm = 0.0003, ToleranceLowerCm = -0.0006 };

        var result = HoleReadBack.Read(null, facts, Workbook, HoleUnits.Metric);

        Assert.Equal(HoleKind.Dowel, result.Spec.Kind);
        Assert.Equal("7 mm", result.Spec.Size);
        Assert.Equal(PrecisionFit.Normal, result.Spec.PrecisionFit);
        Assert.Null(result.Spec.Tolerance);
        Assert.Equal(
            "The hole's tolerance is not in the Precision Tol tab; the default tolerance is picked.",
            Assert.Single(result.Notes));
    }

    [Fact]
    public void ToleranceOnAClearanceSize_IsAClearanceHole()
    {
        var facts = PlainFacts(0.55) with { ToleranceIsoCode = "H7", ToleranceUpperCm = 0.0012, ToleranceLowerCm = 0 };

        var result = HoleReadBack.Read(null, facts, Workbook, HoleUnits.Metric);

        Assert.Equal(HoleKind.Clearance, result.Spec.Kind);
        Assert.Equal("T5", result.Spec.Size);
    }

    [Fact]
    public void ThreadNotInTheWorkbook_GivesABlankSize_InTheThreadsUnits()
    {
        var facts = PlainFacts(0.5) with
        {
            Tapped = true,
            ThreadDesignation = "1/4-20 UNC",
            ThreadType = TappedRow.ImperialThreadType,
        };

        var result = HoleReadBack.Read(null, facts, Workbook, HoleUnits.Metric);

        Assert.Equal(HoleKind.Tapped, result.Spec.Kind);
        Assert.Equal(HoleUnits.Imperial, result.Spec.Units);
        Assert.Equal("", result.Spec.Size);
        Assert.Equal("The thread 1/4-20 UNC is not in the Tapped tab; pick a size.", Assert.Single(result.Notes));
    }

    [Fact]
    public void TappedHoleWithASeatNotInTheWorkbook_KeepsTheSeat_AndTakesTheFirstRowOfTheSameSize_WithANote()
    {
        var facts = FactsOf(Build(Tapped()).Geometry!) with
        {
            Seat = HoleSeat.Counterbore,
            CounterboreDiameterCm = 0.92,
            CounterboreDepthCm = 0.5,
        };

        var result = HoleReadBack.Read(null, facts, Workbook, HoleUnits.Metric);

        Assert.Equal(HoleSeat.Counterbore, result.Spec.Seat);
        Assert.Equal("M5", result.Spec.Size);
        Assert.Equal("TCAP", result.Spec.Fastener);
        Assert.Equal((9.2m, 5m), (result.Spec.CboreDiameter, result.Spec.CboreDepth));
        Assert.Equal(
            "No Counterbore row the size of M5 matches the hole's counterbore; the first, TCAP, is picked as " +
            "Fastener type, and the hole's own counterbore is kept.",
            Assert.Single(result.Notes));
    }

    [Fact]
    public void TappedHoleWithASeat_WithNoRowOfTheSameSize_KeepsTheSeatWithoutAFastener()
    {
        var workbook = Workbook with { Countersink = [] };
        var facts = FactsOf(Build(Tapped() with { Seat = HoleSeat.Countersink }).Geometry!);

        var result = HoleReadBack.Read(null, facts, workbook, HoleUnits.Metric);

        Assert.Equal(HoleSeat.Countersink, result.Spec.Seat);
        Assert.Null(result.Spec.Fastener);
        Assert.Empty(result.Notes);

        // Task 126: the seat needs no row; the hole's own countersink builds.
        Assert.Equal((10.4m, 90m), (result.Spec.CsinkDiameter, result.Spec.CsinkAngleDeg));
        Assert.True(HoleRules.Build(result.Spec, workbook, 1).Succeeded);
    }

    [Fact]
    public void AStoredClearanceSeat_OpensOnItsStoredDimensions_AndOneStoredBeforeTask126_HasNone()
    {
        var built = Build(Clearance() with { Seat = HoleSeat.Counterbore, CboreDiameter = 11m, CboreDepth = 4m });
        var stored = new HoleStoredData(built.Resolved, HoleAppliedState.From(built.Geometry!));
        var facts = FactsOf(built.Geometry!) with { CounterboreDiameterCm = 1.2 };

        var result = HoleReadBack.Read(new HoleStoredDataReadResult(stored, ""), facts, Workbook, HoleUnits.Metric);
        Assert.Equal((11m, 4m), (result.Spec.CboreDiameter, result.Spec.CboreDepth));

        // A version 1 spec names only its row; it is not filled from the hole, and resolves to the row.
        var old = stored with { Spec = stored.Spec with { CboreDiameter = null, CboreDepth = null } };
        var oldResult = HoleReadBack.Read(new HoleStoredDataReadResult(old, ""), facts, Workbook, HoleUnits.Metric);
        Assert.Null(oldResult.Spec.CboreDiameter);
        Assert.Equal(9.5m, HoleRules.Build(oldResult.Spec, Workbook, 1).Resolved.CboreDiameter);
    }

    [Fact]
    public void SeatedHoleWithATolerance_IsASeatedDowel()
    {
        var facts = FactsOf(Build(CboredDowel()).Geometry!);

        var result = HoleReadBack.Read(null, facts, Workbook, HoleUnits.Metric);

        Assert.Equal(HoleKind.Dowel, result.Spec.Kind);
        Assert.Equal(HoleSeat.Counterbore, result.Spec.Seat);
        Assert.Equal("5 mm", result.Spec.Size);
        Assert.Null(result.Spec.Fastener);
        Assert.Equal((9.5m, 5.2m), (result.Spec.CboreDiameter, result.Spec.CboreDepth));
        Assert.Equal("Test location", result.Spec.Tolerance);

        // Without the tolerance it is a counterbored clearance hole.
        var clearance = HoleReadBack.Read(
            null, facts with { ToleranceIsoCode = null, ToleranceUpperCm = null, ToleranceLowerCm = null }, Workbook,
            HoleUnits.Metric);
        Assert.Equal(HoleKind.Clearance, clearance.Spec.Kind);
        Assert.Equal(HoleSeat.Counterbore, clearance.Spec.Seat);
    }

    [Fact]
    public void SeatedDowel_FromGeometry_OpensOnItsOwnSeat_WithItsDepthExpression()
    {
        var facts = FactsOf(Build(CboredDowel() with { CboreDiameter = 7.25m, CboreDepth = 1.5m }).Geometry!)
            with { CounterboreDepthExpression = "d0 / 4" };

        var result = HoleReadBack.Read(null, facts, Workbook, HoleUnits.Metric);

        Assert.Equal((7.25m, 1.5m, "d0 / 4"), (result.Spec.CboreDiameter, result.Spec.CboreDepth, result.Spec.CboreDepthExpression));
        Assert.Empty(result.Notes);
    }

    [Fact]
    public void SeatedDowelStoredBeforeTask106_OpensOnTheSeatTheHoleHas()
    {
        // Stored by task 099: a fastener and no seat sizes. The hole's own counterbore is read in.
        var built = Build(CboredDowel() with { CboreDiameter = 8.5m, CboreDepth = 3.1m });
        var attributes = new HoleStoredData(built.Resolved, HoleAppliedState.From(built.Geometry!)).ToAttributes()
            .Where(a => a.Name is not HoleStoredData.CboreDiameterName and not HoleStoredData.CboreDepthName)
            .Append(new HoleAttribute(HoleStoredData.FastenerName, "TLOW"));
        var facts = FactsOf(built.Geometry!) with { CounterboreDepthExpression = "d1" };

        var result = HoleReadBack.Read(HoleStoredData.Read(attributes), facts, Workbook, HoleUnits.Metric);

        Assert.Equal(HoleSource.Awb, result.Source);
        Assert.Null(result.Spec.Fastener);
        Assert.Equal((8.5m, 3.1m, "d1"), (result.Spec.CboreDiameter, result.Spec.CboreDepth, result.Spec.CboreDepthExpression));
        Assert.Equal(built.Geometry, Build(result.Spec).Geometry! with { CounterboreDepthExpression = null });
    }

    [Fact]
    public void SeatedDowelStored_KeepsItsStoredSeat_OverTheHoles()
    {
        var built = Build(CsunkDowel() with { CsinkDiameter = 6.1m, CsinkAngleDeg = 120m });
        var read = HoleStoredData.Read(new HoleStoredData(built.Resolved, HoleAppliedState.From(built.Geometry!)).ToAttributes());
        var facts = FactsOf(built.Geometry!) with { CountersinkDiameterCm = 0.9, CountersinkAngleDeg = 82 };

        var result = HoleReadBack.Read(read, facts, Workbook, HoleUnits.Metric);

        Assert.Equal((6.1m, 120m), (result.Spec.CsinkDiameter, result.Spec.CsinkAngleDeg));
    }

    [Fact]
    public void WithDowelSeatFromHole_LeavesOtherSpecsAsTheyAre()
    {
        var facts = PlainFacts(0.5) with { CounterboreDiameterCm = 0.9, CounterboreDepthCm = 0.3 };
        var tapped = Tapped() with { Seat = HoleSeat.Counterbore, Fastener = "TCAP" };
        var unseated = Dowel();

        Assert.Same(tapped, HoleReadBack.WithDowelSeatFromHole(tapped, facts));
        Assert.Same(unseated, HoleReadBack.WithDowelSeatFromHole(unseated, facts));

        // Imperial lengths come back in inches; an angle to 4 decimals.
        var imperial = HoleReadBack.WithDowelSeatFromHole(
            Dowel("1/7\"") with { Units = HoleUnits.Imperial, Seat = HoleSeat.Countersink },
            facts with { CountersinkDiameterCm = 0.635, CountersinkAngleDeg = 82.000001 });
        Assert.Equal((0.25m, 82m), (imperial.CsinkDiameter, imperial.CsinkAngleDeg));
    }

    [Fact]
    public void SpotFace_ReadsAsNoSeat_WithANote()
    {
        var result = HoleReadBack.Read(null, PlainFacts(0.55) with { Seat = null }, Workbook, HoleUnits.Metric);

        Assert.Equal(HoleSeat.None, result.Spec.Seat);
        Assert.Equal(HoleFeatureMessages.SpotFaceNotOffered, Assert.Single(result.Notes));
    }

    [Fact]
    public void UnknownTermination_IsThroughAll_WithANote()
    {
        var result = HoleReadBack.Read(null, PlainFacts(0.55) with { Termination = null }, Workbook, HoleUnits.Metric);

        Assert.Equal(HoleTermination.ThroughAll, result.Spec.Termination);
        Assert.Single(result.Notes);
    }

    [Fact]
    public void UnknownDrillPointAngle_TakesTheDefault_WithANote()
    {
        var facts = PlainFacts(0.55) with { Termination = HoleTermination.Distance, DepthCm = 1.2, DrillPointAngleDeg = 100 };

        var result = HoleReadBack.Read(null, facts, Workbook, HoleUnits.Metric);

        Assert.Null(result.Spec.DrillPoint);
        Assert.Equal(12m, result.Spec.Depth);
        Assert.Equal(
            "The Drill Point tab has no row for the hole's drill point angle; the default is picked.",
            Assert.Single(result.Notes));
    }

    [Fact]
    public void UnknownCounterbore_TakesTheClosest_WithANote()
    {
        var facts = PlainFacts(0.55) with { Seat = HoleSeat.Counterbore, CounterboreDiameterCm = 0.92, CounterboreDepthCm = 0.5 };

        var result = HoleReadBack.Read(null, facts, Workbook, HoleUnits.Metric);

        Assert.Equal(HoleSeat.Counterbore, result.Spec.Seat);
        Assert.Equal("T5", result.Spec.Size);
        Assert.Equal("TCAP", result.Spec.Fastener);
        Assert.Equal((9.2m, 5m), (result.Spec.CboreDiameter, result.Spec.CboreDepth));
        Assert.Equal(
            "No Counterbore row has a 9.2 mm counterbore; the closest, T5 TCAP, is picked, and the hole's own " +
            "counterbore is kept.",
            Assert.Single(result.Notes));
    }

    [Theory]
    [InlineData(HoleUnits.Metric, 6.0, "6")]
    [InlineData(HoleUnits.Imperial, 0.25, "0.25")]
    public void FromCm_RoundsInTheUnits(HoleUnits units, double value, string expected)
    {
        var cm = HoleRules.ToCm((decimal)value, units);

        Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), HoleReadBack.FromCm(cm, units));
    }
}
