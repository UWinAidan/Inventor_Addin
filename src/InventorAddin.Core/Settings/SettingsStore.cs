using System.Text.Json;
using System.Text.Json.Serialization;

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
    // Used for writing; Load builds its own copy so it can collect warnings about unknown values.
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions(onUnknownUnits: null);

    private static JsonSerializerOptions CreateJsonOptions(Action<string>? onUnknownUnits) => new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new UnitsSystemConverter(onUnknownUnits) },
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

        // Unknown values that are read as a default rather than making the whole file invalid. Reported only when
        // the file loads; an invalid file gets the .bad warning alone.
        var unknownValues = new List<string>();
        var loadOptions = CreateJsonOptions(raw =>
            unknownValues.Add($"Settings file '{FilePath}' has an unknown defaultUnits value ({raw}). Metric is used."));

        AddinSettings? settings;
        string? problem;
        try
        {
            settings = JsonSerializer.Deserialize<AddinSettings>(json, loadOptions);
            problem = settings is null ? "the file does not contain a settings object" : null;
        }
        catch (JsonException ex)
        {
            settings = null;
            problem = ex.Message;
        }

        if (settings is not null)
        {
            warnings.AddRange(unknownValues);
            return new SettingsLoadResult(settings, warnings);
        }

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

    /// <summary>
    /// Reads and writes <see cref="UnitsSystem"/> as its member name. A name in any case is accepted. Anything else
    /// (an unknown name, a number, an object) reads as <see cref="UnitsSystem.Metric"/> and is reported through the
    /// callback, so one stale value does not make the whole file invalid. A JSON null reads as Metric without a report,
    /// as a missing value would.
    /// </summary>
    private sealed class UnitsSystemConverter : JsonConverter<UnitsSystem>
    {
        private const UnitsSystem Fallback = UnitsSystem.Metric;

        private readonly Action<string>? _onUnknown;

        public UnitsSystemConverter(Action<string>? onUnknown) => _onUnknown = onUnknown;

        public override UnitsSystem Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.Null:
                    return Fallback;

                case JsonTokenType.String:
                    var text = reader.GetString() ?? string.Empty;
                    foreach (var value in Enum.GetValues<UnitsSystem>())
                    {
                        if (string.Equals(value.ToString(), text, StringComparison.OrdinalIgnoreCase))
                            return value;
                    }
                    _onUnknown?.Invoke($"\"{text}\"");
                    return Fallback;

                case JsonTokenType.StartObject or JsonTokenType.StartArray:
                    reader.Skip();
                    _onUnknown?.Invoke(reader.TokenType == JsonTokenType.EndObject ? "an object" : "a list");
                    return Fallback;

                default:
                    // A number or true/false: a single token.
                    using (var doc = JsonDocument.ParseValue(ref reader))
                        _onUnknown?.Invoke(doc.RootElement.GetRawText());
                    return Fallback;
            }
        }

        public override void Write(Utf8JsonWriter writer, UnitsSystem value, JsonSerializerOptions options) =>
            writer.WriteStringValue(Enum.IsDefined(value) ? value.ToString() : Fallback.ToString());
    }
}
