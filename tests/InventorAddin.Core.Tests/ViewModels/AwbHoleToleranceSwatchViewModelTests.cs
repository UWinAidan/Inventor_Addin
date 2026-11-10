using System.ComponentModel;
using System.Globalization;
using InventorAddin.Core.Holes;
using InventorAddin.Core.ViewModels;

namespace InventorAddin.Core.Tests.ViewModels;

/// <summary>
/// The colour swatch on a toleranced hole's Tolerance row (task 138; Aidan, 2026-11-07, spec 09 question 28): shown
/// while Toleranced is ticked on a Clearance or Simple hole, in the colour the hole's faces take on OK, named as a
/// dowel's. Sizes and values are invented.
/// </summary>
public sealed class AwbHoleToleranceSwatchViewModelTests : IDisposable
{
    private const string PartName = "Test bracket.ipt";

    private static readonly HoleWorkbook Workbook = AwbHoleTestWorkbook.Create();

    private static readonly string Normal = HoleFitOption.SwatchOf(HoleSettings.Starter.NormalFitColour);
    private static readonly string Slip = HoleFitOption.SwatchOf(HoleSettings.Starter.SlipFitColour);
    private static readonly string Press = HoleFitOption.SwatchOf(HoleSettings.Starter.PressFitColour);

    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;

    public AwbHoleToleranceSwatchViewModelTests() => CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");

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

    private static void AssertNoSwatch(AwbHoleViewModel vm)
    {
        Assert.False(vm.ShowsToleranceColour);
        Assert.Null(vm.ToleranceSwatch);
        Assert.Null(vm.ToleranceColourText);
    }

    private static List<string> Raised(AwbHoleViewModel vm, Action change)
    {
        var names = new List<string>();
        PropertyChangedEventHandler handler = (_, e) => names.Add(e.PropertyName ?? "");
        vm.PropertyChanged += handler;
        change();
        vm.PropertyChanged -= handler;
        return names;
    }

    [Fact]
    public void Unticked_ThereIsNoSwatch() => AssertNoSwatch(NewHole());

    [Fact]
    public void Ticked_ShowsTheDefaultRowsColour_TheColourTheFacesTake()
    {
        var vm = Ticked();

        Assert.True(vm.ShowsToleranceColour);
        Assert.Equal(Normal, vm.ToleranceSwatch);
        Assert.Equal("Normal fit", vm.ToleranceColourText);
        Assert.Equal(HoleFitOption.SwatchOf(vm.Geometry!.FitColour!), vm.ToleranceSwatch);
    }

    [Theory]
    [InlineData("N bore", "Normal")]
    [InlineData("N loc", "Normal")]
    [InlineData("N second", "Normal")]
    [InlineData("S slip", "Slip")]
    [InlineData("P press", "Press")]
    public void EachRow_ShowsTheColourTheFacesTake(string row, string fit)
    {
        var vm = Ticked();
        vm.Tolerance = row;

        Assert.Equal(HoleFitOption.SwatchOf(vm.Geometry!.FitColour!), vm.ToleranceSwatch);
        Assert.Equal(fit + " fit", vm.ToleranceColourText);
    }

    [Fact]
    public void ChangingTheRow_ChangesTheSwatch()
    {
        var vm = Ticked();

        vm.Tolerance = "P press";
        Assert.Equal(Press, vm.ToleranceSwatch);
        Assert.Equal("Press fit", vm.ToleranceColourText);

        vm.Tolerance = "S slip";
        Assert.Equal(Slip, vm.ToleranceSwatch);
        Assert.Equal("Slip fit", vm.ToleranceColourText);
    }

    [Fact]
    public void Custom_ShowsTheNormalColour_BeforeAndAfterItsLimitsAreTyped()
    {
        var vm = Ticked();
        vm.Tolerance = "P press";
        vm.Tolerance = HoleToleranceOption.CustomValue;

        Assert.Equal(Normal, vm.ToleranceSwatch);
        Assert.Equal("Normal fit", vm.ToleranceColourText);

        vm.ToleranceUpperText = "0.02";
        vm.ToleranceLowerText = "0";
        Assert.True(vm.CanOk);
        Assert.Equal(HoleFitOption.SwatchOf(vm.Geometry!.FitColour!), vm.ToleranceSwatch);
    }

