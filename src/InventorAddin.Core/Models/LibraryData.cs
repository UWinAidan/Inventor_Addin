namespace InventorAddin.Core.Models;

/// <summary>Material and appearance libraries available to Inventor (basis for the material/finish library tool).</summary>
public class LibraryData
{
    public string? ActiveMaterialLibrary { get; set; }
    public string? ActiveAppearanceLibrary { get; set; }
    public List<AssetLibraryData> Libraries { get; set; } = new();
}

public class AssetLibraryData
{
    public string DisplayName { get; set; } = "";
    public string InternalName { get; set; } = "";
    public string? FullFileName { get; set; }
    public bool IsReadOnly { get; set; }
    public List<AssetInfo> Materials { get; set; } = new();
    public List<AssetInfo> Appearances { get; set; } = new();
}
