namespace InventorAddin.Core.Drawing;

/// <summary>A straight edge of a face: its direction (any length, either sign) and its length, in cm.</summary>
public sealed record FaceEdgeCm(double DirX, double DirY, double DirZ, double LengthCm);

/// <summary>
/// How to look at a face: the viewer's direction (the face's outward normal) and the model-space direction that points up on the
/// sheet. The add-in turns these into a camera (spike 225). The vectors are unit length, in model space.
/// </summary>
public sealed record FaceViewOrientation(
    double NormalX, double NormalY, double NormalZ,
    double UpX, double UpY, double UpZ);

/// <summary>
/// The rule for a sheet metal part's folded front view (task 229): look straight at the base face, the longest straight edge of
/// that face horizontal. The sign of an edge is arbitrary and flipping it turns the view 180 degrees (spike 225), so the up
/// direction is chosen to point toward the model's +Z, or +Y when it is level with Z, or +X when it is level with both.
/// </summary>
public static class FaceViewRule
{
    private const double Tolerance = 1e-9;

    /// <summary>
    /// The orientation for a face with the given outward normal and straight edges, or null when the normal is zero or no edge
    /// is usable (none, all zero length, or all parallel to the normal).
    /// </summary>
    public static FaceViewOrientation? Choose(double nx, double ny, double nz, IEnumerable<FaceEdgeCm> edges)
    {
        ArgumentNullException.ThrowIfNull(edges);
        if (!TryNormalise(nx, ny, nz, out (double X, double Y, double Z) n))
            return null;

        // The longest edge that lies in the face's plane; the first one wins a tie.
        (double X, double Y, double Z)? horizontal = null;
        double best = 0;
        foreach (FaceEdgeCm edge in edges)
        {
            if (!double.IsFinite(edge.LengthCm) || !(edge.LengthCm > best + Tolerance))
                continue;
            if (!TryNormalise(edge.DirX, edge.DirY, edge.DirZ, out (double X, double Y, double Z) e))
                continue;

            // Remove any part along the normal, so an edge a little off the plane still gives a true horizontal.
            double along = Dot(e, n);
            if (!TryNormalise(e.X - along * n.X, e.Y - along * n.Y, e.Z - along * n.Z, out (double X, double Y, double Z) flat))
                continue;

            horizontal = flat;
            best = edge.LengthCm;
        }

        if (horizontal is null)
            return null;

        (double X, double Y, double Z) up = Cross(n, horizontal.Value);
        if (PointsAway(up))
            up = (-up.X, -up.Y, -up.Z);

        return new FaceViewOrientation(n.X, n.Y, n.Z, up.X, up.Y, up.Z);
    }

    // True when the up vector points toward -Z, or is level with Z and points toward -Y, or is level with both and points toward -X.
    private static bool PointsAway((double X, double Y, double Z) up)
    {
        if (Math.Abs(up.Z) > Tolerance)
            return up.Z < 0;
        if (Math.Abs(up.Y) > Tolerance)
            return up.Y < 0;
        return up.X < 0;
    }

    private static bool TryNormalise(double x, double y, double z, out (double X, double Y, double Z) unit)
    {
        double length = Math.Sqrt(x * x + y * y + z * z);
        if (!double.IsFinite(length) || length < Tolerance)
        {
            unit = default;
            return false;
        }

        unit = (x / length, y / length, z / length);
        return true;
    }

    private static double Dot((double X, double Y, double Z) a, (double X, double Y, double Z) b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

    private static (double X, double Y, double Z) Cross((double X, double Y, double Z) a, (double X, double Y, double Z) b) =>
        (a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
}
