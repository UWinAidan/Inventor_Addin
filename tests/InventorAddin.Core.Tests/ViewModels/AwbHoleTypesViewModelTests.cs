using System.Globalization;
using InventorAddin.Core.Holes;
using InventorAddin.Core.ViewModels;

namespace InventorAddin.Core.Tests.ViewModels;

/// <summary>
/// The Simple and Taper Tapped types and the Spotface seat in the AWB Hole window (task 112; spec 09, "The window",
/// "Types and seats", "Seat boxes", "Simple holes", "Taper tapped holes"): which fields show for each type, where the
/// boxes start, and what is kept across changes. Sizes and values are invented.
/// </summary>
public sealed class AwbHoleTypesViewModelTests : IDisposable
{
    private const string PartName = "Test bracket.ipt";

    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;

    // T5 and M5 and the 5 mm dowel share the T5 Spotface row (11 x 0.5); T3 and M3 have none.
    private static readonly HoleWorkbook Workbook = AwbHoleTestWorkbook.Create() with
    {
        Spotface =
        [
            new SpotfaceRow(5, HoleUnits.Metric, "T5", 11m, 0.5m, false),
            new SpotfaceRow(6, HoleUnits.Imperial, "#5", 0.45m, 0.02m, false),
        ],
        TaperTapped =
        [
            new TaperTappedRow(5, HoleUnits.Metric, "Rc 1/8", "BSP Taper Internal", 8.4m, 12.6m, 16.8m, false),
            new TaperTappedRow(6, HoleUnits.Metric, "Rc 1/4", "BSP Taper Internal", 11.2m, 16.8m, 22.4m, false),
            new TaperTappedRow(7, HoleUnits.Imperial, "1/4", "NPT", 0.44m, 0.66m, 0.88m, false),
        ],
    };

    public AwbHoleTypesViewModelTests() => CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");

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

    private static List<string> Values<T>(IEnumerable<HoleOption<T>> options) =>
        options.Select(o => o.Text).ToList();

    private static AwbHoleViewModel NewHole(
        HoleWorkbook? workbook = null,
        HoleLastDepths? lastDepths = null,
        HoleLastDowelSeats? lastSeats = null,
        IHoleExpressionChecker? checker = null) =>
        new(workbook ?? Workbook, PartName, lastDepths: lastDepths, expressionChecker: checker, lastDowelSeats: lastSeats)
        {
            CentreCount = 1,
        };

    private static AwbHoleViewModel OfType(string type, HoleLastDepths? lastDepths = null,
        HoleLastDowelSeats? lastSeats = null, IHoleExpressionChecker? checker = null)
    {
        var vm = NewHole(lastDepths: lastDepths, lastSeats: lastSeats, checker: checker);
        vm.Type = type;
        return vm;
    }

    // ---- Simple ----

    [Fact]
    public void Simple_HasNoStandardSizeFitOrFastener_ButADiameterBox()
    {
        var vm = OfType(HoleStandards.SimpleType);

        Assert.Equal(HoleKind.Simple, vm.Kind);
        Assert.False(vm.ShowsStandard);
        Assert.Empty(vm.StandardOptions);
        Assert.False(vm.ShowsSize);
        Assert.Empty(vm.SizeOptions);
        Assert.Null(vm.Size);
        Assert.False(vm.ShowsFit);
        Assert.False(vm.ShowsFastener);
        Assert.False(vm.ShowsTolerance);
        Assert.True(vm.ShowsDiameter);
        Assert.False(vm.ShowsThreadDepth);
        Assert.Equal(["None", "Counterbore", "Countersink", "Spotface"], Values(vm.SeatOptions));
    }

    [Fact]
    public void Simple_StartsEmpty_AndOkWaitsForADiameter()
    {
        var vm = OfType(HoleStandards.SimpleType);

        Assert.Null(vm.Diameter);
        Assert.Equal("", vm.DiameterText);
        Assert.False(vm.CanOk);
        Assert.Equal("Enter a diameter.", vm.DiameterProblem);

        vm.DiameterText = "6.5";

        Assert.True(vm.CanOk);
        Assert.Equal((HoleKind.Simple, 6.5m, ""), (vm.Spec.Kind, vm.Spec.Diameter, vm.Spec.Size));
        Assert.Equal(0.65, vm.Geometry!.HoleDiameterCm, 12);
    }

