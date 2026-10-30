using System.Globalization;
using InventorAddin.Core.Holes;
using InventorAddin.Core.ViewModels;

namespace InventorAddin.Core.Tests.ViewModels;

/// <summary>
/// The seat boxes on clearance and tapped holes (spec 09, "The window": Seat sizes; task 126): they start from the row
/// for the Fastener type and size, stay editable, and a changed fastener or size refills only the boxes not typed.
/// </summary>
public sealed class AwbHoleSeatBoxesViewModelTests : IDisposable
{
    private const string PartName = "Test bracket.ipt";

    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;

    // T5: Counterbore TCAP 9.5 x 5 (Normal), TLOW 8.5 x 3 (Close), THID hidden; Countersink TFLAT 10.4 at 90 (Loose).
    // T3: Counterbore TCAP 6 x 3. M3 and M5 take the T3 and T5 rows; #5-40 UNC has none.
    private static readonly HoleWorkbook Workbook = AwbHoleTestWorkbook.Create();

    public AwbHoleSeatBoxesViewModelTests() => CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");

    public void Dispose() => CultureInfo.CurrentCulture = _culture;

    /// <summary>Knows <c>d0</c> as 20 mm and <c>d0/8</c> as 2.5 mm; anything else is not a length.</summary>
    private sealed class FakeChecker : IHoleExpressionChecker
    {
        public HoleExpressionCheck Check(string expression, HoleUnits units) => expression switch
        {
            "d0" => HoleExpressionCheck.Valid(2.0),
            "d0/8" => HoleExpressionCheck.Valid(0.25),
            _ => HoleExpressionCheck.Invalid(),
        };
    }

    private static AwbHoleViewModel Clearance(
        HoleSeat seat, string size = "T5", IHoleExpressionChecker? checker = null)
    {
        var vm = new AwbHoleViewModel(Workbook, PartName, expressionChecker: checker) { CentreCount = 1 };
        vm.Seat = seat;
        vm.Size = size;
        return vm;
    }

    private static AwbHoleViewModel Tapped(string size, HoleSeat seat, HoleLastDowelSeats? last = null)
    {
        var vm = new AwbHoleViewModel(Workbook, PartName, lastDowelSeats: last) { CentreCount = 1 };
        if (size.StartsWith('#'))
        {
            vm.Units = HoleUnits.Imperial;
        }

        vm.Type = HoleStandards.TappedType;
        vm.Size = size;
        vm.Seat = seat;
        return vm;
    }

    [Fact]
    public void ACounterboredClearanceHole_ShowsItsBoxesFilledFromTheRow()
    {
        var vm = Clearance(HoleSeat.Counterbore);

        Assert.True(vm.ShowsFastener);
        Assert.Equal("TCAP", vm.Fastener);
        Assert.True(vm.ShowsCounterboreSizes);
        Assert.False(vm.ShowsCountersinkSizes);
        Assert.Equal("9.5", vm.CounterboreDiameterText);
        Assert.Equal("5", vm.CounterboreDepthText);
        Assert.Equal((9.5m, 5m), (vm.Spec.CboreDiameter, vm.Spec.CboreDepth));
        Assert.True(vm.CanOk);
    }

    [Fact]
    public void ChangingABox_ChangesTheHole_AndTheResolvedSpec()
    {
        var vm = Clearance(HoleSeat.Counterbore);
        var previews = 0;
        vm.PreviewNeeded += (_, _) => previews++;

        vm.CounterboreDiameterText = "11";

        Assert.Equal(1, previews);
        Assert.Equal(1.1, vm.Geometry!.CounterboreDiameterCm!.Value, 12);
        Assert.Equal(0.55, vm.Geometry.HoleDiameterCm, 12);                    // the through hole is still the row's
        Assert.Equal((11m, 5m, "TCAP"), (vm.BuildResult.Resolved.CboreDiameter, vm.BuildResult.Resolved.CboreDepth,
            vm.BuildResult.Resolved.Fastener));
        Assert.Equal(11m, vm.DiagramInput.CounterboreDiameter);
    }

    [Fact]
    public void ChangingTheFastener_RefillsTheBoxesNotTyped_AndKeepsTheTypedOnes()
    {
        var vm = Clearance(HoleSeat.Counterbore);
        vm.CounterboreDiameterText = "11";

        vm.Fastener = "TLOW";

        Assert.Equal(11m, vm.CounterboreDiameter);          // typed: kept
        Assert.Equal(3m, vm.CounterboreDepth);              // not typed: TLOW's
        Assert.Equal(0.53, vm.Geometry!.HoleDiameterCm, 12);  // TLOW's through hole, at Close

        vm.CounterboreDiameterText = "";
        vm.Fastener = "TCAP";

        Assert.Equal((9.5m, 5m), (vm.CounterboreDiameter, vm.CounterboreDepth));
    }

    [Fact]
    public void ChangingTheSize_RefillsTheBoxesNotTyped()
    {
        var vm = Clearance(HoleSeat.Counterbore);
        vm.CounterboreDepthText = "4";

        vm.Size = "T3";

        Assert.Equal((6m, 4m), (vm.CounterboreDiameter, vm.CounterboreDepth));
    }

