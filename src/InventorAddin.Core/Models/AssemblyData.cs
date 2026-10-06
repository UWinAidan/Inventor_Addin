namespace InventorAddin.Core.Models;

public class AssemblyData : ModelData
{
    public string? BomStructure { get; set; }
    public PhysicalProperties? Physical { get; set; }
    public BoundingBox? RangeBox { get; set; }
    public List<FinishData> Finishes { get; set; } = new();

    /// <summary>Full occurrence tree as seen in the browser.</summary>
    public List<OccurrenceData> Occurrences { get; set; } = new();

    public List<BomRowData> StructuredBom { get; set; } = new();
    public List<BomRowData> PartsOnlyBom { get; set; } = new();
}

public class OccurrenceData
{
    public string Name { get; set; } = "";
    public string? FullFileName { get; set; }
    public string? PartNumber { get; set; }
    public string? Description { get; set; }
    public string? BomStructure { get; set; }
    public bool Suppressed { get; set; }
    public bool Visible { get; set; }
    public bool IsVirtual { get; set; }
    public bool IsPatternElement { get; set; }
    public List<OccurrenceData> Children { get; set; } = new();
}

public class BomRowData
{
    public string ItemNumber { get; set; } = "";
    public string? FullFileName { get; set; }
    public string? PartNumber { get; set; }
    public string? Description { get; set; }
    public string? Material { get; set; }
    public string? BomStructure { get; set; }
    public int ItemQuantity { get; set; }
    public string? TotalQuantity { get; set; }
    public bool Merged { get; set; }
    public List<BomRowData> Children { get; set; } = new();
}
