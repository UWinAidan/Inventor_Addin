using InventorAddin.Core.ViewModels;

namespace InventorAddin.Core.Tests.ViewModels;

public sealed class SettingsSummariesTests
{
    [Theory]
    [InlineData("Test Designer", "a, b", "Metric", "Test Designer, 2 other names, Metric")]
    [InlineData("  Test Designer  ", "a", "Imperial", "Test Designer, 1 other name, Imperial")]
    [InlineData("", "", "Metric", "No default designer, no other names, Metric")]
    [InlineData(null, " , ,, ", "Metric", "No default designer, no other names, Metric")]
    public void General_ListsDesignerOtherNamesAndUnits(string? designer, string? otherNames, string units, string expected)
    {
        Assert.Equal(expected, SettingsSummaries.General(designer, otherNames, units));
    }

    [Theory]
    [InlineData(@"Z:\Register", @"Z:\Register")]
    [InlineData("  ", "No shared register")]
    [InlineData(null, "No shared register")]
    public void PartNumbers_IsTheRegisterFolder(string? folder, string expected)
    {
        Assert.Equal(expected, SettingsSummaries.PartNumbers(folder));
    }

    [Fact]
    public void Folders_ListsTheWorkFolderAndCountsTheOthers()
    {
        Assert.Equal(@"C:\CAD_TEST, 2 other folders", SettingsSummaries.Folders(@"C:\CAD_TEST", new[] { @"D:\A", @"D:\B" }));
        Assert.Equal(@"C:\CAD_TEST, 1 other folder", SettingsSummaries.Folders(@"C:\CAD_TEST", new[] { @"D:\A" }));
        Assert.Equal("No work folder, no other folders", SettingsSummaries.Folders("", Array.Empty<string>()));
    }

    [Theory]
    [InlineData("", "", "starter library, starter finish list")]
    [InlineData(@"D:\Libraries\Materials.adsklib", @"\\server\share\finishes.csv", "Materials.adsklib, finishes.csv")]
    [InlineData("Materials.adsklib", null, "Materials.adsklib, starter finish list")]
    [InlineData(@"D:\Libraries\", "", @"D:\Libraries\, starter finish list")]
    public void MaterialFinish_ShowsFileNamesOrTheStarterFiles(string? library, string? finishList, string expected)
    {
        Assert.Equal(expected, SettingsSummaries.MaterialFinish(library, finishList));
    }

    [Fact]
    public void Developer_SaysWhetherToolsAreShown()
    {
        Assert.Equal("Developer tools shown", SettingsSummaries.Developer(true));
        Assert.Equal("Developer tools hidden", SettingsSummaries.Developer(false));
    }
}
