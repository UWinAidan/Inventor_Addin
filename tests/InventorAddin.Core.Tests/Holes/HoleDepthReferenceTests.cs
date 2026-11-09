using InventorAddin.Core.Holes;

namespace InventorAddin.Core.Tests.Holes;

/// <summary>
/// Tasks 114 and 116: the depths of a hole as Inventor measures them (from the start face, or a spotface's bottom,
/// spike 108) against the window's (a threaded hole's below the seat, any other hole's from the top face), the
/// read-back of holes made by Inventor and of holes stored before task 116, and the expression Inventor reports for a
/// Simple diameter. Sizes and values are invented.
/// </summary>
public sealed class HoleDepthReferenceTests
{
    private static readonly HoleWorkbook Workbook = HoleTestWorkbook.Create() with
    {
        Spotface = [new SpotfaceRow(5, HoleUnits.Metric, "T5", 11m, 0.5m, false)],
        TaperTapped =
        [
            new TaperTappedRow(5, HoleUnits.Metric, "Rc 1/4", "BSP Taper Internal", 11.2m, 16.8m, 22.4m, false),
            new TaperTappedRow(6, HoleUnits.Imperial, "1/4", "NPT", 0.44m, 0.66m, 0.88m, false),
        ],
    };

    // The M5 tap drill (4.2 mm), as the window models it.
    private const double M5TapDrillCm = 0.42;

    private static HoleBuildResult Build(HoleSpec spec) => HoleRules.Build(spec, Workbook, 1);

    private static HoleGeometry Geometry(HoleSpec spec)
    {
        var result = Build(spec);
        Assert.True(result.Succeeded, string.Join(" | ", result.Problems));
        return result.Geometry!;
    }

    private static HoleSpec BlindClearance(HoleSeat seat) => HoleTestWorkbook.Clearance() with
    {
        Seat = seat,
        Termination = HoleTermination.Distance,
        Depth = 12m,
        DrillPoint = "118 deg",
        CboreDiameter = seat == HoleSeat.Counterbore ? 11m : null,
        CboreDepth = seat == HoleSeat.Counterbore ? 2m : null,
        SpotfaceDiameter = seat == HoleSeat.Spotface ? 11m : null,
        SpotfaceDepth = seat == HoleSeat.Spotface ? 2m : null,
    };

    /// <summary>An M5 blind tapped hole, thread 9 and drill 12 below the seat; the seat given its own sizes.</summary>
    private static HoleSpec SeatedTapped(HoleSeat seat, decimal seatDepth = 6m) => HoleTestWorkbook.Tapped() with
    {
        Seat = seat,
        DrillPoint = "118 deg",
        ThreadDepth = 9m,
        DrillDepth = 12m,
        CboreDiameter = seat == HoleSeat.Counterbore ? 9.5m : null,
        CboreDepth = seat == HoleSeat.Counterbore ? seatDepth : null,
        CsinkDiameter = seat == HoleSeat.Countersink ? 10.4m : null,
        CsinkAngleDeg = seat == HoleSeat.Countersink ? 90m : null,
        SpotfaceDiameter = seat == HoleSeat.Spotface ? 11m : null,
        SpotfaceDepth = seat == HoleSeat.Spotface ? seatDepth : null,
    };

    private static InventorHoleFacts FactsOf(HoleGeometry g) => new(
        g.Seat,
        g.Thread is not null,
        g.Thread?.Designation,
        g.Thread?.ThreadType,
        g.Thread is { FullDepth: false, IsTapered: false } thread ? thread.ThreadDepthCm : null,
        // Inventor does not report a tapped hole's diameter (task 099).
        g.Thread is null ? g.HoleDiameterCm : null,
        g.Termination,
        g.DepthCm,
        g.CounterboreDiameterCm,
        g.CounterboreDepthCm,
        g.CountersinkDiameterCm,
        g.CountersinkAngleDeg,
        g.DrillPoint?.IsFlat ?? false,
        g.DrillPoint?.AngleDeg,
        g.Tolerance?.IsoCode,
        g.Tolerance?.UpperCm,
        g.Tolerance?.LowerCm,
        g.Flip)
    {
        TaperedThread = g.Thread?.IsTapered ?? false,
        SpotfaceDiameterCm = g.SpotfaceDiameterCm,
        SpotfaceDepthCm = g.SpotfaceDepthCm,
        DepthExpression = g.DepthExpression,
        ThreadDepthExpression = g.Thread?.ThreadDepthExpression,
        CounterboreDepthExpression = g.CounterboreDepthExpression,
        SpotfaceDepthExpression = g.SpotfaceDepthExpression,
    };

    private static HoleStoredData Stored(HoleBuildResult build) =>
        new(build.Resolved, HoleAppliedState.From(build.Geometry!));

    // ---- the rule ----

    [Theory]
    [InlineData(true, HoleSeat.None, 0)]
    [InlineData(true, HoleSeat.Counterbore, 1)]
    [InlineData(true, HoleSeat.Countersink, 1)]
    [InlineData(true, HoleSeat.Spotface, 0)]
    [InlineData(false, HoleSeat.None, 0)]
    [InlineData(false, HoleSeat.Counterbore, 0)]
    [InlineData(false, HoleSeat.Countersink, 0)]
    [InlineData(false, HoleSeat.Spotface, -1)]
    public void Direction_AddsTheSeatOnAThreadedHole_AndTakesASpotfaceOffAnyOther(bool threaded, HoleSeat seat, int expected)
    {
        Assert.Equal(expected, HoleDepthReference.Direction(threaded, seat));
    }

