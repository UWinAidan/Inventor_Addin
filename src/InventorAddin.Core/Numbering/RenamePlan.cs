using System.Globalization;
using InventorAddin.Core.Logging;
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

    /// <summary>Spec 02 question 18 (Default): a name with a comma is refused when the new file name has no part number.</summary>
    public const string CommaWithoutNumberMessage =
        "This file has no part number, so its new name will not start with one and a comma cannot be used: the part " +
        "name is read from after the first comma, and the text before it would be lost.";

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
    /// name <c>{number},{new_name}</c> with the number from <see cref="NumberToKeep"/>, or <c>{new_name}</c> when there is
    /// none, built by <see cref="PartFileName.Build"/>. Throws <see cref="ArgumentException"/> for a blank path, a file
    /// without an extension, or a name <see cref="PartFileName.ValidateName"/> refuses.
    /// </summary>
    /// <param name="registeredNumber">The file's Part Number when the register holds it (<c>RegisterStore.HasNumber</c>); otherwise null.</param>
    public static string NewFileName(string fullFileName, string newName, PartNumber? registeredNumber = null)
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

        return folder + PartFileName.Build(NumberToKeep(path, registeredNumber), newName, oldName[dot..]);
    }

    /// <summary>
    /// Null when <paramref name="typed"/> can be the new name of <paramref name="fullFileName"/>, otherwise a message for
    /// the first problem: the name (<see cref="PartFileName.ValidateName"/>), a comma when the new file name will have no
    /// number (<see cref="NumberToKeep"/>; <see cref="CommaWithoutNumberMessage"/>), the path length
    /// (<see cref="PartFileName.ValidatePath"/>), then a name that gives the same file (<see cref="SameFileMessage"/>).
    /// Whether the new file already exists is the caller's check. A blank <paramref name="fullFileName"/> gives
    /// <see cref="NotSavedMessage"/>.
    /// </summary>
    /// <param name="registeredNumber">As for <see cref="NewFileName"/>.</param>
    public static string? ValidateNewName(string? fullFileName, string? typed, PartNumber? registeredNumber = null)
    {
        if (string.IsNullOrWhiteSpace(fullFileName))
        {
            return NotSavedMessage;
        }

        var nameProblem = PartFileName.ValidateName(typed);
        if (nameProblem is not null)
        {
            return nameProblem;
        }

        if (NumberToKeep(fullFileName, registeredNumber) is null
            && PartFileName.NormaliseName(typed).Contains(PartFileName.NumberSeparator))
        {
            return CommaWithoutNumberMessage;
        }

        var newPath = NewFileName(fullFileName, typed!, registeredNumber);
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

/// <summary>A rename to make.</summary>
/// <param name="OldFullFileName">The open file's current full path.</param>
/// <param name="NewFullFileName">Its new full path (<see cref="RenamePlan.NewFileName"/>).</param>
/// <param name="NewName">The new part name, normalised (<see cref="PartFileName.NormaliseName"/>), with spaces.</param>
public sealed record RenameRequest(string OldFullFileName, string NewFullFileName, string NewName);

/// <summary>One file Rename changed, or tried to.</summary>
/// <param name="FullFileName">The file.</param>
/// <param name="Problem">Why it was not updated; null when it was updated and saved.</param>
public sealed record RenameFileResult(string FullFileName, string? Problem = null)
{
    public bool Updated => Problem is null;
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

    /// <summary>True when something did not go as planned: a file not updated or a note that is a problem.</summary>
    public bool HasProblems => !Renamed || FailedCount > 0 || Notes.Any(n => n.IsProblem);
}

/// <summary>
/// What <see cref="PartRenamer"/> needs from Inventor. Spike 032 (question 4) settled the method; the add-in supplies it,
/// tests use a fake.
/// </summary>
public interface IRenameTarget
{
    /// <summary>
    /// Saves the open document under <paramref name="newFullFileName"/> (<c>SaveAs(newPath, false)</c>). The old file stays
    /// on disk; open documents that hold it now point at the new file and are modified. Throws when it cannot.
    /// </summary>
    void SaveAs(string newFullFileName);

    /// <summary>
    /// Replaces each reference to <paramref name="oldFullFileName"/> in <paramref name="fullFileName"/> with
    /// <paramref name="newFullFileName"/> and saves it, opening it invisibly and closing it again when it is not open in
    /// Inventor. True when it referenced the old file and was saved; false when it did not reference it. Throws when it
    /// cannot be opened, updated or saved.
    /// </summary>
    bool UpdateReferences(string fullFileName, string oldFullFileName, string newFullFileName);

    /// <summary>
    /// Saves each open document that <see cref="SaveAs"/> pointed at <paramref name="newFullFileName"/> and left
    /// modified. One result per document, with the reason for each that could not be saved.
    /// </summary>
    IReadOnlyList<RenameFileResult> SaveRepointedDocuments(string newFullFileName);

    /// <summary>Sets the renamed document's Description to <paramref name="partName"/> and saves it. Throws when it cannot.</summary>
    void WriteDescription(string partName);

    /// <summary>Deletes a file from disk. Throws when it cannot.</summary>
    void DeleteFile(string fullFileName);
}

/// <summary>
/// The Rename steps (spec 02, "Commands: Rename"; spike 032 question 4), in this order: save the open file under its new
/// name, which repoints open assemblies; replace the reference in each candidate file that uses the old file, saving
/// each; save the open documents the rename changed; set Description to the new name; delete the old file only when
/// every file was updated; update the register entry when the file has a registered number. Every outcome goes into
/// the <see cref="RenameResult"/>.
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

        // 2. The files that are not open, and the open ones too: replace the reference and save.
        var toSearch = filesToSearch
            .Where(f => !SamePath(f, oldPath) && !SamePath(f, newPath))
            .ToList();
        for (var i = 0; i < toSearch.Count; i++)
        {
            var file = toSearch[i];
            progress?.Invoke(RenamePlan.ProgressText(i + 1, toSearch.Count, file));
            try
            {
                if (_target.UpdateReferences(file, oldPath, newPath))
                {
                    files.Add(new RenameFileResult(file));
                    _log.Info($"Rename: '{file}' updated.");
                }
            }
            catch (Exception ex)
            {
                files.Add(new RenameFileResult(file, ex.Message));
                _log.Warn($"Rename: '{file}' could not be updated: {ex.Message}");
            }
        }

        // 3. The open documents SaveAs repointed: save them.
        var openFilesFailed = false;
        progress?.Invoke(SavingOpenFilesProgress);
        try
        {
            foreach (var saved in _target.SaveRepointedDocuments(newPath))
            {
                if (files.Any(f => SamePath(f.FullFileName, saved.FullFileName)))
                {
                    continue;
                }

                files.Add(saved);
                if (saved.Updated)
                    _log.Info($"Rename: open file '{saved.FullFileName}' saved.");
                else
                    _log.Warn($"Rename: open file '{saved.FullFileName}' could not be saved: {saved.Problem}");
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

        // 5. The old file goes last, and only when nothing still uses it: every file found was updated, and every
        //    search folder was searched.
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

        if (!someFailed && !someSkipped)
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
            $"{files.Count(f => !f.Updated)} not updated, old file {(oldDeleted ? "deleted" : "kept")}.");

        return new RenameResult(request, true, oldDeleted, descriptionWritten, files.AsReadOnly(), notes.AsReadOnly());
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
