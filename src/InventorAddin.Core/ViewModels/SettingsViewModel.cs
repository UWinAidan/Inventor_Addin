using InventorAddin.Core.Logging;
using InventorAddin.Core.PartProperties;
using InventorAddin.Core.Settings;

namespace InventorAddin.Core.ViewModels;

/// <summary>One entry in the "Units for new files" dropdown.</summary>
public sealed record UnitsOption(UnitsSystem Units, string DisplayText);

/// <summary>
/// View-model for the Settings window. Edits a copy of the settings it is given; nothing is written
/// until <see cref="SaveCommand"/> runs, so closing or cancelling the window discards the edits.
/// </summary>
public sealed class SettingsViewModel : ObservableObject
{
    /// <summary>
    /// Only the developer tools need a restart: the ribbon is built at startup. Other settings (the default
    /// designer, the register folder, the units for new files) are read from <c>AddinServices.Settings</c> each
    /// time a command runs.
    /// </summary>
    public const string RestartNoticeText = "Showing or hiding developer tools takes effect the next time Inventor starts.";

    public const string DefaultDesignerFieldName = "Default designer";

    public const string HeaderTitleText = "Settings";

    /// <summary>The one-line explanation shown under the default designer box.</summary>
    public const string DefaultDesignerHelpText = "Filled in as Designer in Part Properties when a file has none.";

    /// <summary>The one-line explanation shown under the register folder box.</summary>
    public const string RegisterFolderHelpText = "The shared part number register. Leave blank to use only this PC's copy.";

    public const string RegisterFolderInvalidCharacterError = "The register folder contains a character Windows does not allow.";

    public const string RegisterFolderNotFullPathError = @"Enter a full folder path, such as B:\Data.";

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

    private string? _validationError;
    private string? _saveError;

    public SettingsViewModel(AddinSettings settings, SettingsStore store, ILog? log = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(store);

        _store = store;
        _log = log ?? NullLog.Instance;
        _saved = Normalise(settings);
        _edited = Normalise(settings);
        _defaultDesignerText = settings.DefaultDesigner;
        _registerFolderText = settings.RegisterFolder;
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

    public string DefaultDesignerHelp => DefaultDesignerHelpText;

    public string RegisterFolderHelp => RegisterFolderHelpText;

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
    public static string? ValidateRegisterFolder(string? folder)
    {
        var normalised = PropertyValues.NormaliseText(folder);
        if (normalised.Length == 0)
            return null;

        if (normalised.Any(c => char.IsControl(c) || InvalidPathCharacters.Contains(c)))
            return RegisterFolderInvalidCharacterError;

        return IsFullWindowsPath(normalised) ? null : RegisterFolderNotFullPathError;
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
        };

    // The first problem in window order: the default designer is above the register folder.
    private string? Validate() =>
        PropertyValues.ValidateText(DefaultDesignerFieldName, _defaultDesignerText)
        ?? ValidateRegisterFolder(_registerFolderText);

    private void Save()
    {
        if (_validationError is not null)
            return;

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
    }
}
