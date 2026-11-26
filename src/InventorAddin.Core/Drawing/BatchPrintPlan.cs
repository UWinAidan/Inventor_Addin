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

/// <summary>A planned drawing that could not be opened or exported, and why.</summary>
public sealed record BatchPrintFailure(string DrawingFullFileName, string Reason);

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

    /// <summary>Shown when the active document is not an assembly.</summary>
    public const string WrongDocumentMessage = "Batch Print works on assemblies. Open an assembly first.";

    /// <summary>Shown when the assembly has never been saved, so it has no folder to put the PDF in.</summary>
    public const string UnsavedMessage = "Save the assembly first. Batch Print saves the PDF next to it.";

    /// <summary>Shown when the assembly itself is a Content Center or library file (Default: refused up front).</summary>
    public const string LibraryRootMessage =
        "This assembly is a Content Center or library file, which Batch Print skips, so there is nothing to print.";

    /// <summary>Shown when no model under the assembly has a drawing.</summary>
    public const string NothingToPrintMessage = "No drawings were found for this assembly, so no PDF was made.";

    /// <summary>The question asked before an existing PDF is replaced.</summary>
    public static string ReplaceQuestion(string pdfFullFileName) =>
        $"{FileNameOnly(pdfFullFileName)} already exists in this folder. Replace it?";

    /// <summary>
    /// The report shown after printing, from what actually happened: <paramref name="failures"/> are planned drawings that
    /// could not be opened or exported (a failure for a drawing not in the plan is ignored, and a drawing counts once). The
    /// "Printed" count is the planned drawings less the failed ones. Then the failures, the skipped library parts and the
    /// models with no drawing, by file name.
    /// </summary>
    public string ReportText(IReadOnlyCollection<BatchPrintFailure>? failures = null)
    {
        var planned = new HashSet<string>(Drawings.Select(d => Key(d.DrawingFullFileName)), StringComparer.OrdinalIgnoreCase);
        var failed = new List<BatchPrintFailure>();
        var seenFailed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (BatchPrintFailure f in failures ?? Array.Empty<BatchPrintFailure>())
            if (planned.Contains(Key(f.DrawingFullFileName)) && seenFailed.Add(Key(f.DrawingFullFileName)))
                failed.Add(f);

        int printed = Drawings.Count - failed.Count;
        var lines = new List<string>();
        if (Drawings.Count == 0)
            lines.Add("No drawings were found, so no PDF was made.");
        else if (printed == 0)
            lines.Add(Drawings.Count == 1
                ? "No PDF was made: the drawing could not be printed."
                : $"No PDF was made: none of the {Drawings.Count} drawings could be printed.");
        else if (failed.Count == 0)
            lines.Add($"Printed {printed} {(printed == 1 ? "drawing" : "drawings")} to {PdfFullFileName}.");
        else
            lines.Add($"Printed {printed} of {Drawings.Count} drawings to {PdfFullFileName}.");

        if (failed.Count > 0)
        {
            lines.Add($"{failed.Count} {(failed.Count == 1 ? "drawing" : "drawings")} could not be printed:");
            lines.AddRange(failed.Select(f => "  " + FileNameOnly(f.DrawingFullFileName) + ": " + f.Reason));
        }

        if (SkippedLibraryCount > 0)
            lines.Add($"Skipped {SkippedLibraryCount} library {(SkippedLibraryCount == 1 ? "part" : "parts")} (Content Center and library).");

        if (ModelsWithoutDrawing.Count > 0)
        {
            lines.Add($"{ModelsWithoutDrawing.Count} {(ModelsWithoutDrawing.Count == 1 ? "model has" : "models have")} no drawing:");
            lines.AddRange(ModelsWithoutDrawing.Select(m => "  " + FileNameOnly(m)));
        }

        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>Shown when the PDF cannot be written, usually because a viewer has it open.</summary>
    public static string PdfNotWrittenMessage(string pdfFullFileName) =>
        $"Could not write {pdfFullFileName}. If it is open in a PDF viewer, close it and try again.";

    /// <summary>
    /// True when <paramref name="path"/> is inside one of <paramref name="folders"/> (the project's library paths), ignoring
    /// case and slash direction. Blank folders are ignored. Used for the assembly itself, whose own library state Inventor
    /// does not report.
    /// </summary>
    public static bool IsUnderAnyFolder(string path, IEnumerable<string> folders)
    {
        ArgumentNullException.ThrowIfNull(folders);
        string p = Key(path);
        foreach (string folder in folders)
        {
            if (string.IsNullOrWhiteSpace(folder))
                continue;
            string f = Key(folder).TrimEnd('\\') + "\\";
            if (p.StartsWith(f, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>
    /// A project path as an absolute folder: a rooted path (drive letter, UNC or leading slash) is kept; a relative one is taken
    /// from the folder of <paramref name="projectFileFullName"/> (the .ipj), with "." and ".." segments resolved. Works on Windows
    /// paths on any platform.
    /// </summary>
    public static string ResolveProjectFolder(string projectFileFullName, string folder) =>
        ResolveProjectFolderCore(projectFileFullName, folder);

    private const char Bs = '\\';
    private const char Slash = '/';
    private const char Colon = ':';

    private static string ResolveProjectFolderCore(string projectFileFullName, string folder)
    {
        string f = (folder ?? string.Empty).Trim().Replace(Slash, Bs);
        if (f.Length == 0)
            return string.Empty;
        bool rooted = f.StartsWith(Bs) || (f.Length >= 2 && f[1] == Colon);
        string combined = f;
        if (!rooted)
        {
            string project = (projectFileFullName ?? string.Empty).Trim().Replace(Slash, Bs);
            int slash = project.LastIndexOf(Bs);
            combined = (slash >= 0 ? project[..(slash + 1)] : string.Empty) + f;
        }

        string prefix = combined.StartsWith(@"\\") ? @"\\" : string.Empty;
        var parts = new List<string>();
        foreach (string part in combined[prefix.Length..].Split(Bs, StringSplitOptions.RemoveEmptyEntries))
        {
            if (part == ".")
                continue;
            if (part == ".." && parts.Count > 0 && !parts[^1].EndsWith(Colon))
                parts.RemoveAt(parts.Count - 1);
            else if (part != "..")
                parts.Add(part);
        }

        string joined = string.Join(Bs, parts);
        return combined.StartsWith(Bs) && prefix.Length == 0 ? "\\" + joined : prefix + joined;
    }

    /// <summary>Shown when the PDFs could not be joined or written for a reason other than a locked file.</summary>
    public static string PdfMergeFailedMessage(string pdfFullFileName) =>
        $"Could not make {pdfFullFileName}. An existing PDF was left as it was. See the log for details.";

    private static string Key(string path) => path.Trim().Replace('/', '\\');

    private static string FileNameOnly(string path)
    {
        string p = path.Trim();
        return p[(p.LastIndexOfAny(new[] { '\\', '/' }) + 1)..];
    }
}
