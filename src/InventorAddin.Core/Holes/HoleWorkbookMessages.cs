using InventorAddin.Core.Materials;

namespace InventorAddin.Core.Holes;

/// <summary>
/// The messages for a hole workbook that cannot be read (spec 09, "The workbook"), worded as
/// <see cref="MaterialLibraryMessages"/> words the finish list's: each names the path and whether it is the starter
/// workbook or the one set in Settings, and says how to fix it.
/// </summary>
public static class HoleWorkbookMessages
{
    private const string StarterFix = "Reinstall the add-in, or set the hole data in Settings.";

    /// <summary>The workbook file does not exist.</summary>
    public static string Missing(LibraryPath workbook)
    {
        ArgumentNullException.ThrowIfNull(workbook);

        return workbook.IsStarter
            ? $"The starter hole workbook was not found at {workbook.Path}. {StarterFix}"
            : $"The hole workbook set in Settings was not found at {workbook.Path}. Check the hole data path in Settings.";
    }

    /// <summary>
    /// The add-in's own folder, which holds the starter workbook, could not be found (as
    /// <see cref="MaterialLibraryMessages.AddinFolderUnknown"/> words it for the material library and finish list).
    /// </summary>
    /// <param name="reason">The error that gave, or null.</param>
    public static string AddinFolderUnknown(string? reason)
    {
        var why = (reason ?? "").Trim().TrimEnd('.');
        var detail = why.Length == 0 ? "" : $" ({why})";
        return $"The add-in's folder could not be found{detail}, so the starter hole workbook cannot be read. " +
               "Set the hole data in Settings, or reinstall the add-in.";
    }

    /// <summary>
    /// The note for an optional tab the workbook does not have (<see cref="HoleWorkbook.MissingOptionalTabs"/>; spec 09,
    /// "The workbook"), for the window to show where the tab's sizes would be. Not a workbook problem: a workbook made
    /// before the tab existed loads without it.
    /// </summary>
    /// <param name="tab">The tab's name, for example <see cref="HoleWorkbookReader.TaperTappedTab"/>.</param>
    public static string MissingOptionalTab(string tab)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tab);

        return $"The hole workbook has no \"{tab}\" tab, so it gives no {tab} sizes. " +
               "Copy the tab from the starter workbook into yours to add them.";
    }

    /// <summary>
    /// The workbook exists but could not be read: Excel has it open, or it is not an <c>.xlsx</c> file.
    /// </summary>
    /// <param name="reason">The error the read gave, or null.</param>
    public static string Unreadable(LibraryPath workbook, string? reason)
    {
        ArgumentNullException.ThrowIfNull(workbook);

        var which = workbook.IsStarter ? "the starter hole workbook" : "the hole workbook set in Settings";
        var why = (reason ?? "").Trim();
        var detail = why.Length == 0 ? "" : $" ({why.TrimEnd('.')})";
        var fix = workbook.IsStarter
            ? StarterFix
            : "Close it in Excel, check that it is an .xlsx file, or check the hole data path in Settings.";
        return $"Could not read {which} at {workbook.Path}{detail}. {fix}";
    }
}
