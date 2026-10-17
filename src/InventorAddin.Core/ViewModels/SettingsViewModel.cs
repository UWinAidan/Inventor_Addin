using InventorAddin.Core.Logging;
using InventorAddin.Core.Materials;
using InventorAddin.Core.PartProperties;
using InventorAddin.Core.Settings;

namespace InventorAddin.Core.ViewModels;

/// <summary>One entry in the "New file units" dropdown.</summary>
public sealed record UnitsOption(UnitsSystem Units, string DisplayText);

/// <summary>
/// View-model for the Settings window. Edits a copy of the settings it is given; nothing is written
/// until <see cref="SaveCommand"/> runs, so closing or cancelling the window discards the edits.
/// </summary>
public sealed class SettingsViewModel : ObservableObject
{
    /// <summary>
    /// Only the developer tools need a restart: the ribbon is built at startup. Other settings (the default
    /// designer, the other names, the units for new files and the folders) are read from <c>AddinServices.Settings</c> each
    /// time a command runs.
    /// </summary>
    public const string RestartNoticeText = "Showing or hiding developer tools takes effect the next time Inventor starts.";

    public const string DefaultDesignerFieldName = "Default designer";

    public const string OtherNamesFieldName = "Other names";

    /// <summary>
    /// The other CAD folders row's label, and the name its messages use. Shorter than the setting's name so it fits
    /// the 100-wide label column; the help text says they hold CAD.
    /// </summary>
    public const string OtherCadFoldersFieldName = "Other folders";

    public const string HeaderTitleText = "Settings";

    /// <summary>The one-line explanation shown under the default designer box.</summary>
    public const string DefaultDesignerHelpText = "Filled in as Designer in Part Properties when a file has none.";

    /// <summary>The explanation shown under the other names box.</summary>
    public const string OtherNamesHelpText =
        "Names in old files that mean you, separated by commas, for example Aidan.Brown, 4aida. Your Windows user name always counts.";

    /// <summary>The one-line explanation shown under the register folder box.</summary>
    public const string RegisterFolderHelpText = "The shared part number register. Leave blank to use only this PC's copy.";

    public const string RegisterFolderInvalidCharacterError = "The register folder contains a character Windows does not allow.";

    public const string RegisterFolderNotFullPathError = @"Enter a full folder path, such as B:\Data.";

    /// <summary>The one-line explanation shown under the work folder box.</summary>
    public const string WorkFolderHelpText = "Where New Part and Mass Update start, and the first folder Rename searches.";

    /// <summary>The one-line explanation shown under the other CAD folders box.</summary>
    public const string OtherCadFoldersHelpText = "Other folders that hold your CAD. Rename searches these too.";

    public const string WorkFolderInvalidCharacterError = "The work folder contains a character Windows does not allow.";

    public const string WorkFolderNotFullPathError = @"Enter the work folder as a full folder path, such as C:\CAD.";

    /// <summary>The material library row's label, and the name its messages use.</summary>
    public const string MaterialLibraryFieldName = "Material library";

    /// <summary>The finish list row's label, and the name its messages use.</summary>
    public const string FinishListFieldName = "Finish list";

    /// <summary>The one-line explanation shown under the material library box.</summary>
    public const string MaterialLibraryHelpText = "Blank uses the starter material library shipped with the add-in.";

    /// <summary>The one-line explanation shown under the finish list box.</summary>
    public const string FinishListHelpText = "Blank uses the starter finish list shipped with the add-in.";

    /// <summary>
    /// The line break the other CAD folders text is shown with: the one a WPF text box inserts. Any line break is
    /// accepted when reading it back.
    /// </summary>
    public const string FolderLineBreak = "\r\n";

    /// <summary>Characters Windows does not allow in a path, besides control characters.</summary>
    private const string InvalidPathCharacters = "<>\"|?*";

    private static readonly IReadOnlyList<UnitsOption> AllUnitsOptions = new[]
    {
        new UnitsOption(UnitsSystem.Metric, "Metric"),
        new UnitsOption(UnitsSystem.Imperial, "Imperial"),
    };

