using System.Globalization;
using InventorAddin.Core.Holes;
using InventorAddin.Core.ViewModels;

namespace InventorAddin.Core.Tests.ViewModels;

/// <summary>
/// The Custom tolerance's Code box in the AWB Hole window (task 150; Aidan, 2026-11-09): a typed ISO 286 code shows its
/// limits in Upper and Lower, read-only, and a code that cannot be used is reported under the box. Sizes are invented.
/// </summary>
public sealed class AwbHoleCustomCodeViewModelTests : IDisposable
{
    private const string PartName = "Test bracket.ipt";

    private static readonly HoleWorkbook Workbook = AwbHoleTestWorkbook.Create();

    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;

    public AwbHoleCustomCodeViewModelTests() => CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");

    public void Dispose() => CultureInfo.CurrentCulture = _culture;

    private static AwbHoleViewModel CustomDowel(string size = "5 mm")
    {
        var vm = new AwbHoleViewModel(Workbook, PartName) { CentreCount = 1 };
        vm.Standard = HoleStandards.DowelStandard;
        vm.Size = size;
        vm.Tolerance = HoleToleranceOption.CustomValue;
        return vm;
    }

    private static AwbHoleViewModel CustomSimple(string diameter = "6")
    {
        var vm = new AwbHoleViewModel(Workbook, PartName) { CentreCount = 1 };
        vm.Type = HoleStandards.SimpleType;
        vm.DiameterText = diameter;
        vm.Toleranced = true;
        vm.Tolerance = HoleToleranceOption.CustomValue;
        return vm;
    }

    [Fact]
    public void Custom_StartsWithNoCode_AndTheLimitsTypedAsBefore()
    {
        var vm = CustomDowel();

        Assert.Equal("", vm.ToleranceCodeText);
        Assert.False(vm.HasToleranceCode);
        Assert.Null(vm.ToleranceCodeProblem);
        Assert.Equal(HoleRules.CustomUpperMissingText, vm.ToleranceUpperProblem);
    }

    [Fact]
    public void AHoleCode_ShowsItsLimitsAtTheSize_ReadOnly_AndBuildsAsAFit()
    {
        var vm = CustomDowel();
        vm.ToleranceCodeText = "H8";

        // H8 at 5 mm (3 to 6 mm): +0.018 / 0.
        Assert.True(vm.HasToleranceCode);
        Assert.Equal("0.018", vm.ToleranceUpperText);
        Assert.Equal("0", vm.ToleranceLowerText);
        Assert.Null(vm.ToleranceUpperProblem);
        Assert.Null(vm.ToleranceLowerProblem);
        Assert.True(vm.CanOk);
        Assert.Equal("H8", vm.Spec.CustomCode);
        Assert.Null(vm.Spec.CustomUpper);
        Assert.Equal("H8", vm.Geometry!.Tolerance!.IsoCode);
        Assert.Equal("H8", vm.DiagramInput.Tolerance);

        // Read-only: typing in Upper changes nothing.
        vm.ToleranceUpperText = "0.5";
        Assert.Equal("0.018", vm.ToleranceUpperText);
        Assert.Null(vm.Spec.CustomUpper);
    }

    [Fact]
    public void ASizeChange_WorksTheLimitsOutAgain()
    {
        var vm = CustomDowel();
        vm.ToleranceCodeText = "H8";

        vm.Size = "3 mm";

        // H8 at 3 mm (over 0 to 3 mm): +0.014 / 0.
        Assert.Equal("0.014", vm.ToleranceUpperText);
        Assert.Equal("H8", vm.Spec.CustomCode);
    }

    [Fact]
    public void AShaftCode_OnATolerancedSimpleHole_KeepsItsCase()
    {
        var vm = CustomSimple("6");
        vm.ToleranceCodeText = "k6";

        // k6 at 6 mm (3 to 6 mm): +0.009 / +0.001.
        Assert.Equal("0.009", vm.ToleranceUpperText);
        Assert.Equal("0.001", vm.ToleranceLowerText);
        Assert.Equal("k6", vm.Spec.CustomCode);
        Assert.True(vm.Geometry!.Tolerance!.IsShaftCode);
        Assert.Equal(HoleSettings.Starter.NormalFitColour, vm.Geometry.FitColour);
        Assert.True(vm.CanOk);
    }

