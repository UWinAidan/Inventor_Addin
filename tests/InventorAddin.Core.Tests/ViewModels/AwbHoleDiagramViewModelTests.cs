using System.ComponentModel;
using System.Globalization;
using InventorAddin.Core.Holes;
using InventorAddin.Core.ViewModels;

namespace InventorAddin.Core.Tests.ViewModels;

/// <summary>
/// What the redesigned AWB Hole window (task 104) needs from the view-model: an icon for each choice, the diagram
/// drawn from the fields, its summary and open state, and the value an expression comes to.
/// </summary>
public sealed class AwbHoleDiagramViewModelTests
{
    private const string PartName = "Test bracket.ipt";
    private const string Dia = HoleDiagram.DiameterSign;

    private static readonly HoleWorkbook Workbook = AwbHoleTestWorkbook.Create();

    /// <summary>Knows two expressions: <c>d0</c> is 20 mm and <c>d0/4</c> is 5 mm.</summary>
    private sealed class FakeChecker : IHoleExpressionChecker
    {
        public HoleExpressionCheck Check(string expression, HoleUnits units) => expression switch
        {
            "d0" => HoleExpressionCheck.Valid(2.0),
            "d0/4" => HoleExpressionCheck.Valid(0.5),
            _ => HoleExpressionCheck.Invalid(),
        };
    }

    /// <summary>Runs the test in a culture, so number text does not depend on the machine's settings.</summary>
    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo _previous = CultureInfo.CurrentCulture;

