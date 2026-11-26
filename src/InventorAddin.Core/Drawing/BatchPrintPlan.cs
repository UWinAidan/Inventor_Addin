namespace InventorAddin.Core.Drawing;

/// <summary>
/// One component in the assembly's browser tree, read by the add-in: its full file name, whether it is a Content Center
/// or library part, and its children in browser order.
/// </summary>
/// <param name="FullFileName">The component's model file.</param>
/// <param name="IsLibrary">True for a Content Center or library part, which Batch Print skips.</param>
/// <param name="Children">The sub-components, in browser order. Null or empty for a part.</param>
public sealed record BatchPrintComponent(
    string FullFileName,
    bool IsLibrary = false,
    IReadOnlyList<BatchPrintComponent>? Children = null)
{
    /// <summary>The sub-components, never null.</summary>
    public IReadOnlyList<BatchPrintComponent> ChildList => Children ?? Array.Empty<BatchPrintComponent>();
}

/// <summary>A model and the drawing that prints it.</summary>
public sealed record BatchPrintDrawing(string ModelFullFileName, string DrawingFullFileName);

/// <summary>
/// The plan for Batch Print (spec 05, "Added for M5", item 3, question 17): the assembly's own drawing first, then each
/// component's, depth first in browser order, each file once (Default). A sub-assembly's drawing comes before its
/// children's. Content Center and library parts are skipped and counted. A model with no drawing is listed, not an
/// error. The PDF is <c>&lt;assembly name&gt;.pdf</c> beside the assembly.
/// </summary>
public sealed class BatchPrintPlan
{
    private BatchPrintPlan(
        IReadOnlyList<BatchPrintDrawing> drawings,
        IReadOnlyList<string> modelsWithoutDrawing,
        IReadOnlyList<string> skippedLibraryParts,
        string pdfFullFileName)
    {
        Drawings = drawings;
        ModelsWithoutDrawing = modelsWithoutDrawing;
        SkippedLibraryParts = skippedLibraryParts;
        PdfFullFileName = pdfFullFileName;
    }

    /// <summary>The drawings to print, in page order.</summary>
    public IReadOnlyList<BatchPrintDrawing> Drawings { get; }

    /// <summary>Models with no drawing, in walk order.</summary>
    public IReadOnlyList<string> ModelsWithoutDrawing { get; }

    /// <summary>The library and Content Center files skipped, each once.</summary>
    public IReadOnlyList<string> SkippedLibraryParts { get; }

    /// <summary>The PDF to write: <c>&lt;assembly name&gt;.pdf</c> in the assembly's folder.</summary>
    public string PdfFullFileName { get; }

    /// <summary>How many library parts were skipped.</summary>
    public int SkippedLibraryCount => SkippedLibraryParts.Count;

    /// <summary>Nothing to print: no drawing was found for any model.</summary>
    public bool IsEmpty => Drawings.Count == 0;

    /// <summary>
    /// Builds the plan. <paramref name="findPrint"/> finds a model's existing drawing, normally
    /// <see cref="PrintPlan.FindExistingPrint"/> with the file system's existence check; it returns null for none.
    /// </summary>
    public static BatchPrintPlan Create(BatchPrintComponent assembly, Func<string, string?> findPrint)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentNullException.ThrowIfNull(findPrint);

        var drawings = new List<BatchPrintDrawing>();
        var missing = new List<string>();
        var skipped = new List<string>();
        var seenModels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenDrawings = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Walk(BatchPrintComponent c)
        {
            if (!seenModels.Add(Key(c.FullFileName)))
                return;

            if (c.IsLibrary)
            {
                skipped.Add(c.FullFileName);
                return;
            }

            string? drawing = findPrint(c.FullFileName);
            if (string.IsNullOrWhiteSpace(drawing))
                missing.Add(c.FullFileName);
            else if (seenDrawings.Add(Key(drawing)))
                drawings.Add(new BatchPrintDrawing(c.FullFileName, drawing));

            foreach (BatchPrintComponent child in c.ChildList)
                Walk(child);
        }

        Walk(assembly);
        return new BatchPrintPlan(drawings, missing, skipped, PdfFileName(assembly.FullFileName));
    }

    /// <summary>
    /// <c>&lt;assembly name&gt;.pdf</c> beside <paramref name="assemblyFullFileName"/>, keeping the folder's own slashes.
    /// Works on Windows paths on any platform.
    /// </summary>
    public static string PdfFileName(string assemblyFullFileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyFullFileName);
        string path = assemblyFullFileName.Trim();
        int slash = path.LastIndexOfAny(new[] { '\\', '/' });
        string folder = path[..(slash + 1)];
        string name = path[(slash + 1)..];
        int dot = name.LastIndexOf('.');
        if (dot > 0)
            name = name[..dot];
        return folder + name + ".pdf";
    }

    /// <summary>The report shown after printing: counts, then the models with no drawing by file name.</summary>
    public string ReportText()
    {
        var lines = new List<string>
        {
            Drawings.Count == 0
                ? "No drawings were found, so no PDF was made."
                : $"Printed {Drawings.Count} {(Drawings.Count == 1 ? "drawing" : "drawings")} to {PdfFullFileName}."
        };

        if (SkippedLibraryCount > 0)
            lines.Add($"Skipped {SkippedLibraryCount} library {(SkippedLibraryCount == 1 ? "part" : "parts")} (Content Center and library).");

        if (ModelsWithoutDrawing.Count > 0)
        {
            lines.Add($"{ModelsWithoutDrawing.Count} {(ModelsWithoutDrawing.Count == 1 ? "model has" : "models have")} no drawing:");
            lines.AddRange(ModelsWithoutDrawing.Select(m => "  " + FileNameOnly(m)));
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string Key(string path) => path.Trim().Replace('/', '\\');

    private static string FileNameOnly(string path)
    {
        string p = path.Trim();
        return p[(p.LastIndexOfAny(new[] { '\\', '/' }) + 1)..];
    }
}
