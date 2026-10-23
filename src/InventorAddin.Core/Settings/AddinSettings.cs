using InventorAddin.Core.Holes;

namespace InventorAddin.Core.Settings;

/// <summary>
/// Per-user add-in settings, stored as JSON by <see cref="SettingsStore"/>.
/// Add new settings as properties with a default initialiser: a file written before
/// the property existed loads with that default.
/// </summary>
/// <remarks>
/// A record so that two instances with the same values compare equal. <see cref="Equals(AddinSettings?)"/> is
/// written out because <see cref="OtherCadFolders"/> and <see cref="PreferredMaterials"/> are lists, and
/// <see cref="LastDowelSeatsMm"/> and <see cref="LastDowelSeatsIn"/> maps, which a record would compare by reference: a
/// new setting must be added there and to <see cref="GetHashCode"/>.
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
    /// The depth, in mm, of the last blind clearance or dowel hole kept with OK in Metric (spec 09, "Blind holes";
    /// task 097). A new blind hole in Metric starts at it. Not shown in the Settings window. Null until a hole is made;
    /// a value that is not greater than zero is stored as null. Changed through
    /// <see cref="Holes.HoleLastDepths.Remember"/>.
    /// </summary>
    public decimal? LastBlindDepthMm
    {
        get => _lastBlindDepthMm;
        set => _lastBlindDepthMm = value is > 0 ? value : null;
    }

    private decimal? _lastBlindDepthMm;

    /// <summary>As <see cref="LastBlindDepthMm"/>, in inches, for Imperial.</summary>
    public decimal? LastBlindDepthIn
    {
        get => _lastBlindDepthIn;
        set => _lastBlindDepthIn = value is > 0 ? value : null;
    }

    private decimal? _lastBlindDepthIn;

    /// <summary>
    /// The seat of the last dowel hole kept with OK in Metric, by dowel size, in mm (spec 09 question 14; task 106):
    /// written to the file as an object keyed by the Dowel tab's Size (<c>"6 mm": { "cboreDiameter": 7, ... }</c>), a
    /// value not known left out. A new seat on a dowel of that size starts at it. Not shown in the Settings window.
    /// Empty by default; a null map is stored as empty, blank keys and null entries are dropped, keys match ignoring
    /// case (the first of two such keys wins). The map is copied when set. Changed through
    /// <see cref="HoleLastDowelSeats.Remember"/>.
    /// </summary>
    public IReadOnlyDictionary<string, HoleDowelSeat> LastDowelSeatsMm
    {
        get => _lastDowelSeatsMm;
        set => _lastDowelSeatsMm = DowelSeatMap(value);
    }

    private IReadOnlyDictionary<string, HoleDowelSeat> _lastDowelSeatsMm = DowelSeatMap(null);

    /// <summary>As <see cref="LastDowelSeatsMm"/>, in inches, for Imperial.</summary>
    public IReadOnlyDictionary<string, HoleDowelSeat> LastDowelSeatsIn
    {
        get => _lastDowelSeatsIn;
        set => _lastDowelSeatsIn = DowelSeatMap(value);
    }

    private IReadOnlyDictionary<string, HoleDowelSeat> _lastDowelSeatsIn = DowelSeatMap(null);

    /// <summary>
    /// A copy of a dowel seat map as the settings keep it: keys trimmed and matched ignoring case (the first wins),
    /// blank keys and null entries dropped; empty for null.
    /// </summary>
    public static IReadOnlyDictionary<string, HoleDowelSeat> DowelSeatMap(IEnumerable<KeyValuePair<string, HoleDowelSeat>>? map)
    {
        var copy = new Dictionary<string, HoleDowelSeat>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, seat) in map ?? [])
        {
            var trimmed = (key ?? "").Trim();
            if (trimmed.Length > 0 && seat is not null)
                copy.TryAdd(trimmed, seat);
        }

        return copy;
    }

    private static bool SameDowelSeats(
        IReadOnlyDictionary<string, HoleDowelSeat> a, IReadOnlyDictionary<string, HoleDowelSeat> b) =>
        a.Count == b.Count
        && a.All(entry => b.TryGetValue(entry.Key, out var other) && other == entry.Value
            && b.Keys.Contains(entry.Key, StringComparer.Ordinal));

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
        && LastBlindDepthMm == other.LastBlindDepthMm
        && LastBlindDepthIn == other.LastBlindDepthIn
        && SameDowelSeats(LastDowelSeatsMm, other.LastDowelSeatsMm)
        && SameDowelSeats(LastDowelSeatsIn, other.LastDowelSeatsIn)
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
        hash.Add(LastBlindDepthMm);
        hash.Add(LastBlindDepthIn);
        hash.Add(LastDowelSeatsMm.Count);
        hash.Add(LastDowelSeatsIn.Count);
        foreach (var material in PreferredMaterials)
            hash.Add(material, StringComparer.Ordinal);
        return hash.ToHashCode();
    }
}
