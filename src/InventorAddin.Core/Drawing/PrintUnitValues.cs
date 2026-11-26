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

    /// <summary>The custom properties the title block's FINISH and WEIGHT fields read (task 244).</summary>
    public static IReadOnlyList<string> ManagedNames { get; } =
        new[] { PartPropertyStorage.Mass.PropertyName, PartPropertyStorage.FinishSpec.PropertyName };

    /// <summary>
    /// What a print's FINISH and WEIGHT fields should read, given the print's and the model's units and what the drawing already stores
    /// (task 244). <see cref="Writes"/> holds only the drawing properties whose stored text differs, so an up-to-date print gets none.
    /// <see cref="ReadsDrawing"/> names the fields that must read the drawing's own copy; <see cref="ReadsModel"/> names the managed fields
    /// that must read the model (all of them when the units match).
    /// </summary>
    public sealed record Plan(
        IReadOnlyList<PropertyWrite> Writes,
        IReadOnlyList<string> ReadsDrawing,
        IReadOnlyList<string> ReadsModel);

    /// <summary>
    /// Works out the <see cref="Plan"/>. When the units match, every managed field reads the model and nothing is written (the drawing's
    /// own copies may stay). When they differ, each value <see cref="WritesFor"/> gives goes on the drawing when it is not already
    /// stored there, and its field reads the drawing; a managed field with no value of its own (an assembly's Finish Spec) reads the model.
    /// </summary>
    public static Plan PlanFor(
        PrintUnits partUnits,
        PrintUnits printUnits,
        bool isAssembly,
        double? massKg,
        string? storedFinish,
        string? storedFinishThickness,
        string? modelFinishSpec,
        IReadOnlyList<Finish> finishList,
        string? drawingMass,
        string? drawingFinishSpec)
    {
        var wanted = WritesFor(partUnits, printUnits, isAssembly, massKg, storedFinish, storedFinishThickness, modelFinishSpec, finishList);
        var drawingStored = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [PartPropertyStorage.Mass.PropertyName] = PropertyValues.NormaliseText(drawingMass),
            [PartPropertyStorage.FinishSpec.PropertyName] = PropertyValues.NormaliseText(drawingFinishSpec),
        };

        var changed = wanted
            .Where(w => !string.Equals(
                PropertyValues.NormaliseText(w.TextValue), drawingStored.GetValueOrDefault(w.Location.PropertyName, string.Empty), StringComparison.Ordinal))
            .ToList();
        var drawing = wanted.Select(w => w.Location.PropertyName).ToList();

        // A value the model could not give (an unreadable mass, a finish list that failed to load) does not send a field back to the model
        // when the drawing already stores its own copy: the field keeps reading that copy, and nothing is written (review of task 244).
        // Only when the units differ, and not for an assembly's Finish Spec, which has no value of its own by design.
        if (Differs(partUnits, printUnits))
        {
            foreach (var name in ManagedNames)
            {
                if (drawing.Contains(name, StringComparer.Ordinal) || (isAssembly && name == PartPropertyStorage.FinishSpec.PropertyName))
                {
                    continue;
                }

                if (drawingStored[name].Length > 0)
                {
                    drawing.Add(name);
                }
            }
        }

        var model = ManagedNames.Where(n => !drawing.Contains(n, StringComparer.Ordinal)).ToList();
        return new Plan(changed, drawing, model);
    }

    /// <summary>The custom property set Inventor writes into a field's text (found with a COM probe, task 244).</summary>
    private const string UserDefinedSet = "User Defined Properties";

    /// <summary>The set name the template builder writes; Inventor stores the fields back under <see cref="UserDefinedSet"/>.</summary>
    private const string UserDefinedSetLong = "Inventor User Defined Properties";

    /// <summary>The FormatID of the custom property set, which is the same however the set is named.</summary>
    private const string UserDefinedFormatId = "{D5CDD505-2E9C-101B-9397-08002B2CF9AE}";

    private static readonly Regex PropertyElement = new(@"<Property\b[^>]*>", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex ModelDocument = new(@"\bDocument\s*=\s*(['""])model\1", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex DrawingDocument = new(@"\bDocument\s*=\s*(['""])drawing\1", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>True when the field tag reads a custom (user defined) property: by its FormatID, or by either name of the set.</summary>
    private static bool IsCustomField(string tag) =>
        tag.Contains(UserDefinedFormatId, StringComparison.OrdinalIgnoreCase)
        || Regex.IsMatch(
            tag,
            @"\bPropertySet\s*=\s*(['""])(" + Regex.Escape(UserDefinedSet) + "|" + Regex.Escape(UserDefinedSetLong) + @")\1",
            RegexOptions.IgnoreCase);

    private static bool IsNamed(string tag, IReadOnlyCollection<string> propertyNames) =>
        propertyNames.Any(n => Regex.IsMatch(tag, @"\bProperty\s*=\s*(['""])" + Regex.Escape(n) + @"\1", RegexOptions.IgnoreCase));

    /// <summary>How many custom property fields in <paramref name="formatted"/> are named in <paramref name="propertyNames"/>, whichever document they read.</summary>
    public static int CountFields(string? formatted, IReadOnlyCollection<string> propertyNames)
    {
        ArgumentNullException.ThrowIfNull(propertyNames);
        return string.IsNullOrEmpty(formatted)
            ? 0
            : PropertyElement.Matches(formatted).Count(m => IsCustomField(m.Value) && IsNamed(m.Value, propertyNames));
    }

    /// <summary>
    /// <paramref name="formatted"/> (a title block text's formatted text) with every custom property field named in <paramref name="propertyNames"/>
    /// read from the drawing (<c>Document='drawing'</c>) instead of the model. Other fields and text are untouched, and a field already
    /// on the drawing stays as it is. Returns the same string when there is nothing to change.
    /// </summary>
    public static string PointAtDrawing(string formatted, IReadOnlyCollection<string> propertyNames) =>
        Point(formatted, propertyNames, ModelDocument, "drawing");

    /// <summary>The reverse of <see cref="PointAtDrawing"/>: the named custom fields read the model again (task 244).</summary>
    public static string PointAtModel(string formatted, IReadOnlyCollection<string> propertyNames) =>
        Point(formatted, propertyNames, DrawingDocument, "model");

    private static string Point(string formatted, IReadOnlyCollection<string> propertyNames, Regex from, string to)
    {
        ArgumentNullException.ThrowIfNull(formatted);
        ArgumentNullException.ThrowIfNull(propertyNames);

        return PropertyElement.Replace(formatted, match =>
        {
            var tag = match.Value;
            return IsCustomField(tag) && IsNamed(tag, propertyNames)
                ? from.Replace(tag, m => $"Document={m.Groups[1].Value}{to}{m.Groups[1].Value}")
                : tag;
        });
    }
}
