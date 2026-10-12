using InventorAddin.Core.Settings;

namespace InventorAddin.Core.Drawing;

/// <summary>
/// The decisions behind Create Print (spec 02, "Commands: Create Print"): the drawing's file name in the model's
/// <c>Prints</c> folder, which existing drawing counts as "a print already exists", the scale of the front base view, and the messages.
/// </summary>
public static class PrintPlan
{
    /// <summary>The share of the sheet's width and height the view may fill at the chosen scale (Default).</summary>
    public const double SheetFillFraction = 0.6;

    /// <summary>The drawing file extensions Inventor makes. Spike 032: the drawing template decides which one is used.</summary>
    public static IReadOnlyList<string> DrawingExtensions { get; } = new[] { ".dwg", ".idw" };

    /// <summary>
    /// The extension of a print Create Print makes (Aidan, 2026-10-12). Existing prints with either of
    /// <see cref="DrawingExtensions"/> are still found; this one is preferred when both exist.
    /// </summary>
    public const string NewPrintExtension = ".idw";

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

    /// <summary>The subfolder of the model's folder that holds its prints (Aidan, 2026-10-11).</summary>
    public const string PrintsFolderName = "Prints";

    /// <summary>
    /// The <see cref="PrintsFolderName"/> folder inside the model's folder: <c>C:\Work\TEST_PLATE,AWB-10001.ipt</c>
    /// gives <c>C:\Work\Prints</c>. Joins with the separator the model's path uses last (<c>\</c> or <c>/</c>). Works on
    /// Windows paths on any platform, so it does not use <see cref="Path"/>.
    /// </summary>
    /// <param name="modelFullFileName">The saved model's full file name.</param>
    public static string PrintsFolder(string modelFullFileName) =>
        SplitModelPath(modelFullFileName).Folder + PrintsFolderName;

    /// <summary>
    /// The model's file name with <paramref name="drawingExtension"/> in place of the model's extension, in the
    /// <see cref="PrintsFolder"/>: <c>C:\Work\TEST_PLATE,AWB-10001.ipt</c> and <c>.dwg</c> give
    /// <c>C:\Work\Prints\TEST_PLATE,AWB-10001.dwg</c>. Only the last extension of the file name is replaced; dots in
    /// the folder are left alone. Works on Windows paths on any platform, so it does not use <see cref="Path"/>.
    /// </summary>
    /// <param name="modelFullFileName">The saved model's full file name.</param>
    /// <param name="drawingExtension">The drawing template's extension, with or without the leading dot.</param>
    public static string DrawingFileName(string modelFullFileName, string drawingExtension)
    {
        ModelPath model = SplitModelPath(modelFullFileName);
        return model.InPrintsFolder() + NormaliseExtension(drawingExtension);
    }