        public CultureScope(string name) => CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name);

        public void Dispose() => CultureInfo.CurrentCulture = _previous;
    }

    private static AwbHoleViewModel NewHole() =>
        new(Workbook, PartName, expressionChecker: new FakeChecker()) { CentreCount = 1 };

    private static AwbHoleViewModel BlindDowelWithCounterbore()
    {
        var vm = NewHole();
        vm.Standard = HoleStandards.DowelStandard;
        vm.Size = "5 mm";
        vm.Termination = HoleTermination.Distance;
        vm.Seat = HoleSeat.Counterbore;
        vm.CounterboreDiameterText = "9";
        vm.CounterboreDepthText = "3";
        vm.DepthText = "12";
        return vm;
    }

    // ---- Icons ----

    [Fact]
    public void TypeSeatAndTermination_EachNameTheirIcon()
    {
        var vm = NewHole();

        Assert.Equal([HoleIcons.Clearance, HoleIcons.Tapped], AwbHoleViewModel.TypeOptions.Select(o => o.Icon));
        Assert.Equal(
            [HoleIcons.SeatNone, HoleIcons.Counterbore, HoleIcons.Countersink], vm.SeatOptions.Select(o => o.Icon));
        Assert.Equal([HoleIcons.Distance, HoleIcons.ThroughAll], vm.TerminationOptions.Select(o => o.Icon));
    }

    [Fact]
    public void EditedHoleEndingAtAFace_ToHasItsOwnIcon()
    {
        var spec = HoleSpec.ClearanceHole(HoleUnits.Metric, "T5") with { Termination = HoleTermination.ToFace };

        var vm = AwbHoleViewModel.ForEdit(Workbook, PartName, spec);

        Assert.Equal(
            [HoleIcons.Distance, HoleIcons.ThroughAll, HoleIcons.ToFace], vm.TerminationOptions.Select(o => o.Icon));
    }

    [Fact]
    public void DrillPoints_TakeTheirIconFromTheRowsAngle()
    {
        var vm = NewHole();
        vm.Termination = HoleTermination.Distance;

        Assert.Equal(["118 deg", "90 deg", "Flat"], vm.DrillPointOptions.Select(o => o.Text));
        Assert.Equal(
            [HoleIcons.PointShallow, HoleIcons.PointSteep, HoleIcons.FlatBottom], vm.DrillPointOptions.Select(o => o.Icon));
    }

    [Fact]
    public void EditedHoleWithADrillPointNotInTheWorkbook_ReadsTheIconFromItsName()
    {
        var spec = HoleSpec.ClearanceHole(HoleUnits.Metric, "T5") with
        {
            Termination = HoleTermination.Distance,
            Depth = 10m,
            DrillPoint = "75 deg special",
        };

        var vm = AwbHoleViewModel.ForEdit(Workbook, PartName, spec);

        Assert.Equal(HoleIcons.PointSteep, vm.DrillPointOptions.Single(o => o.Value == "75 deg special").Icon);
    }

    // ---- The diagram ----

    [Fact]
    public void Diagram_BlindDowelWithCounterbore_ShowsTheFieldsValues()
    {
        using var culture = new CultureScope("en-US");
        var vm = BlindDowelWithCounterbore();

        var texts = vm.Diagram.Labels.Select(l => l.Text).ToList();

        // 5 mm dowel, Normal fit, the Default = Y tolerance (G7); point 118 deg is the first Default = Y row.
        Assert.Contains(Dia + "5 G7", texts);
        Assert.Contains("12", texts);
        Assert.Contains(Dia + "9", texts);
        Assert.Contains("3", texts);
        Assert.Contains("118" + HoleDiagram.DegreeSign, texts);
        Assert.Equal($"{Dia}5 G7, 12 deep, counterbore {Dia}9 by 3", vm.DiagramSummary);
    }

    [Fact]
    public void Diagram_ALimitsTolerance_ShowsItsLimits()
    {
        using var culture = new CultureScope("en-US");
        var vm = BlindDowelWithCounterbore();

        vm.Fit = "Press";

        Assert.Equal("P press", vm.Tolerance);
        Assert.Contains(Dia + "5 0 / -0.010", vm.Diagram.Labels.Select(l => l.Text));
    }

    [Fact]
    public void Diagram_RedrawsOnEveryChange()
    {
        using var culture = new CultureScope("en-US");
        var vm = BlindDowelWithCounterbore();
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        var before = vm.Diagram;

        vm.DepthText = "20";

        Assert.Contains(nameof(AwbHoleViewModel.Diagram), changed);
        Assert.Contains(nameof(AwbHoleViewModel.DiagramSummary), changed);
        Assert.NotSame(before, vm.Diagram);
        Assert.Contains("20", vm.Diagram.Labels.Select(l => l.Text));
        Assert.DoesNotContain("12", vm.Diagram.Labels.Select(l => l.Text));
    }

    [Fact]
    public void Diagram_ExpressionDepths_AreMarked()
    {
        using var culture = new CultureScope("en-US");
        var vm = BlindDowelWithCounterbore();

        vm.DepthText = "d0";
        vm.CounterboreDepthText = "d0/4";

        Assert.True(vm.DiagramInput.DepthIsExpression);
        Assert.True(vm.DiagramInput.CounterboreDepthIsExpression);
        Assert.True(vm.Diagram.Labels.Single(l => l.Text == "20").IsExpression);
        Assert.True(vm.Diagram.Labels.Single(l => l.Text == "5").IsExpression);
    }

    [Fact]
    public void Diagram_ClearanceHole_UsesTheBuiltDiameter_ForItsFit()
    {
        using var culture = new CultureScope("en-US");
        var vm = NewHole();
        vm.Size = "T5";

        vm.Fit = "Loose";

        Assert.Equal(5.8m, vm.DiagramInput.HoleDiameter);
        Assert.Contains(Dia + "5.8", vm.Diagram.Labels.Select(l => l.Text));
        Assert.Equal($"{Dia}5.8, through all", vm.DiagramSummary);
    }

    [Fact]
    public void Diagram_BlindTapped_ShowsTheThreadAndBothDepths()
    {
        using var culture = new CultureScope("en-US");
        var vm = NewHole();
        vm.Type = HoleStandards.TappedType;
        vm.Size = "M5";
        vm.Termination = HoleTermination.Distance;

        var input = vm.DiagramInput;

        Assert.Equal(HoleKind.Tapped, input.Kind);
        Assert.Equal(4.2m, input.HoleDiameter);
        Assert.Equal(5m, input.ThreadDiameter);
        Assert.Equal(7.5m, input.ThreadDepth);
        Assert.Equal(10m, input.Depth);
        Assert.StartsWith("M5", input.ThreadName);
        Assert.Equal(118m, input.PointAngleDeg);
        Assert.Contains("7.5", vm.Diagram.Labels.Select(l => l.Text));
        Assert.Contains("10", vm.Diagram.Labels.Select(l => l.Text));
    }

    [Fact]
    public void Diagram_FlatDrillPoint_HasNoAngle()
    {
        var vm = NewHole();
        vm.Termination = HoleTermination.Distance;

        vm.DrillPoint = "Flat";

        Assert.Null(vm.DiagramInput.PointAngleDeg);
        Assert.DoesNotContain(vm.Diagram.Labels, l => l.Text.EndsWith(HoleDiagram.DegreeSign, StringComparison.Ordinal));
    }

    [Fact]
    public void Diagram_WhileTheHoleDoesNotBuild_IsStillDrawnFromTheFields()
    {
        using var culture = new CultureScope("en-US");
        var vm = BlindDowelWithCounterbore();

        vm.DepthText = "";

        Assert.False(vm.BuildResult.Succeeded);
        Assert.Equal(5m, vm.DiagramInput.HoleDiameter);
        Assert.Null(vm.DiagramInput.Depth);
        Assert.NotEmpty(vm.Diagram.Material);
        Assert.Contains("? deep", vm.DiagramSummary);
    }

    // ---- Open or collapsed ----

    [Fact]
    public void IsDiagramExpanded_StartsTrue_AndChangingItRaisesNoPreview()
    {
        var vm = NewHole();
        var previews = 0;
        var changed = new List<string?>();
        vm.PreviewNeeded += (_, _) => previews++;
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        Assert.True(vm.IsDiagramExpanded);
        vm.IsDiagramExpanded = false;

        Assert.False(vm.IsDiagramExpanded);
        Assert.Equal([nameof(AwbHoleViewModel.IsDiagramExpanded)], changed);
        Assert.Equal(0, previews);
    }

    // ---- What an expression comes to ----

    [Fact]
    public void DepthValueText_ShowsTheValueOfAnExpression_InTheWindowsUnit()
    {
        using var culture = new CultureScope("en-US");
        var vm = BlindDowelWithCounterbore();

        vm.DepthText = "d0";

        Assert.Equal("= 20 mm", vm.DepthValueText);
    }

    [Fact]
    public void DepthValueText_IsNull_ForAPlainNumber_AnEmptyBox_AndTextThatIsNotALength()
    {
        var vm = BlindDowelWithCounterbore();
        Assert.Null(vm.DepthValueText);

        vm.DepthText = "";
        Assert.Null(vm.DepthValueText);

        vm.DepthText = "d0";
        Assert.NotNull(vm.DepthValueText);
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.DepthText = "nonsense";

        Assert.Null(vm.DepthValueText);
        Assert.Contains(nameof(AwbHoleViewModel.DepthValueText), changed);
    }

    [Fact]
    public void CounterboreDepthValueText_ShowsTheValueOfAnExpression()
    {
        using var culture = new CultureScope("en-US");
        var vm = BlindDowelWithCounterbore();

        vm.CounterboreDepthText = "d0/4";

        Assert.Equal("= 5 mm", vm.CounterboreDepthValueText);
        Assert.Null(vm.DepthValueText);
    }

    [Fact]
    public void ThreadAndDrillDepthValueTexts_ShowTheValueOfAnExpression()
    {
        using var culture = new CultureScope("en-US");
        var vm = NewHole();
        vm.Type = HoleStandards.TappedType;
        vm.Size = "M5";
        vm.Termination = HoleTermination.Distance;

        vm.DrillDepthText = "d0";
        vm.ThreadDepthText = "d0/4";

        Assert.Equal("= 20 mm", vm.DrillDepthValueText);
        Assert.Equal("= 5 mm", vm.ThreadDepthValueText);
        Assert.True(vm.DiagramInput.DepthIsExpression);
        Assert.True(vm.DiagramInput.ThreadDepthIsExpression);
    }

    [Fact]
    public void ValueTexts_ArePartOfTheChangeNotifications()
    {
        var vm = BlindDowelWithCounterbore();
        var changed = new List<string?>();
        ((INotifyPropertyChanged)vm).PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.DepthText = "d0";

        Assert.Contains(nameof(AwbHoleViewModel.DepthValueText), changed);
        Assert.Contains(nameof(AwbHoleViewModel.CounterboreDepthValueText), changed);
        Assert.Contains(nameof(AwbHoleViewModel.ThreadDepthValueText), changed);
        Assert.Contains(nameof(AwbHoleViewModel.DrillDepthValueText), changed);
    }
}
