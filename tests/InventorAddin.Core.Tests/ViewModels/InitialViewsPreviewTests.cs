using InventorAddin.Core.Drawing;
using InventorAddin.Core.ViewModels;

namespace InventorAddin.Core.Tests.ViewModels;

public sealed class InitialViewsPreviewTests
{
    private static readonly SheetSizeSpec A3 = PrintTemplateLayout.IsoSheets.Single(s => s.Name == "A3");

    private static InitialViewsViewModel Fixed() =>
        new(InitialViewModelKind.Part, new InitialViewsBoxCm(10, 4, 2), null, A3);

    [Fact]
    public void Preview_CarriesTheSheetBorderTitleBlockAndUsableArea()
    {
        InitialViewsSheetPreview p = Fixed().Preview;

        Assert.Equal(A3.WidthMm, p.SheetWidthMm, 9);
        Assert.Equal(A3.HeightMm, p.SheetHeightMm, 9);
        Assert.Equal(A3.BorderLeftMm, p.Border.LeftMm, 9);
        Assert.Equal(A3.BorderTopMm, p.Border.TopMm, 9);
        // The title block fills the border's bottom-right corner; the usable area sits on top of it.
        Assert.Equal(p.Border.RightMm, p.TitleBlock.RightMm, 9);
        Assert.Equal(p.Border.BottomMm, p.TitleBlock.BottomMm, 9);
        Assert.Equal(PrintTemplateLayout.TitleBlockWidthMm, p.TitleBlock.WidthMm, 9);
        Assert.Equal(PrintTemplateLayout.TitleBlockHeightMm, p.TitleBlock.HeightMm, 9);
        Assert.Equal(p.TitleBlock.TopMm, p.UsableArea.BottomMm, 9);
        Assert.Equal(p.Border.TopMm, p.UsableArea.TopMm, 9);
    }

    [Fact]
    public void Preview_ViewRectanglesAreThePlansInMillimetres()
    {
        var vm = Fixed();
        InitialViewsSheetPreview p = vm.Preview;

        Assert.Equal(vm.Plan.Views.Count, p.Views.Count);
        for (int i = 0; i < p.Views.Count; i++)
        {
            PlannedView v = vm.Plan.Views[i];
            PreviewRectMm r = p.Views[i].Rect;
            Assert.Equal(v.WidthCm * 10, r.WidthMm, 9);
            Assert.Equal(v.HeightCm * 10, r.HeightMm, 9);
            Assert.Equal(v.CentreXCm * 10, (r.LeftMm + r.RightMm) / 2, 9);
            Assert.Equal(v.CentreYCm * 10, (r.BottomMm + r.TopMm) / 2, 9);
            Assert.True(r.LeftMm >= p.UsableArea.LeftMm - 1e-6 && r.RightMm <= p.UsableArea.RightMm + 1e-6);
            Assert.True(r.BottomMm >= p.UsableArea.BottomMm - 1e-6 && r.TopMm <= p.UsableArea.TopMm + 1e-6);
        }
    }

    [Fact]
    public void Preview_ChangesWhenAViewIsUnticked_AndRaisesPreview()
    {
        var vm = Fixed();
        var raised = new List<string?>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);
        int before = vm.Preview.Views.Count;

        vm.Views.Single(v => v.Kind == InitialViewKind.Iso).IsChecked = false;

        Assert.Equal(before - 1, vm.Preview.Views.Count);
        Assert.DoesNotContain(vm.Preview.Views, v => v.Kind == InitialViewKind.Iso);
        Assert.Contains("Preview", raised);
    }

    [Fact]
    public void Preview_ChangesWithTheScale()
    {
        var vm = Fixed();
        var raised = new List<string?>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        vm.SelectedScale = vm.ScaleOptions.Single(s => s.Scale == 0.1);

        Assert.Contains("Preview", raised);
        Assert.Equal("1:10", vm.Preview.ScaleText);
        Assert.Equal(10, vm.Preview.Views.Single(v => v.Kind == InitialViewKind.Front).Rect.WidthMm, 6);
    }

    [Fact]
    public void Preview_FollowsTheFrontViewChoice()
    {
        var vm = Fixed();
        vm.SelectedFront = vm.FrontOptions.Single(o => o.Choice == FrontViewChoice.Right);

        PreviewView front = vm.Preview.Views.Single(v => v.Kind == InitialViewKind.Front);
        Assert.Equal("Right", vm.Preview.FrontText);
        Assert.Equal("Front: Right", front.Label);
        Assert.Equal(2 * vm.Plan.Scale * 10, front.Rect.WidthMm, 6);
    }

    [Fact]
    public void Preview_FollowsTheUnitsAndSheet()
    {
        var vm = new InitialViewsViewModel(
            InitialViewModelKind.Part, new InitialViewsBoxCm(10, 4, 2), null, PrintUnits.Metric,
            (Func<PrintUnits, IReadOnlyDictionary<InitialViewKind, ViewSizeCm>?>?)null);
        vm.SelectedSheet = vm.SheetOptions.Single(s => s.Sheet.Name == "A2");
        Assert.Equal(vm.ChosenSheet.WidthMm, vm.Preview.SheetWidthMm, 9);

        vm.SelectedUnits = vm.UnitsOptions.Single(u => u.Units == PrintUnits.Imperial);
        Assert.Equal(vm.ChosenSheet.WidthMm, vm.Preview.SheetWidthMm, 9);
        Assert.Equal(vm.ChosenSheet.BorderLeftMm, vm.Preview.Border.LeftMm, 9);
    }

    [Fact]
    public void Preview_ReportsWhenTheViewsDoNotFit()
    {
        var vm = Fixed();
        vm.SelectedScale = vm.ScaleOptions.Single(s => s.Scale == 1.0);
        Assert.Equal(vm.Plan.Fits, vm.Preview.Fits);

        var big = new InitialViewsViewModel(InitialViewModelKind.Part, new InitialViewsBoxCm(500, 400, 300), null, A3);
        Assert.False(big.Preview.Fits);
    }
}
