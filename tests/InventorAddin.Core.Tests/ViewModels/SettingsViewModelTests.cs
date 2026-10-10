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
        _root = Path.Combine(Path.GetTempPath(), "InventorAddinTests", Guid.NewGuid().ToString("N"));
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
    public void RestartNotice_AppliesOnlyToDeveloperTools()
    {
        var vm = Create();

        Assert.Equal("Showing or hiding developer tools takes effect the next time Inventor starts.", vm.RestartNotice);
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

    // ---- Default designer ----

    private static string LongText(int length) => new('x', length);

    [Fact]
    public void DefaultDesigner_StartsWithGivenValue()
    {
        var vm = Create(new AddinSettings { DefaultDesigner = "A. Person" });

        Assert.Equal("A. Person", vm.DefaultDesigner);
        Assert.False(vm.IsDirty);
        Assert.False(vm.SaveCommand.CanExecute(null));
    }

    [Fact]
    public void DefaultDesigner_Changing_MakesDirty_AndEnablesSave()
    {
        var vm = Create();

        vm.DefaultDesigner = "A. Person";

        Assert.True(vm.IsDirty);
        Assert.True(vm.SaveCommand.CanExecute(null));
        Assert.Null(vm.ErrorText);
    }

    [Fact]
    public void DefaultDesigner_ChangingBack_ClearsDirty()
    {
        var vm = Create(new AddinSettings { DefaultDesigner = "A. Person" });

        vm.DefaultDesigner = "B. Person";
        vm.DefaultDesigner = "A. Person";

        Assert.False(vm.IsDirty);
        Assert.False(vm.SaveCommand.CanExecute(null));
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("   ")]
    [InlineData("\t ")]
    public void DefaultDesigner_OnlySpacesIntoEmpty_IsNotDirty(string typed)
    {
        var vm = Create();

        vm.DefaultDesigner = typed;

        Assert.Equal(typed, vm.DefaultDesigner);
        Assert.False(vm.IsDirty);
        Assert.False(vm.SaveCommand.CanExecute(null));
        Assert.Null(vm.ErrorText);
    }

    [Fact]
    public void DefaultDesigner_SurroundingSpacesOnSavedValue_IsNotDirty()
    {
        var vm = Create(new AddinSettings { DefaultDesigner = "A. Person" });

        vm.DefaultDesigner = "  A. Person ";

        Assert.False(vm.IsDirty);
    }

    [Fact]
    public void DefaultDesigner_UntrimmedValueFromFile_IsNotDirty_AndShownAsGiven()
    {
        var vm = Create(new AddinSettings { DefaultDesigner = " A. Person " });

        Assert.Equal(" A. Person ", vm.DefaultDesigner);
        Assert.False(vm.IsDirty);

        vm.DefaultDesigner = "A. Person";
        Assert.False(vm.IsDirty);
    }

    [Fact]
    public void DefaultDesigner_Null_IsTreatedAsEmpty()
    {
        var vm = Create(new AddinSettings { DefaultDesigner = "A. Person" });

        vm.DefaultDesigner = null!;

        Assert.Equal("", vm.DefaultDesigner);
        Assert.True(vm.IsDirty);
    }

    [Fact]
    public void DefaultDesigner_Save_WritesTrimmedValue()
    {
        var vm = Create();
        vm.DefaultDesigner = "  A.  Person  ";

        vm.SaveCommand.Execute(null);

        Assert.Equal("A.  Person", new SettingsStore(_folder).Load().Settings.DefaultDesigner);
        Assert.Equal("A.  Person", vm.SavedSettings!.DefaultDesigner);
        Assert.Equal("A.  Person", vm.DefaultDesigner);
        Assert.False(vm.IsDirty);
    }

    [Fact]
    public void DefaultDesigner_Save_RaisesDefaultDesignerChanged_WhenTrimmed()
    {
        var vm = Create();
        vm.DefaultDesigner = " A. Person ";
        var changed = RecordPropertyChanges(vm);

        vm.SaveCommand.Execute(null);

        Assert.Contains(nameof(SettingsViewModel.DefaultDesigner), changed);
    }

    [Fact]
    public void DefaultDesigner_Clearing_SavesEmpty()
    {
        var vm = Create(new AddinSettings { DefaultDesigner = "A. Person" });

        vm.DefaultDesigner = "  ";
        Assert.True(vm.IsDirty);
        vm.SaveCommand.Execute(null);

        Assert.Equal("", new SettingsStore(_folder).Load().Settings.DefaultDesigner);
    }

    [Fact]
    public void DefaultDesigner_Changing_RaisesPropertyChanged_ForValueAndIsDirty()
    {
        var vm = Create();
        var changed = RecordPropertyChanges(vm);
        int canExecuteChanged = 0;
        vm.SaveCommand.CanExecuteChanged += (_, _) => canExecuteChanged++;

        vm.DefaultDesigner = "A. Person";

        Assert.Equal(new[] { nameof(SettingsViewModel.DefaultDesigner), nameof(SettingsViewModel.IsDirty) }, changed);
        Assert.Equal(1, canExecuteChanged);
    }

    [Fact]
    public void DefaultDesigner_SettingSameText_RaisesNothing()
    {
        var vm = Create(new AddinSettings { DefaultDesigner = "A. Person" });
        var changed = RecordPropertyChanges(vm);

        vm.DefaultDesigner = "A. Person";

        Assert.Empty(changed);
    }

    [Fact]
    public void DefaultDesigner_AddingSpaces_RaisesValueChanged_ButStaysClean()
    {
        // The text box shows what was typed, so the raw text still changes.
        var vm = Create();
        var changed = RecordPropertyChanges(vm);

        vm.DefaultDesigner = " ";

        Assert.Contains(nameof(SettingsViewModel.DefaultDesigner), changed);
        Assert.False(vm.IsDirty);
    }

    [Fact]
    public void DefaultDesigner_EditsACopy()
    {
        var original = new AddinSettings { DefaultDesigner = "A. Person" };
        var vm = Create(original);

        vm.DefaultDesigner = "B. Person";

        Assert.Equal("A. Person", original.DefaultDesigner);
    }

    [Fact]
    public void DefaultDesigner_AtLengthLimit_IsValid()
    {
        var vm = Create();

        vm.DefaultDesigner = LongText(255);

        Assert.Null(vm.ErrorText);
        Assert.True(vm.SaveCommand.CanExecute(null));
    }

    [Fact]
    public void DefaultDesigner_OverLengthLimitOnlyBySpaces_IsValid()
    {
        var vm = Create();

        vm.DefaultDesigner = "  " + LongText(255) + "  ";

        Assert.Null(vm.ErrorText);
        Assert.True(vm.SaveCommand.CanExecute(null));
    }

    [Fact]
    public void DefaultDesigner_OverLengthLimit_SetsErrorText_AndDisablesSave()
    {
        var vm = Create();

        vm.DefaultDesigner = LongText(256);

        Assert.Equal("Default designer must be 255 characters or fewer.", vm.ErrorText);
        Assert.True(vm.HasError);
        Assert.True(vm.IsDirty);
        Assert.False(vm.SaveCommand.CanExecute(null));
    }

    [Fact]
    public void DefaultDesigner_OverLengthLimit_RaisesErrorTextAndHasError()
    {
        var vm = Create();
        var changed = RecordPropertyChanges(vm);

        vm.DefaultDesigner = LongText(256);

        Assert.Equal(
            new[]
            {
                nameof(SettingsViewModel.DefaultDesigner),
                nameof(SettingsViewModel.ErrorText),
                nameof(SettingsViewModel.HasError),
                nameof(SettingsViewModel.IsDirty),
            },
            changed);
    }

    [Fact]
    public void DefaultDesigner_FixingLength_ClearsErrorText_AndEnablesSave()
    {
        var vm = Create();
        vm.DefaultDesigner = LongText(256);

        vm.DefaultDesigner = LongText(255);

        Assert.Null(vm.ErrorText);
        Assert.False(vm.HasError);
        Assert.True(vm.SaveCommand.CanExecute(null));
    }

    [Fact]
    public void DefaultDesigner_Invalid_ExecuteWritesNothing()
    {
        var vm = Create();
        int saved = 0;
        vm.Saved += (_, _) => saved++;
        vm.DefaultDesigner = LongText(256);

        vm.SaveCommand.Execute(null);

        Assert.False(File.Exists(SettingsPath));
        Assert.Equal(0, saved);
        Assert.Null(vm.SavedSettings);
    }

    [Fact]
    public void DefaultDesigner_InvalidWhileOtherSettingChanged_SaveStaysDisabled()
    {
        var vm = Create();
        vm.ShowDeveloperTools = true;

        vm.DefaultDesigner = LongText(256);

        Assert.False(vm.SaveCommand.CanExecute(null));
    }

    [Fact]
    public void DefaultDesigner_TooLongValueFromFile_ShowsErrorAtOnce()
    {
        var vm = Create(new AddinSettings { DefaultDesigner = LongText(300) });

        Assert.NotNull(vm.ErrorText);
        Assert.False(vm.SaveCommand.CanExecute(null));
    }

    [Fact]
    public void DefaultDesigner_LineBreak_SetsErrorText()
    {
        var vm = Create();

        vm.DefaultDesigner = "A.\nPerson";

        Assert.NotNull(vm.ErrorText);
        Assert.False(vm.SaveCommand.CanExecute(null));
    }

    // ---- Save error cleared by the next edit ----

    private SettingsViewModel CreateWithFailedSave()
    {
        // A directory where settings.json should be makes the store's final replace step fail.
        Directory.CreateDirectory(SettingsPath);
        var vm = Create();
        vm.ShowDeveloperTools = true;
        vm.SaveCommand.Execute(null);
        Assert.NotNull(vm.ErrorText);
        Assert.Contains(SettingsPath, vm.ErrorText);
        Assert.True(vm.HasError);
        return vm;
    }

    [Fact]
    public void EditAfterFailedSave_ClearsErrorText()
    {
        var vm = CreateWithFailedSave();

        vm.DefaultDesigner = "A. Person";

        Assert.Null(vm.ErrorText);
        Assert.False(vm.HasError);
        Assert.True(vm.SaveCommand.CanExecute(null));
    }

    [Fact]
    public void EditAfterFailedSave_RaisesErrorTextAndHasErrorChanged()
    {
        var vm = CreateWithFailedSave();
        var changed = RecordPropertyChanges(vm);

        vm.DefaultDesigner = "A. Person";

        Assert.Equal(
            new[]
            {
                nameof(SettingsViewModel.DefaultDesigner),
                nameof(SettingsViewModel.ErrorText),
                nameof(SettingsViewModel.HasError),
                nameof(SettingsViewModel.IsDirty),
            },
            changed);
    }

    [Fact]
    public void InvalidEditAfterFailedSave_ShowsValidationError()
    {
        var vm = CreateWithFailedSave();
        var changed = RecordPropertyChanges(vm);

        vm.DefaultDesigner = LongText(256);

        Assert.Equal("Default designer must be 255 characters or fewer.", vm.ErrorText);
        Assert.True(vm.HasError);
        // The text changed but there was an error before and after.
        Assert.Contains(nameof(SettingsViewModel.ErrorText), changed);
        Assert.DoesNotContain(nameof(SettingsViewModel.HasError), changed);
    }

    [Fact]
    public void FixingInvalidEditAfterFailedSave_DoesNotBringBackSaveError()
    {
        var vm = CreateWithFailedSave();
        vm.DefaultDesigner = LongText(256);

        vm.DefaultDesigner = "";

        Assert.Null(vm.ErrorText);
        Assert.False(vm.HasError);
    }

    [Fact]
    public void EditBackToSavedValueAfterFailedSave_ClearsErrorText()
    {
        var vm = CreateWithFailedSave();
        var changed = RecordPropertyChanges(vm);

        vm.ShowDeveloperTools = false;

        Assert.Null(vm.ErrorText);
        Assert.False(vm.HasError);
        Assert.False(vm.IsDirty);
        Assert.Contains(nameof(SettingsViewModel.ErrorText), changed);
        Assert.Contains(nameof(SettingsViewModel.HasError), changed);
    }

    [Fact]
    public void SettingSameValueAfterFailedSave_KeepsErrorText()
    {
        var vm = CreateWithFailedSave();
        var error = vm.ErrorText;

        vm.ShowDeveloperTools = true;

        Assert.Equal(error, vm.ErrorText);
    }

    // ---- Register folder (task 037) ----

    [Fact]
    public void RegisterFolder_StartsWithGivenValue()
    {
        var vm = Create(new AddinSettings { RegisterFolder = @"C:\Shared" });

        Assert.Equal(@"C:\Shared", vm.RegisterFolder);
        Assert.False(vm.IsDirty);
        Assert.Null(vm.ErrorText);
    }

    [Fact]
    public void RegisterFolder_DefaultSettings_ShowsDefaultFolder_AndIsValid()
    {
        var vm = Create();

        Assert.Equal(AddinSettings.DefaultRegisterFolder, vm.RegisterFolder);
        Assert.Null(vm.ErrorText);
    }

    [Fact]
    public void RegisterFolder_Changing_MakesDirty_RaisesValueAndIsDirty()
    {
        var vm = Create();
        var changed = RecordPropertyChanges(vm);
        int canExecuteChanged = 0;
        vm.SaveCommand.CanExecuteChanged += (_, _) => canExecuteChanged++;

        vm.RegisterFolder = @"D:\Register";

        Assert.True(vm.IsDirty);
        Assert.True(vm.SaveCommand.CanExecute(null));
        Assert.Equal(new[] { nameof(SettingsViewModel.RegisterFolder), nameof(SettingsViewModel.IsDirty) }, changed);
        Assert.Equal(1, canExecuteChanged);
    }

    [Fact]
    public void RegisterFolder_ChangingBack_ClearsDirty()
    {
        var vm = Create();

        vm.RegisterFolder = @"D:\Register";
        vm.RegisterFolder = AddinSettings.DefaultRegisterFolder;

        Assert.False(vm.IsDirty);
        Assert.False(vm.SaveCommand.CanExecute(null));
    }

    [Fact]
    public void RegisterFolder_SurroundingSpaces_AreNotDirty()
    {
        var vm = Create();

        vm.RegisterFolder = "  " + AddinSettings.DefaultRegisterFolder + " ";

        Assert.False(vm.IsDirty);
        Assert.Null(vm.ErrorText);
    }

    [Fact]
    public void RegisterFolder_Save_WritesTrimmedValue_AndShowsIt()
    {
        var vm = Create();
        vm.RegisterFolder = @"  D:\Register  ";
        var changed = RecordPropertyChanges(vm);

        vm.SaveCommand.Execute(null);

        Assert.Equal(@"D:\Register", new SettingsStore(_folder).Load().Settings.RegisterFolder);
        Assert.Equal(@"D:\Register", vm.SavedSettings!.RegisterFolder);
        Assert.Equal(@"D:\Register", vm.RegisterFolder);
        Assert.Contains(nameof(SettingsViewModel.RegisterFolder), changed);
        Assert.False(vm.IsDirty);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void RegisterFolder_Blank_IsValid_AndSavesBlank(string typed)
    {
        var vm = Create();

        vm.RegisterFolder = typed;

        Assert.Null(vm.ErrorText);
        Assert.True(vm.IsDirty);
        vm.SaveCommand.Execute(null);
        Assert.Equal("", new SettingsStore(_folder).Load().Settings.RegisterFolder);
    }

    [Fact]
    public void RegisterFolder_Null_IsTreatedAsEmpty()
    {
        var vm = Create();

        vm.RegisterFolder = null!;

        Assert.Equal("", vm.RegisterFolder);
        Assert.True(vm.IsDirty);
    }

    [Theory]
    [InlineData(@"B:\Data")]
    [InlineData(@"b:\")]
    [InlineData(@"C:/Shared/Register")]
    [InlineData(@"\\server\share")]
    [InlineData(@"\\server\share\register folder")]
    [InlineData(@"Z:\does\not\exist\anywhere")]
    public void RegisterFolder_FullPath_IsValid(string folder)
    {
        Assert.Null(SettingsViewModel.ValidateRegisterFolder(folder));

        var vm = Create();
        vm.RegisterFolder = folder;
        Assert.Null(vm.ErrorText);
        Assert.True(vm.SaveCommand.CanExecute(null));
    }

    [Theory]
    [InlineData(@"B:\Da<ta")]
    [InlineData(@"B:\Da>ta")]
    [InlineData(@"B:\Da""ta")]
    [InlineData(@"B:\Da|ta")]
    [InlineData(@"B:\Da?ta")]
    [InlineData(@"B:\Da*ta")]
    [InlineData("B:\\Da\tta")]
    [InlineData("B:\\Da\nta")]
    [InlineData("B:\\Da\u0001ta")]
    [InlineData(@"\\?\B:\Data")]
    [InlineData(@"Da*ta")]
    public void RegisterFolder_InvalidCharacter_SetsError_AndDisablesSave(string folder)
    {
        const string expected = "The register folder contains a character Windows does not allow.";
        Assert.Equal(expected, SettingsViewModel.ValidateRegisterFolder(folder));

        var vm = Create();
        vm.RegisterFolder = folder;

        Assert.Equal(expected, vm.ErrorText);
        Assert.True(vm.HasError);
        Assert.False(vm.SaveCommand.CanExecute(null));
    }

    [Theory]
    [InlineData("Data")]
    [InlineData(@"Data\Register")]
    [InlineData(@".\Data")]
    [InlineData(@"..\Data")]
    [InlineData(@"\Data")]
    [InlineData("/Data")]
    [InlineData("B:")]
    [InlineData("B:Data")]
    [InlineData(@"1:\Data")]
    [InlineData(@"\\")]
    [InlineData(@"\\\server")]
    public void RegisterFolder_RelativePath_SetsError_AndDisablesSave(string folder)
    {
        var expected = @"Enter a full folder path, such as B:\Data.";
        Assert.Equal(expected, SettingsViewModel.ValidateRegisterFolder(folder));

        var vm = Create();
        vm.RegisterFolder = folder;

        Assert.Equal(expected, vm.ErrorText);
        Assert.False(vm.SaveCommand.CanExecute(null));
    }

    [Fact]
    public void RegisterFolder_Invalid_ExecuteWritesNothing()
    {
        var vm = Create();
        vm.RegisterFolder = "Data";

        vm.SaveCommand.Execute(null);

        Assert.False(File.Exists(SettingsPath));
        Assert.Null(vm.SavedSettings);
    }

    [Fact]
    public void RegisterFolder_Invalid_RaisesErrorTextAndHasError()
    {
        var vm = Create();
        var changed = RecordPropertyChanges(vm);

        vm.RegisterFolder = "Data";

        Assert.Equal(
            new[]
            {
                nameof(SettingsViewModel.RegisterFolder),
                nameof(SettingsViewModel.ErrorText),
                nameof(SettingsViewModel.HasError),
                nameof(SettingsViewModel.IsDirty),
            },
            changed);
    }

    [Fact]
    public void RegisterFolder_Fixing_ClearsError()
    {
        var vm = Create();
        vm.RegisterFolder = "Data";

        vm.RegisterFolder = @"B:\Data";

        Assert.Null(vm.ErrorText);
        Assert.False(vm.HasError);
        Assert.True(vm.SaveCommand.CanExecute(null));
    }

    [Fact]
    public void RegisterFolder_InvalidValueFromFile_ShowsErrorAtOnce()
    {
        var vm = Create(new AddinSettings { RegisterFolder = "Data" });

        Assert.Equal(SettingsViewModel.RegisterFolderNotFullPathError, vm.ErrorText);
        Assert.False(vm.SaveCommand.CanExecute(null));
    }

    [Fact]
    public void Validation_DefaultDesignerErrorComesFirst_ThenRegisterFolder()
    {
        var vm = Create();
        vm.RegisterFolder = "Data";
        vm.DefaultDesigner = LongText(256);

        Assert.Equal("Default designer must be 255 characters or fewer.", vm.ErrorText);

        vm.DefaultDesigner = "";

        Assert.Equal(SettingsViewModel.RegisterFolderNotFullPathError, vm.ErrorText);
    }

    [Fact]
    public void RegisterFolder_EditsACopy()
    {
        var original = new AddinSettings();
        var vm = Create(original);

        vm.RegisterFolder = @"D:\Register";

        Assert.Equal(AddinSettings.DefaultRegisterFolder, original.RegisterFolder);
    }

    [Fact]
    public void RegisterFolderHelp_IsTheExplanation()
    {
        const string expected = "The shared part number register. Leave blank to use only this PC's copy.";

        Assert.Equal(expected, SettingsViewModel.RegisterFolderHelpText);
        Assert.Equal(expected, Create().RegisterFolderHelp);
    }

    // ---- Units for new files (task 037) ----

    [Fact]
    public void UnitsOptions_AreMetricThenImperial()
    {
        var vm = Create();

        Assert.Equal(
            new[] { new UnitsOption(UnitsSystem.Metric, "Metric"), new UnitsOption(UnitsSystem.Imperial, "Imperial") },
            vm.UnitsOptions);
    }

    [Theory]
    [InlineData(UnitsSystem.Metric)]
    [InlineData(UnitsSystem.Imperial)]
    public void DefaultUnits_StartsWithGivenValue(UnitsSystem units)
    {
        var vm = Create(new AddinSettings { DefaultUnits = units });

        Assert.Equal(units, vm.DefaultUnits);
        Assert.False(vm.IsDirty);
    }

    [Fact]
    public void DefaultUnits_Changing_MakesDirty_RaisesValueAndIsDirty()
    {
        var vm = Create();
        var changed = RecordPropertyChanges(vm);
        int canExecuteChanged = 0;
        vm.SaveCommand.CanExecuteChanged += (_, _) => canExecuteChanged++;

        vm.DefaultUnits = UnitsSystem.Imperial;

        Assert.True(vm.IsDirty);
        Assert.True(vm.SaveCommand.CanExecute(null));
        Assert.Equal(new[] { nameof(SettingsViewModel.DefaultUnits), nameof(SettingsViewModel.IsDirty) }, changed);
        Assert.Equal(1, canExecuteChanged);
    }

    [Fact]
    public void DefaultUnits_SettingSameValue_RaisesNothing()
    {
        var vm = Create();
        var changed = RecordPropertyChanges(vm);

        vm.DefaultUnits = UnitsSystem.Metric;

        Assert.Empty(changed);
        Assert.False(vm.IsDirty);
    }

    [Fact]
    public void DefaultUnits_ChangingBack_ClearsDirty()
    {
        var vm = Create();

        vm.DefaultUnits = UnitsSystem.Imperial;
        vm.DefaultUnits = UnitsSystem.Metric;

        Assert.False(vm.IsDirty);
        Assert.False(vm.SaveCommand.CanExecute(null));
    }

    [Fact]
    public void DefaultUnits_EditsACopy()
    {
        var original = new AddinSettings();
        var vm = Create(original);

        vm.DefaultUnits = UnitsSystem.Imperial;

        Assert.Equal(UnitsSystem.Metric, original.DefaultUnits);
    }

    [Fact]
    public void Save_WritesRegisterFolderAndUnits_AndRoundTrips()
    {
        var vm = Create();
        vm.DefaultUnits = UnitsSystem.Imperial;
        vm.RegisterFolder = @"\\server\share";

        vm.SaveCommand.Execute(null);

        var expected = new AddinSettings { DefaultUnits = UnitsSystem.Imperial, RegisterFolder = @"\\server\share" };
        var loaded = new SettingsStore(_folder).Load();
        Assert.Empty(loaded.Warnings);
        Assert.Equal(expected, loaded.Settings);
        Assert.Equal(expected, vm.SavedSettings);
        Assert.False(vm.IsDirty);
    }

    [Fact]
    public void EditUnitsAfterFailedSave_ClearsErrorText()
    {
        var vm = CreateWithFailedSave();

        vm.DefaultUnits = UnitsSystem.Imperial;

        Assert.Null(vm.ErrorText);
        Assert.True(vm.SaveCommand.CanExecute(null));
    }

    // ---- Window text ----

    [Fact]
    public void Header_IsSettingsOverProductName()
    {
        var vm = Create();

        Assert.Equal("Settings", vm.HeaderTitle);
        Assert.Equal(Branding.ProductName, vm.HeaderSubline);
    }

    [Fact]
    public void DefaultDesignerHelp_IsTheExplanation()
    {
        const string expected = "Filled in as Designer in Part Properties when a file has none.";

        Assert.Equal(expected, SettingsViewModel.DefaultDesignerHelpText);
        Assert.Equal(expected, Create().DefaultDesignerHelp);
    }
}
