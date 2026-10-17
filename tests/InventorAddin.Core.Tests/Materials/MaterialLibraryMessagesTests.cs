using InventorAddin.Core.Materials;

namespace InventorAddin.Core.Tests.Materials;

public sealed class MaterialLibraryMessagesTests
{
    private static readonly LibraryPath Starter = new(@"C:\addin\libraries\StarterMaterials.adsklib", IsStarter: true);
    private static readonly LibraryPath FromSettings = new(@"D:\shared\Test.adsklib", IsStarter: false);

    [Fact]
    public void Missing_Starter_NamesPathAndStarter()
    {
        Assert.Equal(
            @"The starter material library was not found at C:\addin\libraries\StarterMaterials.adsklib. Reinstall the add-in, or set a material library in Settings.",
            MaterialLibraryMessages.Missing(Starter));
    }

    [Fact]
    public void Missing_Settings_NamesPathAndSettings()
    {
        Assert.Equal(
            @"The material library set in Settings was not found at D:\shared\Test.adsklib. Check the path in Settings.",
            MaterialLibraryMessages.Missing(FromSettings));
    }

    [Fact]
    public void OpenFailed_WithReason()
    {
        Assert.Equal(
            @"Inventor could not open the material library set in Settings at D:\shared\Test.adsklib (Unspecified error). Check the path in Settings.",
            MaterialLibraryMessages.OpenFailed(FromSettings, "Unspecified error."));
    }

    [Fact]
    public void OpenFailed_Starter_NoReason()
    {
        Assert.Equal(
            @"Inventor could not open the starter material library at C:\addin\libraries\StarterMaterials.adsklib. Reinstall the add-in, or set a material library in Settings.",
            MaterialLibraryMessages.OpenFailed(Starter, null));
    }

    [Fact]
    public void Empty_NamesWhichLibrary()
    {
        Assert.Equal(
            @"The material library set in Settings at D:\shared\Test.adsklib has no materials that could be read.",
            MaterialLibraryMessages.Empty(FromSettings));
        Assert.StartsWith("The starter material library at ", MaterialLibraryMessages.Empty(Starter));
    }

    private static readonly LibraryPath StarterFinishes = new(@"C:\addin\libraries\finishes.csv", IsStarter: true);
    private static readonly LibraryPath FinishesFromSettings = new(@"D:\shared\Finishes.csv", IsStarter: false);

    [Fact]
    public void AddinFolderUnknown_WithAndWithoutReason()
    {
        Assert.Equal(
            "The add-in's folder could not be found (No location), so the material library and finish list cannot be read.",
            MaterialLibraryMessages.AddinFolderUnknown("No location."));
        Assert.Equal(
            "The add-in's folder could not be found, so the material library and finish list cannot be read.",
            MaterialLibraryMessages.AddinFolderUnknown("  "));
    }

    [Fact]
    public void FinishListMissing_Starter_NamesPathAndStarter()
    {
        Assert.Equal(
            @"The starter finish list was not found at C:\addin\libraries\finishes.csv. Reinstall the add-in, or set a finish list in Settings.",
            MaterialLibraryMessages.FinishListMissing(StarterFinishes));
    }

    [Fact]
    public void FinishListMissing_Settings_NamesPathAndSettings()
    {
        Assert.Equal(
            @"The finish list set in Settings was not found at D:\shared\Finishes.csv. Check the path in Settings.",
            MaterialLibraryMessages.FinishListMissing(FinishesFromSettings));
    }

    [Fact]
    public void FinishListUnreadable_Settings_WithReason()
    {
        Assert.Equal(
            @"Could not read the finish list set in Settings at D:\shared\Finishes.csv (The file is in use). Close it in any other program, or check the path in Settings.",
            MaterialLibraryMessages.FinishListUnreadable(FinishesFromSettings, "The file is in use."));
    }

    [Fact]
    public void FinishListUnreadable_Starter_NoReason()
    {
        Assert.Equal(
            @"Could not read the starter finish list at C:\addin\libraries\finishes.csv. Reinstall the add-in, or set a finish list in Settings.",
            MaterialLibraryMessages.FinishListUnreadable(StarterFinishes, null));
    }

    [Fact]
    public void FinishListProblemLogLine_NamesFileAndLine()
    {
        Assert.Equal(
            @"Material and Finish: finish list D:\shared\Finishes.csv: Line 4: Kind ""Plate"" is not known; row left out.",
            MaterialLibraryMessages.FinishListProblemLogLine(
                FinishesFromSettings, new FinishListProblem(4, @"Kind ""Plate"" is not known; row left out.")));
        Assert.Equal(
            @"Material and Finish: finish list D:\shared\Finishes.csv: The finish list is empty.",
            MaterialLibraryMessages.FinishListProblemLogLine(
                FinishesFromSettings, new FinishListProblem(null, "The finish list is empty.")));
    }
}
