using System.ComponentModel;
using System.Globalization;
using InventorAddin.Core.Holes;
using InventorAddin.Core.ViewModels;

namespace InventorAddin.Core.Tests.ViewModels;

/// <summary>
/// Text boxes update the preview as you type (spec 09, "The window"; task 130): about 300 ms after the last keystroke,
/// with no focus change; incomplete or invalid text keeps the last good value and shows its message; Enter, leaving
/// the box and OK apply at once. On a fake clock.
/// </summary>
public sealed class AwbHoleLiveTypingViewModelTests : IDisposable
{
    private const string PartName = "Test bracket.ipt";

    private static readonly HoleWorkbook Workbook = AwbHoleTestWorkbook.Create();

    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;

    public AwbHoleLiveTypingViewModelTests() => CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");

    public void Dispose() => CultureInfo.CurrentCulture = _culture;

    /// <summary><c>d0</c> is 20 mm and <c>d0+2</c> 22 mm; anything else is not a length. Counts its calls.</summary>
    private sealed class FakeChecker : IHoleExpressionChecker
    {
        public List<string> Calls { get; } = [];

        public HoleExpressionCheck Check(string expression, HoleUnits units)
        {
            Calls.Add(expression);
            return expression switch
            {
                "d0" => HoleExpressionCheck.Valid(2.0),
                "d0+2" => HoleExpressionCheck.Valid(2.2),
                _ => HoleExpressionCheck.Invalid(),
            };
        }
    }

    private sealed class Window
    {
        public Window(IHoleExpressionChecker? checker = null)
        {
            Vm = new AwbHoleViewModel(Workbook, PartName, expressionChecker: checker);
            Vm.CentreCount = 1;
            Vm.Termination = HoleTermination.Distance;
            Vm.DepthText = "10";
            Vm.UseTypingTimer(Timer);
            Vm.PreviewNeeded += (_, _) => Previews++;
            Vm.OkRequested += (_, _) => Oks.Add(Vm.Spec);
            Vm.PropertyChanged += (_, e) => Changed.Add(e.PropertyName);
        }

        public AwbHoleViewModel Vm { get; }

        public FakeDebounceTimer Timer { get; } = new();

        public int Previews { get; set; }

        public List<HoleSpec> Oks { get; } = [];

        public List<string?> Changed { get; } = [];

        /// <summary>Types <paramref name="text"/> one key at a time, as a binding updating on each keystroke does.</summary>
        public void Type(string text, Func<string> box, Action<string> set, int msBetweenKeys = 50)
        {
            foreach (var key in text)
            {
                set(box() + key);
                Timer.Advance(msBetweenKeys);
            }
        }
    }

    [Fact]
    public void TypingADepth_ChangesThePreview300MsAfterTheLastKey_WithNoFocusChange()
    {
        var w = new Window();
        w.Vm.DepthText = "";

        w.Type("12", () => w.Vm.DepthText, t => w.Vm.DepthText = t);

        Assert.Equal(0, w.Previews);
        Assert.Equal(10m, w.Vm.Depth);
        Assert.Equal("12", w.Vm.DepthText);

        w.Timer.Advance(249);
        Assert.Equal(0, w.Previews);

        w.Timer.Advance(1);
        Assert.Equal(12m, w.Vm.Depth);
        Assert.Equal(1, w.Previews);
        Assert.Equal("12", w.Vm.DepthText);
    }

    [Fact]
    public void TheBox_IsNotRewrittenWhileTyping_SoADecimalPointStays()
    {
        var w = new Window();

        w.Vm.DepthText = "1.";
        w.Timer.Advance(300);

        Assert.Equal(1m, w.Vm.Depth);
        Assert.Equal("1.", w.Vm.DepthText);

        w.Vm.DepthText = "1.5";
        w.Timer.Advance(300);
        w.Vm.DepthText = "1.50";
        w.Vm.CommitTyping();

        Assert.Equal(1.5m, w.Vm.Depth);
        Assert.Equal("1.5", w.Vm.DepthText);
    }

    [Fact]
    public void SettingTheText_DoesNotRaiseItsOwnChange_UntilCommitted()
    {
        var w = new Window();

        w.Vm.DepthText = "12";

        Assert.DoesNotContain(nameof(AwbHoleViewModel.DepthText), w.Changed);

        w.Vm.CommitTyping();

        Assert.Contains(nameof(AwbHoleViewModel.DepthText), w.Changed);
    }

