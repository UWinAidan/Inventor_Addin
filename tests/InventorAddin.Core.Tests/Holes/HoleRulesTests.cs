using InventorAddin.Core.Holes;
using static InventorAddin.Core.Tests.Holes.HoleTestWorkbook;

namespace InventorAddin.Core.Tests.Holes;

public sealed class HoleRulesTests
{
    private static readonly HoleWorkbook Workbook = Create();

    private static HoleGeometry Built(HoleSpec spec)
    {
        var result = HoleRules.Build(spec, Workbook, 1);
        Assert.True(result.Succeeded, string.Join(" | ", result.Problems));
        Assert.Empty(result.Problems);
        return result.Geometry!;
    }

    private static HoleCheck OneProblem(HoleSpec spec, HoleField field, int centres = 1)
    {
        var result = HoleRules.Build(spec, Workbook, centres);
        Assert.False(result.Succeeded);
        Assert.Null(result.Geometry);
        var problem = Assert.Single(result.Problems);
        Assert.Equal(field, problem.Field);
        Assert.False(string.IsNullOrWhiteSpace(problem.Message));
        return problem;
    }

    // ---- Clearance ----

    [Theory]
    [InlineData(ClearanceFit.Close, 0.53)]
    [InlineData(ClearanceFit.Normal, 0.55)]
    [InlineData(ClearanceFit.Loose, 0.58)]
    public void Clearance_UsesTheFitColumn_InCentimetres(ClearanceFit fit, double diameterCm)
    {
        var geometry = Built(Clearance() with { ClearanceFit = fit });

        Assert.Equal(HoleKind.Clearance, geometry.Kind);
        Assert.Equal(HoleSeat.None, geometry.Seat);
        Assert.Equal(diameterCm, geometry.HoleDiameterCm, 12);
        Assert.Null(geometry.DepthCm);
        Assert.Null(geometry.DrillPoint);
        Assert.Null(geometry.Thread);
        Assert.Null(geometry.Tolerance);
        Assert.Null(geometry.FitColour);
        Assert.False(geometry.IsColoured);
    }

    [Fact]
    public void Imperial_ConvertsInchesToCentimetres()
    {
        var geometry = Built(HoleSpec.ClearanceHole(HoleUnits.Imperial, "#7"));

        Assert.Equal(0.17 * 2.54, geometry.HoleDiameterCm, 12);
    }

    [Fact]
    public void SizeMatch_IgnoresCaseAndSpaces_AndFindsHiddenRows()
    {
        Assert.Equal(0.55, Built(Clearance(" t5 ")).HoleDiameterCm, 12);
        Assert.Equal(0.98, Built(Clearance("T9")).HoleDiameterCm, 12);
    }

    [Fact]
    public void Clearance_Blind_TakesDepthAndDefaultDrillPoint()
    {
        var result = HoleRules.Build(
            Clearance() with { Termination = HoleTermination.Distance, Depth = 12m }, Workbook, 3);

        var geometry = result.Geometry!;
        Assert.Equal(1.2, geometry.DepthCm!.Value, 12);
        Assert.Equal(new HoleDrillPoint("90 deg", 90), geometry.DrillPoint);
        Assert.Equal("90 deg", result.Resolved.DrillPoint);
    }

    [Fact]
    public void FlatDrillPoint_HasNoAngle()
    {
        var geometry = Built(Clearance() with
        {
            Termination = HoleTermination.Distance,
            Depth = 12m,
            DrillPoint = "flat",
        });

        Assert.True(geometry.DrillPoint!.IsFlat);
        Assert.Null(geometry.DrillPoint.AngleDeg);
        Assert.Equal("Flat", geometry.DrillPoint.Name);
    }

    [Fact]
    public void FlipAndTermination_ArePassedThrough()
    {
        var geometry = Built(Clearance() with { Termination = HoleTermination.ToFace, Flip = true });

        Assert.Equal(HoleTermination.ToFace, geometry.Termination);
        Assert.True(geometry.Flip);
        Assert.Null(geometry.DepthCm);
    }

    // ---- Seats ----

