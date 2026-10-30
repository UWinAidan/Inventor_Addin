using System.Globalization;
using InventorAddin.Core.Holes;
using InventorAddin.Core.ViewModels;

namespace InventorAddin.Core.Tests.ViewModels;

/// <summary>
/// What the AWB Hole window (task 090) needs from the view-model beyond task 085: the termination list without To,
/// the depth boxes as text, the centres row, the add-in's notes in the status line, opening from a read-back, and the
/// workbook that stops the window.
/// </summary>
public sealed class AwbHoleWindowViewModelTests
{
    private const string PartName = "Test bracket.ipt";

    private static readonly HoleWorkbook Workbook = AwbHoleTestWorkbook.Create();

    private static AwbHoleViewModel NewHole() => new(Workbook, PartName);

    private static List<string> Values<T>(IEnumerable<HoleOption<T>> options) => options.Select(o => o.Text).ToList();

    private static HoleSpec MetricSpec(HoleKind kind, string size) =>
        HoleSpec.ClearanceHole(HoleUnits.Metric, size) with { Kind = kind };

    /// <summary>Runs the test in a culture, so number text does not depend on the machine's settings.</summary>
    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo _previous = CultureInfo.CurrentCulture;

        public CultureScope(string name) => CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name);

        public void Dispose() => CultureInfo.CurrentCulture = _previous;
    }

    private static AwbHoleViewModel BlindClearance()
    {
        var vm = NewHole();
        vm.CentreCount = 1;
        vm.Termination = HoleTermination.Distance;
        return vm;
    }

    // ---- Termination: To is left out until the end face can be picked ----

    [Fact]
    public void NewHole_TerminationListsDistanceAndThroughAll_NotTo()
    {
        var vm = NewHole();

        Assert.Equal(["Distance", "Through All"], Values(vm.TerminationOptions));
        Assert.DoesNotContain(vm.TerminationOptions, o => o.Value == HoleTermination.ToFace);
    }

    [Fact]
    public void NewHole_RefusesTo()
    {
        var vm = NewHole();
        var previews = 0;
        vm.PreviewNeeded += (_, _) => previews++;

        vm.Termination = HoleTermination.ToFace;

        Assert.Equal(HoleTermination.ThroughAll, vm.Termination);
        Assert.Equal(0, previews);
        Assert.Null(vm.TerminationNote);
    }

    [Fact]
    public void EditedHoleEndingAtAFace_ListsTo_WithANote_UntilItIsChanged()
    {
        var spec = MetricSpec(HoleKind.Clearance, "T5") with { Termination = HoleTermination.ToFace };

        var vm = AwbHoleViewModel.ForEdit(Workbook, PartName, spec);

        Assert.Equal(["Distance", "Through All", "To"], Values(vm.TerminationOptions));
        Assert.Equal(HoleTermination.ToFace, vm.Termination);
        Assert.Equal(AwbHoleViewModel.ToFaceNoteText, vm.TerminationNote);
        Assert.Contains("cannot pick the face", vm.TerminationNote);

        vm.Termination = HoleTermination.ThroughAll;

        Assert.Null(vm.TerminationNote);
        Assert.Contains(vm.TerminationOptions, o => o.Value == HoleTermination.ToFace);
    }

    [Fact]
    public void EditedHoleNotEndingAtAFace_DoesNotListTo()
    {
        var vm = AwbHoleViewModel.ForEdit(Workbook, PartName, MetricSpec(HoleKind.Clearance, "T5"));

        Assert.Equal(["Distance", "Through All"], Values(vm.TerminationOptions));
    }

    // ---- Depth boxes ----

    [Fact]
    public void DepthText_SetsTheDepth_AndPreviews()
    {
        using var culture = new CultureScope("en-US");
        var vm = BlindClearance();
        var previews = 0;
        vm.PreviewNeeded += (_, _) => previews++;

        vm.DepthText = " 12.5 ";

        Assert.Equal(12.5m, vm.Depth);
        Assert.Equal("12.5", vm.DepthText);
        Assert.Equal(1, previews);
        Assert.Null(vm.DepthProblem);
        Assert.True(vm.CanOk);
    }

    [Fact]
    public void DepthText_ShowsTheNumberWithoutTrailingZeros()
    {
        using var culture = new CultureScope("en-US");
        var vm = BlindClearance();
        vm.Depth = 12m;

        vm.DepthText = "12.000";

        Assert.Equal(12m, vm.Depth);
        Assert.Equal("12", vm.DepthText);
    }

    [Fact]
    public void DepthText_NotANumber_IsKept_WithAMessage_AndDisablesOk_WithoutAPreview()
    {
        using var culture = new CultureScope("en-US");
        var vm = BlindClearance();
        vm.Depth = 10m;
        var previews = 0;
        vm.PreviewNeeded += (_, _) => previews++;
        var okChanged = 0;
        vm.OkCommand.CanExecuteChanged += (_, _) => okChanged++;

        vm.DepthText = "1O";

        Assert.Equal("1O", vm.DepthText);
        Assert.Equal(10m, vm.Depth);
        Assert.Equal(AwbHoleViewModel.NotANumberText, vm.DepthProblem);
        Assert.True(vm.HasTextThatIsNotANumber);
        Assert.False(vm.CanOk);
        Assert.False(vm.OkCommand.CanExecute(null));
        Assert.True(okChanged > 0);
        Assert.Equal(0, previews);
    }

    [Fact]
    public void DepthText_FixedAfterTextThatIsNotANumber_ClearsTheMessage()
    {
        using var culture = new CultureScope("en-US");
        var vm = BlindClearance();
        vm.Depth = 10m;
        vm.DepthText = "ten";

        vm.DepthText = "10";

        Assert.Equal("10", vm.DepthText);
        Assert.Null(vm.DepthProblem);
        Assert.False(vm.HasTextThatIsNotANumber);
        Assert.True(vm.CanOk);
    }

    [Fact]
    public void AnyOtherChange_PutsTheBoxBackToTheNumber()
    {
        using var culture = new CultureScope("en-US");
        var vm = BlindClearance();
        vm.Depth = 10m;
        vm.DepthText = "ten";

        vm.Flip = true;

        Assert.Equal("10", vm.DepthText);
        Assert.False(vm.HasTextThatIsNotANumber);
    }

    [Fact]
    public void DepthText_Blank_ClearsTheDepth()
    {
        using var culture = new CultureScope("en-US");
        var vm = BlindClearance();
        vm.Depth = 10m;

        vm.DepthText = "  ";

        Assert.Null(vm.Depth);
        Assert.Equal("", vm.DepthText);
        Assert.NotNull(vm.DepthProblem);
        Assert.NotEqual(AwbHoleViewModel.NotANumberText, vm.DepthProblem);
    }

    [Fact]
    public void ThreadAndDrillDepthText_SetTheDepths_AndBlankPutsBackTheDefaults()
    {
        using var culture = new CultureScope("en-US");
        var vm = NewHole();
        vm.Type = HoleStandards.TappedType;
        vm.Termination = HoleTermination.Distance;
        Assert.Equal("4.5", vm.ThreadDepthText);
        Assert.Equal("6", vm.DrillDepthText);

        vm.ThreadDepthText = "5";
        vm.DrillDepthText = "7.25";

        Assert.Equal(5m, vm.ThreadDepth);
        Assert.Equal(7.25m, vm.DrillDepth);

        vm.ThreadDepthText = "";

        Assert.Equal(4.5m, vm.ThreadDepth);
        Assert.Equal("4.5", vm.ThreadDepthText);
    }

    [Fact]
    public void ThreadDepthText_NotANumber_ShowsTheMessageOnThatFieldOnly()
    {
        using var culture = new CultureScope("en-US");
        var vm = NewHole();
        vm.Type = HoleStandards.TappedType;
        vm.Termination = HoleTermination.Distance;

        vm.ThreadDepthText = "deep";

        Assert.Equal(AwbHoleViewModel.NotANumberText, vm.ThreadDepthProblem);
        Assert.NotEqual(AwbHoleViewModel.NotANumberText, vm.DrillDepthProblem);
        Assert.Equal(4.5m, vm.ThreadDepth);
    }

    [Fact]
    public void DepthText_RaisesItsChange_WhenTheUnitsConvertTheDepth()
    {
        using var culture = new CultureScope("en-US");
        var vm = BlindClearance();
        vm.Depth = 25.4m;
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.Units = HoleUnits.Imperial;

        // A typed number keeps its length and is written with its unit (task 124).
        Assert.Equal("25.4 mm", vm.DepthText);
        Assert.Equal("= 1 in", vm.DepthValueText);
        Assert.Contains(nameof(AwbHoleViewModel.DepthText), changed);
        Assert.Contains(nameof(AwbHoleViewModel.LengthUnitText), changed);
    }

    [Theory]
    [InlineData("en-US", "12.5", 12.5)]
    [InlineData("en-US", "-3", -3)]
    [InlineData("de-DE", "12,5", 12.5)]
    [InlineData("de-DE", "12.5", 12.5)]
    [InlineData("en-US", "1e1", 10)]
    public void TryParseNumber_ReadsTheUsersFormat_OrAPoint(string cultureName, string text, double expected)
    {
        using var culture = new CultureScope(cultureName);

        Assert.True(AwbHoleViewModel.TryParseNumber(text, out var value));
        Assert.Equal((decimal)expected, value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("1,5")]
    [InlineData("1,000")]
    [InlineData("12 mm")]
    [InlineData(null)]
    public void TryParseNumber_RefusesTextThatIsNotANumber(string? text)
    {
        using var culture = new CultureScope("en-US");

        Assert.False(AwbHoleViewModel.TryParseNumber(text, out _));
    }

    [Fact]
    public void FormatNumber_UsesTheUsersFormat()
    {
        using (new CultureScope("de-DE"))
        {
            Assert.Equal("12,5", AwbHoleViewModel.FormatNumber(12.50m));
        }

        using (new CultureScope("en-US"))
        {
            Assert.Equal("0.1015", AwbHoleViewModel.FormatNumber(0.1015m));
            Assert.Equal("", AwbHoleViewModel.FormatNumber(null));
        }
    }

    [Fact]
    public void LengthUnitText_FollowsTheUnits()
    {
        var vm = NewHole();
        Assert.Equal("mm", vm.LengthUnitText);

        vm.Units = HoleUnits.Imperial;

        Assert.Equal("in", vm.LengthUnitText);
    }

    // ---- Centres ----

    [Theory]
    [InlineData(0, "None picked")]
    [InlineData(1, "1 picked")]
    [InlineData(3, "3 picked")]
    public void CentresText_CountsThePickedCentres(int count, string expected)
    {
        var vm = NewHole();
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.CentreCount = count;

        Assert.Equal(expected, vm.CentresText);
        if (count > 0)
            Assert.Contains(nameof(AwbHoleViewModel.CentresText), changed);
    }

    [Fact]
    public void CentresHelp_SaysHowToPick_OrThatTheCentresCannotBePicked()
    {
        var vm = AwbHoleViewModel.ForEdit(Workbook, PartName, MetricSpec(HoleKind.Clearance, "T5"));
        Assert.True(vm.CanPickCentres);
        Assert.Equal(AwbHoleViewModel.CentresHelpText, vm.CentresHelp);
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.CanPickCentres = false;

        Assert.Equal(AwbHoleViewModel.CentresNotPickableText, vm.CentresHelp);
        Assert.Contains("not sketch points", vm.CentresHelp);
        Assert.Contains(nameof(AwbHoleViewModel.CentresHelp), changed);
    }

    // ---- The add-in's notes on the hole as made ----

    [Fact]
    public void ShowNotes_OneNote_IsTheStatus_NotAnError()
    {
        var vm = NewHole();

        vm.ShowNotes(["  The thread is modelled at Inventor's minor diameter. ", ""]);

        Assert.Equal("The thread is modelled at Inventor's minor diameter.", vm.StatusText);
        Assert.False(vm.StatusIsError);
        Assert.Equal(["The thread is modelled at Inventor's minor diameter."], vm.StatusNotes);
    }

    [Fact]
    public void ShowNotes_SeveralNotes_AreCounted_AndListedBeforeTheWorkbookProblems()
    {
        var vm = new AwbHoleViewModel(Workbook, PartName, [new HoleWorkbookProblem("Dowel", 5, "Bad.")]);

        vm.ShowNotes(["First.", "Second.", "First."]);

        Assert.Equal("2 notes on this hole; see the details.", vm.StatusText);
        Assert.Equal(["First.", "Second.", "Dowel tab, row 5: Bad."], vm.StatusNotes);
    }

    [Fact]
    public void ShowNotes_ClearsARefusal()
    {
        var vm = NewHole();
        vm.ShowRefused("No.");

        vm.ShowNotes(["Made again at the end of the browser."]);

        Assert.False(vm.IsRefused);
        Assert.False(vm.StatusIsError);
        Assert.Equal("Made again at the end of the browser.", vm.StatusText);
    }

    [Fact]
    public void ShowNotes_None_PutsBackTheStatusAtOpen()
    {
        var vm = new AwbHoleViewModel(Workbook, PartName, [new HoleWorkbookProblem("Dowel", 5, "Bad.")]);
        vm.ShowNotes(["A note."]);

        vm.ShowNotes([]);

        Assert.Equal("The hole workbook has 1 problem; see the details.", vm.StatusText);
        Assert.Equal(["Dowel tab, row 5: Bad."], vm.StatusNotes);

        var changed = 0;
        vm.PropertyChanged += (_, _) => changed++;
        vm.ShowNotes(null);
        Assert.Equal(0, changed);
    }

    [Fact]
    public void NotesText_ForNoneOneAndSeveral()
    {
        Assert.Equal("", AwbHoleViewModel.NotesText([]));
        Assert.Equal("A.", AwbHoleViewModel.NotesText(["A."]));
        Assert.Equal("3 notes on this hole; see the details.", AwbHoleViewModel.NotesText(["A.", "B.", "C."]));
    }

    // ---- Opening on an existing hole (for task 092) ----

    [Fact]
    public void ForReadBack_AnAwbHole_OpensOnItsStoredSpec()
    {
        var spec = MetricSpec(HoleKind.Clearance, "T5") with { ClearanceFit = ClearanceFit.Loose };
        var readBack = new HoleReadBackResult(spec, HoleSource.Awb, null, []);

        var vm = AwbHoleViewModel.ForReadBack(Workbook, PartName, readBack);

        Assert.True(vm.IsEdit);
        Assert.False(vm.IsInventorHole);
        Assert.Equal("Loose", vm.Fit);
        Assert.Equal(spec.Normalised(), vm.Spec);
    }

    [Fact]
    public void ForReadBack_AnInventorHole_OpensOnTheClosestSpec_WithTheNotes()
    {
        var spec = MetricSpec(HoleKind.Clearance, "T3");
        var readBack = new HoleReadBackResult(spec, HoleSource.Inventor, null, ["The closest size is picked."]);
        HoleWorkbookProblem[] problems = [new("Dowel", 5, "Bad.")];

        var vm = AwbHoleViewModel.ForReadBack(Workbook, PartName, readBack, problems);

        Assert.True(vm.IsEdit);
        Assert.True(vm.IsInventorHole);
        Assert.Equal(["The closest size is picked."], vm.InventorHoleNotes);
        Assert.Equal("The hole workbook has 1 problem; see the details.", vm.StatusText);
    }

    // ---- A workbook that stops the window ----

    [Fact]
    public void WorkbookStopMessage_None_ForAWorkbookWithSizes_EvenWithProblems()
    {
        var result = new HoleWorkbookReadResult(Workbook, [new HoleWorkbookProblem(null, null, "Something.")]);

        Assert.Null(AwbHoleViewModel.WorkbookStopMessage(result));
    }

    [Fact]
    public void WorkbookStopMessage_AnEmptyWorkbook_GivesTheFileProblems()
    {
        var result = new HoleWorkbookReadResult(
            HoleWorkbook.Empty,
            [
                new HoleWorkbookProblem(null, null, "The hole workbook set in Settings was not found at X."),
                new HoleWorkbookProblem("Clearance", 5, "A row problem."),
                new HoleWorkbookProblem("Tapped", null, "The tab is missing."),
            ]);

        Assert.Equal(
            "The hole workbook set in Settings was not found at X." + Environment.NewLine + "Tapped tab: The tab is missing.",
            AwbHoleViewModel.WorkbookStopMessage(result));
    }

    [Fact]
    public void WorkbookStopMessage_AnEmptyWorkbookWithNoReason_SaysItHasNoSizes()
    {
        var result = new HoleWorkbookReadResult(HoleWorkbook.Empty, [new HoleWorkbookProblem("Clearance", 5, "Row.")]);

        Assert.Equal(AwbHoleViewModel.NoHoleSizesText, AwbHoleViewModel.WorkbookStopMessage(result));
    }

    [Fact]
    public void WorkbookStopMessage_OnlyDowelRows_IsEnough()
    {
        var onlyDowel = HoleWorkbook.Empty with { Dowel = Workbook.Dowel };

        Assert.Null(AwbHoleViewModel.WorkbookStopMessage(new HoleWorkbookReadResult(onlyDowel, [])));
    }

    [Fact]
    public void WrongDocumentText_NamesTheCommandAndParts()
    {
        Assert.Equal("AWB Hole works in part files. Open a part and try again.", AwbHoleViewModel.WrongDocumentText);
    }
}