    private readonly SettingsStore _store;
    private readonly ILog _log;

    // The last values known to be on disk, and the values being edited. Both are private copies.
    // Text settings are held normalised in both, so record equality (IsDirty) compares normalised values.
    private AddinSettings _saved;
    private readonly AddinSettings _edited;

    // The text settings exactly as typed; _edited holds them normalised.
    private string _defaultDesignerText;
    private string _registerFolderText;
    private string _otherNamesText;
    private string _workFolderText;
    private string _otherCadFoldersText;
    private string _materialLibraryText;
    private string _finishListText;

    private readonly Func<string, bool> _fileExists;

    private string? _validationError;
    private string? _saveError;

    /// <param name="fileExists">
    /// Whether a file exists, used by <see cref="SaveCommand"/> to check a changed library path. Defaults to
    /// <see cref="File.Exists(string)"/>; tests pass their own.
    /// </param>
    public SettingsViewModel(AddinSettings settings, SettingsStore store, ILog? log = null, Func<string, bool>? fileExists = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(store);

        _store = store;
        _log = log ?? NullLog.Instance;
        _fileExists = fileExists ?? File.Exists;
        _materialLibraryText = settings.MaterialLibraryPath;
        _finishListText = settings.FinishListPath;
        _saved = Normalise(settings);
        _edited = Normalise(settings);
        _defaultDesignerText = settings.DefaultDesigner;
        _registerFolderText = settings.RegisterFolder;
        _otherNamesText = settings.OtherNamesForMe;
        _workFolderText = settings.WorkFolder;
        _otherCadFoldersText = string.Join(FolderLineBreak, settings.OtherCadFolders);
        _validationError = Validate();
        SaveCommand = new RelayCommand(Save, () => IsDirty && _validationError is null);
    }

    /// <summary>Raised after a successful save. The window closes on it.</summary>
    public event EventHandler? Saved;

    public bool ShowDeveloperTools
    {
        get => _edited.ShowDeveloperTools;
        set
        {
            if (_edited.ShowDeveloperTools == value)
                return;

            _edited.ShowDeveloperTools = value;
            OnPropertyChanged();
            OnEditChanged();
        }
    }

    /// <summary>
    /// The default designer as typed. Saved trimmed (<see cref="PropertyValues.NormaliseText"/>); only the
    /// trimmed value counts towards <see cref="IsDirty"/>.
    /// </summary>
    public string DefaultDesigner
    {
        get => _defaultDesignerText;
        set
        {
            value ??= string.Empty;
            if (_defaultDesignerText == value)
                return;

            _defaultDesignerText = value;
            _edited.DefaultDesigner = PropertyValues.NormaliseText(value);
            OnPropertyChanged();
            SetValidationError(Validate());
            OnEditChanged();
        }
    }

    /// <summary>
    /// The register folder as typed. Saved trimmed; only the trimmed value counts towards <see cref="IsDirty"/>.
    /// Blank means no shared register. A folder that does not exist is allowed, since the NAS may be offline.
    /// </summary>
    public string RegisterFolder
    {
        get => _registerFolderText;
        set
        {
            value ??= string.Empty;
            if (_registerFolderText == value)
                return;

            _registerFolderText = value;
            _edited.RegisterFolder = PropertyValues.NormaliseText(value);
            OnPropertyChanged();
            SetValidationError(Validate());
            OnEditChanged();
        }
    }

    /// <summary>
    /// The other names that mean the user, as typed (comma-separated; task 051 splits them). Saved trimmed; only the
    /// trimmed value counts towards <see cref="IsDirty"/>.
    /// </summary>
    public string OtherNamesForMe
    {
        get => _otherNamesText;
        set
        {
            value ??= string.Empty;
            if (_otherNamesText == value)
                return;

            _otherNamesText = value;
            _edited.OtherNamesForMe = PropertyValues.NormaliseText(value);
            OnPropertyChanged();
            SetValidationError(Validate());
            OnEditChanged();
        }
    }

