namespace InventorAddin.Core.Materials;

/// <summary>
/// Filtering the materials of a library for the Material and Finish window (spec 03): by category, preferred only,
/// and search text; and whether a material name is in the library, for Part Properties' warning.
/// </summary>
public static class MaterialFilter
{
    /// <summary>The Category choice that shows every material. Always first in <see cref="Categories"/>.</summary>
    public const string All = "All";

    /// <summary>
    /// The Category dropdown's choices: <see cref="All"/> first, then the distinct non-empty categories (trimmed,
    /// compared ignoring case, the first spelling kept) sorted ignoring case. A material with no category only shows
    /// under All. A category spelled "All" is not listed twice.
    /// </summary>
    public static IReadOnlyList<string> Categories(IEnumerable<LibraryMaterial> materials)
    {
        ArgumentNullException.ThrowIfNull(materials);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { All };
        var categories = new List<string>();
        foreach (var material in materials)
        {
            var category = (material.Category ?? "").Trim();
            if (category.Length > 0 && seen.Add(category))
            {
                categories.Add(category);
            }
        }

        categories.Sort(StringComparer.OrdinalIgnoreCase);
        categories.Insert(0, All);
        return categories.AsReadOnly();
    }

    /// <summary>
    /// The materials to list, sorted by display name ignoring case.
    /// <paramref name="category"/> is <see cref="All"/> (or blank) for every material, otherwise a category compared
    /// ignoring case and surrounding spaces. With <paramref name="preferredOnly"/>, only materials whose display name
    /// is in <paramref name="preferred"/> (ignoring case and surrounding spaces) are kept. <paramref name="search"/>
    /// keeps display names containing the trimmed text, ignoring case; blank keeps everything.
    /// </summary>
    public static IReadOnlyList<LibraryMaterial> Apply(
        IEnumerable<LibraryMaterial> materials,
        string? category,
        bool preferredOnly,
        IEnumerable<string> preferred,
        string? search)
    {
        ArgumentNullException.ThrowIfNull(materials);
        ArgumentNullException.ThrowIfNull(preferred);

        var wantedCategory = (category ?? "").Trim();
        var anyCategory = wantedCategory.Length == 0
            || string.Equals(wantedCategory, All, StringComparison.OrdinalIgnoreCase);
        var preferredNames = new HashSet<string>(
            preferred.Where(p => p is not null).Select(p => p.Trim()),
            StringComparer.OrdinalIgnoreCase);
        var text = (search ?? "").Trim();

        return materials
            .Where(m => anyCategory
                || string.Equals((m.Category ?? "").Trim(), wantedCategory, StringComparison.OrdinalIgnoreCase))
            .Where(m => !preferredOnly || preferredNames.Contains(Name(m)))
            .Where(m => text.Length == 0 || Name(m).Contains(text, StringComparison.OrdinalIgnoreCase))
            .OrderBy(Name, StringComparer.OrdinalIgnoreCase)
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// True when a material with this display name is in the library, ignoring case and surrounding spaces. A part's
    /// copy of a library material keeps the library's display name (spike 067), so this is how a part's material is
    /// checked. A blank name is not in the library.
    /// </summary>
    public static bool IsInLibrary(string? materialName, IEnumerable<LibraryMaterial> materials)
    {
        ArgumentNullException.ThrowIfNull(materials);

        var name = (materialName ?? "").Trim();
        if (name.Length == 0)
        {
            return false;
        }

        return materials.Any(m => string.Equals(Name(m), name, StringComparison.OrdinalIgnoreCase));
    }

    private static string Name(LibraryMaterial material) => (material.DisplayName ?? "").Trim();
}
