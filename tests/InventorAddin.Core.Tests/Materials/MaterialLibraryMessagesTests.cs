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
}