    /// <summary>
    /// The work folder as typed. Saved trimmed; only the trimmed value counts towards <see cref="IsDirty"/>. Blank
    /// means no work folder. A folder that does not exist is allowed.
    /// </summary>
    public string WorkFolder
    {
        get => _workFolderText;
        set
        {
            value ??= string.Empty;
            if (_workFolderText == value)
                return;

            _workFolderText = value;
            _edited.WorkFolder = PropertyValues.NormaliseText(value);
            OnPropertyChanged();
            SetValidationError(Validate());
            OnEditChanged();
        }
    }

    /// <summary>
    /// The other CAD folders as typed, one per line. Saved as a list (<see cref="ParseFolderLines"/>): each line
    /// trimmed, blank lines dropped. Only that list counts towards <see cref="IsDirty"/>.
    /// </summary>
    public string OtherCadFolders
    {
        get => _otherCadFoldersText;
        set
        {
            value ??= string.Empty;
            if (_otherCadFoldersText == value)
                return;

            _otherCadFoldersText = value;
            _edited.OtherCadFolders = ParseFolderLines(value);
            OnPropertyChanged();
            SetValidationError(Validate());
            OnEditChanged();
        }
    }

    /// <summary>
    /// The material library path as typed. Saved trimmed; only the trimmed value counts towards <see cref="IsDirty"/>.
    /// Blank means the starter library shipped with the add-in. Checked as text while typing
    /// (<see cref="ValidateMaterialLibraryPath"/>); whether the file exists is checked on save.
    /// </summary>
    public string MaterialLibraryPath
    {
        get => _materialLibraryText;
        set
        {
            value ??= string.Empty;
            if (_materialLibraryText == value)
                return;

            _materialLibraryText = value;
            _edited.MaterialLibraryPath = PropertyValues.NormaliseText(value);
            OnPropertyChanged();
            SetValidationError(Validate());
            OnEditChanged();
        }
    }

    /// <summary>The finish list path as typed. As <see cref="MaterialLibraryPath"/>, for a <c>.csv</c> file.</summary>
    public string FinishListPath
    {
        get => _finishListText;
        set
        {
            value ??= string.Empty;
            if (_finishListText == value)
                return;

            _finishListText = value;
            _edited.FinishListPath = PropertyValues.NormaliseText(value);
            OnPropertyChanged();
            SetValidationError(Validate());
            OnEditChanged();
        }
    }

    /// <summary>The units new files are created in.</summary>
    public UnitsSystem DefaultUnits
    {
        get => _edited.DefaultUnits;
        set
        {
            if (_edited.DefaultUnits == value)
                return;

            _edited.DefaultUnits = value;
            OnPropertyChanged();
            OnEditChanged();
        }
    }

    /// <summary>The entries of the units dropdown, in display order.</summary>
    public IReadOnlyList<UnitsOption> UnitsOptions => AllUnitsOptions;

    /// <summary>Whether the edited values differ from the last saved ones.</summary>
    public bool IsDirty => _edited != _saved;

    /// <summary>
    /// Writes the edited settings through the store. Enabled only when <see cref="IsDirty"/> and every
    /// value is valid.
    /// </summary>
    public RelayCommand SaveCommand { get; }

    public string RestartNotice => RestartNoticeText;

    public string HeaderTitle => HeaderTitleText;

    public string HeaderSubline => Branding.ProductName;

    // One-line summaries shown beside a collapsed section's heading (SettingsSummaries), from the values being edited.

    public string GeneralSummary => SettingsSummaries.General(
        _edited.DefaultDesigner,
        _edited.OtherNamesForMe,
        AllUnitsOptions.FirstOrDefault(o => o.Units == _edited.DefaultUnits)?.DisplayText ?? _edited.DefaultUnits.ToString());

    public string PartNumbersSummary => SettingsSummaries.PartNumbers(_edited.RegisterFolder);

    public string FoldersSummary => SettingsSummaries.Folders(_edited.WorkFolder, _edited.OtherCadFolders);

