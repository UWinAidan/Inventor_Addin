using InventorAddin.Core.PartProperties;
using InventorAddin.Core.Settings;

namespace InventorAddin.Core.ViewModels;

/// <summary>
/// The one-line summaries a collapsed Settings section shows beside its heading (Aidan, 2026-10-17), built from the
/// values being edited, so a summary always matches the boxes under it. Each lists the section's values in the order
/// of its rows, separated by commas.
/// </summary>
public static class SettingsSummaries
{
    public const string Separator = ", ";

    public const string NoDefaultDesigner = "No default designer";
    public const string NoRegisterFolder = "No shared register";
    public const string NoWorkFolder = "No work folder";
    public const string StarterLibrary = "starter library";
    public const string StarterFinishList = "starter finish list";
    public const string StarterHoleData = "starter hole data";
    public const string AwbHoleCommand = "AWB Hole";
    public const string InventorHoleCommand = "Inventor's Hole";
    public const string DeveloperToolsShown = "Developer tools shown";
    public const string DeveloperToolsHidden = "Developer tools hidden";

    /// <summary>
    /// General: the default designer, how many other names, the units for new files. For example
    /// "A. Brown, 2 other names, Metric".
    /// </summary>
    public static string General(string? defaultDesigner, string? otherNames, string units)
    {
        var designer = PropertyValues.NormaliseText(defaultDesigner);
        int names = (otherNames ?? string.Empty).Split(',').Count(n => PropertyValues.NormaliseText(n).Length > 0);
        return string.Join(Separator, designer.Length > 0 ? designer : NoDefaultDesigner, Count(names, "other name", "no other names"), units);
    }

    /// <summary>Part numbers: the register folder, or "No shared register" when blank.</summary>
    public static string PartNumbers(string? registerFolder)
    {
        var folder = PropertyValues.NormaliseText(registerFolder);
        return folder.Length > 0 ? folder : NoRegisterFolder;
    }

    /// <summary>Folders: the work folder and how many other folders. For example "C:\CAD_TEST, 2 other folders".</summary>
    public static string Folders(string? workFolder, IReadOnlyCollection<string> otherFolders)
    {
        ArgumentNullException.ThrowIfNull(otherFolders);
        var folder = PropertyValues.NormaliseText(workFolder);
        return string.Join(Separator, folder.Length > 0 ? folder : NoWorkFolder, Count(otherFolders.Count, "other folder", "no other folders"));
    }

    /// <summary>
    /// Material and finish: each file's name, or "starter library" and "starter finish list" when blank. For example
    /// "starter library, starter finish list" or "Materials.adsklib, finishes.csv".
    /// </summary>
    public static string MaterialFinish(string? materialLibraryPath, string? finishListPath) =>
        string.Join(Separator, FileNameOr(materialLibraryPath, StarterLibrary), FileNameOr(finishListPath, StarterFinishList));

    /// <summary>
    /// Holes: the hole workbook's file name, or "starter hole data" when blank, then which Hole command is in use. For
    /// example "starter hole data, AWB Hole" or "AWB_Holes.xlsx, Inventor's Hole".
    /// </summary>
    public static string Holes(string? holeWorkbookPath, bool useInventorHoleCommand) =>
        string.Join(Separator, FileNameOr(holeWorkbookPath, StarterHoleData), useInventorHoleCommand ? InventorHoleCommand : AwbHoleCommand);

    /// <summary>Developer: whether developer tools are shown.</summary>
    public static string Developer(bool showDeveloperTools) =>
        showDeveloperTools ? DeveloperToolsShown : DeveloperToolsHidden;

    private static string Count(int count, string singular, string none) => count switch
    {
        0 => none,
        1 => $"1 {singular}",
        _ => $"{count} {singular}s",
    };

    // Only the file name, so a long path does not push the rest off the line. Windows separators are split by hand:
    // Path.GetFileName on Linux (the cloud tests) does not treat a backslash as one.
    private static string FileNameOr(string? path, string blank)
    {
        var text = PropertyValues.NormaliseText(path);
        if (text.Length == 0)
            return blank;

        int slash = text.LastIndexOfAny(new[] { '\\', '/' });
        var name = slash >= 0 ? text[(slash + 1)..] : text;
        return name.Length > 0 ? name : text;
    }
}
