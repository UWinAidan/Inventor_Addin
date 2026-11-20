namespace InventorAddin.Core.HoleTable;

/// <summary>
/// Update for a stored view that is not open in the Hole Table window: the same <see cref="HoleTablePlan.Refresh"/> and the
/// same <see cref="IHoleTableSource.UpdateTables"/>, using the edits and tags stored on the view. Used by the wizard (for
/// views it does not have open) and by the out-of-date prompt (task 175).
/// </summary>
public static class HoleTableStoredUpdate
{
    /// <summary>
    /// Updates one view. Returns whether it worked; <paramref name="text"/> is the result, or the reason it failed. Never
    /// throws. The add-in runs each call in its own transaction.
    /// </summary>
    public static bool Run(IHoleTableSource source, string sheet, string view, string sheetName, string viewName, out string text)
    {
        try
        {
            var state = source.ReadState(sheet, view);
            if (state is null)
            {
                text = HoleTableMessages.NoStoredTables;
                return false;
            }

            var analysis = source.Analyze(sheet, view);
            if (analysis.FailureMessage is not null)
            {
                text = analysis.FailureMessage;
                return false;
            }

            var origin = HoleTableOriginResult.From(analysis.Origins, analysis.OriginUnreadable);
            if (origin.IsStop)
            {
                text = origin.StopMessage!;
                return false;
            }

            var refresh = HoleTablePlan.Refresh(analysis.Holes, state.Edits, state.Tags, origin.Origin!.Value, analysis.ViewScale, analysis.DrawingUnits);
            var next = HoleTableState.From(sheetName, viewName, refresh.Edits, refresh.Plan);
            var result = source.UpdateTables(sheet, view, refresh.Plan, next);
            text = $"{result} {refresh.Summary}";
            return true;
        }
        catch (Exception ex)
        {
            text = HoleTableStoredCheck.FailureText("update the tables", ex);
            return false;
        }
    }
}
