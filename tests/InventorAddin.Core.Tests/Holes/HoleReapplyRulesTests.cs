using InventorAddin.Core.Holes;
using static InventorAddin.Core.Tests.Holes.HoleTestWorkbook;

namespace InventorAddin.Core.Tests.Holes;

public sealed class HoleReapplyRulesTests
{
    private static readonly HoleWorkbook Workbook = Create();

    private sealed class FakeChecker : IHoleExpressionChecker
    {
        public double D0Cm { get; set; } = 2.0;

        public HoleExpressionCheck Check(string expression, HoleUnits units) => expression switch
        {
            "d0" => HoleExpressionCheck.Valid(D0Cm),
            "d0/8" => HoleExpressionCheck.Valid(D0Cm / 8),
            _ => HoleExpressionCheck.Invalid(),
        };
    }

    /// <summary>The data AWB Hole stores when it applies <paramref name="spec"/>, and the hole it leaves.</summary>
    private static (HoleStoredData Stored, InventorHoleFacts Facts, bool Coloured) Applied(HoleSpec spec)
    {
        var build = HoleRules.Build(spec, Workbook, 1);
        Assert.True(build.Succeeded, string.Join(" | ", build.Problems));
        var g = build.Geometry!;
        var facts = new InventorHoleFacts(
            g.Seat,
            g.Thread is not null,
            g.Thread?.Designation,
            g.Thread?.ThreadType,
            g.Thread is { FullDepth: false } t ? t.ThreadDepthCm : null,
            // A tapped hole reports its model diameter, not necessarily the tap drill (the comparison skips it).
            g.Thread is not null ? 0.4917 : g.HoleDiameterCm,
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
            g.Flip);
        return (new HoleStoredData(build.Resolved, HoleAppliedState.From(g)), facts, g.IsColoured);
    }

    private static HoleReapplyOffer Check(
        (HoleStoredData Stored, InventorHoleFacts Facts, bool Coloured) hole, IHoleExpressionChecker? checker = null) =>
        HoleReapplyRules.Check(hole.Stored, hole.Facts, hole.Coloured, checker);

    public static TheoryData<HoleSpec> EveryKind() => new()
    {
        Clearance(),
        Clearance() with { Termination = HoleTermination.Distance, Depth = 12.5m },
        Clearance() with { Seat = HoleSeat.Counterbore, Fastener = "TLOW" },
        Clearance() with { Seat = HoleSeat.Countersink },
        Tapped(),
        Tapped() with { ThreadDepth = 6.25m, DrillDepth = 8.1m },
        Tapped() with { Termination = HoleTermination.ThroughAll },
        Dowel(),
        Dowel() with { Termination = HoleTermination.Distance, Depth = 14m },
        CboredDowel(),
        CsunkDowel(),
        HoleSpec.ClearanceHole(HoleUnits.Imperial, "#7") with { Termination = HoleTermination.Distance, Depth = 0.375m },
    };

    [Theory]
    [MemberData(nameof(EveryKind))]
    public void AHoleAsAwbHoleLeftIt_MakesNoOffer(HoleSpec spec)
    {
        var offer = Check(Applied(spec));

        Assert.False(offer.IsOffered);
        Assert.Same(HoleReapplyOffer.None, offer);
    }

    [Fact]
    public void ChangedDiameterAndLostTolerance_AreEachOffered()
    {
        var hole = Applied(Dowel());
        var changed = hole with
        {
            Facts = hole.Facts with
            {
                HoleDiameterCm = 0.8,
                ToleranceIsoCode = null,
                ToleranceUpperCm = null,
                ToleranceLowerCm = null,
            },
        };

        var offer = Check(changed);

        Assert.True(offer.IsOffered);
        Assert.Equal(2, offer.Differences.Count);
        Assert.Equal("The hole diameter is 8 mm; AWB Hole made it 7 mm.", offer.Differences[0]);
        Assert.StartsWith("The tolerance is none; AWB Hole set ", offer.Differences[1]);
    }

    [Fact]
    public void LostColour_IsOffered()
    {
        var hole = Applied(Dowel());

        var offer = Check(hole with { Coloured = false });

        Assert.Equal("The hole is no longer coloured by fit.", Assert.Single(offer.Differences));
    }

