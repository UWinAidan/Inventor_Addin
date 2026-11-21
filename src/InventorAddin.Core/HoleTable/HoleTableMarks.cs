namespace InventorAddin.Core.HoleTable;

/// <summary>One circle the view draws at a hole's centre, as the add-in reads it: its radius on the sheet and whether it is a thread edge.</summary>
public readonly record struct HoleCircle(double RadiusCm, bool IsThread);

/// <summary>
/// What the wizard puts on the view beside the tables (task 165, spike 160 rounds 3 to 7): the hole's tag, a precision
/// mark on precision holes, and the one AWB centre mark every hole gets. The names, the centre mark's values and the
/// placement rules are here; the add-in reads and draws.
/// </summary>
public static class HoleTableMarks
{
    /// <summary>The one centre mark style every hole uses, kept in the drawing (Aidan, spike 160 round 7).</summary>
    public const string CentreMarkStyleName = Branding.ShortName + " Centre Mark";

    /// <summary>The sketched symbol definition of a hole tag: one prompted text box at the origin.</summary>
    public const string TagDefinitionName = Branding.ShortName + "_HoleTag";

    /// <summary>The sketched symbol definition of a precision mark: a circle of radius 1 with its top right quadrant filled.</summary>
    public const string PrecisionMarkDefinitionName = Branding.ShortName + "_PrecisionMark";

    /// <summary>The text of the tag's prompt, so the symbol shows a tag until a value is given.</summary>
    public const string TagPromptText = "<Prompt>TAG</Prompt>";

    /// <summary>The attribute set on every tag, precision mark and centre mark the wizard places. Update finds and replaces them by it (task 166).</summary>
    public const string AttributeSetName = Branding.ShortName + "_HoleMark";

    /// <summary>The hole's <see cref="HoleTableHole.Id"/>.</summary>
    public const string HoleIdAttributeName = "HoleId";

    /// <summary>The view's key (task 163), because two views of one part share their hole ids.</summary>
    public const string ViewKeyAttributeName = "ViewKey";

    /// <summary>What the object is: <see cref="TagRole"/>, <see cref="PrecisionMarkRole"/> or <see cref="CentreMarkRole"/>.</summary>
    public const string RoleAttributeName = "Role";

    public const string TagRole = "Tag";
    public const string PrecisionMarkRole = "PrecisionMark";
    public const string CentreMarkRole = "CentreMark";

    /// <summary>
    /// Aidan's rule (round 7): the centre mark's lines run continuously through the hole and end 2 mm on paper past the
    /// outermost visible circle. The style has no gap (0 is refused by Inventor, so a micron), a cross too small to
    /// show, and extension lines 2 mm long. Set on the style every time, so a changed style is put right.
    /// </summary>
    public const double CentreMarkGapCm = 0.0001;

    /// <summary>The cross's size; tiny, so it never reaches past the extension lines of a small hole.</summary>
    public const double CentreMarkSizeCm = 0.01;

    /// <summary>How far the lines end past the circle. Spike 160 measured this property, not <see cref="CentreMarkExtensionLengthCm"/>, setting it.</summary>
    public const double CentreMarkOvershootCm = 0.2;

    /// <summary>Set to the same 2 mm as <see cref="CentreMarkOvershootCm"/>.</summary>
    public const double CentreMarkExtensionLengthCm = 0.2;

    /// <summary>The gap between a tag and the outermost circle, as a share of the tag's text height.</summary>
    public const double TagGapTextHeights = 0.5;

    /// <summary>Whether a hole in this table gets the precision mark: the hole's place in the Precision table, so a swap moves the mark with the hole (Default, spec 04).</summary>
    public static bool HasPrecisionMark(HoleTableKind table) => table == HoleTableKind.Precision;

    /// <summary>
    /// The index of the outermost circle, the largest radius and thread edges included: the seat, else the thread
    /// circle, else the hole. -1 when there are none. A tie keeps the first.
    /// </summary>
    public static int OutermostIndex(IReadOnlyList<HoleCircle> circles)
    {
        ArgumentNullException.ThrowIfNull(circles);
        var best = -1;
        for (var i = 0; i < circles.Count; i++)
        {
            if (best < 0 || circles[i].RadiusCm > circles[best].RadiusCm)
            {
                best = i;
            }
        }

        return best;
    }

    /// <summary>
    /// The index of the hole's own circle: the smallest that is not a thread edge (a tapped hole's thread circle is
    /// larger than its drill circle, a seat is larger than the hole); else the smallest of all. -1 when there are none.
    /// The precision mark's size is this circle's radius.
    /// </summary>
    public static int HoleIndex(IReadOnlyList<HoleCircle> circles)
    {
        ArgumentNullException.ThrowIfNull(circles);
        var best = -1;
        for (var i = 0; i < circles.Count; i++)
        {
            if (best < 0 || Smaller(circles[i], circles[best]))
            {
                best = i;
            }
        }

        return best;

        static bool Smaller(HoleCircle a, HoleCircle b) =>
            a.IsThread != b.IsThread ? !a.IsThread : a.RadiusCm < b.RadiusCm;
    }

    /// <summary>The radius that must stay clear of the tag: the outermost circle, or the precision mark if that is larger. The mark's radius is the hole's.</summary>
    public static double ClearRadiusCm(double outermostRadiusCm, bool hasPrecisionMark, double holeRadiusCm) =>
        hasPrecisionMark ? Math.Max(outermostRadiusCm, holeRadiusCm) : outermostRadiusCm;

    /// <summary>
    /// Where to place the tag symbol (round 7): the symbol lands centred on its point, so this is the tag's centre. Its
    /// lower left corner sits <see cref="TagGapTextHeights"/> x the text height outside the clear radius, at 45 degrees
    /// up and right of the hole centre.
    /// </summary>
    public static SheetPoint TagPoint(SheetPoint centre, double clearRadiusCm, double textHeightCm, double tagWidthCm)
    {
        var d = (clearRadiusCm + TagGapTextHeights * textHeightCm) / Math.Sqrt(2);
        return new SheetPoint(centre.XCm + d + tagWidthCm / 2, centre.YCm + d + textHeightCm / 2);
    }

    /// <summary>The marks line of the status message; empty when nothing was marked and nothing failed.</summary>
    public static string Summary(int holesMarked, int holesSkipped)
    {
        if (holesMarked == 0 && holesSkipped == 0)
        {
            return "";
        }

        var text = holesMarked == 1 ? "Marked 1 hole on the view." : $"Marked {holesMarked} holes on the view.";
        if (holesSkipped > 0)
        {
            text += holesSkipped == 1
                ? " 1 hole could not be marked (see the log)."
                : $" {holesSkipped} holes could not be marked (see the log).";
        }

        return text;
    }
}
