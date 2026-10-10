using System.Globalization;
using InventorAddin.Core.Logging;
using InventorAddin.Core.Models;
using InventorAddin.Core.PartProperties;
using InventorAddin.Core.ViewModels;

namespace InventorAddin.Core.Numbering.MassUpdate;

/// <summary>
/// What Mass Update needs from Inventor (task 048): read a file for the scan, and open one to change it. The add-in
/// supplies it; tests use a fake.
/// </summary>
public interface IMassUpdateFiles
{
    /// <summary>
    /// Reads <paramref name="fullPath"/>: from the open document when it was open in Inventor before Mass Update
    /// started (<see cref="ScannedFile.OpenInInventor"/> true), otherwise by opening it invisibly, reading and closing
    /// it. Never throws for the file: a failure is given as <see cref="ScannedFile.ReadError"/>.
    /// </summary>
    ScannedFile Read(string fullPath);

    /// <summary>
    /// Opens <paramref name="fullPath"/> invisibly to change it. Throws, with a message fit for its row, when the file
    /// was open in Inventor before Mass Update started, is read-only, or cannot be opened.
    /// </summary>
    IMassUpdateDocument Open(string fullPath);
}

/// <summary>A file <see cref="IMassUpdateFiles.Open"/> opened.</summary>
public interface IMassUpdateDocument
{
    /// <summary>Writes the properties in one transaction; throws when one cannot be written.</summary>
    void WriteProperties(IReadOnlyList<PropertyWrite> writes);

    /// <summary>Saves the file under its own name.</summary>
    void Save();

    /// <summary>Closes the file without saving it (after <see cref="Save"/>, nothing is lost).</summary>
    void CloseWithoutSaving();
}

/// <summary>Mass Update could not start or had to stop. The message is fit to show the user.</summary>
public sealed class MassUpdateException : Exception
{
    public MassUpdateException(string message, Exception? inner = null)
        : base(message, inner)
    {
    }
}

/// <summary>What <see cref="MassUpdateScanner.Scan"/> found.</summary>
/// <param name="Files">The files read, in scan order; only those read before a cancel.</param>
/// <param name="Register">The register as synced for this scan, for <see cref="MassUpdateViewModel.ShowScan"/>.</param>
/// <param name="Total">How many files the folder holds to scan.</param>
/// <param name="Cancelled">True when the scan was cancelled before every file was read.</param>
/// <param name="Notice">What to tell the user about the register sync (duplicates, offline), or null.</param>
public sealed record MassUpdateScanResult(
    IReadOnlyList<ScannedFile> Files,
    PartRegister Register,
    int Total,
    bool Cancelled,
    string? Notice);

/// <summary>
/// Mass Update's scan (spec 02, "Commands: Mass Update"; task 048): sync the register, list the files under the folder
/// (<see cref="ScanFolder.FilesToScan"/>, on a background task with a timeout), then read each file through
/// <see cref="IMassUpdateFiles"/> in order, reporting progress before each and stopping between files when asked.
/// </summary>
public sealed class MassUpdateScanner
{
    public const string ListingProgressText = "Listing the files...";

    private readonly RegisterStore _store;
    private readonly IMassUpdateFiles _files;
    private readonly ILog _log;
    private readonly Func<string, IReadOnlyList<string>> _listFiles;
    private readonly TimeSpan _listTimeout;

    /// <param name="store">The session's register.</param>
    /// <param name="files">Reads files in Inventor.</param>
    /// <param name="log">Where problems are recorded.</param>
    /// <param name="listFiles">Lists the files to scan; <see cref="ScanFolder.FilesToScan"/> by default.</param>
    /// <param name="listTimeout">How long the listing may take; <see cref="RenamePlan.FolderListTimeout"/> by default.</param>
    public MassUpdateScanner(
        RegisterStore store,
        IMassUpdateFiles files,
        ILog? log = null,
        Func<string, IReadOnlyList<string>>? listFiles = null,
        TimeSpan? listTimeout = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(files);
        _store = store;
        _files = files;
        _log = log ?? NullLog.Instance;
        _listFiles = listFiles ?? ScanFolder.FilesToScan;
        _listTimeout = listTimeout ?? RenamePlan.FolderListTimeout;
    }

    public static string FolderNotFoundMessage(string folder) =>
        $"The folder '{folder}' does not exist or cannot be reached.";

    public static string ListingTooSlowMessage(string folder, TimeSpan timeout) =>
        $"Listing the files in '{folder}' took longer than {timeout.TotalSeconds.ToString("0", CultureInfo.InvariantCulture)} seconds, so the scan stopped.";

