using InventorAddin.Core.Drawing;
using InventorAddin.Core.ViewModels;

namespace InventorAddin.Core.Tests.ViewModels;

public sealed class InitialViewsUnitsAndSheetTests
{
    private static readonly InitialViewsBoxCm Small = new(5, 3, 2);
    private static readonly InitialViewsBoxCm Large = new(120, 80, 60);
    private static readonly InitialViewsBoxCm Mid = new(20, 12, 8);

    private static InitialViewsViewModel Make(
        InitialViewsBoxCm box,
        PrintUnits units = PrintUnits.Metric,
        Func<PrintUnits, IReadOnlyDictionary<InitialViewKind, ViewSizeCm>?>? measure = null) =>
        new(InitialViewModelKind.Part, box, null, units, measure);

    [Fact]
    public void FixedSheetWindow_OffersNoChoice()
    {
        var vm = new InitialViewsViewModel(InitialViewModelKind.Part, Small, null, PrintTemplateLayout.IsoSheets.Single(s => s.Name == "A3"));
        Assert.False(vm.CanChooseSheet);
        Assert.False(vm.HasNextSheetOffer);
        Assert.Equal(PrintUnits.Metric, vm.ChosenUnits);
        Assert.Equal("A3", vm.ChosenSheet.Name);
    }

    [Theory]
    [InlineData(PrintUnits.Metric, "Metric")]
    [InlineData(PrintUnits.Imperial, "Imperial")]
    public void StartsOnThePartsUnits(PrintUnits units, string label)
    {
        var vm = Make(Small, units);
        Assert.True(vm.CanChooseSheet);
        Assert.Equal(label, vm.SelectedUnits!.Label);
        Assert.Equal(units, vm.ChosenUnits);
    }

    [Fact]
    public void SmallPart_StartsOnTheSmallestSheet_AndLargePartOnALargerOne()
    {
        Assert.Equal("A4", Make(Small).ChosenSheet.Name);
        Assert.Equal(new[] { "A4", "A3", "A2", "A1", "A0" }, Make(Small).SheetOptions.Select(o => o.Sheet.Name));
        Assert.NotEqual("A4", Make(Large).ChosenSheet.Name);
        Assert.True(Make(Large).Plan.Fits);
    }

    [Fact]
    public void SwitchingUnits_LoadsThatUnitsSheets_AndTemplate()
    {
        var vm = Make(Small);
        vm.SelectedUnits = vm.UnitsOptions.Single(u => u.Units == PrintUnits.Imperial);
        Assert.Equal(PrintUnits.Imperial, vm.ChosenUnits);
        Assert.Equal(new[] { "A", "B", "C", "D", "E" }, vm.SheetOptions.Select(o => o.Sheet.Name));
        Assert.StartsWith("AWB ANSI", vm.ChosenSheet.FormatName);
    }

    [Fact]
    public void Measuring_RunsOncePerUnits_WithThatUnits()
    {
        var asked = new List<PrintUnits>();
        var vm = Make(Small, PrintUnits.Metric, u => { asked.Add(u); return null; });
        Assert.Equal(new[] { PrintUnits.Metric }, asked);
        vm.SelectedUnits = vm.UnitsOptions.Single(u => u.Units == PrintUnits.Imperial);
        vm.SelectedUnits = vm.UnitsOptions.Single(u => u.Units == PrintUnits.Metric);
        vm.SelectedUnits = vm.UnitsOptions.Single(u => u.Units == PrintUnits.Imperial);
        Assert.Equal(new[] { PrintUnits.Metric, PrintUnits.Imperial }, asked);
    }

    [Fact]
    public void ChosenSheet_StopsFollowingTheViews()
    {
        var vm = Make(Small);
        vm.SelectedSheet = vm.SheetOptions.Single(o => o.Sheet.Name == "A2");
        vm.SelectedScale = vm.ScaleOptions.Single(s => s.Scale == 1.0);
        Assert.Equal("A2", vm.ChosenSheet.Name);
        Assert.Contains("A2", vm.SheetSizeText);
    }

    [Fact]
    public void ScaleThatDoesNotFit_OffersTheNextSheet_AndTakingItFits()
    {
        var vm = Make(Mid);
        vm.SelectedSheet = vm.SheetOptions.Single(o => o.Sheet.Name == "A3");
        vm.SelectedScale = vm.ScaleOptions.Single(s => s.Scale == 1.0);
        Assert.False(vm.Plan.Fits);
        Assert.True(vm.HasNextSheetOffer);
        Assert.Contains("fits", vm.NextSheetOffer);
        Assert.StartsWith("Use ", vm.NextSheetButtonText);

        vm.UseNextSheetCommand.Execute(null);
        Assert.True(vm.Plan.Fits);
        Assert.False(vm.HasNextSheetOffer);
        Assert.NotEqual("A3", vm.ChosenSheet.Name);
    }

    [Fact]
    public void AutomaticSheet_MovesToTheSmallestThatFits_WhenAFixedScaleIsChosen()
    {
        var vm = Make(Mid);
        Assert.True(vm.Plan.Fits);
        vm.SelectedScale = vm.ScaleOptions.Single(s => s.Scale == 1.0);
        Assert.True(vm.Plan.Fits);
        var sheets = vm.SheetOptions.Select(o => o.Sheet).ToList();
        int index = sheets.FindIndex(s => s.Name == vm.ChosenSheet.Name);
        Assert.True(index > 0);
        var smaller = InitialViewsPlan.Plan(InitialViewModelKind.Part, Mid, null, InitialViewsPlan.UsableArea(sheets[index - 1]), null, 1.0);
        Assert.False(smaller.Fits);
    }

    [Fact]
    public void Ok_HandsBackTheChosenSheet()
    {
        var vm = Make(Small, PrintUnits.Imperial);
        vm.OkCommand.Execute(null);
        Assert.NotNull(vm.Result);
        Assert.Equal(PrintUnits.Imperial, vm.ChosenUnits);
    }
}
