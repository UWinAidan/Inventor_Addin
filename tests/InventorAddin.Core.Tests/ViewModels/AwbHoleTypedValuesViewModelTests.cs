using System.Globalization;
using InventorAddin.Core.Holes;
using InventorAddin.Core.ViewModels;

namespace InventorAddin.Core.Tests.ViewModels;

/// <summary>
/// A value or expression typed in a box is kept when the size, type, standard or units change; only boxes the user has
/// not typed follow the table (task 124, Aidan 2026-10-25).
/// </summary>
public sealed class AwbHoleTypedValuesViewModelTests : IDisposable
{
    private const string PartName = "Test bracket.ipt";

    private static readonly HoleWorkbook Workbook = AwbHoleTestWorkbook.Create();

    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;

    public AwbHoleTypedValuesViewModelTests() => CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");

    public void Dispose() => CultureInfo.CurrentCulture = _culture;

    /// <summary>
    /// <c>d0</c> is 20 mm and <c>d0+5 mm</c> 25 mm in either units; <c>d0+1</c> takes its 1 in the units asked for;
    /// <c>metric</c> reads only in Metric, with a reason in Imperial.
    /// </summary>
    private sealed class FakeChecker : IHoleExpressionChecker
    {
        public HoleExpressionCheck Check(string expression, HoleUnits units) => expression switch
        {
            "d0" => HoleExpressionCheck.Valid(2.0),
            "d0+5 mm" => HoleExpressionCheck.Valid(2.5),
            "d0+1" => HoleExpressionCheck.Valid(units == HoleUnits.Metric ? 2.1 : 4.54),
            "metric" when units == HoleUnits.Metric => HoleExpressionCheck.Valid(1.5),
            "metric" => HoleExpressionCheck.Invalid("Units do not match."),
            _ => HoleExpressionCheck.Invalid(),
        };
    }

    private static AwbHoleViewModel BlindTapped(string size = "M5")
    {
        var vm = new AwbHoleViewModel(Workbook, PartName, expressionChecker: new FakeChecker());
        vm.CentreCount = 1;
        vm.Type = HoleStandards.TappedType;
        vm.Termination = HoleTermination.Distance;
        vm.Size = size;
        return vm;
    }

    private static AwbHoleViewModel BlindClearance()
    {
        var vm = new AwbHoleViewModel(Workbook, PartName, expressionChecker: new FakeChecker());
        vm.CentreCount = 1;
        vm.Termination = HoleTermination.Distance;
        return vm;
    }

    private static AwbHoleViewModel Dowel(HoleSeat seat)
    {
        var vm = new AwbHoleViewModel(Workbook, PartName, expressionChecker: new FakeChecker());
        vm.CentreCount = 1;
        vm.Standard = HoleStandards.DowelStandard;
        vm.Size = "5 mm";
        vm.Seat = seat;
        return vm;
    }

    private static HoleSpec EditedTapped(string? threadExpression = "d0", string? drillExpression = "d0+5 mm") =>
        HoleSpec.ClearanceHole(HoleUnits.Metric, "M5") with
        {
            Kind = HoleKind.Tapped,
            Termination = HoleTermination.Distance,
            ThreadDepth = 20m,
            DrillDepth = 25m,
            ThreadDepthExpression = threadExpression,
            DrillDepthExpression = drillExpression,
        };

    // ---- The PR #29 check 100-7 case ----

    [Fact]
    public void AnEditedTappedHolesExpressions_AreKept_WhenTheSizeChanges()
    {
        var vm = AwbHoleViewModel.ForEdit(Workbook, PartName, EditedTapped(), expressionChecker: new FakeChecker());
        vm.CentreCount = 1;

        vm.Size = "M3";

        Assert.Equal("M3", vm.Size);
        Assert.Equal("d0", vm.ThreadDepthText);
        Assert.Equal("d0+5 mm", vm.DrillDepthText);
        Assert.Equal((20m, 25m), (vm.ThreadDepth, vm.DrillDepth));
        Assert.Equal("d0", vm.Spec.ThreadDepthExpression);
        Assert.Equal("d0+5 mm", vm.Spec.DrillDepthExpression);
        Assert.Equal("d0+5 mm", vm.Geometry!.DepthExpression);
        Assert.Equal("d0", vm.Geometry.Thread!.ThreadDepthExpression);
    }

    [Fact]
    public void AnEditedHolesNumbers_CountAsTyped_AndAreKept_WhenTheSizeChanges()
    {
        var spec = EditedTapped(null, null) with { ThreadDepth = 6m, DrillDepth = 8m };
        var vm = AwbHoleViewModel.ForEdit(Workbook, PartName, spec);

        vm.Size = "M3";

        Assert.Equal((6m, 8m), (vm.ThreadDepth, vm.DrillDepth));
    }

