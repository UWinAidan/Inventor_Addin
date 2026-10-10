using System.Diagnostics;
using System.Text;
using System.Text.Json;
using InventorAddin.Core.Logging;

namespace InventorAddin.Core.Numbering;

/// <summary>
/// Assigns part numbers and keeps the register on disk (spec 02, "The register"): the shared file in the register
/// folder when it can be reached, this PC's copy and pending entries when it cannot, a lock file while writing, and a
/// sync that reports duplicates. The store does not decide where the folders are. Everything is synchronous apart from
/// the reachability probe.
/// </summary>
/// <remarks>
/// Rename (task 047) is the one change ever made to an existing row: <see cref="UpdateEntry"/> rewrites a row's
/// <c>FilePath</c> and <c>Name</c>. Offline, the update waits in <see cref="PendingUpdatesFileName"/> and is applied at
/// the next sync, after the pending entries.
/// </remarks>
public sealed class RegisterStore
{
    public const string SharedFileName = "part-register.csv";
    public const string LockFileName = "part-register.lock";
    public const string LocalFileName = "part-register.csv";
    public const string PendingFileName = "pending.csv";

    /// <summary>
    /// This PC's register updates made while the shared folder could not be reached (task 047, Default). JSON, because
    /// it is the add-in's own file and holds the old path as well as the new one; Aidan never needs to open it.
    /// </summary>
    public const string PendingUpdatesFileName = "pending-updates.json";

    /// <summary>How long <see cref="Assign"/> and <see cref="Sync"/> retry for the lock by default.</summary>
    public static readonly TimeSpan DefaultLockWait = TimeSpan.FromSeconds(5);

    /// <summary>A lock file older than this is left over from a crash and is removed.</summary>
    public static readonly TimeSpan StaleLockAge = TimeSpan.FromMinutes(2);

    /// <summary>How long the shared folder has to answer before it counts as unreachable.</summary>
    public static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(3);

    private static readonly TimeSpan LockRetryDelay = TimeSpan.FromMilliseconds(100);

