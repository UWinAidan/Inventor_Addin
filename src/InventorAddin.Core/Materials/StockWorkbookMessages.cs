namespace InventorAddin.Core.Materials;

/// <summary>
/// The messages for a stock workbook that cannot be read, worded as <see cref="Drawing.NotesWorkbookMessages"/> words the notes
/// workbook's: each names the path and whether it is the starter workbook or the one set in Settings, and says how to fix it.
/// </summary>
public static class StockWorkbookMessages
{
    private const string StarterFix = "Reinstall the add-in, or set the stock workbook in Settings.";

    /// <summary>The workbook file does not exist.</summary>
    public static string Missing(LibraryPath workbook)
    {
        ArgumentNullException.ThrowIfNull(workbook);

        return workbook.IsStarter
            ? $"The starter stock workbook was not found at {workbook.Path}. {StarterFix}"
            : $"The stock workbook set in Settings was not found at {workbook.Path}. Check the stock workbook path in Settings.";
    }

    /// <summary>The add-in's own folder, which holds the starter workbook, could not be found.</summary>
    public static string AddinFolderUnknown(string? reason)
    {
        var why = (reason ?? "").Trim().TrimEnd('.');
        var detail = why.Length == 0 ? "" : $" ({why})";
        return $"The add-in's folder could not be found{detail}, so the starter stock workbook cannot be read. " +
               "Set the stock workbook in Settings, or reinstall the add-in.";
    }

    /// <summary>The workbook exists but could not be read: Excel has it open, or it is not an <c>.xlsx</c> file.</summary>
    public static string Unreadable(LibraryPath workbook, string? reason)
    {
        ArgumentNullException.ThrowIfNull(workbook);

        var which = workbook.IsStarter ? "the starter stock workbook" : "the stock workbook set in Settings";
        var why = (reason ?? "").Trim();
        var detail = why.Length == 0 ? "" : $" ({why.TrimEnd('.')})";
        var fix = workbook.IsStarter
            ? StarterFix
            : "Close it in Excel, check that it is an .xlsx file, or check the stock workbook path in Settings.";
        return $"Could not read {which} at {workbook.Path}{detail}. {fix}";
    }
}
