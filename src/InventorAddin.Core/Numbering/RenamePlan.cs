using System.Globalization;
using InventorAddin.Core.Drawing;
using InventorAddin.Core.Logging;
using InventorAddin.Core.Models;
using InventorAddin.Core.Numbering.MassUpdate;

namespace InventorAddin.Core.Numbering;

/// <summary>
/// What Rename does before it touches Inventor (spec 02, "Commands: Rename"; task 047): the new file name, whether a
/// typed name can be used, which folders and files are searched for references, and the confirmation text.
/// </summary>
/// <remarks>
/// The folders searched are the CAD folders from Settings (Aidan, spec 02 question 23): the work folder and every other
/// CAD folder, plus the renamed file's own folder when it lies in none of them (Default). Inventor's <c>OldVersions</c>
/// folders are skipped (Default).
/// </remarks>
public static class RenamePlan
{
    /// <summary>The kinds of file that can reference a part or assembly: assemblies, drawings and presentations.</summary>
    public static IReadOnlyList<string> CandidateExtensions { get; } =
        Array.AsReadOnly(new[] { ".iam", ".idw", ".dwg", ".ipn" });

    /// <summary>
    /// How long a folder has to answer before it counts as not reachable (as the register's probe, spike 032 question 8:
    /// a dropped drive can block for up to 42 s).
    /// </summary>
    public static readonly TimeSpan FolderProbeTimeout = TimeSpan.FromSeconds(3);

    /// <summary>How long listing one reachable folder may take before it is skipped (Default).</summary>
    public static readonly TimeSpan FolderListTimeout = TimeSpan.FromSeconds(60);

    public const string SameFileMessage =
        "That is the file's current name. Windows does not tell file names apart by case, so a change of case alone " +
        "is not a rename.";

    /// <summary>
    /// Spec 02 question 18 (Default): when the new file name has no part number, a name whose text after its last comma
    /// is itself a part number is refused, since the file name would read back as having that number.
    /// </summary>
    public const string NameEndsWithNumberMessage =
        "This file has no part number, so its new name must not end with a comma and a part number: the file would " +
        "read as numbered with it, and that part of the name would be lost.";

    public const string NotSavedMessage = "Save the file before renaming it.";

    private static readonly char[] FolderSeparators = ['\\', '/'];

    /// <summary>
    /// The number the renamed file's name keeps (spec 02, "Commands: Rename"): <paramref name="registeredNumber"/>, the
    /// file's Part Number when the register holds it; otherwise the number the old file name carries
    /// (<see cref="PartFileName.TryReadNumber"/>); otherwise none. So a file numbered by Generate or Mass Update, whose
    /// name has no number yet, gets one when it is renamed.
    /// </summary>
    public static PartNumber? NumberToKeep(string? fullFileName, PartNumber? registeredNumber) =>
        registeredNumber ?? (PartFileName.TryReadNumber(fullFileName, out var n) ? n : null);