    [Fact]
    public void Counterbore_TakesThroughHoleFromClearanceAtTheRowsHoleFit()
    {
        var result = HoleRules.Build(Clearance() with { Seat = HoleSeat.Counterbore, Fastener = "TCAP" }, Workbook, 1);

        var geometry = result.Geometry!;
        Assert.Equal(HoleSeat.Counterbore, geometry.Seat);
        Assert.Equal(0.55, geometry.HoleDiameterCm, 12);      // T5 Normal
        Assert.Equal(0.95, geometry.CounterboreDiameterCm!.Value, 12);
        Assert.Equal(0.52, geometry.CounterboreDepthCm!.Value, 12);
        Assert.Null(geometry.CountersinkDiameterCm);
        Assert.Equal("TCAP", result.Resolved.Fastener);
    }

    [Fact]
    public void Counterbore_FastenerPicksTheRow_AndTheSpecsClearanceFitIsIgnored()
    {
        var geometry = Built(Clearance() with
        {
            Seat = HoleSeat.Counterbore,
            Fastener = "tlow",
            ClearanceFit = ClearanceFit.Loose,
        });

        Assert.Equal(0.53, geometry.HoleDiameterCm, 12);      // T5 Close, from the TLOW row
        Assert.Equal(0.85, geometry.CounterboreDiameterCm!.Value, 12);
    }

    [Fact]
    public void Counterbore_NoFastener_TakesTheFirstRowOfTheSize()
    {
        var result = HoleRules.Build(Clearance() with { Seat = HoleSeat.Counterbore }, Workbook, 1);

        Assert.Equal("TCAP", result.Resolved.Fastener);
    }

    [Fact]
    public void Countersink_TakesThroughHoleDiameterAndAngle()
    {
        var geometry = Built(Clearance() with { Seat = HoleSeat.Countersink, Fastener = "TFLAT" });

        Assert.Equal(0.58, geometry.HoleDiameterCm, 12);      // T5 Loose
        Assert.Equal(1.04, geometry.CountersinkDiameterCm!.Value, 12);
        Assert.Equal(90, geometry.CountersinkAngleDeg!.Value, 12);
        Assert.Null(geometry.CounterboreDiameterCm);
    }

    [Fact]
    public void ImperialCountersink()
    {
        var geometry = Built(HoleSpec.ClearanceHole(HoleUnits.Imperial, "#7") with { Seat = HoleSeat.Countersink });

        Assert.Equal(0.17 * 2.54, geometry.HoleDiameterCm, 12);
        Assert.Equal(0.3 * 2.54, geometry.CountersinkDiameterCm!.Value, 12);
        Assert.Equal(82, geometry.CountersinkAngleDeg!.Value, 12);
    }

    [Fact]
    public void SeatSizeWithoutAClearanceRow_IsAProblem()
    {
        var problem = OneProblem(Clearance("TNONE") with { Seat = HoleSeat.Counterbore }, HoleField.Size);

        Assert.Contains("Clearance tab", problem.Message);
        Assert.Contains("TNONE", problem.Message);
    }

    [Fact]
    public void UnknownFastener_IsAProblem()
    {
        var problem = OneProblem(Clearance() with { Seat = HoleSeat.Counterbore, Fastener = "TNUT" }, HoleField.Fastener);

        Assert.Contains("TNUT", problem.Message);
    }

    [Fact]
    public void HoleNotSmallerThanCounterbore_IsAProblem()
    {
        // TBIG at Loose is 14 mm; its counterbore is 13 mm.
        var problem = OneProblem(Clearance("TBIG") with { Seat = HoleSeat.Counterbore }, HoleField.Size);

        Assert.Contains("counterbore", problem.Message);
    }

    [Fact]
    public void CounterboreDepthNotLessThanHoleDepth_IsAProblem()
    {
        var spec = Clearance() with
        {
            Seat = HoleSeat.Counterbore,
            Fastener = "TCAP",
            Termination = HoleTermination.Distance,
            Depth = 5.2m,
        };

        var problem = OneProblem(spec, HoleField.Depth);
        Assert.Contains("counterbore depth", problem.Message);

        Assert.Equal(0.53, Built(spec with { Depth = 5.3m }).DepthCm!.Value, 12);
    }

    [Fact]
    public void CounterboreDepth_IsNotCheckedForThroughHoles()
    {
        Built(Clearance() with { Seat = HoleSeat.Counterbore, Termination = HoleTermination.ThroughAll });
    }

