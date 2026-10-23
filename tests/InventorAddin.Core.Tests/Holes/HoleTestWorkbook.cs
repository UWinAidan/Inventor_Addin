using InventorAddin.Core.Holes;

namespace InventorAddin.Core.Tests.Holes;

/// <summary>An invented hole workbook for the rules tests. Sizes and values are made up.</summary>
internal static class HoleTestWorkbook
{
    public static HoleWorkbook Create() => new(
        HoleSettings.Starter,
        Clearance:
        [
            new ClearanceRow(5, HoleUnits.Metric, "T5", 5m, 5.3m, 5.5m, 5.8m, false),
            new ClearanceRow(6, HoleUnits.Metric, "T9", 9m, 9.4m, 9.8m, 10.2m, true),
            new ClearanceRow(7, HoleUnits.Imperial, "#7", 0.15m, 0.16m, 0.17m, 0.18m, false),
            new ClearanceRow(8, HoleUnits.Metric, "TBIG", 10m, 10m, 12m, 14m, false),
        ],
        Counterbore:
        [
            new CounterboreRow(5, HoleUnits.Metric, "T5", "TCAP", 9.5m, 5.2m, ClearanceFit.Normal, false),
            new CounterboreRow(6, HoleUnits.Metric, "T5", "TLOW", 8.5m, 3.1m, ClearanceFit.Close, false),
            new CounterboreRow(7, HoleUnits.Imperial, "#7", "TCAP", 0.28m, 0.16m, ClearanceFit.Loose, false),
            new CounterboreRow(8, HoleUnits.Metric, "TNONE", "TCAP", 6m, 3m, ClearanceFit.Normal, false),
            new CounterboreRow(9, HoleUnits.Metric, "TBIG", "TCAP", 13m, 6m, ClearanceFit.Loose, false),
        ],
        Countersink:
        [
            new CountersinkRow(5, HoleUnits.Metric, "T5", "TFLAT", 10.4m, 90m, ClearanceFit.Loose, false),
            new CountersinkRow(6, HoleUnits.Imperial, "#7", "TFLAT", 0.3m, 82m, ClearanceFit.Normal, false),
        ],
        Tapped:
        [
            new TappedRow(5, HoleUnits.Metric, "M5", 5m, 0.8m, 4.2m, "6H", 7.5m, 10m, false),
            new TappedRow(6, HoleUnits.Imperial, "#7-36 UNF", 0.15m, 36m, 0.12m, "2B", 0.225m, 0.3m, false),
        ],
        Dowel:
        [
            new DowelRow(5, HoleUnits.Metric, "7 mm", 7m, false),
            new DowelRow(6, HoleUnits.Metric, "18 mm", 18m, false),
            new DowelRow(7, HoleUnits.Imperial, "1/7\"", 0.143m, false),
            new DowelRow(8, HoleUnits.Metric, "900 mm", 900m, false),
            new DowelRow(9, HoleUnits.Metric, "5 mm", 5m, false),
        ],
        PrecisionTolerances:
        [
            new PrecisionToleranceRow(5, HoleUnits.Metric, PrecisionFit.Normal, "Test bore", ToleranceKind.Iso, "H7", null, null, false, ""),
            new PrecisionToleranceRow(6, HoleUnits.Metric, PrecisionFit.Normal, "Test location", ToleranceKind.Iso, "G7", null, null, true, ""),
            new PrecisionToleranceRow(7, HoleUnits.Metric, PrecisionFit.Press, "Test press", ToleranceKind.Limits, null, 0m, -0.02m, true, ""),
            new PrecisionToleranceRow(8, HoleUnits.Imperial, PrecisionFit.Slip, "Test slip", ToleranceKind.Limits, null, 0.0005m, 0m, true, ""),
        ],
        DrillPoints:
        [
            new DrillPointRow(5, "118 deg", 118m, false, false, ""),
            new DrillPointRow(6, "90 deg", 90m, true, false, ""),
            new DrillPointRow(7, "Flat", null, false, false, ""),
        ]);

    public static HoleSpec Clearance(string size = "T5") => HoleSpec.ClearanceHole(HoleUnits.Metric, size);

    public static HoleSpec Tapped(string thread = "M5") => Clearance() with
    {
        Kind = HoleKind.Tapped,
        Size = thread,
        Termination = HoleTermination.Distance,
    };

    public static HoleSpec Dowel(string size = "7 mm") => Clearance() with
    {
        Kind = HoleKind.Dowel,
        Size = size,
    };

    /// <summary>A dowel with its own counterbore (task 106), sized as the T5 TCAP row so 5 mm reads back the same.</summary>
    public static HoleSpec CboredDowel(string size = "5 mm") => Dowel(size) with
    {
        Seat = HoleSeat.Counterbore,
        CboreDiameter = 9.5m,
        CboreDepth = 5.2m,
    };

    /// <summary>A dowel with its own countersink (task 106), sized as the T5 TFLAT row.</summary>
    public static HoleSpec CsunkDowel(string size = "5 mm") => Dowel(size) with
    {
        Seat = HoleSeat.Countersink,
        CsinkDiameter = 10.4m,
        CsinkAngleDeg = 90m,
    };
}