    [Fact]
    public void AnEditedHolesDepthsTheSpecLeftBlank_FollowTheTable()
    {
        var spec = EditedTapped(null, null) with { ThreadDepth = null, DrillDepth = null };
        var vm = AwbHoleViewModel.ForEdit(Workbook, PartName, spec);
        Assert.Equal((7.5m, 10m), (vm.ThreadDepth, vm.DrillDepth));

        vm.Size = "M3";

        Assert.Equal((4.5m, 6m), (vm.ThreadDepth, vm.DrillDepth));
    }

    [Fact]
    public void AnEditedExpressionInventorCanNoLongerRead_IsKeptAsItsNumber()
    {
        var spec = HoleSpec.ClearanceHole(HoleUnits.Metric, "T3") with
        {
            Termination = HoleTermination.Distance,
            Depth = 15m,
            DepthExpression = "gone",
        };
        var vm = AwbHoleViewModel.ForEdit(Workbook, PartName, spec, expressionChecker: new FakeChecker());

        vm.Size = "T5";

        Assert.Equal(15m, vm.Depth);
        Assert.Equal("15", vm.DepthText);
        Assert.Null(vm.Spec.DepthExpression);
    }

    // ---- Untouched boxes follow the table ----

    [Fact]
    public void UntouchedTappedDepths_FollowTheNewSize()
    {
        var vm = BlindTapped();
        Assert.Equal((7.5m, 10m), (vm.ThreadDepth, vm.DrillDepth));

        vm.Size = "M3";

        Assert.Equal((4.5m, 6m), (vm.ThreadDepth, vm.DrillDepth));
        Assert.Null(vm.Spec.ThreadDepthExpression);
    }

    [Fact]
    public void OnlyTheTypedBox_IsKept()
    {
        var vm = BlindTapped();
        vm.ThreadDepthText = "d0";

        vm.Size = "M3";

        Assert.Equal("d0", vm.ThreadDepthText);
        Assert.Equal(6m, vm.DrillDepth);
        Assert.Equal("6", vm.DrillDepthText);
    }

    [Fact]
    public void ATypedNumber_IsKept_WhenTheSizeChanges()
    {
        var vm = BlindTapped();
        vm.DrillDepthText = "14";

        vm.Size = "M3";

        Assert.Equal(14m, vm.DrillDepth);
        Assert.Equal(4.5m, vm.ThreadDepth);
    }

    // ---- Clearing a box hands it back to the table ----

    [Fact]
    public void AClearedTappedDepth_TakesTheRowsValue_AndFollowsTheNextSize()
    {
        var vm = BlindTapped();
        vm.ThreadDepthText = "d0";

        vm.ThreadDepthText = "";

        Assert.Equal(7.5m, vm.ThreadDepth);
        Assert.Equal("7.5", vm.ThreadDepthText);
        Assert.Null(vm.Spec.ThreadDepthExpression);

        vm.Size = "M3";

        Assert.Equal(4.5m, vm.ThreadDepth);
    }

    [Fact]
    public void AClearedDepth_FollowsTheBlindDepthRuleAgain_AtTheNextSize()
    {
        var vm = BlindClearance();
        vm.DepthText = "d0";
        vm.Size = "T5";
        Assert.Equal(20m, vm.Depth);

        vm.DepthText = "";
        Assert.Null(vm.Depth);

        vm.Size = "T3";

        Assert.Equal(6m, vm.Depth);
        Assert.Null(vm.Spec.DepthExpression);
    }

    [Fact]
    public void AClearedDowelSeatBox_TakesTheNextSizesStartValue()
    {
        var vm = Dowel(HoleSeat.Counterbore);
        vm.CounterboreDiameterText = "11";
        vm.CounterboreDiameterText = "";
        Assert.Null(vm.CounterboreDiameter);

        vm.Size = "3 mm";

        Assert.Equal(6m, vm.CounterboreDiameter);
    }

    // ---- Type and standard ----

    [Fact]
    public void TypedTappedDepths_ComeBack_AfterATypeChange()
    {
        var vm = BlindTapped();
        vm.ThreadDepthText = "d0";
        vm.DrillDepthText = "d0+5 mm";

        vm.Type = HoleStandards.ClearanceType;

        Assert.Null(vm.ThreadDepth);
        Assert.Null(vm.Spec.ThreadDepthExpression);

        vm.Type = HoleStandards.TappedType;

        Assert.Equal("d0", vm.ThreadDepthText);
        Assert.Equal("d0+5 mm", vm.DrillDepthText);
        Assert.Equal("d0+5 mm", vm.Spec.DrillDepthExpression);
    }

    [Fact]
    public void ATypedDepth_IsKept_AcrossATypeAndAStandardChange()
    {
        var vm = BlindClearance();
        vm.DepthText = "d0";

        vm.Standard = HoleStandards.DowelStandard;
        Assert.Equal("d0", vm.DepthText);
        Assert.Equal("d0", vm.Spec.DepthExpression);

        vm.Type = HoleStandards.TappedType;
        vm.Type = HoleStandards.ClearanceType;

        Assert.Equal("d0", vm.DepthText);
        Assert.Equal(20m, vm.Depth);
    }

