using System.Text.Json;

namespace InventorAddin.Core.Settings;

/// <summary>
/// Where a resizable window was and how big, in WPF device-independent units: the normal (not maximised) bounds, and
/// whether it was maximised.
/// </summary>
public sealed record WindowPlacement(double Left, double Top, double Width, double Height, bool Maximized);

/// <summary>What one window remembers between sessions: its placement, if it is resizable, and its collapsed sections.</summary>
public sealed class RememberedWindow
{
    public WindowPlacement? Placement { get; set; }

    /// <summary>The names of the sections that were collapsed when the window last closed. All others start expanded.</summary>
    public List<string> CollapsedSections { get; set; } = new();
}

/// <summary>Every window's remembered state, by window key (for example <c>Settings</c>).</summary>
public sealed class RememberedWindows
{
    public Dictionary<string, RememberedWindow> Windows { get; set; } = new(StringComparer.Ordinal);

    /// <summary>The state remembered for <paramref name="key"/>, or a new empty one.</summary>
    public RememberedWindow For(string key) =>
        Windows.TryGetValue(key, out var window) && window is not null ? window : new RememberedWindow();
}

/// <summary>
/// Loads and saves <see cref="RememberedWindows"/> as <c>windows.json</c> beside <c>settings.json</c> (task 079). It is
/// kept apart from the settings because it is written whenever a window closes, Cancel included, while settings are
/// written only by Save. Nothing in it matters much: a missing or unreadable file means every window opens as it would
/// the first time.
/// </summary>
public sealed class WindowStateStore
{
    public const string FileName = "windows.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public WindowStateStore(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
            throw new ArgumentException("A folder is required.", nameof(folder));

        Folder = folder;
        FilePath = Path.Combine(folder, FileName);
    }

    public string Folder { get; }
    public string FilePath { get; }

    /// <summary>
    /// Reads the file. Never throws: a missing file gives an empty state, and an unreadable or invalid one gives an
    /// empty state and a reason in <paramref name="warning"/>.
    /// </summary>
    public RememberedWindows Load(out string? warning)
    {
        warning = null;
        try
        {
            if (!File.Exists(FilePath))
                return new RememberedWindows();

            var state = JsonSerializer.Deserialize<RememberedWindows>(File.ReadAllText(FilePath), JsonOptions);
            if (state?.Windows is null)
                return new RememberedWindows();

            // Keys compared exactly, whatever the deserializer built.
            state.Windows = new Dictionary<string, RememberedWindow>(state.Windows, StringComparer.Ordinal);
            return state;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            warning = $"Could not read '{FilePath}': {ex.Message}. Windows open at their usual size.";
            return new RememberedWindows();
        }
    }

    /// <summary>
    /// Replaces the state remembered for <paramref name="key"/> and writes the file, keeping the other windows'
    /// entries as they are on disk. Written through a temporary file. Throws on I/O failure.
    /// </summary>
    public void Save(string key, RememberedWindow window)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(window);

        var state = Load(out _);
        state.Windows[key] = window;

        Directory.CreateDirectory(Folder);
        var tempPath = Path.Combine(Folder, $"{FileName}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(tempPath, JsonSerializer.Serialize(state, JsonOptions));
            File.Move(tempPath, FilePath, overwrite: true);
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
