namespace InventorAddin.Core.Drawing;

/// <summary>Where an automatic note goes on the drawing (spec 05, "Added for M5", item 1).</summary>
public enum NotePlacement
{
    /// <summary>A numbered note in the NOTES list above the title block.</summary>
    List,

    /// <summary>A bold line at the bottom of the sheet.</summary>
    Bottom,
}

/// <summary>One row of the Material or Finish tab: a name pattern (<c>*</c> and <c>?</c> wildcards), its note, and where it goes.</summary>
public sealed record NoteRule(string Pattern, string Note, NotePlacement Placement);

/// <summary>A problem found reading the notes workbook, naming the tab and row when there are any.</summary>
public sealed record NotesWorkbookProblem(string? Tab, int? Row, string Message)
{
    public override string ToString() => (Tab, Row) switch
    {
        ({ } tab, { } row) => $"{tab} tab, row {row}: {Message}",
        ({ } tab, null) => $"{tab} tab: {Message}",
        _ => Message,
    };
}

/// <summary>The rules of the notes workbook, <c>libraries/AWB_Notes.xlsx</c>, in workbook order.</summary>
public sealed record NotesWorkbook(IReadOnlyList<NoteRule> MaterialRules, IReadOnlyList<NoteRule> FinishRules)
{
    public static NotesWorkbook Empty { get; } = new([], []);
}

/// <summary>What reading the notes workbook gave: the rules that loaded and a problem for each row or tab left out.</summary>
public sealed record NotesWorkbookReadResult(NotesWorkbook Workbook, IReadOnlyList<NotesWorkbookProblem> Problems)
{
    public bool HasProblems => Problems.Count > 0;

    /// <summary>
    /// Whether the workbook as a whole could not be used: a problem that names no tab and no row (the file is missing or unreadable, or a
    /// tab is absent). A bad row is not this; the rest of the rules still load.
    /// </summary>
    public bool CouldNotRead => Problems.Any(p => p.Tab == null && p.Row == null);
}
