namespace InventorAddin.Core.PartProperties;

/// <summary>
/// Whether the Part Properties window shows a file's Part Number or <see cref="NotAssignedText"/>
/// (spec 08, "Part number display"). Inventor reports the file name as the Part Number when none has been set,
/// so a Part Number equal to the file name without its extension counts as no number.
/// </summary>
/// <remarks>
/// A display rule only: nothing is written and the Part Number property is not changed. Spec 02 replaces this
/// rule with its own definition of "has a number" when numbering exists.
/// For a never-saved file there is no file name to compare against, so only a blank Part Number counts as not
/// assigned (a default; spec 08, open question 5).
/// </remarks>
public static class PartNumberDisplay
{
    public const string NotAssignedText = "Not assigned";

    /// <summary>
    /// False when <paramref name="partNumber"/> is null or blank, or equals <see cref="PartName.FromFileName"/> of
    /// <paramref name="fullFileName"/>, ignoring case and surrounding spaces on both sides. True otherwise.
    /// </summary>
    public static bool IsAssigned(string? partNumber, string? fullFileName)
    {
        var number = PropertyValues.NormaliseText(partNumber);
        if (number.Length == 0)
        {
            return false;
        }

        var name = PartName.FromFileName(fullFileName).Trim();
        if (name.Length == 0)
        {
            return true;
        }

        return !string.Equals(number, name, StringComparison.OrdinalIgnoreCase);
    }
}