    [Fact]
    public void Simple_StartsAtTheLastDiameterOfItsUnits()
    {
        var last = new HoleLastDepths(null, null) { SimpleDiameterMetric = 8m, SimpleDiameterImperial = 0.3m };

        var vm = OfType(HoleStandards.SimpleType, lastDepths: last);
        Assert.Equal(8m, vm.Diameter);

        vm.Units = HoleUnits.Imperial;
        Assert.Equal(0.3m, vm.Diameter);
    }

    [Fact]
    public void Simple_ATypedDiameter_IsKeptAcrossTypeAndUnitsChanges()
    {
        var last = new HoleLastDepths(null, null) { SimpleDiameterMetric = 8m };
        var vm = OfType(HoleStandards.SimpleType, lastDepths: last);
        vm.DiameterText = "6.5";

        vm.Type = HoleStandards.TappedType;
        Assert.Null(vm.Diameter);
        Assert.False(vm.ShowsDiameter);

        vm.Type = HoleStandards.SimpleType;
        Assert.Equal(6.5m, vm.Diameter);

        vm.Units = HoleUnits.Imperial;
        Assert.Equal("6.5 mm", vm.DiameterText);
    }

    [Fact]
    public void Simple_TheDiameterTakesExpressions()
    {
        var vm = OfType(HoleStandards.SimpleType, checker: new FakeChecker());

        vm.DiameterText = "d0";

        Assert.Equal(20m, vm.Diameter);
        Assert.Equal("d0", vm.DiameterText);
        Assert.Equal("= 20 mm", vm.DiameterValueText);
        Assert.Equal("d0", vm.Spec.DiameterExpression);
        Assert.Equal("d0", vm.Geometry!.DiameterExpression);
    }

    [Fact]
    public void Simple_ABlindHole_StartsAtTheMultiplierTimesTheDiameter_AndFollowsIt()
    {
        var vm = OfType(HoleStandards.SimpleType);
        vm.DiameterText = "6";
        vm.Termination = HoleTermination.Distance;

        Assert.True(vm.ShowsDepth);
        Assert.Equal(12m, vm.Depth);

        vm.DiameterText = "7";
        Assert.Equal(14m, vm.Depth);
    }

    [Fact]
    public void Simple_ASeat_StartsAtTheLastSimpleSeatOfItsUnits_ElseEmpty()
    {
        var last = new HoleLastDowelSeats(
            null, null, simpleMetric: new HoleDowelSeat(10m, 2m, null, null) with { SpotfaceDiameter = 12m, SpotfaceDepth = 0m });
        var vm = OfType(HoleStandards.SimpleType, lastSeats: last);
        vm.DiameterText = "6";

        vm.Seat = HoleSeat.Spotface;
        Assert.True(vm.ShowsSpotfaceSizes);
        Assert.Equal((12m, 0m), (vm.SpotfaceDiameter, vm.SpotfaceDepth));
        Assert.True(vm.CanOk);

        vm.Seat = HoleSeat.Countersink;
        Assert.Null(vm.CountersinkDiameter);
        Assert.False(vm.CanOk);
        Assert.False(vm.ShowsFastener);
    }

    [Fact]
    public void Simple_EditedHole_OpensOnItsOwnValues()
    {
        var spec = HoleSpec.ClearanceHole(HoleUnits.Metric, "") with
        {
            Kind = HoleKind.Simple,
            Diameter = 20m,
            DiameterExpression = "d0",
            Seat = HoleSeat.Spotface,
            SpotfaceDiameter = 26m,
            SpotfaceDepth = 2.5m,
            SpotfaceDepthExpression = "d0/8",
        };

        var vm = AwbHoleViewModel.ForEdit(Workbook, PartName, spec, expressionChecker: new FakeChecker());

        Assert.Equal(HoleStandards.SimpleType, vm.Type);
        Assert.Equal(HoleKind.Simple, vm.Kind);
        Assert.Equal("d0", vm.DiameterText);
        Assert.Equal(("26", "d0/8"), (vm.SpotfaceDiameterText, vm.SpotfaceDepthText));
        Assert.Equal(spec.Normalised(), vm.Spec);
    }

    // ---- Taper Tapped ----