    [Theory]
    [InlineData(HoleKind.Tapped, HoleSeat.None, 1.2)]
    [InlineData(HoleKind.Tapped, HoleSeat.Counterbore, 1.4)]
    [InlineData(HoleKind.Tapped, HoleSeat.Countersink, 1.4)]
    [InlineData(HoleKind.Tapped, HoleSeat.Spotface, 1.2)]
    [InlineData(HoleKind.TaperTapped, HoleSeat.Countersink, 1.4)]
    [InlineData(HoleKind.Clearance, HoleSeat.Counterbore, 1.2)]
    [InlineData(HoleKind.Clearance, HoleSeat.Countersink, 1.2)]
    [InlineData(HoleKind.Clearance, HoleSeat.Spotface, 1.0)]
    [InlineData(HoleKind.Dowel, HoleSeat.Spotface, 1.0)]
    [InlineData(HoleKind.Simple, HoleSeat.Counterbore, 1.2)]
    public void ToInventorCm_AndBack(HoleKind kind, HoleSeat seat, double expected)
    {
        Assert.Equal(expected, HoleDepthReference.ToInventorCm(kind, seat, 1.2, 0.2)!.Value, 9);
        Assert.Equal(1.2, HoleDepthReference.ToWindowCm(HoleStandards.IsThreaded(kind), seat, expected, 0.2)!.Value, 9);
    }

    [Fact]
    public void NullsAndAMissingSeatDepth_ChangeNothing()
    {
        Assert.Null(HoleDepthReference.ToInventorCm(HoleKind.Tapped, HoleSeat.Counterbore, null, 0.2));
        Assert.Equal(1.2, HoleDepthReference.ToInventorCm(HoleKind.Tapped, HoleSeat.Counterbore, 1.2, null));
        Assert.Equal(1.2, HoleDepthReference.ToWindowCm(true, null, 1.2, 0.2));
    }

    [Fact]
    public void CountersinkDepth_IsTheConeDownToTheHoleDiameter()
    {
        // (10.4 - 4.2) / 2 / tan(45 deg) = 3.1.
        Assert.Equal(3.1, HoleDepthReference.CountersinkDepth(10.4, 4.2, 90)!.Value, 9);
        Assert.Equal(1.5 / Math.Tan(Math.PI / 6), HoleDepthReference.CountersinkDepth(7, 4, 60)!.Value, 9);
        Assert.Null(HoleDepthReference.CountersinkDepth(4, 4.2, 90));
        Assert.Null(HoleDepthReference.CountersinkDepth(10.4, 4.2, 180));
        Assert.Null(HoleDepthReference.CountersinkDepth(10.4, null, 90));
    }

    // ---- a threaded hole: below the seat (question 12) ----

    [Fact]
    public void ACounterboredBlindTappedHole_KeepsItsFullThreadBelowTheCounterbore()
    {
        // The acceptance case: thread 9 under a 6 mm counterbore is 9 mm of thread below the counterbore, so Inventor
        // (which measures from the start face) takes 15, and the drill 12 + 6 = 18.
        var g = Geometry(SeatedTapped(HoleSeat.Counterbore));

        Assert.Equal(1.8, g.DepthCm!.Value, 9);
        Assert.Equal(1.5, g.Thread!.ThreadDepthCm, 9);
        Assert.Equal(0.6, g.CounterboreDepthCm!.Value, 9);
    }

    [Fact]
    public void ChangingTheCounterboreDepth_KeepsTheThreadLengthBelowIt()
    {
        var g = Geometry(SeatedTapped(HoleSeat.Counterbore, seatDepth: 2.5m));

        Assert.Equal(1.45, g.DepthCm!.Value, 9);
        Assert.Equal(1.15, g.Thread!.ThreadDepthCm, 9);
        Assert.Equal(0.9, g.Thread.ThreadDepthCm - g.CounterboreDepthCm!.Value, 9);
    }

    [Fact]
    public void ACountersunkBlindTappedHole_IsMeasuredFromWhereTheConeMeetsTheTapDrill()
    {
        // A 10.4 x 90 deg countersink on the 4.2 tap drill is 3.1 deep.
        var g = Geometry(SeatedTapped(HoleSeat.Countersink));

        Assert.Equal(1.51, g.DepthCm!.Value, 9);
        Assert.Equal(1.21, g.Thread!.ThreadDepthCm, 9);
    }

    [Fact]
    public void ASpotfacedBlindTappedHole_IsWrittenAsItIs_SinceInventorMeasuresFromTheSpotfaceBottom()
    {
        var g = Geometry(SeatedTapped(HoleSeat.Spotface, seatDepth: 1m));

        Assert.Equal(1.2, g.DepthCm!.Value, 9);
        Assert.Equal(0.9, g.Thread!.ThreadDepthCm, 9);
    }