    public string MaterialFinishSummary => SettingsSummaries.MaterialFinish(_edited.MaterialLibraryPath, _edited.FinishListPath);

    public string DeveloperSummary => SettingsSummaries.Developer(_edited.ShowDeveloperTools);

    public string DefaultDesignerHelp => DefaultDesignerHelpText;

    public string RegisterFolderHelp => RegisterFolderHelpText;

    public string OtherNamesHelp => OtherNamesHelpText;

    public string WorkFolderHelp => WorkFolderHelpText;

    public string OtherCadFoldersHelp => OtherCadFoldersHelpText;

    public string MaterialLibraryHelp => MaterialLibraryHelpText;

    public string FinishListHelp => FinishListHelpText;

    /// <summary>Where the settings are written, for display.</summary>
    public string SettingsFilePath => _store.FilePath;

    /// <summary>
    /// Why a value cannot be saved, or else why the last save failed, or null when neither applies.
    /// A save error lasts until the next edit or the next save.
    /// </summary>
    public string? ErrorText => _validationError ?? _saveError;

    public bool HasError => ErrorText is not null;

    /// <summary>
    /// A copy of the settings as written by the last successful save, or null when nothing was saved.
    /// The caller hands this to the rest of the add-in after the window closes.
    /// </summary>
    public AddinSettings? SavedSettings { get; private set; }

    /// <summary>
    /// Null when <paramref name="folder"/> can be saved as the register folder, otherwise the reason. Checked after
    /// trimming. Blank is valid (no shared register). Otherwise the folder must be a full Windows path, a drive
    /// (<c>B:\Data</c>) or a network share (<c>\\server\share</c>), with no character Windows does not allow.
    /// Whether the folder exists is not checked.
    /// </summary>
    public static string? ValidateRegisterFolder(string? folder) =>
        ValidateFolder(folder, RegisterFolderInvalidCharacterError, RegisterFolderNotFullPathError);

    /// <summary>
    /// Null when <paramref name="folder"/> can be saved as the work folder, otherwise the reason. The same rule as
    /// <see cref="ValidateRegisterFolder"/>: blank, or a full Windows path. Whether the folder exists is not checked.
    /// </summary>
    public static string? ValidateWorkFolder(string? folder) =>
        ValidateFolder(folder, WorkFolderInvalidCharacterError, WorkFolderNotFullPathError);

    /// <summary>
    /// Null when every non-blank line of <paramref name="text"/> is a full Windows path (the rule of
    /// <see cref="ValidateRegisterFolder"/>), otherwise the reason for the first line that is not, naming its line
    /// number counted from 1, blank lines included. Blank text is valid (no other folders).
    /// </summary>
    public static string? ValidateOtherCadFolders(string? text)
    {
        var lines = SplitLines(text);
        for (var i = 0; i < lines.Length; i++)
        {
            var error = ValidateFolder(
                lines[i],
                $"{OtherCadFoldersFieldName}, line {i + 1}, contains a character Windows does not allow.",
                $@"{OtherCadFoldersFieldName}, line {i + 1}: enter a full folder path, such as D:\CAD.");
            if (error is not null)
                return error;
        }

        return null;
    }

    /// <summary>
    /// Null when <paramref name="path"/> can be saved as the material library, otherwise the reason, naming the
    /// setting. Checked after trimming. Blank is valid (the starter library). Otherwise it must be a full Windows path
    /// (the rule of <see cref="ValidateRegisterFolder"/>) ending in <see cref="LibraryPaths.MaterialLibraryExtension"/>.
    /// Whether the file exists is checked on save, not here.
    /// </summary>
    public static string? ValidateMaterialLibraryPath(string? path) =>
        ValidateLibraryFile(path, MaterialLibraryFieldName, LibraryPaths.MaterialLibraryExtension, @"D:\Libraries\Materials.adsklib");

    /// <summary>As <see cref="ValidateMaterialLibraryPath"/>, for the finish list, which must end in <c>.csv</c>.</summary>
    public static string? ValidateFinishListPath(string? path) =>
        ValidateLibraryFile(path, FinishListFieldName, LibraryPaths.FinishListExtension, @"D:\Libraries\finishes.csv");