    [Fact]
    public void TaperTapped_ListsItsStandardAndSizes_AndCounterboreDisabled()
    {
        var vm = OfType(HoleStandards.TaperTappedType);

        Assert.Equal(HoleKind.TaperTapped, vm.Kind);
        Assert.Equal([HoleStandards.TaperTappedStandard], Values(vm.StandardOptions));
        Assert.True(vm.ShowsStandard);
        Assert.Equal(["Rc 1/8", "Rc 1/4"], Values(vm.SizeOptions));
        Assert.Equal("Rc 1/8", vm.Size);
        Assert.Equal(
            ["None", AwbHoleViewModel.CounterboreNotOnTaperText, "Countersink", "Spotface"], Values(vm.SeatOptions));
        Assert.Equal([true, false, true, true], vm.SeatOptions.Select(o => o.IsEnabled).ToList());
        Assert.False(vm.ShowsFit);
        Assert.False(vm.ShowsTolerance);
        Assert.False(vm.ShowsFastener);
        Assert.False(vm.ShowsDiameter);
        Assert.True(vm.CanOk);
        Assert.True(vm.Geometry!.Thread!.IsTapered);
    }

    [Fact]
    public void TaperTapped_ABlindHole_HasADrillDepthFromItsRow_AndNoThreadDepth()
    {
        var vm = OfType(HoleStandards.TaperTappedType);
        vm.Size = "Rc 1/4";
        vm.Termination = HoleTermination.Distance;

        Assert.True(vm.ShowsDrillDepth);
        Assert.False(vm.ShowsThreadDepth);
        Assert.False(vm.ShowsDepth);
        Assert.Equal(22.4m, vm.DrillDepth);
        Assert.Null(vm.ThreadDepth);

        vm.DrillDepthText = "25";
        vm.Size = "Rc 1/8";
        Assert.Equal(25m, vm.DrillDepth);

        vm.DrillDepthText = "";
        Assert.Equal(16.8m, vm.DrillDepth);
    }

    [Fact]
    public void TaperTapped_ACounterbore_FallsBackToNone_AndComesBackOnATypeThatTakesIt()
    {
        var vm = NewHole();
        vm.Seat = HoleSeat.Counterbore;

        vm.Type = HoleStandards.TaperTappedType;
        Assert.Equal(HoleSeat.None, vm.Seat);

        vm.Type = HoleStandards.ClearanceType;
        Assert.Equal(HoleSeat.Counterbore, vm.Seat);
    }

    [Fact]
    public void TaperTapped_ACounterboreDropped_ShowsANote_UntilAnotherSeatOrTypeIsChosen()
    {
        var vm = NewHole();
        vm.Seat = HoleSeat.Counterbore;
        Assert.Null(vm.SeatNote);

        vm.Type = HoleStandards.TaperTappedType;
        Assert.Equal(AwbHoleViewModel.TaperDroppedCounterboreText, vm.SeatNote);

        vm.Type = HoleStandards.TappedType;
        Assert.Equal(HoleSeat.Counterbore, vm.Seat);
        Assert.Null(vm.SeatNote);

        vm.Type = HoleStandards.TaperTappedType;
        vm.Seat = HoleSeat.Spotface;
        Assert.Null(vm.SeatNote);
    }

    [Fact]
    public void TaperTapped_TheDisabledCounterbore_CannotBeChosen()
    {
        var vm = OfType(HoleStandards.TaperTappedType);

        vm.Seat = HoleSeat.Counterbore;

        Assert.Equal(HoleSeat.None, vm.Seat);
        Assert.Null(vm.SeatNote);
    }

    [Fact]
    public void EveryOtherType_OffersEverySeatEnabled()
    {
        foreach (var type in new[] { HoleStandards.ClearanceType, HoleStandards.TappedType, HoleStandards.SimpleType })
        {
            var vm = OfType(type);
            Assert.All(vm.SeatOptions, o => Assert.True(o.IsEnabled));
        }
    }

    [Fact]
    public void TaperTapped_ACountersinkIsKept_AcrossTypes()
    {
        var vm = NewHole();
        vm.Seat = HoleSeat.Countersink;

        vm.Type = HoleStandards.TaperTappedType;

        Assert.Equal(HoleSeat.Countersink, vm.Seat);
        Assert.True(vm.ShowsCountersinkSizes);
    }

    [Fact]
    public void TaperTapped_ASeat_StartsAtTheLastSeatOfItsSize()
    {
        var last = new HoleLastDowelSeats(
            null, null,
            taperTappedMetric: new Dictionary<string, HoleDowelSeat> { ["Rc 1/4"] = new(null, null, 16m, 90m) });
        var vm = OfType(HoleStandards.TaperTappedType, lastSeats: last);
        vm.Seat = HoleSeat.Countersink;

        Assert.Null(vm.CountersinkDiameter);

        vm.Size = "Rc 1/4";
        Assert.Equal((16m, 90m), (vm.CountersinkDiameter, vm.CountersinkAngle));
        Assert.True(vm.CanOk);
    }

