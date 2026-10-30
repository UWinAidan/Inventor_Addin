using System.Globalization;
using InventorAddin.Core.Holes;
using InventorAddin.Core.ViewModels;

namespace InventorAddin.Core.Tests.ViewModels;

/// <summary>
/// A counterbore or countersink on a clearance size with no row of the seat, in the AWB Hole window (task 137; spec 09
/// question 26, Aidan 2026-10-30): the Size list is the Clearance tab's, every seat is offered on every size, and a
/// size with no row shows empty seat boxes, no Fastener type, and the Fit, and OK waits for values. Sizes and values
/// are invented.
/// </summary>
public sealed class AwbHoleClearanceSeatWithoutRowViewModelTests : IDisposable
{
    private const string PartName = "Test bracket.ipt";

    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;

    // T7 has a Clearance row and no seat rows; T3 has a Counterbore row and no Countersink row; T5 has both.
    private static readonly HoleWorkbook Workbook = AwbHoleTestWorkbook.Create() with
    {
        Clearance =
        [
            .. AwbHoleTestWorkbook.Create().Clearance,
            new ClearanceRow(11, HoleUnits.Metric, "T7", 7m, 7.2m, 7.4m, 7.6m, false),
        ],
    };

    public AwbHoleClearanceSeatWithoutRowViewModelTests() =>
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");

    public void Dispose() => CultureInfo.CurrentCulture = _culture;

    private static List<string> Values<T>(IEnumerable<HoleOption<T>> options) => options.Select(o => o.Text).ToList();

    private static AwbHoleViewModel NewHole() => new(Workbook, PartName) { CentreCount = 1 };

    private static AwbHoleViewModel Counterbored(string size)
    {
        var vm = NewHole();
        vm.Seat = HoleSeat.Counterbore;
        vm.Size = size;
        return vm;
    }

    [Theory]
    [InlineData(HoleSeat.None)]
    [InlineData(HoleSeat.Counterbore)]
    [InlineData(HoleSeat.Countersink)]
    [InlineData(HoleSeat.Spotface)]
    public void TheSizeList_IsTheClearanceTabs_WhateverTheSeat(HoleSeat seat)
    {
        var vm = NewHole();

        vm.Seat = seat;

        Assert.Equal(["T3", "T5", "T7"], Values(vm.SizeOptions));
    }

    [Fact]
    public void EverySeat_IsOfferedOnEveryClearanceSize()
    {
        var vm = NewHole();
        foreach (var size in vm.SizeOptions.Select(o => o.Value).ToList())
        {
            vm.Size = size;

            Assert.Equal(
                [HoleSeat.None, HoleSeat.Counterbore, HoleSeat.Countersink, HoleSeat.Spotface],
                vm.SeatOptions.Select(o => o.Value).ToList());
        }
    }

    [Fact]
    public void ASeatOnASizeWithNoRow_StartsEmpty_HidesTheFastener_ShowsTheFit_AndOkWaits()
    {
        var vm = Counterbored("T7");

        Assert.Equal("T7", vm.Size);
        Assert.Equal(HoleSeat.Counterbore, vm.Seat);
        Assert.False(vm.ShowsFastener);
        Assert.Null(vm.Fastener);
        Assert.True(vm.ShowsCounterboreSizes);
        Assert.Null(vm.CounterboreDiameter);
        Assert.Null(vm.CounterboreDepth);
        Assert.True(vm.ShowsFit);
        Assert.Equal("Normal", vm.Fit);
        Assert.Null(vm.SizeProblem);
        Assert.Equal("Enter a counterbore diameter.", vm.CounterboreDiameterProblem);
        Assert.Equal("Enter a counterbore depth.", vm.CounterboreDepthProblem);
        Assert.False(vm.CanOk);
        Assert.Null(vm.Geometry);

        vm.CounterboreDiameterText = "12";
        Assert.False(vm.CanOk);

        vm.CounterboreDepthText = "4";

        Assert.True(vm.CanOk);
        Assert.Equal(0.74, vm.Geometry!.HoleDiameterCm, 12);
        Assert.Equal(1.2, vm.Geometry.CounterboreDiameterCm!.Value, 12);
        Assert.Null(vm.Spec.Fastener);
    }

    [Fact]
    public void ASeatOnASizeWithNoRow_TakesItsThroughHoleAtTheChosenFit()
    {
        var vm = Counterbored("T7");
        vm.CounterboreDiameterText = "12";
        vm.CounterboreDepthText = "4";

        vm.Fit = "Loose";

        Assert.Equal(0.76, vm.Geometry!.HoleDiameterCm, 12);
        Assert.Equal(ClearanceFit.Loose, vm.Spec.ClearanceFit);
    }

