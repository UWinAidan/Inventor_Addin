using InventorAddin.Core.Theming;

namespace InventorAddin.Core.Tests.Theming;

public sealed class WindowFitTests
{
    [Theory]
    [InlineData(1040)]
    [InlineData(600.5)]
    public void MaxHeight_IsTheWorkingAreaHeight(double height)
    {
        Assert.Equal(height, WindowFit.MaxHeight(height));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void MaxHeight_IsNull_WhenTheScreenCouldNotBeRead(double height)
    {
        Assert.Null(WindowFit.MaxHeight(height));
    }

    [Fact]
    public void ClampTop_LeavesAWindowThatFitsAlone()
    {
        Assert.Null(WindowFit.ClampTop(top: 100, height: 500, workAreaTop: 0, workAreaBottom: 1040));
    }

    [Fact]
    public void ClampTop_MovesAWindowUp_WhenItsBottomIsBelowTheWorkingArea()
    {
        Assert.Equal(540, WindowFit.ClampTop(top: 700, height: 500, workAreaTop: 0, workAreaBottom: 1040));
    }

    [Fact]
    public void ClampTop_MovesAWindowDown_WhenItsTopIsAboveTheWorkingArea()
    {
        Assert.Equal(0, WindowFit.ClampTop(top: -50, height: 500, workAreaTop: 0, workAreaBottom: 1040));
    }

    [Fact]
    public void ClampTop_KeepsTheTitleBarOnScreen_WhenTheWindowIsTallerThanTheWorkingArea()
    {
        Assert.Equal(0, WindowFit.ClampTop(top: 200, height: 1200, workAreaTop: 0, workAreaBottom: 1040));
    }

    [Fact]
    public void ClampTop_WorksOnASecondScreenWithAnOffsetWorkingArea()
    {
        Assert.Equal(-200, WindowFit.ClampTop(top: -400, height: 300, workAreaTop: -200, workAreaBottom: 880));
        Assert.Equal(580, WindowFit.ClampTop(top: 700, height: 300, workAreaTop: -200, workAreaBottom: 880));
    }

    [Theory]
    [InlineData(double.NaN, 500, 0, 1040)]
    [InlineData(100, double.NaN, 0, 1040)]
    [InlineData(100, 500, double.NaN, 1040)]
    [InlineData(100, 500, 0, double.PositiveInfinity)]
    [InlineData(100, -1, 0, 1040)]
    [InlineData(100, 500, 1040, 0)]
    public void ClampTop_IsNull_ForUnusableValues(double top, double height, double workAreaTop, double workAreaBottom)
    {
        Assert.Null(WindowFit.ClampTop(top, height, workAreaTop, workAreaBottom));
    }
}
