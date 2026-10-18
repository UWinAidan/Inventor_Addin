using InventorAddin.Core.Holes;
using InventorAddin.Core.ViewModels;

namespace InventorAddin.Core.Tests.ViewModels;

public sealed class AwbHoleViewModelTests
{
    private const string PartName = "Test bracket.ipt";

    private static readonly HoleWorkbook Workbook = AwbHoleTestWorkbook.Create();

    private static AwbHoleViewModel NewHole(HoleWorkbook? workbook = null) => new(workbook ?? Workbook, PartName);

    private static List<string> Values<T>(IEnumerable<HoleOption<T>> options) =>
        options.Select(o => o.Text).ToList();

    private sealed class Counter
    {
        public int Previews { get; private set; }
        public List<string?> Changed { get; } = [];

        public Counter(AwbHoleViewModel vm)
        {
            vm.PreviewNeeded += (_, _) => Previews++;
            vm.PropertyChanged += (_, e) => Changed.Add(e.PropertyName);
        }
    }

    private static HoleSpec MetricSpec(HoleKind kind, string size) => HoleSpec.ClearanceHole(HoleUnits.Metric, size) with
    {
        Kind = kind,
    };

    // ---- A new hole ----

    [Fact]
    public void NewHole_StartsOnTheDefaults()
    {
        var vm = NewHole();

        Assert.Equal(HoleUnits.Metric, vm.Units);
        Assert.Equal(HoleStandards.ClearanceType, vm.Type);
        Assert.Equal(HoleSeat.None, vm.Seat);
        Assert.Equal(HoleStandards.ClearanceStandard, vm.Standard);
        Assert.Equal(HoleKind.Clearance, vm.Kind);
        Assert.Null(vm.Fastener);
        Assert.Equal("T3", vm.Size);
        Assert.Equal("Normal", vm.Fit);
        Assert.Null(vm.Tolerance);
        Assert.Equal(HoleTermination.ThroughAll, vm.Termination);
        Assert.Null(vm.DrillPoint);
        Assert.False(vm.Flip);
        Assert.False(vm.IsEdit);
        Assert.False(vm.IsInventorHole);
        Assert.Null(vm.InventorHoleNote);
    }

    [Fact]
    public void NewHole_StartsOnTheWorkbooksDefaultUnits()
    {
        var vm = NewHole(AwbHoleTestWorkbook.Create(HoleUnits.Imperial));

        Assert.Equal(HoleUnits.Imperial, vm.Units);
        Assert.Equal("#3", vm.Size);
    }

    [Fact]
    public void Header_NamesTheCommandAndThePart()
    {
        var vm = NewHole();

        Assert.Equal("AWB Hole", vm.HeaderTitle);
        Assert.Equal(PartName, vm.HeaderSubline);
        Assert.Equal("Edit AWB Hole", AwbHoleViewModel.ForEdit(Workbook, PartName, vm.Spec).HeaderTitle);
    }

    [Fact]
    public void FixedLists_AreInTheWindowsOrder()
    {
        Assert.Equal(["Metric", "Imperial"], Values(AwbHoleViewModel.UnitsOptions));
        Assert.Equal(["Clearance", "Tapped"], Values(AwbHoleViewModel.TypeOptions));
        Assert.Equal(["Distance", "Through All"], Values(NewHole().TerminationOptions));
    }

    // ---- Lists ----

    [Fact]
    public void Standard_ListsClearanceAndDowelForClearance_AndTappedForTapped()
    {
        var vm = NewHole();
        Assert.Equal(["AWB Clearance", "AWB Dowel"], Values(vm.StandardOptions));

        vm.Type = HoleStandards.TappedType;

        Assert.Equal(["AWB Tapped"], Values(vm.StandardOptions));
        Assert.Equal(HoleStandards.TappedStandard, vm.Standard);
        Assert.Equal(HoleKind.Tapped, vm.Kind);
    }

    [Fact]
    public void Sizes_LeaveOutHiddenRows_ListARepeatedSizeOnce_AndKeepWorkbookOrder()
    {
        var vm = NewHole();

        Assert.Equal(["T3", "T5"], Values(vm.SizeOptions));
    }

