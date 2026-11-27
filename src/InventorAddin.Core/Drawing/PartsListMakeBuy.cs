namespace InventorAddin.Core.Drawing;

/// <summary>
/// The parts list side of Make/Buy (spec 05, "Added for M5", item 6; task 208): which column is the Make/Buy one, what a new one
/// looks like, whether a cell needs writing, and what the command says afterwards. The Make/Buy decision itself is <see cref="MakeBuy"/>.
/// </summary>
public static class PartsListMakeBuy
{
    /// <summary>The custom property the template's Make/Buy column reads (spike 183).</summary>
    public const string CustomPropertyName = "Make/Buy";

    /// <summary>The heading of a Make/Buy column the command adds.</summary>
    public const string ColumnTitle = "MAKE/BUY";

    /// <summary>Shown when the active document is not a drawing.</summary>
    public const string WrongDocument = "Open a drawing first. Update Parts List works on a drawing.";

    /// <summary>Shown when the active sheet has no parts list.</summary>
    public const string NoPartsList = "The sheet has no parts list. Place a parts list first.";

    /// <summary>
    /// True when a column is the Make/Buy column: its custom property is "Make/Buy", or (when it reads no custom property) its
    /// heading is "MAKE/BUY". Case and surrounding spaces are ignored.
    /// </summary>
    public static bool IsMakeBuyColumn(string? customPropertyName, string? title)
    {
        string property = (customPropertyName ?? string.Empty).Trim();
        if (property.Length > 0)
            return string.Equals(property, CustomPropertyName, StringComparison.OrdinalIgnoreCase);
        return string.Equals((title ?? string.Empty).Trim(), ColumnTitle, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// True when the cell should be written: there is text to write and the cell does not already show it. A blank decision never writes (see <see cref="ShouldClear"/>),
    /// so a row that is neither Make nor Buy keeps whatever was typed in its cell (Default).
    /// </summary>
    public static bool ShouldWrite(string newText, string? currentText) =>
        newText.Length > 0 && !string.Equals(newText, currentText?.Trim(), StringComparison.Ordinal);

    /// <summary>
    /// True when the cell holds a stale Make/Buy text from an earlier run and should be cleared: the new decision is blank and the cell
    /// reads exactly MAKE or BUY (spaces and case ignored). Anything typed by hand is kept.
    /// </summary>
    public static bool ShouldClear(string newText, string? currentText)
    {
        if (newText.Length > 0)
            return false;
        string current = (currentText ?? string.Empty).Trim();
        return string.Equals(current, MakeBuy.MakeText, StringComparison.OrdinalIgnoreCase)
            || string.Equals(current, MakeBuy.BuyText, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The message after the command: counts, columns added, cells cleared, and failures (lists and cells apart).</summary>
    public static string Summary(int partsLists, int columnsAdded, int make, int buy, int blank, int cleared, int failedLists, int failedCells)
    {
        string text = $"Parts lists updated: {partsLists}. Make: {make}, Buy: {buy}, left blank: {blank}.";
        if (cleared > 0)
            text += $" Cleared an out-of-date Make/Buy text from {cleared} cell{(cleared == 1 ? string.Empty : "s")}.";
        if (columnsAdded > 0)
            text += $" A Make/Buy column was added where it was missing ({columnsAdded}).";
        if (failedLists > 0)
            text += $" {failedLists} parts list{(failedLists == 1 ? string.Empty : "s")} could not get a Make/Buy column and {(failedLists == 1 ? "was" : "were")} skipped.";
        if (failedCells > 0)
            text += $" {failedCells} cell{(failedCells == 1 ? string.Empty : "s")} could not be written; see the log.";
        return text;
    }
}
