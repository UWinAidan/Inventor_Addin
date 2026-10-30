using System.Globalization;
using InventorAddin.Core.Holes;
using InventorAddin.Core.ViewModels;

namespace InventorAddin.Core.Tests.ViewModels;

/// <summary>A dowel's seat is sized in its own boxes in the AWB Hole window (spec 09, question 9 and 14; task 106).</summary>
public sealed class AwbHoleDowelSeatViewModelTests : IDisposable
{
    private const string PartName = "Test bracket.ipt";

    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;

    // 3 mm: T3's counterbore row (6 x 3), no countersink row. 5 mm: T5's rows (9.5 x 5, 10.4 at 90). 1/8": no rows.
    private static readonly HoleWorkbook Workbook = AwbHoleTestWorkbook.Create();

    public AwbHoleDowelSeatViewModelTests() => CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");

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

    /// <summary>The workbook with seat columns on the 5 mm Dowel row: a counterbore depth and a countersink angle only.</summary>
    private static HoleWorkbook WithDowelColumns() => Workbook with
    {
        Dowel =
        [
            .. Workbook.Dowel.Select(r => r.Size == "5 mm" ? r with { CboreDepth = 1.5m, CsinkAngleDeg = 100m } : r),
        ],
    };

    private static AwbHoleViewModel Dowel(
        HoleSeat seat,
        string size = "5 mm",
        HoleWorkbook? workbook = null,
        HoleLastDowelSeats? last = null,
        IHoleExpressionChecker? checker = null)
    {
        var vm = new AwbHoleViewModel(
            workbook ?? Workbook, PartName, expressionChecker: checker, lastDowelSeats: last);
        vm.CentreCount = 1;
        vm.Standard = HoleStandards.DowelStandard;
        vm.Size = size;
        vm.Seat = seat;
        return vm;
    }

    private static HoleLastDowelSeats Last(string size, HoleDowelSeat seat) =>
        new(new Dictionary<string, HoleDowelSeat> { [size] = seat }, null);

    [Fact]
    public void Counterbore_ShowsItsTwoBoxes_Countersink_ItsTwo_None_Neither_AndNeverFastenerType()
    {
        var vm = Dowel(HoleSeat.Counterbore);

        Assert.True(vm.ShowsCounterboreSizes);
        Assert.False(vm.ShowsCountersinkSizes);
        Assert.False(vm.ShowsFastener);
        Assert.Equal("9.5", vm.CounterboreDiameterText);
        Assert.Equal("5", vm.CounterboreDepthText);

        vm.Seat = HoleSeat.Countersink;

        Assert.False(vm.ShowsCounterboreSizes);
        Assert.True(vm.ShowsCountersinkSizes);
        Assert.False(vm.ShowsFastener);
        Assert.Equal("10.4", vm.CountersinkDiameterText);
        Assert.Equal("90", vm.CountersinkAngleText);
        Assert.Equal("deg", AwbHoleViewModel.AngleUnitText);

        vm.Seat = HoleSeat.None;

        Assert.False(vm.ShowsCounterboreSizes);
        Assert.False(vm.ShowsCountersinkSizes);
        Assert.False(vm.ShowsFastener);
        Assert.Null(vm.Spec.CsinkDiameter);
    }

    [Fact]
    public void ClearanceAndTappedHoles_KeepFastenerType_AndHaveNoSeatBoxes()
    {
        var vm = new AwbHoleViewModel(Workbook, PartName) { Size = "T5" };
        vm.Seat = HoleSeat.Counterbore;

        Assert.True(vm.ShowsFastener);
        Assert.False(vm.ShowsCounterboreSizes);
        Assert.Null(vm.CounterboreDiameter);

        vm.CounterboreDiameter = 12m;   // not a dowel: ignored
        Assert.Null(vm.CounterboreDiameter);

        vm.Type = HoleStandards.TappedType;
        vm.Size = "M5";
        vm.Seat = HoleSeat.Countersink;

        Assert.True(vm.ShowsFastener);
        Assert.False(vm.ShowsCountersinkSizes);
        Assert.Equal("TFLAT", vm.Fastener);
    }

