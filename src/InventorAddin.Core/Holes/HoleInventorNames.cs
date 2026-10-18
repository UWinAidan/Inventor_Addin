namespace InventorAddin.Core.Holes;

/// <summary>
/// The appearances the add-in makes in a part to colour dowel and precision holes by fit (spec 09, "Dowel and
/// precision holes"; spike 081 section 5): one per fit, found again by its display name, so a part keeps one
/// appearance per fit however many holes use it.
/// </summary>
public static class HoleFitAppearances
{
    /// <summary>
    /// The appearance's display name for a fit: <c>AWB Press fit</c>. Inventor's <c>Assets.Add</c> takes this as
    /// the display name; its name argument is marked reserved, so the add-in finds the appearance by this name.
    /// </summary>
    public static string DisplayName(PrecisionFit fit) => $"{Branding.ShortName} {fit} fit";

    /// <summary>The name passed in <c>Assets.Add</c>'s reserved argument, as the spike did: <c>Awb_HoleFit_Press</c>.</summary>
    public static string Name(PrecisionFit fit) => $"{Branding.IdPrefix}_HoleFit_{fit}";

    /// <summary>The fit whose appearance has this display name, ignoring case and surrounding spaces; null when none.</summary>
    public static PrecisionFit? FitFor(string? displayName)
    {
        foreach (var fit in Enum.GetValues<PrecisionFit>())
        {
            if (string.Equals(displayName?.Trim(), DisplayName(fit), StringComparison.OrdinalIgnoreCase))
            {
                return fit;
            }
        }

        return null;
    }

    /// <summary>True when <paramref name="displayName"/> is one of the fit appearances.</summary>
    public static bool IsFitAppearance(string? displayName) => FitFor(displayName) is not null;
}

/// <summary>
/// The appearance the AWB Hole preview paints on every face of the preview hole while the window is open (task 101;
/// spec 09, "Live 3D preview"): red and partly see-through, as Inventor's Hole shows its preview. It is made in the
/// session's preview transaction, which is aborted before every other change and on every way out, so it never
/// reaches the Undo list and never stays in the part. Its name is not a fit appearance's, so it never counts as a fit
/// colour (task 096's <see cref="HoleSessionRules.ResetsFaceAppearances"/>).
/// </summary>
public static class HolePreviewAppearance
{
    /// <summary>The colour: RGB 220, 40, 40 (Default, task 101).</summary>
    public static HoleColour Colour { get; } = new(220, 40, 40);

    /// <summary>How opaque the preview is, 0 to 1: about half (Default, task 101).</summary>
    public const double Opacity = 0.5;

    /// <summary>The display name the add-in gives the appearance and finds it by: <c>AWB Hole preview</c>.</summary>
    public static string DisplayName => $"{Branding.ShortName} Hole preview";

    /// <summary>The name passed in <c>Assets.Add</c>'s reserved argument: <c>Awb_HolePreview</c>.</summary>
    public static string Name => $"{Branding.IdPrefix}_HolePreview";

    /// <summary>
    /// The Generic appearance value that holds how see-through it is (task 101's spike: a float, 0 opaque to 1 clear).
    /// </summary>
    public const string TransparencyValueName = "generic_transparency";

    /// <summary>The Generic appearance value that holds the colour (spike 081).</summary>
    public const string ColourValueName = "generic_diffuse";

    /// <summary>
    /// The <see cref="TransparencyValueName"/> value for an opacity: 1 minus the opacity, held to 0..1 (an opacity
    /// that is not a number gives fully opaque).
    /// </summary>
    public static double Transparency(double opacity) =>
        double.IsNaN(opacity) ? 0 : Math.Clamp(1 - opacity, 0, 1);

    /// <summary>True when <paramref name="displayName"/> is the preview appearance, ignoring case and surrounding spaces.</summary>
    public static bool Is(string? displayName) =>
        string.Equals(displayName?.Trim(), DisplayName, StringComparison.OrdinalIgnoreCase);
}

/// <summary>The thread names Inventor's hidden <c>_CreateTapInfo</c> takes (spike 081).</summary>
public static class HoleThreadNames
{
    /// <summary>
    /// The nominal size description for a designation as Inventor's thread table names it: <c>M6x1</c> gives
    /// <c>6</c> (as the spike passed it), <c>1/4-20 UNC</c> gives <c>1/4</c>, <c>10-32 UNF</c> gives <c>10</c>.
    /// The imperial form has not been tried in Inventor; if Inventor refuses it the add-in falls back to
    /// <c>CreateTapInfo</c>.
    /// </summary>
    public static string NominalSize(string designation, HoleUnits units)
    {
        ArgumentNullException.ThrowIfNull(designation);

        var text = designation.Trim();
        if (units == HoleUnits.Metric)
        {
            if (text.StartsWith('M') || text.StartsWith('m'))
            {
                text = text[1..];
            }

            var x = text.IndexOfAny(['x', 'X']);
            return (x >= 0 ? text[..x] : text).Trim();
        }

        var dash = text.IndexOf('-');
        var size = dash >= 0 ? text[..dash] : text.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? text;
        return size.Trim().TrimStart('#');
    }

    /// <summary>
    /// Whether the tap drill a hole reports (<c>HoleTapInfo.TapDrillDiameter</c>) is <paramref name="thread"/>'s.
    /// Inventor's type and units for that value have not been tried (task 088), so a number matches when it equals
    /// the tap drill in table units or in centimetres, and text matches by its unit when it has one (<c>5.2 mm</c>,
    /// <c>0.201 in</c>, <c>0.52 cm</c>), else as a number. Null when the value cannot be read as a length, which the
    /// add-in treats as "no change".
    /// </summary>
    public static bool? TapDrillMatches(object? reported, HoleThread thread)
    {
        ArgumentNullException.ThrowIfNull(thread);

        var tableCm = thread.TableUnits == HoleUnits.Metric
            ? thread.TapDrillInTableUnits / 10
            : thread.TapDrillInTableUnits * 2.54;

        static bool Near(double a, double b) => Math.Abs(a - b) < 1e-6;

        switch (reported)
        {
            case double d:
                return Near(d, thread.TapDrillInTableUnits) || Near(d, tableCm);
            case float f:
                return Near(f, thread.TapDrillInTableUnits) || Near(f, tableCm);
            case decimal m:
                return Near((double)m, thread.TapDrillInTableUnits) || Near((double)m, tableCm);
            case int i:
                return Near(i, thread.TapDrillInTableUnits) || Near(i, tableCm);
            case string text:
                var match = System.Text.RegularExpressions.Regex.Match(
                    text.Trim(), @"^([-+]?\d*\.?\d+(?:[eE][-+]?\d+)?)\s*(mm|cm|in|"")?$");
                if (!match.Success
                    || !double.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var number))
                {
                    return null;
                }

                return match.Groups[2].Value switch
                {
                    "mm" => Near(number / 10, tableCm),
                    "cm" => Near(number, tableCm),
                    "in" or "\"" => Near(number * 2.54, tableCm),
                    _ => Near(number, thread.TapDrillInTableUnits) || Near(number, tableCm),
                };
            default:
                return null;
        }
    }
}
