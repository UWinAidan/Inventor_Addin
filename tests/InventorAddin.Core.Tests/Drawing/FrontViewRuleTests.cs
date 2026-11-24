using InventorAddin.Core.Drawing;

namespace InventorAddin.Core.Tests.Drawing;

public sealed class FrontViewRuleTests
{
    private static void AssertOrientation(FaceViewOrientation expected, FaceViewOrientation actual)
    {
        Assert.Equal(expected.NormalX, actual.NormalX, 9);
        Assert.Equal(expected.NormalY, actual.NormalY, 9);
        Assert.Equal(expected.NormalZ, actual.NormalZ, 9);
        Assert.Equal(expected.UpX, actual.UpX, 9);
        Assert.Equal(expected.UpY, actual.UpY, 9);
        Assert.Equal(expected.UpZ, actual.UpZ, 9);
    }

    [Fact]
    public void Auto_FlatInXY_LooksAlongZ_LongerSideHorizontal()
    {
        FaceViewOrientation o = FrontViewRule.Auto(new InitialViewsBoxCm(10, 4, 2));
        AssertOrientation(FrontViewRule.FrontOrientation, o);
    }

    [Fact]
    public void Auto_FlatInXY_WithTallerThanWide_TurnsTheViewSoTheLongSideIsHorizontal()
    {
        var extents = new InitialViewsBoxCm(4, 10, 2);
        FaceViewOrientation o = FrontViewRule.Auto(extents);
        AssertOrientation(new FaceViewOrientation(0, 0, 1, 1, 0, 0), o);

        InitialViewsBoxCm box = FrontViewRule.BoxFor(o, extents);
        Assert.Equal(10, box.WidthCm, 9);
        Assert.Equal(4, box.HeightCm, 9);
        Assert.Equal(2, box.DepthCm, 9);
    }

    // The assembly of the chunk 2 check: it was edge-on from the standard front.
    [Fact]
    public void Auto_Assembly_LooksAlongTheShortestSide_LongestHorizontal()
    {
        var extents = new InitialViewsBoxCm(66.0, 37.9, 172.7);
        FaceViewOrientation o = FrontViewRule.Auto(extents);
        AssertOrientation(new FaceViewOrientation(0, 1, 0, 1, 0, 0), o);

        InitialViewsBoxCm box = FrontViewRule.BoxFor(o, extents);
        Assert.Equal(172.7, box.WidthCm, 9);
        Assert.Equal(66.0, box.HeightCm, 9);
        Assert.Equal(37.9, box.DepthCm, 9);
    }

    [Fact]
    public void Auto_TopAreaLargest_WithXLonger_IsTheStandardTop()
    {
        FaceViewOrientation o = FrontViewRule.Auto(new InitialViewsBoxCm(20, 2, 8));
        AssertOrientation(FrontViewRule.TopOrientation, o);
    }

    [Fact]
    public void Auto_SideAreaLargest_WithZLonger_IsTheStandardRight()
    {
        FaceViewOrientation o = FrontViewRule.Auto(new InitialViewsBoxCm(2, 6, 20));
        AssertOrientation(FrontViewRule.RightOrientation, o);
    }

    [Fact]
    public void Auto_SideAreaLargest_WithYLonger_UsesZUp()
    {
        FaceViewOrientation o = FrontViewRule.Auto(new InitialViewsBoxCm(2, 20, 6));
        AssertOrientation(new FaceViewOrientation(1, 0, 0, 0, 0, 1), o);
    }

    [Theory]
    [InlineData(5, 5, 5)]
    [InlineData(10, 10, 4)]
    [InlineData(10, 4, 10)]
    public void Auto_Ties_PreferTheStandardFrontThenTop(double x, double y, double z)
    {
        FaceViewOrientation o = FrontViewRule.Auto(new InitialViewsBoxCm(x, y, z));
        if (x * y >= x * z && x * y >= y * z)
            AssertOrientation(FrontViewRule.FrontOrientation, o);
        else
            AssertOrientation(FrontViewRule.TopOrientation, o);
    }

    [Fact]
    public void Auto_ChosenArea_IsTheLargestOfTheThree()
    {
        foreach ((double x, double y, double z) in new[] { (3.0, 9.0, 5.0), (9.0, 3.0, 5.0), (5.0, 3.0, 9.0), (1.0, 2.0, 3.0), (30.0, 2.0, 7.0) })
        {
            var extents = new InitialViewsBoxCm(x, y, z);
            InitialViewsBoxCm box = FrontViewRule.BoxFor(FrontViewRule.Auto(extents), extents);
            double largest = Math.Max(x * y, Math.Max(x * z, y * z));
            Assert.Equal(largest, box.WidthCm * box.HeightCm, 9);
            Assert.True(box.WidthCm >= box.HeightCm);
        }
    }

