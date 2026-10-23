using InventorAddin.Core.Settings;

namespace InventorAddin.Core.Materials;

/// <summary>
/// A library file the add-in reads, and whether it is the starter file shipped with the add-in, so a message about
/// a missing or unreadable file can say which one it was.
/// </summary>
public sealed record LibraryPath(string Path, bool IsStarter);

/// <summary>
/// Where the material library and the finish list (spec 03, "Add-in side") and the hole workbook (spec 09) are read
/// from, and the preferred materials list. A blank setting means the starter file in the
/// <see cref="StarterFolderName"/> folder beside the add-in, so a plain download works (Aidan, 2026-10-12).
/// </summary>
public static class LibraryPaths
{
    /// <summary>The folder beside the add-in that holds the starter files. The build copies <c>libraries/</c> there.</summary>
    public const string StarterFolderName = "libraries";

    /// <summary>The starter material library's file name.</summary>
    public const string StarterMaterialLibraryFileName = "StarterMaterials.adsklib";

    /// <summary>The starter finish list's file name.</summary>
    public const string StarterFinishListFileName = "finishes.csv";

    /// <summary>The starter hole workbook's file name (spec 09, "The workbook").</summary>
    public const string StarterHoleWorkbookFileName = "AWB_Holes.xlsx";

    /// <summary>The material library's file extension, compared ignoring case.</summary>
    public const string MaterialLibraryExtension = ".adsklib";

    /// <summary>The finish list's file extension, compared ignoring case.</summary>
    public const string FinishListExtension = ".csv";

    /// <summary>The hole workbook's file extension, compared ignoring case.</summary>
    public const string HoleWorkbookExtension = ".xlsx";

    /// <summary>
    /// The material library to read: <see cref="AddinSettings.MaterialLibraryPath"/> trimmed when it is not blank,
    /// otherwise the starter library in <paramref name="addinFolder"/>. Whether the file exists is not checked.
    /// </summary>
    /// <param name="addinFolder">The folder <c>InventorAddin.dll</c> runs from.</param>
    public static LibraryPath MaterialLibrary(AddinSettings settings, string addinFolder) =>
        Resolve((settings ?? throw new ArgumentNullException(nameof(settings))).MaterialLibraryPath, addinFolder, StarterMaterialLibraryFileName);

    /// <summary>
    /// The finish list to read: <see cref="AddinSettings.FinishListPath"/> trimmed when it is not blank, otherwise
    /// the starter list in <paramref name="addinFolder"/>. Whether the file exists is not checked.
    /// </summary>
    /// <param name="addinFolder">The folder <c>InventorAddin.dll</c> runs from.</param>
    public static LibraryPath FinishList(AddinSettings settings, string addinFolder) =>
        Resolve((settings ?? throw new ArgumentNullException(nameof(settings))).FinishListPath, addinFolder, StarterFinishListFileName);

    /// <summary>
    /// The hole workbook to read: <see cref="AddinSettings.HoleWorkbookPath"/> trimmed when it is not blank, otherwise
    /// the starter workbook in <paramref name="addinFolder"/>. Whether the file exists is not checked.
    /// </summary>
    /// <param name="addinFolder">The folder <c>InventorAddin.dll</c> runs from.</param>
    public static LibraryPath HoleWorkbook(AddinSettings settings, string addinFolder) =>
        Resolve((settings ?? throw new ArgumentNullException(nameof(settings))).HoleWorkbookPath, addinFolder, StarterHoleWorkbookFileName);

    /// <summary>Whether <paramref name="name"/> is in the preferred materials, ignoring case and surrounding spaces.</summary>
    public static bool IsPreferred(AddinSettings settings, string? name)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var trimmed = name?.Trim() ?? string.Empty;
        return trimmed.Length > 0 && settings.PreferredMaterials.Any(m => Matches(m, trimmed));
    }

    /// <summary>
    /// A copy of <paramref name="settings"/> with <paramref name="name"/> added to the preferred materials, or
    /// removed when it is already there (ignoring case and surrounding spaces; every matching entry is removed). The
    /// name is added trimmed, at the end. The window's star calls this and saves the result straight away (spec 03,
    /// question 11). <paramref name="settings"/> is not changed.
    /// </summary>
    /// <exception cref="ArgumentException">The name is null or blank.</exception>
    public static AddinSettings TogglePreferred(AddinSettings settings, string name)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
            throw new ArgumentException("A material name is required.", nameof(name));

        var preferred = settings.PreferredMaterials;
        var updated = preferred.Any(m => Matches(m, trimmed))
            ? preferred.Where(m => !Matches(m, trimmed)).ToArray()
            : preferred.Append(trimmed).ToArray();

        return settings with { PreferredMaterials = updated };
    }

    private static bool Matches(string entry, string trimmedName) =>
        string.Equals(entry.Trim(), trimmedName, StringComparison.OrdinalIgnoreCase);

    private static LibraryPath Resolve(string? setting, string addinFolder, string starterFileName)
    {
        if (string.IsNullOrWhiteSpace(addinFolder))
            throw new ArgumentException("The add-in folder is required.", nameof(addinFolder));

        var trimmed = setting?.Trim() ?? string.Empty;
        return trimmed.Length > 0
            ? new LibraryPath(trimmed, IsStarter: false)
            : new LibraryPath(System.IO.Path.Combine(addinFolder, StarterFolderName, starterFileName), IsStarter: true);
    }
}
