using System.Globalization;
using InventorAddin.Core.Holes;
using InventorAddin.Core.ViewModels;

namespace InventorAddin.Core.Tests.ViewModels;

/// <summary>
/// Toleranced Clearance and Simple holes and the Custom tolerance in the AWB Hole window (task 113; spec 09,
/// "Toleranced clearance and Simple holes", questions 17 and 18): the checkbox, the list, the Custom boxes, what is kept
/// and what is removed. Sizes and values are invented.
/// </summary>
public sealed class AwbHoleTolerancedViewModelTests : IDisposable
{
    private const string PartName = "Test bracket.ipt";

    private static readonly HoleWorkbook Workbook = AwbHoleTestWorkbook.Create();

    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;

    public AwbHoleTolerancedViewModelTests() => CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");

    public void Dispose() => CultureInfo.CurrentCulture = _culture;

    private static AwbHoleViewModel NewHole()
    {
        var vm = new AwbHoleViewModel(Workbook, PartName) { CentreCount = 1 };
        vm.Size = "T5";
        return vm;
    }

    private static AwbHoleViewModel Ticked()
    {
        var vm = NewHole();
        vm.Toleranced = true;
        return vm;
    }

    private static AwbHoleViewModel Custom()
    {
        var vm = Ticked();
        vm.Tolerance = HoleToleranceOption.CustomValue;
        return vm;
    }

    private static List<string> Names(AwbHoleViewModel vm) => vm.ToleranceOptions.Select(o => o.Value).ToList();

    [Fact]
    public void Toleranced_IsOfferedOnClearanceAndSimpleOnly_OffByDefault()
    {
        var vm = NewHole();
        Assert.True(vm.ShowsToleranced);
        Assert.False(vm.Toleranced);
        Assert.False(vm.ShowsTolerance);
        Assert.Empty(vm.ToleranceOptions);
        Assert.False(vm.Spec.Toleranced);
        Assert.False(vm.Geometry!.IsColoured);

        vm.Type = HoleStandards.SimpleType;
        Assert.True(vm.ShowsToleranced);

        vm.Type = HoleStandards.TappedType;
        Assert.False(vm.ShowsToleranced);

        vm.Type = HoleStandards.TaperTappedType;
        Assert.False(vm.ShowsToleranced);

        vm.Type = HoleStandards.ClearanceType;
        vm.Standard = HoleStandards.DowelStandard;
        Assert.False(vm.ShowsToleranced);
        Assert.True(vm.ShowsTolerance);
    }

    [Fact]
    public void Toleranced_IsIgnoredOnAKindThatCannotTakeIt()
    {
        var vm = NewHole();
        vm.Type = HoleStandards.TappedType;
        vm.Toleranced = true;
        Assert.False(vm.Toleranced);
        Assert.False(vm.ShowsTolerance);
    }

    [Fact]
    public void Ticked_ListsEveryRowForTheUnits_ThenCustom_WithTheFirstDefaultPreselected()
    {
        var vm = Ticked();

        Assert.True(vm.ShowsTolerance);
        Assert.Equal(
            ["N bore", "N loc", "N second", "S slip", "P press", HoleToleranceOption.CustomValue], Names(vm));
        Assert.Equal("N loc", vm.Tolerance);
        Assert.Equal(HoleToleranceOption.CustomText, vm.ToleranceOptions[^1].Text);
        Assert.False(vm.ShowsCustomTolerance);
        Assert.True(vm.Spec.Toleranced);
        Assert.Equal("N loc", vm.Spec.Tolerance);
        Assert.True(vm.CanOk);
        Assert.Equal(HoleSettings.Starter.NormalFitColour, vm.Geometry!.FitColour);
    }

    [Fact]
    public void TheListShowsEachToleranceAtTheClearanceDiameter()
    {
        var vm = Ticked();

        // T5 Normal is 5.5 mm: H7 there is +0.012 / 0.
        Assert.Equal("H7  +0.012 / 0", vm.ToleranceOptions.Single(o => o.Value == "N bore").Text);
    }

