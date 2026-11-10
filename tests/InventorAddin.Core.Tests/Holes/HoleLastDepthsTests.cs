using InventorAddin.Core.Holes;
using InventorAddin.Core.Settings;

namespace InventorAddin.Core.Tests.Holes;

public sealed class HoleLastDepthsTests
{
    private static HoleSpec Blind(HoleUnits units, HoleKind kind, decimal? depth) =>
        HoleSpec.ClearanceHole(units, "T5") with
        {
            Kind = kind,
            Termination = HoleTermination.Distance,
            Depth = depth,
        };

    [Fact]
    public void For_GivesEachUnitsItsOwnDepth()
    {
        var depths = new HoleLastDepths(20m, 0.75m);

        Assert.Equal(20m, depths.For(HoleUnits.Metric));
        Assert.Equal(0.75m, depths.For(HoleUnits.Imperial));
        Assert.Null(HoleLastDepths.None.For(HoleUnits.Metric));
        Assert.Null(new HoleLastDepths(0m, -1m).For(HoleUnits.Imperial));
    }

    [Fact]
    public void From_ReadsTheSettingsFile()
    {
        var depths = HoleLastDepths.From(new AddinSettings { LastBlindDepthMm = 12m, LastBlindDepthIn = 0.5m });

        Assert.Equal(new HoleLastDepths(12m, 0.5m), depths);
        Assert.Equal(HoleLastDepths.None, HoleLastDepths.From(new AddinSettings()));
    }

    [Theory]
    [InlineData(HoleKind.Clearance)]
    [InlineData(HoleKind.Dowel)]
    public void Remember_KeepsABlindClearanceOrDowelDepth_ForItsUnits(HoleKind kind)
    {
        var settings = new AddinSettings { LastBlindDepthIn = 0.5m };

        var metric = HoleLastDepths.Remember(settings, Blind(HoleUnits.Metric, kind, 20m));
        var imperial = HoleLastDepths.Remember(settings, Blind(HoleUnits.Imperial, kind, 0.8m));

        Assert.Equal(settings with { LastBlindDepthMm = 20m }, metric);
        Assert.Equal(settings with { LastBlindDepthIn = 0.8m }, imperial);
    }

    [Fact]
    public void Remember_GivesNothing_ForATappedThroughOrDepthlessHole_OrTheSameDepth()
    {
        var settings = new AddinSettings { LastBlindDepthMm = 20m };

        Assert.Null(HoleLastDepths.Remember(settings, Blind(HoleUnits.Metric, HoleKind.Tapped, 30m) with { DrillDepth = 30m }));
        Assert.Null(HoleLastDepths.Remember(
            settings, Blind(HoleUnits.Metric, HoleKind.Clearance, 30m) with { Termination = HoleTermination.ThroughAll }));
        Assert.Null(HoleLastDepths.Remember(settings, Blind(HoleUnits.Metric, HoleKind.Clearance, null)));
        Assert.Null(HoleLastDepths.Remember(settings, Blind(HoleUnits.Metric, HoleKind.Clearance, 0m)));
        Assert.Null(HoleLastDepths.Remember(settings, Blind(HoleUnits.Metric, HoleKind.Clearance, 20.000m)));
    }

    [Fact]
    public void DepthToRemember_GivesTheUnitsAndDepth()
    {
        Assert.Equal((HoleUnits.Imperial, 0.6m), HoleLastDepths.DepthToRemember(Blind(HoleUnits.Imperial, HoleKind.Dowel, 0.6m)));
        Assert.Null(HoleLastDepths.DepthToRemember(Blind(HoleUnits.Metric, HoleKind.Tapped, 10m)));
    }
}
