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

    // ---- Other names, work folder, other CAD folders (task 049) ----

    [Fact]
    public void NewSettings_StartWithGivenValues_NotDirty()
    {
        var vm = Create(new AddinSettings
        {
            OtherNamesForMe = "A.Person, ap",
            WorkFolder = @"D:\Work",
            OtherCadFolders = new[] { @"E:\One", @"\\server\two" },
        });

        Assert.Equal("A.Person, ap", vm.OtherNamesForMe);
        Assert.Equal(@"D:\Work", vm.WorkFolder);
        Assert.Equal("E:\\One\r\n\\\\server\\two", vm.OtherCadFolders);
        Assert.False(vm.IsDirty);
        Assert.Null(vm.ErrorText);
    }

    [Fact]
    public void NewSettings_Defaults_AreShown()
    {
        var vm = Create();

        Assert.Equal("", vm.OtherNamesForMe);
        Assert.Equal(AddinSettings.DefaultWorkFolder, vm.WorkFolder);
        Assert.Equal("", vm.OtherCadFolders);
    }

    [Fact]
    public void OtherNames_Changing_MakesDirty_RaisesValueAndIsDirty()
    {
        var vm = Create();
        var changed = RecordPropertyChanges(vm);

        vm.OtherNamesForMe = "A.Person";

        Assert.True(vm.IsDirty);
        Assert.True(vm.SaveCommand.CanExecute(null));
        Assert.Equal(new[] { nameof(SettingsViewModel.OtherNamesForMe), nameof(SettingsViewModel.IsDirty) }, changed);
    }

    [Fact]
    public void OtherNames_OnlySpaces_IsNotAChange()
    {
        var vm = Create();

        vm.OtherNamesForMe = "   ";

        Assert.False(vm.IsDirty);
    }

    [Fact]
    public void OtherNames_Null_IsTreatedAsEmpty()
    {
        var vm = Create(new AddinSettings { OtherNamesForMe = "x" });

        vm.OtherNamesForMe = null!;

        Assert.Equal("", vm.OtherNamesForMe);
        Assert.True(vm.IsDirty);
    }

    [Fact]
    public void OtherNames_TooLong_SetsError_AndDisablesSave()
    {
        var vm = Create();

        vm.OtherNamesForMe = LongText(256);

        Assert.Equal("Other names must be 255 characters or fewer.", vm.ErrorText);
        Assert.False(vm.SaveCommand.CanExecute(null));
    }

    [Fact]
    public void OtherNames_LineBreak_SetsError()
    {
        var vm = Create();

        vm.OtherNamesForMe = "A\nB";

        Assert.Equal("Other names must be on one line. Remove the line break.", vm.ErrorText);
    }

    [Fact]
    public void WorkFolder_Changing_MakesDirty_RaisesValueAndIsDirty()
    {
        var vm = Create();
        var changed = RecordPropertyChanges(vm);

        vm.WorkFolder = @"D:\Work";

        Assert.True(vm.IsDirty);
        Assert.True(vm.SaveCommand.CanExecute(null));
        Assert.Equal(new[] { nameof(SettingsViewModel.WorkFolder), nameof(SettingsViewModel.IsDirty) }, changed);
    }

    [Fact]
    public void WorkFolder_Blank_IsValid_AndSavedBlank()
    {
        var vm = Create();

        vm.WorkFolder = "  ";
        Assert.Null(vm.ErrorText);
        Assert.True(vm.IsDirty);

        vm.SaveCommand.Execute(null);

        Assert.Equal("", new SettingsStore(_folder).Load().Settings.WorkFolder);
        Assert.Equal("", vm.WorkFolder);
    }

    [Fact]
    public void WorkFolder_TrailingSpaces_AreNotAChange()
    {
        var vm = Create();

        vm.WorkFolder = AddinSettings.DefaultWorkFolder + "  ";

        Assert.False(vm.IsDirty);
    }

    [Theory]
    [InlineData(@"D:\Work")]
    [InlineData(@"\\server\share\cad")]
    [InlineData(@"Z:\does\not\exist")]
    public void WorkFolder_FullPath_IsValid(string folder)
    {
        Assert.Null(SettingsViewModel.ValidateWorkFolder(folder));

        var vm = Create();
        vm.WorkFolder = folder;

        Assert.Null(vm.ErrorText);
    }

    [Theory]
    [InlineData("Work")]
    [InlineData(@"\Work")]
    [InlineData("D:Work")]
    public void WorkFolder_RelativePath_SetsItsOwnError_AndDisablesSave(string folder)
    {
        const string expected = @"Enter the work folder as a full folder path, such as C:\CAD.";
        Assert.Equal(expected, SettingsViewModel.WorkFolderNotFullPathError);
        Assert.Equal(expected, SettingsViewModel.ValidateWorkFolder(folder));

        var vm = Create();
        vm.WorkFolder = folder;

        Assert.Equal(expected, vm.ErrorText);
        Assert.False(vm.SaveCommand.CanExecute(null));
    }

    [Theory]
    [InlineData(@"D:\Wo*rk")]
    [InlineData("D:\\Wo\trk")]
    public void WorkFolder_InvalidCharacter_SetsItsOwnError(string folder)
    {
        const string expected = "The work folder contains a character Windows does not allow.";

        Assert.Equal(expected, SettingsViewModel.ValidateWorkFolder(folder));
        var vm = Create();
        vm.WorkFolder = folder;
        Assert.Equal(expected, vm.ErrorText);
    }

    [Fact]
    public void WorkFolder_EditsACopy()
    {
        var original = new AddinSettings();
        var vm = Create(original);

        vm.WorkFolder = @"D:\Work";

        Assert.Equal(AddinSettings.DefaultWorkFolder, original.WorkFolder);
    }

    [Fact]
    public void OtherCadFolders_Changing_MakesDirty_RaisesValueAndIsDirty()
    {
        var vm = Create();
        var changed = RecordPropertyChanges(vm);

        vm.OtherCadFolders = @"D:\One";

        Assert.True(vm.IsDirty);
        Assert.True(vm.SaveCommand.CanExecute(null));
        Assert.Equal(new[] { nameof(SettingsViewModel.OtherCadFolders), nameof(SettingsViewModel.IsDirty) }, changed);
    }

    [Fact]
    public void OtherCadFolders_BlankLinesOnly_IsNotAChange()
    {
        var vm = Create();

        vm.OtherCadFolders = "\r\n  \r\n";

        Assert.False(vm.IsDirty);
        Assert.Null(vm.ErrorText);
    }

    [Fact]
    public void OtherCadFolders_SameFoldersWithSpacesAndBlankLines_IsNotAChange()
    {
        var vm = Create(new AddinSettings { OtherCadFolders = new[] { @"D:\One", @"E:\Two" } });

        vm.OtherCadFolders = "  D:\\One  \n\n\tE:\\Two\r\n";

        Assert.False(vm.IsDirty);
    }

    [Fact]
    public void OtherCadFolders_OrderChange_IsAChange()
    {
        var vm = Create(new AddinSettings { OtherCadFolders = new[] { @"D:\One", @"E:\Two" } });

        vm.OtherCadFolders = "E:\\Two\r\nD:\\One";

        Assert.True(vm.IsDirty);
    }

    [Theory]
    [InlineData("", new string[0])]
    [InlineData("D:\\One", new[] { @"D:\One" })]
    [InlineData("D:\\One\r\nE:\\Two", new[] { @"D:\One", @"E:\Two" })]
    [InlineData("D:\\One\nE:\\Two\rF:\\Three", new[] { @"D:\One", @"E:\Two", @"F:\Three" })]
    [InlineData("\r\n  D:\\One  \r\n\r\n   \r\n E:\\My Two \r\n", new[] { @"D:\One", @"E:\My Two" })]
    public void ParseFolderLines_TrimsEachLine_DropsBlankLines(string text, string[] expected)
    {
        Assert.Equal(expected, SettingsViewModel.ParseFolderLines(text));
    }

    [Fact]
    public void ParseFolderLines_Null_IsEmpty()
    {
        Assert.Empty(SettingsViewModel.ParseFolderLines(null));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("\r\n\r\n")]
    [InlineData("D:\\One\r\n\r\n\\\\server\\share")]
    public void ValidateOtherCadFolders_BlankOrFullPaths_IsValid(string? text)
    {
        Assert.Null(SettingsViewModel.ValidateOtherCadFolders(text));
    }

    [Theory]
    [InlineData("Relative", 1)]
    [InlineData("D:\\One\r\nRelative", 2)]
    [InlineData("D:\\One\r\n\r\n  Relative  \r\nAlso relative", 3)]
    [InlineData("D:\\One\nE:\\Two\n\\Three", 3)]
    public void ValidateOtherCadFolders_RelativeLine_NamesTheFirstBadLine(string text, int line)
    {
        Assert.Equal(
            $@"Other folders, line {line}: enter a full folder path, such as D:\CAD.",
            SettingsViewModel.ValidateOtherCadFolders(text));
    }

    [Fact]
    public void ValidateOtherCadFolders_InvalidCharacter_NamesTheLine()
    {
        Assert.Equal(
            "Other folders, line 2, contains a character Windows does not allow.",
            SettingsViewModel.ValidateOtherCadFolders("D:\\One\r\nD:\\T?wo"));
    }

    [Fact]
    public void OtherCadFolders_Invalid_SetsError_AndDisablesSave_ThenFixingClearsIt()
    {
        var vm = Create();

        vm.OtherCadFolders = "D:\\One\r\nTwo";

        Assert.Equal(@"Other folders, line 2: enter a full folder path, such as D:\CAD.", vm.ErrorText);
        Assert.False(vm.SaveCommand.CanExecute(null));

        vm.OtherCadFolders = "D:\\One\r\nE:\\Two";

        Assert.Null(vm.ErrorText);
        Assert.True(vm.SaveCommand.CanExecute(null));
    }

    [Fact]
    public void OtherCadFolders_InvalidValueFromFile_ShowsErrorAtOnce()
    {
        var vm = Create(new AddinSettings { OtherCadFolders = new[] { "Relative" } });

        Assert.Equal(@"Other folders, line 1: enter a full folder path, such as D:\CAD.", vm.ErrorText);
        Assert.False(vm.SaveCommand.CanExecute(null));
    }

    [Fact]
    public void Validation_FollowsWindowOrder()
    {
        var vm = Create();
        vm.OtherCadFolders = "Relative";
        vm.WorkFolder = "Relative";
        vm.RegisterFolder = "Relative";
        vm.OtherNamesForMe = LongText(256);
        vm.DefaultDesigner = LongText(256);

        Assert.Equal("Default designer must be 255 characters or fewer.", vm.ErrorText);
        vm.DefaultDesigner = "";
        Assert.Equal("Other names must be 255 characters or fewer.", vm.ErrorText);
        vm.OtherNamesForMe = "";
        Assert.Equal(SettingsViewModel.RegisterFolderNotFullPathError, vm.ErrorText);
        vm.RegisterFolder = "";
        Assert.Equal(SettingsViewModel.WorkFolderNotFullPathError, vm.ErrorText);
        vm.WorkFolder = "";
        Assert.Equal(@"Other folders, line 1: enter a full folder path, such as D:\CAD.", vm.ErrorText);
        vm.OtherCadFolders = "";
        Assert.Null(vm.ErrorText);
    }

    [Fact]
    public void Save_WritesNewSettingsNormalised_ShowsWhatWasSaved_AndRoundTrips()
    {
        var vm = Create();
        vm.OtherNamesForMe = "  A.Person, ap  ";
        vm.WorkFolder = @"  D:\Work  ";
        vm.OtherCadFolders = "\r\n  E:\\One  \r\n\r\n\\\\server\\two\r\n";
        var changed = RecordPropertyChanges(vm);

        vm.SaveCommand.Execute(null);

        var expected = new AddinSettings
        {
            OtherNamesForMe = "A.Person, ap",
            WorkFolder = @"D:\Work",
            OtherCadFolders = new[] { @"E:\One", @"\\server\two" },
        };
        var loaded = new SettingsStore(_folder).Load();
        Assert.Empty(loaded.Warnings);
        Assert.Equal(expected, loaded.Settings);
        Assert.Equal(expected, vm.SavedSettings);
        Assert.False(vm.IsDirty);
        Assert.Equal("A.Person, ap", vm.OtherNamesForMe);
        Assert.Equal(@"D:\Work", vm.WorkFolder);
        Assert.Equal("E:\\One\r\n\\\\server\\two", vm.OtherCadFolders);
        Assert.Contains(nameof(SettingsViewModel.OtherNamesForMe), changed);
        Assert.Contains(nameof(SettingsViewModel.WorkFolder), changed);
        Assert.Contains(nameof(SettingsViewModel.OtherCadFolders), changed);
    }

    [Fact]
    public void Save_NormalisedTextAlreadyShown_RaisesNoValueChange()
    {
        var vm = Create();
        vm.WorkFolder = @"D:\Work";
        vm.OtherCadFolders = "E:\\One\r\nF:\\Two";
        var changed = RecordPropertyChanges(vm);

        vm.SaveCommand.Execute(null);

        Assert.DoesNotContain(nameof(SettingsViewModel.WorkFolder), changed);
        Assert.DoesNotContain(nameof(SettingsViewModel.OtherCadFolders), changed);
    }

    [Fact]
    public void NewSettings_HelpTexts()
    {
        var vm = Create();

        Assert.Equal(
            "Names in old files that mean you, separated by commas, for example Aidan.Brown, 4aida. Your Windows user name always counts.",
            vm.OtherNamesHelp);
        Assert.Equal("Where New Part and Mass Update start, and the first folder Rename searches.", vm.WorkFolderHelp);
        Assert.Equal("Other folders that hold your CAD. Rename searches these too.", vm.OtherCadFoldersHelp);
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

    // ---- Material library and finish list (task 070) ----

    private const string LibraryFile = @"D:\Libraries\Mine.adsklib";
    private const string FinishFile = @"D:\Libraries\finishes.csv";

    // Only the given files exist; the paths that were asked about are recorded.
    private SettingsViewModel CreateWithFiles(AddinSettings? settings, List<string> asked, params string[] existing) =>
        new(settings ?? new AddinSettings(), new SettingsStore(_folder), null, path =>
        {
            asked.Add(path);
            return existing.Contains(path);
        });

    [Fact]
    public void LibraryPaths_StartWithGivenValues_NotDirty()
    {
        var vm = Create(new AddinSettings { MaterialLibraryPath = LibraryFile, FinishListPath = FinishFile });

        Assert.Equal(LibraryFile, vm.MaterialLibraryPath);
        Assert.Equal(FinishFile, vm.FinishListPath);
        Assert.False(vm.IsDirty);
        Assert.Null(vm.ErrorText);
    }

    [Fact]
    public void LibraryPaths_Defaults_AreBlank()
    {
        var vm = Create();

        Assert.Equal("", vm.MaterialLibraryPath);
        Assert.Equal("", vm.FinishListPath);
    }

    [Fact]
    public void MaterialLibraryPath_Changing_MakesDirty_RaisesValueAndIsDirty()
    {
        var vm = CreateWithFiles(null, new List<string>(), LibraryFile);
        var changed = RecordPropertyChanges(vm);

        vm.MaterialLibraryPath = LibraryFile;

        Assert.True(vm.IsDirty);
        Assert.True(vm.SaveCommand.CanExecute(null));
        Assert.Equal(new[] { nameof(SettingsViewModel.MaterialLibraryPath), nameof(SettingsViewModel.IsDirty) }, changed);
    }

    [Fact]
    public void FinishListPath_Changing_MakesDirty_RaisesValueAndIsDirty()
    {
        var vm = CreateWithFiles(null, new List<string>(), FinishFile);
        var changed = RecordPropertyChanges(vm);

        vm.FinishListPath = FinishFile;

        Assert.True(vm.IsDirty);
        Assert.Equal(new[] { nameof(SettingsViewModel.FinishListPath), nameof(SettingsViewModel.IsDirty) }, changed);
    }

    [Fact]
    public void LibraryPaths_OnlySpaces_IsNotAChange()
    {
        var vm = Create();

        vm.MaterialLibraryPath = "   ";
        vm.FinishListPath = "\t";

        Assert.False(vm.IsDirty);
        Assert.Null(vm.ErrorText);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(@"D:\Libraries\Mine.adsklib")]
    [InlineData(@"  D:\Libraries\Mine.ADSKLIB  ")]
    [InlineData(@"\\server\share\Mine.adsklib")]
    public void ValidateMaterialLibraryPath_BlankOrFullAdsklibPath_IsValid(string? path)
    {
        Assert.Null(SettingsViewModel.ValidateMaterialLibraryPath(path));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(@"D:\Libraries\finishes.csv")]
    [InlineData(@"\\server\share\Finishes.CSV")]
    public void ValidateFinishListPath_BlankOrFullCsvPath_IsValid(string? path)
    {
        Assert.Null(SettingsViewModel.ValidateFinishListPath(path));
    }

    [Theory]
    [InlineData("Mine.adsklib")]
    [InlineData(@"Libraries\Mine.adsklib")]
    [InlineData(@"\Libraries\Mine.adsklib")]
    public void ValidateMaterialLibraryPath_NotFullPath_NamesTheSetting(string path)
    {
        Assert.Equal(
            @"Material library: enter a full path to the file, such as D:\Libraries\Materials.adsklib.",
            SettingsViewModel.ValidateMaterialLibraryPath(path));
    }

    [Fact]
    public void ValidateFinishListPath_NotFullPath_NamesTheSetting()
    {
        Assert.Equal(
            @"Finish list: enter a full path to the file, such as D:\Libraries\finishes.csv.",
            SettingsViewModel.ValidateFinishListPath("finishes.csv"));
    }

    [Theory]
    [InlineData(@"D:\Libraries\Mine.csv")]
    [InlineData(@"D:\Libraries\Mine.adsklib.bak")]
    [InlineData(@"D:\Libraries\Mine")]
    public void ValidateMaterialLibraryPath_WrongExtension_NamesTheSetting(string path)
    {
        Assert.Equal("Material library must be a file ending in .adsklib.", SettingsViewModel.ValidateMaterialLibraryPath(path));
    }

    [Fact]
    public void ValidateFinishListPath_WrongExtension_NamesTheSetting()
    {
        Assert.Equal("Finish list must be a file ending in .csv.", SettingsViewModel.ValidateFinishListPath(@"D:\finishes.xlsx"));
    }

    [Fact]
    public void ValidateLibraryPaths_InvalidCharacter_NamesTheSetting()
    {
        Assert.Equal(
            "Material library contains a character Windows does not allow.",
            SettingsViewModel.ValidateMaterialLibraryPath(@"D:\Li?b.adsklib"));
        Assert.Equal(
            "Finish list contains a character Windows does not allow.",
            SettingsViewModel.ValidateFinishListPath("D:\\fin|ishes.csv"));
    }

    [Fact]
    public void LibraryPath_Invalid_SetsError_AndDisablesSave_ThenFixingClearsIt()
    {
        var vm = Create();

        vm.FinishListPath = @"D:\finishes.txt";

        Assert.Equal("Finish list must be a file ending in .csv.", vm.ErrorText);
        Assert.False(vm.SaveCommand.CanExecute(null));

        vm.FinishListPath = "";

        Assert.Null(vm.ErrorText);
    }

    [Fact]
    public void Validation_LibraryPathsComeAfterTheFolders()
    {
        var vm = Create();
        vm.FinishListPath = "Relative.csv";
        vm.MaterialLibraryPath = "Relative.adsklib";
        vm.OtherCadFolders = "Relative";

        Assert.Equal(@"Other folders, line 1: enter a full folder path, such as D:\CAD.", vm.ErrorText);
        vm.OtherCadFolders = "";
        Assert.StartsWith("Material library:", vm.ErrorText);
        vm.MaterialLibraryPath = "";
        Assert.StartsWith("Finish list:", vm.ErrorText);
        vm.FinishListPath = "";
        Assert.Null(vm.ErrorText);
    }

    [Fact]
    public void Save_MissingMaterialLibrary_IsRefused_WithMessageNamingTheSetting()
    {
        var asked = new List<string>();
        var vm = CreateWithFiles(null, asked);
        vm.MaterialLibraryPath = $"  {LibraryFile}  ";
        var saved = false;
        vm.Saved += (_, _) => saved = true;

        vm.SaveCommand.Execute(null);

        Assert.Equal(SettingsViewModel.LibraryFileNotFoundError("Material library", LibraryFile), vm.ErrorText);
        Assert.StartsWith("Material library: ", vm.ErrorText);
        Assert.Contains(LibraryFile, vm.ErrorText);
        Assert.True(vm.HasError);
        Assert.Equal(new[] { LibraryFile }, asked);
        Assert.False(saved);
        Assert.Null(vm.SavedSettings);
        Assert.False(File.Exists(SettingsPath));
        Assert.True(vm.IsDirty);
    }

    [Fact]
    public void Save_MissingFinishList_IsRefused_ThenEditingClearsTheMessage()
    {
        var vm = CreateWithFiles(null, new List<string>(), LibraryFile);
        vm.MaterialLibraryPath = LibraryFile;
        vm.FinishListPath = FinishFile;

        vm.SaveCommand.Execute(null);

        Assert.Equal(SettingsViewModel.LibraryFileNotFoundError("Finish list", FinishFile), vm.ErrorText);
        Assert.False(File.Exists(SettingsPath));

        vm.FinishListPath = "";

        Assert.Null(vm.ErrorText);
    }

    [Fact]
    public void Save_ExistingLibraryFiles_AreSavedTrimmed_AndRoundTrip()
    {
        var vm = CreateWithFiles(null, new List<string>(), LibraryFile, FinishFile);
        vm.MaterialLibraryPath = $" {LibraryFile} ";
        vm.FinishListPath = $"\t{FinishFile}";
        var changed = RecordPropertyChanges(vm);

        vm.SaveCommand.Execute(null);

        var expected = new AddinSettings { MaterialLibraryPath = LibraryFile, FinishListPath = FinishFile };
        var loaded = new SettingsStore(_folder).Load();
        Assert.Null(vm.ErrorText);
        Assert.Equal(expected, loaded.Settings);
        Assert.Equal(expected, vm.SavedSettings);
        Assert.Equal(LibraryFile, vm.MaterialLibraryPath);
        Assert.Equal(FinishFile, vm.FinishListPath);
        Assert.Contains(nameof(SettingsViewModel.MaterialLibraryPath), changed);
        Assert.Contains(nameof(SettingsViewModel.FinishListPath), changed);
    }

    [Fact]
    public void Save_UnchangedOrBlankLibraryPaths_AreNotCheckedOnDisk()
    {
        // The library was saved earlier; its drive is offline now. Saving another setting is not refused.
        var asked = new List<string>();
        var vm = CreateWithFiles(new AddinSettings { MaterialLibraryPath = LibraryFile }, asked);
        vm.ShowDeveloperTools = true;

        vm.SaveCommand.Execute(null);

        Assert.Null(vm.ErrorText);
        Assert.NotNull(vm.SavedSettings);
        Assert.Empty(asked);
    }

    [Fact]
    public void Save_FileCheckThatThrows_CountsAsNotFound()
    {
        var vm = new SettingsViewModel(new AddinSettings(), new SettingsStore(_folder), null,
            _ => throw new IOException("The network path was not found."));
        vm.FinishListPath = FinishFile;

        vm.SaveCommand.Execute(null);

        Assert.Equal(SettingsViewModel.LibraryFileNotFoundError("Finish list", FinishFile), vm.ErrorText);
    }

    [Fact]
    public void Save_KeepsPreferredMaterials_ThatTheWindowDoesNotShow()
    {
        var vm = Create(new AddinSettings { PreferredMaterials = new[] { "Test Steel" } });
        vm.ShowDeveloperTools = true;

        vm.SaveCommand.Execute(null);

        Assert.Equal(new[] { "Test Steel" }, new SettingsStore(_folder).Load().Settings.PreferredMaterials);
        Assert.Equal(new[] { "Test Steel" }, vm.SavedSettings!.PreferredMaterials);
    }

    [Fact]
    public void LibraryHelp_SaysBlankUsesTheStarter()
    {
        var vm = Create();

        Assert.Equal("Blank uses the starter material library shipped with the add-in.", vm.MaterialLibraryHelp);
        Assert.Equal("Blank uses the starter finish list shipped with the add-in.", vm.FinishListHelp);
    }
}
