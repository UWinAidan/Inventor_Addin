using System.Text.RegularExpressions;
using InventorAddin.Core.Materials;
using InventorAddin.Core.PartProperties;

namespace InventorAddin.Core.Drawing;

/// <summary>
/// FINISH and WEIGHT follow the print's units, not the part's (Aidan, 2026-11-23, task 232). When a print is made in other units than
/// the part (an inch part on AWB Metric), Create Print writes the print-units Finish Spec and Mass as custom properties of the drawing,
/// formatted by the same Core code as the part's own, and points the drawing's own title block FINISH and WEIGHT fields at them
/// (spec 05 question 33, Default). When the units match nothing is written and the fields stay on the part's live properties.
/// </summary>
public static class PrintUnitValues
{
    /// <summary>True when the print's units differ from the part's, so the drawing needs its own FINISH and WEIGHT values.</summary>
    public static bool Differs(PrintUnits partUnits, PrintUnits printUnits) => partUnits != printUnits;

    /// <summary>
    /// The drawing properties to write: Mass in the print's units (nothing when the mass is not usable), and, for a part, the Finish Spec
    /// in the print's units. The Finish Spec is rebuilt from the finish only when the stored spec is blank or is the add-in's own spec in
    /// the part's units; any other stored text is hand-written and is copied as it is. Nothing is written for a Finish Spec that is
    /// blank. An assembly gets Mass only (its Finish Spec is a dash in the model, which has no units).
    /// </summary>
    public static IReadOnlyList<PropertyWrite> WritesFor(
        PrintUnits partUnits,
        PrintUnits printUnits,
        bool isAssembly,
        double? massKg,
        string? storedFinish,
        string? storedFinishThickness,
        string? storedFinishSpec,
        IReadOnlyList<Finish> finishList)
    {
        ArgumentNullException.ThrowIfNull(finishList);

        var writes = new List<PropertyWrite>();
        if (!Differs(partUnits, printUnits))
        {
            return writes;
        }

        var mass = MassText.For(massKg, printUnits);
        if (mass is not null)
        {
            writes.Add(PropertyWrite.SetText(PartPropertyStorage.Mass, mass));
        }

        if (isAssembly)
        {
            return writes;
        }

        var stored = PropertyValues.NormaliseText(storedFinishSpec);
        var inPartUnits = FinishSpecBackfill.For(storedFinish, storedFinishThickness, null, finishList, partUnits)?.TextValue ?? string.Empty;
        string spec;
        if (stored.Length > 0 && !string.Equals(stored, inPartUnits, StringComparison.Ordinal))
        {
            spec = stored;
        }
        else
        {
            spec = FinishSpecBackfill.For(storedFinish, storedFinishThickness, null, finishList, printUnits)?.TextValue ?? stored;
        }

        if (spec.Length > 0)
        {
            writes.Add(PropertyWrite.SetText(PartPropertyStorage.FinishSpec, spec));
        }

        return writes;
    }

    private const string UserDefinedSet = "Inventor User Defined Properties";

    private static readonly Regex PropertyElement = new(@"<Property\b[^>]*>", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex ModelDocument = new(@"\bDocument\s*=\s*(['""])model\1", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// <paramref name="formatted"/> (a title block text's formatted text) with every property field named in <paramref name="propertyNames"/>
    /// read from the drawing (<c>Document='drawing'</c>) instead of the model. Other fields and text are untouched, and a field already
    /// on the drawing stays as it is. Returns the same string when there is nothing to change.
    /// </summary>
    public static string PointAtDrawing(string formatted, IReadOnlyCollection<string> propertyNames)
    {
        ArgumentNullException.ThrowIfNull(formatted);
        ArgumentNullException.ThrowIfNull(propertyNames);

        return PropertyElement.Replace(formatted, match =>
        {
            var tag = match.Value;
            var custom = Regex.IsMatch(
                tag, @"\bPropertySet\s*=\s*(['""])" + Regex.Escape(UserDefinedSet) + @"\1", RegexOptions.IgnoreCase);
            var named = custom && propertyNames.Any(n => Regex.IsMatch(
                tag, @"\bProperty\s*=\s*(['""])" + Regex.Escape(n) + @"\1", RegexOptions.IgnoreCase));
            return named ? ModelDocument.Replace(tag, m => $"Document={m.Groups[1].Value}drawing{m.Groups[1].Value}") : tag;
        });
    }
}
