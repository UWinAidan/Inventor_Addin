using InventorAddin.Core.Drawing;

namespace InventorAddin.Core.PartProperties;

/// <summary>
/// The text of the custom "Material Size" property for a sheet metal part with a flat pattern (task 230; Aidan, 2026-11-23): the
/// thickness first, then the flat pattern's two sides, the smaller first, in the part's units: "1.5 x 60 x 120 mm" on a metric part
/// and "0.063 x 2.362 x 4.724 in" on an imperial one. Each number is rounded to 2 decimals in mm or 3 in inches, trailing zeros dropped,
/// with the invariant period. Inventor's lengths are centimetres.
/// </summary>
public static class SheetMetalSizeText
{
    /// <summary>
    /// The text, or null when a size is not a usable number (null, NaN, infinite, zero or negative), so a part whose flat pattern could not
    /// be read is left alone.
    /// </summary>
    public static string? For(double? thicknessCm, double? flatLengthCm, double? flatWidthCm, PrintUnits units)
    {
        if (!IsSize(thicknessCm) || !IsSize(flatLengthCm) || !IsSize(flatWidthCm))
        {
            return null;
        }

        var small = Math.Min(flatLengthCm!.Value, flatWidthCm!.Value);
        var large = Math.Max(flatLengthCm.Value, flatWidthCm.Value);
        var imperial = units == PrintUnits.Imperial;
        return $"{Number(thicknessCm!.Value, imperial)} x {Number(small, imperial)} x {Number(large, imperial)}" + (imperial ? " in" : " mm");
    }

    /// <summary>
    /// The write that brings the stored Material Size up to date, or null when nothing is to be written: a size is unusable, or the stored
    /// text (trimmed) already equals the new text. A stored value that differs is a refresh (<see cref="PropertyWrite.IsRefresh"/>), a blank
    /// one a fill.
    /// </summary>
    public static PropertyWrite? WriteFor(
        double? thicknessCm, double? flatLengthCm, double? flatWidthCm, PrintUnits units, string? storedMaterialSize)
    {
        var text = For(thicknessCm, flatLengthCm, flatWidthCm, units);
        if (text is null)
        {
            return null;
        }

        var stored = PropertyValues.NormaliseText(storedMaterialSize);
        if (string.Equals(stored, text, StringComparison.Ordinal))
        {
            return null;
        }

        return PropertyWrite.SetText(PartPropertyStorage.MaterialSize, text) with { IsRefresh = stored.Length > 0 };
    }

    private static bool IsSize(double? value) => value is > 0 && !double.IsInfinity(value.Value);

    private static string Number(double cm, bool imperial) => MaterialSizeNumber.FromCm(cm, imperial);
}
