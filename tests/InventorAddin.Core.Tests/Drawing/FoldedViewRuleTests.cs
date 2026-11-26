using InventorAddin.Core.Drawing;

namespace InventorAddin.Core.Tests.Drawing;

public class FoldedViewRuleTests
{
    private static readonly FaceEdgeCm[] PlateEdges = { new(1, 0, 0, 20), new(0, 1, 0, 10) };

    private static (double, double, double) Eye(FaceViewOrientation o) => (o.NormalX, o.NormalY, o.NormalZ);

    [Fact]
    public void OneBend_LooksAlongTheAxis_BaseNormalIsUp()
    {
        FaceViewOrientation o = FoldedViewRule.Choose(0, 1, 0, new[] { new BendAxisCm(0, 0, 1, 5) }, PlateEdges)!;
        Assert.Equal((0.0, 0.0, 1.0), Eye(o));
        Assert.Equal((0.0, 1.0, 0.0), (o.UpX, o.UpY, o.UpZ));
    }

    [Fact]
    public void TwoParallelBendsOfEitherSign_GiveTheSameView()
    {
        FaceViewOrientation a = FoldedViewRule.Choose(0, 1, 0, new[] { new BendAxisCm(0, 0, 1, 5), new BendAxisCm(0, 0, -1, 5) }, PlateEdges)!;
        FaceViewOrientation b = FoldedViewRule.Choose(0, 1, 0, new[] { new BendAxisCm(0, 0, -1, 5) }, PlateEdges)!;
        Assert.Equal(a, b);
        Assert.Equal(1.0, a.NormalZ);
    }

    [Fact]
    public void LevelWithZ_EyeGoesTowardMinusY()
    {
        FaceViewOrientation o = FoldedViewRule.Choose(0, 0, 1, new[] { new BendAxisCm(0, 1, 0, 5) }, PlateEdges)!;
        Assert.Equal((0.0, -1.0, 0.0), Eye(o));
    }

    [Fact]
    public void LevelWithZAndY_EyeGoesTowardPlusX()
    {
        FaceViewOrientation o = FoldedViewRule.Choose(0, 0, 1, new[] { new BendAxisCm(-1, 0, 0, 5) }, PlateEdges)!;
        Assert.Equal((1.0, 0.0, 0.0), Eye(o));
    }

    [Fact]
    public void UpDropsAnyPartAlongTheView()
    {
        FaceViewOrientation o = FoldedViewRule.Choose(0, 1, 1, new[] { new BendAxisCm(0, 0, 1, 5) }, PlateEdges)!;
        Assert.Equal(0.0, Math.Round(o.UpX, 9));
        Assert.Equal(1.0, Math.Round(o.UpY, 9));
        Assert.Equal(0.0, Math.Round(o.UpZ, 9));
    }

    [Fact]
    public void BendsInTwoDirections_TheDirectionWithMoreBendsWins()
    {
        var bends = new[] { new BendAxisCm(1, 0, 0, 100), new BendAxisCm(0, 0, 1, 2), new BendAxisCm(0, 0, -1, 2) };
        FaceViewOrientation o = FoldedViewRule.Choose(0, 1, 0, bends, PlateEdges)!;
        Assert.Equal((0.0, 0.0, 1.0), Eye(o));
    }

    [Fact]
    public void BendsInTwoDirections_ATieGoesToTheLongestTotalLength()
    {
        var bends = new[] { new BendAxisCm(1, 0, 0, 3), new BendAxisCm(0, 0, 1, 9) };
        Assert.Equal((0.0, 0.0, 1.0), Eye(FoldedViewRule.Choose(0, 1, 0, bends, PlateEdges)!));

        var swapped = new[] { new BendAxisCm(0, 0, 1, 3), new BendAxisCm(1, 0, 0, 9) };
        Assert.Equal((1.0, 0.0, 0.0), Eye(FoldedViewRule.Choose(0, 1, 0, swapped, PlateEdges)!));
    }

    [Fact]
    public void NoBends_UsesTheBaseFaceRule()
    {
        FaceViewOrientation expected = FaceViewRule.Choose(0, 0, 1, PlateEdges)!;
        Assert.Equal(expected, FoldedViewRule.Choose(0, 0, 1, Array.Empty<BendAxisCm>(), PlateEdges));
    }

    [Fact]
    public void AxisParallelToTheNormal_IsIgnored_AndFallsBack()
    {
        FaceViewOrientation expected = FaceViewRule.Choose(0, 0, 1, PlateEdges)!;
        Assert.Equal(expected, FoldedViewRule.Choose(0, 0, 1, new[] { new BendAxisCm(0, 0, -1, 5) }, PlateEdges));
    }

    [Fact]
    public void ZeroNormal_GivesNull()
    {
        Assert.Null(FoldedViewRule.Choose(0, 0, 0, new[] { new BendAxisCm(0, 0, 1, 5) }, PlateEdges));
    }
}
