using System.Globalization;
using InventorAddin.Core.Holes;

namespace InventorAddin.Core.Tests.Holes;

/// <summary>
/// Task 136: the Full depth checkbox on a Through All tapped hole. Unticked, the thread stops at its thread depth,
/// below the seat (task 116), and the rules, stored data, read-back, re-apply check and diagram follow it. Sizes and
/// values are invented.
/// </summary>
public sealed class HoleFullDepthTests
{
    private const string Dia = HoleDiagram.DiameterSign;

    private static readonly HoleWorkbook Workbook = HoleTestWorkbook.Create() with
    {
        Spotface = [new SpotfaceRow(5, HoleUnits.Metric, "M5", 11m, 0.5m, false)],
    };

    /// <summary>Runs the test in a culture, so number text does not depend on the machine's settings.</summary>
    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo _previous = CultureInfo.CurrentCulture;

        public CultureScope(string name) => CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name);

        public void Dispose() => CultureInfo.CurrentCulture = _previous;
    }

    /// <summary>Reads <c>d0</c> as 20 mm, as a part's parameter would.</summary>
    private sealed class FakeChecker : IHoleExpressionChecker
    {
        public HoleExpressionCheck Check(string expression, HoleUnits units) => expression switch
        {
            "d0" => HoleExpressionCheck.Valid(2.0),
            _ => HoleExpressionCheck.Invalid(),
        };
    }

    /// <summary>An M5 Through All tapped hole (row: thread 7.5, drill 10), Full depth as given.</summary>
    private static HoleSpec ThroughTapped(bool fullDepth = false, decimal? threadDepth = null) => HoleTestWorkbook.Tapped() with
    {
        Termination = HoleTermination.ThroughAll,
        ThreadDepth = threadDepth,
        FullDepth = fullDepth,
    };

    private static HoleSpec Counterbored(HoleSpec spec, decimal depth = 6m) => spec with
    {
        Seat = HoleSeat.Counterbore,
        CboreDiameter = 9.5m,
        CboreDepth = depth,
    };

    private static HoleBuildResult Build(HoleSpec spec) => HoleRules.Build(spec, Workbook, 1);

    private static HoleGeometry Geometry(HoleSpec spec)
    {
        var result = Build(spec);
        Assert.True(result.Succeeded, string.Join(" | ", result.Problems));
        return result.Geometry!;
    }

    /// <summary>What Inventor would report for a hole the add-in made from <paramref name="g"/>.</summary>
    private static InventorHoleFacts FactsOf(HoleGeometry g) => new(
        g.Seat,
        g.Thread is not null,
        g.Thread?.Designation,
        g.Thread?.ThreadType,
        g.Thread is { FullDepth: false, IsTapered: false } thread ? thread.ThreadDepthCm : null,
        g.Thread is null ? g.HoleDiameterCm : null,
        g.Termination,
        g.DepthCm,
        g.CounterboreDiameterCm,
        g.CounterboreDepthCm,
        g.CountersinkDiameterCm,
        g.CountersinkAngleDeg,
        g.DrillPoint?.IsFlat ?? false,
        g.DrillPoint?.AngleDeg,
        g.Tolerance?.IsoCode,
        g.Tolerance?.UpperCm,
        g.Tolerance?.LowerCm,
        g.Flip)
    {
        SpotfaceDiameterCm = g.SpotfaceDiameterCm,
        SpotfaceDepthCm = g.SpotfaceDepthCm,
        ThreadDepthExpression = g.Thread is { FullDepth: false } t ? t.ThreadDepthExpression : null,
        CounterboreDepthExpression = g.CounterboreDepthExpression,
    };

    private static HoleStoredData Stored(HoleSpec spec)
    {
        var build = Build(spec);
        Assert.True(build.Succeeded, string.Join(" | ", build.Problems));
        return new HoleStoredData(build.Resolved, HoleAppliedState.From(build.Geometry!));
    }

    // ---- the spec ----

    [Fact]
    public void ANewSpec_IsFullDepth()
    {
        Assert.True(HoleSpec.ClearanceHole(HoleUnits.Metric, "T5").FullDepth);
        Assert.True(HoleTestWorkbook.Tapped().FullDepth);
    }

    [Theory]
    [InlineData(HoleKind.Tapped, HoleTermination.ThroughAll, false)]
    [InlineData(HoleKind.Tapped, HoleTermination.Distance, true)]
    [InlineData(HoleKind.Tapped, HoleTermination.ToFace, true)]
    [InlineData(HoleKind.TaperTapped, HoleTermination.ThroughAll, true)]
    [InlineData(HoleKind.Clearance, HoleTermination.ThroughAll, true)]
    public void Normalised_KeepsFullDepthUnticked_OnlyOnAThroughAllTappedHole(
        HoleKind kind, HoleTermination termination, bool expected)
    {
        var spec = ThroughTapped() with { Kind = kind, Termination = termination };

        Assert.Equal(expected, spec.Normalised().FullDepth);
    }

    [Fact]
    public void HasThreadDepth_ForABlindTappedHole_AndAThroughAllOneNotFullDepth()
    {
        Assert.True(HoleTestWorkbook.Tapped().HasThreadDepth);
        Assert.True(ThroughTapped(fullDepth: false).HasThreadDepth);
        Assert.False(ThroughTapped(fullDepth: true).HasThreadDepth);
        Assert.False((ThroughTapped(fullDepth: false) with { Kind = HoleKind.TaperTapped }).HasThreadDepth);
    }

    // ---- the rules ----

    [Fact]
    public void Ticked_ThreadsTheFullDepth_AsBefore()
    {
        var geometry = Geometry(ThroughTapped(fullDepth: true) with { ThreadDepthExpression = "d0", ThreadDepth = 20m });

        Assert.True(geometry.Thread!.FullDepth);
        Assert.Null(geometry.Thread.ThreadDepthExpression);
        Assert.Null(geometry.DepthCm);
    }

    [Fact]
    public void Unticked_StopsTheThreadAtTheRowsDefault()
    {
        var build = Build(ThroughTapped());

        Assert.True(build.Succeeded, string.Join(" | ", build.Problems));
        Assert.False(build.Geometry!.Thread!.FullDepth);
        Assert.Equal(0.75, build.Geometry.Thread.ThreadDepthCm, 9);
        Assert.Null(build.Geometry.DepthCm);
        Assert.Equal(7.5m, build.Resolved.ThreadDepth);
        Assert.False(build.Resolved.FullDepth);
    }

    [Fact]
    public void Unticked_TakesATypedDepthAndItsExpression()
    {
        var geometry = Geometry(ThroughTapped(threadDepth: 20m) with { ThreadDepthExpression = " d0 " });

        Assert.Equal(2.0, geometry.Thread!.ThreadDepthCm, 9);
        Assert.Equal("d0", geometry.Thread.ThreadDepthExpression);
    }

    [Fact]
    public void Unticked_UnderACounterbore_IsMeasuredFromTheSeatBottom()
    {
        var geometry = Geometry(Counterbored(ThroughTapped(threadDepth: 9m)));

        // 9 below a 6 mm counterbore: Inventor measures 15 from the start face (task 116).
        Assert.Equal(1.5, geometry.Thread!.ThreadDepthCm, 9);
        Assert.Null(geometry.Thread.ThreadDepthExpression);
    }

    [Fact]
    public void Unticked_UnderACounterbore_KeepsAnExpressionParametric()
    {
        var geometry = Geometry(Counterbored(ThroughTapped(threadDepth: 20m) with { ThreadDepthExpression = "d0" }));

        Assert.Equal(2.6, geometry.Thread!.ThreadDepthCm, 9);
        Assert.Equal("(d0) + (6 mm)", geometry.Thread.ThreadDepthExpression);
    }

    [Fact]
    public void Unticked_UnderASpotface_IsWrittenAsTyped()
    {
        var geometry = Geometry(ThroughTapped(threadDepth: 9m) with
        {
            Seat = HoleSeat.Spotface,
            SpotfaceDiameter = 11m,
            SpotfaceDepth = 1m,
        });

        Assert.Equal(0.9, geometry.Thread!.ThreadDepthCm, 9);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Unticked_AThreadDepthNotAboveZero_IsAProblem(int depth)
    {
        var build = Build(ThroughTapped(threadDepth: depth));

        Assert.False(build.Succeeded);
        var problem = Assert.Single(build.Problems);
        Assert.Equal(HoleField.ThreadDepth, problem.Field);
    }

    [Fact]
    public void Unticked_AThreadDepthDeeperThanTheRowsDrill_IsNotChecked()
    {
        // Through All has no drill depth, and Inventor reports a thread longer than the part (task 136).
        var geometry = Geometry(ThroughTapped(threadDepth: 200m));

        Assert.Equal(20.0, geometry.Thread!.ThreadDepthCm, 9);
    }

    [Fact]
    public void Ticked_AThreadDepthOfZero_IsNotAProblem()
    {
        Geometry(ThroughTapped(fullDepth: true, threadDepth: 0m));
    }

    // ---- stored data ----

    [Fact]
    public void StoredData_Unticked_RoundTrips()
    {
        var data = Stored(Counterbored(ThroughTapped(threadDepth: 20m) with { ThreadDepthExpression = "d0" }));

        var attributes = data.ToAttributes();
        Assert.Contains(attributes, a => a.Name == HoleStoredData.FullDepthName && Equals(a.Value, 0));
        var read = HoleStoredData.Read(attributes);

        Assert.True(read.Succeeded, read.Message);
        Assert.Equal(HoleStoredData.CurrentVersion, read.Data!.Version);
        Assert.False(read.Data.Spec.FullDepth);
        Assert.Equal(20m, read.Data.Spec.ThreadDepth);
        Assert.Equal("d0", read.Data.Spec.ThreadDepthExpression);
        Assert.Equal(data.Spec.Normalised(), read.Data.Spec);
    }

    [Fact]
    public void StoredData_Ticked_StoresOne_AndABlindHoleStoresNone()
    {
        var ticked = Stored(ThroughTapped(fullDepth: true)).ToAttributes();
        Assert.Contains(ticked, a => a.Name == HoleStoredData.FullDepthName && Equals(a.Value, 1));
        Assert.True(HoleStoredData.Read(ticked).Data!.Spec.FullDepth);

        var blind = Stored(HoleTestWorkbook.Tapped() with { DrillPoint = "118 deg" }).ToAttributes();
        Assert.DoesNotContain(blind, a => a.Name == HoleStoredData.FullDepthName);
    }

    [Fact]
    public void StoredData_WithoutFullDepth_ReadsTicked()
    {
        // Every Through All tapped hole stored before task 136 was threaded its full depth.
        var attributes = Stored(ThroughTapped(threadDepth: 9m)).ToAttributes()
            .Where(a => a.Name != HoleStoredData.FullDepthName)
            .ToList();

        var read = HoleStoredData.Read(attributes);

        Assert.True(read.Succeeded, read.Message);
        Assert.True(read.Data!.Spec.FullDepth);
        Assert.Equal(6, HoleStoredData.CurrentVersion);
    }

    [Fact]
    public void StoredData_AFullDepthThatIsNotAFlag_CannotBeRead()
    {
        var attributes = Stored(ThroughTapped()).ToAttributes()
            .Select(a => a.Name == HoleStoredData.FullDepthName ? a with { Value = 2 } : a)
            .ToList();

        var read = HoleStoredData.Read(attributes);

        Assert.False(read.Succeeded);
        Assert.Contains(HoleStoredData.FullDepthName, read.Message);
    }

    [Fact]
    public void StoredData_OlderVersion_ReadsTicked_AndIsNotConverted()
    {
        var attributes = Stored(Counterbored(ThroughTapped(fullDepth: true))).ToAttributes()
            .Where(a => a.Name != HoleStoredData.FullDepthName)
            .Select(a => a.Name == HoleStoredData.VersionName ? a with { Value = 4 } : a)
            .ToList();

        var read = HoleStoredData.Read(attributes);

        Assert.True(read.Succeeded, read.Message);
        Assert.Equal(4, read.Data!.Version);
        Assert.True(read.Data.Spec.FullDepth);
    }

    // ---- reading a hole back ----

    [Fact]
    public void ReadBack_AnAwbHole_OpensUnticked_WithItsDepthBelowTheSeat()
    {
        var spec = Counterbored(ThroughTapped(threadDepth: 9m));
        var data = Stored(spec);
        var facts = FactsOf(Geometry(spec));
        var stored = HoleStoredData.Read(data.ToAttributes());

        var result = HoleReadBack.Read(stored, facts, Workbook, HoleUnits.Metric);

        Assert.Equal(HoleSource.Awb, result.Source);
        Assert.False(result.Spec.FullDepth);
        Assert.Equal(9m, result.Spec.ThreadDepth);
        Assert.False(HoleReapplyRules.Check(result.Stored!, facts, coloured: false).IsOffered);
    }

    [Fact]
    public void ReadBack_AnInventorHoleNotFullDepth_OpensUnticked_WithItsDepthBelowTheSeat()
    {
        // Made in Inventor's Hole: M5 Through All, a 6 mm counterbore, thread depth 15 from the start face.
        var facts = FactsOf(Geometry(Counterbored(ThroughTapped(threadDepth: 9m)))) with { ThreadDepthCm = 1.5 };

        var result = HoleReadBack.Read(null, facts, Workbook, HoleUnits.Metric);

        Assert.Equal(HoleSource.Inventor, result.Source);
        Assert.Equal(HoleKind.Tapped, result.Spec.Kind);
        Assert.Equal(HoleTermination.ThroughAll, result.Spec.Termination);
        Assert.False(result.Spec.FullDepth);
        Assert.Equal(9m, result.Spec.ThreadDepth);
    }

    [Fact]
    public void ReadBack_AnInventorHoleNotFullDepth_ShowsTheUsersExpression()
    {
        var facts = FactsOf(Geometry(Counterbored(ThroughTapped(threadDepth: 20m) with { ThreadDepthExpression = "d0" })));

        var result = HoleReadBack.Read(null, facts, Workbook, HoleUnits.Metric);

        Assert.False(result.Spec.FullDepth);
        Assert.Equal("d0", result.Spec.ThreadDepthExpression);
        Assert.Equal(20m, result.Spec.ThreadDepth);
    }

    [Fact]
    public void ReadBack_AnInventorHoleFullDepth_OpensTicked_WithTheRowsDepth()
    {
        var facts = FactsOf(Geometry(ThroughTapped(fullDepth: true)));

        var result = HoleReadBack.Read(null, facts, Workbook, HoleUnits.Metric);

        Assert.True(result.Spec.FullDepth);
        Assert.Equal(7.5m, result.Spec.ThreadDepth);
        Assert.Null(result.Spec.ThreadDepthExpression);
    }

    // ---- the re-apply check ----

    [Fact]
    public void Reapply_UntickedMadeFullDepthInInventor_IsOffered()
    {
        var spec = ThroughTapped(threadDepth: 9m);
        var facts = FactsOf(Geometry(spec)) with { ThreadDepthCm = null };

        var offer = HoleReapplyRules.Check(Stored(spec), facts, coloured: false);

        Assert.Equal("The thread runs the full depth; AWB Hole set a thread depth.", Assert.Single(offer.Differences));
    }

    [Fact]
    public void Reapply_TickedGivenAThreadDepthInInventor_IsOffered()
    {
        using var culture = new CultureScope("en-US");
        var spec = ThroughTapped(fullDepth: true);
        var facts = FactsOf(Geometry(spec)) with { ThreadDepthCm = 1.2 };

        var offer = HoleReapplyRules.Check(Stored(spec), facts, coloured: false);

        Assert.Equal(
            "The thread stops at 12 mm; AWB Hole threaded the full depth.", Assert.Single(offer.Differences));
    }

    [Fact]
    public void Reapply_UntickedWithAnotherThreadDepth_IsOffered_AndTheSameIsNot()
    {
        using var culture = new CultureScope("en-US");
        var spec = Counterbored(ThroughTapped(threadDepth: 9m));
        var facts = FactsOf(Geometry(spec));
        var stored = Stored(spec);

        Assert.False(HoleReapplyRules.Check(stored, facts, coloured: false).IsOffered);

        var offer = HoleReapplyRules.Check(stored, facts with { ThreadDepthCm = 1.7 }, coloured: false);
        Assert.Equal("The thread depth is 11 mm; AWB Hole set 9 mm.", Assert.Single(offer.Differences));
    }

    [Fact]
    public void Reapply_UntickedWithAnExpression_IsComparedByItsValue()
    {
        var spec = ThroughTapped(threadDepth: 20m) with { ThreadDepthExpression = "d0" };
        var facts = FactsOf(Geometry(spec));

        Assert.False(HoleReapplyRules.Check(Stored(spec), facts, coloured: false, new FakeChecker()).IsOffered);
    }

    // ---- the diagram ----

    private static HoleDiagramInput ThroughTappedDiagram(bool fullDepth) => new()
    {
        Units = HoleUnits.Metric,
        Kind = HoleKind.Tapped,
        Termination = HoleTermination.ThroughAll,
        HoleDiameter = 5m,
        ThreadName = "M6x1",
        ThreadDiameter = 6m,
        ThreadDepth = 9m,
        FullDepth = fullDepth,
    };

    [Fact]
    public void Diagram_Unticked_DimensionsTheThreadDepth_AndClosesTheThread()
    {
        using var culture = new CultureScope("en-US");

        var diagram = HoleDiagram.Draw(ThroughTappedDiagram(fullDepth: false));

        Assert.Equal(
            new[] { Dia + "5", "M6x1", "9" }.OrderBy(t => t),
            diagram.Labels.Select(l => l.Text).OrderBy(t => t));
        // Two thread walls and the two short lines across where the thread stops, as on a blind hole.
        Assert.Equal(4, diagram.Lines.Count(l => l.Ink == DiagramInk.Thread));
        // Still a through hole: the part is cut in two.
        Assert.Equal(2, diagram.Material.Count);
    }

    [Fact]
    public void Diagram_Unticked_TheThreadStopsAboveTheBottomOfThePart()
    {
        var diagram = HoleDiagram.Draw(ThroughTappedDiagram(fullDepth: false));

        var threadBottom = diagram.Lines.Where(l => l.Ink == DiagramInk.Thread).SelectMany(l => l.Points).Max(p => p.Y);
        var partBottom = diagram.Material.SelectMany(p => p).Max(p => p.Y);
        Assert.True(threadBottom < partBottom - 1, $"thread ends at {threadBottom}, the part at {partBottom}");
    }

    [Fact]
    public void Diagram_Unticked_UnderACounterbore_DimensionsFromTheSeatBottom()
    {
        using var culture = new CultureScope("en-US");
        var input = ThroughTappedDiagram(fullDepth: false) with
        {
            Seat = HoleSeat.Counterbore,
            CounterboreDiameter = 11m,
            CounterboreDepth = 6m,
        };

        var diagram = HoleDiagram.Draw(input);

        // The thread depth's dimension runs from the counterbore floor to where the thread stops.
        var thread = diagram.Lines.Where(l => l.Ink == DiagramInk.Thread).SelectMany(l => l.Points).ToList();
        var label = Assert.Single(diagram.Labels, l => l.Text == "9");
        var floor = thread.Min(p => p.Y);
        var end = thread.Max(p => p.Y);
        Assert.InRange(label.At.Y, floor, end);
        var cboreLabel = Assert.Single(diagram.Labels, l => l.Text == "6");
        Assert.True(cboreLabel.At.Y < floor, "the counterbore depth is above its floor");
    }

    [Fact]
    public void Diagram_Ticked_ThreadsTheFullDepth_WithNoThreadDimension()
    {
        using var culture = new CultureScope("en-US");

        var diagram = HoleDiagram.Draw(ThroughTappedDiagram(fullDepth: true));

        Assert.DoesNotContain(diagram.Labels, l => l.Text == "9");
        Assert.Equal(2, diagram.Lines.Count(l => l.Ink == DiagramInk.Thread));
    }

    [Fact]
    public void Diagram_Summary_SaysWhereTheThreadStops()
    {
        using var culture = new CultureScope("en-US");

        Assert.Equal("M6x1, through all, thread 9", HoleDiagram.Summary(ThroughTappedDiagram(fullDepth: false)));
        Assert.Equal("M6x1, through all", HoleDiagram.Summary(ThroughTappedDiagram(fullDepth: true)));
    }
}
