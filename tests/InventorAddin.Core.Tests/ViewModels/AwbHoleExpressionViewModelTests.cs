using System.Globalization;
using InventorAddin.Core.Holes;
using InventorAddin.Core.ViewModels;

namespace InventorAddin.Core.Tests.ViewModels;

/// <summary>The depth boxes take Inventor parameters and expressions (task 100), read by a fake checker.</summary>
public sealed class AwbHoleExpressionViewModelTests
{
    private const string PartName = "Test bracket.ipt";

    private static readonly HoleWorkbook Workbook = AwbHoleTestWorkbook.Create();

    /// <summary>
    /// Knows a few expressions, as the part's parameters would make them: <c>d0</c> is 20 mm, <c>12 mm</c> is 12 mm,
    /// <c>d0+5 mm</c> is 25 mm, and <c>2*d0</c> is 40 mm. A number with no unit in <c>d0+1</c> is in the units asked
    /// for, as Inventor reads it. <c>bad</c> has a reason; anything else has none.
    /// </summary>
    private sealed class FakeChecker : IHoleExpressionChecker
    {
        public List<(string Expression, HoleUnits Units)> Calls { get; } = [];

        public HoleExpressionCheck Check(string expression, HoleUnits units)
        {
            Calls.Add((expression, units));
            return expression switch
            {
                "d0" => HoleExpressionCheck.Valid(2.0),
                "12 mm" => HoleExpressionCheck.Valid(1.2),
                "d0+5 mm" => HoleExpressionCheck.Valid(2.5),
                "2*d0" => HoleExpressionCheck.Valid(4.0),
                "d0+1" => HoleExpressionCheck.Valid(units == HoleUnits.Metric ? 2.1 : 4.54),
                "bad" => HoleExpressionCheck.Invalid("Parameter bad does not exist."),
                _ => HoleExpressionCheck.Invalid(),
            };
        }
    }

    /// <summary>Runs the test in a culture, so number text does not depend on the machine's settings.</summary>
    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo _previous = CultureInfo.CurrentCulture;

