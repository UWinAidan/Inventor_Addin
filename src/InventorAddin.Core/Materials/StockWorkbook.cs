using InventorAddin.Core.Drawing;

namespace InventorAddin.Core.Materials;

/// <summary>
/// The stock sizes of one unit system, from the stock workbook: the listed plate thicknesses and round bar diameters (ascending, in mm
/// or inches), the machining allowance added on each side and the step width and length round up to (same units).
/// </summary>
public sealed record StockUnitData(
    IReadOnlyList<decimal> PlateThicknesses,
    IReadOnlyList<decimal> RoundDiameters,
    decimal AllowancePerSide,
    decimal Step);

/// <summary>A problem found reading the stock workbook, naming the tab and row when there are any.</summary>
public sealed record StockWorkbookProblem(string? Tab, int? Row, string Message)
{
    public override string ToString() => (Tab, Row) switch
    {
        ({ } tab, { } row) => $"{tab} tab, row {row}: {Message}",
        ({ } tab, null) => $"{tab} tab: {Message}",
        _ => Message,
    };
}

/// <summary>The stock workbook, <c>libraries/AWB_Stock.xlsx</c> (spec 05, "Added for M5", item 5).</summary>
public sealed record StockWorkbook(StockUnitData Metric, StockUnitData Imperial)
{
    /// <summary>Allowance per side when the Settings tab has no usable metric row: 1 mm.</summary>
    public const decimal DefaultMetricAllowanceMm = 1m;

    /// <summary>Width and length step when the Settings tab has no usable metric row: 5 mm.</summary>
    public const decimal DefaultMetricStepMm = 5m;

    /// <summary>Allowance per side when the Settings tab has no usable imperial row: 0.04 in.</summary>
    public const decimal DefaultImperialAllowanceIn = 0.04m;

    /// <summary>Width and length step when the Settings tab has no usable imperial row: 0.25 in.</summary>
    public const decimal DefaultImperialStepIn = 0.25m;

    public static StockWorkbook Empty { get; } = new(
        new StockUnitData([], [], DefaultMetricAllowanceMm, DefaultMetricStepMm),
        new StockUnitData([], [], DefaultImperialAllowanceIn, DefaultImperialStepIn));

    public StockUnitData For(PrintUnits units) => units == PrintUnits.Imperial ? Imperial : Metric;
}

/// <summary>What reading the stock workbook gave: what loaded and a problem for each row or tab left out.</summary>
public sealed record StockWorkbookReadResult(StockWorkbook Workbook, IReadOnlyList<StockWorkbookProblem> Problems)
{
    public bool HasProblems => Problems.Count > 0;

    /// <summary>Whether the workbook as a whole could not be used: the file is missing or unreadable, or a tab is absent.</summary>
    public bool CouldNotRead => Problems.Any(p => p.Tab == null && p.Row == null);
}