    [Fact]
    public void ACountersinkBox_TakesNumbers_AndItsAngleIsChecked()
    {
        var vm = Clearance(HoleSeat.Countersink);
        Assert.Equal("TFLAT", vm.Fastener);
        Assert.Equal((10.4m, 90m), (vm.CountersinkDiameter, vm.CountersinkAngle));

        vm.CountersinkAngleText = "0.5";

        Assert.Contains("between 1 and 179 degrees", vm.CountersinkAngleProblem);
        Assert.False(vm.CanOk);

        vm.CountersinkAngleText = "82";
        vm.CountersinkDiameterText = "5";

        Assert.Null(vm.CountersinkAngleProblem);
        Assert.Contains("not smaller than its countersink", vm.CountersinkDiameterProblem);
    }

    [Fact]
    public void AClearanceCounterboreDepth_TakesAnExpression()
    {
        var vm = Clearance(HoleSeat.Counterbore, checker: new FakeChecker());

        vm.CounterboreDepthText = "d0/8";

        Assert.Equal("d0/8", vm.CounterboreDepthText);
        Assert.Equal("= 2.5 mm", vm.CounterboreDepthValueText);
        Assert.Equal("d0/8", vm.Spec.CboreDepthExpression);
        Assert.Equal("d0/8", vm.Geometry!.CounterboreDepthExpression);
        Assert.True(vm.DiagramInput.CounterboreDepthIsExpression);
    }

    [Fact]
    public void ATappedHole_StartsFromTheRowOfTheSameSize_AndItsFastener()
    {
        var vm = Tapped("M5", HoleSeat.Counterbore);

        Assert.Equal("TCAP", vm.Fastener);
        Assert.Equal((9.5m, 5m), (vm.CounterboreDiameter, vm.CounterboreDepth));

        vm.Fastener = "TLOW";

        Assert.Equal((8.5m, 3m), (vm.CounterboreDiameter, vm.CounterboreDepth));
    }

    [Fact]
    public void ATappedHole_TakesACountersinkWithNoRowOfItsSize_TypedInTheBoxes()
    {
        // M3 has no countersink row of its size (acceptance: a lead-in typed on a tapped hole).
        var vm = Tapped("M3", HoleSeat.Countersink);

        Assert.Equal(HoleSeat.Countersink, vm.Seat);
        Assert.False(vm.ShowsFastener);
        Assert.True(vm.ShowsCountersinkSizes);
        Assert.Equal("", vm.CountersinkDiameterText);
        Assert.False(vm.CanOk);

        vm.CountersinkDiameterText = "4";
        vm.CountersinkAngleText = "90";

        Assert.True(vm.CanOk);
        Assert.Equal(0.4, vm.Geometry!.CountersinkDiameterCm!.Value, 12);
        Assert.Equal(90, vm.Geometry.CountersinkAngleDeg!.Value, 12);
        Assert.Null(vm.BuildResult.Resolved.Fastener);
        Assert.Equal((4m, 90m), (vm.BuildResult.Resolved.CsinkDiameter, vm.BuildResult.Resolved.CsinkAngleDeg));
    }

    [Fact]
    public void ATappedSeatWithNoRow_StartsFromTheLastValuesUsed_ForItsUnitsAndThread()
    {
        var last = new HoleLastDowelSeats(
            null, null,
            new Dictionary<string, HoleDowelSeat> { ["M3"] = new(null, null, 4.5m, 90m) },
            new Dictionary<string, HoleDowelSeat> { ["#5-40 UNC"] = new(0.25m, 0.05m, null, null) });

        var metric = Tapped("M3", HoleSeat.Countersink, last);
        Assert.Equal((4.5m, 90m), (metric.CountersinkDiameter, metric.CountersinkAngle));
        Assert.True(metric.CanOk);

        var imperial = Tapped("#5-40 UNC", HoleSeat.Counterbore, last);
        Assert.Equal((0.25m, 0.05m), (imperial.CounterboreDiameter, imperial.CounterboreDepth));

        // A size with a row starts from the row, not the last values.
        var withRow = Tapped("M5", HoleSeat.Countersink, last);
        Assert.Equal((10.4m, 90m), (withRow.CountersinkDiameter, withRow.CountersinkAngle));
    }

    [Fact]
    public void AnEditedClearanceSeat_OpensOnItsChangedValues()
    {
        var spec = HoleSpec.ClearanceHole(HoleUnits.Metric, "T5") with
        {
            Seat = HoleSeat.Counterbore,
            Fastener = "TCAP",
            CboreDiameter = 11m,
            CboreDepth = 5m,
        };

        var vm = AwbHoleViewModel.ForEdit(Workbook, PartName, spec);

        Assert.Equal("11", vm.CounterboreDiameterText);
        Assert.Equal("5", vm.CounterboreDepthText);
        Assert.Equal(spec.Normalised(), vm.Spec);

        // The changed diameter counts as typed; the depth, equal to the row's, follows the fastener.
        vm.Fastener = "TLOW";
        Assert.Equal((11m, 3m), (vm.CounterboreDiameter, vm.CounterboreDepth));
    }

    [Fact]
    public void SeatBoxes_ShowForEverySeatedType_AndNotWithoutASeat()
    {
        var vm = Clearance(HoleSeat.None);
        Assert.False(vm.ShowsCounterboreSizes);
        Assert.False(vm.ShowsCountersinkSizes);
        Assert.Null(vm.Spec.CboreDiameter);

        vm.Seat = HoleSeat.Countersink;
        Assert.True(vm.ShowsCountersinkSizes);

        vm.Type = HoleStandards.TappedType;
        Assert.True(vm.ShowsCountersinkSizes);

        vm.Seat = HoleSeat.Counterbore;
        Assert.True(vm.ShowsCounterboreSizes);
        Assert.False(vm.ShowsCountersinkSizes);
    }
}