    [Fact]
    public void HoleNotSmallerThanCountersink_IsAProblem()
    {
        var workbook = Workbook with
        {
            Countersink = [new CountersinkRow(5, HoleUnits.Metric, "T5", "TFLAT", 5.8m, 90m, ClearanceFit.Loose, false)],
        };

        var result = HoleRules.Build(Clearance() with { Seat = HoleSeat.Countersink }, workbook, 1);

        Assert.Equal(HoleField.Size, Assert.Single(result.Problems).Field);
        Assert.Contains("countersink", result.Problems[0].Message);
    }

    [Fact]
    public void CountersinkDepthNotLessThanHoleDepth_IsAProblem()
    {
        // T5 countersink: 10.4 mm at 90 deg over a 5.8 mm hole is (10.4 - 5.8) / 2 / tan(45 deg) = 2.3 mm deep.
        var spec = Clearance() with
        {
            Seat = HoleSeat.Countersink,
            Termination = HoleTermination.Distance,
            Depth = 2.3m,
        };

        var problem = OneProblem(spec, HoleField.Depth);
        Assert.Equal("The countersink depth (2.3 mm) is not less than the hole depth (2.3 mm).", problem.Message);
        OneProblem(spec with { Depth = 2m }, HoleField.Depth);

        Assert.Equal(0.231, Built(spec with { Depth = 2.31m }).DepthCm!.Value, 12);
    }

    [Fact]
    public void CountersinkDepth_UsesTheAngle()
    {
        // Imperial #7: 0.3 in at 82 deg over a 0.17 in hole is 0.065 / tan(41 deg) = 0.0748 in deep.
        var spec = HoleSpec.ClearanceHole(HoleUnits.Imperial, "#7") with
        {
            Seat = HoleSeat.Countersink,
            Termination = HoleTermination.Distance,
        };

        OneProblem(spec with { Depth = 0.074m }, HoleField.Depth);
        Built(spec with { Depth = 0.076m });
    }

    [Fact]
    public void CountersinkDepth_IsNotCheckedForThroughHoles()
    {
        Built(Clearance() with { Seat = HoleSeat.Countersink, Termination = HoleTermination.ThroughAll });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(180)]
    public void CountersinkAngleOutOfRange_IsAProblem(int angle)
    {
        var workbook = Workbook with
        {
            Countersink = [new CountersinkRow(5, HoleUnits.Metric, "T5", "TFLAT", 10m, angle, ClearanceFit.Loose, false)],
        };

        var result = HoleRules.Build(Clearance() with { Seat = HoleSeat.Countersink }, workbook, 1);

        Assert.Contains("angle", Assert.Single(result.Problems).Message);
    }

    // ---- Tapped ----

    [Fact]
    public void Tapped_UsesTheTappedRow_TapDrillInTableUnits()
    {
        var result = HoleRules.Build(Tapped(), Workbook, 1);

        var geometry = result.Geometry!;
        Assert.Equal(HoleKind.Tapped, geometry.Kind);
        Assert.Equal(0.42, geometry.HoleDiameterCm, 12);
        Assert.Equal(1.0, geometry.DepthCm!.Value, 12);             // default drill depth 10 mm
        Assert.Equal(new HoleDrillPoint("90 deg", 90), geometry.DrillPoint);

        var thread = geometry.Thread!;
        Assert.Equal("M5x0.8", thread.Designation);
        Assert.Equal(TappedRow.MetricThreadType, thread.ThreadType);
        Assert.Equal("6H", thread.Class);
        Assert.Equal(4.2, thread.TapDrillInTableUnits, 12);         // mm, not cm
        Assert.Equal(HoleUnits.Metric, thread.TableUnits);
        Assert.False(thread.FullDepth);
        Assert.Equal(0.75, thread.ThreadDepthCm, 12);

        Assert.Equal(7.5m, result.Resolved.ThreadDepth);
        Assert.Equal(10m, result.Resolved.DrillDepth);
    }

    [Fact]
    public void ImperialTapped_TapDrillInInches_DesignationWithoutHash()
    {
        var geometry = Built(Tapped("#7-36 UNF") with { Units = HoleUnits.Imperial });

        var thread = geometry.Thread!;
        Assert.Equal("7-36 UNF", thread.Designation);
        Assert.Equal(TappedRow.ImperialThreadType, thread.ThreadType);
        Assert.Equal(0.12, thread.TapDrillInTableUnits, 12);        // inches
        Assert.Equal(0.12 * 2.54, geometry.HoleDiameterCm, 12);
        Assert.Equal(0.3 * 2.54, geometry.DepthCm!.Value, 12);
        Assert.Equal(0.225 * 2.54, thread.ThreadDepthCm, 12);
    }

