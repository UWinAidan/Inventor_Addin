using InventorAddin.Core.Holes;

namespace InventorAddin.Core.Tests.Holes;

/// <summary>
/// A counterbore or countersink on a clearance size with no row of the seat (task 137; spec 09 question 26, Aidan
/// 2026-10-30): the through hole is the Clearance row at the chosen fit, the seat comes from its boxes, and the hole
/// has no fastener. Checked, stored and read back. Sizes and values are invented.
/// </summary>
public sealed class HoleClearanceSeatWithoutRowTests
{
    // T7 has a Clearance row and no seat rows; TBIG has a Counterbore row and no Countersink row.
    private static readonly HoleWorkbook Workbook = HoleTestWorkbook.Create() with
    {
        Clearance = [.. HoleTestWorkbook.Create().Clearance, new ClearanceRow(9, HoleUnits.Metric, "T7", 7m, 7.2m, 7.4m, 7.6m, false)],
    };

    private static HoleSpec T7Counterbore(decimal? diameter = 12m, decimal? depth = 4m) =>
        HoleTestWorkbook.Clearance("T7") with { Seat = HoleSeat.Counterbore, CboreDiameter = diameter, CboreDepth = depth };

    private static HoleSpec BigCountersink() =>
        HoleTestWorkbook.Clearance("TBIG") with
        {
            Seat = HoleSeat.Countersink,
            ClearanceFit = ClearanceFit.Close,
            CsinkDiameter = 16m,
            CsinkAngleDeg = 90m,
        };

    private static HoleBuildResult Build(HoleSpec spec) => HoleRules.Build(spec, Workbook, 1);

    private static InventorHoleFacts Facts(double diameterCm) => new(
        HoleSeat.None, false, null, null, null, diameterCm, HoleTermination.ThroughAll, null,
        null, null, null, null, false, null, null, null, null, false);

    // ---- Rules ----

    [Fact]
    public void Counterbore_WithNoRow_AsksForItsValues_AndNotForASize()
    {
        var result = Build(T7Counterbore(null, null));

        Assert.False(result.Succeeded);
        Assert.Equal(
            [HoleField.CounterboreDiameter, HoleField.CounterboreDepth],
            result.Problems.Select(p => p.Field).ToList());
    }

    [Fact]
    public void Counterbore_WithNoRow_TakesTheClearanceRowAtTheChosenFit_AndTheBoxes()
    {
        var result = Build(T7Counterbore() with { ClearanceFit = ClearanceFit.Loose });

        Assert.True(result.Succeeded, string.Join(" | ", result.Problems));
        Assert.Equal(0.76, result.Geometry!.HoleDiameterCm, 12);
        Assert.Equal(1.2, result.Geometry.CounterboreDiameterCm!.Value, 12);
        Assert.Equal(0.4, result.Geometry.CounterboreDepthCm!.Value, 12);
        Assert.Null(result.Resolved.Fastener);
        Assert.Equal(ClearanceFit.Loose, result.Resolved.ClearanceFit);
    }

    [Fact]
    public void Countersink_WithNoRow_TakesTheClearanceRowAtTheChosenFit()
    {
        var result = Build(BigCountersink());

        Assert.True(result.Succeeded, string.Join(" | ", result.Problems));
        Assert.Equal(1.0, result.Geometry!.HoleDiameterCm, 12);
        Assert.Equal(1.6, result.Geometry.CountersinkDiameterCm!.Value, 12);
        Assert.Equal(90.0, result.Geometry.CountersinkAngleDeg!.Value, 12);
    }

    [Fact]
    public void Countersink_WithNoRow_AsksForItsValues()
    {
        var result = Build(BigCountersink() with { CsinkDiameter = null, CsinkAngleDeg = null });

        Assert.Equal(
            [HoleField.CountersinkDiameter, HoleField.CountersinkAngle],
            result.Problems.Select(p => p.Field).ToList());
    }

    [Fact]
    public void ANamedFastener_WithNoRowOfTheSize_IsDropped()
    {
        var result = Build(T7Counterbore() with { Fastener = "TCAP" });

        Assert.True(result.Succeeded, string.Join(" | ", result.Problems));
        Assert.Null(result.Resolved.Fastener);
    }

    [Fact]
    public void ASizeNotInTheClearanceTab_IsAProblemUnderSize()
    {
        var result = Build(T7Counterbore() with { Size = "TX" });

        var problem = Assert.Single(result.Problems);
        Assert.Equal(HoleField.Size, problem.Field);
        Assert.Equal("TX is not in the Clearance tab for Metric holes.", problem.Message);
    }