    public static string ListingFailedMessage(string folder, string reason) =>
        $"The files in '{folder}' could not be listed: {reason}";

    public const string OfflineNotice =
        "The shared register could not be reached, so the scan uses this PC's copy of the register. Numbers registered " +
        "or assigned now are synced later.";

    public static string SyncFailedNotice(string reason) =>
        $"The register could not be synced, so the scan uses this PC's copy. Apply needs the register, so fix this " +
        $"first:{Environment.NewLine}{reason}";

    /// <summary>
    /// Scans <paramref name="folder"/>. <paramref name="progress"/> is called with the file about to be read (1-based)
    /// and the total, and once with <c>(0, 0)</c> before the listing. <paramref name="isCancelled"/> is asked after
    /// each file; when it says yes the scan stops and returns the files read so far.
    /// </summary>
    /// <exception cref="MassUpdateException">The folder cannot be found or listed.</exception>
    public MassUpdateScanResult Scan(string folder, Action<int, int>? progress = null, Func<bool>? isCancelled = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        progress ??= (_, _) => { };
        isCancelled ??= () => false;

        // Sync first, as at every assignment (spec 02, "Syncing"). A scan only reads, so a register that cannot be
        // synced does not stop it; Apply syncs again and stops then.
        var notice = Sync();
        var register = _store.ReadLocal();

        progress(0, 0);
        var paths = List(folder);

        var files = new List<ScannedFile>();
        var cancelled = false;
        for (var i = 0; i < paths.Count; i++)
        {
            progress(i + 1, paths.Count);
            files.Add(Read(paths[i]));

            if (i < paths.Count - 1 && isCancelled())
            {
                cancelled = true;
                _log.Info($"Mass Update scan of '{folder}' cancelled after {files.Count} of {paths.Count} files.");
                break;
            }
        }

        if (!cancelled)
        {
            _log.Info($"Mass Update scanned {files.Count} files in '{folder}'.");
        }

        return new MassUpdateScanResult(files.AsReadOnly(), register, paths.Count, cancelled, notice);
    }

    private string? Sync()
    {
        try
        {
            var sync = _store.Sync();
            var parts = new List<string>();
            if (!sync.Reached && _store.SharedFolder is not null)
            {
                parts.Add(OfflineNotice);
            }

            if (sync.Duplicates.Count > 0)
            {
                parts.Add(PartPropertiesViewModel.FormatDuplicates(
                    sync.Duplicates.Select(PartNumberAssignment.DescribeDuplicate).ToList()));
            }

            return parts.Count == 0 ? null : string.Join(Environment.NewLine + Environment.NewLine, parts);
        }
        catch (Exception ex) when (ex is RegisterBusyException or RegisterUnreadableException or IOException or UnauthorizedAccessException)
        {
            _log.Warn($"Mass Update scan: the register could not be synced ({ex.Message}); this PC's copy is used.");
            return SyncFailedNotice(ex.Message);
        }
    }

    private IReadOnlyList<string> List(string folder)
    {
        var listing = Task.Run(() => _listFiles(folder));
        bool finished;
        try
        {
            finished = listing.Wait(_listTimeout);
        }
        catch (AggregateException)
        {
            finished = true;
        }

        if (!finished)
        {
            // Left to finish on its own, as Rename does.
            throw new MassUpdateException(ListingTooSlowMessage(folder, _listTimeout));
        }

        if (listing.IsFaulted)
        {
            var ex = listing.Exception!.GetBaseException();
            throw ex is DirectoryNotFoundException
                ? new MassUpdateException(FolderNotFoundMessage(folder), ex)
                : new MassUpdateException(ListingFailedMessage(folder, ex.Message), ex);
        }

        return listing.Result;
    }

    private ScannedFile Read(string path)
    {
        try
        {
            return _files.Read(path);
        }
        catch (Exception ex)
        {
            // The adapter should not throw; a file it fails on still gets its row.
            _log.Error($"Mass Update could not read '{path}'.", ex);
            return new ScannedFile(path, "", "", "", "", DocumentKind.Unknown, ReadError: ex.Message);
        }
    }
}

/// <summary>What <see cref="MassUpdateRunner.Apply"/> did.</summary>
/// <param name="Results">One result per file the plan changes, in plan order; empty when it stopped before any.</param>
/// <param name="StopMessage">Why Apply stopped early (the register could not be used), or null.</param>
/// <param name="Notice">What else to tell the user (offline numbers, duplicates found by the sync), or null.</param>
public sealed record MassUpdateApplyResult(IReadOnlyList<FileUpdateResult> Results, string? StopMessage, string? Notice)
{
    /// <summary>How many files were done.</summary>
    public int DoneCount => Results.Count(r => r.Error is null);