    // With a byte order mark, so Excel shows names outside ASCII correctly. RegisterCsv.Read accepts it.
    private static readonly Encoding FileEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);

    private readonly IRegisterEnvironment _environment;
    private readonly ILog _log;

    /// <param name="sharedFolder">The register folder on the NAS. Blank means no shared register: always offline.</param>
    /// <param name="localFolder">This PC's register folder, created when needed.</param>
    /// <param name="environment">Clock, names and folder probe.</param>
    /// <param name="log">Where warnings go.</param>
    /// <param name="lockWait">How long to retry for the lock; <see cref="DefaultLockWait"/> when null.</param>
    public RegisterStore(
        string? sharedFolder,
        string localFolder,
        IRegisterEnvironment environment,
        ILog? log = null,
        TimeSpan? lockWait = null)
    {
        if (string.IsNullOrWhiteSpace(localFolder))
        {
            throw new ArgumentException("A local register folder is required.", nameof(localFolder));
        }

        ArgumentNullException.ThrowIfNull(environment);

        SharedFolder = string.IsNullOrWhiteSpace(sharedFolder) ? null : sharedFolder;
        LocalFolder = localFolder;
        _environment = environment;
        _log = log ?? NullLog.Instance;
        LockWait = lockWait ?? DefaultLockWait;
    }

    /// <summary>The shared register folder, or null when there is none.</summary>
    public string? SharedFolder { get; }

    public string LocalFolder { get; }

    public TimeSpan LockWait { get; }

    public string? SharedFilePath => SharedFolder is null ? null : Path.Combine(SharedFolder, SharedFileName);

    public string? LockFilePath => SharedFolder is null ? null : Path.Combine(SharedFolder, LockFileName);

    public string LocalFilePath => Path.Combine(LocalFolder, LocalFileName);

    public string PendingFilePath => Path.Combine(LocalFolder, PendingFileName);

    public string PendingUpdatesFilePath => Path.Combine(LocalFolder, PendingUpdatesFileName);

    /// <summary>
    /// The local copy followed by the pending entries, with the pending updates applied, without touching the shared
    /// folder. A missing file reads as empty; an unreadable one reads as empty with a logged warning. Never throws for
    /// file problems.
    /// </summary>
    public PartRegister ReadLocal()
    {
        var local = ReadLocalFile(LocalFilePath);
        var pending = ReadLocalFile(PendingFilePath);
        var register = new PartRegister(local.Concat(pending));

        foreach (var update in ReadLocalUpdates())
        {
            register.TryUpdate(update.Number, update.FilePath, update.Name, update.OldFilePath, out register);
        }

        return register;
    }

    /// <summary>Spec 02, "Has a number", against <see cref="ReadLocal"/>.</summary>
    public bool HasNumber(string? partNumberProperty) => ReadLocal().HasNumber(partNumberProperty);

    /// <summary>
    /// Assigns the next number to <paramref name="request"/>. When the shared folder can be reached: syncs the pending
    /// entries into the shared register under the lock, appends the new entry, and refreshes the local copy. When it
    /// cannot: takes the next number from <see cref="ReadLocal"/> and appends the entry to the pending file.
    /// </summary>
    /// <exception cref="RegisterBusyException">The lock could not be taken within <see cref="LockWait"/>.</exception>
    /// <exception cref="RegisterUnreadableException">The shared or pending register cannot be read in full.</exception>
    public RegisterAssignment Assign(RegisterRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Assign(_ => request);
    }

    /// <summary>
    /// As <see cref="Assign(RegisterRequest)"/>, for a file whose path holds the number, such as a new file named
    /// <c>{number},{name}.ipt</c>. <paramref name="requestFor"/> is called once with the number about to be assigned,
    /// before its row is written (under the lock when the shared register is reached). If it throws, nothing is
    /// assigned, no register file is changed, and the exception propagates.
    /// </summary>
    /// <exception cref="RegisterBusyException">The lock could not be taken within <see cref="LockWait"/>.</exception>
    /// <exception cref="RegisterUnreadableException">The shared or pending register cannot be read in full.</exception>
    public RegisterAssignment Assign(Func<PartNumber, RegisterRequest> requestFor)
    {
        ArgumentNullException.ThrowIfNull(requestFor);

        RegisterRequest RequestFor(PartNumber number) =>
            requestFor(number) ?? throw new InvalidOperationException("No register request was given for the new number.");

        if (!IsReachable())
        {
            var number = ReadLocal().NextNumber();
            var offlineRequest = RequestFor(number);
            AppendPending(CreateEntry(number, offlineRequest));
            _log.Info($"Part number {number} assigned offline to '{offlineRequest.FilePath}'; it will be synced to the shared register.");
            return new RegisterAssignment(number, Offline: true, Duplicates: []);
        }

        var assigned = default(PartNumber);
        RegisterRequest? request = null;
        var duplicates = UnderLock(merged =>
        {
            assigned = merged.NextNumber();
            request = RequestFor(assigned);
            return merged.Append(CreateEntry(assigned, request));
        });

        _log.Info($"Part number {assigned} assigned to '{request!.FilePath}'.");
        return new RegisterAssignment(assigned, Offline: false, duplicates);
    }

    /// <summary>
    /// A renamed file (task 047): rewrites the <c>FilePath</c> and <c>Name</c> of the row holding
    /// <paramref name="number"/> (<see cref="PartRegister.TryUpdate"/>; <paramref name="oldPath"/> picks the row when the
    /// number was handed out twice). Rows are never added, removed or reordered by this. When the shared folder can be
    /// reached, the update is made under the lock, with the same sync as <see cref="Assign(RegisterRequest)"/>. When it
    /// cannot, or when that write fails (the lock is busy, a register file cannot be read in full, or an I/O error), the
    /// update waits in the pending updates file and is applied at the next sync (<c>Offline</c> true; after a failed
    /// write, <c>QueuedReason</c> says why). It is not queued when this PC's register (<see cref="ReadLocal"/>) does not
    /// hold the number.
    /// </summary>
    /// <exception cref="RegisterUnreadableException">
    /// The pending updates file cannot be read in full, so the update could not be queued either. Nothing was changed.
    /// </exception>
    public RegisterUpdateResult UpdateEntry(PartNumber number, string newPath, string newName, string? oldPath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newPath);
        ArgumentNullException.ThrowIfNull(newName);

        if (!IsReachable())
        {
            return QueueUpdate(number, newPath, newName, oldPath, queuedReason: null);
        }

        var found = false;
        IReadOnlyList<RegisterDuplicate> duplicates;
        try
        {
            duplicates = UnderLock(merged =>
            {
                found = merged.TryUpdate(number, newPath, newName, oldPath, out var updated);
                return updated;
            });
        }
        catch (Exception ex) when (ex is RegisterBusyException or RegisterUnreadableException or IOException or UnauthorizedAccessException)
        {
            // The rename has happened, so the update must not be lost: it waits, as offline, for the next sync. Applying
            // it again is safe (PartRegister.TryUpdate), so a write that got part way does no harm.
            _log.Warn($"Register entry {number} could not be updated in the shared register ({ex.Message}); it is queued for the next sync.");
            return QueueUpdate(number, newPath, newName, oldPath, queuedReason: ex.Message);
        }

        if (found)
        {
            _log.Info($"Register entry {number} updated to '{newPath}'.");
        }
        else
        {
            _log.Warn($"Register entry {number} was not updated for '{newPath}': the register does not hold it.");
        }

        return new RegisterUpdateResult(found, Offline: false, duplicates);
    }

    /// <summary>
    /// Puts an update in the pending updates file, when this PC's register holds the number. Throws
    /// <see cref="RegisterUnreadableException"/> when the pending updates file cannot be read in full.
    /// </summary>
    private RegisterUpdateResult QueueUpdate(PartNumber number, string newPath, string newName, string? oldPath, string? queuedReason)
    {
        if (!ReadLocal().Contains(number))
        {
            _log.Warn($"Register entry {number} was not updated for '{newPath}': this PC's register does not hold it.");
            return new RegisterUpdateResult(Found: false, Offline: true, Duplicates: [], QueuedReason: queuedReason);
        }

        AppendPendingUpdate(new RegisterUpdate(number, oldPath ?? "", newPath, newName, _environment.Now));
        _log.Info($"Register entry {number} updated offline to '{newPath}'; it will be synced to the shared register.");
        return new RegisterUpdateResult(Found: true, Offline: true, Duplicates: [], QueuedReason: queuedReason);
    }

    /// <summary>
    /// Syncs the pending entries into the shared register and refreshes the local copy, as <see cref="Assign"/> does
    /// but without adding an entry. When the shared folder cannot be reached, does nothing and returns
    /// <c>Reached = false</c>.
    /// </summary>
    /// <exception cref="RegisterBusyException">The lock could not be taken within <see cref="LockWait"/>.</exception>
    /// <exception cref="RegisterUnreadableException">The shared or pending register cannot be read in full.</exception>
    public RegisterSyncResult Sync()
    {
        if (!IsReachable())
        {
            return new RegisterSyncResult(Reached: false, Duplicates: []);
        }

        var duplicates = UnderLock(merged => merged);
        return new RegisterSyncResult(Reached: true, duplicates);
    }

    private bool IsReachable() =>
        SharedFolder is not null && _environment.ProbeFolder(SharedFolder, ProbeTimeout);

    /// <summary>
    /// Takes the lock, reads the shared register, the pending entries and the pending updates, merges the entries,
    /// applies the updates, lets <paramref name="change"/> change the result, writes the shared register when it
    /// changed, deletes the pending files and writes the local copy. The lock is released in all cases. Returns the
    /// duplicates the merge found.
    /// </summary>
    private IReadOnlyList<RegisterDuplicate> UnderLock(Func<PartRegister, PartRegister> change)
    {
        var sharedPath = SharedFilePath!;
        var lockPath = LockFilePath!;

        TakeLock(lockPath);
        try
        {
            var shared = ReadStrict(sharedPath);
            var pending = ReadStrict(PendingFilePath);
            var updates = ReadStrictUpdates(PendingUpdatesFilePath);

            var sync = PartRegister.Merge(shared, pending.Entries);
            foreach (var duplicate in sync.Duplicates)
            {
                _log.Warn(
                    $"Duplicate part number {duplicate.Number}: '{duplicate.Existing.FilePath}' " +
                    $"and '{duplicate.Added.FilePath}'. Both rows are kept in the register.");
            }

            var register = sync.Register;
            foreach (var update in updates)
            {
                if (!register.TryUpdate(update.Number, update.FilePath, update.Name, update.OldFilePath, out register))
                {
                    _log.Warn(
                        $"An update made offline to register entry {update.Number} ('{update.FilePath}') was dropped: " +
                        "the shared register does not hold that number.");
                }
            }

            var result = change(register);

            // Leave the NAS file alone when nothing in it changed.
            if (!result.Entries.SequenceEqual(shared.Entries))
            {
                WriteReplacing(SharedFolder!, SharedFileName, RegisterCsv.Write(result.Entries));
            }

            if (File.Exists(PendingFilePath))
            {
                File.Delete(PendingFilePath);
            }

            if (File.Exists(PendingUpdatesFilePath))
            {
                File.Delete(PendingUpdatesFilePath);
            }

            Directory.CreateDirectory(LocalFolder);
            WriteReplacing(LocalFolder, LocalFileName, RegisterCsv.Write(result.Entries));

            return sync.Duplicates;
        }
        finally
        {
            ReleaseLock(lockPath);
        }
    }

    private RegisterEntry CreateEntry(PartNumber number, RegisterRequest request) =>
        new(
            number,
            request.Name,
            request.PartType,
            request.Kind,
            request.FilePath,
            _environment.Now,
            _environment.AssignedBy,
            _environment.ComputerName);

    private void TakeLock(string lockPath)
    {
        var waited = Stopwatch.StartNew();
        while (true)
        {
            try
            {
                using var stream = new FileStream(lockPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                var holder = FileEncoding.GetBytes(
                    $"{_environment.AssignedBy} on {_environment.ComputerName} at {_environment.Now:O}");
                stream.Write(holder);
                return;
            }
            catch (IOException) when (File.Exists(lockPath))
            {
                // Someone else holds the lock.
            }

            if (ClearStaleLock(lockPath))
            {
                continue;
            }

            if (waited.Elapsed >= LockWait)
            {
                throw new RegisterBusyException(lockPath);
            }

            Thread.Sleep(LockRetryDelay);
        }
    }

    /// <summary>Deletes the lock file when it is older than <see cref="StaleLockAge"/>. True when it was deleted.</summary>
    private bool ClearStaleLock(string lockPath)
    {
        try
        {
            if (!File.Exists(lockPath))
            {
                return false;
            }

            var age = _environment.Now - new DateTimeOffset(File.GetLastWriteTimeUtc(lockPath), TimeSpan.Zero);
            if (age <= StaleLockAge)
            {
                return false;
            }

            File.Delete(lockPath);
            _log.Warn($"Removed a register lock file left over from a crash ('{lockPath}', {age.TotalMinutes:F0} minutes old).");
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private void ReleaseLock(string lockPath)
    {
        try
        {
            File.Delete(lockPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log.Warn($"Could not delete the register lock file '{lockPath}': {ex.Message}. It will be removed as stale after {StaleLockAge.TotalMinutes:F0} minutes.");
        }
    }

    /// <summary>
    /// Reads a register file that is about to be written back or deleted. A missing file is empty. A file that does
    /// not parse, or whose read gives any warning, throws <see cref="RegisterUnreadableException"/> (spec 02 question 19).
    /// I/O errors are left to the caller.
    /// </summary>
    private static PartRegister ReadStrict(string path)
    {
        if (!File.Exists(path))
        {
            return PartRegister.Empty;
        }

        var text = File.ReadAllText(path);
        RegisterReadResult read;
        try
        {
            read = RegisterCsv.Read(text);
        }
        catch (FormatException ex)
        {
            throw new RegisterUnreadableException(path, [ex.Message], ex);
        }

        if (read.Warnings.Count > 0)
        {
            throw new RegisterUnreadableException(path, read.Warnings);
        }

        return new PartRegister(read.Entries);
    }

    /// <summary>Reads a local register file for display and offline numbering. Never throws for file problems.</summary>
    private IReadOnlyList<RegisterEntry> ReadLocalFile(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return [];
            }

            var read = RegisterCsv.Read(File.ReadAllText(path));
            foreach (var warning in read.Warnings)
            {
                _log.Warn($"Register file '{path}': {warning}");
            }

            return read.Entries;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException)
        {
            _log.Warn($"Could not read the register file '{path}': {ex.Message}. It is treated as empty.");
            return [];
        }
    }

    /// <summary>
    /// Reads the pending updates file before it is applied and deleted. A missing file is empty. A file that does not
    /// parse, or holds an update whose number or new path cannot be read, throws
    /// <see cref="RegisterUnreadableException"/>, so nothing is dropped (as spec 02 question 19 does for rows).
    /// </summary>
    private static IReadOnlyList<RegisterUpdate> ReadStrictUpdates(string path)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        var text = File.ReadAllText(path);
        var problems = new List<string>();
        var updates = ParseUpdates(text, problems);
        if (problems.Count > 0)
        {
            throw new RegisterUnreadableException(path, problems);
        }

        return updates;
    }

    /// <summary>The pending updates for <see cref="ReadLocal"/>. Never throws for file problems; problems are logged.</summary>
    private IReadOnlyList<RegisterUpdate> ReadLocalUpdates()
    {
        try
        {
            if (!File.Exists(PendingUpdatesFilePath))
            {
                return [];
            }

            var problems = new List<string>();
            var updates = ParseUpdates(File.ReadAllText(PendingUpdatesFilePath), problems);
            foreach (var problem in problems)
            {
                _log.Warn($"Register updates file '{PendingUpdatesFilePath}': {problem}");
            }

            return updates;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log.Warn($"Could not read the register updates file '{PendingUpdatesFilePath}': {ex.Message}. It is ignored.");
            return [];
        }
    }

    /// <summary>The updates in pending updates JSON, in file order. Each problem is added to <paramref name="problems"/>.</summary>
    private static List<RegisterUpdate> ParseUpdates(string text, List<string> problems)
    {
        var updates = new List<RegisterUpdate>();
        if (string.IsNullOrWhiteSpace(text))
        {
            return updates;
        }

        List<PendingUpdateRecord?>? records;
        try
        {
            records = JsonSerializer.Deserialize<List<PendingUpdateRecord?>>(text, UpdatesJsonOptions);
        }
        catch (JsonException ex)
        {
            problems.Add($"The file is not a list of register updates: {ex.Message}");
            return updates;
        }

        for (var i = 0; i < (records?.Count ?? 0); i++)
        {
            var record = records![i];
            if (record is null || !PartNumber.TryParse(record.Number, out var number) || string.IsNullOrWhiteSpace(record.FilePath))
            {
                problems.Add($"Update {i + 1}: no part number or no new file path.");
                continue;
            }

            updates.Add(new RegisterUpdate(
                number,
                record.OldFilePath ?? "",
                record.FilePath,
                record.Name ?? "",
                record.UpdatedAt ?? DateTimeOffset.MinValue));
        }

        return updates;
    }

    /// <summary>
    /// Adds one update to the pending updates file. The file is rewritten through a temporary file; one that cannot be
    /// read in full throws <see cref="RegisterUnreadableException"/> rather than lose what it holds.
    /// </summary>
    private void AppendPendingUpdate(RegisterUpdate update)
    {
        Directory.CreateDirectory(LocalFolder);

        var updates = ReadStrictUpdates(PendingUpdatesFilePath).ToList();
        updates.Add(update);

        var records = updates.Select(u => new PendingUpdateRecord
        {
            Number = u.Number.ToString(),
            OldFilePath = u.OldFilePath,
            FilePath = u.FilePath,
            Name = u.Name,
            UpdatedAt = u.UpdatedAt,
            UpdatedBy = _environment.AssignedBy,
            Computer = _environment.ComputerName,
        });

        WriteReplacing(LocalFolder, PendingUpdatesFileName, JsonSerializer.Serialize(records, UpdatesJsonOptions));
    }

    private static readonly JsonSerializerOptions UpdatesJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>One update as the pending updates file stores it.</summary>
    private sealed class PendingUpdateRecord
    {
        public string? Number { get; set; }

        public string? OldFilePath { get; set; }

        public string? FilePath { get; set; }

        public string? Name { get; set; }

        public DateTimeOffset? UpdatedAt { get; set; }

        public string? UpdatedBy { get; set; }

        public string? Computer { get; set; }
    }

    /// <summary>Adds one row to the pending file, creating it with a header when it is missing or empty.</summary>
    private void AppendPending(RegisterEntry entry)
    {
        Directory.CreateDirectory(LocalFolder);

        var withHeader = RegisterCsv.Write([entry]);
        var row = withHeader[(RegisterCsv.Header.Length + RegisterCsv.NewLine.Length)..];

        var info = new FileInfo(PendingFilePath);
        if (!info.Exists || info.Length == 0)
        {
            File.WriteAllText(PendingFilePath, withHeader, FileEncoding);
            return;
        }

        // Append rather than rewrite, so a row this PC cannot read is never dropped.
        var existing = File.ReadAllText(PendingFilePath);
        var separator = existing.EndsWith('\n') || existing.EndsWith('\r') ? "" : RegisterCsv.NewLine;
        File.AppendAllText(PendingFilePath, separator + row, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    /// <summary>
    /// Writes <paramref name="text"/> to a temporary file in <paramref name="folder"/> that then replaces
    /// <paramref name="fileName"/>, so a failed write leaves the old file intact (as <c>SettingsStore.Save</c> does).
    /// </summary>
    private static void WriteReplacing(string folder, string fileName, string text)
    {
        var targetPath = Path.Combine(folder, fileName);
        var tempPath = Path.Combine(folder, $"{fileName}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream, FileEncoding))
            {
                writer.Write(text);
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }

            File.Move(tempPath, targetPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            }
        }
    }
}

/// <summary>A change to one register row made by Rename while the shared register could not be reached (task 047).</summary>
/// <param name="Number">The number whose row changes.</param>
/// <param name="OldFilePath">The file's path before the rename, which picks the row when the number is held twice; <c>""</c> when not known.</param>
/// <param name="FilePath">The file's new full path.</param>
/// <param name="Name">The new part name, with spaces.</param>
/// <param name="UpdatedAt">When the rename was made.</param>
public sealed record RegisterUpdate(PartNumber Number, string OldFilePath, string FilePath, string Name, DateTimeOffset UpdatedAt);

/// <summary>What <see cref="RegisterStore.UpdateEntry"/> did.</summary>
/// <param name="Found">
/// False when the register does not hold the number, so nothing was changed or queued. Offline, this is checked
/// against this PC's register.
/// </param>
/// <param name="Offline">
/// True when the shared register could not be reached: the update waits in the pending updates file until the next sync.
/// </param>
/// <param name="Duplicates">Duplicates found by the sync done under the lock; empty when offline.</param>
/// <param name="QueuedReason">
/// Why the update was queued although the shared folder answered (the lock was busy, a register file could not be read in
/// full, or an I/O error); null otherwise.
/// </param>
public sealed record RegisterUpdateResult(
    bool Found,
    bool Offline,
    IReadOnlyList<RegisterDuplicate> Duplicates,
    string? QueuedReason = null);