    [Fact]
    public void ASeatNotWiderThanTheHole_IsAProblemUnderItsBox()
    {
        var result = Build(T7Counterbore(7m));

        var problem = Assert.Single(result.Problems);
        Assert.Equal(HoleField.CounterboreDiameter, problem.Field);
        Assert.Equal("The hole (7.4 mm) is not smaller than its counterbore (7 mm).", problem.Message);
    }

    [Fact]
    public void ASizeWithARow_StillTakesTheRowsHoleFit_NotTheChosenFit()
    {
        var spec = HoleTestWorkbook.Clearance() with
        {
            Seat = HoleSeat.Counterbore,
            Fastener = "TLOW",
            ClearanceFit = ClearanceFit.Loose,
        };

        var result = Build(spec);

        Assert.True(result.Succeeded, string.Join(" | ", result.Problems));
        Assert.Equal(0.53, result.Geometry!.HoleDiameterCm, 12);
        Assert.Equal("TLOW", result.Resolved.Fastener);
        Assert.Equal(ClearanceFit.Normal, result.Resolved.Normalised().ClearanceFit);
    }

    [Fact]
    public void SeatRowStart_ForASizeWithNoRow_IsEmpty()
    {
        var start = HoleRules.SeatRowStart(Workbook, HoleUnits.Metric, HoleKind.Clearance, "T7", null);

        Assert.True(start.IsEmpty);
    }

    // ---- The spec ----

    [Fact]
    public void Normalised_KeepsTheFit_OfASeatWithNoFastener_AndDropsItWithOne()
    {
        var noRow = T7Counterbore() with { ClearanceFit = ClearanceFit.Close };

        Assert.True(noRow.UsesClearanceFit);
        Assert.Equal(ClearanceFit.Close, noRow.Normalised().ClearanceFit);
        Assert.False((noRow with { Fastener = "TCAP" }).UsesClearanceFit);
        Assert.Equal(ClearanceFit.Normal, (noRow with { Fastener = "TCAP" }).Normalised().ClearanceFit);
    }

    // ---- Stored data ----

    [Fact]
    public void StoredData_RoundTrips_WithTheFitAndNoFastener()
    {
        var built = Build(T7Counterbore() with { ClearanceFit = ClearanceFit.Close, CboreDepthExpression = "d0 / 5" });
        Assert.True(built.Succeeded, string.Join(" | ", built.Problems));
        var stored = new HoleStoredData(built.Resolved, HoleAppliedState.From(built.Geometry!));

        var attributes = stored.ToAttributes();
        var read = HoleStoredData.Read(attributes);

        Assert.DoesNotContain(attributes, a => a.Name == HoleStoredData.FastenerName);
        Assert.Contains(attributes, a => a.Name == HoleStoredData.ClearanceFitName && Equals(a.Value, "Close"));
        Assert.True(read.Succeeded, read.Message);
        Assert.Equal(stored.Spec.Normalised(), read.Data!.Spec);
        Assert.Equal(6, HoleStoredData.CurrentVersion);

        var rebuilt = HoleRules.Build(read.Data.Spec, Workbook, 1);
        Assert.Equal(HoleAppliedState.From(rebuilt.Geometry!), stored.Applied);
    }

    [Fact]
    public void StoredData_ACountersinkWithNoRow_RoundTrips()
    {
        var built = Build(BigCountersink());
        var stored = new HoleStoredData(built.Resolved, HoleAppliedState.From(built.Geometry!));

        var read = HoleStoredData.Read(stored.ToAttributes());

        Assert.True(read.Succeeded, read.Message);
        Assert.Equal(ClearanceFit.Close, read.Data!.Spec.ClearanceFit);
        Assert.Null(read.Data.Spec.Fastener);
        Assert.Equal((16m, 90m), (read.Data.Spec.CsinkDiameter, read.Data.Spec.CsinkAngleDeg));
    }

    [Fact]
    public void StoredData_ASeatWithNoFastener_NeedsItsFit()
    {
        var built = Build(T7Counterbore());
        var attributes = new HoleStoredData(built.Resolved, HoleAppliedState.From(built.Geometry!)).ToAttributes()
            .Where(a => a.Name != HoleStoredData.ClearanceFitName);

        var read = HoleStoredData.Read(attributes);

        Assert.False(read.Succeeded);
        Assert.Contains(HoleStoredData.ClearanceFitName, read.Message);
    }