    [Fact]
    public void ATappedHoleWithNoSeat_IsWrittenAsItIs()
    {
        var g = Geometry(SeatedTapped(HoleSeat.None));

        Assert.Equal((1.2, 0.9), (g.DepthCm!.Value, g.Thread!.ThreadDepthCm));
    }

    [Fact]
    public void ACountersunkTaperTappedHole_HasItsDrillDepthBelowTheCone()
    {
        // NPT 1/4: the row's tap drill 0.44 in, a 0.6 x 90 deg countersink is 0.08 in deep; drill 0.88 in below it.
        var g = Geometry(HoleTestWorkbook.Clearance() with
        {
            Kind = HoleKind.TaperTapped, Size = "1/4", Units = HoleUnits.Imperial, Termination = HoleTermination.Distance,
            DrillPoint = "118 deg", Seat = HoleSeat.Countersink, CsinkDiameter = 0.6m, CsinkAngleDeg = 90m,
        });

        Assert.Equal(0.96 * 2.54, g.DepthCm!.Value, 9);
    }

    [Fact]
    public void AThroughAllTappedHole_HasNoDepthToMove()
    {
        var g = Geometry(SeatedTapped(HoleSeat.Counterbore) with { Termination = HoleTermination.ThroughAll });

        Assert.Null(g.DepthCm);
        Assert.True(g.Thread!.FullDepth);
    }

    [Fact]
    public void ACounterboreDeeperThanTheDrillDepth_IsFineOnATappedHole_ButNotOnAClearanceHole()
    {
        // The tapped hole's drill depth is below the counterbore, so it always clears it (task 116).
        var tapped = Build(SeatedTapped(HoleSeat.Counterbore, seatDepth: 14m));
        var clearance = Build(BlindClearance(HoleSeat.Counterbore) with { CboreDepth = 14m });

        Assert.True(tapped.Succeeded, string.Join(" | ", tapped.Problems));
        Assert.Contains(clearance.Problems, p => p.Field == HoleField.Depth);
    }

    [Fact]
    public void ATappedSpotfaceOrCountersinkDeeperThanTheDrillDepth_IsFine()
    {
        var spotface = Build(SeatedTapped(HoleSeat.Spotface, seatDepth: 13m));
        var countersink = Build(SeatedTapped(HoleSeat.Countersink) with { DrillDepth = 3m, ThreadDepth = 2m });

        Assert.True(spotface.Succeeded, string.Join(" | ", spotface.Problems));
        Assert.True(countersink.Succeeded, string.Join(" | ", countersink.Problems));
        Assert.Equal(0.61, countersink.Geometry!.DepthCm!.Value, 9);
    }

    // ---- expressions on a threaded hole ----

    [Fact]
    public void DepthExpressions_OnACounterboredTappedHole_AreWrittenWithTheCounterboreAdded()
    {
        var g = Geometry(SeatedTapped(HoleSeat.Counterbore) with
        {
            DrillDepthExpression = "d0", ThreadDepthExpression = "d0 * 0.75",
        });

        Assert.Equal("(d0) + (6 mm)", g.DepthExpression);
        Assert.Equal("(d0 * 0.75) + (6 mm)", g.Thread!.ThreadDepthExpression);
    }

    [Fact]
    public void ACounterboreDepthExpression_KeepsPlainThreadAndDrillDepthsParametric()
    {
        var g = Geometry(SeatedTapped(HoleSeat.Counterbore) with { CboreDepthExpression = "d1" });

        Assert.Equal("(12 mm) + (d1)", g.DepthExpression);
        Assert.Equal("(9 mm) + (d1)", g.Thread!.ThreadDepthExpression);
        Assert.Equal("d1", g.CounterboreDepthExpression);
    }

    [Fact]
    public void ADepthExpression_OnACountersunkTappedHole_AddsTheConeDepth()
    {
        var g = Geometry(SeatedTapped(HoleSeat.Countersink) with { DrillDepthExpression = "d0" });

        Assert.Equal("(d0) + (3.1 mm)", g.DepthExpression);
        Assert.Null(g.Thread!.ThreadDepthExpression);
    }

    [Fact]
    public void ADepthExpression_OnASpotfacedTappedHole_IsWrittenAsTyped()
    {
        var g = Geometry(SeatedTapped(HoleSeat.Spotface, seatDepth: 1m) with
        {
            DrillDepthExpression = "d0", SpotfaceDepthExpression = "d1",
        });

        Assert.Equal("d0", g.DepthExpression);
        Assert.Null(g.Thread!.ThreadDepthExpression);
    }

    [Fact]
    public void PlainDepthsAndAPlainSeat_WriteNoExpression()
    {
        var g = Geometry(SeatedTapped(HoleSeat.Counterbore));

        Assert.Null(g.DepthExpression);
        Assert.Null(g.Thread!.ThreadDepthExpression);
    }