    /// <summary>How many files failed or were not changed.</summary>
    public int FailedCount => Results.Count(r => r.Error is not null);
}

/// <summary>
/// Mass Update's Apply (spec 02, "Commands: Mass Update"; task 048). First syncs the register: if that fails
/// (<see cref="RegisterBusyException"/>, <see cref="RegisterUnreadableException"/>; spec 02 question 19) Apply stops
/// before any file changes. Then, for each <see cref="FileUpdate"/>: opens the file when it has something to write
/// (a file whose only update is Register is never opened or saved); does the register action through
/// <see cref="RegisterStore"/>; writes the Part Number for an assigned number, and Designer and Detailer for name
/// replacement; saves and closes. One file failing does not stop the others. A register failure part way stops the
/// run: the remaining files are reported as not changed.
/// </summary>
/// <remarks>
/// Updates that register an existing number run first, keeping the plan's order otherwise, so a number assigned to
/// another file in the same run can never take a number a ticked file already carries (Default, task 048). A file
/// that must be opened is opened before its number is assigned, so a file that cannot be opened costs no number.
/// </remarks>
public sealed class MassUpdateRunner
{
    public const string StoppedText = "Not changed: Apply stopped because the register could not be used.";

    private readonly RegisterStore _store;
    private readonly IMassUpdateFiles _files;
    private readonly ILog _log;

    public MassUpdateRunner(RegisterStore store, IMassUpdateFiles files, ILog? log = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(files);
        _store = store;
        _files = files;
        _log = log ?? NullLog.Instance;
    }

    public const string NamesReplacedText = "Names were replaced.";

    public static string AlreadyRegisteredText(PartNumber number) =>
        $"Not registered: {number} is already in the register.";

    public static string FailedAfterAssignText(PartNumber number, string reason) =>
        $"Part number {number} was assigned, but the file could not be saved: {reason} The number stays in the register.";

    public static string RegisterFailedText(string reason) =>
        $"The part register could not be updated: {reason} A number may already have been taken in the register; " +
        "check it before trying again.";

    public static string OfflineNotice(int count) =>
        count == 1
            ? "1 number was registered or assigned offline; it will be added to the shared register at the next sync."
            : $"{count} numbers were registered or assigned offline; they will be added to the shared register at the next sync.";

    /// <summary>For example <c>"7 files updated, 1 not."</c>, or <c>"3 files updated."</c>.</summary>
    public static string SummaryText(MassUpdateApplyResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var done = result.DoneCount == 1 ? "1 file updated" : $"{result.DoneCount} files updated";
        return result.FailedCount == 0 ? done + "." : $"{done}, {result.FailedCount} not.";
    }

    /// <summary>
    /// Applies <paramref name="plan"/>. <paramref name="progress"/> is called with the file about to be changed
    /// (1-based) and the total.
    /// </summary>
    public MassUpdateApplyResult Apply(MassUpdatePlan plan, Action<int, int>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(plan);
        progress ??= (_, _) => { };

        var updates = plan.Updates
            .Where(u => u.Register is RegisterExisting)
            .Concat(plan.Updates.Where(u => u.Register is not RegisterExisting))
            .ToList();
        if (updates.Count == 0)
        {
            return new MassUpdateApplyResult([], null, null);
        }

        var duplicates = new List<RegisterDuplicate>();
        var offline = 0;

        // Spec 02 question 19 (Default): the register must be usable before anything changes.
        try
        {
            duplicates.AddRange(_store.Sync().Duplicates);
        }
        catch (Exception ex) when (IsRegisterFailure(ex))
        {
            _log.Warn($"Mass Update stopped before changing any file: {ex.Message}");
            return new MassUpdateApplyResult([], StopMessage(ex), null);
        }

        var results = new Dictionary<FileUpdate, FileUpdateResult>(ReferenceEqualityComparer.Instance);
        string? stop = null;
        for (var i = 0; i < updates.Count; i++)
        {
            var update = updates[i];
            if (stop is not null)
            {
                results[update] = new FileUpdateResult(update.File.FullPath, StoppedText);
                continue;
            }

            progress(i + 1, updates.Count);
            try
            {
                results[update] = ApplyOne(update, duplicates, ref offline);
            }
            catch (RegisterStopException ex)
            {
                stop = StopMessage(ex.InnerException!);
                results[update] = new FileUpdateResult(update.File.FullPath, stop);
                _log.Error($"Mass Update stopped at '{update.File.FullPath}': the register could not be used.", ex.InnerException);
            }
        }

        foreach (var duplicate in duplicates)
        {
            _log.Warn($"Mass Update: {PartNumberAssignment.DescribeDuplicate(duplicate)}");
        }

        var notices = new List<string>();
        if (offline > 0)
        {
            notices.Add(OfflineNotice(offline));
        }

        if (duplicates.Count > 0)
        {
            notices.Add(PartPropertiesViewModel.FormatDuplicates(
                duplicates.Select(PartNumberAssignment.DescribeDuplicate).ToList()));
        }

        var ordered = plan.Updates.Select(u => results[u]).ToList().AsReadOnly();
        var result = new MassUpdateApplyResult(
            ordered,
            stop,
            notices.Count == 0 ? null : string.Join(Environment.NewLine + Environment.NewLine, notices));
        _log.Info($"Mass Update: {SummaryText(result)}");
        return result;
    }