    [Fact]
    public void Sizes_ComeFromTheTabForTheKind()
    {
        var vm = NewHole();

        vm.Standard = HoleStandards.DowelStandard;
        Assert.Equal(["3 mm", "5 mm"], Values(vm.SizeOptions));

        vm.Type = HoleStandards.TappedType;
        Assert.Equal(["M3", "M5"], Values(vm.SizeOptions));

        vm.Units = HoleUnits.Imperial;
        Assert.Equal(["#5-40 UNC"], Values(vm.SizeOptions));
    }

    [Fact]
    public void Seat_IsNoneOnlyForDowelHoles()
    {
        var vm = NewHole();
        Assert.Equal(["None", "Counterbore", "Countersink"], Values(vm.SeatOptions));

        vm.Seat = HoleSeat.Counterbore;
        vm.Standard = HoleStandards.DowelStandard;

        Assert.Equal(["None"], Values(vm.SeatOptions));
        Assert.Equal(HoleSeat.None, vm.Seat);
    }

    [Fact]
    public void Fastener_ListsTheSeatTabsScrews_OnlyForASeatedHole()
    {
        var vm = NewHole();
        Assert.False(vm.ShowsFastener);
        Assert.Empty(vm.FastenerOptions);

        vm.Seat = HoleSeat.Counterbore;

        Assert.True(vm.ShowsFastener);
        Assert.Equal(["TCAP", "TLOW"], Values(vm.FastenerOptions));
        Assert.Equal("TCAP", vm.Fastener);
        Assert.Equal(["T3", "T5"], Values(vm.SizeOptions));

        vm.Seat = HoleSeat.Countersink;

        Assert.Equal(["TFLAT"], Values(vm.FastenerOptions));
        Assert.Equal("TFLAT", vm.Fastener);
        Assert.Equal(["T5"], Values(vm.SizeOptions));
        Assert.Equal("T5", vm.Size);
    }

    [Fact]
    public void Fastener_PicksTheSizesListed()
    {
        var vm = NewHole();
        vm.Seat = HoleSeat.Counterbore;
        Assert.Equal("T3", vm.Size);

        vm.Fastener = "TLOW";

        Assert.Equal(["T5"], Values(vm.SizeOptions));
        Assert.Equal("T5", vm.Size);
    }

    [Fact]
    public void Fit_ListsClearanceFits_DowelFitsWithColours_AndNothingElse()
    {
        var vm = NewHole();
        Assert.True(vm.ShowsFit);
        Assert.Equal(["Close", "Normal", "Loose"], vm.FitOptions.Select(o => o.Text));
        Assert.All(vm.FitOptions, o => Assert.Null(o.Swatch));
        Assert.False(vm.ShowsFitColour);
        Assert.Null(vm.FitSwatch);

        vm.Standard = HoleStandards.DowelStandard;
        Assert.Equal(["Slip", "Normal", "Press"], vm.FitOptions.Select(o => o.Text));
        Assert.Equal(["#28AA46", "#2864DC", "#DC2828"], vm.FitOptions.Select(o => o.Swatch));
        Assert.True(vm.ShowsFitColour);

        vm.Standard = HoleStandards.ClearanceStandard;
        vm.Seat = HoleSeat.Counterbore;
        Assert.False(vm.ShowsFit);
        Assert.Empty(vm.FitOptions);
        Assert.Null(vm.Fit);

        vm.Type = HoleStandards.TappedType;
        Assert.Empty(vm.FitOptions);
    }

    [Fact]
    public void FitColour_FollowsTheSelectedDowelFit()
    {
        var vm = NewHole();
        vm.Standard = HoleStandards.DowelStandard;

        vm.Fit = "Press";

        Assert.Equal("#DC2828", vm.FitSwatch);
        Assert.Equal("Press fit", vm.FitColourText);
        Assert.Equal(new HoleColour(220, 40, 40), vm.Geometry!.FitColour);
    }

    [Fact]
    public void Tolerance_OnlyForDowels_PreselectsTheFirstDefaultRow()
    {
        var vm = NewHole();
        Assert.False(vm.ShowsTolerance);
        Assert.Empty(vm.ToleranceOptions);

        vm.Standard = HoleStandards.DowelStandard;

        Assert.True(vm.ShowsTolerance);
        Assert.Equal(["N bore", "N loc", "N second"], Values(vm.ToleranceOptions));
        Assert.Equal("N loc", vm.Tolerance);
    }

