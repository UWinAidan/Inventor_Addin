using InventorAddin.Core.PartProperties;

namespace InventorAddin.Core.Materials;

/// <summary>
/// Turns a chosen finish into the iProperties stored on the part (spec 03, "Decided"): <c>Finish</c>,
/// <c>Finish Code</c>, <c>Finish Thickness</c> and <c>Finish Note</c>, all custom text properties.
/// </summary>
public static class FinishProperties
{
    /// <summary>
    /// The four writes, in the order Finish, Finish Code, Finish Thickness, Finish Note. Each is Set to its value,
    /// or Removed when the value is blank, the same way Part Properties removes a cleared custom property.
    /// No finish (null) removes all four.
    /// </summary>
    public static IReadOnlyList<PropertyWrite> For(Finish? finish) =>
        new[]
        {
            Write(PartPropertyStorage.Finish, finish?.Name),
            Write(PartPropertyStorage.FinishCode, finish?.Code),
            Write(PartPropertyStorage.FinishThickness, finish is null ? null : ThicknessText(finish)),
            Write(PartPropertyStorage.FinishNote, finish?.PrintNote),
        }.AsReadOnly();

    /// <summary>
    /// The Finish Thickness text (Default, spec 03 question 13): <c>0.013-0.025 mm</c> using the numbers as written;
    /// one value, <c>0.025 mm</c>, when the minimum and maximum are equal; <c>""</c> when both are zero.
    /// Values that do not read as numbers are written as they are.
    /// </summary>
    public static string ThicknessText(Finish finish)
    {
        ArgumentNullException.ThrowIfNull(finish);

        var minText = finish.ThicknessMinMm.Trim();
        var maxText = finish.ThicknessMaxMm.Trim();

        if (FinishListCsv.TryParseThickness(minText, out var min) && FinishListCsv.TryParseThickness(maxText, out var max))
        {
            if (min == 0 && max == 0)
            {
                return "";
            }

            if (min == max)
            {
                return $"{maxText} mm";
            }
        }

        return $"{minText}-{maxText} mm";
    }

    private static PropertyWrite Write(PropertyLocation location, string? value)
    {
        var text = PropertyValues.NormaliseText(value);
        return text.Length == 0 ? PropertyWrite.Remove(location) : PropertyWrite.SetText(location, text);
    }
}