    private FileUpdateResult ApplyOne(FileUpdate update, List<RegisterDuplicate> duplicates, ref int offline)
    {
        var path = update.File.FullPath;

        if (!update.ChangesFile)
        {
            // Register only: nothing is written to the file, so it is not opened or saved.
            var registered = Register(update, (RegisterExisting)update.Register!, duplicates, ref offline);
            return new FileUpdateResult(path, registered);
        }

        IMassUpdateDocument document;
        try
        {
            document = _files.Open(path);
        }
        catch (Exception ex)
        {
            _log.Error($"Mass Update could not open '{path}'.", ex);
            return new FileUpdateResult(path, ex.Message);
        }

        PartNumber? assigned = null;
        string? note = null;
        try
        {
            var writes = new List<PropertyWrite>();
            switch (update.Register)
            {
                case RegisterExisting existing:
                    note = Register(update, existing, duplicates, ref offline);
                    break;

                case AssignNew:
                    var assignment = Run(() => _store.Assign(update.RequestFor()));
                    assigned = assignment.PartNumber;
                    duplicates.AddRange(assignment.Duplicates);
                    if (assignment.Offline)
                    {
                        offline++;
                    }

                    writes.Add(FileUpdate.PartNumberWrite(assignment.PartNumber));
                    break;
            }

            writes.AddRange(update.Writes);
            if (writes.Count > 0)
            {
                document.WriteProperties(writes);
                document.Save();
            }

            return new FileUpdateResult(path, note is null || update.Writes.Count == 0 ? note : $"{note} {NamesReplacedText}");
        }
        catch (RegisterStopException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _log.Error($"Mass Update could not update '{path}'.", ex);
            return new FileUpdateResult(path, assigned is { } number ? FailedAfterAssignText(number, ex.Message) : ex.Message);
        }
        finally
        {
            try
            {
                document.CloseWithoutSaving();
            }
            catch (Exception closeEx)
            {
                _log.Error($"Mass Update could not close '{path}'.", closeEx);
            }
        }
    }

    /// <summary>Registers the file's number; returns the row's note when it was not added, or null.</summary>
    private string? Register(FileUpdate update, RegisterExisting existing, List<RegisterDuplicate> duplicates, ref int offline)
    {
        var result = Run(() => _store.RegisterExisting(existing.Number, update.RequestFor()));
        duplicates.AddRange(result.Duplicates);
        if (result.Offline && result.Added)
        {
            offline++;
        }

        return result.Added ? null : AlreadyRegisteredText(existing.Number);
    }

    /// <summary>Runs a register call; a register failure becomes a <see cref="RegisterStopException"/>.</summary>
    private static T Run<T>(Func<T> call)
    {
        try
        {
            return call();
        }
        catch (Exception ex) when (IsRegisterFailure(ex))
        {
            throw new RegisterStopException(ex);
        }
    }

    private static bool IsRegisterFailure(Exception ex) =>
        ex is RegisterBusyException or RegisterUnreadableException or IOException or UnauthorizedAccessException
            or InvalidOperationException;

    private static string StopMessage(Exception ex) =>
        ex is RegisterBusyException or RegisterUnreadableException or InvalidOperationException
            ? ex.Message
            : RegisterFailedText(ex.Message);

    /// <summary>A register failure inside one file's update, which stops the run.</summary>
    private sealed class RegisterStopException : Exception
    {
        public RegisterStopException(Exception inner)
            : base(inner.Message, inner)
        {
        }
    }
}
