using System.Diagnostics;
using System.Text;
using InventorAddin.Core.Logging;

namespace InventorAddin.Core.Numbering;

/// <summary>
/// Assigns part numbers and keeps the register on disk (spec 02, "The register"): the shared file in the register
/// folder when it can be reached, this PC's copy and pending entries when it cannot, a lock file while writing, and a
/// sync that reports duplicates. The store does not decide where the folders are. Everything is synchronous apart from
/// the reachability probe.
/// </summary>
public sealed class RegisterStore
{
    public const string SharedFileName = "part-register.csv";
    public const string LockFileName = "part-register.lock";
    public const string LocalFileName = "part-register.csv";
    public const string PendingFileName = "pending.csv";

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

    /// <summary>
    /// The local copy followed by the pending entries, without touching the shared folder. A missing file reads as
    /// empty; an unreadable one reads as empty with a logged warning. Never throws for file problems.
    /// </summary>
    public PartRegister ReadLocal()
    {
        var local = ReadLocalFile(LocalFilePath);
        var pending = ReadLocalFile(PendingFilePath);
        return new PartRegister(local.Concat(pending));
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
    /// Takes the lock, reads the shared register and the pending entries, merges them, lets <paramref name="change"/>
    /// add to the result, writes the shared register, deletes the pending file and writes the local copy. The lock is
    /// released in all cases. Returns the duplicates the merge found.
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

            var sync = PartRegister.Merge(shared, pending.Entries);
            foreach (var duplicate in sync.Duplicates)
            {
                _log.Warn(
                    $"Duplicate part number {duplicate.Number}: '{duplicate.Existing.FilePath}' " +
                    $"and '{duplicate.Added.FilePath}'. Both rows are kept in the register.");
            }

            var result = change(sync.Register);

            // Rows are only ever added, so an unchanged count means an unchanged register: leave the NAS file alone.
            if (result.Entries.Count != shared.Entries.Count)
            {
                WriteReplacing(SharedFolder!, SharedFileName, RegisterCsv.Write(result.Entries));
            }

            if (File.Exists(PendingFilePath))
            {
                File.Delete(PendingFilePath);
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
