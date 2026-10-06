namespace InventorAddin.Core.Models;

public class DrawingData : ModelData
{
    public List<SheetData> Sheets { get; set; } = new();

    /// <summary>Models shown on this drawing, extracted in full (only when requested).</summary>
    public List<ModelData> ReferencedModels { get; set; } = new();
}

public class SheetData
{
    public string Name { get; set; } = "";
    public bool IsActive { get; set; }
    public string? Size { get; set; }
    public string? Orientation { get; set; }
    public double WidthCm { get; set; }
    public double HeightCm { get; set; }
    public string? BorderName { get; set; }
    public TitleBlockData? TitleBlock { get; set; }
    public List<DrawingViewData> Views { get; set; } = new();
    public List<TableSummary> HoleTables { get; set; } = new();
    public List<TableSummary> PartsLists { get; set; } = new();
    public List<TableSummary> RevisionTables { get; set; } = new();
    public List<string> SketchedSymbols { get; set; } = new();
}

public class TitleBlockData
{
    public string Name { get; set; } = "";
    /// <summary>Label (text box text / prompt) → resolved text as shown on the sheet.</summary>
    public List<TitleBlockField> Fields { get; set; } = new();
}

public class TitleBlockField
{
    public string Source { get; set; } = "";
    public string Value { get; set; } = "";
    public bool IsPrompted { get; set; }
}

public class DrawingViewData
{
    public string Name { get; set; } = "";
    public string? ViewType { get; set; }
    public string? ViewStyle { get; set; }
    public string? Orientation { get; set; }
    public double Scale { get; set; }
    public string? ScaleString { get; set; }
    public string? ReferencedFile { get; set; }
    public string? ParentViewName { get; set; }
    public bool IsFlatPatternView { get; set; }
    public bool Suppressed { get; set; }
    /// <summary>Sheet position of view center (cm).</summary>
    public double CenterX { get; set; }
    public double CenterY { get; set; }
    public double WidthCm { get; set; }
    public double HeightCm { get; set; }
}

public class TableSummary
{
    public string? Title { get; set; }
    public string? StyleName { get; set; }
    public List<string> Columns { get; set; } = new();
    public List<List<string>> Rows { get; set; } = new();
}
