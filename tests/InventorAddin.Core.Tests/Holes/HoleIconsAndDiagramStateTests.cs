using InventorAddin.Core.Holes;
using InventorAddin.Core.Settings;

namespace InventorAddin.Core.Tests.Holes;

/// <summary>Which icon each AWB Hole choice shows, and the diagram's remembered state (task 104).</summary>
public sealed class HoleIconsAndDiagramStateTests
{
    [Fact]
    public void Icons_AreAllDifferent()
    {
        Assert.Equal(HoleIcons.All.Count, HoleIcons.All.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(11, HoleIcons.All.Count);
    }

    [Theory]
    [InlineData("Clearance", HoleIcons.Clearance)]
    [InlineData("tapped", HoleIcons.Tapped)]
    [InlineData(null, HoleIcons.Clearance)]
    public void ForType(string? type, string expected) => Assert.Equal(expected, HoleIcons.ForType(type));

    [Fact]
    public void ForSeatAndTermination_EachValueHasItsOwnIcon()
    {
        Assert.Equal(HoleIcons.SeatNone, HoleIcons.ForSeat(HoleSeat.None));
        Assert.Equal(HoleIcons.Counterbore, HoleIcons.ForSeat(HoleSeat.Counterbore));
        Assert.Equal(HoleIcons.Countersink, HoleIcons.ForSeat(HoleSeat.Countersink));
        Assert.Equal(HoleIcons.Distance, HoleIcons.ForTermination(HoleTermination.Distance));
        Assert.Equal(HoleIcons.ThroughAll, HoleIcons.ForTermination(HoleTermination.ThroughAll));
        Assert.Equal(HoleIcons.ToFace, HoleIcons.ForTermination(HoleTermination.ToFace));
    }

    [Theory]
    [InlineData(null, HoleIcons.FlatBottom)]
    [InlineData(118.0, HoleIcons.PointShallow)]
    [InlineData(140.0, HoleIcons.PointShallow)]
    [InlineData(104.0, HoleIcons.PointShallow)]
    [InlineData(103.9, HoleIcons.PointSteep)]
    [InlineData(90.0, HoleIcons.PointSteep)]
    [InlineData(60.0, HoleIcons.PointSteep)]
    public void ForDrillPoint_ShallowFrom104Degrees(double? angle, string expected) =>
        Assert.Equal(expected, HoleIcons.ForDrillPoint(angle is { } a ? (decimal)a : null));

    [Fact]
    public void DiagramState_StartsExpanded()
    {
        Assert.True(new AddinSettings().HoleDiagramExpanded);
        Assert.True(HoleDiagramState.IsExpanded(new AddinSettings()));
    }

    [Fact]
    public void DiagramState_Remember_ReturnsTheChangedSettings_OrNullWhenUnchanged()
    {
        var settings = new AddinSettings { DefaultDesigner = "A. Person" };

        var collapsed = HoleDiagramState.Remember(settings, expanded: false);

        Assert.Equal(settings with { HoleDiagramExpanded = false }, collapsed);
        Assert.Null(HoleDiagramState.Remember(settings, expanded: true));
        Assert.Null(HoleDiagramState.Remember(collapsed!, expanded: false));
        Assert.Equal(settings, HoleDiagramState.Remember(collapsed!, expanded: true));
    }
}
