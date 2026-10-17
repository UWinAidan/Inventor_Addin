namespace InventorAddin.Core.PartProperties;

/// <summary>The type of value a property holds, so the writer knows what to pass to Inventor.</summary>
public enum PropertyValueKind
{
    Text,

    /// <summary>A COM currency value, carried as a <see cref="decimal"/>.</summary>
    Currency,
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
}
