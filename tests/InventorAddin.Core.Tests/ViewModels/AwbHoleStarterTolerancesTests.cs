using System.Globalization;
using InventorAddin.Core.Holes;
using InventorAddin.Core.Tests.Holes;
using InventorAddin.Core.ViewModels;

namespace InventorAddin.Core.Tests.ViewModels;

/// <summary>
/// The Tolerance dropdown on the committed starter workbook's levels (task 132; spec 09, "Dowel and precision holes"):
/// every row of the chosen fit and units, the tolerance at the size and the row's name, the fit's Default = Y row
/// preselected, then Custom.
/// </summary>
public sealed class AwbHoleStarterTolerancesTests : IDisposable
{
    private const string PartName = "Test bracket.ipt";

    private static readonly HoleWorkbook Starter =
        HoleWorkbookReader.Read(HoleWorkbookReaderTests.RepoWorkbookPath()).Workbook;

    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;

    public AwbHoleStarterTolerancesTests() => CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");

    public void Dispose() => CultureInfo.CurrentCulture = _culture;

    private static AwbHoleViewModel Dowel(HoleUnits units, decimal nominal)
    {
        var vm = new AwbHoleViewModel(Starter, PartName) { CentreCount = 1 };
        vm.Units = units;
        vm.Standard = HoleStandards.DowelStandard;
        vm.Size = Starter.Dowel.First(d => d.Units == units && d.Nominal == nominal).Size;
        return vm;
    }

    private static List<(string Value, string Text, string? Detail)> Entries(AwbHoleViewModel vm) =>
        vm.ToleranceOptions.Select(o => (o.Value, o.Text, o.Detail)).ToList();

    [Fact]
    public void Metric6mm_EachFit_ListsItsLevels_WithTheToleranceAtTheSize_AndTheName()
    {
        var vm = Dowel(HoleUnits.Metric, 6m);

        vm.Fit = "Press";
        Assert.Equal(
            [
                ("Light press", "M7  0 / -0.012", "Light press"),
                ("Medium press", "N7  -0.004 / -0.016", "Medium press"),
                ("Heavy press", "P7  -0.008 / -0.020", "Heavy press"),
                (HoleToleranceOption.CustomValue, HoleToleranceOption.CustomText, null),
            ],
            Entries(vm));
        Assert.Equal("Medium press", vm.Tolerance);

        vm.Fit = "Slip";
        Assert.Equal(
            [
                ("Close slip", "G6  +0.012 / +0.004", "Close slip"),
                ("Standard slip", "G7  +0.016 / +0.004", "Standard slip"),
                ("Loose slip", "F7  +0.022 / +0.010", "Loose slip"),
                (HoleToleranceOption.CustomValue, HoleToleranceOption.CustomText, null),
            ],
            Entries(vm));
        Assert.Equal("Standard slip", vm.Tolerance);

        vm.Fit = "Normal";
        Assert.Equal(
            [
                ("Normal", "H7  +0.012 / 0", "Normal"),
                ("Normal fine", "H6  +0.008 / 0", "Normal fine"),
                (HoleToleranceOption.CustomValue, HoleToleranceOption.CustomText, null),
            ],
            Entries(vm));
        Assert.Equal("Normal", vm.Tolerance);
    }

    [Fact]
    public void Imperial_EachFit_ListsItsLevels_AsLimitsInInches()
    {
        var vm = Dowel(HoleUnits.Imperial, Starter.Dowel.First(d => d.Units == HoleUnits.Imperial).Nominal);

        vm.Fit = "Press";
        Assert.Equal(
            ["0 / -0.0003", "0 / -0.0005", "-0.0003 / -0.0008", HoleToleranceOption.CustomText],
            vm.ToleranceOptions.Select(o => o.Text));
        Assert.Equal("Medium press", vm.Tolerance);

        vm.Fit = "Slip";
        Assert.Equal(
            ["+0.0007 / +0.0003", "+0.0010 / +0.0005", "+0.0015 / +0.0008", HoleToleranceOption.CustomText],
            vm.ToleranceOptions.Select(o => o.Text));
        Assert.Equal("Standard slip", vm.Tolerance);

        vm.Fit = "Normal";
        Assert.Equal(["Normal", "Normal fine", HoleToleranceOption.CustomValue], vm.ToleranceOptions.Select(o => o.Value));
        Assert.Equal("Normal", vm.Tolerance);
    }

    [Theory]
    [InlineData("Light press")]
    [InlineData("Medium press")]
    [InlineData("Heavy press")]
    public void Metric6mm_PickingAPressLevel_BuildsWithItsIsoCode(string level)
    {
        var vm = Dowel(HoleUnits.Metric, 6m);
        vm.Fit = "Press";

        vm.Tolerance = level;

        var row = Starter.PrecisionTolerances.Single(r => r.Units == HoleUnits.Metric && r.Name == level);
        Assert.Equal(level, vm.Spec.Tolerance);
        Assert.Equal(row.IsoCode, vm.Geometry!.Tolerance!.IsoCode);
    }

    [Fact]
    public void TolerancedClearance_StartsOnTheNormalRow_AndListsEveryLevelOfTheUnits()
    {
        var vm = new AwbHoleViewModel(Starter, PartName) { CentreCount = 1 };
        vm.Units = HoleUnits.Metric;
        vm.Toleranced = true;

        Assert.Equal("Normal", vm.Tolerance);
        Assert.Equal(
            [
                "Normal", "Normal fine", "Close slip", "Standard slip", "Loose slip", "Light press", "Medium press",
                "Heavy press", HoleToleranceOption.CustomValue,
            ],
            vm.ToleranceOptions.Select(o => o.Value));
    }
}