    [Fact]
    public void Tolerance_FollowsTheFit()
    {
        var vm = NewHole();
        vm.Standard = HoleStandards.DowelStandard;

        vm.Fit = "Press";
        Assert.Equal(["P press"], Values(vm.ToleranceOptions));
        Assert.Equal("P press", vm.Tolerance);

        vm.Fit = "Normal";
        Assert.Equal("N loc", vm.Tolerance);
    }

    [Fact]
    public void ThreadDepths_OnlyForBlindTappedHoles_FromTheTappedRow()
    {
        var vm = NewHole();
        Assert.False(vm.ShowsThreadDepths);
        Assert.Null(vm.ThreadDepth);

        vm.Type = HoleStandards.TappedType;
        Assert.False(vm.ShowsThreadDepths);

        vm.Termination = HoleTermination.Distance;

        Assert.True(vm.ShowsThreadDepths);
        Assert.False(vm.ShowsDepth);
        Assert.Equal(4.5m, vm.ThreadDepth);
        Assert.Equal(6m, vm.DrillDepth);
    }

    [Fact]
    public void DrillPoint_OnlyForBlindHoles_LeavesOutHiddenRows_AndPreselectsTheFirstDefault()
    {
        var vm = NewHole();
        Assert.False(vm.ShowsDrillPoint);
        Assert.Empty(vm.DrillPointOptions);

        vm.Termination = HoleTermination.Distance;

        Assert.True(vm.ShowsDrillPoint);
        Assert.True(vm.ShowsDepth);
        Assert.Equal(["118 deg", "90 deg", "Flat"], Values(vm.DrillPointOptions));
        Assert.Equal("118 deg", vm.DrillPoint);

        vm.Termination = HoleTermination.ThroughAll;
        Assert.Empty(vm.DrillPointOptions);
        Assert.Null(vm.DrillPoint);
    }

    // ---- Cascading ----

    [Fact]
    public void ChangingAField_KeepsLaterChoicesThatStillApply()
    {
        var vm = NewHole();
        vm.Size = "T5";
        vm.Fit = "Loose";
        vm.Termination = HoleTermination.Distance;
        vm.Depth = 12m;
        vm.DrillPoint = "Flat";

        vm.Flip = true;
        vm.Standard = HoleStandards.DowelStandard;
        vm.Standard = HoleStandards.ClearanceStandard;

        Assert.Equal(HoleTermination.Distance, vm.Termination);
        Assert.Equal(12m, vm.Depth);
        Assert.Equal("Flat", vm.DrillPoint);
        Assert.True(vm.Flip);
        Assert.Equal("Normal", vm.Fit);
        Assert.Equal("T3", vm.Size);
    }

    [Fact]
    public void ChangingTheStandard_KeepsAFitBothListsHold()
    {
        var vm = NewHole();
        vm.Fit = "Normal";

        vm.Standard = HoleStandards.DowelStandard;

        Assert.Equal("Normal", vm.Fit);
    }

    [Fact]
    public void ChangingTheStandard_PutsAFitTheNewListLacksBackToNormal()
    {
        var vm = NewHole();
        vm.Fit = "Close";

        vm.Standard = HoleStandards.DowelStandard;

        Assert.Equal("Normal", vm.Fit);
    }

    [Fact]
    public void ChangingTheType_PutsTheSeatBackToNone()
    {
        var vm = NewHole();
        vm.Seat = HoleSeat.Counterbore;

        vm.Type = HoleStandards.TappedType;

        Assert.Equal(HoleSeat.None, vm.Seat);
        Assert.Null(vm.Fastener);
        Assert.Equal("M3", vm.Size);
    }

    [Fact]
    public void ChangingTheSeat_KeepsTheType()
    {
        var vm = NewHole();
        vm.Type = HoleStandards.TappedType;

        vm.Seat = HoleSeat.Countersink;

        Assert.Equal(HoleStandards.TappedType, vm.Type);
        Assert.Equal(HoleSeat.Countersink, vm.Seat);
        Assert.False(vm.ShowsFastener);
    }

    [Fact]
    public void ASeatOnATappedHole_IsRefusedBesideTheSeat()
    {
        var vm = NewHole();
        vm.Type = HoleStandards.TappedType;

        vm.Seat = HoleSeat.Counterbore;

        Assert.NotNull(vm.SeatProblem);
        Assert.Null(vm.Geometry);
        Assert.False(vm.CanOk);
    }

