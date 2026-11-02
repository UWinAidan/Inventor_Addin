using InventorAddin.Core.Holes;
using static InventorAddin.Core.Tests.Holes.HoleTestWorkbook;

namespace InventorAddin.Core.Tests.Holes;

/// <summary>Depths typed as Inventor expressions (task 100): carried, stored and read back beside their values.</summary>
public sealed class HoleExpressionsTests
{
    private static readonly HoleWorkbook Workbook = Create();

    private static HoleSpec BlindClearance(string expression, decimal depth) => Clearance() with
    {
        Termination = HoleTermination.Distance,
        Depth = depth,
        DepthExpression = expression,
    };

    private static HoleSpec BlindTapped(string? thread, string? drill) => Tapped() with
    {
        Termination = HoleTermination.Distance,
        ThreadDepth = 6m,
        DrillDepth = 9m,
        ThreadDepthExpression = thread,
        DrillDepthExpression = drill,
    };

    private static HoleBuildResult Build(HoleSpec spec)
    {
        var result = HoleRules.Build(spec, Workbook, 1);
        Assert.True(result.Succeeded, string.Join(" | ", result.Problems));
        return result;
    }

    [Theory]
    [InlineData(HoleUnits.Metric, "mm")]
    [InlineData(HoleUnits.Imperial, "in")]
    public void UnitName_IsTheUnitsLength(HoleUnits units, string expected)
    {
        Assert.Equal(expected, HoleExpressions.UnitName(units));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("   ", null)]
    [InlineData(" d0 + 1 ", "d0 + 1")]
    public void Clean_TrimsAndBlankIsNull(string? text, string? expected)
    {
        Assert.Equal(expected, HoleExpressions.Clean(text));
    }

    [Fact]
    public void Invalid_KeepsAReasonOnlyWhenThereIsOne()
    {
        Assert.Null(HoleExpressionCheck.Invalid(" ").Reason);
        Assert.Equal("No such parameter.", HoleExpressionCheck.Invalid(" No such parameter. ").Reason);
        Assert.Equal(new HoleExpressionCheck(true, 1.2, null), HoleExpressionCheck.Valid(1.2));
    }

    // ---- The spec ----

    [Fact]
    public void Normalised_KeepsEachExpressionWithItsDepth()
    {
        var spec = BlindClearance(" d0 ", 20m).Normalised();
        Assert.Equal("d0", spec.DepthExpression);

        var through = (BlindClearance("d0", 20m) with { Termination = HoleTermination.ThroughAll }).Normalised();
        Assert.Null(through.DepthExpression);

        var tapped = BlindTapped("d0", " d0 + 3 mm ").Normalised();
        Assert.Equal("d0", tapped.ThreadDepthExpression);
        Assert.Equal("d0 + 3 mm", tapped.DrillDepthExpression);
        Assert.Null(tapped.DepthExpression);
    }

    [Fact]
    public void Normalised_DropsAnExpressionWhoseDepthIsMissing_OrBlank()
    {
        var noDepth = (BlindClearance("d0", 20m) with { Depth = null }).Normalised();
        Assert.Null(noDepth.DepthExpression);

        Assert.Null(BlindClearance("  ", 20m).Normalised().DepthExpression);

        var clearanceWithThread = (BlindClearance("d0", 20m) with { ThreadDepthExpression = "d1" }).Normalised();
        Assert.Null(clearanceWithThread.ThreadDepthExpression);
    }

    // ---- The rules ----

    [Fact]
    public void Build_PutsTheDepthsExpressionsInTheGeometry_InTheSpecsUnits()
    {
        var geometry = Build(BlindClearance("1.5*d0+1", 20m)).Geometry!;

        Assert.Equal(2.0, geometry.DepthCm!.Value, 6);
        Assert.Equal("1.5*d0+1", geometry.DepthExpression);
        Assert.Equal(HoleUnits.Metric, geometry.ExpressionUnits);
        Assert.Null(geometry.Thread);
    }

    [Fact]
    public void Build_ATappedHole_TakesTheThreadAndDrillExpressions()
    {
        var geometry = Build(BlindTapped("d0", "d0 + 3 mm")).Geometry!;

        Assert.Equal("d0 + 3 mm", geometry.DepthExpression);
        Assert.Equal("d0", geometry.Thread!.ThreadDepthExpression);
        Assert.Equal(0.6, geometry.Thread.ThreadDepthCm, 6);
    }

    [Fact]
    public void Build_AThroughHole_HasNoExpressions()
    {
        var spec = BlindTapped("d0", "d1") with { Termination = HoleTermination.ThroughAll };

        var geometry = Build(spec).Geometry!;

        Assert.Null(geometry.DepthExpression);
        Assert.Null(geometry.Thread!.ThreadDepthExpression);
    }

