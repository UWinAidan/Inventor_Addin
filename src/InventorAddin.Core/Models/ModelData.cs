using System.Text.Json.Serialization;

namespace InventorAddin.Core.Models;

/// <summary>Kind of Inventor document, resolved from DocumentType + SubType.</summary>
public enum DocumentKind
{
    Unknown,
    Part,
    SheetMetalPart,
    Assembly,
    WeldmentAssembly,
    Drawing,
    Presentation,
}

/// <summary>
/// Data common to every Inventor document. Derived types add part/assembly/drawing specifics.
/// Lengths are stored in Inventor database units (cm, kg, radians) unless the name says otherwise;
/// the matching *Display strings are formatted in the document's display units.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(PartData), "part")]
[JsonDerivedType(typeof(AssemblyData), "assembly")]
[JsonDerivedType(typeof(DrawingData), "drawing")]
public abstract class ModelData
{
    public DocumentKind Kind { get; set; }
    public string FullFileName { get; set; } = "";
    public string FullDocumentName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string SubTypeId { get; set; } = "";
    public string InternalName { get; set; } = "";
    public string? ModelStateName { get; set; }
    public bool IsDirty { get; set; }
    public bool IsReadOnly { get; set; }

    /// <summary>Most-used iProperties pulled out for convenience.</summary>
    public CommonProperties Common { get; set; } = new();

    /// <summary>Every iProperty: property-set display name → property name → value.</summary>
    public Dictionary<string, Dictionary<string, object?>> Properties { get; set; } = new();

    public List<ParameterData> Parameters { get; set; } = new();
    public List<ILogicRuleData> ILogicRules { get; set; } = new();
    public List<string> ReferencedFiles { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

/// <summary>The iProperties used for part numbering and title blocks.</summary>
public class CommonProperties
{
    public string? PartNumber { get; set; }
    public string? StockNumber { get; set; }
    public string? Description { get; set; }
    public string? Revision { get; set; }
    public string? Title { get; set; }
    public string? Project { get; set; }
    public string? Designer { get; set; }
    public string? Engineer { get; set; }
    public string? Vendor { get; set; }
    public string? Material { get; set; }
    public string? CostCenter { get; set; }
    public string? Author { get; set; }
    public DateTime? CreationDate { get; set; }
}

public class ParameterData
{
    public string Name { get; set; } = "";
    /// <summary>Model, User, Reference, Derived, Table, Custom.</summary>
    public string ParameterType { get; set; } = "";
    public string Expression { get; set; } = "";
    public object? Value { get; set; }
    public string Units { get; set; } = "";
    public string? Comment { get; set; }
    public bool IsKey { get; set; }
    public bool ExposedAsProperty { get; set; }
}

public class ILogicRuleData
{
    public string Name { get; set; } = "";
    public bool IsActive { get; set; }
    public string? Text { get; set; }
}

public class Point3D
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
}

public class BoundingBox
{
    public Point3D Min { get; set; } = new();
    public Point3D Max { get; set; } = new();
    public double LengthX => Max.X - Min.X;
    public double LengthY => Max.Y - Min.Y;
    public double LengthZ => Max.Z - Min.Z;
}
