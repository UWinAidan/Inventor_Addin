using System.Globalization;

namespace InventorAddin.Core.Drawing;

/// <summary>
/// One curve of a drawing view, reduced to what the REF plan needs: its end points and its extents, in sheet centimetres.
/// Build it with <see cref="Line"/> or <see cref="Arc"/> so an arc counts by its extents, not its end points.
/// </summary>
public readonly record struct RefCurve(
    double StartXCm, double StartYCm, double EndXCm, double EndYCm,
    double MinXCm, double MinYCm, double MaxXCm, double MaxYCm)
{
    private const double Tol = 1e-9;

    /// <summary>A straight segment.</summary>
    public static RefCurve Line(double x1, double y1, double x2, double y2) =>
        new(x1, y1, x2, y2, Math.Min(x1, x2), Math.Min(y1, y2), Math.Max(x1, x2), Math.Max(y1, y2));

    /// <summary>
    /// An arc from <paramref name="startRad"/> through <paramref name="sweepRad"/>; counter-clockwise when positive, clockwise when negative (2 pi for a circle).
    /// Extents include any quadrant point the sweep passes.
    /// </summary>
    public static RefCurve Arc(double cx, double cy, double radius, double startRad, double sweepRad)
    {
        if (sweepRad < 0) { startRad += sweepRad; sweepRad = -sweepRad; }
        sweepRad = Math.Min(sweepRad, 2 * Math.PI);
        double endRad = startRad + sweepRad;
        double sx = cx + radius * Math.Cos(startRad), sy = cy + radius * Math.Sin(startRad);
        double ex = cx + radius * Math.Cos(endRad), ey = cy + radius * Math.Sin(endRad);
        double minX = Math.Min(sx, ex), maxX = Math.Max(sx, ex), minY = Math.Min(sy, ey), maxY = Math.Max(sy, ey);
        if (sweepRad >= 2 * Math.PI - Tol)
        {
            minX = cx - radius; maxX = cx + radius; minY = cy - radius; maxY = cy + radius;
        }
        else
        {
            if (Passes(startRad, sweepRad, 0)) maxX = cx + radius;
            if (Passes(startRad, sweepRad, Math.PI / 2)) maxY = cy + radius;
            if (Passes(startRad, sweepRad, Math.PI)) minX = cx - radius;
            if (Passes(startRad, sweepRad, 3 * Math.PI / 2)) minY = cy - radius;
        }
        return new RefCurve(sx, sy, ex, ey, minX, minY, maxX, maxY);
    }

    private static bool Passes(double startRad, double sweepRad, double angle)
    {
        double d = (angle - startRad) % (2 * Math.PI);
        if (d < 0) d += 2 * Math.PI;
        return d <= sweepRad;
    }

    /// <summary>True for a vertical straight line (an arc is never straight, even if its end points line up).</summary>
    public bool IsVertical =>
        Math.Abs(StartXCm - EndXCm) < Tol && Math.Abs(MinXCm - MaxXCm) < Tol && Math.Abs(MaxYCm - MinYCm) > Tol;

    /// <summary>True for a horizontal straight line.</summary>
    public bool IsHorizontal =>
        Math.Abs(StartYCm - EndYCm) < Tol && Math.Abs(MinYCm - MaxYCm) < Tol && Math.Abs(MaxXCm - MinXCm) > Tol;
}

/// <summary>A sheet point in centimetres.</summary>
public readonly record struct RefPoint(double XCm, double YCm);

/// <summary>The view's rectangle on the sheet, in centimetres, Y up.</summary>
public readonly record struct ViewRectCm(double MinXCm, double MinYCm, double MaxXCm, double MaxYCm);