    [Fact]
    public void Tapped_EditedDepthsWin()
    {
        var geometry = Built(Tapped() with { ThreadDepth = 6m, DrillDepth = 8m });

        Assert.Equal(0.8, geometry.DepthCm!.Value, 12);
        Assert.Equal(0.6, geometry.Thread!.ThreadDepthCm, 12);
    }

    [Fact]
    public void Tapped_ThroughAll_IsFullDepth()
    {
        var geometry = Built(Tapped() with { Termination = HoleTermination.ThroughAll });

        Assert.True(geometry.Thread!.FullDepth);
        Assert.Null(geometry.DepthCm);
        Assert.Null(geometry.DrillPoint);
    }

    [Fact]
    public void ThreadDeeperThanDrill_IsAProblem()
    {
        var problem = OneProblem(Tapped() with { ThreadDepth = 10.5m }, HoleField.ThreadDepth);

        Assert.Contains("drill depth", problem.Message);
        Built(Tapped() with { ThreadDepth = 10m });                  // equal is allowed
    }

    [Fact]
    public void ThreadDeeperThanDrill_IsNotCheckedThroughAll()
    {
        Built(Tapped() with { ThreadDepth = 20m, Termination = HoleTermination.ThroughAll });
    }

    [Fact]
    public void ZeroDrillDepth_IsAProblem()
    {
        var result = HoleRules.Build(Tapped() with { ThreadDepth = 0m, DrillDepth = 0m }, Workbook, 1);

        Assert.Contains(result.Problems, p => p.Field == HoleField.DrillDepth);
        Assert.Contains(result.Problems, p => p.Field == HoleField.ThreadDepth);
    }

    // ---- Seats on tapped and dowel holes (task 099) ----

    [Fact]
    public void SameSizeSeatRows_AreTheRowsWhoseSizeHasAClearanceRowOfTheNominal()
    {
        Assert.Equal(["TCAP", "TLOW"], HoleRules.SameSizeCounterbores(Workbook, HoleUnits.Metric, 5m).Select(r => r.Screw));
        Assert.Equal(["TFLAT"], HoleRules.SameSizeCountersinks(Workbook, HoleUnits.Metric, 5m).Select(r => r.Screw));
        Assert.Equal(["#7"], HoleRules.SameSizeCounterbores(Workbook, HoleUnits.Imperial, 0.15m).Select(r => r.Size));
        Assert.Empty(HoleRules.SameSizeCounterbores(Workbook, HoleUnits.Imperial, 5m));
        Assert.Empty(HoleRules.SameSizeCounterbores(Workbook, HoleUnits.Metric, 7m));

        // TNONE has no Clearance row, so it is never the same size.
        Assert.DoesNotContain(
            HoleRules.SameSizeCounterbores(Workbook, HoleUnits.Metric, 6m), r => r.Size == "TNONE");
    }

    [Fact]
    public void SameSize_MatchesTheNominalWithinTheTolerance_AndCountsHiddenClearanceRows()
    {
        Assert.True(HoleRules.IsSameSize(Workbook, HoleUnits.Metric, " t5 ", 5.00005m));
        Assert.False(HoleRules.IsSameSize(Workbook, HoleUnits.Metric, "T5", 5.001m));
        Assert.True(HoleRules.IsSameSize(Workbook, HoleUnits.Metric, "T9", 9m));
    }

    [Fact]
    public void SeatNominal_IsTheThreadsOrTheDowelsNominal()
    {
        Assert.Equal(5m, HoleRules.SeatNominal(Workbook, HoleUnits.Metric, HoleKind.Tapped, " m5 "));
        Assert.Equal(7m, HoleRules.SeatNominal(Workbook, HoleUnits.Metric, HoleKind.Dowel, "7 mm"));
        Assert.Null(HoleRules.SeatNominal(Workbook, HoleUnits.Metric, HoleKind.Clearance, "T5"));
        Assert.Null(HoleRules.SeatNominal(Workbook, HoleUnits.Metric, HoleKind.Tapped, "M99"));
        Assert.Null(HoleRules.SeatNominal(Workbook, HoleUnits.Metric, HoleKind.Dowel, null));
    }