    [Fact]
    public void StoredData_Version1_StillNeedsTheFastener()
    {
        var built = Build(T7Counterbore());
        var attributes = new HoleStoredData(built.Resolved, HoleAppliedState.From(built.Geometry!)).ToAttributes()
            .Select(a => a.Name == HoleStoredData.VersionName ? new HoleAttribute(a.Name, 1) : a);

        Assert.False(HoleStoredData.Read(attributes).Succeeded);
    }

    [Fact]
    public void ReadBack_FromStoredData_OpensOnItsOwnSeat()
    {
        var built = Build(T7Counterbore() with { ClearanceFit = ClearanceFit.Loose });
        var stored = HoleStoredData.Read(
            new HoleStoredData(built.Resolved, HoleAppliedState.From(built.Geometry!)).ToAttributes());

        var result = HoleReadBack.Read(stored, Facts(0.76), Workbook, HoleUnits.Metric);

        Assert.False(result.IsInventorHole);
        Assert.Equal("T7", result.Spec.Size);
        Assert.Equal(ClearanceFit.Loose, result.Spec.ClearanceFit);
        Assert.Equal((12m, 4m), (result.Spec.CboreDiameter, result.Spec.CboreDepth));
    }

    // ---- Read back from an Inventor hole ----

    [Fact]
    public void ReadBack_AnInventorCounterbore_OnASizeWithNoRow_OpensAsThatSize_WithItsOwnSeat()
    {
        // 7.6 mm is T7 at Loose; no Counterbore row has a 12 mm counterbore.
        var facts = Facts(0.76) with { Seat = HoleSeat.Counterbore, CounterboreDiameterCm = 1.2, CounterboreDepthCm = 0.4 };

        var result = HoleReadBack.Read(null, facts, Workbook, HoleUnits.Metric);

        Assert.True(result.IsInventorHole);
        Assert.Equal(HoleKind.Clearance, result.Spec.Kind);
        Assert.Equal(HoleSeat.Counterbore, result.Spec.Seat);
        Assert.Equal("T7", result.Spec.Size);
        Assert.Null(result.Spec.Fastener);
        Assert.Equal(ClearanceFit.Loose, result.Spec.ClearanceFit);
        Assert.Equal((12m, 4m), (result.Spec.CboreDiameter, result.Spec.CboreDepth));
        Assert.Equal(
            "No Counterbore row has a 12 mm counterbore; the hole is read as T7, which has no Counterbore row, with " +
            "its own counterbore.",
            Assert.Single(result.Notes));

        // It builds the hole it was read from.
        var rebuilt = Build(result.Spec);
        Assert.True(rebuilt.Succeeded, string.Join(" | ", rebuilt.Problems));
        Assert.Equal(0.76, rebuilt.Geometry!.HoleDiameterCm, 12);
    }

    [Fact]
    public void ReadBack_AnInventorCountersink_OnASizeWithNoRow_OpensAsThatSize_WithItsOwnSeat()
    {
        // 14 mm is TBIG at Loose; TBIG has no Countersink row.
        var facts = Facts(1.4) with { Seat = HoleSeat.Countersink, CountersinkDiameterCm = 2.0, CountersinkAngleDeg = 90 };

        var result = HoleReadBack.Read(null, facts, Workbook, HoleUnits.Metric);

        Assert.Equal("TBIG", result.Spec.Size);
        Assert.Null(result.Spec.Fastener);
        Assert.Equal(ClearanceFit.Loose, result.Spec.ClearanceFit);
        Assert.Equal((20m, 90m), (result.Spec.CsinkDiameter, result.Spec.CsinkAngleDeg));
        Assert.True(Build(result.Spec).Succeeded);
    }

    [Fact]
    public void ReadBack_AnInventorCounterbore_OnASizeWithRows_PicksTheRowWhoseHoleFitIsTheHoles()
    {
        // 5.3 mm is T5 at Close: TLOW's Hole fit; no row has a 10 mm counterbore.
        var facts = Facts(0.53) with { Seat = HoleSeat.Counterbore, CounterboreDiameterCm = 1.0, CounterboreDepthCm = 0.3 };

        var result = HoleReadBack.Read(null, facts, Workbook, HoleUnits.Metric);

        Assert.Equal("T5", result.Spec.Size);
        Assert.Equal("TLOW", result.Spec.Fastener);
        Assert.Equal((10m, 3m), (result.Spec.CboreDiameter, result.Spec.CboreDepth));
        Assert.Equal(0.53, Build(result.Spec).Geometry!.HoleDiameterCm, 12);
    }
}
