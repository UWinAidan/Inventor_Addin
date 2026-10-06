using System.Text;
using InventorAddin.Core.Logging;

namespace InventorAddin.Core.Tests.Logging;

public sealed class FileLogTests : IDisposable
{
    private static readonly DateTime FixedTime = new(2026, 1, 2, 3, 4, 5, 6, DateTimeKind.Local);
    private const string FixedStamp = "2026-01-02 03:04:05.006";

    private readonly string _root;
    private readonly string _folder;

    public FileLogTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "InventorAddinTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _folder = Path.Combine(_root, "logs");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    // The log file names follow the brand; BrandingTests pins the brand itself.
    private const string Base = Branding.LogFileBaseName;
    private const string Current = $"{Base}.log";

    private string LogPath => Path.Combine(_folder, Current);
    private string RolledPath(int index) => Path.Combine(_folder, $"{Base}.{index}.log");

    private FileLog CreateLog(long maxFileSizeBytes = FileLog.DefaultMaxFileSizeBytes, int keepCount = FileLog.DefaultKeepCount) =>
        new(_folder, maxFileSizeBytes, keepCount, () => FixedTime);

    private static int ByteCount(string line) => Encoding.UTF8.GetByteCount(line + Environment.NewLine);

    private string[] LogFileNames() =>
        Directory.GetFiles(_folder).Select(p => Path.GetFileName(p)!).OrderBy(n => n, StringComparer.Ordinal).ToArray();

    // ---- construction ----