    [Fact]
    public void Tapped_WithACounterbore_TakesTheFirstRowOfTheSameSize_AndKeepsTheThread()
    {
        var result = HoleRules.Build(Tapped() with { Seat = HoleSeat.Counterbore }, Workbook, 1);
        Assert.True(result.Succeeded, string.Join(" | ", result.Problems));

        var geometry = result.Geometry!;
        Assert.Equal(HoleKind.Tapped, geometry.Kind);
        Assert.Equal(HoleSeat.Counterbore, geometry.Seat);
        Assert.Equal(0.95, geometry.CounterboreDiameterCm!.Value, 12);
        Assert.Equal(0.52, geometry.CounterboreDepthCm!.Value, 12);
        Assert.Equal(0.42, geometry.HoleDiameterCm, 12);           // the tap drill, as without a seat
        Assert.Equal("M5x0.8", geometry.Thread!.Designation);
        Assert.False(geometry.Thread.FullDepth);
        Assert.Equal(1.0, geometry.DepthCm!.Value, 12);
        Assert.Null(geometry.FitColour);
        Assert.Equal("TCAP", result.Resolved.Fastener);
    }

    [Fact]
    public void Tapped_FastenerPicksTheRowOfTheSameSize()
    {
        var geometry = Built(Tapped() with { Seat = HoleSeat.Counterbore, Fastener = " tlow " });

        Assert.Equal(0.85, geometry.CounterboreDiameterCm!.Value, 12);
        Assert.Equal(0.31, geometry.CounterboreDepthCm!.Value, 12);
    }

    [Fact]
    public void Tapped_WithACountersink_ThroughAll_IsFullDepth()
    {
        var geometry = Built(Tapped() with { Seat = HoleSeat.Countersink, Termination = HoleTermination.ThroughAll });

        Assert.Equal(HoleSeat.Countersink, geometry.Seat);
        Assert.Equal(1.04, geometry.CountersinkDiameterCm!.Value, 12);
        Assert.Equal(90.0, geometry.CountersinkAngleDeg);
        Assert.True(geometry.Thread!.FullDepth);
        Assert.Null(geometry.DepthCm);
    }

    [Fact]
    public void ImperialTapped_UsesTheRowsOfTheSameNumberSize()
    {
        var geometry = Built(Tapped("#7-36 UNF") with { Units = HoleUnits.Imperial, Seat = HoleSeat.Counterbore });

        Assert.Equal(0.28 * 2.54, geometry.CounterboreDiameterCm!.Value, 12);
        Assert.Equal(0.16 * 2.54, geometry.CounterboreDepthCm!.Value, 12);
        Assert.Equal("7-36 UNF", geometry.Thread!.Designation);
    }

    [Theory]
    [InlineData(HoleKind.Tapped, "M7")]
    [InlineData(HoleKind.Dowel, "7 mm")]
    public void ASizeWithNoSeatRowOfTheSameSize_SaysWhyTheSeatCannotBeKept(HoleKind kind, string size)
    {
        var workbook = Workbook with
        {
            Tapped = [.. Workbook.Tapped, new TappedRow(9, HoleUnits.Metric, "M7", 7m, 1m, 6m, "6H", 10.5m, 14m, false)],
        };
        var spec = (kind == HoleKind.Tapped ? Tapped(size) : Dowel(size)) with { Seat = HoleSeat.Countersink };

        var result = HoleRules.Build(spec, workbook, 1);

        Assert.False(result.Succeeded);
        var problem = Assert.Single(result.Problems);
        Assert.Equal(HoleField.Seat, problem.Field);
        Assert.Contains(size, problem.Message);
        Assert.Contains("Countersink", problem.Message);
        Assert.Contains("7 mm", problem.Message);
        Assert.Contains("Set Seat to None", problem.Message);
    }

    [Fact]
    public void Tapped_UnknownFastenerOfTheSameSize_IsAProblem()
    {
        var problem = OneProblem(Tapped() with { Seat = HoleSeat.Counterbore, Fastener = "TNOPE" }, HoleField.Fastener);

        Assert.Contains("TNOPE", problem.Message);
        Assert.Contains("M5", problem.Message);
    }

