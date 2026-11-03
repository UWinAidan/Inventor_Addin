using System.Globalization;
using InventorAddin.Core.Holes;
using InventorAddin.Core.ViewModels;

namespace InventorAddin.Core.Tests.ViewModels;

/// <summary>
/// A centre-drill spot and a seat depth of 0 in the AWB Hole window (spec 09, "The window": Seat sizes; question 20;
/// task 127): the countersink angle box offers 60 and 90 deg on every type, and a counterbore or spotface depth of 0 is
/// allowed.
/// </summary>
public sealed class AwbHoleCentreDrillSpotViewModelTests : IDisposable
{
    private const string PartName = "Test bracket.ipt";

    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;

    private static readonly HoleWorkbook Workbook = AwbHoleTestWorkbook.Create();

    public AwbHoleCentreDrillSpotViewModelTests() => CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");

    public void Dispose() => CultureInfo.CurrentCulture = _culture;

    private static AwbHoleViewModel Hole(string kind)
    {
        var vm = new AwbHoleViewModel(Workbook, PartName) { CentreCount = 1 };
        switch (kind)
        {
            case "clearance":
                vm.Size = "T5";
                break;
            case "tapped":
                vm.Type = HoleStandards.TappedType;
                vm.Size = "M5";
                break;
            case "dowel":
                vm.Standard = HoleStandards.DowelStandard;
                vm.Size = "5 mm";
                break;
            case "simple":
                vm.Type = HoleStandards.SimpleType;
                vm.DiameterText = "5";
                break;
        }

        return vm;
    }

    [Fact]
    public void TheAngleBox_Offers60And90()
    {
        Assert.Equal(new[] { "60", "90" }, AwbHoleViewModel.CountersinkAngleChoices);
    }

    [Theory]
    [InlineData("clearance", "60")]
    [InlineData("clearance", "90")]
    [InlineData("tapped", "60")]
    [InlineData("tapped", "90")]
    [InlineData("dowel", "60")]
    [InlineData("dowel", "90")]
    [InlineData("simple", "60")]
    [InlineData("simple", "90")]
    public void EveryType_TakesASmallCountersinkAtAQuickChoice(string kind, string angle)
    {
        var vm = Hole(kind);
        vm.Seat = HoleSeat.Countersink;
        vm.CountersinkDiameterText = "7";

        vm.CountersinkAngleText = angle;

        Assert.True(vm.ShowsCountersinkSizes);
        Assert.Null(vm.CountersinkAngleProblem);
        Assert.Null(vm.CountersinkDiameterProblem);
        Assert.True(vm.CanOk);
        Assert.Equal(decimal.Parse(angle, CultureInfo.InvariantCulture), vm.Spec.CsinkAngleDeg);
        Assert.Equal(double.Parse(angle, CultureInfo.InvariantCulture), vm.Geometry!.CountersinkAngleDeg);
        Assert.Equal(0.7, vm.Geometry.CountersinkDiameterCm!.Value, 12);
    }

    [Fact]
    public void AnAngleThatIsNotAQuickChoice_CanStillBeTyped()
    {
        var vm = Hole("tapped");
        vm.Seat = HoleSeat.Countersink;

        vm.CountersinkAngleText = "82";

        Assert.Null(vm.CountersinkAngleProblem);
        Assert.Equal(82m, vm.Spec.CsinkAngleDeg);
    }

    [Theory]
    [InlineData("clearance")]
    [InlineData("tapped")]
    [InlineData("dowel")]
    [InlineData("simple")]
    public void ACounterboreDepthOf0_IsAllowed_ANegativeOneIsNot(string kind)
    {
        var vm = Hole(kind);
        vm.Seat = HoleSeat.Counterbore;
        vm.CounterboreDiameterText = "11";

        vm.CounterboreDepthText = "0";

        Assert.Null(vm.CounterboreDepthProblem);
        Assert.True(vm.CanOk);
        Assert.Equal(0m, vm.Spec.CboreDepth);
        Assert.Equal(0.0, vm.Geometry!.CounterboreDepthCm);

        vm.CounterboreDepthText = "-0.5";

        Assert.Equal("The counterbore depth is less than zero.", vm.CounterboreDepthProblem);
        Assert.False(vm.CanOk);
    }

    [Fact]
    public void ASpotfaceDepthOf0_IsAllowed_ANegativeOneIsNot()
    {
        var vm = Hole("tapped");
        vm.Seat = HoleSeat.Spotface;
        vm.SpotfaceDiameterText = "11";

        vm.SpotfaceDepthText = "0";

        Assert.Null(vm.SpotfaceDepthProblem);
        Assert.True(vm.CanOk);
        Assert.Equal(0.0, vm.Geometry!.SpotfaceDepthCm);

        vm.SpotfaceDepthText = "-0.5";

        Assert.Equal("The spotface depth is less than zero.", vm.SpotfaceDepthProblem);
        Assert.False(vm.CanOk);
    }
}