    [Theory]
    [InlineData("(d0) + (6 mm)", "d0")]
    [InlineData("(d0)+(6 mm)", "d0")]
    [InlineData("(d0 * 2 + 1) + (0.6 cm)", "d0 * 2 + 1")]
    [InlineData("(12 mm) + (6 mm)", null)]
    [InlineData("d0", "(d0) - (6 mm)")]
    [InlineData("(d0) + (5 mm)", "((d0) + (5 mm)) - (6 mm)")]
    [InlineData("(d0) - (6 mm)", "((d0) - (6 mm)) - (6 mm)")]
    [InlineData("(d0) + (6 mm) * 2", "((d0) + (6 mm) * 2) - (6 mm)")]
    public void ToWindowExpression_OnAThreadedCounterbore_UndoesOnlyTheWrittenForm(string inventor, string? window)
    {
        Assert.Equal(window, HoleDepthReference.ToWindowExpression(true, HoleSeat.Counterbore, inventor, 0.6, null));
    }

    [Fact]
    public void ToWindowExpression_MatchesTheSeatByItsExpression()
    {
        Assert.Equal("d0", HoleDepthReference.ToWindowExpression(true, HoleSeat.Counterbore, "(d0) + (d1)", 0.6, "d1"));
        Assert.Equal("d0", HoleDepthReference.ToWindowExpression(true, HoleSeat.Counterbore, "(d0) + ( d1 )", 0.6, "d1"));
        Assert.Null(HoleDepthReference.ToWindowExpression(true, HoleSeat.Counterbore, "(12 mm) + (d1)", 0.6, "d1"));
        Assert.Equal(
            "((d0) + (d2)) - (d1)",
            HoleDepthReference.ToWindowExpression(true, HoleSeat.Counterbore, "(d0) + (d2)", 0.6, "d1"));
    }

    [Fact]
    public void ToWindowExpression_OnAThreadedSpotfaceOrNoSeat_IsUnchanged()
    {
        Assert.Equal("(d0) - (1 mm)", HoleDepthReference.ToWindowExpression(true, HoleSeat.Spotface, "(d0) - (1 mm)", 0.1, null));
        Assert.Equal("d0", HoleDepthReference.ToWindowExpression(true, HoleSeat.None, "d0", null, null));
    }

    [Theory]
    [InlineData(HoleSeat.Counterbore)]
    [InlineData(HoleSeat.Countersink)]
    [InlineData(HoleSeat.Spotface)]
    [InlineData(HoleSeat.None)]
    public void ToWindow_GivesTheWindowsDepthsBack_OnEachSeat(HoleSeat seat)
    {
        var g = Geometry(SeatedTapped(seat, seatDepth: 2m));

        var facts = HoleDepthReference.ToWindow(FactsOf(g), M5TapDrillCm);

        Assert.Equal(1.2, facts.DepthCm!.Value, 9);
        Assert.Equal(0.9, facts.ThreadDepthCm!.Value, 9);
    }

    [Fact]
    public void ToWindow_GivesTheWindowsExpressionsBack()
    {
        var g = Geometry(SeatedTapped(HoleSeat.Counterbore) with
        {
            DrillDepthExpression = "d0", ThreadDepthExpression = "d0 * 0.75", CboreDepthExpression = "d1",
        });

        var facts = HoleDepthReference.ToWindow(FactsOf(g), M5TapDrillCm);

        Assert.Equal(("d0", "d0 * 0.75"), (facts.DepthExpression, facts.ThreadDepthExpression));
    }

    [Fact]
    public void ToWindow_OnACountersink_NeedsTheTapDrill_SinceInventorDoesNotReportIt()
    {
        var facts = FactsOf(Geometry(SeatedTapped(HoleSeat.Countersink)));

        Assert.Equal(1.51, HoleDepthReference.ToWindow(facts).DepthCm!.Value, 9);
        Assert.Equal(1.2, HoleDepthReference.ToWindow(facts, M5TapDrillCm).DepthCm!.Value, 9);
    }

    // ---- any other hole: from the top face (question 16, Default) ----

    [Fact]
    public void ASpotfaceAndACounterboreClearanceHole_WithTheSameWindowDepth_EndAtTheSameDepth()
    {
        // Spec 09 question 16 (Default): a blind clearance hole's Depth is from the top face, whatever its seat. Inventor
        // measures the counterbore hole from the top and the spotface hole from the spotface's bottom.
        var counterbore = Geometry(BlindClearance(HoleSeat.Counterbore));
        var spotface = Geometry(BlindClearance(HoleSeat.Spotface));

        Assert.Equal(1.2, counterbore.DepthCm!.Value, 9);
        Assert.Equal(1.0, spotface.DepthCm!.Value, 9);
        Assert.Equal(
            counterbore.DepthCm!.Value,
            spotface.DepthCm!.Value + spotface.SpotfaceDepthCm!.Value,
            9);
    }

    [Fact]
    public void ACountersunkClearanceHole_IsWrittenAsItIs()
    {
        var g = Geometry(HoleTestWorkbook.Clearance() with
        {
            Seat = HoleSeat.Countersink, Termination = HoleTermination.Distance, Depth = 12m, DrillPoint = "118 deg",
            CsinkDiameter = 10.4m, CsinkAngleDeg = 90m,
        });

        Assert.Equal(1.2, g.DepthCm!.Value, 9);
    }

    [Fact]
    public void ADepthExpression_OnASpotfaceClearanceHole_IsWrittenLessTheSpotface()
    {
        var g = Geometry(BlindClearance(HoleSeat.Spotface) with { DepthExpression = "d0" });

        Assert.Equal("(d0) - (2 mm)", g.DepthExpression);
    }