    [Fact]
    public void Tapped_SeatNotWiderThanTheThreadsNominal_IsAProblem_EvenWhenWiderThanTheTapDrill()
    {
        // 4.8 mm is wider than the 4.2 mm tap drill, but not than the 5 mm thread.
        var workbook = Workbook with
        {
            Counterbore = [new CounterboreRow(5, HoleUnits.Metric, "T5", "TCAP", 4.8m, 2m, ClearanceFit.Normal, false)],
        };

        var result = HoleRules.Build(Tapped() with { Seat = HoleSeat.Counterbore }, workbook, 1);

        var problem = Assert.Single(result.Problems);
        Assert.Equal(HoleField.Size, problem.Field);
        Assert.Equal("The thread M5 (5 mm) is not smaller than its counterbore (4.8 mm).", problem.Message);
    }

    [Fact]
    public void Tapped_CounterboreNotShallowerThanTheDrillDepth_IsAProblem()
    {
        var problem = OneProblem(
            Tapped() with { Seat = HoleSeat.Counterbore, ThreadDepth = 4m, DrillDepth = 5m }, HoleField.Depth);

        Assert.Contains("5.2 mm", problem.Message);
        Assert.Contains("5 mm", problem.Message);
    }

    [Fact]
    public void Tapped_CountersinkDepth_IsFromTheTapDrill()
    {
        // (10.4 - 4.2) / 2 / tan 45 = 3.1 mm of cone.
        Built(Tapped() with { Seat = HoleSeat.Countersink, ThreadDepth = 3m, DrillDepth = 3.2m });
        OneProblem(Tapped() with { Seat = HoleSeat.Countersink, ThreadDepth = 3m, DrillDepth = 3.1m }, HoleField.Depth);
    }

    // ---- Dowel ----

    [Fact]
    public void Dowel_NominalWithDefaultIsoTolerance_AndFitColour()
    {
        var result = HoleRules.Build(Dowel(), Workbook, 1);

        var geometry = result.Geometry!;
        Assert.Equal(HoleKind.Dowel, geometry.Kind);
        Assert.Equal(0.7, geometry.HoleDiameterCm, 12);
        Assert.Equal(HoleSettings.Starter.NormalFitColour, geometry.FitColour);
        Assert.True(geometry.IsColoured);

        var tolerance = geometry.Tolerance!;
        Assert.Equal("Test location", tolerance.Name);              // the Default = Y row
        Assert.Equal("G7", tolerance.IsoCode);
        Assert.True(tolerance.IsIso);
        Assert.Equal(0.0020, tolerance.UpperCm, 9);                 // G7 at 7 mm: +0.020 / +0.005 mm
        Assert.Equal(0.0005, tolerance.LowerCm, 9);
        Assert.Equal("Test location", result.Resolved.Tolerance);
    }

    [Fact]
    public void Dowel_NamedTolerance()
    {
        var geometry = Built(Dowel() with { Tolerance = "test bore" });

        Assert.Equal("H7", geometry.Tolerance!.IsoCode);
        Assert.Equal(0.0015, geometry.Tolerance.UpperCm, 9);        // H7 at 7 mm: +0.015 / 0
        Assert.Equal(0, geometry.Tolerance.LowerCm, 9);
    }

    [Fact]
    public void Dowel_AtAStepLimit_StaysInItsStep()
    {
        // 18 mm is the top of the 10 to 18 mm step: H7 is +0.018.
        var geometry = Built(Dowel("18 mm") with { Tolerance = "Test bore" });

        Assert.Equal(0.0018, geometry.Tolerance!.UpperCm, 9);
    }

    [Fact]
    public void IsoNominal_RoundsAwayConversionNoise()
    {
        Assert.Equal(18, HoleRules.IsoNominalMm(18.000000000000004m, HoleUnits.Metric));
        Assert.Equal(12.7, HoleRules.IsoNominalMm(0.5m, HoleUnits.Imperial));
    }

    [Fact]
    public void Dowel_LimitsRow_InCentimetres_PressColour()
    {
        var geometry = Built(Dowel() with { PrecisionFit = PrecisionFit.Press });

        var tolerance = geometry.Tolerance!;
        Assert.Null(tolerance.IsoCode);
        Assert.False(tolerance.IsIso);
        Assert.Equal(0, tolerance.UpperCm, 12);
        Assert.Equal(-0.002, tolerance.LowerCm, 12);
        Assert.Equal(HoleSettings.Starter.PressFitColour, geometry.FitColour);
    }

    [Fact]
    public void ImperialDowel_LimitsInInches()
    {
        var geometry = Built(Dowel("1/7\"") with { Units = HoleUnits.Imperial, PrecisionFit = PrecisionFit.Slip });

        Assert.Equal(0.143 * 2.54, geometry.HoleDiameterCm, 12);
        Assert.Equal(0.0005 * 2.54, geometry.Tolerance!.UpperCm, 12);
        Assert.Equal(HoleSettings.Starter.SlipFitColour, geometry.FitColour);
    }

