using System.Globalization;
using InventorAddin.Core.Holes;
using InventorAddin.Core.Settings;
using InventorAddin.Core.ViewModels;

namespace InventorAddin.Core.Tests.ViewModels;

/// <summary>
/// A new AWB Hole starts on the last hole kept with OK, for its units (task 173; Aidan, 2026-11-18; spec 09, "A new
/// hole starts on the last hole"). Sizes and names are invented.
/// </summary>
public sealed class AwbHoleLastHoleViewModelTests : IDisposable
{
    private const string PartName = "Test bracket.ipt";

    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;

    public AwbHoleLastHoleViewModelTests() => CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");

    public void Dispose() => CultureInfo.CurrentCulture = _culture;

    private static AwbHoleViewModel Make(HoleWorkbook? workbook = null, AddinSettings? settings = null) =>
        new(workbook ?? AwbHoleTestWorkbook.Create(), PartName, lastHole: HoleLastHole.From(settings ?? new AddinSettings()))
        {
            CentreCount = 1,
        };

    private static AwbHoleViewModel Fresh() => new(AwbHoleTestWorkbook.Create(), PartName) { CentreCount = 1 };

    private static AddinSettings Kept(AwbHoleViewModel made, AddinSettings? settings = null) =>
        HoleLastHole.Remember(settings ?? new AddinSettings(), made.Spec)!;

    [Fact]
    public void WithNoLastHole_StartsOnTheWorkbookDefaults_AsBefore()
    {
        var vm = Make();

        Assert.Equal(Fresh().Spec, vm.Spec);
        Assert.Equal(HoleStandards.ClearanceType, vm.Type);
        Assert.Equal(HoleTermination.ThroughAll, vm.Termination);
    }

    [Fact]
    public void ACounterboredBlindTappedHole_IsStartedOnAgain()
    {
        var made = Fresh();
        made.Type = HoleStandards.TappedType;
        made.Size = "M5";
        made.Seat = HoleSeat.Counterbore;
        made.Fastener = "TLOW";
        made.Termination = HoleTermination.Distance;
        made.DrillPoint = "90 deg";
        var settings = Kept(made);

        var vm = Make(settings: settings);

        Assert.Equal(HoleStandards.TappedType, vm.Type);
        Assert.Equal(HoleStandards.TappedStandard, vm.Standard);
        Assert.Equal("M5", vm.Size);
        Assert.Equal(HoleSeat.Counterbore, vm.Seat);
        Assert.Equal("TLOW", vm.Fastener);
        Assert.Equal(HoleTermination.Distance, vm.Termination);
        Assert.Equal("90 deg", vm.DrillPoint);
    }

    [Fact]
    public void ADowelWithAPressFit_AndATolerance_IsStartedOnAgain()
    {
        var made = Fresh();
        made.Standard = HoleStandards.DowelStandard;
        made.Size = "5 mm";
        made.Fit = "Press";
        made.Tolerance = "P press";
        var settings = Kept(made);

        var vm = Make(settings: settings);

        Assert.Equal(HoleKind.Dowel, vm.Kind);
        Assert.Equal("5 mm", vm.Size);
        Assert.Equal("Press", vm.Fit);
        Assert.Equal("P press", vm.Tolerance);
    }

    [Fact]
    public void AClearanceFit_AndATerminationOfThroughAll_AreStartedOnAgain()
    {
        var made = Fresh();
        made.Size = "T5";
        made.Fit = "Loose";
        var settings = Kept(made);

        var vm = Make(settings: settings);

        Assert.Equal("T5", vm.Size);
        Assert.Equal("Loose", vm.Fit);
        Assert.Equal(HoleTermination.ThroughAll, vm.Termination);
    }

    [Fact]
    public void ATolerancedClearanceHole_WithACustomCode_IsStartedOnAgain()
    {
        var made = Fresh();
        made.Toleranced = true;
        made.Tolerance = HoleToleranceOption.CustomValue;
        made.ToleranceCode = "H8";
        var settings = Kept(made);

        var vm = Make(settings: settings);

        Assert.True(vm.Toleranced);
        Assert.Equal(HoleToleranceOption.CustomValue, vm.Tolerance);
        Assert.Equal("H8", vm.ToleranceCode);
        Assert.Equal("H8", vm.Spec.CustomCode);
        Assert.True(vm.CanOk);
    }

    [Fact]
    public void ACustomToleranceWithLimits_IsStartedOnAgain()
    {
        var made = Fresh();
        made.Toleranced = true;
        made.Tolerance = HoleToleranceOption.CustomValue;
        made.ToleranceUpperText = "0.05";
        made.ToleranceLowerText = "-0.02";
        var settings = Kept(made);

        var vm = Make(settings: settings);

        Assert.True(vm.Toleranced);
        Assert.Equal(0.05m, vm.Spec.CustomUpper);
        Assert.Equal(-0.02m, vm.Spec.CustomLower);
    }