    [Fact]
    public void ATappedHolesDiameter_IsNotCompared()
    {
        var hole = Applied(Tapped());

        Assert.False(Check(hole with { Facts = hole.Facts with { HoleDiameterCm = 0.9 } }).IsOffered);
    }

    [Fact]
    public void ADiameterTheHoleDoesNotReport_IsNotCompared()
    {
        var hole = Applied(Dowel());

        Assert.False(Check(hole with { Facts = hole.Facts with { HoleDiameterCm = null } }).IsOffered);
    }

    [Fact]
    public void ChangedSeat_IsOffered_AndItsSizesAreNotCompared()
    {
        var hole = Applied(CboredDowel());
        var changed = hole with
        {
            Facts = hole.Facts with { Seat = HoleSeat.None, CounterboreDiameterCm = null, CounterboreDepthCm = null },
        };

        Assert.Equal(
            "The hole has no seat; AWB Hole made it with a counterbore.",
            Assert.Single(Check(changed).Differences));
    }

    [Fact]
    public void ASpotFace_IsNamedAsASeatAwbHoleDoesNotOffer()
    {
        var hole = Applied(Clearance());

        Assert.Equal(
            "The hole has a seat AWB Hole does not offer; AWB Hole made it with no seat.",
            Assert.Single(Check(hole with { Facts = hole.Facts with { Seat = null } }).Differences));
    }

    [Fact]
    public void ChangedTermination_IsOffered_AndTheDepthIsNotCompared()
    {
        var hole = Applied(Clearance() with { Termination = HoleTermination.Distance, Depth = 12.5m });
        var changed = hole with { Facts = hole.Facts with { Termination = HoleTermination.ThroughAll, DepthCm = null } };

        Assert.Equal(
            "The hole ends through all; AWB Hole made it end at a distance.",
            Assert.Single(Check(changed).Differences));
    }

    [Fact]
    public void ChangedDepth_IsOffered()
    {
        var hole = Applied(Clearance() with { Termination = HoleTermination.Distance, Depth = 12.5m });

        var offer = Check(hole with { Facts = hole.Facts with { DepthCm = 1.6 } });

        Assert.Equal("The depth is 16 mm; AWB Hole set 12.5 mm.", Assert.Single(offer.Differences));
    }

    [Fact]
    public void AnImperialHole_IsDescribedInInches()
    {
        var hole = Applied(
            HoleSpec.ClearanceHole(HoleUnits.Imperial, "#7") with { Termination = HoleTermination.Distance, Depth = 0.375m });

        var offer = Check(hole with { Facts = hole.Facts with { DepthCm = 1.27 } });

        Assert.Equal("The depth is 0.5 in; AWB Hole set 0.375 in.", Assert.Single(offer.Differences));
    }

    [Fact]
    public void ADepthExpression_IsComparedByWhatItComesToToday()
    {
        var spec = Dowel() with { Termination = HoleTermination.Distance, Depth = 20m, DepthExpression = "d0" };
        var hole = Applied(spec);
        var checker = new FakeChecker { D0Cm = 3.0 };

        // The hole followed d0 to 30 mm: no offer.
        Assert.False(Check(hole with { Facts = hole.Facts with { DepthCm = 3.0 } }, checker).IsOffered);

        // Its depth was changed away from d0 in Inventor: offered, naming the expression and its value.
        var offer = Check(hole with { Facts = hole.Facts with { DepthCm = 2.5 } }, checker);
        Assert.Equal("The depth is 25 mm; AWB Hole set d0 (30 mm).", Assert.Single(offer.Differences));
    }

    [Fact]
    public void ADepthExpressionThatCannotBeReadNow_IsNotCompared()
    {
        var spec = Dowel() with { Termination = HoleTermination.Distance, Depth = 20m, DepthExpression = "gone*2" };
        var hole = Applied(spec);
        var changed = hole with { Facts = hole.Facts with { DepthCm = 2.5 } };

        Assert.False(Check(changed, new FakeChecker()).IsOffered);
        Assert.False(Check(changed with { Stored = Applied(spec with { DepthExpression = "d0" }).Stored }, null).IsOffered);
    }

