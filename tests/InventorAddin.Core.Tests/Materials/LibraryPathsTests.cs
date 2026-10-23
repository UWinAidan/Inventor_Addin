using InventorAddin.Core.Materials;
using InventorAddin.Core.Settings;

namespace InventorAddin.Core.Tests.Materials;

public sealed class LibraryPathsTests
{
    private const string AddinFolder = @"C:\Addins\InventorAddin";

    private static string Starter(string fileName) => Path.Combine(AddinFolder, "libraries", fileName);

    [Fact]
    public void StarterFileNames_AreTheShippedFiles()
    {
        Assert.Equal("libraries", LibraryPaths.StarterFolderName);
        Assert.Equal("StarterMaterials.adsklib", LibraryPaths.StarterMaterialLibraryFileName);
        Assert.Equal("finishes.csv", LibraryPaths.StarterFinishListFileName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void MaterialLibrary_BlankSetting_IsTheStarter(string setting)
    {
        var path = LibraryPaths.MaterialLibrary(new AddinSettings { MaterialLibraryPath = setting }, AddinFolder);

        Assert.Equal(new LibraryPath(Starter("StarterMaterials.adsklib"), IsStarter: true), path);
    }

    [Fact]
    public void MaterialLibrary_Setting_IsUsedTrimmed()
    {
        var path = LibraryPaths.MaterialLibrary(
            new AddinSettings { MaterialLibraryPath = @"  D:\Libraries\Mine.adsklib  " }, AddinFolder);

        Assert.Equal(new LibraryPath(@"D:\Libraries\Mine.adsklib", IsStarter: false), path);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void FinishList_BlankSetting_IsTheStarter(string setting)
    {
        var path = LibraryPaths.FinishList(new AddinSettings { FinishListPath = setting }, AddinFolder);

        Assert.Equal(new LibraryPath(Starter("finishes.csv"), IsStarter: true), path);
    }

    [Fact]
    public void FinishList_Setting_IsUsedTrimmed()
    {
        var path = LibraryPaths.FinishList(new AddinSettings { FinishListPath = "\t\\\\server\\share\\finishes.csv " }, AddinFolder);

        Assert.Equal(new LibraryPath(@"\\server\share\finishes.csv", IsStarter: false), path);
    }

    [Fact]
    public void Paths_EachSettingIsIndependent()
    {
        var settings = new AddinSettings { MaterialLibraryPath = @"D:\Mine.adsklib" };

        Assert.False(LibraryPaths.MaterialLibrary(settings, AddinFolder).IsStarter);
        Assert.True(LibraryPaths.FinishList(settings, AddinFolder).IsStarter);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Paths_NeedAnAddinFolder(string? folder)
    {
        Assert.Throws<ArgumentException>(() => LibraryPaths.MaterialLibrary(new AddinSettings(), folder!));
        Assert.Throws<ArgumentException>(() => LibraryPaths.FinishList(new AddinSettings(), folder!));
    }

    // ---- Hole workbook (task 086) ----

    [Fact]
    public void HoleWorkbook_StarterFileName_AndExtension()
    {
        Assert.Equal("AWB_Holes.xlsx", LibraryPaths.StarterHoleWorkbookFileName);
        Assert.Equal(".xlsx", LibraryPaths.HoleWorkbookExtension);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void HoleWorkbook_BlankSetting_IsTheStarter(string setting)
    {
        var path = LibraryPaths.HoleWorkbook(new AddinSettings { HoleWorkbookPath = setting }, AddinFolder);

        Assert.Equal(new LibraryPath(Starter("AWB_Holes.xlsx"), IsStarter: true), path);
    }

    [Fact]
    public void HoleWorkbook_Setting_IsUsedTrimmed()
    {
        var path = LibraryPaths.HoleWorkbook(new AddinSettings { HoleWorkbookPath = " B:\\Data\\AWB_Holes.xlsx\t" }, AddinFolder);

        Assert.Equal(new LibraryPath(@"B:\Data\AWB_Holes.xlsx", IsStarter: false), path);
    }

    [Fact]
    public void HoleWorkbook_NeedsSettingsAndAnAddinFolder()
    {
        Assert.Throws<ArgumentNullException>(() => LibraryPaths.HoleWorkbook(null!, AddinFolder));
        Assert.Throws<ArgumentException>(() => LibraryPaths.HoleWorkbook(new AddinSettings(), " "));
    }

    [Fact]
    public void Paths_RejectNullSettings()
    {
        Assert.Throws<ArgumentNullException>(() => LibraryPaths.MaterialLibrary(null!, AddinFolder));
        Assert.Throws<ArgumentNullException>(() => LibraryPaths.FinishList(null!, AddinFolder));
    }

    // ---- Preferred materials ----

    [Fact]
    public void TogglePreferred_NameNotThere_IsAddedAtTheEnd_Trimmed()
    {
        var settings = new AddinSettings { PreferredMaterials = new[] { "Test Steel" } };

        var toggled = LibraryPaths.TogglePreferred(settings, "  Test Alloy ");

        Assert.Equal(new[] { "Test Steel", "Test Alloy" }, toggled.PreferredMaterials);
    }

    [Fact]
    public void TogglePreferred_NameThere_IsRemoved_IgnoringCase()
    {
        var settings = new AddinSettings { PreferredMaterials = new[] { "Test Steel", "Test Alloy", "Test Plastic" } };

        var toggled = LibraryPaths.TogglePreferred(settings, "test ALLOY");

        Assert.Equal(new[] { "Test Steel", "Test Plastic" }, toggled.PreferredMaterials);
    }

    [Fact]
    public void TogglePreferred_RemovesEveryMatchingEntry()
    {
        var settings = new AddinSettings { PreferredMaterials = new[] { "Test Steel", " test steel ", "Other" } };

        var toggled = LibraryPaths.TogglePreferred(settings, "Test Steel");

        Assert.Equal(new[] { "Other" }, toggled.PreferredMaterials);
    }

    [Fact]
    public void TogglePreferred_Twice_IsBackWhereItStarted()
    {
        var settings = new AddinSettings { PreferredMaterials = new[] { "Test Steel" } };

        var toggled = LibraryPaths.TogglePreferred(LibraryPaths.TogglePreferred(settings, "Test Alloy"), "Test Alloy");

        Assert.Equal(settings, toggled);
    }

    [Fact]
    public void TogglePreferred_LeavesTheGivenSettingsAndOtherValuesAlone()
    {
        var settings = new AddinSettings
        {
            DefaultDesigner = "A. Person",
            FinishListPath = @"D:\finishes.csv",
            PreferredMaterials = new[] { "Test Steel" },
        };

        var toggled = LibraryPaths.TogglePreferred(settings, "Test Alloy");

        Assert.NotSame(settings, toggled);
        Assert.Equal(new[] { "Test Steel" }, settings.PreferredMaterials);
        Assert.Equal(settings with { PreferredMaterials = new[] { "Test Steel", "Test Alloy" } }, toggled);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TogglePreferred_BlankName_Throws(string? name)
    {
        Assert.Throws<ArgumentException>(() => LibraryPaths.TogglePreferred(new AddinSettings(), name!));
    }

    [Fact]
    public void TogglePreferred_NullSettings_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => LibraryPaths.TogglePreferred(null!, "Test Steel"));
    }

    [Theory]
    [InlineData("Test Steel", true)]
    [InlineData("  TEST steel ", true)]
    [InlineData("Test Alloy", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsPreferred_IgnoresCaseAndSpaces(string? name, bool expected)
    {
        var settings = new AddinSettings { PreferredMaterials = new[] { "Test Steel" } };

        Assert.Equal(expected, LibraryPaths.IsPreferred(settings, name));
    }
}