    [Fact]
    public void ASpotfaceDepthExpression_KeepsAPlainDepthParametric()
    {
        var g = Geometry(BlindClearance(HoleSeat.Spotface) with { SpotfaceDepthExpression = "d1" });

        Assert.Equal("(12 mm) - (d1)", g.DepthExpression);
        Assert.Equal("d1", g.SpotfaceDepthExpression);
    }

    [Fact]
    public void ADepthExpression_OnACounterboreClearanceHole_IsWrittenAsTyped()
    {
        var g = Geometry(BlindClearance(HoleSeat.Counterbore) with { DepthExpression = "d0" });

        Assert.Equal("d0", g.DepthExpression);
    }

    [Theory]
    [InlineData("(d0) - (2 mm)", "d0")]
    [InlineData("(d0)-(2 mm)", "d0")]
    [InlineData("(d0) - (0.2 cm)", "d0")]
    [InlineData("d0", "(d0) + (2 mm)")]
    [InlineData("(d0) + (2 mm)", "((d0) + (2 mm)) + (2 mm)")]
    [InlineData("(d0) - (2 mm) * 2", "((d0) - (2 mm) * 2) + (2 mm)")]
    [InlineData("(d0 * 2 + 1) - (d1)", "((d0 * 2 + 1) - (d1)) + (2 mm)")]
    [InlineData("(d0) - (5 mm)", "((d0) - (5 mm)) + (2 mm)")]
    public void ToWindowExpression_OnASpotfaceClearanceHole_UndoesOnlyTheWrittenForm(string inventor, string window)
    {
        // Task 114's follow-up: a (X) - (Y) whose Y is not the spotface depth is not read as X.
        Assert.Equal(window, HoleDepthReference.ToWindowExpression(false, HoleSeat.Spotface, inventor, 0.2, null));
    }

    [Fact]
    public void ToWindowExpression_AddsTheSpotfaceExpression_AndLeavesOtherSeatsAlone()
    {
        Assert.Equal("(d0) + (d1)", HoleDepthReference.ToWindowExpression(false, HoleSeat.Spotface, "d0", 0.2, "d1"));
        Assert.Equal("d0", HoleDepthReference.ToWindowExpression(false, HoleSeat.Spotface, "(d0) - (d1)", 0.2, "d1"));
        Assert.Equal("d0", HoleDepthReference.ToWindowExpression(false, HoleSeat.Counterbore, "d0", 0.2, null));
        Assert.Null(HoleDepthReference.ToWindowExpression(false, HoleSeat.Spotface, null, 0.2, null));
    }

    // ---- read-back of holes made by Inventor ----

    [Fact]
    public void AnInventorSpotfaceHole_ReadsAsASpotface_WithItsDepthFromTheTop()
    {
        // A T5 normal (5.5 mm) hole, blind 10 below a 9.4 x 2 spotface: Inventor reports a Depth of 10 mm.
        var facts = FactsOf(Geometry(BlindClearance(HoleSeat.None) with { Depth = 10m })) with
        {
            Seat = HoleSeat.Spotface,
            SpotfaceDiameterCm = 0.94,
            SpotfaceDepthCm = 0.2,
        };

        var result = HoleReadBack.Read(null, facts, Workbook, HoleUnits.Metric);

        Assert.Equal(HoleKind.Clearance, result.Spec.Kind);
        Assert.Equal((HoleSeat.Spotface, 9.4m, 2m), (result.Spec.Seat, result.Spec.SpotfaceDiameter, result.Spec.SpotfaceDepth));
        Assert.Equal(12m, result.Spec.Depth);
    }

    [Fact]
    public void AnInventorCounterboredTappedHole_OpensWithItsDepthsBelowTheCounterbore()
    {
        // Inventor: drill 16 and thread 13.5 from the start face, with a 9.5 x 6 counterbore.
        var facts = FactsOf(Geometry(SeatedTapped(HoleSeat.None))) with
        {
            Seat = HoleSeat.Counterbore,
            DepthCm = 1.6,
            ThreadDepthCm = 1.35,
            CounterboreDiameterCm = 0.95,
            CounterboreDepthCm = 0.6,
        };

        var result = HoleReadBack.Read(null, facts, Workbook, HoleUnits.Metric);

        Assert.Equal((HoleKind.Tapped, HoleSeat.Counterbore), (result.Spec.Kind, result.Spec.Seat));
        Assert.Equal((10m, 7.5m), (result.Spec.DrillDepth, result.Spec.ThreadDepth));
    }

    [Fact]
    public void AnInventorCountersunkTappedHole_TakesTheConeAtTheWorkbooksTapDrill()
    {
        // Inventor reports no diameter for a tapped hole: the M5 row's 4.2 tap drill gives a 3.1 mm cone.
        var facts = FactsOf(Geometry(SeatedTapped(HoleSeat.None))) with
        {
            Seat = HoleSeat.Countersink,
            DepthCm = 1.31,
            ThreadDepthCm = 1.06,
            CountersinkDiameterCm = 1.04,
            CountersinkAngleDeg = 90,
        };

        var result = HoleReadBack.Read(null, facts, Workbook, HoleUnits.Metric);

        Assert.Equal((10m, 7.5m), (result.Spec.DrillDepth, result.Spec.ThreadDepth));
    }

