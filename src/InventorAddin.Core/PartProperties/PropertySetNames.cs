namespace InventorAddin.Core.PartProperties;

/// <summary>
/// Inventor's internal, language-independent property set names, spelled exactly as the add-in's
/// <c>PropertyReader</c> uses them. Display names differ by language; these do not.
/// </summary>
public static class PropertySetNames
{
    public const string Summary = "Inventor Summary Information";
    public const string DocumentSummary = "Inventor Document Summary Information";
    public const string DesignTracking = "Design Tracking Properties";

    /// <summary>The set that holds custom properties; the only one properties can be added to or removed from.</summary>
    public const string UserDefined = "Inventor User Defined Properties";
}