    [Fact]
    public void ChangingTheSize_PutsTheTappedDepthsBackToTheRowsDefaults()
    {
        var vm = NewHole();
        vm.Type = HoleStandards.TappedType;
        vm.Termination = HoleTermination.Distance;
        vm.ThreadDepth = 5m;

        vm.Flip = true;
        Assert.Equal(5m, vm.ThreadDepth);

        vm.Size = "M5";

        Assert.Equal(7.5m, vm.ThreadDepth);
        Assert.Equal(10m, vm.DrillDepth);
    }

    [Fact]
    public void ClearingATappedDepth_PutsBackTheRowsDefault()
    {
        var vm = NewHole();
        vm.Type = HoleStandards.TappedType;
        vm.Termination = HoleTermination.Distance;
        vm.DrillDepth = 9m;

        vm.DrillDepth = null;

        Assert.Equal(6m, vm.DrillDepth);
    }

    [Fact]
    public void Units_KeepTheSizeWithTheNearestNominal()
    {
        var vm = NewHole();
        vm.Size = "T5";

        vm.Units = HoleUnits.Imperial;
        Assert.Equal("#5", vm.Size);

        vm.Size = "#3";
        vm.Units = HoleUnits.Metric;
        Assert.Equal("T3", vm.Size);
    }

    [Fact]
    public void Units_ForASeatedHole_UseTheClearanceNominal()
    {
        var vm = NewHole();
        vm.Seat = HoleSeat.Counterbore;
        vm.Size = "T3";

        vm.Units = HoleUnits.Imperial;

        Assert.Equal("TCAP", vm.Fastener);
        Assert.Equal("#5", vm.Size);
    }

    [Fact]
    public void Units_ConvertTheDepth()
    {
        var vm = NewHole();
        vm.Termination = HoleTermination.Distance;
        vm.Depth = 25.4m;

        vm.Units = HoleUnits.Imperial;

        Assert.Equal(1m, vm.Depth);
    }

    // ---- Setters a list does not hold ----

    [Fact]
    public void AValueTheListDoesNotHold_IsIgnored()
    {
        var vm = NewHole();
        var counter = new Counter(vm);

        vm.Size = null;
        vm.Size = "T4";
        vm.Fastener = "TCAP";
        vm.Standard = "Something else";
        vm.Seat = (HoleSeat)42;

        Assert.Equal("T3", vm.Size);
        Assert.Null(vm.Fastener);
        Assert.Equal(HoleStandards.ClearanceStandard, vm.Standard);
        Assert.Equal(HoleSeat.None, vm.Seat);
        Assert.Equal(0, counter.Previews);
        Assert.Contains(nameof(AwbHoleViewModel.Size), counter.Changed);
    }

    [Fact]
    public void ASize_MatchesIgnoringCaseAndSpaces_AndTakesTheListsSpelling()
    {
        var vm = NewHole();

        vm.Size = " t5 ";

        Assert.Equal("T5", vm.Size);
    }

    // ---- Preview ----

    [Fact]
    public void OneUserChange_RaisesOnePreview_HoweverManyFieldsItCascadesInto()
    {
        var vm = NewHole();
        vm.Seat = HoleSeat.Counterbore;
        var counter = new Counter(vm);

        vm.Type = HoleStandards.TappedType;

        Assert.Equal(1, counter.Previews);
        Assert.True(counter.Changed.Count > 3);
    }

    [Fact]
    public void SettingTheSameValue_RaisesNoPreview()
    {
        var vm = NewHole();
        var counter = new Counter(vm);

        vm.Size = "T3";
        vm.Units = HoleUnits.Metric;
        vm.Flip = false;

        Assert.Equal(0, counter.Previews);
    }

    [Fact]
    public void AChangeTheSpecDoesNotUse_RaisesNoPreview()
    {
        var vm = NewHole();
        vm.Termination = HoleTermination.Distance;
        vm.Depth = 10m;
        vm.Termination = HoleTermination.ThroughAll;
        var counter = new Counter(vm);

        vm.Depth = 20m;

        Assert.Equal(0, counter.Previews);
        Assert.Equal(20m, vm.Depth);
    }