    [Fact]
    public void AnIncompleteExpression_ShowsItsMessage_AndKeepsTheLastPreview_UntilFinished()
    {
        var checker = new FakeChecker();
        var w = new Window(checker);
        w.Vm.DepthText = "d0";
        w.Timer.Advance(300);
        Assert.Equal(20m, w.Vm.Depth);
        var previews = w.Previews;
        var spec = w.Vm.Spec;

        w.Vm.DepthText = "d0+";
        w.Timer.Advance(300);

        Assert.Equal(20m, w.Vm.Depth);
        Assert.Equal(spec, w.Vm.Spec);
        Assert.Equal(previews, w.Previews);
        Assert.Equal(HoleExpressions.NotALengthText, w.Vm.DepthProblem);
        Assert.Equal("d0+", w.Vm.DepthText);
        Assert.False(w.Vm.CanOk);
        Assert.False(w.Vm.OkCommand.CanExecute(null));

        w.Vm.DepthText = "d0+2";

        Assert.Equal(HoleExpressions.NotALengthText, w.Vm.DepthProblem);

        w.Timer.Advance(300);

        Assert.Equal(22m, w.Vm.Depth);
        Assert.Equal(previews + 1, w.Previews);
        Assert.Null(w.Vm.DepthProblem);
        Assert.Equal("= 22 mm", w.Vm.DepthValueText);
        Assert.True(w.Vm.CanOk);
    }

    [Fact]
    public void ExpressionChecks_RunOnlyAfterThePause()
    {
        var checker = new FakeChecker();
        var w = new Window(checker);
        checker.Calls.Clear();
        w.Vm.DepthText = "";

        w.Type("d0+2", () => w.Vm.DepthText, t => w.Vm.DepthText = t);

        Assert.Empty(checker.Calls);

        w.Timer.Advance(300);

        Assert.Equal(["d0+2"], checker.Calls);
        Assert.Equal(22m, w.Vm.Depth);
    }

    [Fact]
    public void Commit_AppliesAtOnce_AsEnterOrLeavingTheBoxDo()
    {
        var w = new Window();

        w.Vm.DepthText = "12";
        w.Vm.CommitTyping();

        Assert.Equal(12m, w.Vm.Depth);
        Assert.Equal(1, w.Previews);
        Assert.False(w.Vm.HasPendingTyping);
        Assert.False(w.Timer.IsRunning);

        w.Timer.Advance(300);
        Assert.Equal(1, w.Previews);
    }

    [Fact]
    public void Ok_StraightAfterTyping_UsesTheTypedValue()
    {
        var w = new Window();

        w.Vm.DepthText = "12";
        w.Vm.OkCommand.Execute(null);

        Assert.Single(w.Oks);
        Assert.Equal(12m, w.Oks[0].Depth);
    }

    [Fact]
    public void Ok_StraightAfterTypingTextThatIsNotALength_DoesNotOk()
    {
        var w = new Window();

        w.Vm.DepthText = "12x";

        Assert.True(w.Vm.OkCommand.CanExecute(null));

        w.Vm.OkCommand.Execute(null);

        Assert.Empty(w.Oks);
        Assert.Equal(AwbHoleViewModel.NotANumberText, w.Vm.DepthProblem);
        Assert.Equal(10m, w.Vm.Depth);
    }

    [Fact]
    public void Ok_IsEnabledWhileFixedTextWaitsForThePause()
    {
        var w = new Window();
        w.Vm.DepthText = "12x";
        w.Timer.Advance(300);
        Assert.False(w.Vm.OkCommand.CanExecute(null));
        var raised = 0;
        w.Vm.OkCommand.CanExecuteChanged += (_, _) => raised++;

        w.Vm.DepthText = "12";

        Assert.True(raised > 0);
        Assert.True(w.Vm.OkCommand.CanExecute(null));

        w.Vm.OkCommand.Execute(null);

        Assert.Single(w.Oks);
        Assert.Equal(12m, w.Oks[0].Depth);
    }

    [Fact]
    public void Cancel_DropsTextWaitingForThePause()
    {
        var w = new Window();
        var cancels = 0;
        w.Vm.CancelRequested += (_, _) => cancels++;

        w.Vm.DepthText = "12";
        w.Vm.CancelCommand.Execute(null);
        w.Timer.Advance(300);

        Assert.Equal(1, cancels);
        Assert.Equal(10m, w.Vm.Depth);
        Assert.Equal("10", w.Vm.DepthText);
        Assert.Equal(0, w.Previews);
    }

    [Fact]
    public void Discard_DropsTheText_AndTheBoxShowsTheFieldAgain()
    {
        var w = new Window();

        w.Vm.DepthText = "12";
        w.Vm.DiscardTyping();
        w.Timer.Advance(300);

        Assert.Equal(10m, w.Vm.Depth);
        Assert.Equal("10", w.Vm.DepthText);
        Assert.Contains(nameof(AwbHoleViewModel.DepthText), w.Changed);
    }

    [Fact]
    public void ABlankBox_WaitsForACommit_SoClearingItToRetypeKeepsThePreview()
    {
        var w = new Window();

        w.Vm.DepthText = "";
        w.Timer.Advance(300);

        Assert.Equal(10m, w.Vm.Depth);
        Assert.Equal("", w.Vm.DepthText);
        Assert.Equal(0, w.Previews);

        w.Vm.CommitTyping();

        Assert.Null(w.Vm.Depth);
    }

