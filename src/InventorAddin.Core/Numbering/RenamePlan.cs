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

    /// <summary>The new file name is exactly the current one. A change of case only is a rename (task 061).</summary>
    public const string SameFileMessage = "That is the file's current name. Type a different name.";

    /// <summary>
    /// The start of the temporary file name a change of case only goes through (task 061): it has no comma, so it can
    /// never read as a part file name with a number.
    /// </summary>
    public const string TemporaryNamePrefix = "~RENAME-";

    /// <summary>How many temporary names <see cref="TemporaryFileName"/> tries before it gives up.</summary>
    public const int TemporaryNameAttempts = 10;

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
    /// (<see cref="PartFileName.ValidatePath"/>), then a name that gives exactly the current file name
    /// (<see cref="SameFileMessage"/>). A name that differs from the current one in case only is accepted
    /// (<see cref="IsCaseOnlyChange"/>; Aidan, task 061). Whether the new file already exists is the caller's check,
    /// which a change of case only skips. A blank <paramref name="fullFileName"/> gives
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

        return string.Equals(newPath, fullFileName.Trim(), StringComparison.Ordinal) ? SameFileMessage : null;
    }

    /// <summary>
    /// True when <paramref name="newFullFileName"/> names the same file as <paramref name="oldFullFileName"/> to Windows
    /// (equal ignoring case, after trimming) but is spelled differently: a change of case only (task 061). Such a rename
    /// goes through a temporary name (<see cref="TemporaryFileName"/>), and the new name is not taken by the file itself.
    /// </summary>
    public static bool IsCaseOnlyChange(string? oldFullFileName, string? newFullFileName)
    {
        var oldPath = oldFullFileName?.Trim() ?? string.Empty;
        var newPath = newFullFileName?.Trim() ?? string.Empty;
        return oldPath.Length > 0
            && string.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(oldPath, newPath, StringComparison.Ordinal);
    }

    /// <summary>
    /// The temporary full path a change of case only goes through (task 061): the same folder and extension as
    /// <paramref name="fullFileName"/>, and the file name <see cref="TemporaryNamePrefix"/> followed by a new GUID in 32
    /// digits, which has no comma and so never reads as a part file name. A name <paramref name="exists"/> reports as
    /// taken is skipped; after <see cref="TemporaryNameAttempts"/> taken names it throws <see cref="IOException"/>.
    /// </summary>
    /// <param name="exists">True when a file exists at the path, or a document is open under it.</param>
    /// <param name="newGuid">Replaces <see cref="Guid.NewGuid"/>; for tests.</param>
    public static string TemporaryFileName(string fullFileName, Func<string, bool> exists, Func<Guid>? newGuid = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullFileName);
        ArgumentNullException.ThrowIfNull(exists);

        var path = fullFileName.Trim();
        var cut = path.LastIndexOfAny(FolderSeparators) + 1;
        var name = path[cut..];
        var dot = name.LastIndexOf('.');
        if (dot < 0)
        {
            throw new ArgumentException($"The file \"{path}\" has no extension.", nameof(fullFileName));
        }

        var guid = newGuid ?? Guid.NewGuid;
        for (var i = 0; i < TemporaryNameAttempts; i++)
        {
            var candidate = path[..cut] + TemporaryNamePrefix + guid().ToString("N", CultureInfo.InvariantCulture) + name[dot..];
            if (!exists(candidate))
            {
                return candidate;
            }
        }

        throw new IOException("No free temporary name could be found in the file's folder.");
    }

    /// <summary>True when the file name of <paramref name="fullFileName"/> starts with <see cref="TemporaryNamePrefix"/>, ignoring case.</summary>
    public static bool IsTemporaryFileName(string? fullFileName) =>
        FileNameOnly(fullFileName?.Trim()).StartsWith(TemporaryNamePrefix, StringComparison.OrdinalIgnoreCase);

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

        if (IsCaseOnlyChange(request.OldFullFileName, request.NewFullFileName))
        {
            text += nl + nl + "Only the case of the name changes. Windows treats both names as the same file, so it is " +
                    "renamed in two steps: to a temporary name, then to the new one.";
        }

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
    /// The start of the temporary name an occurrence is given before its new name (task 065, spike 062): letters and a
    /// dash only, with no comma, so it never reads as a file name with a part number.
    /// </summary>
    public const string TemporaryOccurrencePrefix = Branding.ShortName + "RENAME-";

    /// <summary>
    /// The new name of an occurrence of a renamed file (spec 02, "Commands: Rename"; Aidan, 2026-10-11): when
    /// <paramref name="occurrenceName"/> is the old file name without its folder and extension, a colon and one or more
    /// digits (ignoring case), or is any name in the AWB format (Aidan 2026-10-28, task 135), the new file name without
    /// folder and extension, a colon and the same digits. Otherwise null: an occurrence given any other name is left alone. <c>BRACKET,AWB-10005:2</c> becomes
    /// <c>BRACKET_2,AWB-10005:2</c> when <c>BRACKET,AWB-10005.ipt</c> is renamed to <c>BRACKET_2,AWB-10005.ipt</c>.
    /// </summary>
    /// <remarks>
    /// A name that is already the new file name, a colon and digits (ignoring case) also gives the new name (task 065):
    /// in a closed assembly opened invisibly, <c>ComponentOccurrence.Name</c> reads the new file name after
    /// <c>ReplaceReference</c> while the name stored in the file is still the old one (spike 062), so such an occurrence
    /// must be set again.
    /// <para>
    /// Any name in the AWB format (<see cref="IsAwbOccurrenceName"/>) also gives the new name (Aidan 2026-10-28, spec 02
    /// question 27; task 135), so a name left stale by an earlier Save As and Replace is fixed:
    /// <c>BRACKET_V4,AWB-10017:1</c> becomes <c>BRACKET_V6,AWB-10022:1</c> when <c>BRACKET_V5,AWB-10022.ipt</c> is
    /// renamed to <c>BRACKET_V6,AWB-10022.ipt</c>. A name the user gave, not in that format, is still left alone.
    /// Save As and Replace does not use this wider rule (<see cref="RenamedOccurrenceNamedAfterFile"/>).
    /// </para>
    /// </remarks>
    public static string? RenamedOccurrence(string? occurrenceName, string? oldFullFileName, string? newFullFileName) =>
        NewOccurrenceName(occurrenceName, oldFullFileName, newFullFileName, anyAwbName: true);

    /// <summary>
    /// As <see cref="RenamedOccurrence"/>, without the AWB-format rule: only a name that is the old or the new file name
    /// without folder and extension, a colon and digits (ignoring case), gives the new name. Save As and Replace uses
    /// this (task 134; task 135 kept it apart from Rename's wider rule).
    /// </summary>
    public static string? RenamedOccurrenceNamedAfterFile(
        string? occurrenceName, string? oldFullFileName, string? newFullFileName) =>
        NewOccurrenceName(occurrenceName, oldFullFileName, newFullFileName, anyAwbName: false);

    /// <summary>
    /// True when <paramref name="occurrenceName"/> is in the AWB format, <c>NAME,AWB-#####:n</c> (task 135): text before
    /// the last comma that is not empty, a comma, a part number <see cref="PartNumber.TryParse"/> accepts (any number),
    /// a colon and one or more ASCII digits. Case is ignored as in the part number.
    /// </summary>
    public static bool IsAwbOccurrenceName(string? occurrenceName) => AwbNameDigits(occurrenceName) is not null;

    private static string? NewOccurrenceName(
        string? occurrenceName, string? oldFullFileName, string? newFullFileName, bool anyAwbName)
    {
        var oldStem = Stem(oldFullFileName);
        var newStem = Stem(newFullFileName);
        if (string.IsNullOrEmpty(occurrenceName) || oldStem.Length == 0 || newStem.Length == 0)
        {
            return null;
        }

        var digits = DigitsAfter(occurrenceName, oldStem)
                     ?? DigitsAfter(occurrenceName, newStem)
                     ?? (anyAwbName ? AwbNameDigits(occurrenceName) : null);
        return digits is null ? null : newStem + ":" + digits;
    }

    /// <summary>
    /// The digits after the colon when <paramref name="occurrenceName"/> is in the AWB format
    /// (<see cref="IsAwbOccurrenceName"/>); otherwise null. The stem before the last colon is split by
    /// <see cref="PartFileName.SplitStem"/>, the rule file names are read by.
    /// </summary>
    private static string? AwbNameDigits(string? occurrenceName)
    {
        if (string.IsNullOrEmpty(occurrenceName))
        {
            return null;
        }

        var colon = occurrenceName.LastIndexOf(':');
        if (colon <= 0)
        {
            return null;
        }

        var stem = occurrenceName[..colon];
        var digits = DigitsAfter(occurrenceName, stem);
        if (digits is null || PartFileName.SplitStem(stem, out var nameText) is null || nameText.Length == 0)
        {
            return null;
        }

        return digits;
    }

    /// <summary>
    /// The temporary name an occurrence is given before it is set to <paramref name="newName"/> (task 065, spike 062):
    /// setting <c>ComponentOccurrence.Name</c> to the value it already reads changes nothing, so the name is set to this
    /// first. It is <see cref="TemporaryOccurrencePrefix"/> followed by a GUID in 32 digits, then the colon and digits
    /// <paramref name="newName"/> ends with, when it ends with them: <c>AWBRENAME-3f2a…:1</c>. Letters, digits and dashes
    /// only, apart from that suffix.
    /// </summary>
    /// <param name="guid">Replaces <see cref="Guid.NewGuid"/>; for tests.</param>
    public static string TemporaryOccurrenceName(string newName, Guid? guid = null)
    {
        ArgumentNullException.ThrowIfNull(newName);

        var colon = newName.LastIndexOf(':');
        var suffix = colon >= 0 && colon < newName.Length - 1 && newName[(colon + 1)..].All(char.IsAsciiDigit)
            ? newName[colon..]
            : string.Empty;
        return TemporaryOccurrencePrefix + (guid ?? Guid.NewGuid()).ToString("N", CultureInfo.InvariantCulture) + suffix;
    }

    /// <summary>
    /// The text added to an occurrence problem's reason when the occurrence could not be given its new name nor its
    /// original one back (task 065): it says the name it is left under. Empty when <paramref name="leftUnder"/> is blank
    /// or is <paramref name="originalName"/> (exactly; a change of case is reported).
    /// </summary>
    public static string OccurrenceLeftUnderText(string? originalName, string? leftUnder) =>
        string.IsNullOrWhiteSpace(leftUnder) || string.Equals(leftUnder, originalName, StringComparison.Ordinal)
            ? string.Empty
            : $" It could not be given its name back and is now named {leftUnder}.";

    /// <summary>
    /// The digits after <paramref name="stem"/> and a colon when <paramref name="occurrenceName"/> is exactly that
    /// (ignoring case) followed by one or more ASCII digits; otherwise null.
    /// </summary>
    private static string? DigitsAfter(string occurrenceName, string stem)
    {
        var prefix = stem + ":";
        if (occurrenceName.Length <= prefix.Length
            || !occurrenceName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var digits = occurrenceName[prefix.Length..];
        return digits.All(char.IsAsciiDigit) ? digits : null;
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
/// <param name="TemporaryFullFileName">
/// For a change of case only (task 061) that stopped after its first step: the temporary name the file now has, which
/// every file updated uses. Null when the file has its new name, or was not renamed.
/// </param>
public sealed record RenameResult(
    RenameRequest Request,
    bool Renamed,
    bool OldFileDeleted,
    bool DescriptionWritten,
    IReadOnlyList<RenameFileResult> Files,
    IReadOnlyList<RenameNote> Notes,
    string? TemporaryFullFileName = null)
{
    /// <summary>
    /// Where the file is once Rename has run: <see cref="TemporaryFullFileName"/> when it stopped there, otherwise the
    /// new name (also when nothing was renamed; check <see cref="Renamed"/>).
    /// </summary>
    public string FullFileNameNow => TemporaryFullFileName ?? Request.NewFullFileName;

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
/// <see cref="RenameResult"/>. A change of case only runs these steps twice, through a temporary name (task 061).
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
    private readonly Func<Guid>? _newGuid;

    /// <param name="target">Inventor.</param>
    /// <param name="store">The session's register, for the entry update; null when the file has no registered number.</param>
    /// <param name="log">Where each step is recorded.</param>
    /// <param name="newGuid">Replaces <see cref="Guid.NewGuid"/> for the temporary name of a change of case only; for tests.</param>
    public PartRenamer(IRenameTarget target, RegisterStore? store = null, ILog? log = null, Func<Guid>? newGuid = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        _target = target;
        _store = store;
        _log = log ?? NullLog.Instance;
        _newGuid = newGuid;
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

    /// <summary>
    /// A change of case only stopped after its first step because the old file had to be kept (task 061): the file has
    /// the temporary name, which every file updated uses, and the new name cannot be given while the old file exists.
    /// </summary>
    public static string StoppedAtTemporaryNameOldFileKeptMessage(
        string oldFullFileName, string temporaryFullFileName, string newFullFileName) =>
        "Only the case of the name changes, so the file is renamed in two steps through a temporary name. It stopped " +
        $"after the first step, because the old file {RenamePlan.FileNameOnly(oldFullFileName)} was kept (see above). " +
        $"The file is now named {RenamePlan.FileNameOnly(temporaryFullFileName)}, and every file that was updated uses " +
        "that name. Once nothing uses the old file, delete it, then run Rename again on " +
        $"{RenamePlan.FileNameOnly(temporaryFullFileName)} to name it {RenamePlan.FileNameOnly(newFullFileName)}.";

    /// <summary>
    /// A change of case only could not save the file under its new name in the second step (task 061): it keeps the
    /// temporary name, which every file updated uses.
    /// </summary>
    public static string StoppedAtTemporaryNameMessage(string temporaryFullFileName, string newFullFileName, string reason) =>
        "Only the case of the name changes, so the file is renamed in two steps through a temporary name. The second " +
        $"step could not save it as {RenamePlan.FileNameOnly(newFullFileName)}: {reason} The file is now named " +
        $"{RenamePlan.FileNameOnly(temporaryFullFileName)}, and every file that was updated uses that name. Run Rename " +
        $"again on it to name it {RenamePlan.FileNameOnly(newFullFileName)}.";

    /// <summary>Runs the steps.</summary>
    /// <param name="request">The rename.</param>
    /// <param name="filesToSearch">The candidate files (<see cref="RenameSearchList.Files"/>).</param>
    /// <param name="registeredNumber">The file's Part Number when the register holds it; null otherwise.</param>
    /// <param name="progress">Told the step under way, for the window.</param>
    /// <param name="skippedFolders">
    /// The search folders that were not listed (<see cref="RenameSearchList.Skipped"/>). Any at all keeps the old file.
    /// </param>
    /// <remarks>
    /// A change of case only (<see cref="RenamePlan.IsCaseOnlyChange"/>; task 061) runs the steps twice under the one
    /// confirmation already given: old name to a temporary name (<see cref="RenamePlan.TemporaryFileName"/>), then the
    /// temporary name to the new one, since saving a document under a name that differs only in case would write over
    /// itself. The second step runs only when the first deleted the old file; otherwise the file keeps the temporary
    /// name and the result says so.
    /// </remarks>
    public RenameResult Run(
        RenameRequest request,
        IReadOnlyList<string> filesToSearch,
        PartNumber? registeredNumber = null,
        Action<string>? progress = null,
        IReadOnlyList<SkippedFolder>? skippedFolders = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(filesToSearch);

        if (RenamePlan.IsCaseOnlyChange(request.OldFullFileName, request.NewFullFileName))
        {
            return RunCaseOnly(request, filesToSearch, registeredNumber, progress, skippedFolders);
        }

        var oldPath = request.OldFullFileName;
        var newPath = request.NewFullFileName;
        var step = RunStep(oldPath, newPath, filesToSearch, progress, skippedFolders, request.NewName);
        if (step.SaveAsError is { } error)
        {
            return NotRenamed(request, error, step.Notes);
        }

        // 6. The register entry (Default).
        var notes = step.Notes;
        if (registeredNumber is { } number && _store is not null)
        {
            notes.Add(UpdateRegister(number, request, newPath));
        }

        LogFinished(oldPath, newPath, step.Files, step.OldDeleted);
        return new RenameResult(
            request, true, step.OldDeleted, step.DescriptionWritten, step.Files.AsReadOnly(), notes.AsReadOnly());
    }

    /// <summary>The two steps of a change of case only (task 061), through a temporary name.</summary>
    private RenameResult RunCaseOnly(
        RenameRequest request,
        IReadOnlyList<string> filesToSearch,
        PartNumber? registeredNumber,
        Action<string>? progress,
        IReadOnlyList<SkippedFolder>? skippedFolders)
    {
        var oldPath = request.OldFullFileName;
        var newPath = request.NewFullFileName;

        string tempPath;
        try
        {
            tempPath = RenamePlan.TemporaryFileName(oldPath, _target.FileExists, _newGuid);
        }
        catch (Exception ex)
        {
            _log.Error($"Rename: no temporary name could be found for '{oldPath}'. Nothing was changed.", ex);
            return NotRenamed(request, ex.Message, []);
        }

        _log.Info($"Rename: only the case changes; '{oldPath}' is renamed through '{tempPath}'.");

        // First step: old name to the temporary name. Nothing has changed if its SaveAs fails.
        var first = RunStep(oldPath, tempPath, filesToSearch, progress, skippedFolders, descriptionName: null);
        if (first.SaveAsError is { } error)
        {
            return NotRenamed(request, error, first.Notes);
        }

        // The second step saves under a name Windows treats as the old file's, so it runs only once the old file is gone.
        if (!first.OldDeleted)
        {
            _log.Warn($"Rename: stopped at '{tempPath}', because '{oldPath}' was kept.");
            first.Notes.Add(new RenameNote(
                StoppedAtTemporaryNameOldFileKeptMessage(oldPath, tempPath, newPath), IsProblem: true));
            return StoppedAtTemporaryName(request, registeredNumber, tempPath, first);
        }

        // Second step: the temporary name to the new one. It searches the same list, with each drawing the first step
        // renamed under its temporary name (task 059's implementer note).
        var secondFiles = filesToSearch
            .Select(f => first.MovedDrawings.TryGetValue(f, out var moved) ? moved : f)
            .Concat(first.CopiedDrawings)
            .ToList();
        var second = RunStep(tempPath, newPath, secondFiles, progress, skippedFolders: null, request.NewName);
        if (second.SaveAsError is { } secondError)
        {
            _log.Warn($"Rename: stopped at '{tempPath}': it could not be saved as '{newPath}': {secondError}");
            first.Notes.Add(new RenameNote(
                StoppedAtTemporaryNameMessage(tempPath, newPath, secondError), IsProblem: true));
            return StoppedAtTemporaryName(request, registeredNumber, tempPath, first);
        }

        var files = MergeSteps(first, second);
        var notes = first.Notes.Concat(second.Notes).ToList();
        if (registeredNumber is { } number && _store is not null)
        {
            notes.Add(UpdateRegister(number, request, newPath));
        }

        LogFinished(oldPath, newPath, files, second.OldDeleted);
        return new RenameResult(
            request, true, second.OldDeleted, second.DescriptionWritten, files.AsReadOnly(), notes.AsReadOnly());
    }

    /// <summary>
    /// A change of case only that ends under its temporary name: Description is still written, and the register entry
    /// names the temporary file, where the file is.
    /// </summary>
    private RenameResult StoppedAtTemporaryName(
        RenameRequest request, PartNumber? registeredNumber, string tempPath, StepOutcome first)
    {
        var notes = first.Notes;
        var descriptionWritten = WriteDescription(request.NewName, notes);
        if (registeredNumber is { } number && _store is not null)
        {
            notes.Add(UpdateRegister(number, request, tempPath));
        }

        LogFinished(request.OldFullFileName, tempPath, first.Files, first.OldDeleted);
        return new RenameResult(
            request, true, first.OldDeleted, descriptionWritten, first.Files.AsReadOnly(), notes.AsReadOnly(), tempPath);
    }

    /// <summary>
    /// The two steps' file lists as one: each file once, under the name it was found by, in the first step's order and
    /// then any the second step found alone. A file in both takes the second step's outcome, keeps the first step's
    /// occurrence problems, and keeps the first step's new name when the second could not rename it.
    /// </summary>
    private static List<RenameFileResult> MergeSteps(StepOutcome first, StepOutcome second)
    {
        var foundAs = first.MovedDrawings.ToDictionary(p => p.Value, p => p.Key, StringComparer.OrdinalIgnoreCase);
        var secondByName = new Dictionary<string, RenameFileResult>(StringComparer.OrdinalIgnoreCase);
        var secondOrder = new List<string>();
        foreach (var file in second.Files)
        {
            var name = foundAs.TryGetValue(file.FullFileName, out var original) ? original : file.FullFileName;
            if (secondByName.TryAdd(name, file with { FullFileName = name }))
            {
                secondOrder.Add(name);
            }
        }

        var merged = new List<RenameFileResult>();
        foreach (var a in first.Files)
        {
            if (!secondByName.Remove(a.FullFileName, out var b))
            {
                merged.Add(a);
                continue;
            }

            var problems = (a.OccurrenceProblems ?? []).Concat(b.OccurrenceProblems ?? []).ToList();
            merged.Add(b with
            {
                OccurrencesRenamed = b.Updated ? b.OccurrencesRenamed : a.OccurrencesRenamed,
                OccurrenceProblems = problems.Count > 0 ? problems : null,
                RenamedTo = b.RenamedTo ?? a.RenamedTo,
            });
        }

        merged.AddRange(secondOrder.Where(secondByName.ContainsKey).Select(n => secondByName[n]));
        return merged;
    }

    /// <summary>
    /// One rename from <paramref name="oldPath"/> to <paramref name="newPath"/>: save under the new name, update the
    /// files found, save the open documents repointed, write Description when <paramref name="descriptionName"/> is
    /// given, and delete the old file under task 058's rules. The register is the caller's.
    /// </summary>
    private StepOutcome RunStep(
        string oldPath,
        string newPath,
        IReadOnlyList<string> filesToSearch,
        Action<string>? progress,
        IReadOnlyList<SkippedFolder>? skippedFolders,
        string? descriptionName)
    {
        var step = new StepOutcome();
        var files = step.Files;
        var notes = step.Notes;

        // 1. Save under the new name. Nothing has changed if this fails.
        try
        {
            _target.SaveAs(newPath);
        }
        catch (Exception ex)
        {
            _log.Error($"Rename: '{oldPath}' could not be saved as '{newPath}'. Nothing was changed.", ex);
            step.SaveAsError = ex.Message;
            return step;
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
                    UpdateDrawing(file, newDrawing, oldPath, newPath, step, handled);
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
        if (descriptionName is not null)
        {
            step.DescriptionWritten = WriteDescription(descriptionName, notes);
        }

        // 5. The old file goes last, and only when nothing still uses it: every file found was updated, every search
        //    folder was searched, and no open document still references it.
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
                step.OldDeleted = true;
            }
            catch (Exception ex)
            {
                notes.Add(new RenameNote(OldFileNotDeletedMessage(oldPath, ex.Message), IsProblem: true));
                _log.Warn($"Rename: '{oldPath}' could not be deleted: {ex.Message}");
            }
        }

        return step;
    }

    /// <summary>Sets Description to the new name; a failure is a note.</summary>
    private bool WriteDescription(string name, List<RenameNote> notes)
    {
        try
        {
            _target.WriteDescription(name);
            return true;
        }
        catch (Exception ex)
        {
            notes.Add(new RenameNote(DescriptionFailedMessage(ex.Message), IsProblem: true));
            _log.Error("Rename: Description could not be written.", ex);
            return false;
        }
    }

    private static RenameResult NotRenamed(RenameRequest request, string reason, List<RenameNote> notes)
    {
        notes.Add(new RenameNote(RenameFailedMessage(reason), IsProblem: true));
        return new RenameResult(request, false, false, false, [], notes.AsReadOnly());
    }

    private void LogFinished(string oldPath, string newPath, List<RenameFileResult> files, bool oldDeleted) =>
        _log.Info(
            $"Rename: finished '{oldPath}' -> '{newPath}': {files.Count(f => f.Updated)} updated, " +
            $"{files.Count(f => !f.Updated)} not updated, {files.Sum(f => f.OccurrencesRenamed)} occurrences renamed, " +
            $"{files.Count(f => f.RenamedTo is not null)} drawings renamed, old file {(oldDeleted ? "deleted" : "kept")}.");

    /// <summary>What one step of <see cref="RunStep"/> did.</summary>
    private sealed class StepOutcome
    {
        /// <summary>Why the file could not be saved under the step's new name; null when it was.</summary>
        public string? SaveAsError { get; set; }

        public bool OldDeleted { get; set; }

        public bool DescriptionWritten { get; set; }

        public List<RenameFileResult> Files { get; } = [];

        public List<RenameNote> Notes { get; } = [];

        /// <summary>Drawings of the model renamed whose old file was deleted: old path to new path.</summary>
        public Dictionary<string, string> MovedDrawings { get; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>The new paths of drawings renamed whose old file could not be deleted, so both files exist.</summary>
        public List<string> CopiedDrawings { get; } = [];
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
        StepOutcome step,
        HashSet<string> handled)
    {
        var files = step.Files;
        var notes = step.Notes;
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

        if (drawing.OldFileNotDeletedReason is null)
        {
            step.MovedDrawings[file] = newDrawing;
        }
        else
        {
            step.CopiedDrawings.Add(newDrawing);
        }

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

    /// <summary>Points the register entry from the old file to <paramref name="fullFileNameNow"/>, where the file is.</summary>
    private RenameNote UpdateRegister(PartNumber number, RenameRequest request, string fullFileNameNow)
    {
        try
        {
            var result = _store!.UpdateEntry(number, fullFileNameNow, request.NewName, request.OldFullFileName);
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
