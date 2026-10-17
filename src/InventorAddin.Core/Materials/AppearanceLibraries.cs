namespace InventorAddin.Core.Materials;

/// <summary>A library Inventor has loaded, as the appearance search sees it.</summary>
/// <param name="InternalName">The library's internal name (a GUID for Autodesk's own libraries, spike 067).</param>
/// <param name="DisplayName">The library's display name.</param>
public sealed record LoadedLibrary(string InternalName, string DisplayName);

/// <summary>
/// The search order of the libraries an appearance is looked up in (task 074; <see cref="AppearanceCandidate.LibraryRank"/>):
/// the Autodesk Appearance Library first, then the other libraries Inventor loaded, then the material library the
/// add-in opened.
/// </summary>
public static class AppearanceLibraries
{
    /// <summary>The Autodesk Appearance Library's internal name (spike 067).</summary>
    public const string AppearanceLibraryInternalName = "314DE259-5443-4621-BFBD-1730C6CC9AE9";

    /// <summary>Its display name, used only when no library has <see cref="AppearanceLibraryInternalName"/>.</summary>
    public const string AppearanceLibraryDisplayName = "Autodesk Appearance Library";

    public const int AppearanceLibraryRank = 0;
    public const int LoadedLibraryRank = 1;
    public const int OpenedLibraryRank = 2;

    /// <summary>
    /// The rank of each library, in the order given. The library whose internal name is
    /// <paramref name="openedInternalName"/> is <see cref="OpenedLibraryRank"/>. The library with
    /// <see cref="AppearanceLibraryInternalName"/> is <see cref="AppearanceLibraryRank"/>; when none has it, every
    /// library named <see cref="AppearanceLibraryDisplayName"/> is. Everything else is <see cref="LoadedLibraryRank"/>.
    /// Internal and display names are compared ignoring case and surrounding spaces; braces round a GUID are ignored.
    /// </summary>
    public static IReadOnlyList<int> Rank(IReadOnlyList<LoadedLibrary> libraries, string? openedInternalName)
    {
        ArgumentNullException.ThrowIfNull(libraries);

        var opened = Key(openedInternalName);
        var byInternalName = libraries.Any(l => Key(l.InternalName) == Key(AppearanceLibraryInternalName));

        return libraries
            .Select(l =>
            {
                if (opened.Length > 0 && Key(l.InternalName) == opened)
                {
                    return OpenedLibraryRank;
                }

                var isAppearanceLibrary = byInternalName
                    ? Key(l.InternalName) == Key(AppearanceLibraryInternalName)
                    : string.Equals((l.DisplayName ?? "").Trim(), AppearanceLibraryDisplayName, StringComparison.OrdinalIgnoreCase);
                return isAppearanceLibrary ? AppearanceLibraryRank : LoadedLibraryRank;
            })
            .ToList()
            .AsReadOnly();
    }

    private static string Key(string? internalName) =>
        (internalName ?? "").Trim().Trim('{', '}').ToUpperInvariant();
}
