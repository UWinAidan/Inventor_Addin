using InventorAddin.Core.Settings;

namespace InventorAddin.Core.Drawing;

/// <summary>
/// The decisions behind Create Print (spec 02, "Commands: Create Print"): the drawing's file name, which existing
/// drawing beside the model counts as "a print already exists", the scale of the front base view, and the messages.
/// </summary>
public static class PrintPlan
{
    /// <summary>The share of the sheet's width and height the view may fill at the chosen scale (Default).</summary>
    public const double SheetFillFraction = 0.6;

    /// <summary>The drawing file extensions Inventor makes. Spike 032: the drawing template decides which one is used.</summary>
    public static IReadOnlyList<string> DrawingExtensions { get; } = new[] { ".dwg", ".idw" };

    /// <summary>The standard view scales, largest first (Default): 10:1 down to 1:50.</summary>
    public static IReadOnlyList<double> StandardScales { get; } = new[]
    {
        10.0, 5.0, 4.0, 2.0, 1.0, 1.0 / 2, 1.0 / 4, 1.0 / 5, 1.0 / 10, 1.0 / 20, 1.0 / 50,
    };

    public const string UnsavedMessage = "Save the part before creating its print.";

    public const string AlreadyExistsQuestion = "A print for this part already exists. Open it?";

    public const string WrongDocumentMessage = "Create Print works on parts and assemblies.";

    /// <summary>Shown when the model's range box is empty or flat, so there is nothing to scale a view to (Default).</summary>
    public const string NoGeometryMessage =
        "The model has no geometry to show in a front view, so no print was created.";

    /// <summary>
    /// The model's folder and file name with <paramref name="drawingExtension"/> in place of the model's extension:
    /// <c>C:\Work\TEST_PLATE,AWB-10001.ipt</c> and <c>.dwg</c> give <c>C:\Work\TEST_PLATE,AWB-10001.dwg</c>.
    /// Only the last extension of the file name is replaced; dots in the folder are left alone. Works on Windows paths
    /// on any platform, so it does not use <see cref="Path"/>.
    /// </summary>
    /// <param name="modelFullFileName">The saved model's full file name.</param>
    /// <param name="drawingExtension">The drawing template's extension, with or without the leading dot.</param>
    public static string DrawingFileName(string modelFullFileName, string drawingExtension)
    {
        if (string.IsNullOrWhiteSpace(modelFullFileName))
            throw new ArgumentException("The model has no file name.", nameof(modelFullFileName));
        string extension = NormaliseExtension(drawingExtension);

        string model = modelFullFileName.Trim();
        int nameStart = model.LastIndexOfAny(new[] { '\\', '/' }) + 1;
        if (nameStart >= model.Length)
            throw new ArgumentException($"'{modelFullFileName}' has no file name.", nameof(modelFullFileName));

        int dot = model.LastIndexOf('.');
        string withoutExtension = dot > nameStart ? model[..dot] : model;
        return withoutExtension + extension;
    }

    /// <summary>
    /// The drawing files that count as an existing print of the model, in the order to look for them:
    /// <paramref name="preferredExtension"/> first, then the other drawing extensions (spec 02: an <c>.idw</c> or
    /// <c>.dwg</c> beside the model both count).
    /// </summary>
    public static IReadOnlyList<string> ExistingPrintCandidates(string modelFullFileName, string preferredExtension)
    {
        string preferred = NormaliseExtension(preferredExtension);
        return new[] { preferred }
            .Concat(DrawingExtensions.Where(e => !string.Equals(e, preferred, StringComparison.OrdinalIgnoreCase)))
            .Select(e => DrawingFileName(modelFullFileName, e))
            .ToArray();
    }

    /// <summary>
    /// The first of <see cref="ExistingPrintCandidates"/> for which <paramref name="exists"/> is true, or null.
    /// </summary>
    public static string? FindExistingPrint(string modelFullFileName, string preferredExtension, Func<string, bool> exists)
    {
        ArgumentNullException.ThrowIfNull(exists);
        return ExistingPrintCandidates(modelFullFileName, preferredExtension).FirstOrDefault(exists);
    }

    /// <summary>
    /// The largest of <see cref="StandardScales"/> at which a view <paramref name="viewWidthCm"/> by
    /// <paramref name="viewHeightCm"/> at scale 1 fits within <see cref="SheetFillFraction"/> of the sheet's width and
    /// of its height (Default). When none fits, the smallest standard scale.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">A size is zero, negative, or not a finite number.</exception>
    public static double ChooseScale(double viewWidthCm, double viewHeightCm, double sheetWidthCm, double sheetHeightCm)
    {
        RequirePositive(viewWidthCm, nameof(viewWidthCm));
        RequirePositive(viewHeightCm, nameof(viewHeightCm));
        RequirePositive(sheetWidthCm, nameof(sheetWidthCm));
        RequirePositive(sheetHeightCm, nameof(sheetHeightCm));

        double maxWidth = sheetWidthCm * SheetFillFraction;
        double maxHeight = sheetHeightCm * SheetFillFraction;

        foreach (double scale in StandardScales)
        {
            if (Fits(viewWidthCm * scale, maxWidth) && Fits(viewHeightCm * scale, maxHeight))
                return scale;
        }

        return StandardScales[^1];
    }

    /// <summary>
    /// Names the units, for example
    /// <c>Inventor has no default drawing template for metric units. No print was created.</c>
    /// </summary>
    public static string MissingTemplateMessage(UnitsSystem units) =>
        $"Inventor has no default drawing template for {UnitsName(units)} units. No print was created.";

    /// <summary>
    /// Says which step failed and why, and what became of the drawing, for example
    /// <c>Creating the print failed while adding the front view: ... The drawing was closed without saving.</c>
    /// </summary>
    public static string FailedMessage(string step, string reason, PrintCleanup cleanup) =>
        $"Creating the print failed while {step}: {reason} " + cleanup switch
        {
            PrintCleanup.NoDrawing => "No drawing was made.",
            PrintCleanup.Closed => "The drawing was closed without saving.",
            PrintCleanup.CloseFailed => "The half-made drawing could not be closed. Close it without saving.",
            _ => throw new ArgumentOutOfRangeException(nameof(cleanup), cleanup, "Unknown cleanup result."),
        };

    // A tiny allowance so that a view that fits exactly is not rejected by rounding (0.6 * 55.88 and the like).
    private static bool Fits(double size, double limit) => size <= limit * (1 + 1e-9);

    private static void RequirePositive(double value, string name)
    {
        if (!(value > 0) || double.IsInfinity(value))
            throw new ArgumentOutOfRangeException(name, value, "Sizes must be positive, finite numbers of centimetres.");
    }

    private static string NormaliseExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            throw new ArgumentException("A drawing extension is required.", nameof(extension));
        string trimmed = extension.Trim();
        return trimmed.StartsWith('.') ? trimmed : "." + trimmed;
    }

    private static string UnitsName(UnitsSystem units) => units switch
    {
        UnitsSystem.Metric => "metric",
        UnitsSystem.Imperial => "imperial",
        _ => units.ToString().ToLowerInvariant(),
    };
}

/// <summary>What became of the half-made drawing after Create Print failed.</summary>
public enum PrintCleanup
{
    /// <summary>The failure came before a drawing was made.</summary>
    NoDrawing,

    /// <summary>The drawing was closed without saving.</summary>
    Closed,

    /// <summary>Closing the drawing failed, so it may still be open, unsaved.</summary>
    CloseFailed,
}
