namespace InventorAddin.Core.Models;

public class PartData : ModelData
{
    public string? BomStructure { get; set; }
    public bool IsContentCenterPart { get; set; }

    public AssetInfo? Material { get; set; }
    public AssetInfo? Appearance { get; set; }
    public PhysicalProperties? Physical { get; set; }
    public BoundingBox? RangeBox { get; set; }

    public SheetMetalData? SheetMetal { get; set; }

    public List<HoleData> Holes { get; set; } = new();
    public List<FinishData> Finishes { get; set; } = new();

    /// <summary>Feature type (e.g. kExtrudeFeatureObject) → count.</summary>
    public Dictionary<string, int> FeatureCounts { get; set; } = new();
    public int SurfaceBodyCount { get; set; }
}

public class AssetInfo
{
    public string Name { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string? Category { get; set; }
    public string? LibraryName { get; set; }
}

public class PhysicalProperties
{
    public double MassKg { get; set; }
    public double VolumeCm3 { get; set; }
    public double AreaCm2 { get; set; }
    public double? DensityGPerCm3 { get; set; }
    public Point3D? CenterOfMass { get; set; }
    public string? MassDisplay { get; set; }
    public string? VolumeDisplay { get; set; }
    public string? AreaDisplay { get; set; }
}

public class SheetMetalData
{
    public string? StyleName { get; set; }
    public double ThicknessCm { get; set; }
    public string? ThicknessDisplay { get; set; }
    public bool HasFlatPattern { get; set; }
    public double? FlatLengthCm { get; set; }
    public double? FlatWidthCm { get; set; }
    public string? FlatLengthDisplay { get; set; }
    public string? FlatWidthDisplay { get; set; }
    public int? BendCount { get; set; }
}

public class HoleData
{
    public string FeatureName { get; set; } = "";
    public bool Suppressed { get; set; }

    /// <summary>Drilled, Counterbore, Countersink, Spotface.</summary>
    public string HoleType { get; set; } = "";
    /// <summary>Distance, ThroughAll, To, etc.</summary>
    public string? Termination { get; set; }

    public double? DiameterCm { get; set; }
    public double? DepthCm { get; set; }
    public string? DiameterDisplay { get; set; }
    public string? DepthDisplay { get; set; }

    public double? CBoreDiameterCm { get; set; }
    public double? CBoreDepthCm { get; set; }
    public double? CSinkDiameterCm { get; set; }
    public double? CSinkAngleRad { get; set; }
    public double? SpotFaceDiameterCm { get; set; }
    public double? SpotFaceDepthCm { get; set; }

    public bool Tapped { get; set; }
    public ThreadData? Thread { get; set; }

    /// <summary>Hole centers in model space (cm). One entry per instance of the feature.</summary>
    public List<Point3D> Centers { get; set; } = new();
}

public class ThreadData
{
    public string? ThreadType { get; set; }
    public string? Designation { get; set; }
    public string? NominalSize { get; set; }
    public string? ThreadClass { get; set; }
    public bool? RightHanded { get; set; }
    public bool? FullDepth { get; set; }
    public double? ThreadDepthCm { get; set; }
}

public class FinishData
{
    public string FeatureName { get; set; } = "";
    public bool Suppressed { get; set; }
    public string? FinishType { get; set; }
    public string? ProcessName { get; set; }
    public string? AppearanceName { get; set; }
    public string? Comments { get; set; }
}
