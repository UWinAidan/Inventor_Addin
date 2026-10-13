namespace InventorAddin.Core.Materials;

/// <summary>
/// The messages for a material library that cannot be read (spec 03, "Add-in side": a clear message when the file is
/// missing or unreadable). Each names the path and whether it is the starter library or the one set in Settings.
/// </summary>
public static class MaterialLibraryMessages
{
    /// <summary>The library file does not exist.</summary>
    public static string Missing(LibraryPath library)
    {
        ArgumentNullException.ThrowIfNull(library);

        return library.IsStarter
            ? $"The starter material library was not found at {library.Path}. Reinstall the add-in, or set a material library in Settings."
            : $"The material library set in Settings was not found at {library.Path}. Check the path in Settings.";
    }

    /// <summary>Inventor could not open the library file.</summary>
    /// <param name="reason">The error Inventor gave, or null.</param>
    public static string OpenFailed(LibraryPath library, string? reason)
    {
        ArgumentNullException.ThrowIfNull(library);

        var which = library.IsStarter ? "the starter material library" : "the material library set in Settings";
        var why = (reason ?? "").Trim();
        var detail = why.Length == 0 ? "" : $" ({why.TrimEnd('.')})";
        var fix = library.IsStarter
            ? "Reinstall the add-in, or set a material library in Settings."
            : "Check the path in Settings.";
        return $"Inventor could not open {which} at {library.Path}{detail}. {fix}";
    }

    /// <summary>The library opened but holds no material that could be read.</summary>
    public static string Empty(LibraryPath library)
    {
        ArgumentNullException.ThrowIfNull(library);

        var which = library.IsStarter ? "The starter material library" : "The material library set in Settings";
        return $"{which} at {library.Path} has no materials that could be read.";
    }
}
