using System.Globalization;
using InventorAddin.Core.Drawing;
using InventorAddin.Core.PartProperties;

namespace InventorAddin.Core.Materials;

/// <summary>
/// Turns a chosen finish into the iProperties stored on the part (spec 03, "Decided"): <c>Finish</c>,
/// <c>Finish Code</c>, <c>Finish Thickness</c>, <c>Finish Note</c> and <c>Finish Spec</c>, all custom text properties.
/// </summary>
public static class FinishProperties
{
    /// <summary>
    /// The five writes, in the order Finish, Finish Code, Finish Thickness, Finish Note, Finish Spec. Each is Set to its value,
    /// or Removed when the value is blank, the same way Part Properties removes a cleared custom property.
    /// No finish (null) removes all five. <paramref name="units"/> are the part's units and only change Finish Spec
    /// (Finish Thickness stays in mm).
    /// </summary>
    public static IReadOnlyList<PropertyWrite> For(Finish? finish, PrintUnits units) =>
        new[]
        {
            Write(PartPropertyStorage.Finish, finish?.Name),
            Write(PartPropertyStorage.FinishCode, finish?.Code),
            Write(PartPropertyStorage.FinishThickness, finish is null ? null : ThicknessText(finish)),
            Write(PartPropertyStorage.FinishNote, finish?.PrintNote),
            Write(PartPropertyStorage.FinishSpec, finish is null ? null : SpecText(finish, units)),
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

    /// <summary>
    /// The Finish Spec text (Aidan, 2026-11-22, spec 05 question 23): the finish's print name (its name when it has none), then <c>, </c> and the thickness in
    /// the part's units when there is one. Metric is micrometres, <c>Test Coat, 10-15 µm</c>; imperial is inches,
    /// <c>Test Coat, .0004-.0006 IN</c>. The name alone, with no comma, when both values are zero or do not read as numbers.
    /// </summary>
    public static string SpecText(Finish finish, PrintUnits units)
    {
        ArgumentNullException.ThrowIfNull(finish);

        return SpecText(finish.SpecName, finish.ThicknessMinMm, finish.ThicknessMaxMm, units);
    }

    /// <summary>
    /// The Finish Spec text from a name and a thickness range in mm as text: the same rules as
    /// <see cref="SpecText(Finish, PrintUnits)"/>. Used when the finish is no longer in the finish list and only the
    /// stored Finish and Finish Thickness are known (task 217).
    /// </summary>
    internal static string SpecText(string name, string thicknessMinMm, string thicknessMaxMm, PrintUnits units)
    {
        var trimmed = name.Trim();
        var thickness = SpecThicknessText(thicknessMinMm, thicknessMaxMm, units);
        return thickness.Length == 0 ? trimmed : $"{trimmed}, {thickness}";
    }

    private static string SpecThicknessText(string thicknessMinMm, string thicknessMaxMm, PrintUnits units)
    {
        if (!FinishListCsv.TryParseThickness(thicknessMinMm.Trim(), out var min)
            || !FinishListCsv.TryParseThickness(thicknessMaxMm.Trim(), out var max)
            || (min == 0 && max == 0))
        {
            return "";
        }

        var imperial = units == PrintUnits.Imperial;
        var minText = imperial ? Inches(min) : Micrometres(min);
        var maxText = imperial ? Inches(max) : Micrometres(max);
        var suffix = imperial ? " IN" : " µm";
        return minText == maxText ? maxText + suffix : $"{minText}-{maxText}{suffix}";
    }

    /// <summary>mm to µm with up to one decimal, trailing zeros dropped.</summary>
    private static string Micrometres(decimal mm) =>
        Math.Round(mm * 1000m, 1, MidpointRounding.AwayFromZero).ToString("0.#", CultureInfo.InvariantCulture);

    /// <summary>mm to inches to four decimals, with no leading zero.</summary>
    private static string Inches(decimal mm)
    {
        var text = Math.Round(mm / 25.4m, 4, MidpointRounding.AwayFromZero).ToString("0.0000", CultureInfo.InvariantCulture);
        return text.StartsWith("0.", StringComparison.Ordinal) ? text[1..] : text;
    }

    private static PropertyWrite Write(PropertyLocation location, string? value)
    {
        var text = PropertyValues.NormaliseText(value);
        return text.Length == 0 ? PropertyWrite.Remove(location) : PropertyWrite.SetText(location, text);
    }
}