    [Fact]
    public void Constructor_UsesBrandedLogFileInGivenFolder_WithDefaults()
    {
        var log = new FileLog(_folder);

        Assert.Equal(_folder, log.Folder);
        Assert.Equal(LogPath, log.FilePath);
        Assert.Equal(RolledPath(3), log.RolledFilePath(3));
        Assert.Equal(1024 * 1024, log.MaxFileSizeBytes);
        Assert.Equal(5, log.KeepCount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_RejectsEmptyFolder(string folder)
    {
        Assert.Throws<ArgumentException>(() => new FileLog(folder));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_RejectsNonPositiveMaxSize(long maxFileSizeBytes)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FileLog(_folder, maxFileSizeBytes));
    }

    [Fact]
    public void Constructor_RejectsNegativeKeepCount()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FileLog(_folder, keepCount: -1));
    }

    [Fact]
    public void Constructor_DoesNotTouchTheFileSystem()
    {
        _ = CreateLog();

        Assert.False(Directory.Exists(_folder));
    }

    // ---- line format ----

    [Fact]
    public void FirstWrite_CreatesFolderAndFile()
    {
        var nested = Path.Combine(_folder, "a", "b");
        var log = new FileLog(nested, clock: () => FixedTime);

        log.Info("hello");

        Assert.True(File.Exists(Path.Combine(nested, Current)));
    }

    [Fact]
    public void Info_WritesOneLine_WithTimestampLevelAndMessage()
    {
        CreateLog().Info("hello world");

        Assert.Equal(new[] { $"{FixedStamp} INFO hello world" }, File.ReadAllLines(LogPath));
    }

    [Fact]
    public void Levels_AreInfoWarnError()
    {
        var log = CreateLog();

        log.Info("a");
        log.Warn("b");
        log.Error("c");

        Assert.Equal(
            new[] { $"{FixedStamp} INFO a", $"{FixedStamp} WARN b", $"{FixedStamp} ERROR c" },
            File.ReadAllLines(LogPath));
    }

    [Fact]
    public void Timestamp_ComesFromInjectedClock_InTwentyFourHourFormat()
    {
        var time = new DateTime(2025, 12, 31, 23, 59, 58, 7, DateTimeKind.Local);
        new FileLog(_folder, clock: () => time).Info("x");

        Assert.Equal("2025-12-31 23:59:58.007 INFO x", File.ReadAllLines(LogPath).Single());
    }

    [Fact]
    public void Timestamp_UsesInvariantDigitsAndSeparators_UnderAnyCulture()
    {
        var previous = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("ar-SA");
            CreateLog().Info("x");
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = previous;
        }

        Assert.Equal($"{FixedStamp} INFO x", File.ReadAllLines(LogPath).Single());
    }

    [Fact]
    public void LineBreaksInMessage_AreReplaced_SoTheEntryStaysOneLine()
    {
        CreateLog().Warn("one\ntwo\r\nthree\rfour");

        Assert.Equal(new[] { $"{FixedStamp} WARN one two three four" }, File.ReadAllLines(LogPath));
    }

    [Fact]
    public void NullMessage_IsWrittenAsEmpty()
    {
        CreateLog().Info(null!);

        Assert.Equal(new[] { $"{FixedStamp} INFO " }, File.ReadAllLines(LogPath));
    }

    [Fact]
    public void Entries_AreAppended_AcrossInstances()
    {
        CreateLog().Info("first");
        CreateLog().Info("second");

        Assert.Equal(new[] { $"{FixedStamp} INFO first", $"{FixedStamp} INFO second" }, File.ReadAllLines(LogPath));
    }

    // ---- exceptions ----

    [Fact]
    public void Error_WithException_WritesExceptionInFullAfterTheLine()
    {
        Exception ex;
        try
        {
            throw new InvalidOperationException("outer", new FormatException("inner"));
        }
        catch (Exception caught)
        {
            ex = caught;
        }

        var log = CreateLog();
        log.Error("command failed", ex);
        log.Info("after");

        var expected =
            $"{FixedStamp} ERROR command failed" + Environment.NewLine +
            ex.ToString() + Environment.NewLine +
            $"{FixedStamp} INFO after" + Environment.NewLine;
        Assert.Equal(expected, File.ReadAllText(LogPath));
        Assert.Contains("inner", ex.ToString());
        Assert.Contains(nameof(Error_WithException_WritesExceptionInFullAfterTheLine), ex.ToString());
    }

    [Fact]
    public void Error_WithoutException_WritesOnlyTheLine()
    {
        CreateLog().Error("no exception", null);

        Assert.Equal($"{FixedStamp} ERROR no exception" + Environment.NewLine, File.ReadAllText(LogPath));
    }

    // ---- rollover ----

    [Fact]
    public void Rollover_ShiftsFilesNewestFirst()
    {
        // A max size of 1 byte means every write to a non-empty file rolls it, so each file holds one entry.
        var log = CreateLog(maxFileSizeBytes: 1, keepCount: 5);

        log.Info("m1");
        log.Info("m2");
        log.Info("m3");
        log.Info("m4");

        Assert.Equal($"{FixedStamp} INFO m4", File.ReadAllLines(LogPath).Single());
        Assert.Equal($"{FixedStamp} INFO m3", File.ReadAllLines(RolledPath(1)).Single());
        Assert.Equal($"{FixedStamp} INFO m2", File.ReadAllLines(RolledPath(2)).Single());
        Assert.Equal($"{FixedStamp} INFO m1", File.ReadAllLines(RolledPath(3)).Single());
        Assert.False(File.Exists(RolledPath(4)));
    }

    [Fact]
    public void Rollover_HappensOnlyWhenTheWriteWouldExceedTheMaximum()
    {
        var entrySize = ByteCount($"{FixedStamp} INFO aa");
        var log = CreateLog(maxFileSizeBytes: entrySize * 2);

        log.Info("aa");
        log.Info("bb"); // reaches the maximum exactly: no roll
        Assert.False(File.Exists(RolledPath(1)));
        Assert.Equal(entrySize * 2, new FileInfo(LogPath).Length);

        log.Info("cc"); // would pass the maximum: roll first
        Assert.Equal(new[] { $"{FixedStamp} INFO aa", $"{FixedStamp} INFO bb" }, File.ReadAllLines(RolledPath(1)));
        Assert.Equal(new[] { $"{FixedStamp} INFO cc" }, File.ReadAllLines(LogPath));
    }

    [Fact]
    public void Rollover_ExceptionTextCountsTowardsTheSize()
    {
        var entrySize = ByteCount($"{FixedStamp} INFO aa");
        var log = CreateLog(maxFileSizeBytes: entrySize * 2);

        log.Info("aa");
        log.Error("bb", new InvalidOperationException("boom"));

        Assert.Equal(new[] { $"{FixedStamp} INFO aa" }, File.ReadAllLines(RolledPath(1)));
        Assert.StartsWith($"{FixedStamp} ERROR bb", File.ReadAllText(LogPath));
    }

    [Fact]
    public void OversizedEntry_IntoEmptyFile_IsWrittenWithoutRolling()
    {
        var log = CreateLog(maxFileSizeBytes: 5);

        log.Info("this entry is longer than five bytes");

        Assert.Equal(new[] { Current }, LogFileNames());
        Assert.Single(File.ReadAllLines(LogPath));
    }

    [Fact]
    public void KeepCount_DeletesFilesBeyondIt()
    {
        var log = CreateLog(maxFileSizeBytes: 1, keepCount: 2);

        for (var i = 1; i <= 6; i++)
            log.Info($"m{i}");

        Assert.Equal(new[] { $"{Base}.1.log", $"{Base}.2.log", Current }, LogFileNames());
        Assert.Equal($"{FixedStamp} INFO m6", File.ReadAllLines(LogPath).Single());
        Assert.Equal($"{FixedStamp} INFO m5", File.ReadAllLines(RolledPath(1)).Single());
        Assert.Equal($"{FixedStamp} INFO m4", File.ReadAllLines(RolledPath(2)).Single());
    }

    [Fact]
    public void KeepCountZero_KeepsNoOldFiles()
    {
        var log = CreateLog(maxFileSizeBytes: 1, keepCount: 0);

        log.Info("m1");
        log.Info("m2");
        log.Info("m3");

        Assert.Equal(new[] { Current }, LogFileNames());
        Assert.Equal($"{FixedStamp} INFO m3", File.ReadAllLines(LogPath).Single());
    }

    [Fact]
    public void Rollover_DeletesOlderFilesLeftByALargerKeepCount_AndLeavesOtherFilesAlone()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(RolledPath(4), "old 4");
        File.WriteAllText(RolledPath(9), "old 9");
        File.WriteAllText(Path.Combine(_folder, "settings.json"), "{}");
        File.WriteAllText(Path.Combine(_folder, $"{Base}.notes.log"), "not a rolled file");
        var log = CreateLog(maxFileSizeBytes: 1, keepCount: 2);

        log.Info("m1");
        log.Info("m2");

        Assert.Equal(
            new[] { "settings.json", $"{Base}.1.log", Current, $"{Base}.notes.log" }.OrderBy(n => n, StringComparer.Ordinal),
            LogFileNames());
    }

    // ---- never throws ----

    [Fact]
    public void LockedFile_DoesNotThrow_AndLoggingResumesWhenReleased()
    {
        var log = CreateLog();
        log.Info("before");

        using (new FileStream(LogPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            // Whether this entry is dropped depends on the platform's locking; only "no throw" is asserted.
            var ex = Record.Exception(() =>
            {
                log.Info("while locked");
                log.Error("while locked", new InvalidOperationException("boom"));
            });
            Assert.Null(ex);
        }

        log.Info("after");

        var lines = File.ReadAllLines(LogPath);
        Assert.Equal($"{FixedStamp} INFO before", lines.First());
        Assert.Equal($"{FixedStamp} INFO after", lines.Last());
    }

    [Fact]
    public void LockedFile_DuringRollover_DoesNotThrow()
    {
        var log = CreateLog(maxFileSizeBytes: 1);
        log.Info("before");

        using (new FileStream(LogPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var ex = Record.Exception(() => log.Info("while locked"));
            Assert.Null(ex);
        }

        log.Info("after");
        Assert.Equal($"{FixedStamp} INFO after", File.ReadAllLines(LogPath).Last());
    }

    [Fact]
    public void FolderPathIsAFile_DoesNotThrow_AndWritesNothing()
    {
        File.WriteAllText(_folder, "I am a file");
        var log = CreateLog();

        var ex = Record.Exception(() =>
        {
            log.Info("a");
            log.Warn("b");
            log.Error("c", new InvalidOperationException("boom"));
        });

        Assert.Null(ex);
        Assert.Equal("I am a file", File.ReadAllText(_folder));
    }

    [Fact]
    public void LogFilePathIsADirectory_DoesNotThrow()
    {
        Directory.CreateDirectory(LogPath);
        var log = CreateLog(maxFileSizeBytes: 1);

        var ex = Record.Exception(() =>
        {
            log.Info("a");
            log.Info("b");
        });

        Assert.Null(ex);
        Assert.True(Directory.Exists(LogPath));
    }

    [Fact]
    public void ThrowingClock_DoesNotThrow()
    {
        var log = new FileLog(_folder, clock: () => throw new InvalidOperationException("clock broke"));

        Assert.Null(Record.Exception(() => log.Info("x")));
    }

    // ---- threads ----

    [Fact]
    public void ConcurrentWrites_LoseNoEntries_AndDoNotInterleave()
    {
        const int threads = 8;
        const int perThread = 200;
        var log = CreateLog(maxFileSizeBytes: 4096, keepCount: 1000);

        Parallel.For(0, threads, new ParallelOptions { MaxDegreeOfParallelism = threads }, t =>
        {
            for (var i = 0; i < perThread; i++)
                log.Info($"t{t:D2} i{i:D3}");
        });

        var lines = Directory.GetFiles(_folder).SelectMany(File.ReadAllLines).ToList();
        Assert.Equal(threads * perThread, lines.Count);
        Assert.All(lines, line => Assert.Matches($"^{FixedStamp} INFO t\\d\\d i\\d\\d\\d$", line));
        Assert.Equal(threads * perThread, lines.Distinct().Count());
        Assert.All(Directory.GetFiles(_folder), path => Assert.True(new FileInfo(path).Length <= 4096));
    }

    // ---- NullLog ----

    [Fact]
    public void NullLog_DoesNothing_AndDoesNotThrow()
    {
        ILog log = NullLog.Instance;

        var ex = Record.Exception(() =>
        {
            log.Info("a");
            log.Warn("b");
            log.Error("c");
            log.Error("d", new InvalidOperationException());
        });

        Assert.Null(ex);
        Assert.False(Directory.Exists(_folder));
    }
}