    /// <summary>The message <see cref="SaveCommand"/> shows when a changed library path names no file.</summary>
    public static string LibraryFileNotFoundError(string fieldName, string path) =>
        $"{fieldName}: '{path}' was not found. Choose an existing file, or leave it blank to use the starter file.";

    private static string? ValidateLibraryFile(string? path, string fieldName, string extension, string example)
    {
        var error = ValidateFolder(
            path,
            $"{fieldName} contains a character Windows does not allow.",
            $"{fieldName}: enter a full path to the file, such as {example}.");
        if (error is not null)
            return error;

        var normalised = PropertyValues.NormaliseText(path);
        if (normalised.Length > 0 && !normalised.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            return $"{fieldName} must be a file ending in {extension}.";

        return null;
    }

    /// <summary>The other CAD folders text as a list: one folder per line, each trimmed, blank lines dropped.</summary>
    public static IReadOnlyList<string> ParseFolderLines(string? text) =>
        SplitLines(text).Select(PropertyValues.NormaliseText).Where(line => line.Length > 0).ToArray();

    private static string[] SplitLines(string? text) =>
        (text ?? string.Empty).Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);

    private static string? ValidateFolder(string? folder, string invalidCharacterError, string notFullPathError)
    {
        var normalised = PropertyValues.NormaliseText(folder);
        if (normalised.Length == 0)
            return null;

        if (normalised.Any(c => char.IsControl(c) || InvalidPathCharacters.Contains(c)))
            return invalidCharacterError;

        return IsFullWindowsPath(normalised) ? null : notFullPathError;
    }

    // Written out rather than Path.IsPathFullyQualified, which follows the platform the code runs on, and the
    // tests run on Linux as well as Windows.
    private static bool IsFullWindowsPath(string path)
    {
        static bool IsSeparator(char c) => c is '\\' or '/';

        if (path.Length >= 3 && char.IsAsciiLetter(path[0]) && path[1] == ':' && IsSeparator(path[2]))
            return true;

        // A network share: two separators, then a server name.
        return path.Length >= 3 && IsSeparator(path[0]) && IsSeparator(path[1]) && !IsSeparator(path[2]);
    }

    private static AddinSettings Normalise(AddinSettings settings) =>
        settings with
        {
            DefaultDesigner = PropertyValues.NormaliseText(settings.DefaultDesigner),
            RegisterFolder = PropertyValues.NormaliseText(settings.RegisterFolder),
            OtherNamesForMe = PropertyValues.NormaliseText(settings.OtherNamesForMe),
            WorkFolder = PropertyValues.NormaliseText(settings.WorkFolder),
            OtherCadFolders = settings.OtherCadFolders.Select(PropertyValues.NormaliseText).Where(f => f.Length > 0).ToArray(),
            MaterialLibraryPath = PropertyValues.NormaliseText(settings.MaterialLibraryPath),
            FinishListPath = PropertyValues.NormaliseText(settings.FinishListPath),
        };

    // The first problem in window order: General (default designer, other names), then Part numbers (register
    // folder), then Folders (work folder, other CAD folders), then Material and finish (library, finish list).
    private string? Validate() =>
        PropertyValues.ValidateText(DefaultDesignerFieldName, _defaultDesignerText)
        ?? PropertyValues.ValidateText(OtherNamesFieldName, _otherNamesText)
        ?? ValidateRegisterFolder(_registerFolderText)
        ?? ValidateWorkFolder(_workFolderText)
        ?? ValidateOtherCadFolders(_otherCadFoldersText)
        ?? ValidateMaterialLibraryPath(_materialLibraryText)
        ?? ValidateFinishListPath(_finishListText);

    // Whether the library files exist is checked only on save, and only for a path that changed: a check per
    // keystroke could block on a disconnected drive (spike 032), and a library on a NAS that is offline today should
    // not stop the user saving another setting.
    private string? CheckLibraryFilesExist() =>
        CheckFileExists(MaterialLibraryFieldName, _edited.MaterialLibraryPath, _saved.MaterialLibraryPath)
        ?? CheckFileExists(FinishListFieldName, _edited.FinishListPath, _saved.FinishListPath);

