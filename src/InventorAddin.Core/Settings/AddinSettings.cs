namespace InventorAddin.Core.Settings;

/// <summary>
/// Per-user add-in settings, stored as JSON by <see cref="SettingsStore"/>.
/// Add new settings as properties with a default initialiser: a file written before
/// the property existed loads with that default.
/// </summary>
/// <remarks>A record so that two instances with the same values compare equal.</remarks>
public sealed record AddinSettings
{
    /// <summary>The schema version this build writes.</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>Schema version of the file. Kept for future migrations; read and written as-is.</summary>
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    /// <summary>Shows the Developer panel (model data export buttons). Off for end users.</summary>
    public bool ShowDeveloperTools { get; set; }

    /// <summary>
    /// Filled in as Designer in Part Properties when a file has none. <c>""</c> means no default.
    /// A null (for example <c>"defaultDesigner": null</c> in the file) is stored as <c>""</c>.
    /// </summary>
    public string DefaultDesigner
    {
        get => _defaultDesigner;
        set => _defaultDesigner = value ?? string.Empty;
    }

    private string _defaultDesigner = string.Empty;
}
