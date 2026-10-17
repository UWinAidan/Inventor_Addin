namespace InventorAddin.Core.Holes;

/// <summary>
/// What the add-in says when it makes or changes a hole feature in Inventor (task 088), so the window and the log
/// word it the same way.
/// </summary>
public static class HoleFeatureMessages
{
    private const string CommandName = Branding.ShortName + " Hole";

    /// <summary>
    /// The hole was made but is not healthy (spike 081: a hole pointing out of the material cuts nothing, with no
    /// error).
    /// </summary>
    /// <param name="health">Inventor's health status, for example <c>kDriverLostHealth</c>.</param>
    public static string Unhealthy(string health) =>
        $"Inventor could not make this hole (its status is {Describe(health)}). Check the direction (Flip) and the " +
        "hole centres.";

    /// <summary>The hole was made but has no faces: it cuts nothing.</summary>
    public const string CutsNothing =
        "The hole cuts nothing. Check the direction (Flip) and that the hole centres are on the part.";

    /// <summary>Inventor refused the change outright.</summary>
    /// <param name="reason">The error Inventor gave, or null.</param>
    public static string Refused(string? reason)
    {
        var why = (reason ?? "").Trim().TrimEnd('.');
        return why.Length == 0
            ? "Inventor could not make this hole."
            : $"Inventor could not make this hole ({why}).";
    }

    /// <summary>A hole that ends at a face needs the face.</summary>
    public const string NoEndFace = "Pick the face the hole ends at.";

    /// <summary>A hole needs at least one centre.</summary>
    public const string NoCentres = "Pick at least one hole centre.";

    /// <summary>
    /// The hidden <c>_CreateTapInfo</c> failed, so the hole was made with Inventor's own tap drill (spike 081:
    /// Inventor then models the thread's minor diameter).
    /// </summary>
    /// <param name="designation">The thread, as Inventor names it.</param>
    /// <param name="reason">Why <c>_CreateTapInfo</c> failed, or null.</param>
    public static string TapDrillFallback(string designation, string? reason)
    {
        var why = (reason ?? "").Trim().TrimEnd('.');
        var detail = why.Length == 0 ? "" : $" ({why})";
        return $"{designation} is modelled at Inventor's minor diameter, not the workbook's tap drill: this Inventor " +
               $"did not accept the tap drill{detail}.";
    }

    /// <summary>The hole had to be made again (tapped to untapped, or a new thread) and could not keep its place.</summary>
    public const string RecreatedAtEnd =
        "The hole was made again and is now at the end of the browser; it could not keep its place.";

    /// <summary>A hole of a kind the add-in does not make (a spot face) read back as the nearest AWB hole.</summary>
    public const string SpotFaceNotOffered = "Inventor's spot face is not offered in " + CommandName + "; Seat is None.";

    private static string Describe(string health)
    {
        var text = (health ?? "").Trim();
        if (text.StartsWith('k') && text.EndsWith("Health", StringComparison.Ordinal) && text.Length > 7)
        {
            text = text[1..^6];
        }

        // kDriverLostHealth -> "driver lost".
        var words = new System.Text.StringBuilder();
        foreach (var c in text)
        {
            if (char.IsUpper(c) && words.Length > 0)
            {
                words.Append(' ');
            }

            words.Append(char.ToLowerInvariant(c));
        }

        return words.Length == 0 ? "unknown" : words.ToString();
    }
}