    [Fact]
    public void TapDrillCm_IsTheRowsTapDrill_ForAStraightOrATaperThread()
    {
        var tapped = FactsOf(Geometry(SeatedTapped(HoleSeat.None)));
        var taper = tapped with { TaperedThread = true, ThreadDesignation = "1/4", ThreadType = "NPT" };

        Assert.Equal(M5TapDrillCm, HoleReadBack.TapDrillCm(tapped, Workbook)!.Value, 9);
        Assert.Equal(0.44 * 2.54, HoleReadBack.TapDrillCm(taper, Workbook)!.Value, 9);
        Assert.Null(HoleReadBack.TapDrillCm(tapped with { ThreadDesignation = "M99" }, Workbook));
        Assert.Null(HoleReadBack.TapDrillCm(tapped with { Tapped = false }, Workbook));
    }

    [Fact]
    public void AnInventorTaperTappedHole_ReadsAsTaperTapped_WithItsThread()
    {
        var facts = FactsOf(Geometry(HoleTestWorkbook.Clearance() with
        {
            Kind = HoleKind.TaperTapped, Size = "1/4", Units = HoleUnits.Imperial, Termination = HoleTermination.Distance,
            DrillPoint = "118 deg", Seat = HoleSeat.Countersink, CsinkDiameter = 0.6m, CsinkAngleDeg = 90m,
        }));

        var result = HoleReadBack.Read(null, facts, Workbook, HoleUnits.Metric);

        Assert.Equal(HoleKind.TaperTapped, result.Spec.Kind);
        Assert.Equal((HoleUnits.Imperial, "1/4"), (result.Spec.Units, result.Spec.Size));
        Assert.Equal(HoleSeat.Countersink, result.Spec.Seat);
        Assert.Equal(0.88m, result.Spec.DrillDepth);
        Assert.Empty(result.Notes);
    }

    // ---- re-apply ----

    [Fact]
    public void ASpotfaceHole_MadeByTheAddin_IsNotOfferedReapply()
    {
        var build = Build(BlindClearance(HoleSeat.Spotface));

        var offer = HoleReapplyRules.Check(Stored(build), FactsOf(build.Geometry!), coloured: false);

        Assert.False(offer.IsOffered, string.Join(" | ", offer.Differences));
    }

    [Theory]
    [InlineData(HoleSeat.Counterbore)]
    [InlineData(HoleSeat.Countersink)]
    [InlineData(HoleSeat.Spotface)]
    public void ASeatedTappedHole_MadeByTheAddin_IsNotOfferedReapply(HoleSeat seat)
    {
        var build = Build(SeatedTapped(seat, seatDepth: 2m));

        var offer = HoleReapplyRules.Check(Stored(build), FactsOf(build.Geometry!), coloured: false);

        Assert.False(offer.IsOffered, string.Join(" | ", offer.Differences));
    }

    [Fact]
    public void ASpotfaceHole_WhoseDepthChangedInInventor_IsOfferedReapply_InWindowTerms()
    {
        var build = Build(BlindClearance(HoleSeat.Spotface));

        var offer = HoleReapplyRules.Check(Stored(build), FactsOf(build.Geometry!) with { DepthCm = 1.3 }, coloured: false);

        Assert.Contains(offer.Differences, d => d.Contains("15 mm", StringComparison.Ordinal));
    }

    [Fact]
    public void ACounterboredTappedHole_WhoseDrillDepthChangedInInventor_IsOfferedReapply_BelowTheSeat()
    {
        var build = Build(SeatedTapped(HoleSeat.Counterbore));

        // Inventor's 20 from the start face is 14 below the 6 mm counterbore.
        var offer = HoleReapplyRules.Check(Stored(build), FactsOf(build.Geometry!) with { DepthCm = 2.0 }, coloured: false);

        Assert.Contains(offer.Differences, d => d.Contains("drill depth is 14 mm", StringComparison.Ordinal));
    }

    // ---- data stored before task 116 (version 4 and older) ----

    /// <summary>The attributes of <paramref name="data"/> as an add-in before task 116 wrote them, at <paramref name="version"/>.</summary>
    private static IEnumerable<HoleAttribute> AtVersion(HoleStoredData data, int version) =>
        data.ToAttributes().Select(a => a.Name == HoleStoredData.VersionName ? new HoleAttribute(a.Name, version) : a);

    /// <summary>
    /// A hole an add-in before task 116 made: <paramref name="topFaceSpec"/>'s depths were from the top face, and it wrote
    /// them unchanged on a counterbore or countersink, less the spotface on a spotface (task 114).
    /// </summary>
    private static (HoleStoredDataReadResult Stored, InventorHoleFacts Facts) MadeBefore116(HoleSpec topFaceSpec, int version = 4)
    {
        var build = Build(topFaceSpec);
        Assert.True(build.Succeeded, string.Join(" | ", build.Problems));
        var g = build.Geometry!;
        var spotface = g.SpotfaceDepthCm ?? 0;
        var facts = FactsOf(g) with
        {
            // Undo task 116's own conversion, to what the old add-in wrote.
            DepthCm = HoleRules.ToCm(build.Resolved.DrillDepth!.Value, topFaceSpec.Units) - spotface,
            ThreadDepthCm = HoleRules.ToCm(build.Resolved.ThreadDepth!.Value, topFaceSpec.Units) - spotface,
            DepthExpression = null,
            ThreadDepthExpression = null,
        };
        return (HoleStoredData.Read(AtVersion(Stored(build), version)), facts);
    }