    // ---- Units ----

    [Fact]
    public void ATypedNumber_KeepsItsLength_InTheOtherUnits_AndShowsAsTypedOnTheWayBack()
    {
        var vm = BlindTapped();
        vm.ThreadDepthText = "12";

        vm.Units = HoleUnits.Imperial;

        Assert.Equal("#5-40 UNC", vm.Size);
        Assert.Equal("12 mm", vm.ThreadDepthText);
        Assert.Equal(0.47244m, vm.ThreadDepth);
        Assert.Equal("= 0.4724 in", vm.ThreadDepthValueText);
        Assert.Equal("12 mm", vm.Spec.ThreadDepthExpression);
        Assert.Equal(0.25m, vm.DrillDepth);

        vm.Units = HoleUnits.Metric;

        Assert.Equal("12", vm.ThreadDepthText);
        Assert.Equal(12m, vm.ThreadDepth);
        Assert.Null(vm.Spec.ThreadDepthExpression);
    }

    [Fact]
    public void ATypedExpression_IsKeptAsWritten_InTheOtherUnits()
    {
        var vm = BlindTapped();
        vm.ThreadDepthText = "d0";
        vm.DrillDepthText = "d0+5 mm";

        vm.Units = HoleUnits.Imperial;

        Assert.Equal("d0", vm.ThreadDepthText);
        Assert.Equal("d0+5 mm", vm.DrillDepthText);
        Assert.Equal(0.7874m, vm.ThreadDepth);
        Assert.Equal(0.98425m, vm.DrillDepth);
        Assert.Equal("d0+5 mm", vm.Geometry!.DepthExpression);
        Assert.Equal(HoleUnits.Imperial, vm.Geometry.ExpressionUnits);
    }

    [Fact]
    public void AnExpressionTheOtherUnitsCannotRead_ShowsWhy_AndComesBackInItsOwnUnits()
    {
        var vm = BlindClearance();
        vm.DepthText = "metric";
        Assert.Equal(15m, vm.Depth);

        vm.Units = HoleUnits.Imperial;

        Assert.Equal("metric", vm.DepthText);
        Assert.Equal("Units do not match.", vm.DepthProblem);
        Assert.False(vm.CanOk);

        vm.Flip = true;
        Assert.Equal("Units do not match.", vm.DepthProblem);

        vm.Units = HoleUnits.Metric;

        Assert.Equal("metric", vm.DepthText);
        Assert.Null(vm.DepthProblem);
        Assert.Equal(15m, vm.Depth);
        Assert.True(vm.CanOk);
    }

    [Fact]
    public void ADowelsTypedSeatBoxes_AreConverted_TheAngleIsNot()
    {
        var vm = Dowel(HoleSeat.Countersink);
        vm.CountersinkDiameterText = "11";
        vm.CountersinkAngleText = "100";

        vm.Units = HoleUnits.Imperial;

        Assert.Equal("1/8\"", vm.Size);
        Assert.Equal(0.4331m, vm.CountersinkDiameter);
        Assert.Equal("0.4331", vm.CountersinkDiameterText);
        Assert.Equal(100m, vm.CountersinkAngle);

        vm.Units = HoleUnits.Metric;

        Assert.Equal(11m, vm.CountersinkDiameter);
        Assert.Equal(100m, vm.CountersinkAngle);
    }

    [Fact]
    public void ADowelsTypedCounterboreDepth_KeepsItsLengthWithItsUnit()
    {
        var vm = Dowel(HoleSeat.Counterbore);
        vm.CounterboreDepthText = "2";

        vm.Units = HoleUnits.Imperial;

        Assert.Equal("2 mm", vm.CounterboreDepthText);
        Assert.Equal("2 mm", vm.Spec.CboreDepthExpression);
        Assert.Equal(0.07874m, vm.CounterboreDepth);
    }

    [Fact]
    public void AUnitsSwitch_ReadsATypedExpressionOnce()
    {
        var calls = 0;
        var checker = new CountingChecker(() => calls++);
        var vm = new AwbHoleViewModel(Workbook, PartName, expressionChecker: checker);
        vm.Termination = HoleTermination.Distance;
        vm.DepthText = "d0";
        calls = 0;

        vm.Units = HoleUnits.Imperial;
        vm.Flip = true;
        vm.Size = "#3";

        Assert.Equal(1, calls);
    }

    private sealed class CountingChecker(Action called) : IHoleExpressionChecker
    {
        public HoleExpressionCheck Check(string expression, HoleUnits units)
        {
            called();
            return HoleExpressionCheck.Valid(2.0);
        }
    }
}