    [Fact]
    public void CentreCount_RaisesNoPreview_ButUpdatesTheChecks()
    {
        var vm = NewHole();
        var counter = new Counter(vm);
        var canExecuteChanged = 0;
        vm.OkCommand.CanExecuteChanged += (_, _) => canExecuteChanged++;
        Assert.NotNull(vm.CentresProblem);
        Assert.False(vm.CanOk);

        vm.CentreCount = 3;

        Assert.Equal(0, counter.Previews);
        Assert.Null(vm.CentresProblem);
        Assert.True(vm.CanOk);
        Assert.True(vm.OkCommand.CanExecute(null));
        Assert.True(canExecuteChanged > 0);
    }

    // ---- Geometry, checks and OK ----

    [Fact]
    public void Geometry_IsWhatTheRulesBuild_EvenBeforeCentresArePicked()
    {
        var vm = NewHole();
        vm.Size = "T5";

        var expected = HoleRules.Build(vm.Spec, Workbook, 1);

        Assert.Equal(expected.Geometry, vm.Geometry);
        Assert.Equal(expected.Resolved, vm.BuildResult.Resolved);
        Assert.Equal(0.55, vm.Geometry!.HoleDiameterCm, 10);
    }

    [Fact]
    public void Problems_ShowBesideTheirField_AndOkWaitsForThem()
    {
        var vm = NewHole();
        vm.CentreCount = 1;
        vm.Termination = HoleTermination.Distance;
        Assert.True(vm.CanOk);

        vm.DepthText = "";

        Assert.Equal("Enter a depth greater than zero.", vm.DepthProblem);
        Assert.Null(vm.SizeProblem);
        Assert.True(vm.HasProblems);
        Assert.False(vm.CanOk);
        Assert.Null(vm.Geometry);

        vm.Depth = 10m;

        Assert.Null(vm.DepthProblem);
        Assert.True(vm.CanOk);
        Assert.Equal(1.0, vm.Geometry!.DepthCm!.Value, 10);
    }

    [Fact]
    public void OkAndCancel_RaiseTheirEvents()
    {
        var vm = NewHole();
        vm.CentreCount = 1;
        var ok = 0;
        var cancel = 0;
        vm.OkRequested += (_, _) => ok++;
        vm.CancelRequested += (_, _) => cancel++;

        vm.OkCommand.Execute(null);
        vm.CancelCommand.Execute(null);

        Assert.Equal(1, ok);
        Assert.Equal(1, cancel);
    }

    [Fact]
    public void Ok_DoesNothingWithoutCentres()
    {
        var vm = NewHole();
        var ok = 0;
        vm.OkRequested += (_, _) => ok++;

        vm.OkCommand.Execute(null);

        Assert.Equal(0, ok);
    }

    // ---- Edit mode ----

    [Fact]
    public void Edit_ShowsTheSpecExactly_WithHiddenRows_AndRaisesNoPreview()
    {
        var spec = MetricSpec(HoleKind.Clearance, "T4") with
        {
            ClearanceFit = ClearanceFit.Loose,
            Termination = HoleTermination.Distance,
            Depth = 12m,
            DrillPoint = "60 deg",
            Flip = true,
        };

        var vm = AwbHoleViewModel.ForEdit(Workbook, PartName, spec);
        var counter = new Counter(vm);

        Assert.True(vm.IsEdit);
        Assert.Equal(spec.Normalised(), vm.Spec);
        Assert.Equal("T4", vm.Size);
        Assert.Equal(["T3", "T4", "T5"], Values(vm.SizeOptions));
        Assert.Equal("Loose", vm.Fit);
        Assert.Equal("60 deg", vm.DrillPoint);
        Assert.Contains("60 deg", Values(vm.DrillPointOptions));
        Assert.True(vm.BuildResult.Succeeded);
        Assert.Equal(0, counter.Previews);

        vm.Flip = false;
        Assert.Equal("T4", vm.Size);
        Assert.Equal(1, counter.Previews);

        vm.Size = "T5";
        Assert.Equal(["T3", "T5"], Values(vm.SizeOptions));
    }

    [Fact]
    public void Edit_KeepsASizeNotInTheWorkbook_UntilAFieldAboveItChanges()
    {
        var vm = AwbHoleViewModel.ForEdit(Workbook, PartName, MetricSpec(HoleKind.Clearance, "T99"));

        Assert.Equal("T99", vm.Size);
        Assert.Contains("T99", Values(vm.SizeOptions));
        Assert.NotNull(vm.SizeProblem);
        Assert.Null(vm.Geometry);

        vm.Flip = true;
        Assert.Equal("T99", vm.Size);

        vm.Standard = HoleStandards.DowelStandard;
        vm.Standard = HoleStandards.ClearanceStandard;
        Assert.Equal("T3", vm.Size);
        Assert.DoesNotContain("T99", Values(vm.SizeOptions));
    }