    [Fact]
    public void TaperTapped_AWorkbookWithoutTheTab_ListsNoSizes_WithANote()
    {
        var workbook = AwbHoleTestWorkbook.Create() with
        {
            MissingOptionalTabs = [HoleWorkbookReader.SpotfaceTab, HoleWorkbookReader.TaperTappedTab],
        };
        var vm = NewHole(workbook);
        vm.Type = HoleStandards.TaperTappedType;

        Assert.Empty(vm.SizeOptions);
        Assert.Equal(HoleWorkbookMessages.MissingOptionalTab(HoleWorkbookReader.TaperTappedTab), vm.SizeNote);
        Assert.False(vm.CanOk);

        vm.Type = HoleStandards.ClearanceType;
        Assert.Null(vm.SizeNote);
    }

    [Fact]
    public void TaperTapped_EditedHole_OpensOnItsOwnValues()
    {
        var spec = HoleSpec.ClearanceHole(HoleUnits.Imperial, "1/4") with
        {
            Kind = HoleKind.TaperTapped,
            Termination = HoleTermination.Distance,
            DrillDepth = 1m,
            DrillPoint = "118 deg",
            Seat = HoleSeat.Countersink,
            CsinkDiameter = 0.7m,
            CsinkAngleDeg = 90m,
        };

        var vm = AwbHoleViewModel.ForEdit(Workbook, PartName, spec);

        Assert.Equal(HoleStandards.TaperTappedType, vm.Type);
        Assert.Equal(HoleStandards.TaperTappedStandard, vm.Standard);
        Assert.Equal((1m, 0.7m), (vm.DrillDepth, vm.CountersinkDiameter));
        Assert.Equal(spec.Normalised(), vm.Spec);
        Assert.True(vm.BuildResult.Succeeded, string.Join(" | ", vm.BuildResult.Problems));
    }

    // ---- Spotface ----

    [Fact]
    public void Spotface_OnAClearanceHole_StartsFromItsRow_KeepsTheFit_AndHasNoFastener()
    {
        var vm = NewHole();
        vm.Size = "T5";

        vm.Seat = HoleSeat.Spotface;

        Assert.True(vm.ShowsSpotfaceSizes);
        Assert.False(vm.ShowsCounterboreSizes);
        Assert.False(vm.ShowsFastener);
        Assert.Null(vm.Fastener);
        Assert.True(vm.ShowsFit);
        Assert.Equal((11m, 0.5m), (vm.SpotfaceDiameter, vm.SpotfaceDepth));
        Assert.Equal(("11", "0.5"), (vm.SpotfaceDiameterText, vm.SpotfaceDepthText));
        Assert.True(vm.CanOk);

        vm.Fit = "Loose";
        Assert.Equal(0.58, vm.Geometry!.HoleDiameterCm, 12);
        Assert.Equal(ClearanceFit.Loose, vm.Spec.ClearanceFit);
    }

    [Fact]
    public void Spotface_OnAClearanceSizeWithNoRow_StartsEmpty_AndOkWaits()
    {
        var vm = NewHole();
        vm.Seat = HoleSeat.Spotface;
        vm.Size = "T3";

        Assert.Null(vm.SpotfaceDiameter);
        Assert.False(vm.CanOk);
        Assert.Equal("Enter a spotface diameter.", vm.SpotfaceDiameterProblem);

        vm.SpotfaceDiameterText = "8";
        vm.SpotfaceDepthText = "0";
        Assert.True(vm.CanOk);
    }

    [Fact]
    public void Spotface_ATypedValue_IsKeptAcrossASizeChange_AnUntypedOneFollowsTheRow()
    {
        var vm = NewHole();
        vm.Size = "T3";
        vm.Seat = HoleSeat.Spotface;
        vm.SpotfaceDiameterText = "8";

        vm.Size = "T5";

        Assert.Equal((8m, 0.5m), (vm.SpotfaceDiameter, vm.SpotfaceDepth));
    }

    [Fact]
    public void Spotface_TheDepthTakesExpressions()
    {
        var vm = NewHole(checker: new FakeChecker());
        vm.Size = "T5";
        vm.Seat = HoleSeat.Spotface;

        vm.SpotfaceDepthText = "d0/8";

        Assert.Equal(2.5m, vm.SpotfaceDepth);
        Assert.Equal("= 2.5 mm", vm.SpotfaceDepthValueText);
        Assert.Equal("d0/8", vm.Spec.SpotfaceDepthExpression);
    }