    /// <summary>
    /// The renamed file's full path: the same folder and extension as <paramref name="fullFileName"/>, and the file
    /// name <c>{new_name},{number}</c> with the number from <see cref="NumberToKeep"/>, or <c>{new_name}</c> when there is
    /// none, built by <see cref="PartFileName.Build"/>. Throws <see cref="ArgumentException"/> for a blank path, a file
    /// without an extension, or a name <see cref="PartFileName.ValidateName"/> refuses.
    /// </summary>
    /// <param name="registeredNumber">The file's Part Number when the register holds it (<c>RegisterStore.HasNumber</c>); otherwise null.</param>
    /// <param name="kind">
    /// The file's kind: an assembly's new name gets <see cref="PartFileName.AssemblyPrefix"/> (<see cref="PartFileName.ForKind"/>).
    /// </param>
    public static string NewFileName(
        string fullFileName,
        string newName,
        PartNumber? registeredNumber = null,
        DocumentKind kind = DocumentKind.Part)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullFileName);
        ArgumentNullException.ThrowIfNull(newName);

        var path = fullFileName.Trim();
        var cut = path.LastIndexOfAny(FolderSeparators) + 1;
        var folder = path[..cut];
        var oldName = path[cut..];
        var dot = oldName.LastIndexOf('.');
        if (dot < 0)
        {
            throw new ArgumentException($"The file \"{path}\" has no extension.", nameof(fullFileName));
        }

        return folder + PartFileName.Build(
            NumberToKeep(path, registeredNumber), PartFileName.ForKind(newName, kind), oldName[dot..]);
    }

    /// <summary>
    /// Null when <paramref name="typed"/> can be the new name of <paramref name="fullFileName"/>, otherwise a message for
    /// the first problem: the name (<see cref="PartFileName.ValidateName"/>), a name that would read back as numbered
    /// when the new file name will have no number (<see cref="NumberToKeep"/>; <see cref="NameEndsWithNumberMessage"/>;
    /// any other comma is allowed), the path length
    /// (<see cref="PartFileName.ValidatePath"/>), then a name that gives the same file (<see cref="SameFileMessage"/>).
    /// Whether the new file already exists is the caller's check. A blank <paramref name="fullFileName"/> gives
    /// <see cref="NotSavedMessage"/>.
    /// </summary>
    /// <param name="registeredNumber">As for <see cref="NewFileName"/>.</param>
    /// <param name="kind">As for <see cref="NewFileName"/>; the name is checked by <see cref="PartFileName.ValidateNameForKind"/>.</param>
    public static string? ValidateNewName(
        string? fullFileName,
        string? typed,
        PartNumber? registeredNumber = null,
        DocumentKind kind = DocumentKind.Part)
    {
        if (string.IsNullOrWhiteSpace(fullFileName))
        {
            return NotSavedMessage;
        }

        var nameProblem = PartFileName.ValidateNameForKind(typed, kind);
        if (nameProblem is not null)
        {
            return nameProblem;
        }

        var newPath = NewFileName(fullFileName, typed!, registeredNumber, kind);
        if (NumberToKeep(fullFileName, registeredNumber) is null && PartFileName.TryReadNumber(newPath, out _))
        {
            return NameEndsWithNumberMessage;
        }

        var cut = newPath.LastIndexOfAny(FolderSeparators) + 1;
        var pathProblem = PartFileName.ValidatePath(newPath[..cut], newPath[cut..]);
        if (pathProblem is not null)
        {
            return pathProblem;
        }

        return string.Equals(newPath, fullFileName.Trim(), StringComparison.OrdinalIgnoreCase) ? SameFileMessage : null;
    }

    /// <summary>
    /// Why Rename stops before it starts when open documents that use the file have unsaved changes (Default): Rename
    /// saves each file that uses the renamed one, and would save those changes with it.
    /// </summary>
    public static string UnsavedOpenFilesMessage(IReadOnlyList<string> fullFileNames)
    {
        ArgumentNullException.ThrowIfNull(fullFileNames);
        return "These open files use this file and have unsaved changes. Rename saves every file that uses it, so " +
               "save or close them first:" +
               string.Concat(fullFileNames.Select(f => Environment.NewLine + "- " + FileNameOnly(f)));
    }

    /// <summary>The message when the new file name is taken.</summary>
    public static string TargetExistsMessage(string fileName) =>
        $"A file named {fileName} already exists in this folder. Choose another name.";

    /// <summary>
    /// The folders Rename searches: <paramref name="workFolder"/>, then each of <paramref name="otherCadFolders"/>, each
    /// trimmed, with blanks dropped, and with duplicates and folders inside another listed folder removed (ignoring case
    /// and the direction of the slashes). When <paramref name="renamedFile"/> lies in none of them, its own folder is
    /// added last (Default, spec 02 question 23). Folders are returned as typed, without a trailing separator except at
    /// a drive root.
    /// </summary>
    public static IReadOnlyList<string> SearchFolders(
        string? workFolder,
        IEnumerable<string?>? otherCadFolders,
        string? renamedFile)
    {
        var listed = new List<string>();
        foreach (var folder in new[] { workFolder }.Concat(otherCadFolders ?? []))
        {
            var tidy = TidyFolder(folder);
            if (tidy.Length > 0)
            {
                listed.Add(tidy);
            }
        }

        var kept = new List<string>();
        for (var i = 0; i < listed.Count; i++)
        {
            var folder = listed[i];
            var coveredByEarlier = kept.Any(k => IsSameOrInside(folder, k));
            var insideAnother = listed.Where((other, j) => j != i)
                .Any(other => IsInside(folder, other));
            if (!coveredByEarlier && !insideAnother)
            {
                kept.Add(folder);
            }
        }

        var fileFolder = FolderOf(renamedFile);
        if (fileFolder.Length > 0 && !kept.Any(k => IsSameOrInside(fileFolder, k)))
        {
            kept.Add(fileFolder);
        }

        return kept.AsReadOnly();
    }

    /// <summary>
    /// True when <paramref name="path"/> is an assembly, drawing or presentation (<see cref="CandidateExtensions"/>,
    /// ignoring case) and no folder on its path is named <see cref="ScanFolder.OldVersionsFolderName"/>.
    /// </summary>
    public static bool IsCandidate(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        if (!CandidateExtensions.Any(ext => path.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        var segments = path.Split(FolderSeparators);
        return !segments.Take(segments.Length - 1)
            .Any(s => string.Equals(s, ScanFolder.OldVersionsFolderName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// The files of <paramref name="files"/> that could reference a renamed file (<see cref="IsCandidate"/>), each once
    /// (ignoring case), sorted by path ignoring case.
    /// </summary>
    public static IReadOnlyList<string> CandidateFiles(IEnumerable<string?> files)
    {
        ArgumentNullException.ThrowIfNull(files);

        var result = files
            .Where(IsCandidate)
            .Select(f => f!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        result.Sort(StringComparer.OrdinalIgnoreCase);
        return result.AsReadOnly();
    }

    /// <summary>
    /// The candidate files under <paramref name="folders"/>, including subfolders, skipping <c>OldVersions</c> folders,
    /// each once, sorted. A folder that does not exist is skipped, as is a subfolder that cannot be read. Synchronous;
    /// the add-in uses <see cref="ListFilesToSearch"/>, which adds the timeouts.
    /// </summary>
    public static IReadOnlyList<string> FilesToSearch(IEnumerable<string> folders)
    {
        ArgumentNullException.ThrowIfNull(folders);

        var files = new List<string>();
        foreach (var folder in folders)
        {
            if (!string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder))
            {
                files.AddRange(CandidateFilesIn(folder));
            }
        }

        return CandidateFiles(files);
    }

    /// <summary>
    /// <see cref="FilesToSearch"/> with each folder checked and listed on a background task: a folder that does not
    /// answer within <paramref name="probeTimeout"/> (<see cref="FolderProbeTimeout"/> by default) or does not exist is
    /// skipped as not reachable; one whose listing takes longer than <paramref name="listTimeout"/>
    /// (<see cref="FolderListTimeout"/>) or fails is skipped with the reason. A background task that does not finish in
    /// time is left to finish on its own.
    /// </summary>
    /// <param name="folderExists">Replaces <see cref="Directory.Exists(string)"/>; for tests.</param>
    /// <param name="listFolder">Replaces the listing of one folder; for tests.</param>
    public static RenameSearchList ListFilesToSearch(
        IReadOnlyList<string> folders,
        TimeSpan? probeTimeout = null,
        TimeSpan? listTimeout = null,
        Func<string, bool>? folderExists = null,
        Func<string, IReadOnlyList<string>>? listFolder = null)
    {
        ArgumentNullException.ThrowIfNull(folders);

        var exists = folderExists ?? Directory.Exists;
        var list = listFolder ?? CandidateFilesIn;
        var probeWait = probeTimeout ?? FolderProbeTimeout;
        var listWait = listTimeout ?? FolderListTimeout;

        var files = new List<string>();
        var searched = new List<string>();
        var skipped = new List<SkippedFolder>();

        foreach (var folder in folders)
        {
            if (string.IsNullOrWhiteSpace(folder))
            {
                continue;
            }

            var probe = Task.Run(() => exists(folder));
            if (!Finished(probe, probeWait) || !probe.Result)
            {
                skipped.Add(new SkippedFolder(folder, SkippedFolder.NotReachableReason));
                continue;
            }

            var listing = Task.Run(() => list(folder));
            if (!Finished(listing, listWait))
            {
                skipped.Add(new SkippedFolder(folder, SkippedFolder.TooSlowReason(listWait)));
                continue;
            }

            if (listing.IsFaulted)
            {
                var reason = listing.Exception?.GetBaseException().Message ?? "it could not be read";
                skipped.Add(new SkippedFolder(folder, $"it could not be read: {reason}"));
                continue;
            }

            files.AddRange(listing.Result);
            searched.Add(folder);
        }

        return new RenameSearchList(CandidateFiles(files), searched.AsReadOnly(), skipped.AsReadOnly());
    }

    /// <summary>
    /// The drawings Rename renames with the model (spec 02, "Commands: Rename"; task 059): the files of
    /// <paramref name="files"/> that are prints of the old model by Create Print's rule (<see cref="PrintPlan.IsPrintOf"/>:
    /// an <c>.idw</c> or <c>.dwg</c> in the model's folder or its <c>Prints</c> folder, in any case, whose file name
    /// without extension equals the old model's, ignoring case), each once, in the order given. Each comes with its new
    /// path: the same folder, the new model's file name without extension, and the drawing's own extension. A print
    /// beside the model is renamed where it is (Default); it is not moved into <c>Prints</c>. Whether the drawing uses
    /// the model is the target's check.
    /// </summary>
    public static IReadOnlyList<DrawingRename> DrawingsOfModel(
        string oldModelFullFileName,
        string newModelFullFileName,
        IEnumerable<string?> files)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(oldModelFullFileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(newModelFullFileName);
        ArgumentNullException.ThrowIfNull(files);

        var newStem = Stem(newModelFullFileName);
        return files
            .Where(f => PrintPlan.IsPrintOf(oldModelFullFileName, f))
            .Select(f => f!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(f =>
            {
                var path = f.Trim();
                var cut = path.LastIndexOfAny(FolderSeparators) + 1;
                var name = path[cut..];
                return new DrawingRename(f, path[..cut] + newStem + name[name.LastIndexOf('.')..]);
            })
            .ToList()
            .AsReadOnly();
    }

    /// <summary>The confirmation Rename asks before it changes anything.</summary>
    public static string ConfirmationMessage(RenameRequest request, RenameSearchList search)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(search);

        var nl = Environment.NewLine;
        var text = $"Rename {FileNameOnly(request.OldFullFileName)} to {FileNameOnly(request.NewFullFileName)}?" + nl + nl;

        text += search.Files.Count == 0
            ? "No assemblies, drawings or presentations were found in the CAD folders, so only the file is renamed."
            : $"{Count(search.Files.Count, "assembly, drawing or presentation", "assemblies, drawings and presentations")} " +
              $"in {Count(search.Folders.Count, "folder", "folders")} will be searched, and each one that uses the file " +
              "is updated and saved. Expect a second or two for each file that uses it.";

        var drawings = DrawingsOfModel(request.OldFullFileName, request.NewFullFileName, search.Files);
        if (drawings.Count > 0)
        {
            text += nl + nl + "Drawings named after it that use it are renamed to match, unless a file with the new name " +
                    "already exists:" +
                    string.Concat(drawings.Select(d =>
                        nl + $"- {FileNameOnly(d.OldFullFileName)} to {FileNameOnly(d.NewFullFileName)}"));
        }

        if (search.Folders.Count > 0)
        {
            text += nl + nl + "Searched:" + string.Concat(search.Folders.Select(f => nl + "- " + f));
        }

        if (search.Skipped.Count > 0)
        {
            text += nl + nl + "Not searched, so files there that use it will not be updated, and the old file will be kept:" +
                    string.Concat(search.Skipped.Select(s => nl + $"- {s.Folder}: {s.Reason}"));
        }

        return text + nl + nl + "Files that are changed are saved. Rename cannot be undone with Undo.";
    }

    /// <summary>The progress line while candidate <paramref name="index"/> (1-based) of <paramref name="count"/> is checked.</summary>
    public static string ProgressText(int index, int count, string fullFileName) =>
        string.Create(CultureInfo.InvariantCulture, $"Checking {index} of {count}: {FileNameOnly(fullFileName)}");

    /// <summary>The file name without its folder.</summary>
    public static string FileNameOnly(string? fullFileName)
    {
        if (string.IsNullOrEmpty(fullFileName))
        {
            return string.Empty;
        }

        return fullFileName[(fullFileName.LastIndexOfAny(FolderSeparators) + 1)..];
    }

    /// <summary>
    /// The new name of an occurrence of a renamed file (spec 02, "Commands: Rename"; Aidan, 2026-10-11): when
    /// <paramref name="occurrenceName"/> is the old file name without its folder and extension, a colon and one or more
    /// digits (ignoring case), the new file name without folder and extension, a colon and the same digits. Otherwise
    /// null: an occurrence given any other name is left alone. <c>BRACKET,AWB-10005:2</c> becomes
    /// <c>BRACKET_2,AWB-10005:2</c> when <c>BRACKET,AWB-10005.ipt</c> is renamed to <c>BRACKET_2,AWB-10005.ipt</c>.
    /// </summary>
    public static string? RenamedOccurrence(string? occurrenceName, string? oldFullFileName, string? newFullFileName)
    {
        var oldStem = Stem(oldFullFileName);
        var newStem = Stem(newFullFileName);
        if (string.IsNullOrEmpty(occurrenceName) || oldStem.Length == 0 || newStem.Length == 0)
        {
            return null;
        }

        var prefix = oldStem + ":";
        if (occurrenceName.Length <= prefix.Length
            || !occurrenceName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var digits = occurrenceName[prefix.Length..];
        return digits.All(char.IsAsciiDigit) ? newStem + ":" + digits : null;
    }

    /// <summary>
    /// The file path in an Inventor full document name: the name without a trailing model state in angle brackets
    /// (<c>C:\Cad\a.ipt&lt;Default&gt;</c> gives <c>C:\Cad\a.ipt</c>), trimmed. Empty for null.
    /// </summary>
    public static string FileOfDocumentName(string? fullDocumentName)
    {
        var name = fullDocumentName?.Trim() ?? string.Empty;
        if (name.EndsWith('>'))
        {
            var open = name.LastIndexOf('<');
            if (open > 0)
            {
                name = name[..open].TrimEnd();
            }
        }

        return name;
    }

    /// <summary>The file name without its folder and extension; empty for a blank path.</summary>
    private static string Stem(string? fullFileName)
    {
        var name = FileNameOnly(fullFileName?.Trim());
        var dot = name.LastIndexOf('.');
        return dot > 0 ? name[..dot] : name;
    }

    /// <summary>The candidate files under one folder, recursively, skipping <c>OldVersions</c>. Throws when it does not exist.</summary>
    private static IReadOnlyList<string> CandidateFilesIn(string root)
    {
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
            files.AddRange(Directory.EnumerateFiles(folder, "*", options).Where(IsCandidate));

            foreach (var sub in Directory.EnumerateDirectories(folder, "*", options))
            {
                if (!string.Equals(Path.GetFileName(sub), ScanFolder.OldVersionsFolderName, StringComparison.OrdinalIgnoreCase))
                {
                    folders.Push(sub);
                }
            }
        }

        return files;
    }

    private static bool Finished(Task task, TimeSpan timeout)
    {
        try
        {
            return task.Wait(timeout);
        }
        catch (AggregateException)
        {
            // Faulted: finished. The caller reads the fault from the task.
            return true;
        }
    }

    private static string Count(int n, string one, string many) =>
        string.Create(CultureInfo.InvariantCulture, $"{n} {(n == 1 ? one : many)}");

    /// <summary>Trimmed, without trailing separators, except where that would leave a drive root (<c>C:</c>) or nothing.</summary>
    private static string TidyFolder(string? folder)
    {
        var trimmed = folder?.Trim() ?? string.Empty;
        var stripped = trimmed.TrimEnd(FolderSeparators);
        return stripped.Length == 0 || stripped.EndsWith(':') ? trimmed : stripped;
    }

    /// <summary>The folder of a file path as written, without the trailing separator; <c>""</c> when there is none.</summary>
    private static string FolderOf(string? fullFileName)
    {
        var path = fullFileName?.Trim() ?? string.Empty;
        var cut = path.LastIndexOfAny(FolderSeparators);
        return cut < 0 ? string.Empty : TidyFolder(path[..(cut + 1)]);
    }

    private static string Key(string folder) => folder.Replace('/', '\\').TrimEnd('\\');

    private static bool IsSameOrInside(string folder, string parent) =>
        string.Equals(Key(folder), Key(parent), StringComparison.OrdinalIgnoreCase) || IsInside(folder, parent);

    private static bool IsInside(string folder, string parent)
    {
        var child = Key(folder);
        var root = Key(parent);
        return child.Length > root.Length + 1
            && child.StartsWith(root + "\\", StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>A folder Rename did not search, and why.</summary>
public sealed record SkippedFolder(string Folder, string Reason)
{
    public const string NotReachableReason = "it was not found or did not answer";

    public static string TooSlowReason(TimeSpan timeout) =>
        string.Create(CultureInfo.InvariantCulture, $"listing it took longer than {timeout.TotalSeconds:0} seconds");
}

/// <summary>What <see cref="RenamePlan.ListFilesToSearch"/> found.</summary>
/// <param name="Files">The candidate files, each once, sorted.</param>
/// <param name="Folders">The folders that were listed.</param>
/// <param name="Skipped">The folders that were not, with the reason.</param>
public sealed record RenameSearchList(
    IReadOnlyList<string> Files,
    IReadOnlyList<string> Folders,
    IReadOnlyList<SkippedFolder> Skipped);

/// <summary>A drawing Rename renames with the model (<see cref="RenamePlan.DrawingsOfModel"/>).</summary>
/// <param name="OldFullFileName">The drawing's path, as found.</param>
/// <param name="NewFullFileName">Its new path: the same folder and extension, and the model's new file name.</param>
public sealed record DrawingRename(string OldFullFileName, string NewFullFileName);

/// <summary>A rename to make.</summary>
/// <param name="OldFullFileName">The open file's current full path.</param>
/// <param name="NewFullFileName">Its new full path (<see cref="RenamePlan.NewFileName"/>).</param>
/// <param name="NewName">The new part name, normalised (<see cref="PartFileName.NormaliseName"/>), with spaces.</param>
public sealed record RenameRequest(string OldFullFileName, string NewFullFileName, string NewName);

/// <summary>One file Rename changed, or tried to.</summary>
/// <param name="FullFileName">The file.</param>
/// <param name="Problem">Why it was not updated; null when it was updated and saved.</param>
/// <param name="OccurrencesRenamed">How many of its occurrences were renamed (<see cref="RenamePlan.RenamedOccurrence"/>).</param>
/// <param name="OccurrenceProblems">The occurrences that should have been renamed and could not be; null for none.</param>
/// <param name="RenamedTo">
/// For a drawing of the model saved under the model's new name (<see cref="RenamePlan.DrawingsOfModel"/>), its new full
/// path; null otherwise.
/// </param>
public sealed record RenameFileResult(
    string FullFileName,
    string? Problem = null,
    int OccurrencesRenamed = 0,
    IReadOnlyList<OccurrenceProblem>? OccurrenceProblems = null,
    string? RenamedTo = null)
{
    public bool Updated => Problem is null;
}

/// <summary>An occurrence Rename should have renamed and could not.</summary>
/// <param name="OccurrenceName">Its name, unchanged.</param>
/// <param name="Reason">Why.</param>
public sealed record OccurrenceProblem(string OccurrenceName, string Reason);

/// <summary>What <see cref="IRenameTarget.UpdateReferences"/> did to a file that uses the renamed one.</summary>
/// <param name="OccurrencesRenamed">How many of its occurrences were renamed.</param>
/// <param name="OccurrenceProblems">The occurrences that could not be renamed; null for none.</param>
public sealed record RenameFileUpdate(int OccurrencesRenamed = 0, IReadOnlyList<OccurrenceProblem>? OccurrenceProblems = null);

/// <summary>
/// What <see cref="IRenameTarget.UpdateAndRenameDrawing"/> did to a drawing of the model that uses it. The drawing's
/// reference to the model was replaced and saved in every case.
/// </summary>
/// <param name="NotRenamedReason">
/// Why the drawing could not be saved under its new name; null when it was. When set, it keeps its old name and uses the
/// renamed model.
/// </param>
/// <param name="OldFileNotDeletedReason">Saved under its new name, but its old file could not be deleted: why.</param>
/// <param name="NotReopenedReason">Saved under its new name, but it was open and could not be opened again: why.</param>
public sealed record DrawingRenameUpdate(
    string? NotRenamedReason = null,
    string? OldFileNotDeletedReason = null,
    string? NotReopenedReason = null)
{
    public bool Renamed => NotRenamedReason is null;
}

/// <summary>A line in Rename's result that is not about one referencing file.</summary>
public sealed record RenameNote(string Text, bool IsProblem);

/// <summary>What <see cref="PartRenamer.Run"/> did.</summary>
/// <param name="Request">The rename asked for.</param>
/// <param name="Renamed">True when the file was saved under its new name. False means nothing was changed.</param>
/// <param name="OldFileDeleted">True when the old file was deleted.</param>
/// <param name="DescriptionWritten">True when Description was set to the new name and saved.</param>
/// <param name="Files">Each referencing file updated, or that failed, in the order handled.</param>
/// <param name="Notes">Everything else to report, in order.</param>
public sealed record RenameResult(
    RenameRequest Request,
    bool Renamed,
    bool OldFileDeleted,
    bool DescriptionWritten,
    IReadOnlyList<RenameFileResult> Files,
    IReadOnlyList<RenameNote> Notes)
{
    public int UpdatedCount => Files.Count(f => f.Updated);

    public int FailedCount => Files.Count(f => !f.Updated);

    /// <summary>How many occurrences were renamed, over every file.</summary>
    public int OccurrencesRenamed => Files.Sum(f => f.OccurrencesRenamed);

    /// <summary>How many drawings of the model were saved under its new name.</summary>
    public int DrawingsRenamed => Files.Count(f => f.RenamedTo is not null);

    /// <summary>True when something did not go as planned: a file not updated or a note that is a problem.</summary>
    public bool HasProblems => !Renamed || FailedCount > 0 || Notes.Any(n => n.IsProblem);
}

/// <summary>
/// What <see cref="PartRenamer"/> needs from Inventor. Spikes 032 (question 4) and 052 (question 1) settled the method;
/// the add-in supplies it, tests use a fake.
/// </summary>
public interface IRenameTarget
{
    /// <summary>
    /// Saves the open document under <paramref name="newFullFileName"/> (<c>SaveAs(newPath, false)</c>). The old file stays
    /// on disk; open documents that hold it now point at the new file in memory, but are not marked modified (spike 052),
    /// so nothing is written for them until they are marked and saved. Throws when it cannot.
    /// </summary>
    void SaveAs(string newFullFileName);

    /// <summary>
    /// Updates <paramref name="fullFileName"/> when it uses the renamed file, opening it invisibly and closing it again
    /// when it is not open in Inventor. It uses the file when one of its direct references is
    /// <paramref name="oldFullFileName"/> or <paramref name="newFullFileName"/> (an open document repointed by
    /// <see cref="SaveAs"/>). Each reference to the old file is replaced with the new one, each occurrence of the file
    /// that <see cref="RenamePlan.RenamedOccurrence"/> renames is renamed, then the document is marked modified and
    /// saved. Null when it does not use the file. Throws when it cannot be opened, updated or saved.
    /// </summary>
    RenameFileUpdate? UpdateReferences(string fullFileName, string oldFullFileName, string newFullFileName);

    /// <summary>
    /// Updates and saves, as <see cref="UpdateReferences"/> does, each document open in Inventor (visible or not) that
    /// uses the renamed file and is not in <paramref name="alreadyHandled"/>, leaving out never-saved documents and the
    /// renamed one. These are the open documents outside the search that <see cref="SaveAs"/> repointed in memory. One
    /// result per document, with the reason for each that could not be saved.
    /// </summary>
    IReadOnlyList<RenameFileResult> SaveRepointedDocuments(
        string oldFullFileName,
        string newFullFileName,
        IReadOnlyCollection<string> alreadyHandled);

    /// <summary>
    /// The documents open in Inventor, visible or not, other than the renamed one, that still have a direct reference to
    /// <paramref name="oldFullFileName"/>. Asked before the old file is deleted.
    /// </summary>
    IReadOnlyList<string> OpenDocumentsReferencing(string oldFullFileName);

    /// <summary>True when a file exists on disk at <paramref name="fullFileName"/>, or a document is open in Inventor under it.</summary>
    bool FileExists(string fullFileName);

    /// <summary>
    /// For a drawing of the model (<see cref="RenamePlan.DrawingsOfModel"/>; spike 052, section 5): when it uses the
    /// renamed file, updates and saves it under its old name as <see cref="UpdateReferences"/> does, so it never breaks
    /// whatever follows; then marks it modified and saves it as <paramref name="newDrawingFullFileName"/>
    /// (<c>SaveAs(path, false)</c>, keeping its extension), closes it (an open drawing keeps its old file locked until it
    /// is closed), deletes the old drawing file, and opens it again, visibly, when it was open. Null when it does not use
    /// the renamed file. Throws only when its reference could not be updated and saved; a failure after that is
    /// returned in the <see cref="DrawingRenameUpdate"/>.
    /// </summary>
    DrawingRenameUpdate? UpdateAndRenameDrawing(
        string drawingFullFileName,
        string newDrawingFullFileName,
        string oldFullFileName,
        string newFullFileName);

    /// <summary>Sets the renamed document's Description to <paramref name="partName"/> and saves it. Throws when it cannot.</summary>
    void WriteDescription(string partName);

    /// <summary>Deletes a file from disk. Throws when it cannot.</summary>
    void DeleteFile(string fullFileName);
}

/// <summary>
/// The Rename steps (spec 02, "Commands: Rename"; spikes 032 question 4 and 052 question 1), in this order: save the
/// open file under its new name, which repoints open documents in memory; update and save each candidate file that uses
/// the file, renaming its occurrences, and saving each drawing of the model under the model's new name (task 059); update
/// and save the other open documents that use it; set Description to the new
/// name; delete the old file only when every file was updated, every folder searched, and no open document still
/// references it; update the register entry when the file has a registered number. Every outcome goes into the
/// <see cref="RenameResult"/>.
/// </summary>
/// <remarks>
/// A failure part way keeps the old file, so a file that could not be updated still opens; the result says which
/// files were updated and which were not. Rename cannot be undone with Undo: each changed file is saved.
/// </remarks>
public sealed class PartRenamer
{
    public const string SavingOpenFilesProgress = "Saving the open files that use it";

    private readonly IRenameTarget _target;
    private readonly RegisterStore? _store;
    private readonly ILog _log;

    /// <param name="target">Inventor.</param>
    /// <param name="store">The session's register, for the entry update; null when the file has no registered number.</param>
    /// <param name="log">Where each step is recorded.</param>
    public PartRenamer(IRenameTarget target, RegisterStore? store = null, ILog? log = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        _target = target;
        _store = store;
        _log = log ?? NullLog.Instance;
    }

    public static string RenameFailedMessage(string reason) =>
        $"The file could not be saved under its new name: {reason} Nothing was changed.";

    public static string OldFileKeptMessage(string oldFullFileName) =>
        $"The old file {RenamePlan.FileNameOnly(oldFullFileName)} was kept, because not every file could be updated: " +
        "those still use it and open as before. Update them by hand, then delete the old file.";

    /// <summary>
    /// Parent decision (Default): the old file is kept whenever a search folder was skipped, since files there that use
    /// it were not updated.
    /// </summary>
    public static string OldFileKeptForSkippedFoldersMessage(string oldFullFileName, IReadOnlyList<SkippedFolder> skipped) =>
        $"The old file {RenamePlan.FileNameOnly(oldFullFileName)} was kept, because these folders were not searched and " +
        "files there may still use it: " + string.Join("; ", skipped.Select(f => $"{f.Folder} ({f.Reason})")) +
        ". When those folders can be reached, update any file there that uses it by hand, then delete the old file.";

    public static string OldFileNotDeletedMessage(string oldFullFileName, string reason) =>
        $"The old file {RenamePlan.FileNameOnly(oldFullFileName)} could not be deleted: {reason} Every file found uses the " +
        "new name, so it can be deleted by hand.";

    public static string DescriptionFailedMessage(string reason) =>
        $"Description could not be set to the new name: {reason} Apply in Part Properties sets it.";

    public static string OpenFilesFailedMessage(string reason) =>
        $"The open files that use it could not all be saved: {reason}";

    /// <summary>Why the old file is kept when documents open in Inventor still reference it.</summary>
    public static string OldFileStillUsedMessage(string oldFullFileName, IReadOnlyList<string> openFiles)
    {
        ArgumentNullException.ThrowIfNull(openFiles);
        return $"The old file {RenamePlan.FileNameOnly(oldFullFileName)} was kept, because these open files still use " +
               "it: " + string.Join("; ", openFiles.Select(RenamePlan.FileNameOnly)) +
               ". Update them by hand, then delete the old file.";
    }

    public static string OldFileCheckFailedMessage(string oldFullFileName, string reason) =>
        $"The old file {RenamePlan.FileNameOnly(oldFullFileName)} was kept, because the open files could not be checked " +
        $"for it: {reason} Delete it by hand once nothing uses it.";

    public static string OccurrenceNotRenamedMessage(string fullFileName, OccurrenceProblem problem)
    {
        ArgumentNullException.ThrowIfNull(problem);
        return $"The occurrence {problem.OccurrenceName} in {RenamePlan.FileNameOnly(fullFileName)} could not be renamed: " +
               $"{problem.Reason} The file was still updated.";
    }

    /// <summary>A drawing of the model kept its name because the new one is taken; it was still updated.</summary>
    public static string DrawingNameTakenMessage(string drawingFullFileName, string newDrawingFullFileName) =>
        $"The drawing {RenamePlan.FileNameOnly(drawingFullFileName)} was updated but not renamed, because a file named " +
        $"{RenamePlan.FileNameOnly(newDrawingFullFileName)} already exists there. It keeps its name.";

    /// <summary>A drawing of the model was updated under its old name, but could not be saved under the new one.</summary>
    public static string DrawingNotRenamedMessage(string drawingFullFileName, string newDrawingFullFileName, string reason) =>
        $"The drawing {RenamePlan.FileNameOnly(drawingFullFileName)} was updated but could not be renamed to " +
        $"{RenamePlan.FileNameOnly(newDrawingFullFileName)}: {reason} It keeps its name and uses the renamed file.";

    /// <summary>A drawing was renamed, but its old file is still on disk.</summary>
    public static string DrawingOldFileNotDeletedMessage(string drawingFullFileName, string newDrawingFullFileName, string reason) =>
        $"The drawing was renamed to {RenamePlan.FileNameOnly(newDrawingFullFileName)}, but its old file " +
        $"{RenamePlan.FileNameOnly(drawingFullFileName)} could not be deleted: {reason} Both use the renamed file; delete " +
        "the old one by hand.";

    /// <summary>A drawing that was open was renamed, but could not be opened again.</summary>
    public static string DrawingNotReopenedMessage(string newDrawingFullFileName, string reason) =>
        $"The drawing was renamed to {RenamePlan.FileNameOnly(newDrawingFullFileName)} and closed, but could not be " +
        $"opened again: {reason} Open it by hand.";

    /// <summary>Runs the steps.</summary>
    /// <param name="request">The rename.</param>
    /// <param name="filesToSearch">The candidate files (<see cref="RenameSearchList.Files"/>).</param>
    /// <param name="registeredNumber">The file's Part Number when the register holds it; null otherwise.</param>
    /// <param name="progress">Told the step under way, for the window.</param>
    /// <param name="skippedFolders">
    /// The search folders that were not listed (<see cref="RenameSearchList.Skipped"/>). Any at all keeps the old file.
    /// </param>
    public RenameResult Run(
        RenameRequest request,
        IReadOnlyList<string> filesToSearch,
        PartNumber? registeredNumber = null,
        Action<string>? progress = null,
        IReadOnlyList<SkippedFolder>? skippedFolders = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(filesToSearch);

        var files = new List<RenameFileResult>();
        var notes = new List<RenameNote>();
        var oldPath = request.OldFullFileName;
        var newPath = request.NewFullFileName;

        // 1. Save under the new name. Nothing has changed if this fails.
        try
        {
            _target.SaveAs(newPath);
        }
        catch (Exception ex)
        {
            _log.Error($"Rename: '{oldPath}' could not be saved as '{newPath}'. Nothing was changed.", ex);
            notes.Add(new RenameNote(RenameFailedMessage(ex.Message), IsProblem: true));
            return new RenameResult(request, false, false, false, files, notes);
        }

        _log.Info($"Rename: '{oldPath}' saved as '{newPath}'. Searching {filesToSearch.Count} files for references.");

        // 2. Each file found, open or closed: replace the reference, rename occurrences, mark it modified and save. A
        //    drawing of the model is also saved under the model's new name (task 059), in place of the plain update.
        var toSearch = filesToSearch
            .Where(f => !SamePath(f, oldPath) && !SamePath(f, newPath))
            .ToList();
        var drawings = RenamePlan.DrawingsOfModel(oldPath, newPath, toSearch)
            .ToDictionary(d => d.OldFullFileName, d => d.NewFullFileName, StringComparer.OrdinalIgnoreCase);
        var handled = new HashSet<string>(toSearch, StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < toSearch.Count; i++)
        {
            var file = toSearch[i];
            progress?.Invoke(RenamePlan.ProgressText(i + 1, toSearch.Count, file));
            try
            {
                if (drawings.TryGetValue(file, out var newDrawing))
                {
                    UpdateDrawing(file, newDrawing, oldPath, newPath, files, notes, handled);
                }
                else if (_target.UpdateReferences(file, oldPath, newPath) is { } update)
                {
                    AddUpdated(file, update, files, notes);
                }
            }
            catch (Exception ex)
            {
                files.Add(new RenameFileResult(file, ex.Message));
                _log.Warn($"Rename: '{file}' could not be updated: {ex.Message}");
            }
        }

        // 3. The other open documents SaveAs repointed in memory: update and save them too. Every file the search
        //    handled is passed, and each drawing's new name, so none is saved twice.
        var openFilesFailed = false;
        progress?.Invoke(SavingOpenFilesProgress);
        try
        {
            foreach (var saved in _target.SaveRepointedDocuments(oldPath, newPath, handled))
            {
                if (handled.Contains(saved.FullFileName) || files.Any(f => SamePath(f.FullFileName, saved.FullFileName)))
                {
                    continue;
                }

                files.Add(saved);
                if (saved.Updated)
                {
                    _log.Info($"Rename: open file '{saved.FullFileName}' saved, {saved.OccurrencesRenamed} occurrences renamed.");
                    AddOccurrenceNotes(saved, notes);
                }
                else
                {
                    _log.Warn($"Rename: open file '{saved.FullFileName}' could not be saved: {saved.Problem}");
                }
            }
        }
        catch (Exception ex)
        {
            openFilesFailed = true;
            notes.Add(new RenameNote(OpenFilesFailedMessage(ex.Message), IsProblem: true));
            _log.Error("Rename: the open files that use the renamed file could not be saved.", ex);
        }

        // 4. Description follows the new name.
        var descriptionWritten = false;
        try
        {
            _target.WriteDescription(request.NewName);
            descriptionWritten = true;
        }
        catch (Exception ex)
        {
            notes.Add(new RenameNote(DescriptionFailedMessage(ex.Message), IsProblem: true));
            _log.Error("Rename: Description could not be written.", ex);
        }

        // 5. The old file goes last, and only when nothing still uses it: every file found was updated, every search
        //    folder was searched, and no open document still references it.
        var oldDeleted = false;
        var someFailed = files.Any(f => !f.Updated) || openFilesFailed;
        var someSkipped = skippedFolders is { Count: > 0 };
        if (someFailed)
        {
            notes.Add(new RenameNote(OldFileKeptMessage(oldPath), IsProblem: true));
            _log.Warn($"Rename: '{oldPath}' kept, because not every file could be updated.");
        }

        if (someSkipped)
        {
            notes.Add(new RenameNote(OldFileKeptForSkippedFoldersMessage(oldPath, skippedFolders!), IsProblem: true));
            _log.Warn($"Rename: '{oldPath}' kept, because {skippedFolders!.Count} search folders were not searched.");
        }

        var stillUsed = false;
        if (!someFailed && !someSkipped)
        {
            try
            {
                var openUsers = _target.OpenDocumentsReferencing(oldPath);
                if (openUsers.Count > 0)
                {
                    stillUsed = true;
                    notes.Add(new RenameNote(OldFileStillUsedMessage(oldPath, openUsers), IsProblem: true));
                    _log.Warn($"Rename: '{oldPath}' kept, because open files still use it: {string.Join("; ", openUsers)}.");
                }
            }
            catch (Exception ex)
            {
                stillUsed = true;
                notes.Add(new RenameNote(OldFileCheckFailedMessage(oldPath, ex.Message), IsProblem: true));
                _log.Error($"Rename: '{oldPath}' kept, because the open files could not be checked for it.", ex);
            }
        }

        if (!someFailed && !someSkipped && !stillUsed)
        {
            try
            {
                _target.DeleteFile(oldPath);
                oldDeleted = true;
            }
            catch (Exception ex)
            {
                notes.Add(new RenameNote(OldFileNotDeletedMessage(oldPath, ex.Message), IsProblem: true));
                _log.Warn($"Rename: '{oldPath}' could not be deleted: {ex.Message}");
            }
        }

        // 6. The register entry (Default).
        if (registeredNumber is { } number && _store is not null)
        {
            notes.Add(UpdateRegister(number, request));
        }

        _log.Info(
            $"Rename: finished '{oldPath}' -> '{newPath}': {files.Count(f => f.Updated)} updated, " +
            $"{files.Count(f => !f.Updated)} not updated, {files.Sum(f => f.OccurrencesRenamed)} occurrences renamed, " +
            $"{files.Count(f => f.RenamedTo is not null)} drawings renamed, old file {(oldDeleted ? "deleted" : "kept")}.");

        return new RenameResult(request, true, oldDeleted, descriptionWritten, files.AsReadOnly(), notes.AsReadOnly());
    }

    /// <summary>Records a file updated by <see cref="IRenameTarget.UpdateReferences"/>, with its occurrence notes.</summary>
    private void AddUpdated(string file, RenameFileUpdate update, List<RenameFileResult> files, List<RenameNote> notes)
    {
        var result = new RenameFileResult(file, null, update.OccurrencesRenamed, update.OccurrenceProblems);
        files.Add(result);
        _log.Info($"Rename: '{file}' updated, {update.OccurrencesRenamed} occurrences renamed.");
        AddOccurrenceNotes(result, notes);
    }

    /// <summary>
    /// A drawing of the model (task 059). When its new name is free it is updated and saved under it; when the name is
    /// taken, or cannot be checked, it is only updated, as any other file, and keeps its name. A drawing that does not
    /// use the model is left alone. Throws, as <see cref="IRenameTarget.UpdateReferences"/> does, only when its reference
    /// could not be updated; every later failure is a note, since the drawing then uses the renamed file either way.
    /// </summary>
    private void UpdateDrawing(
        string file,
        string newDrawing,
        string oldPath,
        string newPath,
        List<RenameFileResult> files,
        List<RenameNote> notes,
        HashSet<string> handled)
    {
        string? takenNote;
        try
        {
            takenNote = _target.FileExists(newDrawing) ? DrawingNameTakenMessage(file, newDrawing) : null;
        }
        catch (Exception ex)
        {
            takenNote = DrawingNotRenamedMessage(file, newDrawing, ex.Message);
        }

        if (takenNote is not null)
        {
            if (_target.UpdateReferences(file, oldPath, newPath) is { } update)
            {
                AddUpdated(file, update, files, notes);
                notes.Add(new RenameNote(takenNote, IsProblem: true));
                _log.Warn($"Rename: drawing '{file}' updated but not renamed to '{newDrawing}': {takenNote}");
            }

            return;
        }

        if (_target.UpdateAndRenameDrawing(file, newDrawing, oldPath, newPath) is not { } drawing)
        {
            return;
        }

        if (!drawing.Renamed)
        {
            files.Add(new RenameFileResult(file));
            notes.Add(new RenameNote(DrawingNotRenamedMessage(file, newDrawing, drawing.NotRenamedReason!), IsProblem: true));
            _log.Warn($"Rename: drawing '{file}' updated but not renamed to '{newDrawing}': {drawing.NotRenamedReason}");
            return;
        }

        files.Add(new RenameFileResult(file, RenamedTo: newDrawing));
        handled.Add(newDrawing);
        _log.Info($"Rename: drawing '{file}' updated and renamed to '{newDrawing}'.");

        if (drawing.OldFileNotDeletedReason is { } notDeleted)
        {
            notes.Add(new RenameNote(DrawingOldFileNotDeletedMessage(file, newDrawing, notDeleted), IsProblem: true));
            _log.Warn($"Rename: old drawing '{file}' could not be deleted: {notDeleted}");
        }

        if (drawing.NotReopenedReason is { } notReopened)
        {
            notes.Add(new RenameNote(DrawingNotReopenedMessage(newDrawing, notReopened), IsProblem: true));
            _log.Warn($"Rename: drawing '{newDrawing}' could not be opened again: {notReopened}");
        }
    }

    /// <summary>A note, and a log line, for each occurrence of <paramref name="file"/> that could not be renamed.</summary>
    private void AddOccurrenceNotes(RenameFileResult file, List<RenameNote> notes)
    {
        foreach (var problem in file.OccurrenceProblems ?? [])
        {
            notes.Add(new RenameNote(OccurrenceNotRenamedMessage(file.FullFileName, problem), IsProblem: true));
            _log.Warn($"Rename: occurrence '{problem.OccurrenceName}' in '{file.FullFileName}' could not be renamed: {problem.Reason}");
        }
    }

    private RenameNote UpdateRegister(PartNumber number, RenameRequest request)
    {
        try
        {
            var result = _store!.UpdateEntry(number, request.NewFullFileName, request.NewName, request.OldFullFileName);
            if (!result.Found)
            {
                return new RenameNote($"The register does not hold {number}, so no register entry was changed.", IsProblem: true);
            }

            var text = result.QueuedReason is not null
                ? $"Register entry {number} could not be written to the shared register ({result.QueuedReason}). The " +
                  "update is kept on this PC and applied at the next sync."
                : result.Offline
                    ? $"Register entry {number} updated offline; it will be added to the shared register at the next sync."
                    : $"Register entry {number} updated.";
            if (result.Duplicates.Count > 0)
            {
                text += " The register sync found part numbers given to two files; both rows are kept, fix them by hand: " +
                        string.Join("; ", result.Duplicates.Select(d =>
                            $"{d.Number} ('{d.Existing.FilePath}' and '{d.Added.FilePath}')"));
            }

            return new RenameNote(text, IsProblem: result.Duplicates.Count > 0 || result.QueuedReason is not null);
        }
        catch (Exception ex) when (ex is RegisterBusyException or RegisterUnreadableException or IOException or UnauthorizedAccessException)
        {
            _log.Error($"Rename: register entry {number} could not be updated.", ex);
            return new RenameNote(
                $"Register entry {number} could not be updated: {ex.Message} The file is renamed; the register still " +
                "names the old file.",
                IsProblem: true);
        }
    }

    private static bool SamePath(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
