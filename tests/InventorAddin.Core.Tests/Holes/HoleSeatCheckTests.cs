using InventorAddin.Core.Holes;

namespace InventorAddin.Core.Tests.Holes;

public sealed class HoleSeatCheckTests
{
    private static readonly HoleGeometry Counterbored = new(
        HoleKind.Clearance, HoleSeat.Counterbore, HoleTermination.ThroughAll, false, 0.9, null, 1.5, 0.86, null, null,
        null, null, null, null);

    private static readonly HoleGeometry Countersunk = Counterbored with
    {
        Seat = HoleSeat.Countersink,
        CounterboreDiameterCm = null,
        CounterboreDepthCm = null,
        CountersinkDiameterCm = 1.68,
        CountersinkAngleDeg = 90,
    };

    private static readonly HoleGeometry Plain = Counterbored with
    {
        Seat = HoleSeat.None,
        CounterboreDiameterCm = null,
        CounterboreDepthCm = null,
    };

    private static InventorHoleFacts FactsOf(HoleGeometry g) => new(
        g.Seat, false, null, null, null, g.HoleDiameterCm, g.Termination, g.DepthCm,
        g.CounterboreDiameterCm, g.CounterboreDepthCm, g.CountersinkDiameterCm, g.CountersinkAngleDeg,
        false, null, null, null, null, g.Flip);

    [Fact]
    public void MatchingSeats_HaveNoDifferences()
    {
        Assert.Empty(HoleSeatCheck.Differences(Counterbored, FactsOf(Counterbored)));
        Assert.Empty(HoleSeatCheck.Differences(Countersunk, FactsOf(Countersunk)));
        Assert.Empty(HoleSeatCheck.Differences(Plain, FactsOf(Plain)));
    }

    [Fact]
    public void CounterboreLeftAtTheOldSize_IsReported()
    {
        // Task 093: the hole grew to 9 mm but the counterbore stayed at the 6.5 x 3.4 mm of the size before.
        var facts = FactsOf(Counterbored) with { CounterboreDiameterCm = 0.65, CounterboreDepthCm = 0.34 };

        var differences = HoleSeatCheck.Differences(Counterbored, facts);

        Assert.Equal(2, differences.Count);
        Assert.Contains("counterbore diameter is 6.5 mm; it should be 15 mm", differences[0]);
        Assert.Contains("counterbore depth is 3.4 mm; it should be 8.6 mm", differences[1]);
    }

    [Fact]
    public void CountersinkDiameterAndAngle_AreChecked()
    {
        var facts = FactsOf(Countersunk) with { CountersinkDiameterCm = 0.66, CountersinkAngleDeg = 82 };

        var differences = HoleSeatCheck.Differences(Countersunk, facts);

        Assert.Equal(2, differences.Count);
        Assert.Contains("countersink diameter is 6.6 mm; it should be 16.8 mm", differences[0]);
        Assert.Contains("countersink angle is 82 deg; it should be 90 deg", differences[1]);
    }

    [Fact]
    public void ValuesWithinTolerance_Match()
    {
        var facts = FactsOf(Counterbored) with { CounterboreDiameterCm = 1.5 + 1e-7 };
        Assert.Empty(HoleSeatCheck.Differences(Counterbored, facts));

        var sink = FactsOf(Countersunk) with { CountersinkAngleDeg = 90.001 };
        Assert.Empty(HoleSeatCheck.Differences(Countersunk, sink));
    }

    [Fact]
    public void WrongSeat_IsOneDifference()
    {
        var differences = HoleSeatCheck.Differences(Counterbored, FactsOf(Countersunk));

        Assert.Equal("the seat is a countersink; it should be a counterbore.", Assert.Single(differences));
    }

    [Fact]
    public void SpotFace_IsReportedAsAnotherSeat()
    {
        var facts = FactsOf(Plain) with { Seat = null };

        Assert.Contains("spot face", Assert.Single(HoleSeatCheck.Differences(Plain, facts)));
    }

    [Fact]
    public void UnreadableValue_IsReported()
    {
        var facts = FactsOf(Counterbored) with { CounterboreDepthCm = null };

        Assert.Equal(
            "the counterbore depth is unreadable; it should be 8.6 mm.",
            Assert.Single(HoleSeatCheck.Differences(Counterbored, facts)));
    }
}
