using InventorAddin.Core.Holes;

namespace InventorAddin.Core.HoleTable;

/// <summary>
/// A drawing sheet in the wizard's Sheet list. The add-in chooses the <paramref name="Key"/>; it must stay the same when the
/// sheet is renamed or reordered. The window shows <paramref name="Name"/>.
/// </summary>
public sealed record HoleTableSheetInfo(string Key, string Name, bool IsActive);

/// <summary>
/// A drawing view in the wizard's View list. The add-in chooses the <paramref name="Key"/> (for example an attribute on the
/// DrawingView), because view names change on rename or reorder. The window shows <paramref name="Name"/>.
/// </summary>
public sealed record HoleTableViewInfo(string Key, string Name);

/// <summary>
/// What the add-in read from one drawing view. A failure leaves the other fields at their defaults.
/// </summary>
/// <param name="Holes">The holes seen end on in the view.</param>
/// <param name="Origins">Every ordinate origin found on the view, in sheet centimetres (task 157 decides what they mean).</param>
/// <param name="ViewScale">Model-to-sheet factor.</param>
/// <param name="DrawingUnits">The drawing's units.</param>
/// <param name="LeftOutCount">How many holes were left out because they are not seen end on (spec 04 question 16).</param>
/// <param name="FailureMessage">Why the view could not be analysed; null when it could.</param>
public sealed record HoleTableViewAnalysis(
    IReadOnlyList<HoleTableHole> Holes,
    IReadOnlyList<SheetPoint> Origins,
    double ViewScale,
    HoleUnits DrawingUnits,
    int LeftOutCount,
    string? FailureMessage = null)
{
    public static HoleTableViewAnalysis Failed(string message) =>
        new([], [], 1, HoleUnits.Metric, 0, message);
}

/// <summary>
/// The drawing, as the Hole Table Wizard's view-model sees it. The add-in implements it (a later task); tests use a fake.
/// Table methods return a plain-words result for the status line. Every call takes the sheet and view keys the source
/// itself gave out. The view-model catches any exception a call throws and shows a plain status message, so a source
/// need not guard its own calls.
/// </summary>
public interface IHoleTableSource
{
    string DrawingName { get; }

    IReadOnlyList<HoleTableSheetInfo> GetSheets();

    IReadOnlyList<HoleTableViewInfo> GetViews(string sheetKey);

    HoleTableViewAnalysis Analyze(string sheetKey, string viewKey);

    string GenerateTable(string sheetKey, string viewKey, HoleTableKind kind, HoleTablePlan plan);

    string CreateTables(string sheetKey, string viewKey, HoleTablePlan plan);

    string AlignTables(string sheetKey, string viewKey);
}
