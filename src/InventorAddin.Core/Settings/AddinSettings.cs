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

    /// <summary>
    /// The folder the shared part number register is kept in when nothing else has been chosen (spec 02,
    /// "The register: Location"). Aidan's choice; this is the only place it appears in code.
    /// </summary>
    public const string DefaultRegisterFolder = @"B:\aidan\AWB_Design_Data";

    /// <summary>
    /// The folder holding the shared part number register. Defaults to <see cref="DefaultRegisterFolder"/>; a null
    /// (for example <c>"registerFolder": null</c> in the file) is stored as the default. Blank is kept as blank and
    /// means "no shared register": every number is assigned from this PC's copy.
    /// </summary>
    public string RegisterFolder
    {
        get => _registerFolder;
        set => _registerFolder = value ?? DefaultRegisterFolder;
    }

    private string _registerFolder = DefaultRegisterFolder;

    /// <summary>
    /// The units new files are created in. Written to the file as the member name (<c>"defaultUnits": "Metric"</c>).
    /// <see cref="SettingsStore.Load"/> reads an unknown value as <see cref="UnitsSystem.Metric"/> with a warning.
    /// </summary>
    public UnitsSystem DefaultUnits { get; set; } = UnitsSystem.Metric;
}