    [Fact]
    public void StartValues_TheLastUsed_ThenTheDowelColumns_ThenTheSameSizeRow_ValueByValue()
    {
        var last = Last("5 MM", new HoleDowelSeat(8m, null, null, null));

        var vm = Dowel(HoleSeat.Counterbore, workbook: WithDowelColumns(), last: last);

        // Diameter: the last used. Depth: the Dowel tab's column. Countersink: the column's angle, the row's diameter.
        Assert.Equal((8m, 1.5m), (vm.CounterboreDiameter, vm.CounterboreDepth));
        Assert.Equal((10.4m, 100m), (vm.CountersinkDiameter, vm.CountersinkAngle));
        Assert.Equal(0.8, vm.Geometry!.CounterboreDiameterCm!.Value, 12);
        Assert.Equal(0.15, vm.Geometry.CounterboreDepthCm!.Value, 12);
    }

    [Fact]
    public void StartValues_NoneKnown_LeaveTheBoxesEmpty_WithAMessage_AndOkDisabled()
    {
        var vm = Dowel(HoleSeat.Countersink, size: "3 mm");

        Assert.Equal("", vm.CountersinkDiameterText);
        Assert.Equal("", vm.CountersinkAngleText);
        Assert.Equal("Enter a countersink diameter.", vm.CountersinkDiameterProblem);
        Assert.Equal("Enter a countersink angle.", vm.CountersinkAngleProblem);
        Assert.Null(vm.Geometry);
        Assert.False(vm.CanOk);

        vm.CountersinkDiameterText = "4";
        vm.CountersinkAngleText = "90";

        Assert.Null(vm.CountersinkDiameterProblem);
        Assert.Null(vm.CountersinkAngleProblem);
        Assert.True(vm.CanOk);
        Assert.Equal(0.4, vm.Geometry!.CountersinkDiameterCm!.Value, 12);
    }

    [Fact]
    public void AChangedValue_PreviewsOnce_AndGoesToTheSpecAndGeometry()
    {
        var vm = Dowel(HoleSeat.Counterbore);
        var previews = 0;
        vm.PreviewNeeded += (_, _) => previews++;

        vm.CounterboreDiameterText = "11.25";

        Assert.Equal(1, previews);
        Assert.Equal(11.25m, vm.Spec.CboreDiameter);
        Assert.Equal(1.125, vm.Geometry!.CounterboreDiameterCm!.Value, 12);
        Assert.NotNull(vm.Geometry.FitColour);   // the seat is coloured by fit with the rest

        vm.CounterboreDiameterText = "11.250";   // the same number

        Assert.Equal(1, previews);
        Assert.Equal("11.25", vm.CounterboreDiameterText);
    }

    [Fact]
    public void TypedValues_AreKept_AcrossSeatFitAndSizeChanges_WhileUntypedBoxesTakeTheSizesStartValues()
    {
        var vm = Dowel(HoleSeat.Counterbore);
        vm.CounterboreDiameterText = "11";

        vm.Seat = HoleSeat.Countersink;
        vm.Fit = "Press";
        vm.Seat = HoleSeat.Counterbore;

        Assert.Equal(11m, vm.CounterboreDiameter);

        // Task 124: the typed diameter stays; the depth, not typed, follows the size.
        vm.Size = "3 mm";

        Assert.Equal((11m, 3m), (vm.CounterboreDiameter, vm.CounterboreDepth));

        vm.Size = "5 mm";

        Assert.Equal((11m, 5m), (vm.CounterboreDiameter, vm.CounterboreDepth));
    }

    [Fact]
    public void AUnitsChange_TakesTheNewSizesStartValues()
    {
        var last = new HoleLastDowelSeats(null, new Dictionary<string, HoleDowelSeat> { ["1/8\""] = new(0.2m, 0.05m, null, null) });
        var vm = Dowel(HoleSeat.Counterbore, size: "3 mm", last: last);

        vm.Units = HoleUnits.Imperial;

        Assert.Equal("1/8\"", vm.Size);
        Assert.Equal((0.2m, 0.05m), (vm.CounterboreDiameter, vm.CounterboreDepth));

        vm.Units = HoleUnits.Metric;

        Assert.Equal("3 mm", vm.Size);
        Assert.Equal((6m, 3m), (vm.CounterboreDiameter, vm.CounterboreDepth));
    }

