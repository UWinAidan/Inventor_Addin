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

    /// <summary>
    /// The add-in's own folder could not be found, so the starter files beside it cannot be either (task 075).
    /// </summary>
    /// <param name="reason">The error, or null.</param>
    public static string AddinFolderUnknown(string? reason)
    {
        var why = (reason ?? "").Trim();
        var detail = why.Length == 0 ? "" : $" ({why.TrimEnd('.')})";
        return $"The add-in's folder could not be found{detail}, so the material library and finish list cannot be read.";
    }

    /// <summary>The finish list file does not exist (task 075). Names the path and which list it is.</summary>
    public static string FinishListMissing(LibraryPath finishList)
    {
        ArgumentNullException.ThrowIfNull(finishList);

        return finishList.IsStarter
            ? $"The starter finish list was not found at {finishList.Path}. Reinstall the add-in, or set a finish list in Settings."
            : $"The finish list set in Settings was not found at {finishList.Path}. Check the path in Settings.";
    }

    /// <summary>The finish list file exists but could not be read, for example because Excel has it open.</summary>
    /// <param name="reason">The error the read gave, or null.</param>
    public static string FinishListUnreadable(LibraryPath finishList, string? reason)
    {
        ArgumentNullException.ThrowIfNull(finishList);

        var which = finishList.IsStarter ? "the starter finish list" : "the finish list set in Settings";
        var why = (reason ?? "").Trim();
        var detail = why.Length == 0 ? "" : $" ({why.TrimEnd('.')})";
        var fix = finishList.IsStarter
            ? "Reinstall the add-in, or set a finish list in Settings."
            : "Close it in any other program, or check the path in Settings.";
        return $"Could not read {which} at {finishList.Path}{detail}. {fix}";
    }

    /// <summary>
    /// The log line for one problem in the finish list, naming the file, for example
    /// <c>"Material and Finish: finish list C:\x\finishes.csv: Line 4: Kind \"Plate\" is not ..."</c>. The window logs
    /// each problem once when it opens; the view-model does not log them.
    /// </summary>
    public static string FinishListProblemLogLine(LibraryPath finishList, FinishListProblem problem)
    {
        ArgumentNullException.ThrowIfNull(finishList);
        ArgumentNullException.ThrowIfNull(problem);

        return $"{MaterialFinishTargeting.LogPrefix}finish list {finishList.Path}: {problem}";
    }
}