    [Fact]
    public void ASimpleHolesListShowsEachToleranceAtItsDiameter()
    {
        var vm = Ticked();
        vm.Type = HoleStandards.SimpleType;
        vm.DiameterText = "7";

        // H7 at 7 mm (6 to 10 mm): +0.015 / 0.
        Assert.Equal("H7  +0.015 / 0", vm.ToleranceOptions.Single(o => o.Value == "N bore").Text);
    }

    [Fact]
    public void PickingARowOfAnotherFit_TakesItsFitAndColour()
    {
        var vm = Ticked();
        vm.Tolerance = "P press";

        Assert.Equal(PrecisionFit.Press, vm.Spec.PrecisionFit);
        Assert.Equal(HoleSettings.Starter.PressFitColour, vm.Geometry!.FitColour);
        Assert.Equal(PrecisionFit.Press, vm.BuildResult.Resolved.PrecisionFit);
    }

    [Fact]
    public void Custom_ShowsEmptyBoxes_AndOkWaits()
    {
        var vm = Custom();

        Assert.True(vm.ShowsCustomTolerance);
        Assert.Equal("", vm.ToleranceUpperText);
        Assert.Equal("", vm.ToleranceLowerText);
        Assert.False(vm.CanOk);
        Assert.Equal(HoleRules.CustomUpperMissingText, vm.ToleranceUpperProblem);
        Assert.Equal(HoleRules.CustomLowerMissingText, vm.ToleranceLowerProblem);
    }

    [Fact]
    public void Custom_RefusesTextThatIsNotANumber()
    {
        var vm = Custom();
        vm.ToleranceLowerText = "0";
        vm.ToleranceUpperText = "d0";

        Assert.Equal(AwbHoleViewModel.NotANumberText, vm.ToleranceUpperProblem);
        Assert.Equal("d0", vm.ToleranceUpperText);
        Assert.False(vm.CanOk);
    }

    [Fact]
    public void Custom_RefusesUpperNotAboveLower()
    {
        var vm = Custom();
        vm.ToleranceUpperText = "0.01";
        vm.ToleranceLowerText = "0.02";

        Assert.Contains("is not above the lower limit", vm.ToleranceUpperProblem);
        Assert.False(vm.CanOk);
    }

    [Fact]
    public void Custom_WithValidLimits_BuildsWithTheNormalColour_AndShowsOnTheDiagram()
    {
        var vm = Custom();
        vm.Tolerance = "P press";
        vm.Tolerance = HoleToleranceOption.CustomValue;
        vm.ToleranceUpperText = "0.01";
        vm.ToleranceLowerText = "-0.005";

        Assert.True(vm.CanOk);
        Assert.True(vm.Spec.CustomTolerance);
        Assert.Null(vm.Spec.Tolerance);
        Assert.Equal(PrecisionFit.Normal, vm.Spec.PrecisionFit);
        Assert.Equal(0.01m, vm.Spec.CustomUpper);
        Assert.Equal(-0.005m, vm.Spec.CustomLower);
        Assert.Equal(HoleSettings.Starter.NormalFitColour, vm.Geometry!.FitColour);
        Assert.Equal("+0.010 / -0.005", vm.DiagramInput.Tolerance);
    }

    [Fact]
    public void ARowsCodeShowsOnTheDiagram()
    {
        Assert.Equal("G7", Ticked().DiagramInput.Tolerance);
        Assert.Null(NewHole().DiagramInput.Tolerance);
    }

    [Fact]
    public void TypedLimits_AreKeptAcrossAUnitsSwitch_Converted_AndBack()
    {
        var vm = Custom();
        vm.ToleranceUpperText = "0.0254";
        vm.ToleranceLowerText = "0";

        vm.Units = HoleUnits.Imperial;
        Assert.True(vm.ShowsCustomTolerance);
        Assert.Equal("0.001", vm.ToleranceUpperText);
        Assert.Equal("0", vm.ToleranceLowerText);

        vm.Units = HoleUnits.Metric;
        Assert.Equal("0.0254", vm.ToleranceUpperText);
    }