    [Fact]
    public void Dowel_WithoutAToleranceRow_IsAProblem()
    {
        var problem = OneProblem(Dowel() with { PrecisionFit = PrecisionFit.Slip }, HoleField.Tolerance);
        Assert.Contains("Slip", problem.Message);

        OneProblem(Dowel() with { Tolerance = "No such row" }, HoleField.Tolerance);
    }

    [Fact]
    public void Dowel_SizeOutsideIso286_IsAProblem()
    {
        OneProblem(Dowel("900 mm"), HoleField.Tolerance);
    }

    [Fact]
    public void Dowel_WithACounterbore_KeepsItsNominalToleranceAndColour()
    {
        var result = HoleRules.Build(Dowel("5 mm") with { Seat = HoleSeat.Counterbore }, Workbook, 1);
        Assert.True(result.Succeeded, string.Join(" | ", result.Problems));

        var geometry = result.Geometry!;
        Assert.Equal(HoleKind.Dowel, geometry.Kind);
        Assert.Equal(HoleSeat.Counterbore, geometry.Seat);
        Assert.Equal(0.5, geometry.HoleDiameterCm, 12);
        Assert.Equal(0.95, geometry.CounterboreDiameterCm!.Value, 12);
        Assert.Equal("G7", geometry.Tolerance!.IsoCode);
        Assert.Equal(HoleSettings.Starter.NormalFitColour, geometry.FitColour);
        Assert.Equal("TCAP", result.Resolved.Fastener);
        Assert.Equal("Test location", result.Resolved.Tolerance);
    }

    [Fact]
    public void Dowel_WithACountersink()
    {
        var geometry = Built(Dowel("5 mm") with { Seat = HoleSeat.Countersink, PrecisionFit = PrecisionFit.Press });

        Assert.Equal(1.04, geometry.CountersinkDiameterCm!.Value, 12);
        Assert.Equal(HoleSettings.Starter.PressFitColour, geometry.FitColour);
        Assert.False(geometry.Tolerance!.IsIso);
    }

    [Fact]
    public void Dowel_BlindNotDeeperThanItsCounterbore_IsAProblem()
    {
        OneProblem(
            Dowel("5 mm") with { Seat = HoleSeat.Counterbore, Termination = HoleTermination.Distance, Depth = 5.2m },
            HoleField.Depth);
    }

    [Fact]
    public void Normalised_KeepsTheSeatAndFastenerOfATappedOrDowelHole()
    {
        var tapped = (Tapped() with { Seat = HoleSeat.Counterbore, Fastener = " TLOW " }).Normalised();
        var dowel = (Dowel() with { Seat = HoleSeat.Countersink, Fastener = "TFLAT" }).Normalised();
        var unseated = (Dowel() with { Fastener = "TFLAT" }).Normalised();

        Assert.Equal((HoleSeat.Counterbore, "TLOW"), (tapped.Seat, tapped.Fastener));
        Assert.Equal((HoleSeat.Countersink, "TFLAT"), (dowel.Seat, dowel.Fastener));
        Assert.Null(unseated.Fastener);
    }

    // ---- General checks ----

    [Fact]
    public void NoCentres_IsAProblem()
    {
        var problem = OneProblem(Clearance(), HoleField.Centres, centres: 0);

        Assert.Equal("Pick at least one hole centre.", problem.Message);
    }

    [Theory]
    [InlineData(HoleKind.Clearance, "T6")]
    [InlineData(HoleKind.Tapped, "M6")]
    [InlineData(HoleKind.Dowel, "6 mm")]
    public void SizeMissingFromTheWorkbook_IsAProblem(HoleKind kind, string size)
    {
        var spec = Clearance() with { Kind = kind, Size = size };

        var result = HoleRules.Build(spec, Workbook, 1);

        var problem = Assert.Single(result.Problems, p => p.Field == HoleField.Size);
        Assert.Contains(size, problem.Message);
        Assert.Contains("Metric", problem.Message);
    }

    [Fact]
    public void SizeOfTheOtherUnits_IsAProblem()
    {
        OneProblem(Clearance("#7"), HoleField.Size);
    }