    [Fact]
    public void ANotWiderSeat_IsAProblemUnderItsBox()
    {
        var vm = Counterbored("T7");
        vm.CounterboreDiameterText = "7";
        vm.CounterboreDepthText = "4";

        Assert.Equal("The hole (7.4 mm) is not smaller than its counterbore (7 mm).", vm.CounterboreDiameterProblem);
        Assert.False(vm.CanOk);
    }

    [Fact]
    public void ACountersinkOnASizeWithNoCountersinkRow_StartsEmpty()
    {
        var vm = NewHole();
        vm.Size = "T3";

        vm.Seat = HoleSeat.Countersink;

        Assert.Equal("T3", vm.Size);
        Assert.False(vm.ShowsFastener);
        Assert.True(vm.ShowsFit);
        Assert.Null(vm.CountersinkDiameter);
        Assert.Null(vm.CountersinkAngle);
        Assert.Equal("Enter a countersink angle.", vm.CountersinkAngleProblem);
        Assert.False(vm.CanOk);

        vm.CountersinkDiameterText = "6";
        vm.CountersinkAngleText = "90";

        Assert.True(vm.CanOk);
        Assert.Equal(0.34, vm.Geometry!.HoleDiameterCm, 12);
    }

    [Fact]
    public void ChangingToASizeWithARow_FillsTheUntypedBoxesFromTheRow()
    {
        var vm = Counterbored("T7");
        vm.CounterboreDiameterText = "12";

        vm.Size = "T5";

        // The typed diameter is kept (task 124); the untyped depth takes the row's.
        Assert.True(vm.ShowsFastener);
        Assert.Equal("TCAP", vm.Fastener);
        Assert.False(vm.ShowsFit);
        Assert.Equal((12m, 5m), (vm.CounterboreDiameter, vm.CounterboreDepth));
        Assert.True(vm.CanOk);
    }

    [Fact]
    public void ChangingFromASizeWithARow_EmptiesTheUntypedBoxes_WithNoLastValues()
    {
        var vm = Counterbored("T5");
        Assert.Equal((9.5m, 5m), (vm.CounterboreDiameter, vm.CounterboreDepth));

        vm.Size = "T7";

        Assert.Null(vm.CounterboreDiameter);
        Assert.Null(vm.CounterboreDepth);
        Assert.False(vm.CanOk);
    }

    [Fact]
    public void ASizeWithARow_BehavesAsBefore()
    {
        var vm = Counterbored("T5");

        vm.Fastener = "TLOW";

        Assert.False(vm.ShowsFit);
        Assert.Equal(["TCAP", "TLOW"], Values(vm.FastenerOptions));
        Assert.Equal((8.5m, 3m), (vm.CounterboreDiameter, vm.CounterboreDepth));
        Assert.Equal(0.53, vm.Geometry!.HoleDiameterCm, 12);
        Assert.Equal("TLOW", vm.Spec.Fastener);
    }

    [Fact]
    public void AnEditedHoleWithNoRow_OpensOnItsOwnSeatAndFit_AndKeepsThemAcrossASizeChange()
    {
        var spec = HoleSpec.ClearanceHole(HoleUnits.Metric, "T7") with
        {
            Seat = HoleSeat.Counterbore,
            ClearanceFit = ClearanceFit.Close,
            CboreDiameter = 12m,
            CboreDepth = 4m,
        };

        var vm = AwbHoleViewModel.ForEdit(Workbook, PartName, spec);
        vm.CentreCount = 1;

        Assert.Equal("T7", vm.Size);
        Assert.False(vm.ShowsFastener);
        Assert.Equal("Close", vm.Fit);
        Assert.Equal((12m, 4m), (vm.CounterboreDiameter, vm.CounterboreDepth));
        Assert.True(vm.CanOk);
        Assert.Equal(0.72, vm.Geometry!.HoleDiameterCm, 12);

        vm.Size = "T5";

        // The hole's own values count as typed: no row gave them (task 126).
        Assert.Equal((12m, 4m), (vm.CounterboreDiameter, vm.CounterboreDepth));
    }

    [Fact]
    public void AnInventorHoleReadBackOnASizeWithNoRow_OpensOnItsOwnSeat()
    {
        var facts = new InventorHoleFacts(
            HoleSeat.Counterbore, false, null, null, null, 0.76, HoleTermination.ThroughAll, null,
            1.2, 0.4, null, null, false, null, null, null, null, false);
        var readBack = HoleReadBack.Read(null, facts, Workbook, HoleUnits.Metric);

        var vm = AwbHoleViewModel.ForReadBack(Workbook, PartName, readBack);
        vm.CentreCount = 1;

        Assert.Equal("T7", vm.Size);
        Assert.Equal("Loose", vm.Fit);
        Assert.Equal((12m, 4m), (vm.CounterboreDiameter, vm.CounterboreDepth));
        Assert.True(vm.CanOk);
        Assert.Equal(0.76, vm.Geometry!.HoleDiameterCm, 12);
    }
}
