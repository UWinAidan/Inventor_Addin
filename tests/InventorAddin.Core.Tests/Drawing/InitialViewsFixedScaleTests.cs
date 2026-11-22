using InventorAddin.Core.Drawing;

namespace InventorAddin.Core.Tests.Drawing;

public sealed class InitialViewsFixedScaleTests
{
    private static readonly SheetSizeSpec A3 = PrintTemplateLayout.IsoSheets.Single(s => s.Name == "A3");
    private static readonly InitialViewsBoxCm Box = new(10, 6, 4);

    [Fact]
    public void FixedScale_IsUsedAsGiven_AndFitsWhenSmallEnough()
    {
        SheetAreaCm area = InitialViewsPlan.UsableArea(A3);
        InitialViewsResult r = InitialViewsPlan.Plan(InitialViewModelKind.Part, Box, null, area, null, 0.5);
        Assert.Equal(0.5, r.Scale);
        Assert.True(r.Fits);
        Assert.Empty(r.Messages);
        Assert.Equal(5.0, r.Views.Single(v => v.Kind == InitialViewKind.Front).WidthCm, 9);
    }

    [Fact]
    public void FixedScale_TooLarge_DoesNotFitAndSaysSo()
    {
        SheetAreaCm area = InitialViewsPlan.UsableArea(A3);
        InitialViewsResult r = InitialViewsPlan.Plan(InitialViewModelKind.Part, Box, null, area, null, 10.0);
        Assert.Equal(10.0, r.Scale);
        Assert.False(r.Fits);
        Assert.Contains(InitialViewsPlan.DoesNotFitAtScaleMessage, r.Messages);
        Assert.Equal(4, r.Views.Count);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    public void FixedScale_NotPositive_Throws(double scale)
    {
        SheetAreaCm area = InitialViewsPlan.UsableArea(A3);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            InitialViewsPlan.Plan(InitialViewModelKind.Part, Box, null, area, null, scale));
    }
}
