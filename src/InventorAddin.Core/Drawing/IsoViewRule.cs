namespace InventorAddin.Core.Drawing;

/// <summary>
/// The rule for the iso view (task 243; Aidan, 2026-11-26): it follows the chosen front view. It looks at the corner at the front, top
/// and right of that view, so after choosing Left or Back the iso still shows the part from the corner at the top right of the front
/// view. For the standard front this is Inventor's top-right iso. Pure arithmetic; the add-in turns the orientation into a camera.
/// </summary>
public static class IsoViewRule
{
    private const double Tolerance = 1e-9;

    /// <summary>
    /// The iso orientation for a front view whose viewer direction is <c>f</c> and up is <c>u</c> (model space): the viewer is on
    /// <c>f + u + r</c> (normalised), where <c>r = u x f</c> is the sheet's right; up is <c>u</c> with the part along the viewing
    /// direction removed.
    /// </summary>
    public static FaceViewOrientation Choose(FaceViewOrientation front)
    {
        ArgumentNullException.ThrowIfNull(front);

        double rx = front.UpY * front.NormalZ - front.UpZ * front.NormalY;
        double ry = front.UpZ * front.NormalX - front.UpX * front.NormalZ;
        double rz = front.UpX * front.NormalY - front.UpY * front.NormalX;

        double nx = front.NormalX + front.UpX + rx;
        double ny = front.NormalY + front.UpY + ry;
        double nz = front.NormalZ + front.UpZ + rz;
        double nLength = Math.Sqrt(nx * nx + ny * ny + nz * nz);
        if (!double.IsFinite(nLength) || nLength < Tolerance)
            throw new ArgumentException("The front view's direction and up do not give an iso corner.", nameof(front));
        nx /= nLength;
        ny /= nLength;
        nz /= nLength;

        double along = front.UpX * nx + front.UpY * ny + front.UpZ * nz;
        double ux = front.UpX - along * nx;
        double uy = front.UpY - along * ny;
        double uz = front.UpZ - along * nz;
        double uLength = Math.Sqrt(ux * ux + uy * uy + uz * uz);
        if (!double.IsFinite(uLength) || uLength < Tolerance)
            throw new ArgumentException("The front view's up direction is along the iso direction.", nameof(front));

        return new FaceViewOrientation(nx, ny, nz, ux / uLength, uy / uLength, uz / uLength);
    }

    /// <summary>True when the iso is Inventor's own top-right iso, which the add-in then uses unchanged (the front is the standard front).</summary>
    public static bool IsStandardIso(FaceViewOrientation? front) => front == null || FrontViewRule.IsStandardFront(front);
}