    [Fact]
    public void AChangeElsewhere_FirstAppliesWhatIsTyped()
    {
        var w = new Window();

        w.Vm.DepthText = "12";
        w.Vm.Flip = true;

        Assert.Equal(12m, w.Vm.Depth);
        Assert.True(w.Vm.Spec.Flip);
        Assert.False(w.Vm.HasPendingTyping);
        Assert.Equal("12", w.Vm.DepthText);
    }

    [Fact]
    public void AUnitsSwitch_KeepsTheTypedDepth_AsTyping124Does()
    {
        var w = new Window();

        w.Vm.DepthText = "12";
        w.Vm.Units = HoleUnits.Imperial;

        Assert.Equal(HoleUnits.Imperial, w.Vm.Units);
        Assert.Equal("12 mm", w.Vm.DepthText);
    }

    [Fact]
    public void ThreadAndDrillDepthBoxes_FollowTheSameRule()
    {
        var w = new Window();
        w.Vm.Type = HoleStandards.TappedType;
        w.Vm.Size = "M5";
        var previews = w.Previews;

        w.Vm.ThreadDepthText = "9";
        Assert.Equal(previews, w.Previews);
        w.Timer.Advance(300);
        Assert.Equal(9m, w.Vm.ThreadDepth);

        w.Vm.DrillDepthText = "14";
        w.Timer.Advance(300);
        Assert.Equal(14m, w.Vm.DrillDepth);
        Assert.Equal(previews + 2, w.Previews);
    }

    [Fact]
    public void SeatBoxes_FollowTheSameRule()
    {
        var w = new Window();
        w.Vm.Seat = HoleSeat.Counterbore;

        w.Vm.CounterboreDiameterText = "11";
        w.Vm.CounterboreDepthText = "4";
        Assert.NotEqual(11m, w.Vm.CounterboreDiameter);
        w.Timer.Advance(300);

        Assert.Equal(11m, w.Vm.CounterboreDiameter);
        Assert.Equal(4m, w.Vm.CounterboreDepth);
    }

    [Fact]
    public void TheCountersinkAngle_TypedWaitsForThePause_PickedAppliesAtOnce()
    {
        var w = new Window();
        w.Vm.Seat = HoleSeat.Countersink;
        w.Vm.CountersinkDiameterText = "9";
        w.Vm.CommitTyping();

        // Typed: after the pause.
        w.Vm.CountersinkAngleText = "8";
        w.Vm.CountersinkAngleText = "82";
        Assert.NotEqual(82m, w.Vm.CountersinkAngle);
        w.Timer.Advance(300);
        Assert.Equal(82m, w.Vm.CountersinkAngle);

        // Picked from the list: the window commits on DropDownClosed.
        w.Vm.CountersinkAngleText = "60";
        w.Vm.CommitTyping();
        Assert.Equal(60m, w.Vm.CountersinkAngle);
        Assert.Equal("60", w.Vm.CountersinkAngleText);
    }

    [Fact]
    public void TheDiameterAndCustomLimitBoxes_FollowTheSameRule()
    {
        var w = new Window();
        w.Vm.Type = HoleStandards.SimpleType;

        w.Vm.DiameterText = "6";
        Assert.Null(w.Vm.Diameter);
        w.Timer.Advance(300);
        Assert.Equal(6m, w.Vm.Diameter);

        w.Vm.Toleranced = true;
        w.Vm.Tolerance = HoleToleranceOption.CustomValue;
        w.Vm.ToleranceUpperText = "0.02";
        w.Timer.Advance(300);
        Assert.Equal(0.02m, w.Vm.ToleranceUpper);
    }

    [Fact]
    public void WithoutATimer_EverySetAppliesAtOnce_AsBefore()
    {
        var vm = new AwbHoleViewModel(Workbook, PartName) { CentreCount = 1 };
        vm.Termination = HoleTermination.Distance;

        vm.DepthText = "12";

        Assert.Equal(12m, vm.Depth);
        Assert.False(vm.HasPendingTyping);
    }

    [Fact]
    public void TheTimer_CanBeSetOnce()
    {
        var vm = new AwbHoleViewModel(Workbook, PartName);
        vm.UseTypingTimer(new FakeDebounceTimer());

        Assert.Throws<InvalidOperationException>(() => vm.UseTypingTimer(new FakeDebounceTimer()));
        Assert.Throws<ArgumentNullException>(() => new AwbHoleViewModel(Workbook, PartName).UseTypingTimer(null!));
    }

    [Fact]
    public void ProblemChanges_AreRaisedAfterThePause_SoTheMessageShowsWithoutLeavingTheBox()
    {
        var w = new Window();
        w.Vm.DepthText = "12x";
        Assert.DoesNotContain(nameof(AwbHoleViewModel.DepthProblem), w.Changed);

        w.Timer.Advance(300);

        Assert.Contains(nameof(AwbHoleViewModel.DepthProblem), w.Changed);
        Assert.Equal("12x", w.Vm.DepthText);
    }
}