    [Fact]
    public void LeavingAwbDowel_HidesTheTypedValues_AndComingBackBringsThemBack()
    {
        var vm = Dowel(HoleSeat.Counterbore);
        vm.CounterboreDiameterText = "11";

        vm.Standard = HoleStandards.ClearanceStandard;

        Assert.Null(vm.CounterboreDiameter);
        Assert.Equal(HoleSeat.Counterbore, vm.Seat);
        Assert.True(vm.ShowsFastener);
        Assert.Null(vm.Spec.CboreDiameter);

        vm.Standard = HoleStandards.DowelStandard;
        vm.Size = "5 mm";

        // Task 124: the typed diameter comes back; the depth, not typed, takes the size's start value.
        Assert.Equal((11m, 5m), (vm.CounterboreDiameter, vm.CounterboreDepth));
    }

    [Fact]
    public void TheDiametersAndTheAngle_TakeNumbersOnly_EvenWithAnExpressionChecker()
    {
        var vm = Dowel(HoleSeat.Countersink, checker: new FakeChecker());

        vm.CountersinkDiameterText = "d0";
        vm.CountersinkAngleText = "steep";

        Assert.Equal("d0", vm.CountersinkDiameterText);
        Assert.Equal(AwbHoleViewModel.NotANumberText, vm.CountersinkDiameterProblem);
        Assert.Equal(AwbHoleViewModel.NotANumberText, vm.CountersinkAngleProblem);
        Assert.Equal(10.4m, vm.CountersinkDiameter);
        Assert.True(vm.HasTextThatIsNotANumber);
        Assert.False(vm.CanOk);

        vm.CountersinkDiameterText = "12";

        Assert.Null(vm.CountersinkDiameterProblem);
        Assert.Equal(12m, vm.CountersinkDiameter);
    }

    [Fact]
    public void TheCounterboreDepth_TakesExpressions_AndTheHoleStaysParametric()
    {
        var vm = Dowel(HoleSeat.Counterbore, checker: new FakeChecker());

        vm.CounterboreDepthText = "d0/8";

        Assert.Equal("d0/8", vm.CounterboreDepthText);
        Assert.Equal(2.5m, vm.CounterboreDepth);
        Assert.Equal("d0/8", vm.Spec.CboreDepthExpression);
        Assert.Equal("d0/8", vm.Geometry!.CounterboreDepthExpression);
        Assert.Equal(0.25, vm.Geometry.CounterboreDepthCm!.Value, 12);

        vm.CounterboreDepthText = "nope";

        Assert.Equal(HoleExpressions.NotALengthText, vm.CounterboreDepthProblem);
        Assert.False(vm.CanOk);

        vm.CounterboreDepthText = "3";

        Assert.Null(vm.Spec.CboreDepthExpression);
        Assert.Null(vm.Geometry!.CounterboreDepthExpression);
    }

    [Fact]
    public void ASizeChange_KeepsACounterboreDepthExpression()
    {
        var vm = Dowel(HoleSeat.Counterbore, checker: new FakeChecker());
        vm.CounterboreDepthText = "d0/8";

        vm.Size = "3 mm";

        Assert.Equal("d0/8", vm.CounterboreDepthText);
        Assert.Equal(2.5m, vm.CounterboreDepth);
        Assert.Equal("d0/8", vm.Spec.CboreDepthExpression);
        Assert.Equal(6m, vm.CounterboreDiameter);
    }

    [Fact]
    public void Checks_AreShownUnderTheirBoxes()
    {
        var vm = Dowel(HoleSeat.Counterbore);

        vm.CounterboreDiameterText = "5";
        Assert.Contains("not smaller than its counterbore", vm.CounterboreDiameterProblem);
        Assert.False(vm.CanOk);

        vm.CounterboreDiameterText = "9";
        vm.CounterboreDepthText = "0";
        Assert.Null(vm.CounterboreDiameterProblem);
        Assert.Equal("The counterbore depth is not greater than zero.", vm.CounterboreDepthProblem);

        vm.Seat = HoleSeat.Countersink;
        vm.CountersinkAngleText = "180";
        Assert.Contains("between 0 and 180", vm.CountersinkAngleProblem);
        Assert.False(vm.CanOk);

        vm.CountersinkAngleText = "";
        Assert.Equal("Enter a countersink angle.", vm.CountersinkAngleProblem);
    }

