namespace InventorAddin.Core.Drawing;

/// <summary>The Front view dropdown of the Initial Views window (task 233).</summary>
public enum FrontViewChoice
{
    /// <summary>The direction with the largest projected area, longest side horizontal. A sheet metal part uses its base face.</summary>
    Auto,
    Front,
    Back,
    Top,
    Bottom,
    Left,
    Right,
}

/// <summary>
/// The front view the plan is made for: the direction to look from (null for a sheet metal part on Auto, whose folded front looks at the
/// base face) and the model's size as that view sees it.
/// </summary>
/// <param name="Orientation">The viewer's direction and the up direction, in model space; null means "use the base face rule".</param>
/// <param name="Box">The width, height and depth of the front view, for <see cref="InitialViewsPlan.Plan"/>.</param>
public sealed record FrontViewSelection(FaceViewOrientation? Orientation, InitialViewsBoxCm Box);

/// <summary>
/// Which way the front view looks (task 233; Aidan, 2026-11-23). The six named choices are Inventor's standard views of a Y-up model
/// (spike 225: the standard front is the viewer on +Z with +Y up; a view above it looks from +Y, a view to its right from +X). Auto
/// looks along the model's shortest side, so the front view has the largest projected area, with the longer of the other two sides
/// horizontal. Pure arithmetic on the model's extents; the add-in turns the orientation into a camera.
/// </summary>
public static class FrontViewRule
{
    private const double AreaTolerance = 1e-6;

    public static readonly FaceViewOrientation FrontOrientation = new(0, 0, 1, 0, 1, 0);
    public static readonly FaceViewOrientation BackOrientation = new(0, 0, -1, 0, 1, 0);
    public static readonly FaceViewOrientation TopOrientation = new(0, 1, 0, 0, 0, -1);
    public static readonly FaceViewOrientation BottomOrientation = new(0, -1, 0, 0, 0, 1);
    public static readonly FaceViewOrientation LeftOrientation = new(-1, 0, 0, 0, 1, 0);
    public static readonly FaceViewOrientation RightOrientation = new(1, 0, 0, 0, 1, 0);

    /// <summary>The words the window shows for a choice.</summary>
    public static string LabelFor(FrontViewChoice choice) => choice.ToString();

    /// <summary>True when the orientation is the standard front view (the add-in then uses Inventor's own front orientation).</summary>
    public static bool IsStandardFront(FaceViewOrientation orientation)
    {
        ArgumentNullException.ThrowIfNull(orientation);
        return Same(orientation, FrontOrientation);
    }

    /// <summary>
    /// The front view for a model: its orientation and the box the plan sees. <paramref name="extents"/> is the model's range box
    /// (width is X, height is Y, depth is Z). A sheet metal part on Auto has no orientation here (null): its folded front looks at the
    /// base face (task 229), and its box stays the range box, which measuring replaces. A sheet metal part on any other choice looks
    /// along that direction.
    /// </summary>
    public static FrontViewSelection Select(FrontViewChoice choice, InitialViewModelKind model, InitialViewsBoxCm extents)
    {
        ArgumentNullException.ThrowIfNull(extents);
        if (choice == FrontViewChoice.Auto && model == InitialViewModelKind.SheetMetalPart)
            return new FrontViewSelection(null, extents);

        FaceViewOrientation orientation = choice == FrontViewChoice.Auto ? Auto(extents) : Named(choice);
        return new FrontViewSelection(orientation, BoxFor(orientation, extents));
    }

    /// <summary>The standard orientation of a named choice. Auto is not named: use <see cref="Auto"/>.</summary>
    public static FaceViewOrientation Named(FrontViewChoice choice) => choice switch
    {
        FrontViewChoice.Front => FrontOrientation,
        FrontViewChoice.Back => BackOrientation,
        FrontViewChoice.Top => TopOrientation,
        FrontViewChoice.Bottom => BottomOrientation,
        FrontViewChoice.Left => LeftOrientation,
        FrontViewChoice.Right => RightOrientation,
        _ => throw new ArgumentOutOfRangeException(nameof(choice), choice, "Auto has no fixed direction."),
    };

    /// <summary>
    /// Looks along the shortest of the three sides (the other two make the largest area; a tie prefers Z, then Y, then X, so the
    /// standard front wins) with the longer of the other two horizontal (a tie keeps the standard view's horizontal). The viewer is
    /// on the positive side of the axis. Where the horizontal is not the standard view's, up is the positive direction of the third axis.
    /// </summary>
    public static FaceViewOrientation Auto(InitialViewsBoxCm extents)
    {
        ArgumentNullException.ThrowIfNull(extents);
        double x = extents.WidthCm, y = extents.HeightCm, z = extents.DepthCm;

        double areaAlongZ = x * y, areaAlongY = x * z, areaAlongX = y * z;
        double best = Math.Max(areaAlongZ, Math.Max(areaAlongY, areaAlongX));
        bool Ties(double area) => area >= best * (1 - AreaTolerance);

        if (Ties(areaAlongZ))
            return x >= y ? FrontOrientation : new FaceViewOrientation(0, 0, 1, 1, 0, 0);
        if (Ties(areaAlongY))
            return x >= z ? TopOrientation : new FaceViewOrientation(0, 1, 0, 1, 0, 0);
        return z >= y ? RightOrientation : new FaceViewOrientation(1, 0, 0, 0, 0, 1);
    }

    /// <summary>The width, height and depth of a front view looking by <paramref name="orientation"/> (an axis-aligned one), from the model's extents.</summary>
    public static InitialViewsBoxCm BoxFor(FaceViewOrientation orientation, InitialViewsBoxCm extents)
    {
        ArgumentNullException.ThrowIfNull(orientation);
        ArgumentNullException.ThrowIfNull(extents);

        // right = up x n, the direction a view placed to the right looks from (spike 225).
        double rx = orientation.UpY * orientation.NormalZ - orientation.UpZ * orientation.NormalY;
        double ry = orientation.UpZ * orientation.NormalX - orientation.UpX * orientation.NormalZ;
        double rz = orientation.UpX * orientation.NormalY - orientation.UpY * orientation.NormalX;

        double Along(double dx, double dy, double dz) =>
            Math.Abs(dx) * extents.WidthCm + Math.Abs(dy) * extents.HeightCm + Math.Abs(dz) * extents.DepthCm;

        return new InitialViewsBoxCm(
            Along(rx, ry, rz),
            Along(orientation.UpX, orientation.UpY, orientation.UpZ),
            Along(orientation.NormalX, orientation.NormalY, orientation.NormalZ));
    }

    private static bool Same(FaceViewOrientation a, FaceViewOrientation b) =>
        Math.Abs(a.NormalX - b.NormalX) < 1e-9 && Math.Abs(a.NormalY - b.NormalY) < 1e-9 && Math.Abs(a.NormalZ - b.NormalZ) < 1e-9 &&
        Math.Abs(a.UpX - b.UpX) < 1e-9 && Math.Abs(a.UpY - b.UpY) < 1e-9 && Math.Abs(a.UpZ - b.UpZ) < 1e-9;
}
