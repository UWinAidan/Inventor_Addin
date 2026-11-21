using InventorAddin.Core.Holes;

namespace InventorAddin.Core.HoleTable;

/// <summary>Decisions about a drawing view that the add-in's reading of Inventor feeds (task 163).</summary>
public static class HoleTableViewRules
{
    /// <summary>A hole whose axis and the view direction have |dot| above this is seen end on (spec 04 question 16).</summary>
    public const double EndOnDot = 0.999;

    /// <summary>
    /// True when the hole is seen end on: its axis is parallel to the view direction, in either sense. Both vectors
    /// are unit vectors.
    /// </summary>
    public static bool IsEndOn(double axisX, double axisY, double axisZ, double viewX, double viewY, double viewZ) =>
        Math.Abs(axisX * viewX + axisY * viewY + axisZ * viewZ) > EndOnDot;

    /// <summary>
    /// The hole units for a drawing's length unit, by the name of Inventor's <c>UnitsTypeEnum</c> member. Inch, foot,
    /// mil, yard and mile are imperial; everything else (including a name that is not known) is metric.
    /// </summary>
    public static HoleUnits UnitsFor(string? lengthUnitName) => lengthUnitName switch
    {
        "kInchLengthUnits" or "kFootLengthUnits" or "kMilLengthUnits" or "kYardLengthUnits" or "kMileLengthUnits" => HoleUnits.Imperial,
        _ => HoleUnits.Metric,
    };
}
