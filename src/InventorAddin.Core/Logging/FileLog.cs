using System.Globalization;
using System.Text;

namespace InventorAddin.Core.Logging;

/// <summary>
/// Appends entries to the log file (<see cref="Branding.LogFileName"/>) in a folder chosen by the caller, rolling the file over by size.
/// The file is opened, appended to and closed on every write, so it can be read while the add-in runs.
/// </summary>
public sealed class FileLog : ILog
{
    public const string FileName = Branding.LogFileName;
    public const long DefaultMaxFileSizeBytes = 1024 * 1024;
    public const int DefaultKeepCount = 5;
    public const string TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff";

    private const string BaseName = Branding.LogFileBaseName;
    private const string Extension = ".log";

    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private readonly object _sync = new();
    private readonly Func<DateTime> _clock;

    /// <param name="folder">Folder for the log files. Created on first write if it does not exist.</param>
    /// <param name="maxFileSizeBytes">Size the current file may reach before it rolls over.</param>
    /// <param name="keepCount">Number of rolled-over files to keep (<c>{base name}.1.log</c> and up). Zero keeps none.</param>
    /// <param name="clock">Source of the local timestamp on each entry. Defaults to <see cref="DateTime.Now"/>.</param>
    public FileLog(
        string folder,
        long maxFileSizeBytes = DefaultMaxFileSizeBytes,
        int keepCount = DefaultKeepCount,
        Func<DateTime>? clock = null)
    {
        if (string.IsNullOrWhiteSpace(folder))
            throw new ArgumentException("A log folder is required.", nameof(folder));
        if (maxFileSizeBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxFileSizeBytes), maxFileSizeBytes, "The maximum file size must be positive.");
        if (keepCount < 0)
            throw new ArgumentOutOfRangeException(nameof(keepCount), keepCount, "The keep count cannot be negative.");

        Folder = folder;
        FilePath = Path.Combine(folder, FileName);
        MaxFileSizeBytes = maxFileSizeBytes;
        KeepCount = keepCount;
        _clock = clock ?? (() => DateTime.Now);
    }

    public string Folder { get; }
    public string FilePath { get; }
    public long MaxFileSizeBytes { get; }
    public int KeepCount { get; }

    /// <summary>Path of the rolled-over file with the given index: 1 is the newest.</summary>
    public string RolledFilePath(int index) =>
        Path.Combine(Folder, $"{BaseName}.{index.ToString(CultureInfo.InvariantCulture)}{Extension}");

    public void Info(string message) => Write("INFO", message, null);

    public void Warn(string message) => Write("WARN", message, null);

    public void Error(string message, Exception? ex = null) => Write("ERROR", message, ex);

    private void Write(string level, string? message, Exception? ex)
    {
        try
        {
            var text = FormatEntry(level, message, ex);
            var bytes = Utf8NoBom.GetBytes(text);

            lock (_sync)
            {
                Directory.CreateDirectory(Folder);

                var file = new FileInfo(FilePath);
                if (file.Exists && file.Length > 0 && file.Length + bytes.Length > MaxFileSizeBytes)
                    TryRoll();

                using var stream = new FileStream(FilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
                stream.Write(bytes, 0, bytes.Length);
            }
        }
        catch
        {
            // Logging must never fail the caller. The entry is dropped.
        }
    }

    private string FormatEntry(string level, string? message, Exception? ex)
    {
        var sb = new StringBuilder();
        sb.Append(_clock().ToString(TimestampFormat, CultureInfo.InvariantCulture));
        sb.Append(' ').Append(level).Append(' ');
        sb.Append(ToSingleLine(message));
        sb.Append(Environment.NewLine);

        if (ex is not null)
        {
            sb.Append(ex.ToString());
            sb.Append(Environment.NewLine);
        }

        return sb.ToString();
    }

    // Each entry's first line is one line, so line breaks inside the message are replaced with spaces.
    private static string ToSingleLine(string? message)
    {
        if (string.IsNullOrEmpty(message))
            return string.Empty;

        return message.Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ');
    }

    // If a rollover fails (for example, an old file is held open elsewhere), the entry is still appended
    // to the current file, which grows past the limit until a later rollover succeeds.
    private void TryRoll()
    {
        try
        {
            Roll();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    // Shifts the current .log -> .1 -> .2 ... and deletes anything numbered beyond KeepCount.
    private void Roll()
    {
        DeleteRolledFilesBeyondKeepCount();

        if (KeepCount == 0)
        {
            File.Delete(FilePath);
            return;
        }

        for (var i = KeepCount - 1; i >= 1; i--)
        {
            var source = RolledFilePath(i);
            if (File.Exists(source))
                File.Move(source, RolledFilePath(i + 1), overwrite: true);
        }

        File.Move(FilePath, RolledFilePath(1), overwrite: true);
    }

    private void DeleteRolledFilesBeyondKeepCount()
    {
        var prefix = BaseName + ".";
        foreach (var path in Directory.GetFiles(Folder, $"{BaseName}.*{Extension}"))
        {
            // Re-check the name: on Windows a "*.log" pattern also matches longer extensions such as ".logx".
            var name = Path.GetFileName(path);
            if (!name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                || !name.EndsWith(Extension, StringComparison.OrdinalIgnoreCase)
                || name.Length <= prefix.Length + Extension.Length)
                continue;

            var middle = name.Substring(prefix.Length, name.Length - prefix.Length - Extension.Length);
            if (int.TryParse(middle, NumberStyles.None, CultureInfo.InvariantCulture, out var index) && index > KeepCount)
                File.Delete(path);
        }
    }
}
