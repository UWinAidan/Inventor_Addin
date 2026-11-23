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
    /// already set. A Finish Spec that is set is only replaced (the write has <see cref="PropertyWrite.IsRefresh"/>)
    /// when the finish is in the list, has a print name different from its name, and the stored text is the add-in's own
    /// spec built from the long name (task 224); hand-written text is never touched. The finish is found by name in <paramref name="finishList"/> (ignoring case and surrounding spaces)
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
        if (name.Length == 0)
        {
            return null;
        }

        var finish = finishList.FirstOrDefault(f => string.Equals(f.Name.Trim(), name, StringComparison.OrdinalIgnoreCase));
        var storedSpec = PropertyValues.NormaliseText(storedFinishSpec);
        if (storedSpec.Length > 0)
        {
            return finish is null ? null : Refresh(finish, storedSpec, units);
        }

        var spec = finish is not null
            ? FinishProperties.SpecText(finish, units)
            : FromStored(name, storedFinishThickness, units);

        return spec.Length == 0 ? null : PropertyWrite.SetText(PartPropertyStorage.FinishSpec, spec);
    }

    /// <summary>
    /// The refresh of a Finish Spec the add-in wrote from the finish's long name before it had a print name (task 224):
    /// the stored text is the long-name spec in either units, with or without the thickness, and today's spec (from the
    /// print name) differs. Anything else is someone's own text, or already current, and gives null.
    /// </summary>
    private static PropertyWrite? Refresh(Finish finish, string storedSpec, PrintUnits units)
    {
        var longName = finish.Name.Trim();
        if (string.Equals(finish.SpecName.Trim(), longName, StringComparison.Ordinal))
        {
            return null;
        }

        var today = FinishProperties.SpecText(finish, units);
        if (string.Equals(storedSpec, today, StringComparison.Ordinal))
        {
            return null;
        }

        var old = new[]
        {
            longName,
            FinishProperties.SpecText(longName, finish.ThicknessMinMm, finish.ThicknessMaxMm, PrintUnits.Metric),
            FinishProperties.SpecText(longName, finish.ThicknessMinMm, finish.ThicknessMaxMm, PrintUnits.Imperial),
        };

        return old.Contains(storedSpec, StringComparer.Ordinal) && today.Length > 0
            ? PropertyWrite.SetText(PartPropertyStorage.FinishSpec, today) with { IsRefresh = true }
            : null;
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