    [Theory]
    [InlineData(FrontViewChoice.Front, 10, 4, 2)]
    [InlineData(FrontViewChoice.Back, 10, 4, 2)]
    [InlineData(FrontViewChoice.Top, 10, 2, 4)]
    [InlineData(FrontViewChoice.Bottom, 10, 2, 4)]
    [InlineData(FrontViewChoice.Left, 2, 4, 10)]
    [InlineData(FrontViewChoice.Right, 2, 4, 10)]
    public void NamedChoices_BoxFollowsTheDirection(FrontViewChoice choice, double width, double height, double depth)
    {
        var extents = new InitialViewsBoxCm(10, 4, 2);
        FrontViewSelection selection = FrontViewRule.Select(choice, InitialViewModelKind.Part, extents);
        Assert.NotNull(selection.Orientation);
        Assert.Equal(width, selection.Box.WidthCm, 9);
        Assert.Equal(height, selection.Box.HeightCm, 9);
        Assert.Equal(depth, selection.Box.DepthCm, 9);
    }

    [Fact]
    public void NamedChoices_AreThePartsStandardViews()
    {
        AssertOrientation(new FaceViewOrientation(0, 0, 1, 0, 1, 0), FrontViewRule.Named(FrontViewChoice.Front));
        AssertOrientation(new FaceViewOrientation(0, 0, -1, 0, 1, 0), FrontViewRule.Named(FrontViewChoice.Back));
        AssertOrientation(new FaceViewOrientation(0, 1, 0, 0, 0, -1), FrontViewRule.Named(FrontViewChoice.Top));
        AssertOrientation(new FaceViewOrientation(0, -1, 0, 0, 0, 1), FrontViewRule.Named(FrontViewChoice.Bottom));
        AssertOrientation(new FaceViewOrientation(-1, 0, 0, 0, 1, 0), FrontViewRule.Named(FrontViewChoice.Left));
        AssertOrientation(new FaceViewOrientation(1, 0, 0, 0, 1, 0), FrontViewRule.Named(FrontViewChoice.Right));
        Assert.Throws<ArgumentOutOfRangeException>(() => FrontViewRule.Named(FrontViewChoice.Auto));
    }

    [Fact]
    public void OnlyTheFrontOrientation_IsTheStandardFront()
    {
        Assert.True(FrontViewRule.IsStandardFront(FrontViewRule.FrontOrientation));
        Assert.False(FrontViewRule.IsStandardFront(FrontViewRule.BackOrientation));
        Assert.False(FrontViewRule.IsStandardFront(FrontViewRule.TopOrientation));
    }

    [Fact]
    public void SheetMetal_OnAuto_UsesTheBaseFace_AndKeepsTheBox()
    {
        var extents = new InitialViewsBoxCm(30, 2, 20);
        FrontViewSelection selection = FrontViewRule.Select(FrontViewChoice.Auto, InitialViewModelKind.SheetMetalPart, extents);
        Assert.Null(selection.Orientation);
        Assert.Equal(extents, selection.Box);
    }

    [Fact]
    public void SheetMetal_OnAChosenDirection_LooksThatWay()
    {
        var extents = new InitialViewsBoxCm(30, 2, 20);
        FrontViewSelection selection = FrontViewRule.Select(FrontViewChoice.Top, InitialViewModelKind.SheetMetalPart, extents);
        AssertOrientation(FrontViewRule.TopOrientation, selection.Orientation!);
        Assert.Equal(30, selection.Box.WidthCm, 9);
        Assert.Equal(20, selection.Box.HeightCm, 9);
    }

    [Fact]
    public void PartAndAssembly_OnAuto_AlwaysHaveAnOrientation()
    {
        var extents = new InitialViewsBoxCm(30, 2, 20);
        Assert.NotNull(FrontViewRule.Select(FrontViewChoice.Auto, InitialViewModelKind.Part, extents).Orientation);
        Assert.NotNull(FrontViewRule.Select(FrontViewChoice.Auto, InitialViewModelKind.Assembly, extents).Orientation);
    }
}