    private string? CheckFileExists(string fieldName, string path, string savedPath)
    {
        if (path.Length == 0 || path == savedPath)
            return null;

        bool exists;
        try
        {
            exists = _fileExists(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            exists = false;
        }

        return exists ? null : LibraryFileNotFoundError(fieldName, path);
    }

    private void Save()
    {
        if (_validationError is not null)
            return;

        var missing = CheckLibraryFilesExist();
        if (missing is not null)
        {
            SetSaveError(missing);
            return;
        }

        var toWrite = _edited with { };
        try
        {
            _store.Save(toWrite);
        }
        catch (Exception ex)
        {
            // A UI boundary: the window shows the reason and stays open so the user can retry or cancel.
            _log.Error($"Could not save settings to '{_store.FilePath}'.", ex);
            SetSaveError($"Settings could not be saved to '{_store.FilePath}': {ex.Message}");
            return;
        }

        _log.Info($"Settings saved to '{_store.FilePath}'.");
        _saved = toWrite;
        SavedSettings = toWrite with { };
        SetSaveError(null);

        // Show what was saved: the trimmed text.
        if (_defaultDesignerText != toWrite.DefaultDesigner)
        {
            _defaultDesignerText = toWrite.DefaultDesigner;
            OnPropertyChanged(nameof(DefaultDesigner));
        }

        if (_registerFolderText != toWrite.RegisterFolder)
        {
            _registerFolderText = toWrite.RegisterFolder;
            OnPropertyChanged(nameof(RegisterFolder));
        }

        if (_otherNamesText != toWrite.OtherNamesForMe)
        {
            _otherNamesText = toWrite.OtherNamesForMe;
            OnPropertyChanged(nameof(OtherNamesForMe));
        }

        if (_workFolderText != toWrite.WorkFolder)
        {
            _workFolderText = toWrite.WorkFolder;
            OnPropertyChanged(nameof(WorkFolder));
        }

        var foldersText = string.Join(FolderLineBreak, toWrite.OtherCadFolders);
        if (_otherCadFoldersText != foldersText)
        {
            _otherCadFoldersText = foldersText;
            OnPropertyChanged(nameof(OtherCadFolders));
        }

        if (_materialLibraryText != toWrite.MaterialLibraryPath)
        {
            _materialLibraryText = toWrite.MaterialLibraryPath;
            OnPropertyChanged(nameof(MaterialLibraryPath));
        }

        if (_finishListText != toWrite.FinishListPath)
        {
            _finishListText = toWrite.FinishListPath;
            OnPropertyChanged(nameof(FinishListPath));
        }

        OnEditChanged();
        Saved?.Invoke(this, EventArgs.Empty);
    }

    private void SetValidationError(string? value) => SetError(ref _validationError, value);

    private void SetSaveError(string? value) => SetError(ref _saveError, value);

    private void SetError(ref string? field, string? value)
    {
        if (field == value)
            return;

        var before = ErrorText;
        field = value;
        if (ErrorText == before)
            return;

        OnPropertyChanged(nameof(ErrorText));
        if ((before is null) != (ErrorText is null))
            OnPropertyChanged(nameof(HasError));
    }

    private void OnEditChanged()
    {
        // A failed save's reason no longer describes what is on screen once the user edits again.
        SetSaveError(null);
        OnPropertyChanged(nameof(IsDirty));
        SaveCommand.RaiseCanExecuteChanged();

        // Cheap to rebuild, so every edit refreshes every summary rather than tracking which section changed.
        OnPropertyChanged(nameof(GeneralSummary));
        OnPropertyChanged(nameof(PartNumbersSummary));
        OnPropertyChanged(nameof(FoldersSummary));
        OnPropertyChanged(nameof(MaterialFinishSummary));
        OnPropertyChanged(nameof(DeveloperSummary));
    }
}
