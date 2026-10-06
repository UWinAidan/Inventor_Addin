using System.ComponentModel;
using InventorAddin.Core.Logging;
using InventorAddin.Core.Settings;
using InventorAddin.Core.ViewModels;

namespace InventorAddin.Core.Tests.ViewModels;

public sealed class SettingsViewModelTests : IDisposable
{
    private readonly string _root;
    private readonly string _folder;

    public SettingsViewModelTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "WorkflowToolsTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _folder = Path.Combine(_root, "settings");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private string SettingsPath => Path.Combine(_folder, "settings.json");

    private SettingsViewModel Create(AddinSettings? settings = null, ILog? log = null) =>
        new(settings ?? new AddinSettings(), new SettingsStore(_folder), log);

    private static List<string> RecordPropertyChanges(INotifyPropertyChanged source)
    {
        var names = new List<string>();
        source.PropertyChanged += (_, e) => names.Add(e.PropertyName!);
        return names;
    }

    private sealed class RecordingLog : ILog
    {
        public List<string> Infos { get; } = new();
        public List<(string Message, Exception? Ex)> Errors { get; } = new();

        public void Info(string message) => Infos.Add(message);
        public void Warn(string message) { }
        public void Error(string message, Exception? ex = null) => Errors.Add((message, ex));
    }