    [Theory]
    [InlineData("Z9x")]
    [InlineData("K9")]
    public void ACodeThatCannotBeUsed_IsReportedUnderTheBox_AndOkWaits(string code)
    {
        var vm = CustomDowel();
        vm.ToleranceCodeText = code;

        Assert.NotNull(vm.ToleranceCodeProblem);
        Assert.False(vm.CanOk);
        Assert.Equal("", vm.ToleranceUpperText);
        Assert.Equal("", vm.ToleranceLowerText);
        Assert.Null(vm.ToleranceUpperProblem);
        Assert.Equal(code, vm.ToleranceCodeText);
    }

    [Fact]
    public void ClearingTheCode_BringsBackTheTypedLimits()
    {
        var vm = CustomDowel();
        vm.ToleranceUpperText = "0.02";
        vm.ToleranceLowerText = "0";
        vm.ToleranceCodeText = "Z9x";
        Assert.False(vm.CanOk);

        vm.ToleranceCodeText = "";

        Assert.False(vm.HasToleranceCode);
        Assert.Null(vm.ToleranceCodeProblem);
        Assert.Equal("0.02", vm.ToleranceUpperText);
        Assert.Equal(0.02m, vm.Spec.CustomUpper);
        Assert.Null(vm.Spec.CustomCode);
        Assert.True(vm.CanOk);
    }

    [Fact]
    public void TheCode_IsKeptWhenCustomIsLeftAndChosenAgain()
    {
        var vm = CustomDowel();
        vm.ToleranceCodeText = "H8";

        vm.Tolerance = vm.ToleranceOptions[0].Value;
        Assert.False(vm.HasToleranceCode);
        Assert.Null(vm.Spec.CustomCode);

        vm.Tolerance = HoleToleranceOption.CustomValue;
        Assert.Equal("H8", vm.ToleranceCodeText);
        Assert.Equal("H8", vm.Spec.CustomCode);
    }

    [Fact]
    public void Imperial_ShowsTheLimitsInInches()
    {
        var vm = CustomSimple("6.35");
        vm.ToleranceCodeText = "H7";

        vm.Units = HoleUnits.Imperial;

        // 0.25 in is 6.35 mm (6 to 10 mm): H7 is +0.015 mm, 0.000591 in.
        Assert.Equal("0.000591", vm.ToleranceUpperText);
        Assert.Equal("H7", vm.Spec.CustomCode);
    }

    [Fact]
    public void Edit_OpensOnCustomWithTheCodeAsTyped()
    {
        var spec = HoleSpec.ClearanceHole(HoleUnits.Metric, "T5") with
        {
            Toleranced = true,
            CustomTolerance = true,
            CustomCode = "k6",
        };

        var vm = AwbHoleViewModel.ForEdit(Workbook, PartName, spec);
        vm.CentreCount = 1;

        Assert.Equal(HoleToleranceOption.CustomValue, vm.Tolerance);
        Assert.Equal("k6", vm.ToleranceCodeText);
        Assert.True(vm.HasToleranceCode);
        Assert.Equal(spec.Normalised(), vm.Spec);
        Assert.True(vm.CanOk);
    }

    [Fact]
    public void LiveTyping_AppliesTheCodeAfterThePause()
    {
        var vm = CustomDowel();
        var timer = new FakeDebounceTimer();
        vm.UseTypingTimer(timer);

        vm.ToleranceCodeText = "H";
        vm.ToleranceCodeText = "H8";
        Assert.Equal("H8", vm.ToleranceCodeText);
        Assert.Null(vm.Spec.CustomCode);
        Assert.True(vm.HasPendingTyping);

        timer.Advance(Debouncer.TypingPause);

        Assert.Equal("H8", vm.Spec.CustomCode);
        Assert.Equal("0.018", vm.ToleranceUpperText);

        vm.CommitTyping();
        Assert.Equal("H8", vm.ToleranceCodeText);
        Assert.False(vm.HasPendingTyping);
    }
}
