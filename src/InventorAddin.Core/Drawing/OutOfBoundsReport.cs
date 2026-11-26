namespace InventorAddin.Core.Drawing;

/// <summary>One finding with the sheet it was found on (task 195).</summary>
public sealed record SheetFinding(string SheetName, BoundsFinding Finding)
{
    /// <summary>The list line: the sheet, then the finding's own message.</summary>
    public string Line => $"{SheetName}: {Finding.Message}";
}

/// <summary>The words Check Out of Bounds shows (spec 05, task 195).</summary>
public static class OutOfBoundsReport
{
    public const string NoneMessage = "Everything is inside the border.";

    public const string WrongDocumentMessage = "Open a drawing first. Check Out of Bounds works on a drawing.";

    /// <summary>The warning logged for an item whose extent could not be read.</summary>
    public static string NotCheckedWarning(string name) => $"{name} was not checked: its extent could not be read.";

    /// <summary>The sentence added to the result when some items could not be checked.</summary>
    public static string NotCheckedLine(int count) =>
        count == 1
            ? "1 item could not be read and was not checked. See the log."
            : $"{count} items could not be read and were not checked. See the log.";

    /// <summary>The heading above the list, for example "2 items are outside the border or over the title block."</summary>
    public static string Heading(int count) =>
        count == 1
            ? "1 item is outside the border or over the title block. Pick one to select it."
            : $"{count} items are outside the border or over the title block. Pick one to select it.";

    /// <summary>The list lines, in sheet order then the order the items were checked.</summary>
    public static IReadOnlyList<string> Lines(IEnumerable<SheetFinding> findings) =>
        findings.Select(f => f.Line).ToList();
}
