using InventorAddin.Core.Drawing;
using InventorAddin.Core.ViewModels;

namespace InventorAddin.Core.Tests.ViewModels;

public sealed class InitialViewsViewModelTests
{
    private static readonly SheetSizeSpec A3 = PrintTemplateLayout.IsoSheets.Single(s => s.Name == "A3");
    private static readonly InitialViewsBoxCm Box = new(10, 6, 4);

    private static InitialViewsViewModel Part() => new(InitialViewModelKind.Part, Box, null, A3);

    private static InitialViewChoice Get(InitialViewsViewModel vm, InitialViewKind k) => vm.Views.Single(v => v.Kind == k);

    private static void UntickAll(InitialViewsViewModel vm)
    {
        foreach (var v in vm.Views.ToList())
            v.IsChecked = false;
    }

    [Fact]
    public void Starts_WithAllViewsTicked_AndSuggestedScaleSelected()
    {
        var vm = Part();
        Assert.All(vm.Views, v => Assert.True(v.IsChecked));
        Assert.Equal(4, vm.Views.Count);
        Assert.Null(vm.SelectedScale.Scale);
        Assert.Equal(4, vm.Plan.Views.Count);
        Assert.True(vm.CanOk);
        Assert.False(vm.HasWarning);
        Assert.Equal("A3, 420 x 297 mm", vm.SheetSizeText);
    }

    [Fact]
    public void ScaleList_SuggestedFirst_ThenStandardScales()
    {
        var vm = Part();
        Assert.Equal(PrintPlan.StandardScales.Count + 1, vm.ScaleOptions.Count);
        Assert.Equal($"Suggested ({InitialViewsViewModel.FormatScale(vm.Plan.Scale)})", vm.ScaleOptions[0].Label);
        Assert.Equal(PrintPlan.StandardScales, vm.ScaleOptions.Skip(1).Select(o => o.Scale!.Value).ToList());
        Assert.Equal("1:2", vm.ScaleOptions.Single(o => o.Scale == 0.5).Label);
    }

    [Theory]
    [InlineData(0.5, "1:2")]
    [InlineData(2.0, "2:1")]
    [InlineData(1.0, "1:1")]
    [InlineData(0.02, "1:50")]
    public void FormatScale(double scale, string expected) =>
        Assert.Equal(expected, InitialViewsViewModel.FormatScale(scale));

    [Fact]
    public void Unticking_RePlans()
    {
        var vm = Part();
        Get(vm, InitialViewKind.Iso).IsChecked = false;
        Assert.DoesNotContain(vm.Plan.Views, v => v.Kind == InitialViewKind.Iso);
        Assert.Equal(3, vm.Plan.Views.Count);
    }

    [Fact]
    public void TickingTop_WithFrontOff_TurnsFrontOn()
    {
        var vm = Part();
        Get(vm, InitialViewKind.Front).IsChecked = false;
        Assert.False(Get(vm, InitialViewKind.Top).IsChecked);

        Get(vm, InitialViewKind.Top).IsChecked = true;
        Assert.True(Get(vm, InitialViewKind.Front).IsChecked);
        Assert.Contains(vm.Plan.Views, v => v.Kind == InitialViewKind.Front);
        Assert.Contains(vm.Plan.Views, v => v.Kind == InitialViewKind.Top);
    }

    [Fact]
    public void TickingRight_WithFrontOff_TurnsFrontOn()
    {
        var vm = Part();
        Get(vm, InitialViewKind.Front).IsChecked = false;
        Get(vm, InitialViewKind.Right).IsChecked = true;
        Assert.True(Get(vm, InitialViewKind.Front).IsChecked);
        Assert.False(Get(vm, InitialViewKind.Top).IsChecked);
        Assert.Contains(vm.Plan.Views, v => v.Kind == InitialViewKind.Front);
    }

    [Fact]
    public void UntickingFront_UnticksTopAndRight_ButKeepsIso()
    {
        var vm = Part();
        Get(vm, InitialViewKind.Front).IsChecked = false;
        Assert.False(Get(vm, InitialViewKind.Top).IsChecked);
        Assert.False(Get(vm, InitialViewKind.Right).IsChecked);
        Assert.True(Get(vm, InitialViewKind.Iso).IsChecked);
        Assert.Equal(new[] { InitialViewKind.Iso }, vm.Plan.Views.Select(v => v.Kind));
    }

