using InventorAddin.Core.Drawing;
using InventorAddin.Core.PartProperties;

namespace InventorAddin.Core.Materials;

/// <summary>
/// Fills in the Finish Spec of a part finished before task 211, which has Finish but no Finish Spec (Aidan, 2026-11-22,
/// task 217). The add-in runs it when Material and Finish opens, in Create Print and in Mass Update.
/// </summary>
public static class FinishSpecBackfill
{
    /// <summary>
    /// The Finish Spec write for a part, or null when nothing is to be written: Finish is blank, or Finish Spec is
    /// already set. The finish is found by name in <paramref name="finishList"/> (ignoring case and surrounding spaces)
    /// for its print name and thickness. A finish that is not in the list is built from the stored values, with
    /// <paramref name="storedFinishThickness"/> read as the mm text Finish Thickness holds (<c>0.013-0.025 mm</c> or
    /// <c>0.025 mm</c>); a blank or unreadable thickness gives the name alone.
    /// </summary>
    /// <param name="storedFinish">The part's Finish property, or null.</param>
    /// <param name="storedFinishThickness">The part's Finish Thickness property, or null.</param>
    /// <param name="storedFinishSpec">The part's Finish Spec property, or null.</param>
    /// <param name="finishList">The finishes of the finish list; empty when it could not be read.</param>
    /// <param name="units">The part's units.</param>
    public static PropertyWrite? For(
        string? storedFinish,
        string? storedFinishThickness,
        string? storedFinishSpec,
        IEnumerable<Finish> finishList,
        PrintUnits units)
    {
        ArgumentNullException.ThrowIfNull(finishList);

        var name = PropertyValues.NormaliseText(storedFinish);
        if (name.Length == 0 || PropertyValues.NormaliseText(storedFinishSpec).Length > 0)
        {
            return null;
        }

        var finish = finishList.FirstOrDefault(f => string.Equals(f.Name.Trim(), name, StringComparison.OrdinalIgnoreCase));
        var spec = finish is not null
            ? FinishProperties.SpecText(finish, units)
            : FromStored(name, storedFinishThickness, units);

        return spec.Length == 0 ? null : PropertyWrite.SetText(PartPropertyStorage.FinishSpec, spec);
    }

    private static string FromStored(string name, string? thicknessText, PrintUnits units)
    {
        var text = PropertyValues.NormaliseText(thicknessText);
        if (text.EndsWith("mm", StringComparison.OrdinalIgnoreCase))
        {
            text = text[..^2];
        }

        var parts = text.Split('-', StringSplitOptions.TrimEntries);
        return parts.Length switch
        {
            1 => FinishProperties.SpecText(name, parts[0], parts[0], units),
            2 => FinishProperties.SpecText(name, parts[0], parts[1], units),
            _ => name,
        };
    }
}
