using InventorAddin.Core.Drawing;

namespace InventorAddin.Core.Tests.Drawing;

public class RefDimensionPlanTests
{
    // A 26 x 10 cm rectangle with its corner at (5, 8), as four lines.
    private static List<RefCurve> Box() => new()
    {
        RefCurve.Line(5, 8, 31, 8),
        RefCurve.Line(31, 8, 31, 18),
        RefCurve.Line(31, 18, 5, 18),
        RefCurve.Line(5, 18, 5, 8),
        RefCurve.Line(10, 10, 12, 10),
    };

    private static readonly ViewRectCm Rect = new(5, 8, 31, 18);

    [Fact]
    public void Box_PicksLeftRightLowHigh()
    {
        var r = RefDimensionPlan.Plan(Box(), Rect)!;
        Assert.Equal(3, r.Width.FirstCurveIndex);
        Assert.Equal(1, r.Width.SecondCurveIndex);
        Assert.Equal(26, r.Width.SpanCm, 6);
        Assert.Equal(0, r.Height.FirstCurveIndex);
        Assert.Equal(2, r.Height.SecondCurveIndex);
        Assert.Equal(10, r.Height.SpanCm, 6);
        Assert.False(r.Width.NeedsExtentFallback);
        Assert.False(r.Height.NeedsExtentFallback);
        Assert.True(r.Width.IsHorizontal);
        Assert.False(r.Height.IsHorizontal);
    }

    [Fact]
    public void TextPoints_TenMmOutside_BelowAndRight()
    {
        var r = RefDimensionPlan.Plan(Box(), Rect)!;
        Assert.Equal(18, r.Width.TextPoint.XCm, 6);
        Assert.Equal(7, r.Width.TextPoint.YCm, 6);
        Assert.Equal(32, r.Height.TextPoint.XCm, 6);
        Assert.Equal(13, r.Height.TextPoint.YCm, 6);
    }

    [Fact]
    public void ArcAtExtreme_CountsByExtents_AndNeedsFallback()
    {
        // A circle of radius 3 centred at (20, 12), sticking out left of a box that starts at x = 18.
        var curves = new List<RefCurve>
        {
            RefCurve.Line(18, 10, 30, 10),
            RefCurve.Line(30, 10, 30, 14),
            RefCurve.Arc(20, 12, 3, 0, 2 * Math.PI),
        };
        var r = RefDimensionPlan.Plan(curves, new ViewRectCm(17, 9, 30, 15))!;
        Assert.Equal(2, r.Width.FirstCurveIndex);
        Assert.Equal(17, r.Width.FirstCm, 6);
        Assert.True(r.Width.NeedsExtentFallback);
        Assert.Equal(2, r.Height.SecondCurveIndex);
        Assert.Equal(15, r.Height.SecondCm, 6);
        Assert.True(r.Height.NeedsExtentFallback);
    }

    [Fact]
    public void Arc_QuarterSweep_UsesPassedQuadrantPoints()
    {
        // From 45 to 135 degrees passes the top (90) only.
        var a = RefCurve.Arc(0, 0, 2, Math.PI / 4, Math.PI / 2);
        Assert.Equal(2, a.MaxYCm, 6);
        Assert.Equal(-Math.Sqrt(2), a.MinXCm, 6);
        Assert.Equal(Math.Sqrt(2), a.MaxXCm, 6);
        Assert.Equal(Math.Sqrt(2), a.MinYCm, 6);
    }

    [Fact]
    public void Arc_SweepWrapsPastZero()
    {
        // From 300 to 60 degrees passes angle 0, so the rightmost point is the full radius.
        var a = RefCurve.Arc(1, 1, 2, 5 * Math.PI / 3, 2 * Math.PI / 3);
        Assert.Equal(3, a.MaxXCm, 6);
    }

    [Fact]
    public void TieOnExtreme_PrefersStraightLine()
    {
        var curves = new List<RefCurve>
        {
            RefCurve.Arc(2, 5, 2, Math.PI / 2, Math.PI), // leftmost point at x = 0
            RefCurve.Line(0, 0, 0, 10),
            RefCurve.Line(0, 0, 8, 0),
            RefCurve.Line(8, 0, 8, 10),
        };
        var r = RefDimensionPlan.Plan(curves, new ViewRectCm(0, 0, 8, 10))!;
        Assert.Equal(1, r.Width.FirstCurveIndex);
        Assert.Equal(3, r.Width.SecondCurveIndex);
        Assert.False(r.Width.NeedsExtentFallback);
    }

    [Fact]
    public void NoCurves_ReturnsNull() => Assert.Null(RefDimensionPlan.Plan(new List<RefCurve>(), Rect));

    [Fact]
    public void Text_IsValueThenRef_Bold()
    {
        Assert.Equal("260 REF", RefDimensionPlan.PlainText(260));
        Assert.Equal("<StyleOverride Bold='True'><DimensionValue/> REF</StyleOverride>", RefDimensionPlan.FormattedText);
        Assert.True(RefDimensionPlan.TextHeightScale > 1);
    }

