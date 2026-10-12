using InventorAddin.Core.Logging;
using InventorAddin.Core.Ribbon;
using InventorAddin.Core.ViewModels;

namespace InventorAddin.Core.Tests;

public sealed class BrandingTests
{
    // The one place the tests spell out the brand. Other tests build names from Branding.
    [Fact]
    public void Names()
    {
        Assert.Equal("AWB Addin", Branding.TabName);
        Assert.Equal("AWB Addin", Branding.ProductName);
        Assert.Equal("AWB", Branding.ShortName);
        Assert.Equal("Awb", Branding.IdPrefix);
        Assert.Equal("AwbAddin", Branding.DataFolderName);
        Assert.Equal("awbaddin", Branding.LogFileBaseName);
        Assert.Equal("awbaddin.log", Branding.LogFileName);
    }

    [Fact]
    public void IdsSeenByInventor()
    {
        Assert.Equal("id_Tab_Awb", RibbonIds.TabId);
        Assert.Equal("id_Panel_Awb_Create", RibbonIds.CreatePanelId);
        Assert.Equal("id_Panel_Awb_Properties", RibbonIds.PropertiesPanelId);
        Assert.Equal("id_Panel_Awb_Drawing", RibbonIds.DrawingPanelId);
        Assert.Equal("id_Panel_Awb_Dev", RibbonIds.DeveloperPanelId);
        Assert.Equal("id_Panel_Awb_Awb", RibbonIds.AwbPanelId);
        Assert.Equal("Awb_ExportModelData", CommandNames.ExportModelData);
        Assert.Equal("Awb_ExportLibraries", CommandNames.ExportLibraries);
        Assert.Equal("Awb_Settings", CommandNames.Settings);
        Assert.Equal("Awb_About", CommandNames.About);
    }

    [Fact]
    public void UsersOfTheBrand()
    {
        Assert.Equal(Branding.TabName, RibbonIds.TabName);
        Assert.Equal(Branding.ShortName, RibbonIds.AwbPanelName);
        Assert.Equal(Branding.ProductName, AboutViewModel.TitleText);
        Assert.Equal(Branding.LogFileName, FileLog.FileName);
    }
}
