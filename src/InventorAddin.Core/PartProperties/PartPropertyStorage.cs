namespace InventorAddin.Core.PartProperties;

/// <summary>The type of value a property holds, so the writer knows what to pass to Inventor.</summary>
public enum PropertyValueKind
{
    Text,

    /// <summary>A COM currency value, carried as a <see cref="decimal"/>.</summary>
    Currency,

    /// <summary>A whole number, carried as an <see cref="int"/> (Design Status).</summary>
    Integer,
}

/// <summary>Where one field is stored: a property set (internal name), a property name and a value type.</summary>
public sealed record PropertyLocation(string SetName, string PropertyName, PropertyValueKind Kind)
{
    /// <summary>
    /// True for a custom property (the user-defined set). Only custom properties can be added or removed;
    /// standard ones always exist.
    /// </summary>
    public bool IsCustom => SetName == PropertySetNames.UserDefined;
}

/// <summary>
/// Where each stored Part Properties field lives (spec 07, "Where the data lives"). Standard properties are
/// used wherever Inventor has one. Material is the document's material and weight is not stored, so neither
/// has a location here.
/// </summary>
public static class PartPropertyStorage
{
    public static readonly PropertyLocation PartNumber =
        new(PropertySetNames.DesignTracking, "Part Number", PropertyValueKind.Text);

    /// <summary>Kept equal to the part name (the file name without its extension).</summary>
    public static readonly PropertyLocation Description =
        new(PropertySetNames.DesignTracking, "Description", PropertyValueKind.Text);

    /// <summary>Holds the short code from <see cref="PartTypes"/>.</summary>
    public static readonly PropertyLocation PartType =
        new(PropertySetNames.UserDefined, "Part Type", PropertyValueKind.Text);

    public static readonly PropertyLocation Designer =
        new(PropertySetNames.DesignTracking, "Designer", PropertyValueKind.Text);

    /// <summary>
    /// The standard Author property. Kept equal to Designer (spec 02, Aidan, 2026-10-11): Inventor fills it with the
    /// Windows user name on every new file. Not shown in Part Properties.
    /// </summary>
    public static readonly PropertyLocation Author =
        new(PropertySetNames.Summary, "Author", PropertyValueKind.Text);

    public static readonly PropertyLocation Detailer =
        new(PropertySetNames.UserDefined, "Detailer", PropertyValueKind.Text);

    /// <summary>Shown in Inventor as Estimated Cost.</summary>
    public static readonly PropertyLocation Cost =
        new(PropertySetNames.DesignTracking, "Cost", PropertyValueKind.Currency);

    /// <summary>The finish's name (spec 03).</summary>
    public static readonly PropertyLocation Finish =
        new(PropertySetNames.UserDefined, "Finish", PropertyValueKind.Text);

    /// <summary>The finish's short code (spec 03).</summary>
    public static readonly PropertyLocation FinishCode =
        new(PropertySetNames.UserDefined, "Finish Code", PropertyValueKind.Text);

    /// <summary>The finish's thickness range in mm, as text (spec 03).</summary>
    public static readonly PropertyLocation FinishThickness =
        new(PropertySetNames.UserDefined, "Finish Thickness", PropertyValueKind.Text);

    /// <summary>The finish's print note for the drawing (spec 03).</summary>
    public static readonly PropertyLocation FinishNote =
        new(PropertySetNames.UserDefined, "Finish Note", PropertyValueKind.Text);

    /// <summary>
    /// The finish name and thickness as one line for the title block, such as "Test Coat, 0.010-0.015 mm" (spec 05,
    /// "Finish on the print"). Written with the finish so the print reads one property.
    /// </summary>
    public static readonly PropertyLocation FinishSpec =
        new(PropertySetNames.UserDefined, "Finish Spec", PropertyValueKind.Text);

    /// <summary>Summary Information "Revision Number" (PropertyID 9), the print's REV field (task 219).</summary>
    public static readonly PropertyLocation RevisionNumber =
        new(PropertySetNames.Summary, "Revision Number", PropertyValueKind.Text);

    /// <summary>
    /// Design Tracking "Design Status" (PropertyID 40): a whole number, see <see cref="PartStatus"/>. The name is the
    /// one the API reference gives for id 40; not yet confirmed by a write in Inventor.
    /// </summary>
    public static readonly PropertyLocation DesignStatus =
        new(PropertySetNames.DesignTracking, "Design Status", PropertyValueKind.Integer);

    /// <summary>Design Tracking "User Status" (PropertyID 17), the iProperties Status tab's Status text (task 219).</summary>
    public static readonly PropertyLocation UserStatus =
        new(PropertySetNames.DesignTracking, "User Status", PropertyValueKind.Text);

    /// <summary>
    /// The custom text property "Mass" ("4.33 kg" or "9.55 lb"), written by the add-in on every save and read by both print
    /// templates' WEIGHT field (task 222; spike 225 showed the live mass field cannot print "lb").
    /// </summary>
    public static readonly PropertyLocation Mass =
        new(PropertySetNames.UserDefined, "Mass", PropertyValueKind.Text);

    /// <summary>
    /// The custom text property "Material Size" (capital S): for sheet metal the thickness and the flat pattern size, "1.5 x 60 x 120 mm"
    /// (task 230); the title block's MATERIAL SIZE field reads it.
    /// </summary>
    public static readonly PropertyLocation MaterialSize =
        new(PropertySetNames.UserDefined, "Material Size", PropertyValueKind.Text);
}