    [Fact]
    public void NothingTicked_DisablesOk_AndWarns()
    {
        var vm = Part();
        UntickAll(vm);
        Assert.False(vm.CanOk);
        Assert.False(vm.OkCommand.CanExecute(null));
        Assert.Contains(InitialViewsPlan.NoViewsMessage, vm.Messages);
        Assert.True(vm.HasWarning);
    }

    [Fact]
    public void ChoosingTooLargeScale_Warns_AndRestoringSuggestedClears()
    {
        var vm = Part();
        vm.SelectedScale = vm.ScaleOptions.Single(o => o.Scale == 10.0);
        Assert.Equal(10.0, vm.Plan.Scale);
        Assert.True(vm.HasWarning);
        Assert.Contains(InitialViewsPlan.DoesNotFitAtScaleMessage, vm.Warning);
        Assert.StartsWith("Suggested (", vm.ScaleOptions[0].Label);

        vm.SelectedScale = vm.ScaleOptions[0];
        Assert.False(vm.HasWarning);
    }

    [Fact]
    public void SuggestedLabel_FollowsTheTickedViews()
    {
        var vm = new InitialViewsViewModel(InitialViewModelKind.Part, new InitialViewsBoxCm(30, 3, 30), null, A3);
        string before = vm.ScaleOptions[0].Label;
        Get(vm, InitialViewKind.Iso).IsChecked = false;
        Get(vm, InitialViewKind.Top).IsChecked = false;
        Get(vm, InitialViewKind.Right).IsChecked = false;
        Assert.NotEqual(before, vm.ScaleOptions[0].Label);
    }

    [Fact]
    public void Ok_StoresResultAndCloses()
    {
        var vm = Part();
        int closed = 0;
        vm.CloseRequested += (_, _) => closed++;
        vm.OkCommand.Execute(null);
        Assert.Same(vm.Plan, vm.Result);
        Assert.Equal(1, closed);
    }

    [Fact]
    public void Ok_WhenDisabled_DoesNothing()
    {
        var vm = Part();
        UntickAll(vm);
        int closed = 0;
        vm.CloseRequested += (_, _) => closed++;
        vm.OkCommand.Execute(null);
        Assert.Null(vm.Result);
        Assert.Equal(0, closed);
    }

    [Fact]
    public void Cancel_ReturnsNoResult_AndCloses()
    {
        var vm = Part();
        int closed = 0;
        vm.CloseRequested += (_, _) => closed++;
        vm.CancelCommand.Execute(null);
        Assert.Null(vm.Result);
        Assert.Equal(1, closed);
    }

    [Fact]
    public void SheetMetal_WithoutFlatPatternSize_WarnsButOkStaysOn()
    {
        var vm = new InitialViewsViewModel(InitialViewModelKind.SheetMetalPart, Box, null, A3);
        Assert.Equal(2, vm.Views.Count);
        Assert.Contains(InitialViewsPlan.FlatPatternUnknownMessage, vm.Messages);
        Assert.True(vm.CanOk);
    }

    [Fact]
    public void Imperial_SheetSizeText()
    {
        SheetSizeSpec b = PrintTemplateLayout.AnsiSheets.Single(s => s.Name == "B");
        var vm = new InitialViewsViewModel(InitialViewModelKind.Assembly, Box, null, b);
        Assert.Equal("ANSI B, 17 x 11 in", vm.SheetSizeText);
    }

    [Fact]
    public void PropertyChanged_RaisedForPlanProperties()
    {
        var vm = Part();
        var names = new List<string?>();
        vm.PropertyChanged += (_, e) => names.Add(e.PropertyName);
        Get(vm, InitialViewKind.Iso).IsChecked = false;
        Assert.Contains(nameof(vm.Plan), names);
        Assert.Contains(nameof(vm.CanOk), names);
        Assert.Contains(nameof(vm.Warning), names);
    }
}