    /// <summary>
    /// The drawing files that count as an existing print of the model, in the order to look for them: in the
    /// <see cref="PrintsFolder"/> with <paramref name="preferredExtension"/> and then the other drawing extension, then
    /// beside the model with the same two, where prints made before 2026-10-11 were saved (Default). An <c>.idw</c> or
    /// <c>.dwg</c> both count (spec 02).
    /// </summary>
    public static IReadOnlyList<string> ExistingPrintCandidates(string modelFullFileName, string preferredExtension)
    {
        ModelPath model = SplitModelPath(modelFullFileName);
        string preferred = NormaliseExtension(preferredExtension);
        string[] extensions = new[] { preferred }
            .Concat(DrawingExtensions.Where(e => !string.Equals(e, preferred, StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        string inPrints = model.InPrintsFolder();
        string beside = model.Folder + model.NameWithoutExtension;
        return extensions.Select(e => inPrints + e)
            .Concat(extensions.Select(e => beside + e))
            .ToArray();
    }

    /// <summary>
    /// True when <paramref name="drawingFullFileName"/> is one of the model's <see cref="ExistingPrintCandidates"/>,
    /// ignoring case and the direction of the slashes: an <c>.idw</c> or <c>.dwg</c> with the model's file name without
    /// its extension, in the model's <see cref="PrintsFolder"/> or beside the model. Rename uses this rule to find the
    /// drawings it renames with the model (task 059), so Create Print finds them again afterwards. False for a blank path.
    /// </summary>
    /// <param name="modelFullFileName">The model's full file name.</param>
    /// <param name="drawingFullFileName">The drawing's full file name.</param>
    public static bool IsPrintOf(string modelFullFileName, string? drawingFullFileName)
    {
        if (string.IsNullOrWhiteSpace(drawingFullFileName))
            return false;

        string drawing = PathKey(drawingFullFileName);
        return ExistingPrintCandidates(modelFullFileName, DrawingExtensions[0])
            .Any(c => string.Equals(PathKey(c), drawing, StringComparison.OrdinalIgnoreCase));
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
    /// The <see cref="NewPrintExtension"/> drawing template to make a print from, given the template paths Inventor's
    /// lookups return, in order (spike 032: they return <c>.dwg</c> templates, and the <c>.idw</c> ones sit beside them).
    /// For each path, blank ones skipped: the path itself when it already ends <c>.idw</c> (ignoring case), otherwise the
    /// same path with its extension changed to <c>.idw</c>; the first of those that <paramref name="exists"/>, or null.
    /// Never a <c>.dwg</c> template (Default, task 063). Works on Windows paths on any platform, so it does not use
    /// <see cref="Path"/>.
    /// </summary>
    /// <param name="lookedUp">The paths Inventor's template lookups returned, most wanted first. Read lazily, so a
    /// later lookup can run only when the earlier paths have no <c>.idw</c> template.</param>
    /// <param name="exists">Whether a file exists.</param>
    public static string? IdwTemplate(IEnumerable<string?> lookedUp, Func<string, bool> exists)
    {
        ArgumentNullException.ThrowIfNull(lookedUp);
        ArgumentNullException.ThrowIfNull(exists);

        foreach (string? path in lookedUp)
        {
            if (string.IsNullOrWhiteSpace(path))
                continue;

            string candidate = WithExtension(path.Trim(), NewPrintExtension);
            if (exists(candidate))
                return candidate;
        }

        return null;
    }

    /// <summary>
    /// The file name of Inventor's default <c>.idw</c> template in <paramref name="units"/>:
    /// <c>ANSI (mm).idw</c> or <c>ANSI (in).idw</c> (spike 032).
    /// </summary>
    public static string IdwTemplateName(UnitsSystem units) => units switch
    {
        UnitsSystem.Metric => "ANSI (mm)" + NewPrintExtension,
        UnitsSystem.Imperial => "ANSI (in)" + NewPrintExtension,
        _ => throw new ArgumentOutOfRangeException(nameof(units), units, "Unknown units."),
    };

    /// <summary>
    /// Names the template kind and the units, for example
    /// <c>Inventor has no .idw drawing template for metric units (ANSI (mm).idw). No print was created.</c>
    /// </summary>
    public static string MissingTemplateMessage(UnitsSystem units) =>
        $"Inventor has no {NewPrintExtension} drawing template for {UnitsName(units)} units ({IdwTemplateName(units)}). " +
        "No print was created.";

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

    /// <summary>A path trimmed, with forward slashes turned into backslashes, for comparing.</summary>
    private static string PathKey(string path) => path.Trim().Replace('/', '\\');

    private static ModelPath SplitModelPath(string modelFullFileName)
    {
        if (string.IsNullOrWhiteSpace(modelFullFileName))
            throw new ArgumentException("The model has no file name.", nameof(modelFullFileName));

        string model = modelFullFileName.Trim();
        int lastSeparator = model.LastIndexOfAny(new[] { '\\', '/' });
        int nameStart = lastSeparator + 1;
        if (nameStart >= model.Length)
            throw new ArgumentException($"'{modelFullFileName}' has no file name.", nameof(modelFullFileName));

        char separator = lastSeparator >= 0 ? model[lastSeparator] : '\\';
        int dot = model.LastIndexOf('.');
        string name = dot > nameStart ? model[nameStart..dot] : model[nameStart..];
        return new ModelPath(model[..nameStart], name, separator);
    }

    /// <summary>
    /// A model's folder (with its trailing separator, or empty when the path has no folder), its file name without
    /// the extension, and the separator to join with.
    /// </summary>
    private readonly record struct ModelPath(string Folder, string NameWithoutExtension, char Separator)
    {
        /// <summary>The model's name, without an extension, in its <see cref="PrintsFolderName"/> folder.</summary>
        public string InPrintsFolder() => Folder + PrintsFolderName + Separator + NameWithoutExtension;
    }

    /// <summary>
    /// <paramref name="path"/> with the extension of its file name replaced by <paramref name="extension"/>, or added
    /// when it has none; returned as it is when it already ends in <paramref name="extension"/> (ignoring case). Dots in
    /// the folder are left alone.
    /// </summary>
    private static string WithExtension(string path, string extension)
    {
        if (path.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            return path;

        int nameStart = path.LastIndexOfAny(new[] { '\\', '/' }) + 1;
        int dot = path.LastIndexOf('.');
        return (dot >= nameStart ? path[..dot] : path) + extension;
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