    [Fact]
    public void Edit_ShowsADowelWithItsTolerance_NotTheDefault()
    {
        var spec = MetricSpec(HoleKind.Dowel, "5 mm") with { PrecisionFit = PrecisionFit.Normal, Tolerance = "N bore" };

        var vm = AwbHoleViewModel.ForEdit(Workbook, PartName, spec);

        Assert.Equal(HoleStandards.ClearanceType, vm.Type);
        Assert.Equal(HoleStandards.DowelStandard, vm.Standard);
        Assert.Equal("Normal", vm.Fit);
        Assert.Equal("N bore", vm.Tolerance);
        Assert.Equal(spec.Normalised(), vm.Spec);
    }

    [Fact]
    public void Edit_ShowsASeatedHoleWithAHiddenFastener()
    {
        var spec = MetricSpec(HoleKind.Clearance, "T5") with { Seat = HoleSeat.Counterbore, Fastener = "THID" };

        var vm = AwbHoleViewModel.ForEdit(Workbook, PartName, spec);

        Assert.Equal("THID", vm.Fastener);
        Assert.Equal(["TCAP", "TLOW", "THID"], Values(vm.FastenerOptions));
        Assert.Equal("T5", vm.Size);
        Assert.True(vm.BuildResult.Succeeded);
    }

    [Fact]
    public void Edit_ShowsATappedHolesOwnDepths()
    {
        var spec = MetricSpec(HoleKind.Tapped, "M5") with
        {
            Termination = HoleTermination.Distance,
            ThreadDepth = 6m,
            DrillDepth = 8m,
        };

        var vm = AwbHoleViewModel.ForEdit(Workbook, PartName, spec);

        Assert.Equal(HoleStandards.TappedType, vm.Type);
        Assert.Equal(6m, vm.ThreadDepth);
        Assert.Equal(8m, vm.DrillDepth);
        Assert.Equal("118 deg", vm.DrillPoint);
    }

    [Fact]
    public void Edit_FillsDefaultsTheSpecLeftBlank()
    {
        var spec = MetricSpec(HoleKind.Tapped, "M5") with { Termination = HoleTermination.Distance };

        var vm = AwbHoleViewModel.ForEdit(Workbook, PartName, spec);

        Assert.Equal(7.5m, vm.ThreadDepth);
        Assert.Equal(10m, vm.DrillDepth);
        Assert.Equal("118 deg", vm.DrillPoint);
    }

    [Fact]
    public void Edit_ShowsASeatOnATappedHole_WithItsProblem()
    {
        var spec = MetricSpec(HoleKind.Tapped, "M5") with { Seat = HoleSeat.Countersink };

        var vm = AwbHoleViewModel.ForEdit(Workbook, PartName, spec);

        Assert.Equal(HoleSeat.Countersink, vm.Seat);
        Assert.NotNull(vm.SeatProblem);
    }

    [Fact]
    public void InventorHole_OpensOnTheClosestSpec_WithANote()
    {
        var spec = MetricSpec(HoleKind.Clearance, "T5");

        var vm = AwbHoleViewModel.ForInventorHole(Workbook, PartName, spec, ["  The closest size is picked. ", " "]);

        Assert.True(vm.IsEdit);
        Assert.True(vm.IsInventorHole);
        Assert.Equal(AwbHoleViewModel.InventorHoleNoteText, vm.InventorHoleNote);
        Assert.Contains("OK makes it an AWB Hole", vm.InventorHoleNote);
        Assert.Equal(["The closest size is picked."], vm.InventorHoleNotes);
        Assert.Equal(spec.Normalised(), vm.Spec);
    }

    [Fact]
    public void AStoredHole_HasNoInventorNote()
    {
        var vm = AwbHoleViewModel.ForEdit(Workbook, PartName, MetricSpec(HoleKind.Clearance, "T5"));

        Assert.False(vm.IsInventorHole);
        Assert.Null(vm.InventorHoleNote);
        Assert.Empty(vm.InventorHoleNotes);
    }

