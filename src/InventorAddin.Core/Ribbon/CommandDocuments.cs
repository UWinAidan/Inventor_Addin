namespace InventorAddin.Core.Ribbon;

/// <summary>Which document a command acts on (task 109).</summary>
public enum CommandDocument
{
    /// <summary>
    /// The document being edited (<c>ActiveEditDocument</c>): the part or sub-assembly edited in place, otherwise the
    /// document open in the window.
    /// </summary>
    Edited,

    /// <summary>The top-level document open in the window (<c>ActiveDocument</c>), also while something is edited in place.</summary>
    TopLevel,

    /// <summary>No document: the command does not act on one.</summary>
    None,
}

/// <summary>
/// The one rule for which document each command acts on (task 109). Every AWB command works while a part or sub-assembly
/// is edited in place (Aidan, 2026-10-24), so most act on the document being edited. This is the target the command reads
/// and changes; the document a transaction is started on, so that one Undo in the assembly window reverts it, is a
/// separate choice the command makes.
/// </summary>
public static class CommandDocuments
{
    /// <summary>Each command's explicit row, keyed by its <see cref="CommandNames"/> constant.</summary>
    public static IReadOnlyDictionary<string, CommandDocument> Rows { get; } = new Dictionary<string, CommandDocument>
    {
        [CommandNames.PartProperties] = CommandDocument.Edited,

        // Material and Finish keeps its own targeting (MaterialFinishTargeting, task 075); the part being edited is its
        // target.
        [CommandNames.MaterialFinish] = CommandDocument.Edited,
        [CommandNames.CreatePrint] = CommandDocument.Edited,
        [CommandNames.SaveAsCopy] = CommandDocument.Edited,
        [CommandNames.ExportModelData] = CommandDocument.Edited,
        [CommandNames.AwbHole] = CommandDocument.Edited,
        [CommandNames.AwbHoleEdit] = CommandDocument.Edited,

        // The selection is held by the top-level assembly, also while a sub-assembly is edited in place (task 060,
        // spike 052).
        [CommandNames.SaveAsReplace] = CommandDocument.TopLevel,

        // Only the folder of the document open in the window is taken (Default: unchanged, task 109).
        [CommandNames.NewPart] = CommandDocument.TopLevel,

        [CommandNames.Settings] = CommandDocument.None,

        // Task 129: Inventor Hole runs Inventor's own command, which picks its document; the toggle changes a setting.
        [CommandNames.InventorHole] = CommandDocument.None,
        [CommandNames.UseInventorHoleByDefault] = CommandDocument.None,
        [CommandNames.About] = CommandDocument.None,

        // A folder of files, not a document.
        [CommandNames.MassUpdate] = CommandDocument.None,
        [CommandNames.ExportLibraries] = CommandDocument.None,
    };

    /// <summary>The document <paramref name="commandName"/> acts on. A name with no row gives <see cref="CommandDocument.Edited"/>.</summary>
    public static CommandDocument For(string commandName) =>
        Rows.TryGetValue(commandName, out CommandDocument document) ? document : CommandDocument.Edited;
}