    [Fact]
    public void AnEditedDowel_OpensOnItsOwnSeat_NotTheStartValues()
    {
        var spec = HoleSpec.ClearanceHole(HoleUnits.Metric, "5 mm") with
        {
            Kind = HoleKind.Dowel,
            Seat = HoleSeat.Counterbore,
            CboreDiameter = 7.25m,
            CboreDepth = 1.5m,
            Tolerance = "N bore",
        };
        var last = Last("5 mm", new HoleDowelSeat(8m, 2m, 6m, 80m));

        var vm = AwbHoleViewModel.ForEdit(Workbook, PartName, spec, lastDowelSeats: last);
        vm.CentreCount = 1;

        Assert.Equal((7.25m, 1.5m), (vm.CounterboreDiameter, vm.CounterboreDepth));
        Assert.Equal(7.25m, vm.Spec.CboreDiameter);
        Assert.NotNull(vm.Geometry);

        // A change that keeps the size keeps the hole's values; the seat it does not have starts as a new one would.
        vm.Fit = "Press";
        Assert.Equal(7.25m, vm.CounterboreDiameter);
        vm.Seat = HoleSeat.Countersink;
        Assert.Equal((6m, 80m), (vm.CountersinkDiameter, vm.CountersinkAngle));
    }

    [Fact]
    public void AnEditedDowel_ChecksItsCounterboreDepthExpressionAgain()
    {
        var spec = HoleSpec.ClearanceHole(HoleUnits.Metric, "5 mm") with
        {
            Kind = HoleKind.Dowel,
            Seat = HoleSeat.Counterbore,
            CboreDiameter = 9m,
            CboreDepth = 3m,
            CboreDepthExpression = "d0/8",
            Tolerance = "N bore",
        };

        var vm = AwbHoleViewModel.ForEdit(Workbook, PartName, spec, expressionChecker: new FakeChecker());

        // The parameters moved since: d0/8 is 2.5 mm now.
        Assert.Equal("d0/8", vm.CounterboreDepthText);
        Assert.Equal(2.5m, vm.CounterboreDepth);
        Assert.Equal("d0/8", vm.Spec.CboreDepthExpression);
    }

    [Fact]
    public void AnEditedDowelReadBackFromAnOldHole_OpensOnTheHolesSeat()
    {
        // A dowel stored by task 099 has a fastener and no seat sizes; the read-back fills them from the hole.
        var facts = new InventorHoleFacts(
            HoleSeat.Counterbore, false, null, null, null, 0.5, HoleTermination.ThroughAll, null,
            0.85, 0.3, null, null, false, null, "H7", 0.0012, 0, false);
        var stored = HoleSpec.ClearanceHole(HoleUnits.Metric, "5 mm") with
        {
            Kind = HoleKind.Dowel,
            Seat = HoleSeat.Counterbore,
            Fastener = "TLOW",
            Tolerance = "N bore",
        };
        var data = new HoleStoredData(stored, new HoleAppliedState(0.5, "H7", 0.0012, 0, true));
        var readBack = new HoleReadBackResult(HoleReadBack.WithDowelSeatFromHole(stored, facts), HoleSource.Awb, data, []);

        var vm = AwbHoleViewModel.ForReadBack(Workbook, PartName, readBack);

        Assert.Equal((8.5m, 3m), (vm.CounterboreDiameter, vm.CounterboreDepth));
        Assert.Null(vm.Fastener);
        Assert.False(vm.ShowsFastener);
    }

    [Fact]
    public void TheBoxes_RaiseTheirTextsAndProblems()
    {
        var vm = Dowel(HoleSeat.Counterbore);
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.CounterboreDepthText = "2";

        Assert.Contains(nameof(AwbHoleViewModel.CounterboreDepthText), changed);
        Assert.Contains(nameof(AwbHoleViewModel.CounterboreDepthProblem), changed);
        Assert.Contains(nameof(AwbHoleViewModel.ShowsCounterboreSizes), changed);
        Assert.Contains(nameof(AwbHoleViewModel.CanOk), changed);
    }
}