/// <summary>
/// One overall dimension. <see cref="FirstCurveIndex"/> and <see cref="SecondCurveIndex"/> index the input curves
/// (least and greatest X for width, Y for height). <see cref="NeedsExtentFallback"/> is true when an end is not a straight
/// vertical (width) or horizontal (height) line: it can be an arc, a sloped line, or a line running along the axis.
/// The writer must then dimension to the curve's extent (its 2D evaluator range) instead of the curve itself.
/// Do not assume the extreme is an arc.
/// </summary>
public sealed record RefDimension(
    bool IsHorizontal,
    int FirstCurveIndex,
    int SecondCurveIndex,
    double FirstCm,
    double SecondCm,
    bool NeedsExtentFallback,
    RefPoint TextPoint)
{
    /// <summary>The geometry's size on the sheet in centimetres (model size times view scale).</summary>
    public double SpanCm => SecondCm - FirstCm;
}

/// <summary>The two overall REF dimensions for a view.</summary>
public sealed record RefDimensionResult(RefDimension Width, RefDimension Height);

/// <summary>
/// Picks the extreme edges of a view and where its overall REF dimensions go (spec 05, "Overall REF dimensions", task 203).
/// Defaults (DECISIONS 2026-11-21): one horizontal and one vertical dimension, text 10 mm outside the view, below and to the right.
/// Aidan 2026-11-23: text reads "260 REF", number and REF bold, slightly larger than normal dimensions.
/// </summary>
public static class RefDimensionPlan
{
    /// <summary>Gap between the view's rectangle and the dimension text, in millimetres.</summary>
    public const double TextOffsetMm = 10;

    /// <summary>Default: REF text is this many times the normal dimension text height ("slightly larger").</summary>
    public const double TextHeightScale = 1.25;

    /// <summary>Inventor formatted text for a REF dimension: the dimension value then " REF", all bold.</summary>
    public const string FormattedText = "<StyleOverride Bold='True'><DimensionValue/> REF</StyleOverride>";

    /// <summary>The plain text a REF dimension prints for a value, for display and tests: "260 REF".</summary>
    public static string PlainText(double value, int decimals = 0) =>
        value.ToString("F" + Math.Max(0, decimals), CultureInfo.InvariantCulture) + " REF";

    /// <summary>
    /// Plans the dimensions: the width pair is the leftmost and rightmost geometry, the height pair the lowest and highest.
    /// On a tie, a straight line wins over an arc. The width text sits below the view and the height text to its right,
    /// each <see cref="TextOffsetMm"/> outside <paramref name="viewRect"/>. Returns null when there are no curves.
    /// </summary>
    public static RefDimensionResult? Plan(IReadOnlyList<RefCurve> curves, ViewRectCm viewRect)
    {
        if (curves.Count == 0) return null;
        const double eps = 1e-6;

        int left = 0, right = 0, low = 0, high = 0;
        for (int i = 1; i < curves.Count; i++)
        {
            var c = curves[i];
            if (Better(c.MinXCm, curves[left].MinXCm, true, c.IsVertical, curves[left].IsVertical)) left = i;
            if (Better(c.MaxXCm, curves[right].MaxXCm, false, c.IsVertical, curves[right].IsVertical)) right = i;
            if (Better(c.MinYCm, curves[low].MinYCm, true, c.IsHorizontal, curves[low].IsHorizontal)) low = i;
            if (Better(c.MaxYCm, curves[high].MaxYCm, false, c.IsHorizontal, curves[high].IsHorizontal)) high = i;
        }

        double minX = curves[left].MinXCm, maxX = curves[right].MaxXCm;
        double minY = curves[low].MinYCm, maxY = curves[high].MaxYCm;
        double offsetCm = TextOffsetMm / 10.0;

        var width = new RefDimension(true, left, right, minX, maxX,
            !(curves[left].IsVertical && curves[right].IsVertical),
            new RefPoint((minX + maxX) / 2, viewRect.MinYCm - offsetCm));
        var height = new RefDimension(false, low, high, minY, maxY,
            !(curves[low].IsHorizontal && curves[high].IsHorizontal),
            new RefPoint(viewRect.MaxXCm + offsetCm, (minY + maxY) / 2));
        return new RefDimensionResult(width, height);

        static bool Better(double candidate, double current, bool wantLower, bool candStraight, bool curStraight)
        {
            double d = wantLower ? current - candidate : candidate - current;
            if (d > eps) return true;
            return Math.Abs(d) <= eps && candStraight && !curStraight;
        }
    }
}
