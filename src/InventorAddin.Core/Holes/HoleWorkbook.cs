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
/// <c>Show = N</c> are kept with <c>Hidden</c> set; rows with problems are left out. The Spotface and Taper Tapped
/// tabs (task 111) are optional: a workbook without them has no rows for them, no problem, and names them in
/// <see cref="MissingOptionalTabs"/>.
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

    /// <summary>The Spotface tab's rows, or none when the workbook has no Spotface tab.</summary>
    public IReadOnlyList<SpotfaceRow> Spotface { get; init; } = [];

    /// <summary>The Taper Tapped tab's rows, or none when the workbook has no Taper Tapped tab.</summary>
    public IReadOnlyList<TaperTappedRow> TaperTapped { get; init; } = [];

    /// <summary>
    /// The optional tabs (<see cref="HoleWorkbookReader.OptionalTabs"/>) the workbook does not have, in that order,
    /// so the window can say why a list is empty (<see cref="HoleWorkbookMessages.MissingOptionalTab"/>). A tab that is
    /// present but left out for a missing header is not listed here; it is reported as a problem.
    /// </summary>
    public IReadOnlyList<string> MissingOptionalTabs { get; init; } = [];
}

/// <summary>What <see cref="HoleWorkbookReader"/> found: the workbook and the problems, in the order found.</summary>
public sealed record HoleWorkbookReadResult(HoleWorkbook Workbook, IReadOnlyList<HoleWorkbookProblem> Problems)
{
    public bool HasProblems => Problems.Count > 0;
}
