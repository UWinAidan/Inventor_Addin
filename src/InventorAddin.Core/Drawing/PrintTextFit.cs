namespace InventorAddin.Core.Drawing;

/// <summary>
/// Estimates how wide Tahoma text is, so the print template's layout (task 215) and the title block's values (tasks 216 and 218) can be
/// checked against their cells without Inventor. Sizes are the em size in millimetres, the same convention as the mockup's SVG
/// <c>font-size</c> (<c>docs/ui-mockups/title-block.html</c>); whether Inventor's FontSize override means the em or the cap height is
/// for the builder to confirm (task 215 follow-up).
/// </summary>
/// <remarks>
/// Advance widths are in em, measured from the Tahoma and Tahoma Bold fonts installed with Windows (GDI+, 1000 px em, typographic
/// string format, no kerning) for the characters the template uses. A character that is not in the table counts as
/// <see cref="FallbackAdvanceEm"/>, wider than any capital, so an unknown character can only make the estimate too wide.
/// </remarks>
public static class PrintTextFit
{
    /// <summary>Tahoma's capital height as a fraction of the em.</summary>
    public const double CapHeightEm = 0.727;

    /// <summary>The advance used for a character not in the tables: wider than any capital but W and M.</summary>
    public const double FallbackAdvanceEm = 0.75;

    private const string Chars =
        " ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789.,:;-/()±°µ_+%&";

    private static readonly double[] Regular =
    {
        0.313,
        0.600, 0.589, 0.601, 0.678, 0.561, 0.521, 0.667, 0.675, 0.373, 0.417, 0.588, 0.498, 0.771,
        0.667, 0.708, 0.551, 0.708, 0.621, 0.557, 0.584, 0.656, 0.597, 0.902, 0.581, 0.576, 0.559,
        0.525, 0.553, 0.461, 0.553, 0.526, 0.318, 0.553, 0.558, 0.229, 0.282, 0.498, 0.229, 0.840,
        0.558, 0.543, 0.553, 0.553, 0.360, 0.446, 0.334, 0.558, 0.498, 0.742, 0.495, 0.498, 0.444,
        0.546, 0.546, 0.546, 0.546, 0.546, 0.546, 0.546, 0.546, 0.546, 0.546,
        0.303, 0.303, 0.354, 0.354, 0.363, 0.382, 0.383, 0.383, 0.728, 0.471, 0.568, 0.546, 0.728, 0.977, 0.674,
    };

    private static readonly double[] Bold =
    {
        0.293,
        0.685, 0.686, 0.667, 0.757, 0.615, 0.581, 0.745, 0.764, 0.483, 0.500, 0.696, 0.572, 0.893,
        0.771, 0.770, 0.657, 0.770, 0.726, 0.633, 0.612, 0.739, 0.675, 1.028, 0.685, 0.670, 0.623,
        0.599, 0.632, 0.527, 0.629, 0.594, 0.382, 0.629, 0.640, 0.302, 0.363, 0.603, 0.302, 0.954,
        0.640, 0.617, 0.629, 0.629, 0.434, 0.515, 0.416, 0.640, 0.579, 0.890, 0.604, 0.576, 0.526,
        0.637, 0.637, 0.637, 0.637, 0.637, 0.637, 0.637, 0.637, 0.637, 0.637,
        0.313, 0.313, 0.363, 0.363, 0.431, 0.577, 0.454, 0.454, 0.818, 0.520, 0.651, 0.637, 0.818, 1.199, 0.781,
    };

    private static double AdvanceEm(char c, bool bold)
    {
        int i = Chars.IndexOf(c);
        return i < 0 ? FallbackAdvanceEm : (bold ? Bold : Regular)[i];
    }

    /// <summary>The estimated width of a single line of Tahoma text, in millimetres.</summary>
    public static double WidthMm(string text, double sizeMm, bool bold = false)
    {
        double em = 0;
        foreach (char c in text)
            em += AdvanceEm(c, bold);
        return em * sizeMm;
    }

    /// <summary>The height of Tahoma's capitals at this size, in millimetres.</summary>
    public static double CapHeightMm(double sizeMm) => CapHeightEm * sizeMm;

    /// <summary>
    /// Where a text's origin goes so its baseline lands on <paramref name="baselineYMm"/> when the box is justified to the upper edge
    /// (spike 218: Inventor's lower justification puts the bottom of the actual letters at the origin, so descenders lift the text;
    /// upper justification puts the origin a cap height above the baseline whatever the letters, and the baseline itself cannot be
    /// requested). The size is the em, as for <see cref="CapHeightMm"/>.
    /// </summary>
    public static double UpperOriginYMm(double baselineYMm, double sizeMm) => baselineYMm + CapHeightMm(sizeMm);

    /// <summary>
    /// Breaks the text into lines at spaces so that each is at most <paramref name="maxWidthMm"/> wide. A single word wider than that
    /// stays on a line of its own.
    /// </summary>
    public static IReadOnlyList<string> Wrap(string text, double sizeMm, double maxWidthMm, bool bold = false)
    {
        var lines = new List<string>();
        string current = string.Empty;
        foreach (string word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            string candidate = current.Length == 0 ? word : current + " " + word;
            if (current.Length == 0 || WidthMm(candidate, sizeMm, bold) <= maxWidthMm)
            {
                current = candidate;
            }
            else
            {
                lines.Add(current);
                current = word;
            }
        }
        if (current.Length > 0)
            lines.Add(current);
        return lines;
    }
}