    [Fact]
    public void TappedDepths_AreCompared()
    {
        var hole = Applied(Tapped() with { ThreadDepth = 6.25m, DrillDepth = 8.1m });
        var changed = hole with { Facts = hole.Facts with { DepthCm = 1.0, ThreadDepthCm = 0.7 } };

        var offer = Check(changed);

        Assert.Equal(
            ["The drill depth is 10 mm; AWB Hole set 8.1 mm.", "The thread depth is 7 mm; AWB Hole set 6.25 mm."],
            offer.Differences);
    }

    [Fact]
    public void ATappedThreadMadeFullDepth_IsOffered()
    {
        var hole = Applied(Tapped() with { ThreadDepth = 6.25m, DrillDepth = 8.1m });

        Assert.Equal(
            "The thread runs the full depth; AWB Hole set a thread depth.",
            Assert.Single(Check(hole with { Facts = hole.Facts with { ThreadDepthCm = null } }).Differences));
    }

    [Fact]
    public void ADowelsCounterbore_IsCompared()
    {
        var hole = Applied(CboredDowel());
        var changed = hole with { Facts = hole.Facts with { CounterboreDiameterCm = 1.0, CounterboreDepthCm = 0.6 } };

        Assert.Equal(
            ["The counterbore diameter is 10 mm; AWB Hole set 9.5 mm.", "The counterbore depth is 6 mm; AWB Hole set 5.2 mm."],
            Check(changed).Differences);
    }

    [Fact]
    public void ADowelsCounterboreDepthExpression_IsComparedByItsValue()
    {
        var hole = Applied(CboredDowel() with { CboreDepth = 2.5m, CboreDepthExpression = "d0/8" });
        var checker = new FakeChecker();

        Assert.False(Check(hole with { Facts = hole.Facts with { CounterboreDepthCm = 0.25 } }, checker).IsOffered);
        Assert.Equal(
            "The counterbore depth is 3 mm; AWB Hole set d0/8 (2.5 mm).",
            Assert.Single(Check(hole with { Facts = hole.Facts with { CounterboreDepthCm = 0.3 } }, checker).Differences));
    }

    [Fact]
    public void ADowelsCountersink_IsCompared()
    {
        var hole = Applied(CsunkDowel());
        var changed = hole with { Facts = hole.Facts with { CountersinkDiameterCm = 1.2, CountersinkAngleDeg = 82 } };

        Assert.Equal(
            ["The countersink diameter is 12 mm; AWB Hole set 10.4 mm.", "The countersink angle is 82 deg; AWB Hole set 90 deg."],
            Check(changed).Differences);
    }

    [Fact]
    public void TinyAngleDifferences_Match()
    {
        var hole = Applied(CsunkDowel());

        Assert.False(Check(hole with { Facts = hole.Facts with { CountersinkAngleDeg = 90.001 } }).IsOffered);
    }

    [Fact]
    public void ADowelStoredBeforeItsSeatHadSizes_DoesNotCompareThem()
    {
        var hole = Applied(CboredDowel());
        var old = hole.Stored with { Spec = hole.Stored.Spec with { CboreDiameter = null, CboreDepth = null } };

        Assert.False(Check(hole with { Stored = old, Facts = hole.Facts with { CounterboreDiameterCm = 1.4 } }).IsOffered);
    }

    [Fact]
    public void AClearanceCounterbore_IsNotComparedBySize()
    {
        // Its sizes come from the workbook row, which the stored data does not hold.
        var hole = Applied(Clearance() with { Seat = HoleSeat.Counterbore, Fastener = "TLOW" });

        Assert.False(Check(hole with { Facts = hole.Facts with { CounterboreDiameterCm = 2.0 } }).IsOffered);
    }

    [Fact]
    public void Texts_SayWhatOkAndCancelDo()
    {
        Assert.Equal("This hole was changed outside AWB Hole:", HoleReapplyRules.HeadingText);
        Assert.Contains("OK re-applies them, with the fit, tolerance and colour", HoleReapplyRules.OfferText);
        Assert.Contains("Cancel leaves the hole as it is", HoleReapplyRules.OfferText);
    }

    [Fact]
    public void NullArguments_Throw()
    {
        var hole = Applied(Clearance());

        Assert.Throws<ArgumentNullException>(() => HoleReapplyRules.Check(null!, hole.Facts, false));
        Assert.Throws<ArgumentNullException>(() => HoleReapplyRules.Check(hole.Stored, null!, false));
    }
}