    [Fact]
    public void StoredData_IsWrittenAtVersion6_WithDepthsBelowTheSeat()
    {
        var attributes = Stored(Build(SeatedTapped(HoleSeat.Counterbore))).ToAttributes();

        // Version 6 (task 150) keeps version 5's depths below the seat.
        Assert.Equal(new HoleAttribute(HoleStoredData.VersionName, 6), attributes[0]);
        Assert.Equal(6, HoleStoredData.Read(attributes).Data!.Version);
        Assert.True(HoleStoredData.CurrentVersion >= HoleStoredData.DepthsBelowSeatVersion);
    }

    [Fact]
    public void Version4Data_ReadsWithItsVersion_AndItsDepthsAsStored()
    {
        var (stored, _) = MadeBefore116(SeatedTapped(HoleSeat.Counterbore) with { DrillDepth = 18m, ThreadDepth = 15m });

        Assert.Equal(4, stored.Data!.Version);
        Assert.Equal((18m, 15m), (stored.Data.Spec.DrillDepth, stored.Data.Spec.ThreadDepth));
    }

    [Theory]
    [InlineData(HoleSeat.Counterbore, 6, 18, 15)]
    [InlineData(HoleSeat.Spotface, 1, 13, 10)]
    [InlineData(HoleSeat.Countersink, 3.1, 15.1, 12.1)]
    [InlineData(HoleSeat.None, 0, 12, 9)]
    [InlineData(HoleSeat.Counterbore, 6, 18, 15, 1)]
    public void AnOlderTappedHole_OpensWithItsDepthsBelowTheSeat(
        HoleSeat seat, double seatDepth, double oldDrill, double oldThread, int version = 4)
    {
        // Made before task 116 with depths from the top face: the hole in Inventor has 12 drill and 9 thread below the
        // seat, and opens so.
        var spec = SeatedTapped(seat, seatDepth: (decimal)seatDepth) with
        {
            DrillDepth = (decimal)oldDrill, ThreadDepth = (decimal)oldThread,
        };
        var (stored, facts) = MadeBefore116(spec, version);

        var result = HoleReadBack.Read(stored, facts, Workbook, HoleUnits.Metric);

        Assert.Equal(HoleSource.Awb, result.Source);
        Assert.Equal((12m, 9m), (result.Spec.DrillDepth, result.Spec.ThreadDepth));
        Assert.Equal(HoleStoredData.CurrentVersion, result.Stored!.Version);
        Assert.Equal((12m, 9m), (result.Stored.Spec.DrillDepth, result.Stored.Spec.ThreadDepth));
    }

    public static TheoryData<string> OlderHoles => new() { "metric counterbore", "imperial 82 deg countersink" };

    private static HoleSpec OlderHole(string name) => name switch
    {
        "metric counterbore" => SeatedTapped(HoleSeat.Counterbore) with { DrillDepth = 18m, ThreadDepth = 15m },
        // #7-36 UNF on its 0.12 in tap drill under a 0.3 in x 82 deg countersink: an irrational cone depth
        // (0.09 / tan 41 deg = 0.1035...), the case where rounding the seat to 5 decimals of an inch was out by more
        // than the re-apply tolerance.
        "imperial 82 deg countersink" => HoleTestWorkbook.Tapped("#7-36 UNF") with
        {
            Units = HoleUnits.Imperial, Seat = HoleSeat.Countersink, DrillPoint = "118 deg",
            CsinkDiameter = 0.3m, CsinkAngleDeg = 82m, DrillDepth = 0.45m, ThreadDepth = 0.35m,
        },
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };

    [Theory]
    [MemberData(nameof(OlderHoles))]
    public void AnOlderTappedHole_IsNotOfferedReapply_AndRewritesTheSameHole(string name)
    {
        var (stored, facts) = MadeBefore116(OlderHole(name));

        var result = HoleReadBack.Read(stored, facts, Workbook, HoleUnits.Metric);
        var offer = HoleReapplyRules.Check(result.Stored!, facts, coloured: false);
        var again = Geometry(result.Spec);

        Assert.False(offer.IsOffered, string.Join(" | ", offer.Differences));
        Assert.Equal(facts.DepthCm!.Value, again.DepthCm!.Value, 9);
        Assert.Equal(facts.ThreadDepthCm!.Value, again.Thread!.ThreadDepthCm, 9);
    }

    [Fact]
    public void AnOlderTappedHole_ReappliedWithoutReadBack_IsNotOfferedEither()
    {
        var (stored, facts) = MadeBefore116(SeatedTapped(HoleSeat.Countersink) with { DrillDepth = 15.1m, ThreadDepth = 12.1m });

        var offer = HoleReapplyRules.Check(stored.Data!, facts, coloured: false);

        Assert.False(offer.IsOffered, string.Join(" | ", offer.Differences));
    }

