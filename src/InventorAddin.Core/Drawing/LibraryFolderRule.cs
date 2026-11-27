namespace InventorAddin.Core.Drawing;

/// <summary>
/// The rule for "is this file in a library folder", shared by Batch Print and the parts list Make/Buy rule. Folders are the
/// project's library paths, resolved to absolute with <see cref="ResolveProjectFolder"/>.
/// </summary>
public static class LibraryFolderRule
{
    private const char Bs = '\\';
    private const char Slash = '/';
    private const char Colon = ':';

    /// <summary>
    /// True when <paramref name="file"/> is inside one of <paramref name="folders"/> at any depth, ignoring case and slash
    /// direction. Only whole folder names match (<c>C:\Lib</c> does not contain <c>C:\Library\a.ipt</c>). A null file or
    /// list gives false; blank entries and a bare slash are skipped.
    /// </summary>
    public static bool IsUnder(string? file, IEnumerable<string>? folders)
    {
        if (string.IsNullOrWhiteSpace(file) || folders is null)
            return false;

        string p = Key(file);
        foreach (string folder in folders)
        {
            if (string.IsNullOrWhiteSpace(folder))
                continue;
            string f = Key(folder).TrimEnd(Bs) + Bs;
            if (f.Length > 1 && p.StartsWith(f, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>
    /// A project path as an absolute folder: a rooted path (drive letter, UNC or leading slash) is kept; a relative one is taken
    /// from the folder of <paramref name="projectFileFullName"/> (the .ipj), with "." and ".." segments resolved. Works on Windows
    /// paths on any platform.
    /// </summary>
    public static string ResolveProjectFolder(string? projectFileFullName, string? folder)
    {
        string f = (folder ?? string.Empty).Trim().Replace(Slash, Bs);
        if (f.Length == 0)
            return string.Empty;
        bool rooted = f.StartsWith(Bs) || (f.Length >= 2 && f[1] == Colon);
        string combined = f;
        if (!rooted)
        {
            string project = (projectFileFullName ?? string.Empty).Trim().Replace(Slash, Bs);
            int slash = project.LastIndexOf(Bs);
            combined = (slash >= 0 ? project[..(slash + 1)] : string.Empty) + f;
        }

        string prefix = combined.StartsWith(@"\\") ? @"\\" : string.Empty;
        var parts = new List<string>();
        foreach (string part in combined[prefix.Length..].Split(Bs, StringSplitOptions.RemoveEmptyEntries))
        {
            if (part == ".")
                continue;
            if (part == ".." && parts.Count > 0 && !parts[^1].EndsWith(Colon))
                parts.RemoveAt(parts.Count - 1);
            else if (part != "..")
                parts.Add(part);
        }

        string joined = string.Join(Bs, parts);
        return combined.StartsWith(Bs) && prefix.Length == 0 ? "\\" + joined : prefix + joined;
    }

    private static string Key(string path) => path.Trim().Replace(Slash, Bs);
}
