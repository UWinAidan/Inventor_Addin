using InventorAddin.Core.Logging;
using InventorAddin.Core.Settings;

namespace InventorAddin.Core.ViewModels;

/// <summary>
/// View-model for the Settings window. Edits a copy of the settings it is given; nothing is written
/// until <see cref="SaveCommand"/> runs, so closing or cancelling the window discards the edits.
/// </summary>
public sealed class SettingsViewModel : ObservableObject
{
    public const string RestartNoticeText = "Changes take effect the next time Inventor starts.";

    private readonly SettingsStore _store;
    private readonly ILog _log;

    // The last values known to be on disk, and the values being edited. Both are private copies.
    private AddinSettings _saved;
    private readonly AddinSettings _edited;

    private string? _errorText;

    public SettingsViewModel(AddinSettings settings, SettingsStore store, ILog? log = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(store);

        _store = store;
        _log = log ?? NullLog.Instance;
        _saved = settings with { };
        _edited = settings with { };
        SaveCommand = new RelayCommand(Save, () => IsDirty);
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

    /// <summary>Whether the edited values differ from the last saved ones.</summary>
    public bool IsDirty => _edited != _saved;

    /// <summary>Writes the edited settings through the store. Enabled only when <see cref="IsDirty"/>.</summary>
    public RelayCommand SaveCommand { get; }

    public string RestartNotice => RestartNoticeText;

    /// <summary>Where the settings are written, for display.</summary>
    public string SettingsFilePath => _store.FilePath;

    /// <summary>Why the last save failed, or null when it did not fail.</summary>
    public string? ErrorText
    {
        get => _errorText;
        private set
        {
            if (SetProperty(ref _errorText, value))
                OnPropertyChanged(nameof(HasError));
        }
    }

    public bool HasError => _errorText is not null;

    /// <summary>
    /// A copy of the settings as written by the last successful save, or null when nothing was saved.
    /// The caller hands this to the rest of the add-in after the window closes.
    /// </summary>
    public AddinSettings? SavedSettings { get; private set; }

    private void Save()
    {
        var toWrite = _edited with { };
        try
        {
            _store.Save(toWrite);
        }
        catch (Exception ex)
        {
            // A UI boundary: the window shows the reason and stays open so the user can retry or cancel.
            _log.Error($"Could not save settings to '{_store.FilePath}'.", ex);
            ErrorText = $"Settings could not be saved to '{_store.FilePath}': {ex.Message}";
            return;
        }

        _log.Info($"Settings saved to '{_store.FilePath}'.");
        _saved = toWrite;
        SavedSettings = toWrite with { };
        ErrorText = null;
        OnEditChanged();
        Saved?.Invoke(this, EventArgs.Empty);
    }

    private void OnEditChanged()
    {
        OnPropertyChanged(nameof(IsDirty));
        SaveCommand.RaiseCanExecuteChanged();
    }
}
