using InventorAddin.Core.Drawing;
using InventorAddin.Core.ViewModels;

namespace InventorAddin.Core.Tests.ViewModels;

public sealed class InitialViewsFrontViewTests
{
    private static readonly SheetSizeSpec A3 = PrintTemplateLayout.IsoSheets.Single(s => s.Name == "A3");

    [Fact]
    public void OffersTheSevenChoices_StartingOnAuto()
    {
        var vm = new InitialViewsViewModel(InitialViewModelKind.Part, new InitialViewsBoxCm(10, 4, 2), null, A3);
        Assert.Equal(
            new[] { "Auto", "Front", "Back", "Top", "Bottom", "Left", "Right" },
            vm.FrontOptions.Select(o => o.Label).ToArray());
        Assert.Equal(FrontViewChoice.Auto, vm.SelectedFront.Choice);
    }

    [Fact]
    public void Auto_PlansTheViewsFromTheLongestAreaDirection()
    {
        // Edge-on from the standard front (X 6.6, Y 3.8, Z 17.3): Auto looks along Y, so the front view is 17.3 wide.
        var vm = new InitialViewsViewModel(InitialViewModelKind.Assembly, new InitialViewsBoxCm(6.6, 3.8, 17.3), null, A3);
        PlannedView front = vm.Plan.Views.Single(v => v.Kind == InitialViewKind.Front);
        Assert.Equal(17.3 * vm.Plan.Scale, front.WidthCm, 9);
        Assert.Equal(6.6 * vm.Plan.Scale, front.HeightCm, 9);
        Assert.NotNull(vm.Plan.FrontOrientation);
        Assert.Equal(1, vm.Plan.FrontOrientation!.NormalY, 9);
    }

    [Fact]
    public void ChangingTheFrontView_ReplansWithTheNewDirection()
    {
        var vm = new InitialViewsViewModel(InitialViewModelKind.Part, new InitialViewsBoxCm(10, 4, 2), null, A3);
        vm.SelectedFront = vm.FrontOptions.Single(o => o.Choice == FrontViewChoice.Right);

        PlannedView front = vm.Plan.Views.Single(v => v.Kind == InitialViewKind.Front);
        Assert.Equal(2 * vm.Plan.Scale, front.WidthCm, 9);
        Assert.Equal(4 * vm.Plan.Scale, front.HeightCm, 9);
        Assert.Equal(1, vm.Plan.FrontOrientation!.NormalX, 9);
    }

    [Fact]
    public void ChangingTheFrontView_RaisesPlan()
    {
        var vm = new InitialViewsViewModel(InitialViewModelKind.Part, new InitialViewsBoxCm(10, 4, 2), null, A3);
        var raised = new List<string?>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);
        vm.SelectedFront = vm.FrontOptions.Single(o => o.Choice == FrontViewChoice.Bottom);
        Assert.Contains(nameof(InitialViewsViewModel.Plan), raised);
    }

    [Fact]
    public void SheetMetal_OnAuto_HasNoOrientation_AndOnAChoice_Has()
    {
        var vm = new InitialViewsViewModel(InitialViewModelKind.SheetMetalPart, new InitialViewsBoxCm(10, 4, 2), new FlatPatternSizeCm(12, 8), A3);
        Assert.Null(vm.Plan.FrontOrientation);
        vm.SelectedFront = vm.FrontOptions.Single(o => o.Choice == FrontViewChoice.Front);
        Assert.NotNull(vm.Plan.FrontOrientation);
        vm.SelectedFront = vm.FrontOptions.Single(o => o.Choice == FrontViewChoice.Auto);
        Assert.Null(vm.Plan.FrontOrientation);
    }

    [Fact]
    public void MeasuringFollowsTheChoice_AndIsDoneOncePerChoiceAndUnits()
    {
        var calls = new List<(PrintUnits, FrontViewChoice)>();
        var vm = new InitialViewsViewModel(InitialViewModelKind.Part, new InitialViewsBoxCm(10, 4, 2), null, PrintUnits.Metric,
            (units, front) =>
            {
                calls.Add((units, front));
                return null;
            });
        Assert.Equal(new[] { (PrintUnits.Metric, FrontViewChoice.Auto) }, calls);

        vm.SelectedFront = vm.FrontOptions.Single(o => o.Choice == FrontViewChoice.Top);
        vm.SelectedFront = vm.FrontOptions.Single(o => o.Choice == FrontViewChoice.Auto);
        vm.SelectedFront = vm.FrontOptions.Single(o => o.Choice == FrontViewChoice.Top);
        Assert.Equal(new[] { (PrintUnits.Metric, FrontViewChoice.Auto), (PrintUnits.Metric, FrontViewChoice.Top) }, calls);

        vm.SelectedUnits = vm.UnitsOptions.Single(u => u.Units == PrintUnits.Imperial);
        Assert.Equal((PrintUnits.Imperial, FrontViewChoice.Top), calls[^1]);
    }

    [Fact]
    public void MeasuredSizes_ReplaceTheEstimateForTheChosenFront()
    {
        var vm = new InitialViewsViewModel(InitialViewModelKind.Part, new InitialViewsBoxCm(10, 4, 2), null, PrintUnits.Metric,
            (_, front) => front == FrontViewChoice.Top
                ? new Dictionary<InitialViewKind, ViewSizeCm> { [InitialViewKind.Front] = new ViewSizeCm(7, 3) }
                : null);
        vm.SelectedFront = vm.FrontOptions.Single(o => o.Choice == FrontViewChoice.Top);
        PlannedView front = vm.Plan.Views.Single(v => v.Kind == InitialViewKind.Front);
        Assert.Equal(7 * vm.Plan.Scale, front.WidthCm, 9);
    }

    [Fact]
    public void FixedSheet_MeasuresPerFrontChoice()
    {
        var asked = new List<FrontViewChoice>();
        var vm = new InitialViewsViewModel(InitialViewModelKind.Part, new InitialViewsBoxCm(10, 4, 2), null, A3, null,
            front =>
            {
                asked.Add(front);
                return null;
            });
        vm.SelectedFront = vm.FrontOptions.Single(o => o.Choice == FrontViewChoice.Left);
        Assert.Equal(new[] { FrontViewChoice.Auto, FrontViewChoice.Left }, asked);
    }

    [Fact]
    public void OkHandsBackThePlanWithTheOrientation()
    {
        var vm = new InitialViewsViewModel(InitialViewModelKind.Part, new InitialViewsBoxCm(10, 4, 2), null, A3);
        vm.SelectedFront = vm.FrontOptions.Single(o => o.Choice == FrontViewChoice.Back);
        vm.OkCommand.Execute(null);
        Assert.NotNull(vm.Result);
        Assert.Equal(-1, vm.Result!.FrontOrientation!.NormalZ, 9);
    }
}