    [Fact]
    public void TypedLimits_AreKeptWhenCustomIsLeftAndChosenAgain()
    {
        var vm = Custom();
        vm.ToleranceUpperText = "0.02";
        vm.ToleranceLowerText = "0";

        vm.Tolerance = "N bore";
        Assert.False(vm.ShowsCustomTolerance);
        Assert.Null(vm.Spec.CustomUpper);

        vm.Tolerance = HoleToleranceOption.CustomValue;
        Assert.Equal("0.02", vm.ToleranceUpperText);
        Assert.True(vm.CanOk);
    }

    [Fact]
    public void TheChoice_IsKeptFromClearanceToSimple()
    {
        var vm = Ticked();
        vm.Tolerance = "S slip";
        vm.Type = HoleStandards.SimpleType;
        vm.DiameterText = "6";

        Assert.True(vm.Toleranced);
        Assert.Equal("S slip", vm.Tolerance);
        Assert.True(vm.Spec.Toleranced);
    }

    [Fact]
    public void Unticking_RemovesTheToleranceAndColour()
    {
        var vm = Ticked();
        vm.Toleranced = false;

        Assert.False(vm.ShowsTolerance);
        Assert.Null(vm.Tolerance);
        Assert.False(vm.Spec.Toleranced);
        Assert.Null(vm.Geometry!.Tolerance);
        Assert.False(vm.Geometry.IsColoured);
        Assert.False(vm.Spec.IsPrecision);
    }

    [Fact]
    public void AChangeToATypeThatCannotBeToleranced_UnticksIt()
    {
        var vm = Ticked();
        vm.Type = HoleStandards.TappedType;
        Assert.False(vm.Toleranced);
        Assert.False(vm.Spec.Toleranced);

        vm.Type = HoleStandards.ClearanceType;
        Assert.False(vm.Toleranced);
        Assert.False(vm.ShowsTolerance);
    }

    [Fact]
    public void ADowelsList_EndsWithCustom()
    {
        var vm = NewHole();
        vm.Standard = HoleStandards.DowelStandard;

        Assert.Equal(HoleToleranceOption.CustomValue, Names(vm)[^1]);
        vm.Tolerance = HoleToleranceOption.CustomValue;
        Assert.True(vm.ShowsCustomTolerance);
        vm.ToleranceUpperText = "0.01";
        vm.ToleranceLowerText = "0";
        Assert.True(vm.CanOk);
        Assert.True(vm.Spec.CustomTolerance);
    }

    [Fact]
    public void Edit_OpensOnATolerancedCustomHole_WithItsLimitsTyped()
    {
        var spec = HoleSpec.ClearanceHole(HoleUnits.Metric, "T5") with
        {
            Toleranced = true,
            CustomTolerance = true,
            CustomUpper = 0.02m,
            CustomLower = -0.01m,
        };

        var vm = AwbHoleViewModel.ForEdit(Workbook, PartName, spec);
        vm.CentreCount = 1;

        Assert.True(vm.Toleranced);
        Assert.Equal(HoleToleranceOption.CustomValue, vm.Tolerance);
        Assert.Equal("0.02", vm.ToleranceUpperText);
        Assert.Equal("-0.01", vm.ToleranceLowerText);
        Assert.Equal(spec.Normalised(), vm.Spec);
        Assert.True(vm.CanOk);

        vm.Size = "T3";
        Assert.Equal("0.02", vm.ToleranceUpperText);
    }

    [Fact]
    public void Edit_OpensOnATolerancedRow()
    {
        var spec = HoleSpec.ClearanceHole(HoleUnits.Metric, "T5") with
        {
            Toleranced = true,
            Tolerance = "P press",
            PrecisionFit = PrecisionFit.Press,
        };

        var vm = AwbHoleViewModel.ForEdit(Workbook, PartName, spec);

        Assert.True(vm.Toleranced);
        Assert.Equal("P press", vm.Tolerance);
        Assert.Equal(spec.Normalised(), vm.Spec);
    }

    [Fact]
    public void ATolerancedChange_RaisesOnePreview()
    {
        var vm = NewHole();
        var previews = 0;
        vm.PreviewNeeded += (_, _) => previews++;

        vm.Toleranced = true;
        Assert.Equal(1, previews);
    }
}
