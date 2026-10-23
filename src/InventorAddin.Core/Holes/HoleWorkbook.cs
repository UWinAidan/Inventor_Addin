namespace InventorAddin.Core.Holes;

/// <summary>
/// One thing wrong with the hole workbook. <see cref="Tab"/> and <see cref="Row"/> (1-based worksheet row) are null
/// when the problem is with the file as a whole.
/// </summary>
public sealed record HoleWorkbookProblem(string? Tab, int? Row, string Message)
{
    public override string ToString() => (Tab, Row) switch
    {
        ({ } tab, { } row) => $"{tab} tab, row {row}: {Message}",
        ({ } tab, null) => $"{tab} tab: {Message}",
        _ => Message,
    };
}

/// <summary>
/// Everything read from the hole workbook (spec 09, "The workbook"), each tab's rows in workbook order. Rows with
/// <c>Show = N</c> are kept with <c>Hidden</c> set; rows with problems are left out.
/// </summary>
public sealed record HoleWorkbook(
    HoleSettings Settings,
    IReadOnlyList<ClearanceRow> Clearance,
    IReadOnlyList<CounterboreRow> Counterbore,
    IReadOnlyList<CountersinkRow> Countersink,
    IReadOnlyList<TappedRow> Tapped,
    IReadOnlyList<DowelRow> Dowel,
    IReadOnlyList<PrecisionToleranceRow> PrecisionTolerances,
    IReadOnlyList<DrillPointRow> DrillPoints)
{
    /// <summary>A workbook with no rows and the starter settings, for a file that could not be read.</summary>
    public static HoleWorkbook Empty { get; } = new(HoleSettings.Starter, [], [], [], [], [], [], []);
}

/// <summary>What <see cref="HoleWorkbookReader"/> found: the workbook and the problems, in the order found.</summary>
public sealed record HoleWorkbookReadResult(HoleWorkbook Workbook, IReadOnlyList<HoleWorkbookProblem> Problems)
{
    public bool HasProblems => Problems.Count > 0;
}
