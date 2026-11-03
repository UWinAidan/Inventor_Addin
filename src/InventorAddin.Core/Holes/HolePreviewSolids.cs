namespace InventorAddin.Core.Holes;

/// <summary>
/// How the AWB Hole preview is drawn (task 110; spec 09, "Live 3D preview"; spike 107): a faint, see-through red copy
/// of the hole volume, drawn over the model with client graphics. The values are Aidan's, from spike 107.
/// </summary>
public static class HolePreviewGraphics
{
    /// <summary>The colour: RGB 255, 0, 0 (Aidan, spike 107).</summary>
    public static HoleColour Colour { get; } = new(255, 0, 0);

    /// <summary>How opaque the preview is, 0 to 1: 0.25 (Aidan, spike 107).</summary>
    public const double Opacity = 0.25;

    /// <summary>Drawn over the part's faces, so the whole volume shows (spike 107: without it, "looks terrible").</summary>
    public const bool BurnThrough = true;

    /// <summary>The surface graphics' depth priority (spike 107: 0 was enough).</summary>
    public const int DepthPriority = 0;

    /// <summary>
    /// The client graphics' id in the part's <c>ClientGraphicsCollection</c>: <c>Awb_HolePreview</c>. Found again by
    /// it to remove the preview.
    /// </summary>
    public static string ClientId => $"{Branding.IdPrefix}_HolePreview";
}

/// <summary>The kind of face a preview solid is made from.</summary>
public enum HolePreviewSurface
{
    Cylinder,
    Cone,
}

/// <summary>
/// A cylindrical or conical face of a hole, read from Inventor for the preview (task 110), in centimetres and radians.
/// </summary>
/// <param name="Surface">A cylinder or a cone.</param>
/// <param name="AxisPoint">The surface's base point, on its axis.</param>
/// <param name="AxisDirection">The surface's axis. Need not be unit length.</param>
/// <param name="RadiusCm">The radius at <paramref name="AxisPoint"/>.</param>
/// <param name="HalfAngleRad">A cone's half-angle; ignored for a cylinder.</param>
/// <param name="IsExpanding">A cone's radius grows along <paramref name="AxisDirection"/>; ignored for a cylinder.</param>
/// <param name="Points">Points sampled along every edge of the face (or an edge's vertices when it cannot be
/// sampled). Only their position along the axis is used.</param>
public sealed record HolePreviewFace(
    HolePreviewSurface Surface,
    HoleVector AxisPoint,
    HoleVector AxisDirection,
    double RadiusCm,
    double HalfAngleRad,
    bool IsExpanding,
    IReadOnlyList<HoleVector> Points);

/// <summary>
/// A solid cylinder or cone to draw: from <paramref name="Bottom"/> to <paramref name="Top"/> on the face's axis, with
/// a radius at each end, as <c>TransientBRep.CreateSolidCylinderCone</c> takes it.
/// </summary>
public readonly record struct HolePreviewSolid(HoleVector Bottom, HoleVector Top, double BottomRadiusCm, double TopRadiusCm);

/// <summary>
/// The solids the AWB Hole preview draws (task 110; spike 107 round 4): one solid of revolution for each cylindrical
/// and conical face of the hole, the full 360 degrees, over the face's extent along its axis. The extent comes from
/// the points sampled along the face's edges, arcs included, so a seat that runs off the edge of the part is drawn
/// whole, with its end caps. A cone whose points all sit at one height (a drill point, whose apex edge cannot be
/// sampled) runs to its apex. No radius is under <see cref="MinRadiusCm"/>, since Inventor refuses a radius of 0.
/// </summary>
public static class HolePreviewSolids
{
    /// <summary>The smallest end radius, in centimetres: a cone's tip (spike 107 round 4).</summary>
    public const double MinRadiusCm = 0.001;

    /// <summary>How many points are sampled along each edge, its two ends included (spike 107 round 3).</summary>
    public const int PointsPerEdge = 25;

    /// <summary>A face shorter than this along its axis, in centimetres, has no length.</summary>
    public const double MinLengthCm = 1e-6;

    /// <summary>
    /// <see cref="PointsPerEdge"/> parameters spread evenly from <paramref name="start"/> to
    /// <paramref name="end"/>, both included: what the add-in asks an edge's evaluator for.
    /// </summary>
    public static double[] SampleParams(double start, double end)
    {
        var values = new double[PointsPerEdge];
        for (var i = 0; i < PointsPerEdge; i++)
        {
            values[i] = start + (end - start) * i / (PointsPerEdge - 1);
        }

        return values;
    }

    /// <summary>
    /// The points in a flat array of coordinates, three to a point (as Inventor's evaluators give them). A trailing
    /// part point is left out.
    /// </summary>
    public static IReadOnlyList<HoleVector> PointsOf(double[] xyz)
    {
        ArgumentNullException.ThrowIfNull(xyz);

        var points = new List<HoleVector>(xyz.Length / 3);
        for (var i = 0; i + 2 < xyz.Length; i += 3)
        {
            points.Add(new HoleVector(xyz[i], xyz[i + 1], xyz[i + 2]));
        }

        return points;
    }

    /// <summary>The solid for each face that has one, in order; faces with none are left out.</summary>
    public static IReadOnlyList<HolePreviewSolid> For(IEnumerable<HolePreviewFace> faces)
    {
        ArgumentNullException.ThrowIfNull(faces);

        var solids = new List<HolePreviewSolid>();
        foreach (var face in faces)
        {
            if (For(face) is { } solid)
            {
                solids.Add(solid);
            }
        }

        return solids;
    }

    /// <summary>
    /// The solid for one face, or null when it has none: an axis of no length, no usable points, or no length along
    /// its axis (a cone with no apex included).
    /// </summary>
    public static HolePreviewSolid? For(HolePreviewFace face)
    {
        ArgumentNullException.ThrowIfNull(face);

        var length = face.AxisDirection.Length;
        if (!double.IsFinite(length) || length < 1e-12)
        {
            return null;
        }

        var axis = new HoleVector(face.AxisDirection.X / length, face.AxisDirection.Y / length, face.AxisDirection.Z / length);
        var basePoint = face.AxisPoint;

        double slope = 0;
        double? apex = null;
        if (face.Surface == HolePreviewSurface.Cone)
        {
            slope = Math.Tan(face.HalfAngleRad) * (face.IsExpanding ? 1 : -1);
            if (double.IsFinite(slope) && Math.Abs(slope) > 1e-12)
            {
                apex = -face.RadiusCm / slope;
            }
        }

        var t0 = double.MaxValue;
        var t1 = double.MinValue;
        foreach (var point in face.Points ?? [])
        {
            var t = (point - basePoint).Dot(axis);
            if (!double.IsFinite(t))
            {
                continue;
            }

            t0 = Math.Min(t0, t);
            t1 = Math.Max(t1, t);
        }

        if (t0 > t1)
        {
            return null;
        }

        if (t1 - t0 < MinLengthCm && apex is { } tip)
        {
            t0 = Math.Min(t0, tip);
            t1 = Math.Max(t1, tip);
        }

        if (t1 - t0 < MinLengthCm)
        {
            return null;
        }

        double RadiusAt(double t) => Math.Max(MinRadiusCm, face.RadiusCm + t * slope);

        return new HolePreviewSolid(
            At(basePoint, axis, t0),
            At(basePoint, axis, t1),
            RadiusAt(t0),
            RadiusAt(t1));
    }

    private static HoleVector At(HoleVector basePoint, HoleVector axis, double t) =>
        new(basePoint.X + axis.X * t, basePoint.Y + axis.Y * t, basePoint.Z + axis.Z * t);
}