    [Fact]
    public void Spotface_ANegativeDepth_IsAProblemUnderItsBox()
    {
        var vm = NewHole();
        vm.Size = "T5";
        vm.Seat = HoleSeat.Spotface;

        vm.SpotfaceDepthText = "-1";

        Assert.Equal("The spotface depth is less than zero.", vm.SpotfaceDepthProblem);
        Assert.False(vm.CanOk);
    }

    [Fact]
    public void Spotface_OnATappedHole_TakesTheRowOfTheSameSize()
    {
        var vm = NewHole();
        vm.Type = HoleStandards.TappedType;
        vm.Size = "M5";

        vm.Seat = HoleSeat.Spotface;

        Assert.Equal((11m, 0.5m), (vm.SpotfaceDiameter, vm.SpotfaceDepth));
        Assert.False(vm.ShowsFastener);
        Assert.True(vm.CanOk);
    }

    [Fact]
    public void Spotface_OnATappedHoleWithNoRow_StartsAtTheLastSpotfaceOfItsThread()
    {
        var last = new HoleLastDowelSeats(
            null, null,
            tappedMetric: new Dictionary<string, HoleDowelSeat>
            {
                ["M3"] = HoleDowelSeat.Empty with { SpotfaceDiameter = 7m, SpotfaceDepth = 0.3m },
            });
        var vm = NewHole(lastSeats: last);
        vm.Type = HoleStandards.TappedType;
        vm.Size = "M3";

        vm.Seat = HoleSeat.Spotface;

        Assert.Equal((7m, 0.3m), (vm.SpotfaceDiameter, vm.SpotfaceDepth));
    }

    [Fact]
    public void Spotface_OnADowel_TakesTheRowOfTheSameSize_BeforeTheLastSpotface()
    {
        var last = new HoleLastDowelSeats(
            new Dictionary<string, HoleDowelSeat>
            {
                ["5 mm"] = new HoleDowelSeat(9m, 2m, null, null) with { SpotfaceDiameter = 13m, SpotfaceDepth = 1m },
                ["3 mm"] = HoleDowelSeat.Empty with { SpotfaceDiameter = 6m, SpotfaceDepth = 0.2m },
            },
            null);
        var vm = NewHole(lastSeats: last);
        vm.Standard = HoleStandards.DowelStandard;
        vm.Size = "5 mm";

        vm.Seat = HoleSeat.Spotface;
        Assert.Equal((11m, 0.5m), (vm.SpotfaceDiameter, vm.SpotfaceDepth));

        vm.Size = "3 mm";
        Assert.Equal((6m, 0.2m), (vm.SpotfaceDiameter, vm.SpotfaceDepth));

        vm.Seat = HoleSeat.Counterbore;
        vm.Size = "5 mm";
        Assert.Equal((9m, 2m), (vm.CounterboreDiameter, vm.CounterboreDepth));
    }

    [Fact]
    public void Spotface_TheDiagramShowsIt()
    {
        var vm = NewHole();
        vm.Size = "T5";
        vm.Seat = HoleSeat.Spotface;

        Assert.Equal(HoleSeat.Spotface, vm.DiagramInput.Seat);
        Assert.Equal((11m, 0.5m), (vm.DiagramInput.SpotfaceDiameter, vm.DiagramInput.SpotfaceDepth));
        Assert.Contains("spotface", vm.DiagramSummary);
    }

    // ---- Fastener type ----

    [Theory]
    [InlineData(HoleSeat.Counterbore, true)]
    [InlineData(HoleSeat.Countersink, true)]
    [InlineData(HoleSeat.Spotface, false)]
    [InlineData(HoleSeat.None, false)]
    public void Fastener_ShowsOnlyForAClearanceCounterboreOrCountersink(HoleSeat seat, bool shows)
    {
        var vm = NewHole();
        vm.Size = "T5";
        vm.Seat = seat;

        Assert.Equal(shows, vm.ShowsFastener);
    }

    // ---- Preview ----

    [Fact]
    public void ChangingToANewType_RaisesOnePreview()
    {
        var vm = NewHole();
        var previews = 0;
        vm.PreviewNeeded += (_, _) => previews++;

        vm.Type = HoleStandards.TaperTappedType;

        Assert.Equal(1, previews);
        Assert.Equal(HoleKind.TaperTapped, vm.Spec.Kind);
    }
}
