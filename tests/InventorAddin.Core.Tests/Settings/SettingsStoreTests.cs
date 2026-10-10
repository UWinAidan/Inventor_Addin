using System.Text.Json.Nodes;
using InventorAddin.Core.Settings;

namespace InventorAddin.Core.Tests.Settings;

public sealed class SettingsStoreTests : IDisposable
{
    private readonly string _root;
    private readonly string _folder;

    public SettingsStoreTests()
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
    private string BadPath => SettingsPath + ".bad";

    private void WriteSettingsFile(string content)
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(SettingsPath, content);
    }

    [Fact]
    public void Defaults_AreSchemaVersion1_AndDeveloperToolsOff()
    {
        var settings = new AddinSettings();

        Assert.Equal(1, AddinSettings.CurrentSchemaVersion);
        Assert.Equal(1, settings.SchemaVersion);
        Assert.False(settings.ShowDeveloperTools);
        Assert.Equal("", settings.DefaultDesigner);
        Assert.Equal(AddinSettings.DefaultRegisterFolder, settings.RegisterFolder);
        Assert.Equal(UnitsSystem.Metric, settings.DefaultUnits);
    }

    // ---- Register folder and units (task 037) ----

    [Fact]
    public void DefaultRegisterFolder_IsAidansNasFolder()
    {
        Assert.Equal(@"B:\aidan\AWB_Design_Data", AddinSettings.DefaultRegisterFolder);
    }

    [Fact]
    public void Load_FileWrittenBeforeRegisterFolderAndUnits_LoadsDefaults()
    {
        WriteSettingsFile("""
            {
              "schemaVersion": 1,
              "showDeveloperTools": true,
              "defaultDesigner": "A. Person"
            }
            """);

        var result = new SettingsStore(_folder).Load();

        Assert.Empty(result.Warnings);
        Assert.Equal(AddinSettings.DefaultRegisterFolder, result.Settings.RegisterFolder);
        Assert.Equal(UnitsSystem.Metric, result.Settings.DefaultUnits);
        Assert.True(result.Settings.ShowDeveloperTools);
        Assert.Equal("A. Person", result.Settings.DefaultDesigner);
    }

    [Fact]
    public void Load_NullRegisterFolder_LoadsDefault()
    {
        WriteSettingsFile("{ \"registerFolder\": null }");

        var result = new SettingsStore(_folder).Load();

        Assert.Empty(result.Warnings);
        Assert.Equal(AddinSettings.DefaultRegisterFolder, result.Settings.RegisterFolder);
    }

    [Fact]
    public void RegisterFolder_SetToNull_StoresDefault()
    {
        var settings = new AddinSettings { RegisterFolder = null! };

        Assert.Equal(AddinSettings.DefaultRegisterFolder, settings.RegisterFolder);
    }

    [Fact]
    public void Load_BlankRegisterFolder_StaysBlank()
    {
        WriteSettingsFile("{ \"registerFolder\": \"\" }");

        var result = new SettingsStore(_folder).Load();

        Assert.Empty(result.Warnings);
        Assert.Equal("", result.Settings.RegisterFolder);
    }

    [Fact]
    public void Save_WritesRegisterFolderAndUnitsInCamelCase_UnitsByName()
    {
        new SettingsStore(_folder).Save(new AddinSettings { RegisterFolder = @"C:\Shared\Register", DefaultUnits = UnitsSystem.Imperial });

        var json = JsonNode.Parse(File.ReadAllText(SettingsPath))!.AsObject();

        Assert.Equal(@"C:\Shared\Register", json["registerFolder"]!.GetValue<string>());
        Assert.Equal("Imperial", json["defaultUnits"]!.GetValue<string>());
    }

    [Fact]
    public void Save_DefaultUnits_WritesMetric()
    {
        new SettingsStore(_folder).Save(new AddinSettings());

        Assert.Contains("\"defaultUnits\": \"Metric\"", File.ReadAllText(SettingsPath));
    }

    [Theory]
    [InlineData("\"Metric\"", UnitsSystem.Metric)]
    [InlineData("\"Imperial\"", UnitsSystem.Imperial)]
    [InlineData("\"imperial\"", UnitsSystem.Imperial)]
    [InlineData("null", UnitsSystem.Metric)]
    public void Load_KnownUnits_LoadWithoutWarning(string value, UnitsSystem expected)
    {
        WriteSettingsFile($"{{ \"defaultUnits\": {value} }}");

        var result = new SettingsStore(_folder).Load();

        Assert.Empty(result.Warnings);
        Assert.Equal(expected, result.Settings.DefaultUnits);
    }

    [Theory]
    [InlineData("\"Furlongs\"", "\"Furlongs\"")]
    [InlineData("\"\"", "\"\"")]
    [InlineData("\"1\"", "\"1\"")]
    [InlineData("1", "1")]
    [InlineData("true", "true")]
    [InlineData("{ \"a\": 1 }", "an object")]
    [InlineData("[1, 2]", "a list")]
    public void Load_UnknownUnits_LoadsMetric_WithWarning_NotBadFile(string value, string shownAs)
    {
        WriteSettingsFile($$"""
            {
              "showDeveloperTools": true,
              "defaultUnits": {{value}},
              "defaultDesigner": "A. Person"
            }
            """);

        var result = new SettingsStore(_folder).Load();

        Assert.Equal(UnitsSystem.Metric, result.Settings.DefaultUnits);
        Assert.True(result.Settings.ShowDeveloperTools);
        Assert.Equal("A. Person", result.Settings.DefaultDesigner);
        var warning = Assert.Single(result.Warnings);
        Assert.Contains("defaultUnits", warning);
        Assert.Contains(shownAs, warning);
        Assert.Contains("Metric is used", warning);
        Assert.Contains(SettingsPath, warning);
        Assert.True(File.Exists(SettingsPath));
        Assert.False(File.Exists(BadPath));
    }

    [Fact]
    public void Load_UnknownUnitsInInvalidFile_GivesOnlyTheBadFileWarning()
    {
        WriteSettingsFile("{ \"defaultUnits\": \"Furlongs\", \"showDeveloperTools\": \"yes\" }");

        var result = new SettingsStore(_folder).Load();

        var warning = Assert.Single(result.Warnings);
        Assert.Contains("settings.json.bad", warning);
        Assert.Equal(new AddinSettings(), result.Settings);
    }

    [Fact]
    public void Load_UnknownUnits_WarnsOnEveryLoadUntilSaved()
    {
        WriteSettingsFile("{ \"defaultUnits\": \"Furlongs\" }");
        var store = new SettingsStore(_folder);

        Assert.Single(store.Load().Warnings);
        Assert.Single(store.Load().Warnings);

        store.Save(store.Load().Settings);

        Assert.Empty(store.Load().Warnings);
    }

    [Fact]
    public void Equality_ComparesRegisterFolderAndUnits()
    {
        Assert.NotEqual(new AddinSettings { RegisterFolder = "" }, new AddinSettings());
        Assert.NotEqual(new AddinSettings { DefaultUnits = UnitsSystem.Imperial }, new AddinSettings());
        Assert.Equal(new AddinSettings { RegisterFolder = @"C:\R", DefaultUnits = UnitsSystem.Imperial },
                     new AddinSettings { RegisterFolder = @"C:\R", DefaultUnits = UnitsSystem.Imperial });
    }

    [Fact]
    public void Load_FileWrittenBeforeDefaultDesigner_LoadsItAsEmpty()
    {
        WriteSettingsFile("""
            {
              "schemaVersion": 1,
              "showDeveloperTools": true
            }
            """);

        var result = new SettingsStore(_folder).Load();

        Assert.Empty(result.Warnings);
        Assert.Equal("", result.Settings.DefaultDesigner);
        Assert.True(result.Settings.ShowDeveloperTools);
        Assert.Equal(1, result.Settings.SchemaVersion);
    }

    [Fact]
    public void Load_NullDefaultDesigner_LoadsAsEmpty()
    {
        WriteSettingsFile("{ \"defaultDesigner\": null }");

        var result = new SettingsStore(_folder).Load();

        Assert.Empty(result.Warnings);
        Assert.Equal("", result.Settings.DefaultDesigner);
        Assert.Equal(new AddinSettings(), result.Settings);
    }

    [Fact]
    public void DefaultDesigner_SetToNull_StoresEmpty()
    {
        var settings = new AddinSettings { DefaultDesigner = null! };

        Assert.Equal("", settings.DefaultDesigner);
    }

    [Fact]
    public void Equality_ComparesDefaultDesigner()
    {
        Assert.Equal(new AddinSettings { DefaultDesigner = "A. Person" }, new AddinSettings { DefaultDesigner = "A. Person" });
        Assert.NotEqual(new AddinSettings { DefaultDesigner = "A. Person" }, new AddinSettings());
    }

    [Fact]
    public void Save_WritesDefaultDesignerInCamelCase()
    {
        new SettingsStore(_folder).Save(new AddinSettings { DefaultDesigner = "A. Person" });

        var json = JsonNode.Parse(File.ReadAllText(SettingsPath))!.AsObject();

        Assert.Equal("A. Person", json["defaultDesigner"]!.GetValue<string>());
    }

    [Fact]
    public void Constructor_UsesSettingsJsonInGivenFolder()
    {
        var store = new SettingsStore(_folder);

        Assert.Equal(_folder, store.Folder);
        Assert.Equal(SettingsPath, store.FilePath);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_RejectsEmptyFolder(string folder)
    {
        Assert.Throws<ArgumentException>(() => new SettingsStore(folder));
    }

    [Fact]
    public void Load_MissingFile_ReturnsDefaults_WithoutCreatingFileOrFolder()
    {
        var result = new SettingsStore(_folder).Load();

        Assert.Equal(new AddinSettings(), result.Settings);
        Assert.Empty(result.Warnings);
        Assert.False(File.Exists(SettingsPath));
        Assert.False(Directory.Exists(_folder));
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("[1, 2]")]
    [InlineData("{ \"showDeveloperTools\": \"yes\" }")]
    public void Load_InvalidFile_ReturnsDefaults_RenamesToBad_AndWarns(string content)
    {
        WriteSettingsFile(content);

        var result = new SettingsStore(_folder).Load();

        Assert.Equal(new AddinSettings(), result.Settings);
        var warning = Assert.Single(result.Warnings);
        Assert.Contains("settings.json.bad", warning);
        Assert.False(File.Exists(SettingsPath));
        Assert.Equal(content, File.ReadAllText(BadPath));
    }

    [Fact]
    public void Load_InvalidFile_OverwritesOlderBadFile()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(BadPath, "older bad file");
        WriteSettingsFile("{ newer bad file");

        var result = new SettingsStore(_folder).Load();

        Assert.Single(result.Warnings);
        Assert.Equal("{ newer bad file", File.ReadAllText(BadPath));
        Assert.False(File.Exists(SettingsPath));
    }

    [Fact]
    public void Load_IgnoresUnknownProperties()
    {
        WriteSettingsFile("""
            {
              "schemaVersion": 1,
              "showDeveloperTools": true,
              "someFutureSetting": "value",
              "nested": { "a": [1, 2, 3] }
            }
            """);

        var result = new SettingsStore(_folder).Load();

        Assert.Empty(result.Warnings);
        Assert.True(result.Settings.ShowDeveloperTools);
        Assert.Equal(1, result.Settings.SchemaVersion);
        Assert.True(File.Exists(SettingsPath));
        Assert.False(File.Exists(BadPath));
    }

    [Fact]
    public void Load_FillsMissingPropertiesWithDefaults()
    {
        WriteSettingsFile("{ \"showDeveloperTools\": true }");

        var result = new SettingsStore(_folder).Load();

        Assert.Empty(result.Warnings);
        Assert.True(result.Settings.ShowDeveloperTools);
        Assert.Equal(AddinSettings.CurrentSchemaVersion, result.Settings.SchemaVersion);
    }

    [Fact]
    public void Load_EmptyObject_ReturnsDefaults()
    {
        WriteSettingsFile("{}");

        var result = new SettingsStore(_folder).Load();

        Assert.Empty(result.Warnings);
        Assert.Equal(new AddinSettings(), result.Settings);
    }

    [Fact]
    public void Load_ReadsSchemaVersionAsWritten()
    {
        WriteSettingsFile("{ \"schemaVersion\": 7 }");

        var result = new SettingsStore(_folder).Load();

        Assert.Equal(7, result.Settings.SchemaVersion);
    }

    [Fact]
    public void Save_CreatesFolder_AndWritesFile()
    {
        var store = new SettingsStore(_folder);

        store.Save(new AddinSettings { ShowDeveloperTools = true });

        Assert.True(File.Exists(SettingsPath));
    }

    [Fact]
    public void Save_WritesIndentedCamelCaseJson()
    {
        new SettingsStore(_folder).Save(new AddinSettings { ShowDeveloperTools = true });

        var text = File.ReadAllText(SettingsPath);
        var json = JsonNode.Parse(text)!.AsObject();

        Assert.Equal(1, json["schemaVersion"]!.GetValue<int>());
        Assert.True(json["showDeveloperTools"]!.GetValue<bool>());
        Assert.False(json.ContainsKey("SchemaVersion"));
        Assert.False(json.ContainsKey("ShowDeveloperTools"));
        Assert.Contains("\n", text);
        Assert.Contains("  \"schemaVersion\"", text);
    }

    [Fact]
    public void Save_ReplacesExistingFile_AndLeavesNoTempFiles()
    {
        var store = new SettingsStore(_folder);
        store.Save(new AddinSettings { ShowDeveloperTools = true });

        store.Save(new AddinSettings { ShowDeveloperTools = false });

        Assert.False(store.Load().Settings.ShowDeveloperTools);
        Assert.Equal(new[] { SettingsPath }, Directory.GetFiles(_folder));
    }

    [Fact]
    public void Save_WhenReplaceFails_Throws_AndRemovesTempFile()
    {
        // A directory where settings.json should be makes the final replace step fail.
        Directory.CreateDirectory(SettingsPath);
        var store = new SettingsStore(_folder);

        // Linux throws IOException; Windows throws UnauthorizedAccessException for a move onto a directory.
        var ex = Record.Exception(() => store.Save(new AddinSettings()));
        Assert.True(ex is IOException or UnauthorizedAccessException, $"Unexpected exception: {ex}");

        Assert.Empty(Directory.GetFiles(_folder));
        Assert.True(Directory.Exists(SettingsPath));
    }

    [Fact]
    public void Save_RejectsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new SettingsStore(_folder).Save(null!));
    }

    [Theory]
    [InlineData(false, 1, "", AddinSettings.DefaultRegisterFolder, UnitsSystem.Metric)]
    [InlineData(true, 1, "", "", UnitsSystem.Metric)]
    [InlineData(true, 3, "", @"\\server\share\register", UnitsSystem.Imperial)]
    [InlineData(false, 1, "A. Person", @"D:\Ünïcode folder", UnitsSystem.Imperial)]
    [InlineData(true, 1, "Ünïcode Désigner \"quoted\"", AddinSettings.DefaultRegisterFolder, UnitsSystem.Metric)]
    public void SaveThenLoad_RoundTrips(bool showDeveloperTools, int schemaVersion, string defaultDesigner, string registerFolder, UnitsSystem units)
    {
        var store = new SettingsStore(_folder);
        var original = new AddinSettings
        {
            ShowDeveloperTools = showDeveloperTools,
            SchemaVersion = schemaVersion,
            DefaultDesigner = defaultDesigner,
            RegisterFolder = registerFolder,
            DefaultUnits = units,
        };

        store.Save(original);
        var result = store.Load();

        Assert.Empty(result.Warnings);
        Assert.Equal(original, result.Settings);
        Assert.NotSame(original, result.Settings);
    }
}
