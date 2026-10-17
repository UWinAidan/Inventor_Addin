using InventorAddin.Core.Holes;

namespace InventorAddin.Core.Tests.ViewModels;

/// <summary>
/// An invented hole workbook for the AWB Hole view-model tests, with hidden rows, a repeated size and
/// several Default = Y rows. Sizes and values are made up.
/// </summary>
internal static class AwbHoleTestWorkbook
{
    public static HoleWorkbook Create(HoleUnits defaultUnits = HoleUnits.Metric) => new(
        HoleSettings.Starter with { DefaultUnits = defaultUnits },
        Clearance:
        [
            new ClearanceRow(5, HoleUnits.Metric, "T3", 3m, 3.2m, 3.4m, 3.6m, false),
            new ClearanceRow(6, HoleUnits.Metric, "T4", 4m, 4.2m, 4.4m, 4.6m, true),
            new ClearanceRow(7, HoleUnits.Metric, "T5", 5m, 5.3m, 5.5m, 5.8m, false),
            new ClearanceRow(8, HoleUnits.Metric, "t5", 5m, 9m, 9m, 9m, false),
            new ClearanceRow(9, HoleUnits.Imperial, "#3", 0.12m, 0.125m, 0.13m, 0.14m, false),
            new ClearanceRow(10, HoleUnits.Imperial, "#5", 0.2m, 0.21m, 0.22m, 0.23m, false),
        ],
        Counterbore:
        [
            new CounterboreRow(5, HoleUnits.Metric, "T3", "TCAP", 6m, 3m, ClearanceFit.Normal, false),
            new CounterboreRow(6, HoleUnits.Metric, "T5", "TCAP", 9.5m, 5m, ClearanceFit.Normal, false),
            new CounterboreRow(7, HoleUnits.Metric, "T5", "TLOW", 8.5m, 3m, ClearanceFit.Close, false),
            new CounterboreRow(8, HoleUnits.Metric, "T5", "THID", 8m, 2m, ClearanceFit.Close, true),
            new CounterboreRow(9, HoleUnits.Imperial, "#5", "TCAP", 0.35m, 0.2m, ClearanceFit.Normal, false),
        ],
        Countersink:
        [
            new CountersinkRow(5, HoleUnits.Metric, "T5", "TFLAT", 10.4m, 90m, ClearanceFit.Loose, false),
            new CountersinkRow(6, HoleUnits.Imperial, "#5", "TFLAT", 0.4m, 82m, ClearanceFit.Normal, false),
        ],
        Tapped:
        [
            new TappedRow(5, HoleUnits.Metric, "M3", 3m, 0.5m, 2.5m, "6H", 4.5m, 6m, false),
            new TappedRow(6, HoleUnits.Metric, "M4", 4m, 0.7m, 3.3m, "6H", 6m, 8m, true),
            new TappedRow(7, HoleUnits.Metric, "M5", 5m, 0.8m, 4.2m, "6H", 7.5m, 10m, false),
            new TappedRow(8, HoleUnits.Imperial, "#5-40 UNC", 0.125m, 40m, 0.1015m, "2B", 0.19m, 0.25m, false),
        ],
        Dowel:
        [
            new DowelRow(5, HoleUnits.Metric, "3 mm", 3m, false),
            new DowelRow(6, HoleUnits.Metric, "5 mm", 5m, false),
            new DowelRow(7, HoleUnits.Metric, "6 mm", 6m, true),
            new DowelRow(8, HoleUnits.Imperial, "1/8\"", 0.125m, false),
        ],
        PrecisionTolerances:
        [
            new PrecisionToleranceRow(5, HoleUnits.Metric, PrecisionFit.Normal, "N bore", ToleranceKind.Iso, "H7", null, null, false, ""),
            new PrecisionToleranceRow(6, HoleUnits.Metric, PrecisionFit.Normal, "N loc", ToleranceKind.Iso, "G7", null, null, true, ""),
            new PrecisionToleranceRow(7, HoleUnits.Metric, PrecisionFit.Normal, "N second", ToleranceKind.Iso, "H8", null, null, true, ""),
            new PrecisionToleranceRow(8, HoleUnits.Metric, PrecisionFit.Slip, "S slip", ToleranceKind.Limits, null, 0.01m, 0m, true, ""),
            new PrecisionToleranceRow(9, HoleUnits.Metric, PrecisionFit.Press, "P press", ToleranceKind.Limits, null, 0m, -0.01m, true, ""),
            new PrecisionToleranceRow(10, HoleUnits.Imperial, PrecisionFit.Normal, "IN bore", ToleranceKind.Iso, "H7", null, null, true, ""),
        ],
        DrillPoints:
        [
            new DrillPointRow(5, "118 deg", 118m, true, false, ""),
            new DrillPointRow(6, "90 deg", 90m, true, false, ""),
            new DrillPointRow(7, "Flat", null, false, false, ""),
            new DrillPointRow(8, "60 deg", 60m, false, true, ""),
        ]);
}