    [Fact]
    public void ASimpleHole_IsStartedOnAgain()
    {
        var made = Fresh();
        made.Type = HoleStandards.SimpleType;
        made.Termination = HoleTermination.Distance;
        made.DrillPoint = "Flat";
        var settings = Kept(made);

        var vm = Make(settings: settings);

        Assert.Equal(HoleKind.Simple, vm.Kind);
        Assert.Equal(HoleTermination.Distance, vm.Termination);
        Assert.Equal("Flat", vm.DrillPoint);
    }

    [Fact]
    public void ImperialUnits_StartOnTheImperialHole_AndMetricOnTheMetricHole()
    {
        var metric = Fresh();
        metric.Type = HoleStandards.TappedType;
        metric.Size = "M5";
        var imperial = Fresh();
        imperial.Units = HoleUnits.Imperial;
        imperial.Type = HoleStandards.TappedType;
        imperial.Size = "#5-40 UNC";
        var settings = Kept(imperial, Kept(metric));

        var onMetric = Make(settings: settings);
        var onImperial = Make(AwbHoleTestWorkbook.Create(HoleUnits.Imperial), settings);

        Assert.Equal(HoleUnits.Metric, onMetric.Units);
        Assert.Equal("M5", onMetric.Size);
        Assert.Equal(HoleUnits.Imperial, onImperial.Units);
        Assert.Equal("#5-40 UNC", onImperial.Size);
    }

    [Fact]
    public void AnImperialLastHole_DoesNotChangeAMetricStart()
    {
        var imperial = Fresh();
        imperial.Units = HoleUnits.Imperial;
        imperial.Size = "#5";
        var settings = Kept(imperial);

        Assert.Equal(Fresh().Spec, Make(settings: settings).Spec);
    }

    [Fact]
    public void ASizeTheWorkbookNoLongerHas_FallsBackToTheDefault_AndTheRestStillApply()
    {
        var made = Fresh();
        made.Type = HoleStandards.TappedType;
        made.Size = "M5";
        made.Termination = HoleTermination.Distance;
        var settings = Kept(made);
        settings = settings with
        {
            LastHoleMm = settings.LastHoleMm! with { Size = "M99", DrillPoint = "No such point" },
        };

        var vm = Make(settings: settings);

        Assert.Equal(HoleStandards.TappedType, vm.Type);
        Assert.Equal("M3", vm.Size);
        Assert.Equal(HoleTermination.Distance, vm.Termination);
        Assert.Equal("118 deg", vm.DrillPoint);
    }

    [Fact]
    public void AFastenerNotOffered_FallsBackToTheDefault_AndTheSeatStillApplies()
    {
        var made = Fresh();
        made.Size = "T5";
        made.Seat = HoleSeat.Counterbore;
        made.Fastener = "TLOW";
        var settings = Kept(made);
        settings = settings with { LastHoleMm = settings.LastHoleMm! with { Fastener = "GONE" } };

        var vm = Make(settings: settings);

        Assert.Equal(HoleSeat.Counterbore, vm.Seat);
        Assert.Equal("TCAP", vm.Fastener);
    }

    [Fact]
    public void AKindTheWorkbookHasNoRowsFor_StartsOnTheDefaults()
    {
        var made = Fresh();
        made.Standard = HoleStandards.DowelStandard;
        var settings = Kept(made);
        var noDowels = AwbHoleTestWorkbook.Create() with { Dowel = [] };

        var vm = Make(noDowels, settings);

        Assert.Equal(HoleKind.Clearance, vm.Kind);
        Assert.NotEmpty(vm.Size ?? "");
    }

    [Fact]
    public void TheValuesItSets_AreStartingValues_SoASizeChangeStillFollowsTheTable()
    {
        var made = Fresh();
        made.Type = HoleStandards.TappedType;
        made.Size = "M5";
        var settings = Kept(made);
        var vm = Make(settings: settings);
        var thread = vm.ThreadDepthText;

        vm.Size = "M3";

        Assert.NotEqual(thread, vm.ThreadDepthText);
        Assert.Equal("7.5", thread);
        Assert.Equal("4.5", vm.ThreadDepthText);
    }

    [Fact]
    public void EditMode_OpensOnTheHole_NotTheLastHole()
    {
        var made = Fresh();
        made.Type = HoleStandards.TappedType;
        made.Size = "M5";
        var settings = Kept(made);
        var edited = HoleSpec.ClearanceHole(HoleUnits.Metric, "T3");

        var vm = AwbHoleViewModel.ForEdit(AwbHoleTestWorkbook.Create(), PartName, edited);

        Assert.Equal(edited, vm.Spec);
        Assert.NotNull(settings.LastHoleMm);
    }

    [Fact]
    public void TheWindow_DoesNotChangeTheRememberedHole_UntilRememberIsCalled()
    {
        var made = Fresh();
        made.Size = "T5";
        var settings = Kept(made);
        var before = settings.LastHoleMm;
        var vm = Make(settings: settings);

        vm.Size = "T3";
        vm.Termination = HoleTermination.Distance;
        vm.CancelCommand.Execute(null);

        Assert.Equal(before, settings.LastHoleMm);
        Assert.Equal("T5", Make(settings: settings).Size);
    }
}