    [Fact]
    public void AnOlderTappedHole_WithDepthExpressions_KeepsThemLessTheSeat()
    {
        var spec = SeatedTapped(HoleSeat.Counterbore) with
        {
            DrillDepth = 18m, DrillDepthExpression = "d0", ThreadDepth = 15m, ThreadDepthExpression = "d0 - 3",
        };
        var (stored, facts) = MadeBefore116(spec);

        var result = HoleReadBack.Read(stored, facts, Workbook, HoleUnits.Metric);

        Assert.Equal(("(d0) - (6 mm)", "(d0 - 3) - (6 mm)"), (result.Spec.DrillDepthExpression, result.Spec.ThreadDepthExpression));
    }

    [Fact]
    public void AnOlderTappedHole_WithACounterboreExpression_TakesTheExpressionOff()
    {
        var spec = SeatedTapped(HoleSeat.Counterbore) with
        {
            DrillDepth = 18m, DrillDepthExpression = "d0", ThreadDepth = 15m, CboreDepthExpression = "d1",
        };
        var (stored, facts) = MadeBefore116(spec);

        var result = HoleReadBack.Read(stored, facts, Workbook, HoleUnits.Metric);

        Assert.Equal("(d0) - (d1)", result.Spec.DrillDepthExpression);
        Assert.Equal(9m, result.Spec.ThreadDepth);
    }

    [Fact]
    public void AnOlderTaperTappedHole_HasItsDrillDepthConverted()
    {
        var spec = HoleTestWorkbook.Clearance() with
        {
            Kind = HoleKind.TaperTapped, Size = "1/4", Units = HoleUnits.Imperial, Termination = HoleTermination.Distance,
            DrillPoint = "118 deg", Seat = HoleSeat.Countersink, CsinkDiameter = 0.6m, CsinkAngleDeg = 90m,
            DrillDepth = 0.96m,
        };
        var build = Build(spec);
        var stored = HoleStoredData.Read(AtVersion(Stored(build), 4));

        var result = HoleReadBack.Read(stored, FactsOf(build.Geometry!), Workbook, HoleUnits.Metric);

        Assert.Equal(0.88m, result.Spec.DrillDepth);
    }

    [Theory]
    [InlineData(HoleSeat.Spotface)]
    [InlineData(HoleSeat.Counterbore)]
    public void AnOlderClearanceHole_KeepsItsDepth(HoleSeat seat)
    {
        // Question 16 (Default): a blind clearance hole's Depth is still from the top face.
        var build = Build(BlindClearance(seat));
        var stored = HoleStoredData.Read(AtVersion(Stored(build), 4));

        var result = HoleReadBack.Read(stored, FactsOf(build.Geometry!), Workbook, HoleUnits.Metric);

        Assert.Equal(12m, result.Spec.Depth);
        Assert.Equal(HoleStoredData.CurrentVersion, result.Stored!.Version);
    }

    [Fact]
    public void FromTopFace_LeavesThroughAllUnseatedAndUntappedHoles_AndAnUnknownSeatDepth_Alone()
    {
        var through = SeatedTapped(HoleSeat.Counterbore) with { Termination = HoleTermination.ThroughAll };
        var unseated = SeatedTapped(HoleSeat.None);
        var clearance = BlindClearance(HoleSeat.Counterbore);
        var counterbored = SeatedTapped(HoleSeat.Counterbore);

        Assert.Same(through, HoleDepthReference.FromTopFace(through, 0.6, null));
        Assert.Same(unseated, HoleDepthReference.FromTopFace(unseated, 0.6, null));
        Assert.Same(clearance, HoleDepthReference.FromTopFace(clearance, 0.2, null));
        Assert.Same(counterbored, HoleDepthReference.FromTopFace(counterbored, null, null));
        Assert.Same(counterbored, HoleDepthReference.FromTopFace(counterbored, 0, null));
    }

    // ---- a Simple diameter's expression ----

    [Theory]
    [InlineData("spk_d * 2 ul", "spk_d * 2")]
    [InlineData("spk_d * 2ul", "spk_d * 2ul")]
    [InlineData("d0 * 1.5 UL", "d0 * 1.5")]
    [InlineData("1.5 ul * d0", "1.5 ul * d0")]
    [InlineData("width_ul", "width_ul")]
    [InlineData("d0 * ul", "d0 * ul")]
    [InlineData("  d0  ", "d0")]
    [InlineData("ul", "ul")]
    public void FromInventor_DropsOnlyATrailingUnitlessAfterANumber(string inventor, string shown)
    {
        Assert.Equal(shown, HoleExpressions.FromInventor(inventor));
    }

    [Fact]
    public void FromInventor_BlankIsNull()
    {
        Assert.Null(HoleExpressions.FromInventor("  "));
        Assert.Null(HoleExpressions.FromInventor(null));
    }

    [Fact]
    public void ASimpleDiameterExpression_GoesToTheGeometry()
    {
        var g = Geometry(HoleTestWorkbook.Clearance() with
        {
            Kind = HoleKind.Simple, Size = "", Diameter = 8m, DiameterExpression = "d0 * 2",
        });

        Assert.Equal((0.8, "d0 * 2"), (g.HoleDiameterCm, g.DiameterExpression));
    }
}
