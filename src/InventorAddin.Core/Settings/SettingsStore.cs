using System.Text.Json;

namespace InventorAddin.Core.Settings;

/// <summary>Result of <see cref="SettingsStore.Load"/>: the settings to use and anything the caller should report.</summary>
public sealed class SettingsLoadResult
{
    public SettingsLoadResult(AddinSettings settings, IReadOnlyList<string> warnings)
    {
        Settings = settings;
        Warnings = warnings;
    }

    public AddinSettings Settings { get; }
    public IReadOnlyList<string> Warnings { get; }
}

/// <summary>
/// Loads and saves <see cref="AddinSettings"/> as <c>settings.json</c> in a folder chosen by the caller.
/// The store does not decide where that folder is.
/// </summary>
public sealed class SettingsStore
{
    public const string FileName = "settings.json";
    public const string BadFileSuffix = ".bad";

    // Private on purpose: settings files and model dumps (ModelJson) have different needs.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public SettingsStore(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
            throw new ArgumentException("A settings folder is required.", nameof(folder));

        Folder = folder;
        FilePath = Path.Combine(folder, FileName);
    }

    public string Folder { get; }
    public string FilePath { get; }

    /// <summary>
    /// Reads the settings file. Never throws for a missing, unreadable or invalid file: it returns
    /// defaults instead. An invalid file is renamed to <c>settings.json.bad</c> and a warning is returned.
    /// </summary>
    public SettingsLoadResult Load()
    {
        var warnings = new List<string>();

        if (!File.Exists(FilePath))
            return new SettingsLoadResult(new AddinSettings(), warnings);

        string json;
        try
        {
            json = File.ReadAllText(FilePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            warnings.Add($"Could not read settings file '{FilePath}': {ex.Message}. Using default settings.");
            return new SettingsLoadResult(new AddinSettings(), warnings);
        }

        AddinSettings? settings;
        string? problem;
        try
        {
            settings = JsonSerializer.Deserialize<AddinSettings>(json, JsonOptions);
            problem = settings is null ? "the file does not contain a settings object" : null;
        }
        catch (JsonException ex)
        {
            settings = null;
            problem = ex.Message;
        }

        if (settings is not null)
            return new SettingsLoadResult(settings, warnings);

        var badPath = FilePath + BadFileSuffix;
        try
        {
            File.Move(FilePath, badPath, overwrite: true);
            warnings.Add($"Settings file '{FilePath}' is not valid ({problem}). It was renamed to '{badPath}' and default settings are used.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            warnings.Add($"Settings file '{FilePath}' is not valid ({problem}) and could not be renamed to '{badPath}': {ex.Message}. Default settings are used.");
        }

        return new SettingsLoadResult(new AddinSettings(), warnings);
    }

    /// <summary>
    /// Writes the settings file, creating the folder if needed. The JSON goes to a temporary file in the
    /// same folder first, which then replaces <c>settings.json</c>, so a failed write leaves the old file intact.
    /// Throws on I/O failure; the caller decides how to report it.
    /// </summary>
    public void Save(AddinSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        Directory.CreateDirectory(Folder);

        var json = JsonSerializer.Serialize(settings, JsonOptions);
        var tempPath = Path.Combine(Folder, $"{FileName}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream))
            {
                writer.Write(json);
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }

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
