using InventorAddin.Core.ViewModels;

namespace InventorAddin.Core.HoleTable;

/// <summary>What <see cref="HoleTableStoredCheck.Run"/> found: the views out of date, and the ones that could not be read.</summary>
public sealed record HoleTableStoredCheckResult(
    IReadOnlyList<HoleTableStoredViewCheck> OutOfDate,
    IReadOnlyList<HoleTableStoredViewCheck> CouldNotCheck);

/// <summary>
/// The check of stored hole tables (task 171), shared by the wizard's window and the automatic check (task 175): for
/// every sheet and every view with a stored state, reads the view again and compares it with what was drawn.
/// </summary>
public static class HoleTableStoredCheck
{
    /// <summary>
    /// Only <see cref="IHoleTableSource.GetKeyedViews"/>, <see cref="IHoleTableSource.ReadState"/> and
    /// <see cref="IHoleTableSource.Analyze"/> are called; no table or state is written. A view that cannot be read is
    /// listed as could not be checked, never as up to date.
    /// </summary>
    public static HoleTableStoredCheckResult Run(IHoleTableSource source, IEnumerable<HoleTableSheetInfo> sheets)
    {
        var outOfDate = new List<HoleTableStoredViewCheck>();
        var couldNot = new List<HoleTableStoredViewCheck>();
        foreach (var sheet in sheets)
        {
            IReadOnlyList<HoleTableViewInfo> views;
            try
            {
                views = source.GetKeyedViews(sheet.Key);
            }
            catch (Exception ex)
            {
                couldNot.Add(new(sheet.Key, sheet.Name, "", "All views", FailureText("list the views", ex)));
                continue;
            }

            foreach (var view in views)
            {
                HoleTableStaleness result;
                try
                {
                    var state = source.ReadState(sheet.Key, view.Key);
                    if (state is null)
                    {
                        continue;
                    }

                    result = HoleTableStaleness.Check(state, source.Analyze(sheet.Key, view.Key));
                }
                catch (Exception ex)
                {
                    result = HoleTableStaleness.CouldNot(FailureText("check the stored tables", ex));
                }

                var item = new HoleTableStoredViewCheck(sheet.Key, sheet.Name, view.Key, view.Name, result.Text);
                if (result.Kind == HoleTableStalenessKind.OutOfDate)
                {
                    outOfDate.Add(item);
                }
                else if (result.Kind == HoleTableStalenessKind.CouldNotCheck)
                {
                    couldNot.Add(item);
                }
            }
        }

        return new(outOfDate, couldNot);
    }

    internal static string FailureText(string what, Exception ex) => $"Could not {what}: {ex.Message}";
}