    [Fact]
    public void ASimpleHole_ShowsTheSwatch_TheColourItsFacesTake()
    {
        var vm = Ticked();
        vm.Type = HoleStandards.SimpleType;
        vm.DiameterText = "6";
        vm.Tolerance = "S slip";

        Assert.Equal(Slip, vm.ToleranceSwatch);
        Assert.Equal(HoleFitOption.SwatchOf(vm.Geometry!.FitColour!), vm.ToleranceSwatch);
    }

    [Fact]
    public void ASimpleHoleWithNoDiameterYet_StillShowsItsRowsColour()
    {
        var vm = Ticked();
        vm.Type = HoleStandards.SimpleType;
        vm.Tolerance = "P press";

        Assert.Null(vm.Geometry);
        Assert.Equal(Press, vm.ToleranceSwatch);
    }

    [Fact]
    public void Unticking_RemovesTheSwatch()
    {
        var vm = Ticked();
        vm.Toleranced = false;

        AssertNoSwatch(vm);
    }

    [Fact]
    public void ATypeThatCannotBeToleranced_HasNoSwatch()
    {
        var vm = Ticked();
        vm.Type = HoleStandards.TappedType;
        AssertNoSwatch(vm);

        vm.Type = HoleStandards.TaperTappedType;
        AssertNoSwatch(vm);
    }

    [Fact]
    public void ADowel_KeepsItsColourOnFit_AndHasNoToleranceSwatch()
    {
        var vm = Ticked();
        vm.Standard = HoleStandards.DowelStandard;
        vm.Fit = "Press";

        AssertNoSwatch(vm);
        Assert.True(vm.ShowsFitColour);
        Assert.Equal(Press, vm.FitSwatch);
        Assert.Equal("Press fit", vm.FitColourText);
    }

    [Fact]
    public void AUnitsChange_ShowsTheColourOfTheNewUnitsRow()
    {
        var vm = Ticked();
        vm.Tolerance = "P press";

        vm.Units = HoleUnits.Imperial;

        Assert.True(vm.ShowsTolerance);
        Assert.Equal("IN bore", vm.Tolerance);
        Assert.Equal(Normal, vm.ToleranceSwatch);
        Assert.Equal("Normal fit", vm.ToleranceColourText);
    }

    [Fact]
    public void Edit_OpensWithTheSwatchOfTheStoredRow()
    {
        var spec = HoleSpec.ClearanceHole(HoleUnits.Metric, "T5") with
        {
            Toleranced = true,
            Tolerance = "S slip",
            PrecisionFit = PrecisionFit.Slip,
        };

        var vm = AwbHoleViewModel.ForEdit(Workbook, PartName, spec);

        Assert.Equal(Slip, vm.ToleranceSwatch);
        Assert.Equal("Slip fit", vm.ToleranceColourText);
    }

    [Fact]
    public void TheSwatchIsRaised_WhenToleranced_TheRow_TheType_TheStandardOrTheUnitsChange()
    {
        string[] swatch = [nameof(AwbHoleViewModel.ShowsToleranceColour), nameof(AwbHoleViewModel.ToleranceSwatch),
            nameof(AwbHoleViewModel.ToleranceColourText)];
        var vm = NewHole();

        Assert.Subset(Raised(vm, () => vm.Toleranced = true).ToHashSet(), swatch.ToHashSet());
        Assert.Subset(Raised(vm, () => vm.Tolerance = "P press").ToHashSet(), swatch.ToHashSet());
        Assert.Subset(Raised(vm, () => vm.Type = HoleStandards.SimpleType).ToHashSet(), swatch.ToHashSet());
        Assert.Subset(Raised(vm, () => vm.Type = HoleStandards.ClearanceType).ToHashSet(), swatch.ToHashSet());
        Assert.Subset(Raised(vm, () => vm.Standard = HoleStandards.DowelStandard).ToHashSet(), swatch.ToHashSet());
        Assert.Subset(Raised(vm, () => vm.Units = HoleUnits.Imperial).ToHashSet(), swatch.ToHashSet());
    }
}