    // ---- Status ----

    [Fact]
    public void WorkbookProblems_AreCountedInTheStatusAtOpen()
    {
        HoleWorkbookProblem[] problems =
        [
            new("Clearance", 7, "The Normal value is not a number."),
            new("Dowel", null, "The tab is missing."),
        ];

        var vm = new AwbHoleViewModel(Workbook, PartName, problems);

        Assert.Equal("The hole workbook has 2 problems; see the details.", vm.StatusText);
        Assert.False(vm.StatusIsError);
        Assert.Equal(["Clearance tab, row 7: The Normal value is not a number.", "Dowel tab: The tab is missing."], vm.StatusNotes);
        Assert.True(vm.HasStatusDetails);
    }

    [Fact]
    public void NoWorkbookProblems_LeaveTheStatusBlank()
    {
        var vm = NewHole();

        Assert.Equal("", vm.StatusText);
        Assert.Null(vm.StatusDetails);
    }

    [Fact]
    public void Refused_ShowsInTheErrorStyle_UntilTheNextChange()
    {
        var vm = NewHole();

        vm.ShowRefused("The hole cuts nothing.");

        Assert.True(vm.IsRefused);
        Assert.Equal(AwbHoleViewModel.RefusedText, vm.StatusText);
        Assert.Equal("Inventor could not make this hole; the last good hole is kept.", vm.StatusText);
        Assert.True(vm.StatusIsError);
        Assert.Equal("The hole cuts nothing.", vm.StatusDetails);

        vm.Flip = true;

        Assert.False(vm.IsRefused);
        Assert.Equal("", vm.StatusText);
        Assert.False(vm.StatusIsError);
    }

    [Fact]
    public void ClearRefused_BringsBackTheStatusAtOpen()
    {
        var vm = new AwbHoleViewModel(Workbook, PartName, [new HoleWorkbookProblem("Dowel", 5, "Bad.")]);
        vm.ShowRefused(null);
        Assert.Null(vm.StatusDetails);

        vm.ClearRefused();

        Assert.False(vm.IsRefused);
        Assert.Equal("The hole workbook has 1 problem; see the details.", vm.StatusText);
        Assert.False(vm.StatusIsError);
    }

    [Fact]
    public void AnEmptyWorkbook_OpensWithASizeProblem()
    {
        var vm = new AwbHoleViewModel(HoleWorkbook.Empty, PartName);

        Assert.Null(vm.Size);
        Assert.Empty(vm.SizeOptions);
        Assert.NotNull(vm.SizeProblem);
        Assert.False(vm.CanOk);
    }

    // ---- The blind depth a new hole starts at (task 097) ----

    private static AwbHoleViewModel NewHoleWith(HoleLastDepths lastDepths, HoleWorkbook? workbook = null) =>
        new(workbook ?? Workbook, PartName, null, lastDepths);

    [Fact]
    public void BlindClearance_WithNoDepthUsedYet_StartsAtTwiceTheNominal_AndCanOk()
    {
        var vm = NewHole();
        vm.CentreCount = 1;

        vm.Termination = HoleTermination.Distance;

        Assert.Equal(6m, vm.Depth);
        Assert.Equal("6", vm.DepthText);
        Assert.Equal(6m, vm.Spec.Depth);
        Assert.Null(vm.DepthProblem);
        Assert.True(vm.CanOk);
    }

    [Fact]
    public void BlindDowel_StartsAtTwiceTheNominal()
    {
        var vm = NewHole();
        vm.Standard = HoleStandards.DowelStandard;
        vm.Size = "5 mm";

        vm.Termination = HoleTermination.Distance;

        Assert.Equal(10m, vm.Depth);
    }

    [Fact]
    public void BlindCounterbore_TakesItsClearanceRowsNominal()
    {
        var vm = NewHole();
        vm.Seat = HoleSeat.Counterbore;
        vm.Size = "T5";

        vm.Termination = HoleTermination.Distance;

        Assert.Equal(10m, vm.Depth);
    }

    [Fact]
    public void BlindDepth_UsesTheWorkbooksMultiplier_RoundedToThreeDecimals()
    {
        var starter = AwbHoleTestWorkbook.Create();
        var workbook = starter with { Settings = starter.Settings with { BlindDepthMultiplier = 1.2345m } };
        var vm = NewHole(workbook);

        vm.Termination = HoleTermination.Distance;

        Assert.Equal(3.704m, vm.Depth);
    }

