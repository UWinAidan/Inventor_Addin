using InventorAddin.Core.Materials;
using InventorAddin.Core.Settings;

namespace InventorAddin.Core.Drawing;

/// <summary>
/// Task 239: where Build Print Template also copies the templates it made, and what its done message says about it.
/// Create Print reads the starter copy beside the running add-in, so a template saved only to the chosen folder
/// would not take effect until the add-in was rebuilt and redeployed.
/// </summary>
public static class PrintTemplatePublish
{
    /// <summary>
    /// The starter templates folder beside the add-in (<c>libraries\Templates</c> in <paramref name="addinFolder"/>),
    /// or null when it is the same folder as <paramref name="chosenFolder"/> and no copy is needed.
    /// </summary>
    public static string? CopyTarget(string chosenFolder, string addinFolder)
    {
        if (string.IsNullOrWhiteSpace(chosenFolder))
            throw new ArgumentException("The chosen folder is required.", nameof(chosenFolder));
        if (string.IsNullOrWhiteSpace(addinFolder))
            throw new ArgumentException("The add-in folder is required.", nameof(addinFolder));

        var starter = Path.Combine(addinFolder, LibraryPaths.StarterFolderName, LibraryPaths.PrintTemplateFolderName);
        return SameFolder(chosenFolder, starter) ? null : starter;
    }

    /// <summary>The Settings print template paths that are not blank, trimmed. Create Print uses these, not the starter copy.</summary>
    public static IReadOnlyList<string> SettingsOverrides(AddinSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return new[] { settings.PrintTemplateMetricPath, settings.PrintTemplateImperialPath }
            .Select(p => p?.Trim() ?? string.Empty)
            .Where(p => p.Length > 0)
            .ToArray();
    }

    /// <summary>
    /// The lines added to the done message: where the copies went (or why none were made), each copy that failed, and,
    /// when a Settings template path is set, that Create Print uses that path rather than the starter copy.
    /// </summary>
    /// <param name="chosenFolder">The folder the templates were saved to.</param>
    /// <param name="copyTarget">The result of <see cref="CopyTarget"/>.</param>
    /// <param name="copyFailures">One text per copy that failed (file name and reason); empty when all copied.</param>
    public static IReadOnlyList<string> Report(
        string chosenFolder, string? copyTarget, IReadOnlyList<string> copyFailures, AddinSettings settings)
    {
        ArgumentNullException.ThrowIfNull(copyFailures);

        var lines = new List<string>();
        if (copyTarget == null)
            lines.Add($"Saved in {chosenFolder}, which is the folder beside the add-in that Create Print reads, so no copy was needed.");
        else if (copyFailures.Count == 0)
            lines.Add($"Saved in {chosenFolder} and copied to {copyTarget}, which Create Print reads, so the new templates take effect at once.");
        else
        {
            lines.Add($"Saved in {chosenFolder}, but the copy to {copyTarget} did not finish, so Create Print may still use the old template:");
            lines.AddRange(copyFailures.Select(f => "  " + f));
        }

        var overrides = SettingsOverrides(settings);
        if (overrides.Count > 0)
            lines.Add("A print template path is set in Settings (" + string.Join("; ", overrides) + "). Create Print uses that path, not the copy beside the add-in.");

        return lines;
    }

    private static bool SameFolder(string a, string b) =>
        string.Equals(Normalise(a), Normalise(b), StringComparison.OrdinalIgnoreCase);

    private static string Normalise(string path) =>
        Path.GetFullPath(path.Trim()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
}
