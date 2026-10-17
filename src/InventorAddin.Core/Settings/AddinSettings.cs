namespace InventorAddin.Core.Settings;

/// <summary>
/// Per-user add-in settings, stored as JSON by <see cref="SettingsStore"/>.
/// Add new settings as properties with a default initialiser: a file written before
/// the property existed loads with that default.
/// </summary>
/// <remarks>
/// A record so that two instances with the same values compare equal. <see cref="Equals(AddinSettings?)"/> is
/// written out because <see cref="OtherCadFolders"/> and <see cref="PreferredMaterials"/> are lists, which a record would compare by reference: a new
/// setting must be added there and to <see cref="GetHashCode"/>.
/// </remarks>
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

    /// <summary>
    /// Aidan's main CAD folder (spec 02, question 23): the default <see cref="WorkFolder"/>. This is the only place
    /// it appears in code.
    /// </summary>
    public const string DefaultWorkFolder = @"C:\Users\4aida\OneDrive\Documents\CADING";

    /// <summary>
    /// The main CAD folder: where New Part (with no saved document open) and Mass Update start, and the first folder
    /// Rename searches. Defaults to <see cref="DefaultWorkFolder"/>; a null (for example <c>"workFolder": null</c> in
    /// the file) is stored as the default. Blank is kept as blank and means "no work folder".
    /// </summary>
    public string WorkFolder
    {
        get => _workFolder;
        set => _workFolder = value ?? DefaultWorkFolder;
    }

    private string _workFolder = DefaultWorkFolder;

    /// <summary>
    /// Other folders that hold CAD, which Rename searches after <see cref="WorkFolder"/>. Written to the file as a
    /// JSON array of paths. Empty by default; a null list is stored as empty and null entries are dropped. The list is
    /// copied when set, so instances made with <c>with</c> can share it safely.
    /// </summary>
    public IReadOnlyList<string> OtherCadFolders
    {
        get => _otherCadFolders;
        set => _otherCadFolders = value is null ? Array.Empty<string>() : value.Where(f => f is not null).ToArray();
    }

    private IReadOnlyList<string> _otherCadFolders = Array.Empty<string>();

    /// <summary>
    /// Other names that mean the user (old Designer and Detailer values), as typed: comma-separated. Task 051 parses
    /// it. <c>""</c> by default; a null is stored as <c>""</c>.
    /// </summary>
    public string OtherNamesForMe
    {
        get => _otherNamesForMe;
        set => _otherNamesForMe = value ?? string.Empty;
    }

    private string _otherNamesForMe = string.Empty;

    /// <summary>
    /// The material library (<c>.adsklib</c>) the Material and Finish window reads. <c>""</c> by default, which means
    /// the starter library shipped with the add-in (<see cref="Materials.LibraryPaths.MaterialLibrary"/>). A null is
    /// stored as <c>""</c>.
    /// </summary>
    public string MaterialLibraryPath
    {
        get => _materialLibraryPath;
        set => _materialLibraryPath = value ?? string.Empty;
    }

    private string _materialLibraryPath = string.Empty;

    /// <summary>
    /// The finish list (<c>.csv</c>). <c>""</c> by default, which means the starter list shipped with the add-in
    /// (<see cref="Materials.LibraryPaths.FinishList"/>). A null is stored as <c>""</c>.
    /// </summary>
    public string FinishListPath
    {
        get => _finishListPath;
        set => _finishListPath = value ?? string.Empty;
    }

    private string _finishListPath = string.Empty;

    /// <summary>
    /// The hole workbook (<c>.xlsx</c>) AWB Hole reads (spec 09, "Settings"). <c>""</c> by default, which means the
    /// starter workbook shipped with the add-in (<see cref="Materials.LibraryPaths.HoleWorkbook"/>). A null is stored
    /// as <c>""</c>.
    /// </summary>
    public string HoleWorkbookPath
    {
        get => _holeWorkbookPath;
        set => _holeWorkbookPath = value ?? string.Empty;
    }

    private string _holeWorkbookPath = string.Empty;

    /// <summary>
    /// "Use Inventor's Hole command" (spec 09, "Settings"): on gives Inventor's Hole button and H shortcut back; off,
    /// the default, gives them to AWB Hole. Only stored here; task 091 acts on it.
    /// </summary>
    public bool UseInventorHoleCommand { get; set; }

    /// <summary>
    /// Display names of the materials starred as preferred in the Material and Finish window (spec 03, question 11).
    /// Not shown in the Settings window. Empty by default; a null list is stored as empty and null entries are
    /// dropped. The list is copied when set. Changed through <see cref="Materials.LibraryPaths.TogglePreferred"/>.
    /// </summary>
    public IReadOnlyList<string> PreferredMaterials
    {
        get => _preferredMaterials;
        set => _preferredMaterials = value is null ? Array.Empty<string>() : value.Where(m => m is not null).ToArray();
    }

    private IReadOnlyList<string> _preferredMaterials = Array.Empty<string>();

    /// <summary>
    /// Value equality over every setting, comparing <see cref="OtherCadFolders"/> and <see cref="PreferredMaterials"/>
    /// entry by entry, in order.
    /// </summary>
    public bool Equals(AddinSettings? other) =>
        other is not null
        && SchemaVersion == other.SchemaVersion
        && ShowDeveloperTools == other.ShowDeveloperTools
        && DefaultDesigner == other.DefaultDesigner
        && RegisterFolder == other.RegisterFolder
        && DefaultUnits == other.DefaultUnits
        && WorkFolder == other.WorkFolder
        && OtherCadFolders.SequenceEqual(other.OtherCadFolders, StringComparer.Ordinal)
        && OtherNamesForMe == other.OtherNamesForMe
        && MaterialLibraryPath == other.MaterialLibraryPath
        && FinishListPath == other.FinishListPath
        && HoleWorkbookPath == other.HoleWorkbookPath
        && UseInventorHoleCommand == other.UseInventorHoleCommand
        && PreferredMaterials.SequenceEqual(other.PreferredMaterials, StringComparer.Ordinal);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(SchemaVersion);
        hash.Add(ShowDeveloperTools);
        hash.Add(DefaultDesigner, StringComparer.Ordinal);
        hash.Add(RegisterFolder, StringComparer.Ordinal);
        hash.Add(DefaultUnits);
        hash.Add(WorkFolder, StringComparer.Ordinal);
        foreach (var folder in OtherCadFolders)
            hash.Add(folder, StringComparer.Ordinal);
        hash.Add(OtherNamesForMe, StringComparer.Ordinal);
        hash.Add(MaterialLibraryPath, StringComparer.Ordinal);
        hash.Add(FinishListPath, StringComparer.Ordinal);
        hash.Add(HoleWorkbookPath, StringComparer.Ordinal);
        hash.Add(UseInventorHoleCommand);
        foreach (var material in PreferredMaterials)
            hash.Add(material, StringComparer.Ordinal);
        return hash.ToHashCode();
    }
}
