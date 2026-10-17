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
