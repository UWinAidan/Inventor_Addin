namespace InventorAddin.Core.Drawing;

/// <summary>A bend's axis (any length, either sign) in model space, and its length along the axis, in cm.</summary>
public sealed record BendAxisCm(double DirX, double DirY, double DirZ, double LengthCm);

/// <summary>
/// The rule for a sheet metal part's folded front view (task 242): look along the bend lines, so the bend profile shows (a Z-shaped
/// part prints as a Z). The viewing direction is the axis shared by most bends (parallel either sign); a tie goes to the direction with
/// the longest total bend length. Up on the sheet is the base face's normal with any part along the viewing direction removed, so the
/// base flange prints horizontal. With no usable bend (none, or every axis parallel to the base normal) the old base-face rule applies.
/// </summary>
public static class FoldedViewRule
{
    private const double Tolerance = 1e-9;

    // Two axes count as parallel when the sine of the angle between them is under this (about 1 degree).
    private const double ParallelSine = 0.0175;

    /// <summary>
    /// The orientation for a folded sheet metal part, or null when neither the bends nor the base face's edges give one. The edges are
    /// the base face's straight edges, used only by the fallback.
    /// </summary>
    public static FaceViewOrientation? Choose(
        double nx, double ny, double nz, IEnumerable<BendAxisCm> bends, IEnumerable<FaceEdgeCm> baseFaceEdges)
    {
        ArgumentNullException.ThrowIfNull(bends);
        ArgumentNullException.ThrowIfNull(baseFaceEdges);

        double nLength = Math.Sqrt(nx * nx + ny * ny + nz * nz);
        if (!double.IsFinite(nLength) || nLength < Tolerance)
            return null;
        (double X, double Y, double Z) n = (nx / nLength, ny / nLength, nz / nLength);

        // Group the usable bends by direction. A bend whose axis runs along the base normal cannot be looked along.
        var groups = new List<(double X, double Y, double Z, int Count, double Length)>();
        foreach (BendAxisCm bend in bends)
        {
            double length = Math.Sqrt(bend.DirX * bend.DirX + bend.DirY * bend.DirY + bend.DirZ * bend.DirZ);
            if (!double.IsFinite(length) || length < Tolerance)
                continue;
            (double X, double Y, double Z) a = (bend.DirX / length, bend.DirY / length, bend.DirZ / length);
            if (Sine(a, n) < ParallelSine)
                continue;

            double weight = double.IsFinite(bend.LengthCm) && bend.LengthCm > 0 ? bend.LengthCm : 0;
            int found = groups.FindIndex(g => Sine((g.X, g.Y, g.Z), a) < ParallelSine);
            if (found < 0)
                groups.Add((a.X, a.Y, a.Z, 1, weight));
            else
                groups[found] = (groups[found].X, groups[found].Y, groups[found].Z, groups[found].Count + 1, groups[found].Length + weight);
        }

        if (groups.Count == 0)
            return FaceViewRule.Choose(nx, ny, nz, baseFaceEdges);

        // Most bends win; a tie goes to the longest total length; the first found wins a further tie.
        var best = groups[0];
        foreach (var g in groups.Skip(1))
        {
            if (g.Count > best.Count || (g.Count == best.Count && g.Length > best.Length + Tolerance))
                best = g;
        }

        // The sign of an axis is arbitrary and flipping it mirrors the view, so the eye goes to the side toward +Z, else -Y, else +X.
        (double X, double Y, double Z) direction = (best.X, best.Y, best.Z);
        if (EyeIsOnWrongSide(direction))
            direction = (-direction.X, -direction.Y, -direction.Z);

        // Up is the base normal without its part along the viewing direction, so the base flange prints horizontal.
        double along = n.X * direction.X + n.Y * direction.Y + n.Z * direction.Z;
        double ux = n.X - along * direction.X;
        double uy = n.Y - along * direction.Y;
        double uz = n.Z - along * direction.Z;
        double uLength = Math.Sqrt(ux * ux + uy * uy + uz * uz);
        if (uLength < Tolerance)
            return FaceViewRule.Choose(nx, ny, nz, baseFaceEdges);

        return new FaceViewOrientation(direction.X, direction.Y, direction.Z, ux / uLength, uy / uLength, uz / uLength);
    }

    // True when the direction points toward -Z, or is level with Z and points toward +Y, or is level with both and points toward -X.
    private static bool EyeIsOnWrongSide((double X, double Y, double Z) d)
    {
        if (Math.Abs(d.Z) > Tolerance)
            return d.Z < 0;
        if (Math.Abs(d.Y) > Tolerance)
            return d.Y > 0;
        return d.X < 0;
    }

    // The sine of the angle between two unit vectors: the length of their cross product.
    private static double Sine((double X, double Y, double Z) a, (double X, double Y, double Z) b)
    {
        double x = a.Y * b.Z - a.Z * b.Y;
        double y = a.Z * b.X - a.X * b.Z;
        double z = a.X * b.Y - a.Y * b.X;
        return Math.Sqrt(x * x + y * y + z * z);
    }
}
