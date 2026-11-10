using System.Globalization;
using InventorAddin.Core.Holes;
using InventorAddin.Core.ViewModels;

namespace InventorAddin.Core.Tests.ViewModels;

/// <summary>
/// Task 136: the window's Full depth checkbox on a Through All tapped hole, and the Thread depth box it shows when
/// unticked (live typing, task 130; typed values kept, task 124; expressions, task 100).
/// </summary>
public sealed class AwbHoleFullDepthViewModelTests : IDisposable
{
    private const string PartName = "Test bracket.ipt";

    private static readonly HoleWorkbook Workbook = AwbHoleTestWorkbook.Create();

    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;

    public AwbHoleFullDepthViewModelTests() => CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");

    public void Dispose() => CultureInfo.CurrentCulture = _culture;

    /// <summary><c>d0</c> is 20 mm.</summary>
    private sealed class FakeChecker : IHoleExpressionChecker
    {
        public HoleExpressionCheck Check(string expression, HoleUnits units) => expression switch
        {
            "d0" => HoleExpressionCheck.Valid(2.0),
            _ => HoleExpressionCheck.Invalid(),
        };
    }

    /// <summary>A new M5 tapped hole, Through All (the new hole's termination); the M5 row has thread 7.5.</summary>
    private static AwbHoleViewModel ThroughTapped(string size = "M5")
    {
        var vm = new AwbHoleViewModel(Workbook, PartName, expressionChecker: new FakeChecker());
        vm.CentreCount = 1;
        vm.Type = HoleStandards.TappedType;
        vm.Size = size;
        return vm;
    }

    [Fact]
    public void ANewThroughAllTappedHole_ShowsFullDepthTicked_AndNoThreadDepth()
    {
        var vm = ThroughTapped();

        Assert.Equal(HoleTermination.ThroughAll, vm.Termination);
        Assert.True(vm.ShowsFullDepth);
        Assert.True(vm.FullDepth);
        Assert.False(vm.ShowsThreadDepth);
        Assert.True(vm.Spec.FullDepth);
        Assert.True(vm.Geometry!.Thread!.FullDepth);
        Assert.True(vm.DiagramInput.FullDepth);
    }

    [Fact]
    public void Unticked_ShowsTheThreadDepthBox_AtTheRowsDefault()
    {
        var vm = ThroughTapped();
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.FullDepth = false;

        Assert.False(vm.FullDepth);
        Assert.True(vm.ShowsThreadDepth);
        Assert.False(vm.ShowsDrillDepth);
        Assert.Equal("7.5", vm.ThreadDepthText);
        Assert.False(vm.Spec.FullDepth);
        Assert.Equal(7.5m, vm.Spec.ThreadDepth);
        Assert.False(vm.Geometry!.Thread!.FullDepth);
        Assert.Equal(0.75, vm.Geometry.Thread.ThreadDepthCm, 9);
        Assert.False(vm.DiagramInput.FullDepth);
        Assert.Equal(7.5m, vm.DiagramInput.ThreadDepth);
        Assert.Contains(nameof(AwbHoleViewModel.FullDepth), changed);
        Assert.Contains(nameof(AwbHoleViewModel.ShowsThreadDepth), changed);
    }

    [Fact]
    public void Unticking_AsksForAPreview()
    {
        var vm = ThroughTapped();
        var previews = 0;
        vm.PreviewNeeded += (_, _) => previews++;

        vm.FullDepth = false;

        Assert.True(previews > 0);
    }

    [Theory]
    [InlineData(HoleStandards.TappedType, HoleTermination.Distance)]
    [InlineData(HoleStandards.ClearanceType, HoleTermination.ThroughAll)]
    [InlineData(HoleStandards.TaperTappedType, HoleTermination.ThroughAll)]
    [InlineData(HoleStandards.SimpleType, HoleTermination.ThroughAll)]
    public void FullDepth_ShowsOnlyOnAThroughAllTappedHole(string type, HoleTermination termination)
    {
        var vm = new AwbHoleViewModel(Workbook, PartName);
        vm.Type = type;
        vm.Termination = termination;

        Assert.False(vm.ShowsFullDepth);
    }

