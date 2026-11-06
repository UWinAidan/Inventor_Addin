using InventorAddin.Core.Holes;

namespace InventorAddin.Core.Tests.Holes;

/// <summary>
/// Task 114: the depths of a spotface hole as Inventor measures them (from the spotface's bottom, spike 108) against the
/// window's (from the top face), the read-back of spotface, taper tapped and Simple holes made by Inventor, and the
/// expression Inventor reports for a Simple diameter. Sizes and values are invented.
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

    private static HoleGeometry Geometry(HoleSpec spec)
    {
        var result = HoleRules.Build(spec, Workbook, 1);
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

    private static InventorHoleFacts FactsOf(HoleGeometry g) => new(
        g.Seat,
        g.Thread is not null,
        g.Thread?.Designation,
        g.Thread?.ThreadType,
        g.Thread is { FullDepth: false, IsTapered: false } thread ? thread.ThreadDepthCm : null,
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
    };

    // ---- the conversion ----

    [Theory]
    [InlineData(HoleSeat.None, 1.2)]
    [InlineData(HoleSeat.Counterbore, 1.2)]
    [InlineData(HoleSeat.Countersink, 1.2)]
    [InlineData(HoleSeat.Spotface, 1.0)]
    public void ToInventorCm_TakesTheSpotfaceDepthOff_OnlyForASpotface(HoleSeat seat, double expected)
    {
        Assert.Equal(expected, HoleDepthReference.ToInventorCm(seat, 1.2, 0.2)!.Value, 9);
        Assert.Equal(1.2, HoleDepthReference.ToWindowCm(seat, expected, 0.2)!.Value, 9);
    }

    [Fact]
    public void ToInventorCm_NullsAndAMissingSpotfaceDepth_ChangeNothing()
    {
        Assert.Null(HoleDepthReference.ToInventorCm(HoleSeat.Spotface, null, 0.2));
        Assert.Equal(1.2, HoleDepthReference.ToInventorCm(HoleSeat.Spotface, 1.2, null));
        Assert.Equal(1.2, HoleDepthReference.ToWindowCm(null, 1.2, 0.2));
    }

    [Fact]
    public void ASpotfaceAndACounterboreHole_WithTheSameWindowDepth_EndAtTheSameDepth()
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
    public void ABlindTappedSpotfaceHole_HasItsDrillAndThreadDepthsFromTheSpotfaceBottom()
    {
        var g = Geometry(HoleTestWorkbook.Tapped() with
        {
            Seat = HoleSeat.Spotface, SpotfaceDiameter = 11m, SpotfaceDepth = 1m, DrillPoint = "118 deg",
        });

        // M5: drill 10, thread 7.5, from the top face in the window.
        Assert.Equal(0.9, g.DepthCm!.Value, 9);
        Assert.Equal(0.65, g.Thread!.ThreadDepthCm, 9);
    }

    [Fact]
    public void ADepthExpression_OnASpotfaceHole_IsWrittenLessTheSpotface()
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
    public void ADepthExpression_OnAnyOtherSeat_IsWrittenAsTyped()
    {
        var g = Geometry(BlindClearance(HoleSeat.Counterbore) with { DepthExpression = "d0" });

        Assert.Equal("d0", g.DepthExpression);
    }

    [Theory]
    [InlineData("(d0) - (2 mm)", "d0")]
    [InlineData("(d0 * 2 + 1) - (d1)", "d0 * 2 + 1")]
    [InlineData("(d0)-(2 mm)", "d0")]
    [InlineData("d0", "(d0) + (2 mm)")]
    [InlineData("(d0) + (2 mm)", "((d0) + (2 mm)) + (2 mm)")]
    [InlineData("(d0) - (2 mm) * 2", "((d0) - (2 mm) * 2) + (2 mm)")]
    public void ToWindowExpression_UndoesTheWrittenForm_ElseAddsTheSpotface(string inventor, string window)
    {
        Assert.Equal(window, HoleDepthReference.ToWindowExpression(HoleSeat.Spotface, inventor, 0.2, null));
    }

    [Fact]
    public void ToWindowExpression_AddsTheSpotfaceExpression_AndLeavesOtherSeatsAlone()
    {
        Assert.Equal("(d0) + (d1)", HoleDepthReference.ToWindowExpression(HoleSeat.Spotface, "d0", 0.2, "d1"));
        Assert.Equal("d0", HoleDepthReference.ToWindowExpression(HoleSeat.Counterbore, "d0", 0.2, null));
        Assert.Null(HoleDepthReference.ToWindowExpression(HoleSeat.Spotface, null, 0.2, null));
    }

    // ---- read-back ----

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

    [Fact]
    public void ASpotfaceHole_MadeByTheAddin_IsNotOfferedReapply()
    {
        var spec = BlindClearance(HoleSeat.Spotface) with { DepthExpression = null };
        var build = HoleRules.Build(spec, Workbook, 1);
        var g = build.Geometry!;
        var stored = new HoleStoredData(build.Resolved, new HoleAppliedState(g.HoleDiameterCm, null, null, null, false));

        var offer = HoleReapplyRules.Check(stored, FactsOf(g), coloured: false);

        Assert.False(offer.IsOffered, string.Join(" | ", offer.Differences));
    }

    [Fact]
    public void ASpotfaceHole_WhoseDepthChangedInInventor_IsOfferedReapply_InWindowTerms()
    {
        var build = HoleRules.Build(BlindClearance(HoleSeat.Spotface), Workbook, 1);
        var g = build.Geometry!;
        var stored = new HoleStoredData(build.Resolved, new HoleAppliedState(g.HoleDiameterCm, null, null, null, false));

        var offer = HoleReapplyRules.Check(stored, FactsOf(g) with { DepthCm = 1.3 }, coloured: false);

        Assert.Contains(offer.Differences, d => d.Contains("15 mm", StringComparison.Ordinal));
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
