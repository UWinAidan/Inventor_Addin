using System.Text.RegularExpressions;
using InventorAddin.Core.Materials;
using InventorAddin.Core.PartProperties;

namespace InventorAddin.Core.Drawing;

/// <summary>
/// The dash on an assembly's print (task 245; Aidan, 2026-11-26, spec 05 question 35). MATERIAL, MATERIAL SIZE and FINISH are part-only
/// fields. The assembly itself stays blank; when its value is blank, the print's field reads a drawing property holding "—", and when
/// the value is set the field reads the model again. MATERIAL is a Design Tracking field, so its tag is swapped for a custom
/// <c>Material</c> tag on the drawing and back (proved with a COM probe in task 245).
/// </summary>
public static class AssemblyDashFields
{
    /// <summary>The text on the print for a blank field.</summary>
    public const string Dash = PrintPropertyBackfill.NotApplicable;

    /// <summary>The drawing's own custom <c>Material</c> property, which the MATERIAL field reads while the assembly's Material is blank.</summary>
    public static readonly PropertyLocation DrawingMaterial = new(PropertySetNames.UserDefined, "Material", PropertyValueKind.Text);

    /// <summary>The custom-property fields that follow the same rule as MATERIAL (named as in the title block).</summary>
    public static IReadOnlyList<string> CustomNames { get; } =
        new[] { PartPropertyStorage.MaterialSize.PropertyName, PartPropertyStorage.FinishSpec.PropertyName };

    /// <summary>
    /// What the print needs: <see cref="Writes"/> are the drawing properties that do not hold the dash yet; <see cref="ReadsDrawing"/> and
    /// <see cref="ReadsModel"/> split <see cref="CustomNames"/>; <see cref="MaterialReadsDrawing"/> says where MATERIAL reads.
    /// </summary>
    public sealed record Plan(
        IReadOnlyList<PropertyWrite> Writes,
        IReadOnlyList<string> ReadsDrawing,
        IReadOnlyList<string> ReadsModel,
        bool MaterialReadsDrawing);

    /// <summary>
    /// Works out the <see cref="Plan"/> from the assembly's own values and what the drawing stores. A field is on the drawing exactly when
    /// the assembly's value is blank. Nothing is ever written to the assembly.
    /// </summary>
    public static Plan PlanFor(
        string? modelMaterial,
        string? modelMaterialSize,
        string? modelFinishSpec,
        string? drawingMaterial,
        string? drawingMaterialSize,
        string? drawingFinishSpec)
    {
        var writes = new List<PropertyWrite>();
        var drawing = new List<string>();
        var model = new List<string>();

        bool materialBlank = IsBlank(modelMaterial);
        if (materialBlank && !IsDash(drawingMaterial))
        {
            writes.Add(PropertyWrite.SetText(DrawingMaterial, Dash));
        }

        Add(PartPropertyStorage.MaterialSize, modelMaterialSize, drawingMaterialSize);
        Add(PartPropertyStorage.FinishSpec, modelFinishSpec, drawingFinishSpec);
        return new Plan(writes, drawing, model, materialBlank);

        void Add(PropertyLocation location, string? modelValue, string? drawingValue)
        {
            if (IsBlank(modelValue))
            {
                if (!IsDash(drawingValue))
                {
                    writes.Add(PropertyWrite.SetText(location, Dash));
                }

                drawing.Add(location.PropertyName);
            }
            else
            {
                model.Add(location.PropertyName);
            }
        }
    }

    /// <summary>
    /// <paramref name="units"/> (task 244's plan) with the dash rule applied: the dash plan decides Material Size and Finish Spec, the units
    /// plan keeps deciding Mass. <paramref name="dash"/> is null for a part, which leaves the units plan as it is.
    /// </summary>
    public static PrintUnitValues.Plan Combine(PrintUnitValues.Plan units, Plan? dash)
    {
        ArgumentNullException.ThrowIfNull(units);
        if (dash is null)
        {
            return units;
        }

        bool Kept(string name) => !CustomNames.Contains(name, StringComparer.Ordinal);
        return new PrintUnitValues.Plan(
            units.Writes.Where(w => Kept(w.Location.PropertyName)).Concat(dash.Writes).ToList(),
            units.ReadsDrawing.Where(Kept).Concat(dash.ReadsDrawing).ToList(),
            units.ReadsModel.Where(Kept).Concat(dash.ReadsModel).ToList());
    }

    private static bool IsBlank(string? value) => PropertyValues.NormaliseText(value).Length == 0;

    private static bool IsDash(string? value) => string.Equals(PropertyValues.NormaliseText(value), Dash, StringComparison.Ordinal);

    // The tags, as Inventor stores them (probe, task 245). Inventor adds a PropertyID to a custom tag it has re-read, so a custom tag is
    // found by its FormatID and name, never by its exact text.
    private const string DesignTrackingFormatId = "{32853F0F-3444-11D1-9E93-0060B03C1CA6}";
    private const string CustomFormatId = "{D5CDD505-2E9C-101B-9397-08002B2CF9AE}";
    private const string MaterialAtDrawing =
        "<Property Document='drawing' PropertySet='User Defined Properties' Property='Material' FormatID='" + CustomFormatId + "'>";
    private const string MaterialAtModel =
        "<Property Document='model' PropertySet='Design Tracking Properties' Property='Material' FormatID='" + DesignTrackingFormatId + "' PropertyID='20'>";

    private static readonly Regex PropertyElement = new(@"<Property\b[^>]*>", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static bool IsDesignTrackingMaterial(string tag) =>
        tag.Contains(DesignTrackingFormatId, StringComparison.OrdinalIgnoreCase)
        && Regex.IsMatch(tag, @"\bPropertyID\s*=\s*(['""])20\1", RegexOptions.IgnoreCase);

    private static bool IsDrawingCustomMaterial(string tag) =>
        tag.Contains(CustomFormatId, StringComparison.OrdinalIgnoreCase)
        && Regex.IsMatch(tag, @"\bProperty\s*=\s*(['""])Material\1", RegexOptions.IgnoreCase)
        && Regex.IsMatch(tag, @"\bDocument\s*=\s*(['""])drawing\1", RegexOptions.IgnoreCase);

    /// <summary>True when <paramref name="formatted"/> has a MATERIAL field, whichever of the two tags it carries now.</summary>
    public static bool HasMaterialField(string? formatted) =>
        !string.IsNullOrEmpty(formatted)
        && PropertyElement.Matches(formatted).Any(m => IsDesignTrackingMaterial(m.Value) || IsDrawingCustomMaterial(m.Value));

    /// <summary>
    /// <paramref name="formatted"/> with the MATERIAL field reading the drawing's custom <c>Material</c> (<paramref name="onDrawing"/>) or the
    /// model's Design Tracking Material (not <paramref name="onDrawing"/>). Everything else, and a field already right, is untouched.
    /// </summary>
    public static string PointMaterial(string formatted, bool onDrawing)
    {
        ArgumentNullException.ThrowIfNull(formatted);
        return PropertyElement.Replace(formatted, match =>
        {
            var tag = match.Value;
            if (onDrawing)
            {
                return IsDesignTrackingMaterial(tag) ? MaterialAtDrawing : tag;
            }

            return IsDrawingCustomMaterial(tag) ? MaterialAtModel : tag;
        });
    }
}