        public CultureScope(string name) => CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name);

        public void Dispose() => CultureInfo.CurrentCulture = _previous;
    }

    private static AwbHoleViewModel BlindClearance(IHoleExpressionChecker? checker)
    {
        var vm = new AwbHoleViewModel(Workbook, PartName, expressionChecker: checker);
        vm.CentreCount = 1;
        vm.Termination = HoleTermination.Distance;
        return vm;
    }

    private static AwbHoleViewModel BlindTapped(IHoleExpressionChecker? checker)
    {
        var vm = BlindClearance(checker);
        vm.Type = HoleStandards.TappedType;
        vm.Size = "M5";
        return vm;
    }

    [Fact]
    public void AParameter_SetsTheDepthToItsValue_AndTheBoxShowsIt()
    {
        using var culture = new CultureScope("en-US");
        var vm = BlindClearance(new FakeChecker());
        var previews = 0;
        vm.PreviewNeeded += (_, _) => previews++;

        vm.DepthText = " d0 ";

        Assert.Equal(20m, vm.Depth);
        Assert.Equal("d0", vm.DepthText);
        Assert.Equal("d0", vm.Spec.DepthExpression);
        Assert.Equal("d0", vm.Geometry!.DepthExpression);
        Assert.Null(vm.DepthProblem);
        Assert.True(vm.CanOk);
        Assert.Equal(1, previews);
    }

    [Fact]
    public void AUnitInTheText_IsTakenInThatUnit()
    {
        using var culture = new CultureScope("en-US");
        var vm = BlindClearance(new FakeChecker());
        vm.Units = HoleUnits.Imperial;
        vm.Termination = HoleTermination.Distance;

        vm.DepthText = "12 mm";

        Assert.Equal(0.47244m, vm.Depth);
        Assert.Equal("12 mm", vm.DepthText);
        Assert.Equal(HoleUnits.Imperial, vm.Geometry!.ExpressionUnits);
    }

    [Fact]
    public void AnExpression_IsCheckedInTheWindowsUnits()
    {
        var checker = new FakeChecker();
        var vm = BlindClearance(checker);

        vm.DepthText = "d0+1";

        Assert.Equal(("d0+1", HoleUnits.Metric), checker.Calls.Last());
        Assert.Equal(21m, vm.Depth);
    }

    [Fact]
    public void APlainNumber_IsNotAnExpression_AndIsNotChecked()
    {
        using var culture = new CultureScope("en-US");
        var checker = new FakeChecker();
        var vm = BlindClearance(checker);
        vm.DepthText = "d0";

        vm.DepthText = "12.5";

        Assert.Equal(12.5m, vm.Depth);
        Assert.Equal("12.5", vm.DepthText);
        Assert.Null(vm.Spec.DepthExpression);
        Assert.DoesNotContain(checker.Calls, c => c.Expression == "12.5");
    }

    [Fact]
    public void ANumberEqualToTheExpressionsValue_StillReplacesTheExpression()
    {
        using var culture = new CultureScope("en-US");
        var vm = BlindClearance(new FakeChecker());
        vm.DepthText = "d0";
        var previews = 0;
        vm.PreviewNeeded += (_, _) => previews++;

        vm.DepthText = "20";

        Assert.Equal("20", vm.DepthText);
        Assert.Null(vm.Spec.DepthExpression);
        Assert.Equal(1, previews);
    }

    [Fact]
    public void TextInventorCannotRead_ShowsItsReason_AndDisablesOk()
    {
        var vm = BlindClearance(new FakeChecker());
        vm.DepthText = "d0";

        vm.DepthText = "bad";

        Assert.Equal("bad", vm.DepthText);
        Assert.Equal("Parameter bad does not exist.", vm.DepthProblem);
        Assert.Equal(20m, vm.Depth);
        Assert.Equal("d0", vm.Spec.DepthExpression);
        Assert.True(vm.HasTextThatIsNotANumber);
        Assert.False(vm.CanOk);
    }

    [Fact]
    public void TextInventorCannotRead_WithNoReason_ShowsTheNotALengthText()
    {
        var vm = BlindClearance(new FakeChecker());

        vm.DepthText = "abc";

        Assert.Equal(HoleExpressions.NotALengthText, vm.DepthProblem);
        Assert.False(vm.CanOk);

        vm.DepthText = "d0";
        Assert.Null(vm.DepthProblem);
        Assert.True(vm.CanOk);
    }

    [Fact]
    public void WithoutAChecker_TextThatIsNotANumber_IsRefusedAsBefore()
    {
        var vm = BlindClearance(null);

        vm.DepthText = "d0";

        Assert.Equal(AwbHoleViewModel.NotANumberText, vm.DepthProblem);
        Assert.False(vm.CanOk);
    }

    [Fact]
    public void TappedDepths_TakeExpressions_AndTheChecksUseTheirValues()
    {
        var vm = BlindTapped(new FakeChecker());

        vm.DrillDepthText = "d0";
        vm.ThreadDepthText = "d0+5 mm";

        Assert.Equal(25m, vm.ThreadDepth);
        Assert.Equal(20m, vm.DrillDepth);
        Assert.NotNull(vm.ThreadDepthProblem);
        Assert.False(vm.CanOk);

        vm.DrillDepthText = "2*d0";

        Assert.Null(vm.ThreadDepthProblem);
        Assert.True(vm.CanOk);
        Assert.Equal("2*d0", vm.Geometry!.DepthExpression);
        Assert.Equal("d0+5 mm", vm.Geometry.Thread!.ThreadDepthExpression);
    }

    [Fact]
    public void AUnitsSwitch_TurnsTheExpressionIntoANumber()
    {
        using var culture = new CultureScope("en-US");
        var vm = BlindClearance(new FakeChecker());
        vm.DepthText = "d0";

        vm.Units = HoleUnits.Imperial;

        Assert.Equal(0.7874m, vm.Depth);
        Assert.Equal("0.7874", vm.DepthText);
        Assert.Null(vm.Spec.DepthExpression);
    }

    [Fact]
    public void ANewSize_PutsBackTheTappedRowsDepths_WithoutTheExpressions()
    {
        using var culture = new CultureScope("en-US");
        var vm = BlindTapped(new FakeChecker());
        vm.ThreadDepthText = "d0";

        vm.Size = "M3";

        Assert.Equal(4.5m, vm.ThreadDepth);
        Assert.Equal("4.5", vm.ThreadDepthText);
        Assert.Null(vm.Spec.ThreadDepthExpression);
    }

    [Fact]
    public void AnotherChange_KeepsTheExpression()
    {
        var vm = BlindClearance(new FakeChecker());
        vm.DepthText = "d0";

        vm.Flip = true;

        Assert.Equal("d0", vm.DepthText);
        Assert.Equal("d0", vm.Spec.DepthExpression);
    }

    // ---- Editing a hole ----

    private static HoleSpec EditedClearance(string expression, decimal depth) =>
        HoleSpec.ClearanceHole(HoleUnits.Metric, "T5") with
        {
            Termination = HoleTermination.Distance,
            Depth = depth,
            DepthExpression = expression,
        };

    [Fact]
    public void Edit_ShowsTheHolesExpression_AtItsValueNow()
    {
        // The hole was made when d0 was 15 mm; it is 20 mm now.
        var vm = AwbHoleViewModel.ForEdit(
            Workbook, PartName, EditedClearance("d0", 15m), expressionChecker: new FakeChecker());

        Assert.Equal("d0", vm.DepthText);
        Assert.Equal(20m, vm.Depth);
        Assert.Equal("d0", vm.Spec.DepthExpression);
    }

    [Fact]
    public void Edit_WithoutAChecker_ShowsTheStoredExpressionAndValue()
    {
        var vm = AwbHoleViewModel.ForEdit(Workbook, PartName, EditedClearance("d0", 15m));

        Assert.Equal("d0", vm.DepthText);
        Assert.Equal(15m, vm.Depth);
        Assert.Equal("d0", vm.Spec.DepthExpression);
    }

    [Fact]
    public void Edit_AnExpressionInventorCanNoLongerRead_IsShownAsNotALength()
    {
        var vm = AwbHoleViewModel.ForEdit(
            Workbook, PartName, EditedClearance("gone", 15m), expressionChecker: new FakeChecker());
        vm.CentreCount = 1;

        Assert.Equal("gone", vm.DepthText);
        Assert.Equal(HoleExpressions.NotALengthText, vm.DepthProblem);
        Assert.False(vm.CanOk);
    }

    [Fact]
    public void Edit_TappedDepthExpressions_AreShown()
    {
        var spec = HoleSpec.ClearanceHole(HoleUnits.Metric, "M5") with
        {
            Kind = HoleKind.Tapped,
            Termination = HoleTermination.Distance,
            ThreadDepth = 20m,
            DrillDepth = 25m,
            ThreadDepthExpression = "d0",
            DrillDepthExpression = "d0+5 mm",
        };

        var vm = AwbHoleViewModel.ForEdit(Workbook, PartName, spec, expressionChecker: new FakeChecker());

        Assert.Equal("d0", vm.ThreadDepthText);
        Assert.Equal("d0+5 mm", vm.DrillDepthText);
        Assert.Equal(25m, vm.DrillDepth);
    }
}