    [Fact]
    public void BlankSize_IsAProblem()
    {
        OneProblem(Clearance(" "), HoleField.Size);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-1)]
    public void BlindWithoutAPositiveDepth_IsAProblem(int? depth)
    {
        OneProblem(Clearance() with { Termination = HoleTermination.Distance, Depth = depth }, HoleField.Depth);
    }

    [Fact]
    public void ZeroDiameterInTheWorkbook_IsAProblem()
    {
        var workbook = Workbook with
        {
            Clearance = [new ClearanceRow(5, HoleUnits.Metric, "T5", 5m, 0m, 0m, 0m, false)],
        };

        var result = HoleRules.Build(Clearance(), workbook, 1);

        Assert.Equal(HoleField.Size, Assert.Single(result.Problems).Field);
    }

    [Fact]
    public void UnknownDrillPoint_IsAProblem()
    {
        OneProblem(
            Clearance() with { Termination = HoleTermination.Distance, Depth = 5m, DrillPoint = "45 deg" },
            HoleField.DrillPoint);
    }

    [Fact]
    public void NoDrillPointRows_IsAProblemOnlyForBlindHoles()
    {
        var workbook = Workbook with { DrillPoints = [] };

        Assert.True(HoleRules.Build(Clearance(), workbook, 1).Succeeded);
        var result = HoleRules.Build(Clearance() with { Termination = HoleTermination.Distance, Depth = 5m }, workbook, 1);
        Assert.Equal(HoleField.DrillPoint, Assert.Single(result.Problems).Field);
    }

    [Fact]
    public void DefaultDrillPoint_SkipsHiddenRows()
    {
        var workbook = Workbook with
        {
            DrillPoints =
            [
                new DrillPointRow(5, "90 deg", 90m, true, true, ""),
                new DrillPointRow(6, "118 deg", 118m, false, false, ""),
            ],
        };

        Assert.Equal("118 deg", HoleRules.FindDrillPoint(workbook, null)!.Name);
        Assert.Equal("90 deg", HoleRules.FindDrillPoint(workbook, "90 DEG")!.Name);
    }

    [Fact]
    public void FindTolerance_WithoutADefault_TakesTheFirstRow()
    {
        var workbook = Workbook with
        {
            PrecisionTolerances =
            [
                new PrecisionToleranceRow(5, HoleUnits.Metric, PrecisionFit.Slip, "First", ToleranceKind.Iso, "H7", null, null, false, ""),
                new PrecisionToleranceRow(6, HoleUnits.Metric, PrecisionFit.Slip, "Second", ToleranceKind.Iso, "G7", null, null, false, ""),
            ],
        };

        Assert.Equal("First", HoleRules.FindTolerance(workbook, HoleUnits.Metric, PrecisionFit.Slip, null)!.Name);
        Assert.Null(HoleRules.FindTolerance(workbook, HoleUnits.Imperial, PrecisionFit.Slip, null));
    }

    [Fact]
    public void SeveralProblems_AreAllReported()
    {
        var result = HoleRules.Build(Clearance("T6") with { Termination = HoleTermination.Distance }, Workbook, 0);

        Assert.Equal(
            [HoleField.Centres, HoleField.Size, HoleField.Depth],
            result.Problems.Select(p => p.Field).ToArray());
    }

    // ---- Standards ----

    [Fact]
    public void Standards_MapOntoKinds()
    {
        Assert.Equal("Clearance", HoleStandards.TypeName(HoleKind.Dowel));
        Assert.Equal("Tapped", HoleStandards.TypeName(HoleKind.Tapped));
        Assert.Equal(
            [HoleStandards.ClearanceStandard, HoleStandards.DowelStandard],
            HoleStandards.StandardsFor(HoleStandards.ClearanceType));
        Assert.Equal([HoleStandards.TappedStandard], HoleStandards.StandardsFor("tapped"));

        foreach (var kind in Enum.GetValues<HoleKind>())
        {
            Assert.Equal(kind, HoleStandards.KindFor(HoleStandards.StandardName(kind)));
        }

        Assert.Null(HoleStandards.KindFor("ISO"));
        Assert.Null(HoleStandards.KindFor(null));
    }

    [Fact]
    public void ToCm()
    {
        Assert.Equal(0.6, HoleRules.ToCm(6m, HoleUnits.Metric), 12);
        Assert.Equal(0.635, HoleRules.ToCm(0.25m, HoleUnits.Imperial), 12);
    }
}