    [Fact]
    public void Build_ChecksTheValues_NotTheExpressions()
    {
        // The thread's value is deeper than the drill's, whatever the expressions say.
        var spec = BlindTapped("d0", "d0 + 5 mm") with { ThreadDepth = 12m, DrillDepth = 9m };

        var result = HoleRules.Build(spec, Workbook, 1);

        Assert.Contains(result.Problems, p => p.Field == HoleField.ThreadDepth);
    }

    [Fact]
    public void Build_TheResolvedSpecKeepsTheExpressions_AndTheLastDepthIsTheValue()
    {
        var resolved = Build(BlindClearance("d0", 20m)).Resolved;

        Assert.Equal("d0", resolved.DepthExpression);
        Assert.Equal(20m, resolved.Depth);
    }

    // ---- Stored data ----

    [Fact]
    public void StoredData_RoundTripsTheExpressions()
    {
        foreach (var spec in new[] { BlindClearance("d0", 20m), BlindTapped("d0", "1.5 ul * d0 + 1 mm") })
        {
            var built = Build(spec);
            var stored = new HoleStoredData(built.Resolved, HoleAppliedState.From(built.Geometry!));

            var read = HoleStoredData.Read(stored.ToAttributes());

            Assert.True(read.Succeeded, read.Message);
            Assert.Equal(stored.Spec.Normalised(), read.Data!.Spec);
        }
    }

    [Fact]
    public void StoredData_APlainNumber_StoresNoExpression()
    {
        var built = Build(Clearance() with { Termination = HoleTermination.Distance, Depth = 20m });
        var attributes = new HoleStoredData(built.Resolved, HoleAppliedState.From(built.Geometry!)).ToAttributes();

        Assert.DoesNotContain(attributes, a => a.Name is HoleStoredData.DepthExpressionName
            or HoleStoredData.ThreadDepthExpressionName or HoleStoredData.DrillDepthExpressionName);
    }

    [Fact]
    public void StoredData_WrittenBeforeTask100_ReadsAsPlainNumbers()
    {
        var built = Build(BlindClearance("d0", 20m));
        var attributes = new HoleStoredData(built.Resolved, HoleAppliedState.From(built.Geometry!))
            .ToAttributes()
            .Where(a => a.Name != HoleStoredData.DepthExpressionName)
            .ToList();

        var read = HoleStoredData.Read(attributes);

        Assert.True(read.Succeeded, read.Message);
        Assert.Null(read.Data!.Spec.DepthExpression);
        Assert.Equal(20m, read.Data.Spec.Depth);
    }

    // ---- Read back from Inventor's geometry ----

    private static InventorHoleFacts FactsOf(HoleGeometry geometry) => new(
        geometry.Seat,
        geometry.Thread is not null,
        geometry.Thread?.Designation,
        geometry.Thread?.ThreadType,
        geometry.Thread is { FullDepth: false } thread ? thread.ThreadDepthCm : null,
        geometry.HoleDiameterCm,
        geometry.Termination,
        geometry.DepthCm,
        geometry.CounterboreDiameterCm,
        geometry.CounterboreDepthCm,
        geometry.CountersinkDiameterCm,
        geometry.CountersinkAngleDeg,
        geometry.DrillPoint?.IsFlat ?? false,
        geometry.DrillPoint?.AngleDeg,
        geometry.Tolerance?.IsoCode,
        geometry.Tolerance?.UpperCm,
        geometry.Tolerance?.LowerCm,
        geometry.Flip);

    [Fact]
    public void ReadBack_AnInventorHole_TakesItsDepthExpression()
    {
        var facts = FactsOf(Build(BlindClearance("d0", 20m)).Geometry!) with { DepthExpression = "d0" };

        var spec = HoleReadBack.Read(null, facts, Workbook, HoleUnits.Metric).Spec;

        Assert.Equal(20m, spec.Depth);
        Assert.Equal("d0", spec.DepthExpression);
    }

    [Fact]
    public void ReadBack_AnInventorTappedHole_TakesItsThreadAndDrillExpressions()
    {
        var facts = FactsOf(Build(BlindTapped(null, null)).Geometry!) with
        {
            ThreadDepthExpression = "d0",
            DepthExpression = "d0 + 3 mm",
        };

        var spec = HoleReadBack.Read(null, facts, Workbook, HoleUnits.Metric).Spec;

        Assert.Equal("d0", spec.ThreadDepthExpression);
        Assert.Equal("d0 + 3 mm", spec.DrillDepthExpression);
    }

    [Fact]
    public void ReadBack_ANumberDepth_HasNoExpression()
    {
        var facts = FactsOf(Build(BlindClearance("d0", 20m)).Geometry!);

        var spec = HoleReadBack.Read(null, facts, Workbook, HoleUnits.Metric).Spec;

        Assert.Null(spec.DepthExpression);
    }
}
