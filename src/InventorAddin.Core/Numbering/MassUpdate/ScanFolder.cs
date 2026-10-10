namespace InventorAddin.Core.Numbering.MassUpdate;

/// <summary>Which files Mass Update reads (spec 02, "Commands: Mass Update").</summary>
public static class ScanFolder
{
    /// <summary>The folder name Inventor keeps old versions in. Folders with this name are not scanned.</summary>
    public const string OldVersionsFolderName = "OldVersions";

    private static readonly string[] Extensions = [NewFileKinds.PartExtension, NewFileKinds.AssemblyExtension];

    /// <summary>
    /// The full path of every <c>.ipt</c> and <c>.iam</c> file under <paramref name="root"/>, including subfolders,
    /// skipping any folder named <see cref="OldVersionsFolderName"/> (ignoring case) and everything below it. Sorted by
    /// path, ignoring case as Windows does. Folders that cannot be read are skipped. Throws
    /// <see cref="DirectoryNotFoundException"/> when <paramref name="root"/> does not exist.
    /// </summary>
    public static IReadOnlyList<string> FilesToScan(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException($"The folder \"{root}\" does not exist.");
        }

        var options = new EnumerationOptions
        {
            RecurseSubdirectories = false,
            IgnoreInaccessible = true,
            MatchCasing = MatchCasing.CaseInsensitive,
        };

        var files = new List<string>();
        var folders = new Stack<string>();
        folders.Push(root);
        while (folders.Count > 0)
        {
            var folder = folders.Pop();

            foreach (var file in Directory.EnumerateFiles(folder, "*", options))
            {
                if (IsInventorModel(file))
                {
                    files.Add(file);
                }
            }

            foreach (var sub in Directory.EnumerateDirectories(folder, "*", options))
            {
                if (!string.Equals(Path.GetFileName(sub), OldVersionsFolderName, StringComparison.OrdinalIgnoreCase))
                {
                    folders.Push(sub);
                }
            }
        }

        files.Sort(StringComparer.OrdinalIgnoreCase);
        return files.AsReadOnly();
    }

    /// <summary>True when <paramref name="path"/> ends in <c>.ipt</c> or <c>.iam</c>, ignoring case.</summary>
    public static bool IsInventorModel(string path) =>
        Extensions.Any(ext => path.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
}