    [Fact]
    public void FullDepth_IsIgnoredWhereItDoesNotShow()
    {
        var vm = new AwbHoleViewModel(Workbook, PartName);

        vm.FullDepth = false;

        Assert.True(vm.FullDepth);
        Assert.True(vm.Spec.FullDepth);
    }

    [Fact]
    public void ABlindTappedHole_KeepsItsThreadAndDrillDepths_WhateverFullDepthWas()
    {
        var vm = ThroughTapped();
        vm.FullDepth = false;

        vm.Termination = HoleTermination.Distance;

        Assert.False(vm.ShowsFullDepth);
        Assert.True(vm.ShowsThreadDepth);
        Assert.True(vm.ShowsDrillDepth);
        Assert.True(vm.Spec.FullDepth);

        // Back to Through All: the choice was kept while the window is open.
        vm.Termination = HoleTermination.ThroughAll;
        Assert.False(vm.FullDepth);
        Assert.True(vm.ShowsThreadDepth);
    }

    [Fact]
    public void ATypedThreadDepth_IsKept_WhenTheSizeChanges_AndAnUntypedOneFollowsTheRow()
    {
        var typed = ThroughTapped();
        typed.FullDepth = false;
        typed.ThreadDepthText = "12";
        typed.Size = "M3";
        Assert.Equal(12m, typed.ThreadDepth);

        var untyped = ThroughTapped();
        untyped.FullDepth = false;
        untyped.Size = "M3";
        Assert.Equal(4.5m, untyped.ThreadDepth);
    }

    [Fact]
    public void AnExpression_GoesToTheGeometry()
    {
        var vm = ThroughTapped();
        vm.FullDepth = false;

        vm.ThreadDepthText = "d0";

        Assert.Equal(20m, vm.ThreadDepth);
        Assert.Equal("d0", vm.ThreadDepthText);
        Assert.Equal("= 20 mm", vm.ThreadDepthValueText);
        Assert.Equal("d0", vm.Spec.ThreadDepthExpression);
        Assert.Equal("d0", vm.Geometry!.Thread!.ThreadDepthExpression);
        Assert.Null(vm.ThreadDepthProblem);
    }

    [Fact]
    public void AThreadDepthOfZero_IsAProblemOnTheBox()
    {
        var vm = ThroughTapped();
        vm.FullDepth = false;

        vm.ThreadDepthText = "0";

        Assert.NotNull(vm.ThreadDepthProblem);
        Assert.False(vm.CanOk);

        vm.FullDepth = true;
        Assert.Null(vm.ThreadDepthProblem);
    }

    [Fact]
    public void Edit_AnUntickedHole_OpensWithItsThreadDepth_Typed()
    {
        var spec = HoleSpec.ClearanceHole(HoleUnits.Metric, "M5") with
        {
            Kind = HoleKind.Tapped,
            ThreadDepth = 12m,
            FullDepth = false,
        };

        var vm = AwbHoleViewModel.ForEdit(Workbook, PartName, spec);

        Assert.True(vm.ShowsFullDepth);
        Assert.False(vm.FullDepth);
        Assert.True(vm.ShowsThreadDepth);
        Assert.Equal("12", vm.ThreadDepthText);

        vm.Size = "M3";
        Assert.Equal(12m, vm.ThreadDepth);
    }

    [Fact]
    public void Edit_ATickedHole_OpensTicked_AndItsUnusedThreadDepthFollowsTheRow()
    {
        var spec = HoleSpec.ClearanceHole(HoleUnits.Metric, "M5") with { Kind = HoleKind.Tapped, ThreadDepth = 7.5m };

        var vm = AwbHoleViewModel.ForEdit(Workbook, PartName, spec);

        Assert.True(vm.FullDepth);
        Assert.False(vm.ShowsThreadDepth);

        vm.Size = "M3";
        vm.FullDepth = false;
        Assert.Equal(4.5m, vm.ThreadDepth);
    }
}