    [Fact]
    public void Arc_NegativeSweep_SameExtentsAsCounterClockwise()
    {
        var cw = RefCurve.Arc(0, 0, 2, 3 * Math.PI / 4, -Math.PI / 2);
        var ccw = RefCurve.Arc(0, 0, 2, Math.PI / 4, Math.PI / 2);
        Assert.Equal(2, cw.MaxYCm, 6);
        Assert.Equal(ccw.MinXCm, cw.MinXCm, 6);
        Assert.Equal(ccw.MaxXCm, cw.MaxXCm, 6);
        Assert.Equal(ccw.MinYCm, cw.MinYCm, 6);
        Assert.Equal(ccw.MaxYCm, cw.MaxYCm, 6);
    }

    [Fact]
    public void Arc_SweepOverTwoPi_IsACircle()
    {
        var a = RefCurve.Arc(1, 1, 2, 0.3, 3 * Math.PI);
        Assert.Equal(-1, a.MinXCm, 6);
        Assert.Equal(3, a.MaxXCm, 6);
        Assert.Equal(-1, a.MinYCm, 6);
        Assert.Equal(3, a.MaxYCm, 6);
    }

    [Fact]
    public void HeightAxisTie_HorizontalLineBeatsArc()
    {
        var curves = new List<RefCurve>
        {
            RefCurve.Arc(4, 2, 2, Math.PI, Math.PI), // lowest point y = 0, highest y = 2 at the ends
            RefCurve.Line(0, 0, 8, 0),
            RefCurve.Line(0, 10, 8, 10),
            RefCurve.Arc(4, 8, 2, 0, Math.PI), // top at y = 10
        };
        var r = RefDimensionPlan.Plan(curves, new ViewRectCm(0, 0, 8, 10))!;
        Assert.Equal(1, r.Height.FirstCurveIndex);
        Assert.Equal(2, r.Height.SecondCurveIndex);
        Assert.False(r.Height.NeedsExtentFallback);
    }

    [Fact]
    public void Tie_StraightFirstThenArc_KeepsStraight()
    {
        var curves = new List<RefCurve>
        {
            RefCurve.Line(0, 0, 0, 10),
            RefCurve.Arc(2, 5, 2, Math.PI / 2, Math.PI),
            RefCurve.Line(8, 0, 8, 10),
        };
        var r = RefDimensionPlan.Plan(curves, new ViewRectCm(0, 0, 8, 10))!;
        Assert.Equal(0, r.Width.FirstCurveIndex);
        Assert.False(r.Width.NeedsExtentFallback);
    }

    [Fact]
    public void LineAtOneEnd_ArcAtOther_FlagsThePair()
    {
        var curves = new List<RefCurve>
        {
            RefCurve.Line(0, 0, 0, 10),
            RefCurve.Line(0, 0, 6, 0),
            RefCurve.Line(0, 10, 6, 10),
            RefCurve.Arc(6, 5, 5, -Math.PI / 2, Math.PI), // rightmost at x = 11
        };
        var r = RefDimensionPlan.Plan(curves, new ViewRectCm(0, 0, 11, 10))!;
        Assert.Equal(0, r.Width.FirstCurveIndex);
        Assert.Equal(3, r.Width.SecondCurveIndex);
        Assert.True(r.Width.NeedsExtentFallback);
        Assert.False(r.Height.NeedsExtentFallback);
    }

    [Fact]
    public void SlopedLineAtExtreme_NeedsExtentFallback()
    {
        var curves = new List<RefCurve>
        {
            RefCurve.Line(0, 0, 4, 10),
            RefCurve.Line(4, 10, 8, 10),
            RefCurve.Line(8, 10, 8, 0),
            RefCurve.Line(8, 0, 0, 0),
        };
        var r = RefDimensionPlan.Plan(curves, new ViewRectCm(0, 0, 8, 10))!;
        Assert.True(r.Width.NeedsExtentFallback);
        Assert.False(r.Height.NeedsExtentFallback);
    }

    [Fact]
    public void TextPoints_CentredOnGeometry_NotOnViewRect()
    {
        // Geometry spans x 10..20, y 4..6; the view rectangle is wider (annotations) at 0..30, 0..12.
        var curves = new List<RefCurve>
        {
            RefCurve.Line(10, 4, 20, 4), RefCurve.Line(20, 4, 20, 6),
            RefCurve.Line(20, 6, 10, 6), RefCurve.Line(10, 6, 10, 4),
        };
        var r = RefDimensionPlan.Plan(curves, new ViewRectCm(0, 0, 30, 12))!;
        Assert.Equal(15, r.Width.TextPoint.XCm, 6);
        Assert.Equal(-1, r.Width.TextPoint.YCm, 6);
        Assert.Equal(31, r.Height.TextPoint.XCm, 6);
        Assert.Equal(5, r.Height.TextPoint.YCm, 6);
    }
}
