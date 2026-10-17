using InventorAddin.Core.Holes;

namespace InventorAddin.Core.Tests.Holes;

public sealed class HoleStateComparisonTests
{
    private static readonly HoleAppliedState IsoDowel = new(0.7, "G7", 0.002, 0.0005, true);
    private static readonly HoleAppliedState LimitsDowel = new(0.7, null, 0, -0.002, true);
    private static readonly HoleAppliedState Plain = new(0.55, null, null, null, false);

    [Fact]
    public void SameState_Matches()
    {
        Assert.True(HoleStateComparison.Compare(IsoDowel, IsoDowel).Matches);
        Assert.True(HoleStateComparison.Compare(LimitsDowel, LimitsDowel).Matches);
        Assert.True(HoleStateComparison.Compare(Plain, Plain).Matches);
    }

    [Fact]
    public void TinyDifferences_Match()
    {
        var current = LimitsDowel with { HoleDiameterCm = 0.7 + 1e-9, ToleranceLowerCm = -0.002 - 1e-9 };

        Assert.True(HoleStateComparison.Compare(LimitsDowel, current).Matches);
    }

    [Fact]
    public void IsoCode_MatchesByCode_IgnoringCase_WhenTheHoleReportsOne()
    {
        var current = IsoDowel with { ToleranceIsoCode = "g7", ToleranceUpperCm = null, ToleranceLowerCm = null };

        Assert.True(HoleStateComparison.Compare(IsoDowel, current).Matches);
        Assert.False(HoleStateComparison.Compare(IsoDowel, current with { ToleranceIsoCode = "H7" }).Matches);
    }

    [Fact]
    public void IsoCode_MatchesByLimits_WhenTheHoleReportsNoCode()
    {
        var current = IsoDowel with { ToleranceIsoCode = null };

        Assert.True(HoleStateComparison.Compare(IsoDowel, current).Matches);
        Assert.False(HoleStateComparison.Compare(IsoDowel, current with { ToleranceUpperCm = 0.0015 }).Matches);
    }

    [Fact]
    public void LimitsReplacedByAnIsoCode_DoesNotMatch()
    {
        var current = LimitsDowel with { ToleranceIsoCode = "H7" };

        Assert.False(HoleStateComparison.Compare(LimitsDowel, current).Matches);
    }

    [Fact]
    public void ChangedDiameter_IsReported()
    {
        var match = HoleStateComparison.Compare(IsoDowel, IsoDowel with { HoleDiameterCm = 0.8 });

        Assert.False(match.Matches);
        Assert.Equal("The hole diameter is 8 mm; AWB Hole made it 7 mm.", Assert.Single(match.Differences));
    }

    [Fact]
    public void ToleranceRemoved_IsReported()
    {
        var current = LimitsDowel with { ToleranceUpperCm = null, ToleranceLowerCm = null };

        var match = HoleStateComparison.Compare(LimitsDowel, current);

        Assert.Equal("The tolerance is none; AWB Hole set 0/-0.02 mm.", Assert.Single(match.Differences));
    }

    [Fact]
    public void ToleranceAddedToAPlainHole_IsReported()
    {
        var match = HoleStateComparison.Compare(Plain, Plain with { ToleranceIsoCode = "H7" });

        Assert.Equal("The tolerance is H7; AWB Hole set none.", Assert.Single(match.Differences));
    }

    [Fact]
    public void Colour_IsCompared()
    {
        Assert.Single(HoleStateComparison.Compare(IsoDowel, IsoDowel with { Coloured = false }).Differences);
        Assert.Single(HoleStateComparison.Compare(Plain, Plain with { Coloured = true }).Differences);
    }

    [Fact]
    public void DowelTurnedPlain_ReportsEveryDifference()
    {
        var match = HoleStateComparison.Compare(IsoDowel, Plain);

        Assert.Equal(3, match.Differences.Count);
    }

    [Fact]
    public void From_TakesTheGeometrysToleranceAndColour()
    {
        var geometry = new HoleGeometry(
            HoleKind.Dowel, HoleSeat.None, HoleTermination.ThroughAll, false, 0.7, null, null, null, null, null, null,
            null, new HoleTolerance("Test", "G7", 0.002, 0.0005), new HoleColour(1, 2, 3));

        Assert.Equal(IsoDowel, HoleAppliedState.From(geometry));
    }
}