    [Fact]
    public void BlindDepth_StartsAtTheLastDepthUsed_InEachUnits()
    {
        var vm = NewHoleWith(new HoleLastDepths(20m, 0.75m));

        vm.Termination = HoleTermination.Distance;
        Assert.Equal(20m, vm.Depth);

        vm.Units = HoleUnits.Imperial;
        Assert.Equal(0.75m, vm.Depth);
    }

    [Fact]
    public void BlindDepth_WithALastDepthInTheOtherUnitsOnly_UsesTheMultiplierHere()
    {
        var vm = NewHoleWith(new HoleLastDepths(null, 0.75m));

        vm.Termination = HoleTermination.Distance;
        Assert.Equal(6m, vm.Depth);

        vm.Units = HoleUnits.Imperial;
        Assert.Equal(0.75m, vm.Depth);
    }

    [Fact]
    public void AnUntypedDepth_FollowsTheSizeAndUnits()
    {
        var vm = NewHole();
        vm.Termination = HoleTermination.Distance;
        Assert.Equal(6m, vm.Depth);

        vm.Size = "T5";
        Assert.Equal(10m, vm.Depth);

        vm.Units = HoleUnits.Imperial;
        Assert.Equal("#5", vm.Size);
        Assert.Equal(0.4m, vm.Depth);
    }

    [Fact]
    public void ATypedDepth_IsKept_WhenTheSizeOrUnitsChange()
    {
        var vm = NewHoleWith(new HoleLastDepths(20m, 0.75m));
        vm.Termination = HoleTermination.Distance;

        vm.DepthText = "7";
        vm.Size = "T5";
        Assert.Equal(7m, vm.Depth);

        vm.Termination = HoleTermination.ThroughAll;
        vm.Termination = HoleTermination.Distance;
        Assert.Equal(7m, vm.Depth);

        vm.Units = HoleUnits.Imperial;
        Assert.Equal(0.2756m, vm.Depth);
    }

    [Fact]
    public void AnEmptiedDepthBox_DisablesOk_UntilTheNextChangeTheRuleFollows()
    {
        var vm = NewHole();
        vm.CentreCount = 1;
        vm.Termination = HoleTermination.Distance;

        vm.DepthText = "";
        Assert.Null(vm.Depth);
        Assert.False(vm.CanOk);

        vm.Flip = true;
        Assert.Null(vm.Depth);
        Assert.False(vm.CanOk);

        vm.Size = "T5";
        Assert.Equal(10m, vm.Depth);
        Assert.True(vm.CanOk);
    }

    [Fact]
    public void ATappedHole_KeepsTheTappedRowsDepths()
    {
        var vm = NewHoleWith(new HoleLastDepths(20m, null));
        vm.Type = HoleStandards.TappedType;
        vm.Size = "M5";

        vm.Termination = HoleTermination.Distance;

        Assert.Null(vm.Spec.Depth);
        Assert.Equal(7.5m, vm.ThreadDepth);
        Assert.Equal(10m, vm.DrillDepth);
    }

    [Fact]
    public void AnEditedHole_KeepsItsOwnDepth()
    {
        var spec = MetricSpec(HoleKind.Clearance, "T3") with { Termination = HoleTermination.Distance, Depth = 12m };

        var vm = AwbHoleViewModel.ForEdit(Workbook, PartName, spec, null, new HoleLastDepths(20m, null));
        Assert.Equal(12m, vm.Depth);
        Assert.Equal(12m, vm.Spec.Depth);

        vm.Size = "T5";
        Assert.Equal(12m, vm.Depth);
    }

    [Fact]
    public void AnEditedThroughHole_MadeBlind_StartsAtTheLastDepthUsed()
    {
        var spec = MetricSpec(HoleKind.Clearance, "T3");

        var vm = AwbHoleViewModel.ForEdit(Workbook, PartName, spec, null, new HoleLastDepths(20m, null));
        vm.Termination = HoleTermination.Distance;

        Assert.Equal(20m, vm.Depth);
    }

    [Fact]
    public void SwatchOf_WritesHexRgb()
    {
        Assert.Equal("#0A10FF", HoleFitOption.SwatchOf(new HoleColour(10, 16, 255)));
    }
}
