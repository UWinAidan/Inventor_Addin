using InventorAddin.Core.Settings;
using InventorAddin.Core.Theming;

namespace InventorAddin.Core.Tests.Theming;

public sealed class WindowFitRestoreTests
{
    private static readonly ScreenArea Main = new(0, 0, 1920, 1040);
    private static readonly ScreenArea Right = new(1920, 0, 1280, 984);

    [Fact]
    public void Restore_KeepsAPlacementThatFits()
    {
        var saved = new WindowPlacement(100, 80, 640, 700, Maximized: false);

        Assert.Equal(saved, WindowFit.Restore(saved, new[] { Main }, 460, 320));
    }

    [Fact]
    public void Restore_KeepsMaximized()
    {
        var saved = new WindowPlacement(100, 80, 640, 700, Maximized: true);

        Assert.True(WindowFit.Restore(saved, new[] { Main }, 460, 320)!.Maximized);
    }

    [Fact]
    public void Restore_ShrinksAWindowTallerThanTheWorkingArea_AndMovesItInside()
    {
        var saved = new WindowPlacement(100, 300, 640, 1200, Maximized: false);

        Assert.Equal(new WindowPlacement(100, 0, 640, 1040, false), WindowFit.Restore(saved, new[] { Main }, 460, 320));
    }

    [Fact]
    public void Restore_PullsAWindowHangingOffTheEdgeBackOnScreen()
    {
        var saved = new WindowPlacement(1700, 900, 640, 400, Maximized: false);

        Assert.Equal(new WindowPlacement(1280, 640, 640, 400, false), WindowFit.Restore(saved, new[] { Main }, 460, 320));
    }

    [Fact]
    public void Restore_UsesTheScreenTheWindowOverlapsMost()
    {
        var saved = new WindowPlacement(2000, 100, 640, 1000, Maximized: false);

        Assert.Equal(new WindowPlacement(2000, 0, 640, 984, false), WindowFit.Restore(saved, new[] { Main, Right }, 460, 320));
    }

    [Fact]
    public void Restore_EnforcesTheMinimumSize()
    {
        var saved = new WindowPlacement(100, 100, 50, 40, Maximized: false);

        Assert.Equal(new WindowPlacement(100, 100, 460, 320, false), WindowFit.Restore(saved, new[] { Main }, 460, 320));
    }

    [Fact]
    public void Restore_IsNull_WhenTheWindowWasOnAScreenThatIsGone()
    {
        var saved = new WindowPlacement(2000, 100, 640, 700, Maximized: false);

        Assert.Null(WindowFit.Restore(saved, new[] { Main }, 460, 320));
    }

    [Theory]
    [InlineData(double.NaN, 0, 640, 700)]
    [InlineData(0, double.PositiveInfinity, 640, 700)]
    [InlineData(0, 0, 0, 700)]
    [InlineData(0, 0, 640, -1)]
    public void Restore_IsNull_ForAnUnusablePlacement(double left, double top, double width, double height)
    {
        Assert.Null(WindowFit.Restore(new WindowPlacement(left, top, width, height, false), new[] { Main }, 460, 320));
    }

    [Fact]
    public void Restore_IsNull_WithNothingSaved()
    {
        Assert.Null(WindowFit.Restore(null, new[] { Main }, 460, 320));
    }
}