    [Fact]
    public void Constructor_RejectsNulls()
    {
        Assert.Throws<ArgumentNullException>(() => new SettingsViewModel(null!, new SettingsStore(_folder)));
        Assert.Throws<ArgumentNullException>(() => new SettingsViewModel(new AddinSettings(), null!));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StartsWithGivenValues_NotDirty_SaveDisabled(bool showDeveloperTools)
    {
        var vm = Create(new AddinSettings { ShowDeveloperTools = showDeveloperTools });

        Assert.Equal(showDeveloperTools, vm.ShowDeveloperTools);
        Assert.False(vm.IsDirty);
        Assert.False(vm.SaveCommand.CanExecute(null));
        Assert.Null(vm.ErrorText);
        Assert.False(vm.HasError);
        Assert.Null(vm.SavedSettings);
    }

    [Fact]
    public void EditsACopy_CallerInstanceIsNeitherChangedNorObserved()
    {
        var original = new AddinSettings { ShowDeveloperTools = false };
        var vm = Create(original);

        vm.ShowDeveloperTools = true;
        Assert.False(original.ShowDeveloperTools);

        original.ShowDeveloperTools = true;
        Assert.True(vm.IsDirty);
    }

    [Fact]
    public void RestartNotice_SaysNextStart()
    {
        var vm = Create();

        Assert.Equal("Changes take effect the next time Inventor starts.", vm.RestartNotice);
    }

    [Fact]
    public void SettingsFilePath_IsTheStoreFile()
    {
        Assert.Equal(SettingsPath, Create().SettingsFilePath);
    }

    [Fact]
    public void Changing_MakesDirty_AndEnablesSave()
    {
        var vm = Create();

        vm.ShowDeveloperTools = true;

        Assert.True(vm.IsDirty);
        Assert.True(vm.SaveCommand.CanExecute(null));
    }

    [Fact]
    public void ChangingBack_ClearsDirty_AndDisablesSave()
    {
        var vm = Create();

        vm.ShowDeveloperTools = true;
        vm.ShowDeveloperTools = false;

        Assert.False(vm.IsDirty);
        Assert.False(vm.SaveCommand.CanExecute(null));
    }

    [Fact]
    public void Changing_RaisesPropertyChanged_ForValueAndIsDirty()
    {
        var vm = Create();
        var changed = RecordPropertyChanges(vm);

        vm.ShowDeveloperTools = true;

        Assert.Equal(new[] { nameof(SettingsViewModel.ShowDeveloperTools), nameof(SettingsViewModel.IsDirty) }, changed);
    }

    [Fact]
    public void SettingSameValue_RaisesNothing()
    {
        var vm = Create();
        var changed = RecordPropertyChanges(vm);
        int canExecuteChanged = 0;
        vm.SaveCommand.CanExecuteChanged += (_, _) => canExecuteChanged++;

        vm.ShowDeveloperTools = false;

        Assert.Empty(changed);
        Assert.Equal(0, canExecuteChanged);
    }

    [Fact]
    public void Changing_RaisesSaveCanExecuteChanged()
    {
        var vm = Create();
        int canExecuteChanged = 0;
        vm.SaveCommand.CanExecuteChanged += (_, _) => canExecuteChanged++;

        vm.ShowDeveloperTools = true;

        Assert.Equal(1, canExecuteChanged);
    }

    [Fact]
    public void Save_WritesThroughStore_AndRoundTrips()
    {
        var vm = Create(new AddinSettings { SchemaVersion = 3 });
        vm.ShowDeveloperTools = true;

        vm.SaveCommand.Execute(null);

        var loaded = new SettingsStore(_folder).Load();
        Assert.Empty(loaded.Warnings);
        Assert.Equal(new AddinSettings { ShowDeveloperTools = true, SchemaVersion = 3 }, loaded.Settings);
    }

    [Fact]
    public void Save_RaisesSaved_SetsSavedSettings_AndClearsDirty()
    {
        var vm = Create();
        int saved = 0;
        vm.Saved += (_, _) => saved++;
        vm.ShowDeveloperTools = true;

        vm.SaveCommand.Execute(null);

        Assert.Equal(1, saved);
        Assert.Equal(new AddinSettings { ShowDeveloperTools = true }, vm.SavedSettings);
        Assert.False(vm.IsDirty);
        Assert.False(vm.SaveCommand.CanExecute(null));
        Assert.Null(vm.ErrorText);
    }

    [Fact]
    public void Save_SavedSettingsIsACopy()
    {
        var vm = Create();
        vm.ShowDeveloperTools = true;
        vm.SaveCommand.Execute(null);

        vm.SavedSettings!.ShowDeveloperTools = false;

        Assert.True(vm.ShowDeveloperTools);
        Assert.False(vm.IsDirty);
    }

    [Fact]
    public void Save_RaisesIsDirtyChanged_AndCanExecuteChanged()
    {
        var vm = Create();
        vm.ShowDeveloperTools = true;
        var changed = RecordPropertyChanges(vm);
        int canExecuteChanged = 0;
        vm.SaveCommand.CanExecuteChanged += (_, _) => canExecuteChanged++;

        vm.SaveCommand.Execute(null);

        Assert.Contains(nameof(SettingsViewModel.IsDirty), changed);
        Assert.Equal(1, canExecuteChanged);
    }

    [Fact]
    public void Save_LogsInfo()
    {
        var log = new RecordingLog();
        var vm = Create(log: log);
        vm.ShowDeveloperTools = true;

        vm.SaveCommand.Execute(null);

        Assert.Contains(log.Infos, m => m.Contains(SettingsPath));
        Assert.Empty(log.Errors);
    }

    [Fact]
    public void Execute_WhenNotDirty_WritesNothing()
    {
        var vm = Create();
        int saved = 0;
        vm.Saved += (_, _) => saved++;

        vm.SaveCommand.Execute(null);

        Assert.False(File.Exists(SettingsPath));
        Assert.Equal(0, saved);
        Assert.Null(vm.SavedSettings);
    }

    [Fact]
    public void EditingWithoutSaving_WritesNothing()
    {
        // Cancel or closing the window just drops the view-model.
        var vm = Create();
        vm.ShowDeveloperTools = true;

        Assert.False(File.Exists(SettingsPath));
        Assert.False(Directory.Exists(_folder));
    }

    [Fact]
    public void FailedSave_SetsErrorText_DoesNotThrow_AndStaysDirty()
    {
        // A directory where settings.json should be makes the store's final replace step fail.
        Directory.CreateDirectory(SettingsPath);
        var log = new RecordingLog();
        var vm = Create(log: log);
        int saved = 0;
        vm.Saved += (_, _) => saved++;
        vm.ShowDeveloperTools = true;

        var ex = Record.Exception(() => vm.SaveCommand.Execute(null));

        Assert.Null(ex);
        Assert.NotNull(vm.ErrorText);
        Assert.Contains(SettingsPath, vm.ErrorText);
        Assert.True(vm.HasError);
        Assert.Equal(0, saved);
        Assert.Null(vm.SavedSettings);
        Assert.True(vm.IsDirty);
        Assert.True(vm.SaveCommand.CanExecute(null));

        var error = Assert.Single(log.Errors);
        Assert.NotNull(error.Ex);
    }

    [Fact]
    public void FailedSave_RaisesErrorTextAndHasErrorChanged()
    {
        Directory.CreateDirectory(SettingsPath);
        var vm = Create();
        vm.ShowDeveloperTools = true;
        var changed = RecordPropertyChanges(vm);

        vm.SaveCommand.Execute(null);

        Assert.Equal(new[] { nameof(SettingsViewModel.ErrorText), nameof(SettingsViewModel.HasError) }, changed);
    }

    [Fact]
    public void SaveAfterFailure_ClearsErrorText()
    {
        Directory.CreateDirectory(SettingsPath);
        var vm = Create();
        vm.ShowDeveloperTools = true;
        vm.SaveCommand.Execute(null);
        Assert.True(vm.HasError);

        Directory.Delete(SettingsPath);
        vm.SaveCommand.Execute(null);

        Assert.Null(vm.ErrorText);
        Assert.False(vm.HasError);
        Assert.True(new SettingsStore(_folder).Load().Settings.ShowDeveloperTools);
    }
}
