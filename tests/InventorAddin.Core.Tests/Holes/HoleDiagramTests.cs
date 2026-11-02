using System.Globalization;
using InventorAddin.Core.Holes;

namespace InventorAddin.Core.Tests.Holes;

/// <summary>The AWB Hole window's diagram (task 104): what each kind of hole shows, and that it fits its box.</summary>
public sealed class HoleDiagramTests
{
    private const string Dia = HoleDiagram.DiameterSign;
    private const string Deg = HoleDiagram.DegreeSign;

    /// <summary>Runs the test in a culture, so number text does not depend on the machine's settings.</summary>
    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo _previous = CultureInfo.CurrentCulture;

        public CultureScope(string name) => CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name);

        public void Dispose() => CultureInfo.CurrentCulture = _previous;
    }

    // The mockup's holes, with invented values.

    private static HoleDiagramInput BlindDowelCounterbore => new()
    {
        Units = HoleUnits.Metric,
        Kind = HoleKind.Dowel,
        Seat = HoleSeat.Counterbore,
        Termination = HoleTermination.Distance,
        HoleDiameter = 6m,
        Tolerance = "H7",
        Depth = 31m,
        DepthIsExpression = true,
        PointAngleDeg = 118m,
        CounterboreDiameter = 11m,
        CounterboreDepth = 6.5m,
        CounterboreDepthIsExpression = true,
    };

    private static HoleDiagramInput BlindTapped => new()
    {
        Units = HoleUnits.Metric,
        Kind = HoleKind.Tapped,
        Termination = HoleTermination.Distance,
        HoleDiameter = 5m,
        Depth = 12m,
        PointAngleDeg = 118m,
        ThreadName = "M6x1",
        ThreadDiameter = 6m,
        ThreadDepth = 9m,
    };

    private static HoleDiagramInput ThroughClearanceCounterbore => new()
    {
        Units = HoleUnits.Metric,
        Kind = HoleKind.Clearance,
        Seat = HoleSeat.Counterbore,
        Termination = HoleTermination.ThroughAll,
        HoleDiameter = 6.6m,
        CounterboreDiameter = 11m,
        CounterboreDepth = 6.5m,
    };

    private static HoleDiagramInput ThroughDowelCountersink => new()
    {
        Units = HoleUnits.Metric,
        Kind = HoleKind.Dowel,
        Seat = HoleSeat.Countersink,
        Termination = HoleTermination.ThroughAll,
        HoleDiameter = 6m,
        Tolerance = "G7",
        CountersinkDiameter = 9m,
        CountersinkAngleDeg = 90m,
    };

    private static List<string> Texts(HoleDiagram diagram) => diagram.Labels.Select(l => l.Text).ToList();

    private static DiagramLabel Label(HoleDiagram diagram, string text) => Assert.Single(diagram.Labels, l => l.Text == text);

    [Fact]
    public void BlindDowelWithCounterbore_ShowsSeatDepthDiameterWithToleranceAndPoint()
    {
        using var culture = new CultureScope("en-US");

        var diagram = HoleDiagram.Draw(BlindDowelCounterbore);

        Assert.Equal(
            new[] { "31", "6.5", Dia + "11", Dia + "6 H7", "118" + Deg }.OrderBy(t => t),
            Texts(diagram).OrderBy(t => t));
        Assert.True(Label(diagram, "31").IsExpression);
        Assert.True(Label(diagram, "6.5").IsExpression);
        Assert.False(Label(diagram, Dia + "6 H7").IsExpression);
        Assert.Single(diagram.Material);
        Assert.DoesNotContain(diagram.Lines, l => l.Ink == DiagramInk.Thread);
        Assert.Single(diagram.Lines, l => l.Ink == DiagramInk.CentreLine);
    }

    [Fact]
    public void BlindDowel_DepthIsOnTheLeft_SeatDepthOnTheRight_SeatDiameterAbove()
    {
        using var culture = new CultureScope("en-US");

        var diagram = HoleDiagram.Draw(BlindDowelCounterbore);
        var centre = diagram.Lines.Single(l => l.Ink == DiagramInk.CentreLine).Points[0].X;
        var outlineTop = diagram.Lines.Where(l => l.Ink == DiagramInk.Outline).SelectMany(l => l.Points).Min(p => p.Y);

        Assert.True(Label(diagram, "31").At.X < centre);
        Assert.Equal(DiagramAnchor.End, Label(diagram, "31").Anchor);
        Assert.True(Label(diagram, "6.5").At.X > centre);
        Assert.Equal(DiagramAnchor.Start, Label(diagram, "6.5").Anchor);
        Assert.True(Label(diagram, Dia + "11").At.Y < outlineTop);
        Assert.Equal(DiagramAnchor.Middle, Label(diagram, Dia + "11").Anchor);
    }

    [Fact]
    public void BlindTapped_ShowsDrillAndThreadDepths_TapDrill_ThreadName_AndThread()
    {
        using var culture = new CultureScope("en-US");

        var diagram = HoleDiagram.Draw(BlindTapped);

        Assert.Equal(
            new[] { "12", "9", Dia + "5", "M6x1", "118" + Deg }.OrderBy(t => t),
            Texts(diagram).OrderBy(t => t));
        Assert.Equal(4, diagram.Lines.Count(l => l.Ink == DiagramInk.Thread));
        Assert.All(diagram.Labels, l => Assert.False(l.IsExpression));
    }

    [Fact]
    public void ThroughTapped_ThreadsTheFullDepth_WithNoDepthsOrPoint()
    {
        using var culture = new CultureScope("en-US");

        var diagram = HoleDiagram.Draw(BlindTapped with { Termination = HoleTermination.ThroughAll });

        Assert.Equal(new[] { Dia + "5", "M6x1" }.OrderBy(t => t), Texts(diagram).OrderBy(t => t));
        Assert.Equal(2, diagram.Lines.Count(l => l.Ink == DiagramInk.Thread));
        Assert.Equal(2, diagram.Material.Count);
    }

    [Fact]
    public void ThroughClearanceWithCounterbore_ShowsSeatAndHole_NoDepthNoPoint()
    {
        using var culture = new CultureScope("en-US");

        var diagram = HoleDiagram.Draw(ThroughClearanceCounterbore);

        Assert.Equal(new[] { Dia + "11", "6.5", Dia + "6.6" }.OrderBy(t => t), Texts(diagram).OrderBy(t => t));
        Assert.Equal(2, diagram.Material.Count);
        Assert.Equal(2, diagram.Lines.Count(l => l.Ink == DiagramInk.Outline));
    }

    [Fact]
    public void ThroughDowelWithCountersink_ShowsSeatDiameterAngleAndToleranceDiameter()
    {
        using var culture = new CultureScope("en-US");

        var diagram = HoleDiagram.Draw(ThroughDowelCountersink);

        Assert.Equal(new[] { Dia + "9", "90" + Deg, Dia + "6 G7" }.OrderBy(t => t), Texts(diagram).OrderBy(t => t));
    }

    [Fact]
    public void FlatBottom_HasNoAngle_AndTheOutlineEndsFlat()
    {
        var diagram = HoleDiagram.Draw(BlindDowelCounterbore with { PointAngleDeg = null });

        Assert.DoesNotContain(diagram.Labels, l => l.Text.EndsWith(Deg, StringComparison.Ordinal));
        var outline = diagram.Lines.Single(l => l.Ink == DiagramInk.Outline).Points;
        var bottom = outline.Max(p => p.Y);
        // Both walls end at the bottom, which runs straight across through the centre.
        var atBottom = outline.Where(p => Math.Abs(p.Y - bottom) < 1e-9).ToList();
        Assert.Equal(3, atBottom.Count);
        Assert.True(atBottom.Min(p => p.X) < atBottom[1].X && atBottom[1].X < atBottom.Max(p => p.X));
    }

    [Fact]
    public void NinetyDegreePoint_IsDeeperThanOneHundredEighteen_ForTheSameHole()
    {
        static double TipBelowWalls(HoleDiagram d)
        {
            var outline = d.Lines.Single(l => l.Ink == DiagramInk.Outline).Points;
            var tip = outline.Max(p => p.Y);
            var wallBottom = outline.Where(p => p.Y < tip - 1e-9).Max(p => p.Y);
            var width = outline.Max(p => p.X) - outline.Min(p => p.X);
            return (tip - wallBottom) / width;
        }

        var steep = TipBelowWalls(HoleDiagram.Draw(BlindTapped with { PointAngleDeg = 90m }));
        var shallow = TipBelowWalls(HoleDiagram.Draw(BlindTapped with { PointAngleDeg = 118m }));

        Assert.True(steep > shallow * 1.3, $"90 deg tip {steep}, 118 deg tip {shallow}");
    }

    [Fact]
    public void UnknownValues_AreLeftOut_ButTheHoleIsStillDrawn()
    {
        var diagram = HoleDiagram.Draw(BlindDowelCounterbore with
        {
            HoleDiameter = null,
            Depth = null,
            CounterboreDepth = null,
        });

        Assert.Equal(new[] { Dia + "11", "118" + Deg }.OrderBy(t => t), Texts(diagram).OrderBy(t => t));
        Assert.Single(diagram.Material);
        Assert.NotEmpty(diagram.Lines.Single(l => l.Ink == DiagramInk.Outline).Points);
    }

    public static TheoryData<string> Cases => new()
    {
        "dowel counterbore", "tapped", "tapped through", "clearance counterbore", "dowel countersink", "plain through",
        "tapped counterbore blind", "imperial dowel", "seat narrower than hole", "seat deeper than hole", "nothing known",
        "long expression values", "tiny countersink angle",
    };

    private static HoleDiagramInput Case(string name) => name switch
    {
        "dowel counterbore" => BlindDowelCounterbore,
        "tapped" => BlindTapped,
        "tapped through" => BlindTapped with { Termination = HoleTermination.ThroughAll },
        "clearance counterbore" => ThroughClearanceCounterbore,
        "dowel countersink" => ThroughDowelCountersink,
        "plain through" => new HoleDiagramInput { Kind = HoleKind.Clearance, HoleDiameter = 5.5m },
        "tapped counterbore blind" => BlindTapped with { Seat = HoleSeat.Counterbore, CounterboreDiameter = 11m, CounterboreDepth = 3m },
        "imperial dowel" => BlindDowelCounterbore with
        {
            Units = HoleUnits.Imperial, HoleDiameter = 0.25m, Depth = 1.25m, CounterboreDiameter = 0.4375m,
            CounterboreDepth = 0.25m, Tolerance = "+0.0005 / 0",
        },
        "seat narrower than hole" => BlindDowelCounterbore with { CounterboreDiameter = 4m },
        "seat deeper than hole" => BlindDowelCounterbore with { CounterboreDepth = 40m },
        "nothing known" => new HoleDiagramInput { Termination = HoleTermination.Distance, Seat = HoleSeat.Countersink },
        "long expression values" => BlindDowelCounterbore with { Depth = 123.456m, CounterboreDepth = 12.345m },
        "tiny countersink angle" => ThroughDowelCountersink with { CountersinkAngleDeg = 1m },
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void EveryLineAndLabel_FitsTheBox(string name)
    {
        using var culture = new CultureScope("en-US");

        var diagram = HoleDiagram.Draw(Case(name));

        Assert.Equal(HoleDiagram.DefaultWidth, diagram.Width);
        Assert.Equal(HoleDiagram.DefaultHeight, diagram.Height);
        var points = diagram.Material.SelectMany(p => p)
            .Concat(diagram.Lines.SelectMany(l => l.Points))
            .Concat(diagram.Arrows.Select(a => a.Tip))
            .ToList();
        Assert.NotEmpty(points);
        Assert.All(points, p =>
        {
            Assert.True(double.IsFinite(p.X) && double.IsFinite(p.Y), $"{name}: {p}");
            Assert.InRange(p.X, -0.5, diagram.Width + 0.5);
            Assert.InRange(p.Y, -0.5, diagram.Height + 0.5);
        });
        Assert.All(diagram.Labels, l =>
        {
            var w = HoleDiagram.EstimateTextWidth(l.Text, l.IsExpression);
            var left = l.Anchor switch
            {
                DiagramAnchor.Start => l.At.X,
                DiagramAnchor.Middle => l.At.X - w / 2,
                _ => l.At.X - w,
            };
            Assert.True(left >= -0.5 && left + w <= diagram.Width + 0.5, $"{name}: '{l.Text}' from {left} to {left + w}");
            Assert.True(l.At.Y - 6 >= -0.5 && l.At.Y + 6 <= diagram.Height + 0.5, $"{name}: '{l.Text}' at {l.At.Y}");
        });
    }

    [Fact]
    public void Draw_InAnotherBox_FitsThatBox()
    {
        var diagram = HoleDiagram.Draw(BlindTapped, 200, 172);

        Assert.All(diagram.Lines.SelectMany(l => l.Points), p =>
        {
            Assert.InRange(p.X, -0.5, 200.5);
            Assert.InRange(p.Y, -0.5, 172.5);
        });
    }

    [Fact]
    public void Arrows_PointAlongUnitDirections()
    {
        var diagram = HoleDiagram.Draw(BlindDowelCounterbore);

        Assert.NotEmpty(diagram.Arrows);
        Assert.All(diagram.Arrows, a =>
            Assert.Equal(1.0, Math.Sqrt(a.DirectionX * a.DirectionX + a.DirectionY * a.DirectionY), 6));
    }

    // ---- The collapsed summary ----

    [Fact]
    public void Summary_BlindDowelWithCounterbore_ReadsAsTheMockup()
    {
        using var culture = new CultureScope("en-US");

        Assert.Equal($"{Dia}6 H7, 31 deep, counterbore {Dia}11 by 6.5", HoleDiagram.Summary(BlindDowelCounterbore));
    }

    [Fact]
    public void Summary_Tapped_NamesTheThreadAndBothDepths()
    {
        using var culture = new CultureScope("en-US");

        Assert.Equal("M6x1, thread 9, drill 12", HoleDiagram.Summary(BlindTapped));
        Assert.Equal("M6x1, through all", HoleDiagram.Summary(BlindTapped with { Termination = HoleTermination.ThroughAll }));
    }

    [Fact]
    public void Summary_CountersinkAndToFace()
    {
        using var culture = new CultureScope("en-US");

        Assert.Equal(
            $"{Dia}6 G7, through all, countersink {Dia}9 at 90{Deg}", HoleDiagram.Summary(ThroughDowelCountersink));
        Assert.Equal(
            $"{Dia}6.6, to a face, counterbore {Dia}11 by 6.5",
            HoleDiagram.Summary(ThroughClearanceCounterbore with { Termination = HoleTermination.ToFace }));
    }

    [Fact]
    public void Summary_UnknownValues_ShowAQuestionMark()
    {
        var summary = HoleDiagram.Summary(new HoleDiagramInput
        {
            Kind = HoleKind.Dowel,
            Seat = HoleSeat.Counterbore,
            Termination = HoleTermination.Distance,
        });

        Assert.Equal($"{Dia}?, ? deep, counterbore {Dia}? by ?", summary);
        Assert.Equal("?, thread ?, drill ?", HoleDiagram.Summary(new HoleDiagramInput
        {
            Kind = HoleKind.Tapped,
            Termination = HoleTermination.Distance,
        }));
    }

    // ---- Numbers ----

    [Theory]
    [InlineData(31, HoleUnits.Metric, "31")]
    [InlineData(6.5, HoleUnits.Metric, "6.5")]
    [InlineData(6.6666, HoleUnits.Metric, "6.667")]
    [InlineData(0.25, HoleUnits.Imperial, "0.25")]
    [InlineData(0.123456, HoleUnits.Imperial, "0.1235")]
    public void FormatLength_RoundsByUnits_AndDropsTrailingZeros(double value, HoleUnits units, string expected)
    {
        using var culture = new CultureScope("en-US");

        Assert.Equal(expected, HoleDiagram.FormatLength((decimal)value, units));
    }

    [Fact]
    public void FormatLength_UsesTheUsersDecimalSeparator()
    {
        using var culture = new CultureScope("de-DE");

        Assert.Equal("6,5", HoleDiagram.FormatLength(6.5m, HoleUnits.Metric));
    }

    [Fact]
    public void FormatAngle_AddsTheDegreeSign()
    {
        using var culture = new CultureScope("en-US");

        Assert.Equal("118" + Deg, HoleDiagram.FormatAngle(118m));
        Assert.Equal("82.5" + Deg, HoleDiagram.FormatAngle(82.5m));
    }
}
