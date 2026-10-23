using System.Text.Json.Nodes;
using InventorAddin.Core.Holes;
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

    // ---- Work folder, other CAD folders, other names (task 049) ----

    [Fact]
    public void DefaultWorkFolder_IsAidansCadFolder()
    {
        Assert.Equal(@"C:\Users\4aida\OneDrive\Documents\CADING", AddinSettings.DefaultWorkFolder);
    }

    [Fact]
    public void NewSettings_Defaults()
    {
        var settings = new AddinSettings();

        Assert.Equal(AddinSettings.DefaultWorkFolder, settings.WorkFolder);
        Assert.Empty(settings.OtherCadFolders);
        Assert.Equal("", settings.OtherNamesForMe);
    }

    [Fact]
    public void Load_FileWrittenBeforeFoldersAndNames_LoadsDefaults()
    {
        WriteSettingsFile("""
            {
              "schemaVersion": 1,
              "showDeveloperTools": true,
              "defaultDesigner": "A. Person",
              "registerFolder": "",
              "defaultUnits": "Imperial"
            }
            """);

        var result = new SettingsStore(_folder).Load();

        Assert.Empty(result.Warnings);
        Assert.Equal(AddinSettings.DefaultWorkFolder, result.Settings.WorkFolder);
        Assert.Empty(result.Settings.OtherCadFolders);
        Assert.Equal("", result.Settings.OtherNamesForMe);
        Assert.Equal(UnitsSystem.Imperial, result.Settings.DefaultUnits);
        Assert.Equal("", result.Settings.RegisterFolder);
    }

    [Fact]
    public void Load_NullsForNewSettings_LoadDefaults()
    {
        WriteSettingsFile("""{ "workFolder": null, "otherCadFolders": null, "otherNamesForMe": null }""");

        var result = new SettingsStore(_folder).Load();

        Assert.Empty(result.Warnings);
        Assert.Equal(new AddinSettings(), result.Settings);
    }

    [Fact]
    public void Load_BlankWorkFolder_StaysBlank()
    {
        WriteSettingsFile("""{ "workFolder": "" }""");

        var result = new SettingsStore(_folder).Load();

        Assert.Empty(result.Warnings);
        Assert.Equal("", result.Settings.WorkFolder);
    }

    [Fact]
    public void Load_OtherCadFolders_ReadsArray_DroppingNullEntries()
    {
        WriteSettingsFile("""{ "otherCadFolders": [ "D:\\One", null, "\\\\server\\two" ] }""");

        var result = new SettingsStore(_folder).Load();

        Assert.Empty(result.Warnings);
        Assert.Equal(new[] { @"D:\One", @"\\server\two" }, result.Settings.OtherCadFolders);
    }

    [Fact]
    public void Load_OtherCadFoldersNotAList_IsABadFile()
    {
        WriteSettingsFile("""{ "otherCadFolders": "D:\\One" }""");

        var result = new SettingsStore(_folder).Load();

        Assert.Contains("settings.json.bad", Assert.Single(result.Warnings));
        Assert.Equal(new AddinSettings(), result.Settings);
    }

    [Fact]
    public void Setters_Null_StoreDefaults()
    {
        var settings = new AddinSettings { WorkFolder = null!, OtherCadFolders = null!, OtherNamesForMe = null! };

        Assert.Equal(AddinSettings.DefaultWorkFolder, settings.WorkFolder);
        Assert.Empty(settings.OtherCadFolders);
        Assert.Equal("", settings.OtherNamesForMe);
    }

    [Fact]
    public void OtherCadFolders_IsCopiedWhenSet()
    {
        var list = new List<string> { @"D:\One" };
        var settings = new AddinSettings { OtherCadFolders = list };

        list.Add(@"E:\Two");

        Assert.Equal(new[] { @"D:\One" }, settings.OtherCadFolders);
    }

    [Fact]
    public void Save_WritesNewSettingsInCamelCase_FoldersAsArray()
    {
        new SettingsStore(_folder).Save(new AddinSettings
        {
            WorkFolder = @"D:\Work",
            OtherCadFolders = new[] { @"E:\One", @"\\server\two" },
            OtherNamesForMe = "A.Person, ap",
        });

        var json = JsonNode.Parse(File.ReadAllText(SettingsPath))!.AsObject();

        Assert.Equal(@"D:\Work", json["workFolder"]!.GetValue<string>());
        var folders = json["otherCadFolders"]!.AsArray();
        Assert.Equal(new[] { @"E:\One", @"\\server\two" }, folders.Select(f => f!.GetValue<string>()));
        Assert.Equal("A.Person, ap", json["otherNamesForMe"]!.GetValue<string>());
    }

    [Fact]
    public void Save_DefaultOtherCadFolders_WritesEmptyArray()
    {
        new SettingsStore(_folder).Save(new AddinSettings());

        var json = JsonNode.Parse(File.ReadAllText(SettingsPath))!.AsObject();

        Assert.Empty(json["otherCadFolders"]!.AsArray());
    }

    [Fact]
    public void Equality_ComparesNewSettings_FoldersByValueAndOrder()
    {
        Assert.Equal(
            new AddinSettings { OtherCadFolders = new[] { @"D:\One", @"E:\Two" } },
            new AddinSettings { OtherCadFolders = new List<string> { @"D:\One", @"E:\Two" } });
        Assert.Equal(
            new AddinSettings { OtherCadFolders = new[] { @"D:\One" } }.GetHashCode(),
            new AddinSettings { OtherCadFolders = new[] { @"D:\One" } }.GetHashCode());
        Assert.NotEqual(
            new AddinSettings { OtherCadFolders = new[] { @"D:\One", @"E:\Two" } },
            new AddinSettings { OtherCadFolders = new[] { @"E:\Two", @"D:\One" } });
        Assert.NotEqual(new AddinSettings { OtherCadFolders = new[] { @"D:\One" } }, new AddinSettings());
        Assert.NotEqual(new AddinSettings { WorkFolder = "" }, new AddinSettings());
        Assert.NotEqual(new AddinSettings { OtherNamesForMe = "x" }, new AddinSettings());
    }

    [Fact]
    public void Equality_ComparesEverySetting()
    {
        // Guards the hand-written Equals and GetHashCode: each setting changed on its own makes the two unequal
        // and changes the hash code.
        var changes = new Func<AddinSettings, AddinSettings>[]
        {
            s => s with { SchemaVersion = 2 },
            s => s with { ShowDeveloperTools = true },
            s => s with { DefaultDesigner = "x" },
            s => s with { RegisterFolder = "" },
            s => s with { DefaultUnits = UnitsSystem.Imperial },
            s => s with { WorkFolder = "" },
            s => s with { OtherCadFolders = new[] { @"D:\One" } },
            s => s with { OtherNamesForMe = "x" },
            s => s with { MaterialLibraryPath = @"D:\Lib.adsklib" },
            s => s with { FinishListPath = @"D:\finishes.csv" },
            s => s with { HoleWorkbookPath = @"D:\AWB_Holes.xlsx" },
            s => s with { UseInventorHoleCommand = true },
            s => s with { LastBlindDepthMm = 12m },
            s => s with { LastBlindDepthIn = 0.5m },
            s => s with { LastDowelSeatsMm = new Dictionary<string, HoleDowelSeat> { ["6 mm"] = new(7m, 3m, null, null) } },
            s => s with { LastDowelSeatsIn = new Dictionary<string, HoleDowelSeat> { ["1/4\""] = new(null, null, 0.3m, 90m) } },
            s => s with { PreferredMaterials = new[] { "Test Steel" } },
        };
        var settableProperties = typeof(AddinSettings).GetProperties().Count(p => p.CanWrite);

        Assert.Equal(settableProperties, changes.Length);
        foreach (var change in changes)
        {
            var changed = change(new AddinSettings());
            Assert.NotEqual(new AddinSettings(), changed);
            Assert.NotEqual(new AddinSettings().GetHashCode(), changed.GetHashCode());
        }
    }

    // ---- Library paths and preferred materials (task 070) ----

    [Fact]
    public void LibrarySettings_Defaults_AreBlankAndEmpty()
    {
        var settings = new AddinSettings();

        Assert.Equal("", settings.MaterialLibraryPath);
        Assert.Equal("", settings.FinishListPath);
        Assert.Empty(settings.PreferredMaterials);
    }

    [Fact]
    public void Load_FileWrittenBeforeLibrarySettings_LoadsDefaults()
    {
        WriteSettingsFile("""
            {
              "schemaVersion": 1,
              "defaultDesigner": "A. Person",
              "workFolder": "D:\\Work",
              "otherCadFolders": [ "E:\\One" ],
              "otherNamesForMe": "ap"
            }
            """);

        var result = new SettingsStore(_folder).Load();

        Assert.Empty(result.Warnings);
        Assert.Equal("", result.Settings.MaterialLibraryPath);
        Assert.Equal("", result.Settings.FinishListPath);
        Assert.Empty(result.Settings.PreferredMaterials);
        Assert.Equal("A. Person", result.Settings.DefaultDesigner);
    }

    [Fact]
    public void Load_NullLibrarySettings_LoadDefaults()
    {
        WriteSettingsFile("""{ "materialLibraryPath": null, "finishListPath": null, "preferredMaterials": null }""");

        var result = new SettingsStore(_folder).Load();

        Assert.Empty(result.Warnings);
        Assert.Equal(new AddinSettings(), result.Settings);
    }

    [Fact]
    public void Load_PreferredMaterials_ReadsArray_DroppingNullEntries()
    {
        WriteSettingsFile("""{ "preferredMaterials": [ "Test Steel", null, "Test Alloy" ] }""");

        var result = new SettingsStore(_folder).Load();

        Assert.Empty(result.Warnings);
        Assert.Equal(new[] { "Test Steel", "Test Alloy" }, result.Settings.PreferredMaterials);
    }

    [Fact]
    public void LibrarySetters_Null_StoreDefaults()
    {
        var settings = new AddinSettings { MaterialLibraryPath = null!, FinishListPath = null!, PreferredMaterials = null! };

        Assert.Equal("", settings.MaterialLibraryPath);
        Assert.Equal("", settings.FinishListPath);
        Assert.Empty(settings.PreferredMaterials);
    }

    [Fact]
    public void PreferredMaterials_IsCopiedWhenSet()
    {
        var list = new List<string> { "Test Steel" };
        var settings = new AddinSettings { PreferredMaterials = list };

        list.Add("Test Alloy");

        Assert.Equal(new[] { "Test Steel" }, settings.PreferredMaterials);
    }

    [Fact]
    public void Equality_ComparesPreferredMaterialsByValueAndOrder()
    {
        Assert.Equal(
            new AddinSettings { PreferredMaterials = new[] { "A", "B" } },
            new AddinSettings { PreferredMaterials = new List<string> { "A", "B" } });
        Assert.Equal(
            new AddinSettings { PreferredMaterials = new[] { "A" } }.GetHashCode(),
            new AddinSettings { PreferredMaterials = new List<string> { "A" } }.GetHashCode());
        Assert.NotEqual(
            new AddinSettings { PreferredMaterials = new[] { "A", "B" } },
            new AddinSettings { PreferredMaterials = new[] { "B", "A" } });
    }

    [Fact]
    public void Save_WritesLibrarySettingsInCamelCase_ThenRoundTrips()
    {
        var store = new SettingsStore(_folder);
        var original = new AddinSettings
        {
            MaterialLibraryPath = @"D:\Ünïcode\Lib.adsklib",
            FinishListPath = @"\\server\share\finishes.csv",
            PreferredMaterials = new[] { "Test Steel", "Test \"Alloy\"" },
        };

        store.Save(original);

        var json = JsonNode.Parse(File.ReadAllText(SettingsPath))!.AsObject();
        Assert.Equal(@"D:\Ünïcode\Lib.adsklib", json["materialLibraryPath"]!.GetValue<string>());
        Assert.Equal(@"\\server\share\finishes.csv", json["finishListPath"]!.GetValue<string>());
        Assert.Equal(new[] { "Test Steel", "Test \"Alloy\"" }, json["preferredMaterials"]!.AsArray().Select(m => m!.GetValue<string>()));

        var result = store.Load();
        Assert.Empty(result.Warnings);
        Assert.Equal(original, result.Settings);
    }

    // ---- Hole settings (task 086) ----

    [Fact]
    public void HoleSettings_Defaults_AreBlankAndAwbHole()
    {
        var settings = new AddinSettings();

        Assert.Equal("", settings.HoleWorkbookPath);
        Assert.False(settings.UseInventorHoleCommand);
    }

    [Fact]
    public void Load_FileWrittenBeforeHoleSettings_LoadsDefaults()
    {
        WriteSettingsFile("""
            {
              "schemaVersion": 1,
              "materialLibraryPath": "D:\\Lib.adsklib",
              "finishListPath": "D:\\finishes.csv"
            }
            """);

        var result = new SettingsStore(_folder).Load();

        Assert.Empty(result.Warnings);
        Assert.Equal("", result.Settings.HoleWorkbookPath);
        Assert.False(result.Settings.UseInventorHoleCommand);
        Assert.Equal(@"D:\Lib.adsklib", result.Settings.MaterialLibraryPath);
    }

    [Fact]
    public void Load_NullHoleWorkbookPath_LoadsAsBlank()
    {
        WriteSettingsFile("""{ "holeWorkbookPath": null }""");

        var result = new SettingsStore(_folder).Load();

        Assert.Empty(result.Warnings);
        Assert.Equal(new AddinSettings(), result.Settings);
    }

    [Fact]
    public void Save_WritesHoleSettingsInCamelCase_ThenRoundTrips()
    {
        var store = new SettingsStore(_folder);
        var original = new AddinSettings { HoleWorkbookPath = @"\\server\share\AWB_Holes.xlsx", UseInventorHoleCommand = true };

        store.Save(original);

        var json = JsonNode.Parse(File.ReadAllText(SettingsPath))!.AsObject();
        Assert.Equal(@"\\server\share\AWB_Holes.xlsx", json["holeWorkbookPath"]!.GetValue<string>());
        Assert.True(json["useInventorHoleCommand"]!.GetValue<bool>());

        var result = store.Load();
        Assert.Empty(result.Warnings);
        Assert.Equal(original, result.Settings);
    }

    // ---- Last blind hole depths (task 097) ----

    [Fact]
    public void Save_WritesLastBlindDepthsInCamelCase_ThenRoundTrips()
    {
        var store = new SettingsStore(_folder);
        var original = new AddinSettings { LastBlindDepthMm = 12.5m, LastBlindDepthIn = 0.375m };

        store.Save(original);

        var json = JsonNode.Parse(File.ReadAllText(SettingsPath))!.AsObject();
        Assert.Equal(12.5m, json["lastBlindDepthMm"]!.GetValue<decimal>());
        Assert.Equal(0.375m, json["lastBlindDepthIn"]!.GetValue<decimal>());

        var result = store.Load();
        Assert.Empty(result.Warnings);
        Assert.Equal(original, result.Settings);
    }

    [Fact]
    public void Load_FileWrittenBeforeLastBlindDepths_LoadsThemAsNone()
    {
        WriteSettingsFile("""
            {
              "schemaVersion": 1,
              "holeWorkbookPath": "D:\\Holes.xlsx"
            }
            """);

        var result = new SettingsStore(_folder).Load();

        Assert.Empty(result.Warnings);
        Assert.Null(result.Settings.LastBlindDepthMm);
        Assert.Null(result.Settings.LastBlindDepthIn);
    }

    [Fact]
    public void LastBlindDepths_NotGreaterThanZero_AreStoredAsNone()
    {
        WriteSettingsFile("{ \"lastBlindDepthMm\": 0, \"lastBlindDepthIn\": -1 }");

        var result = new SettingsStore(_folder).Load();

        Assert.Empty(result.Warnings);
        Assert.Equal(new AddinSettings(), result.Settings);
    }

    // ---- Last dowel seats (task 106) ----

    [Fact]
    public void Save_WritesLastDowelSeats_KeyedBySize_LeavingOutUnknownValues_ThenRoundTrips()
    {
        var store = new SettingsStore(_folder);
        var original = new AddinSettings
        {
            LastDowelSeatsMm = new Dictionary<string, HoleDowelSeat>
            {
                ["6 mm"] = new(7.5m, 3m, null, null),
                ["8 mm"] = new(9m, 4m, 9.5m, 100m),
            },
            LastDowelSeatsIn = new Dictionary<string, HoleDowelSeat> { ["1/4\""] = new(null, null, 0.29m, 90m) },
        };

        store.Save(original);

        var json = JsonNode.Parse(File.ReadAllText(SettingsPath))!.AsObject();
        var six = json["lastDowelSeatsMm"]!["6 mm"]!.AsObject();
        Assert.Equal(7.5m, six["cboreDiameter"]!.GetValue<decimal>());
        Assert.Equal(3m, six["cboreDepth"]!.GetValue<decimal>());
        Assert.False(six.ContainsKey("csinkDiameter"));
        Assert.False(six.ContainsKey("isEmpty"));
        Assert.Equal(90m, json["lastDowelSeatsIn"]!["1/4\""]!["csinkAngleDeg"]!.GetValue<decimal>());

        var result = store.Load();
        Assert.Empty(result.Warnings);
        Assert.Equal(original, result.Settings);
        Assert.Equal(new HoleDowelSeat(9m, 4m, 9.5m, 100m), result.Settings.LastDowelSeatsMm["8 MM"]);
    }

    [Fact]
    public void Load_FileWrittenBeforeLastDowelSeats_LoadsThemAsEmpty()
    {
        WriteSettingsFile("{ \"schemaVersion\": 1, \"lastBlindDepthMm\": 12 }");

        var result = new SettingsStore(_folder).Load();

        Assert.Empty(result.Warnings);
        Assert.Empty(result.Settings.LastDowelSeatsMm);
        Assert.Empty(result.Settings.LastDowelSeatsIn);
    }

    [Fact]
    public void Load_LastDowelSeats_NullMapsEntriesAndMissingValues_AreTolerated()
    {
        WriteSettingsFile("""
            {
              "lastDowelSeatsMm": { "6 mm": null, " ": { "cboreDiameter": 7 }, "8 mm": { "cboreDiameter": 9 }, "8 MM": { "cboreDiameter": 1 } },
              "lastDowelSeatsIn": null
            }
            """);

        var result = new SettingsStore(_folder).Load();

        Assert.Empty(result.Warnings);
        var seat = Assert.Single(result.Settings.LastDowelSeatsMm);
        Assert.Equal("8 mm", seat.Key);
        Assert.Equal(new HoleDowelSeat(9m, null, null, null), seat.Value);
        Assert.Empty(result.Settings.LastDowelSeatsIn);
    }

    [Fact]
    public void LastDowelSeats_AreCopiedWhenSet()
    {
        var map = new Dictionary<string, HoleDowelSeat> { ["6 mm"] = new(7m, 3m, null, null) };
        var settings = new AddinSettings { LastDowelSeatsMm = map };

        map["8 mm"] = new(9m, 4m, null, null);

        Assert.Single(settings.LastDowelSeatsMm);
        Assert.Equal(settings, new AddinSettings { LastDowelSeatsMm = new Dictionary<string, HoleDowelSeat> { ["6 mm"] = new(7m, 3m, null, null) } });
        Assert.NotEqual(settings, new AddinSettings { LastDowelSeatsMm = new Dictionary<string, HoleDowelSeat> { ["6 mm"] = new(7m, 2m, null, null) } });
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
            WorkFolder = registerFolder,
            OtherCadFolders = showDeveloperTools ? new[] { @"E:\Ünïcode one", @"\\server\two" } : Array.Empty<string>(),
            OtherNamesForMe = defaultDesigner + ", other",
        };

        store.Save(original);
        var result = store.Load();

        Assert.Empty(result.Warnings);
        Assert.Equal(original, result.Settings);
        Assert.NotSame(original, result.Settings);
    }
}
