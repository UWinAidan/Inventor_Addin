using InventorAddin.Core.Drawing;

namespace InventorAddin.Core.Tests.Drawing;

public class OutOfBoundsCheckTests
{
    private static readonly SheetRect Border = new(10, 10, 410, 287);
    private static readonly SheetRect Title = new(230, 10, 410, 72);

    private static IReadOnlyList<BoundsFinding> Run(SheetRect r, double tol = OutOfBoundsCheck.DefaultToleranceMm) =>
        OutOfBoundsCheck.Check(Border, Title, new[] { new BoundsItem("V", r) }, tol);

    [Fact]
    public void Inside_NotReported() => Assert.Empty(Run(new SheetRect(50, 100, 200, 250)));

    [Fact]
    public void TouchingBorder_WithinTolerance_NotReported() =>
        Assert.Empty(Run(new SheetRect(9.6, 100, 200, 287.4)));

    [Fact]
    public void TouchingTitleBlock_WithinTolerance_NotReported() =>
        Assert.Empty(Run(new SheetRect(250, 72.3, 300, 150)));

    [Fact]
    public void ThinItemJustInsideBorder_NotReported() =>
        Assert.Empty(Run(new SheetRect(50, 10.2, 200, 10.2)));

    [Fact]
    public void ItemStraddlingBorderLineWithinTolerance_NotReported() =>
        Assert.Empty(Run(new SheetRect(9.8, 100, 10.2, 120)));

    [Fact]
    public void ZeroThicknessLineAcrossTitleBlock_Reported()
    {
        var f = Assert.Single(Run(new SheetRect(250, 40, 350, 40)));
        Assert.Equal(BoundsProblem.OverTitleBlock, f.Problem);
    }

    [Fact]
    public void PastRightEdge_ReportsEdge()
    {
        var f = Assert.Single(Run(new SheetRect(300, 100, 420, 200)));
        Assert.Equal(BoundsProblem.PartlyOutside, f.Problem);
        Assert.Equal(BorderEdges.Right, f.Edges);
        Assert.Contains("right", f.Message);
    }

    [Fact]
    public void PastCorner_ReportsBothEdges()
    {
        var f = Assert.Single(Run(new SheetRect(0, 100, 100, 300)));
        Assert.Equal(BorderEdges.Left | BorderEdges.Top, f.Edges);
        Assert.Contains("left and top", f.Message);
    }

    [Fact]
    public void WhollyOutside_Reported()
    {
        var f = Assert.Single(Run(new SheetRect(500, 100, 600, 200)));
        Assert.Equal(BoundsProblem.Outside, f.Problem);
        Assert.Equal(BorderEdges.None, f.Edges);
    }

    [Fact]
    public void OverTitleBlock_Reported()
    {
        var f = Assert.Single(Run(new SheetRect(300, 50, 350, 120)));
        Assert.Equal(BoundsProblem.OverTitleBlock, f.Problem);
        Assert.True(f.OverTitleBlock);
    }

    [Fact]
    public void PartlyOutsideAndOverTitleBlock_BothFlagged()
    {
        var f = Assert.Single(Run(new SheetRect(300, 50, 420, 120)));
        Assert.Equal(BoundsProblem.PartlyOutside, f.Problem);
        Assert.True(f.OverTitleBlock);
    }

    [Fact]
    public void NoTitleBlock_SkipsThatCheck() =>
        Assert.Empty(OutOfBoundsCheck.Check(Border, null, new[] { new BoundsItem("V", new SheetRect(300, 50, 350, 120)) }));

    [Fact]
    public void ZeroTolerance_ReportsSmallOvershoot() =>
        Assert.Equal(BorderEdges.Left, Assert.Single(Run(new SheetRect(9.6, 100, 200, 200), 0)).Edges);

    [Fact]
    public void OnlyFailingItemsReported()
    {
        var res = OutOfBoundsCheck.Check(Border, Title, new[]
        {
            new BoundsItem("A", new SheetRect(50, 100, 100, 150)),
            new BoundsItem("B", new SheetRect(0, 100, 100, 150)),
        });
        Assert.Equal("B", Assert.Single(res).Name);
    }
}
