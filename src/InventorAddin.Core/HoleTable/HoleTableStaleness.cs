namespace InventorAddin.Core.HoleTable;

public enum HoleTableStalenessKind
{
    UpToDate,
    OutOfDate,

    /// <summary>The view could not be analysed, or has no single origin to measure from. Never read as up to date.</summary>
    CouldNotCheck,
}

/// <summary>What the on-open check found for one view's stored tables (task 171).</summary>
/// <param name="Moved">Holes whose XDIM or YDIM as drawn would now read differently. Always 0 when <paramref name="MovesNotChecked"/>.</param>
/// <param name="Added">Holes with no stored tag.</param>
/// <param name="Removed">Holes with a stored tag that are no longer in the view.</param>
/// <param name="DescriptionChanged">Whole groups that kept their letter with a new description, plus single holes that now get a different tag (retagged). A retagged hole that also moved counts here and in <paramref name="Moved"/>.</param>
/// <param name="LeftOut">Holes the whole view's analysis left out because they are not seen end on (not tied to any removed hole).</param>
/// <param name="MovesNotChecked">A version 1 state has no stored positions, so moves cannot be detected.</param>
/// <param name="Reason">Why the view cannot be checked; null otherwise.</param>
public sealed record HoleTableStaleness(
    HoleTableStalenessKind Kind,
    int Moved = 0,
    int Added = 0,
    int Removed = 0,
    int DescriptionChanged = 0,
    int LeftOut = 0,
    bool MovesNotChecked = false,
    string? Reason = null)
{
    public bool IsOutOfDate => Kind == HoleTableStalenessKind.OutOfDate;

    /// <summary>The reasons in words, for example <c>2 holes moved, 1 hole added.</c>; the failure reason for "could not be checked".</summary>
    public string Text => HoleTableMessages.StalenessText(this);

    /// <summary>
    /// Compares a stored state with a fresh analysis, through <see cref="HoleTablePlan.Refresh"/> (the refresh Update uses),
    /// so a view called up to date here is one Update would leave unchanged. Reads only; nothing is drawn.
    /// </summary>
    public static HoleTableStaleness Check(HoleTableState state, HoleTableViewAnalysis analysis)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(analysis);

        if (analysis.FailureMessage is not null)
        {
            return CouldNot(analysis.FailureMessage);
        }

        var origin = HoleTableOriginResult.From(analysis.Origins, analysis.OriginUnreadable);
        if (origin.IsStop)
        {
            return CouldNot(origin.StopMessage!);
        }

        HoleTableRefresh refresh;
        try
        {
            refresh = HoleTablePlan.Refresh(analysis.Holes, state.Edits, state.Tags, origin.Origin!.Value, analysis.ViewScale, analysis.DrawingUnits);
        }
        catch (Exception ex)
        {
            return CouldNot(ex.Message);
        }

        var added = refresh.AddedIds.ToHashSet(StringComparer.Ordinal);
        var newTags = refresh.Plan.ToStoredTags().ByHole;

        // A hole that was tagged and still is, but in another group or number: it no longer fits its old group.
        var retagged = newTags.Count(p =>
            !added.Contains(p.Key) && state.Tags.ByHole.TryGetValue(p.Key, out var was) && was != p.Value);

        var moved = 0;
        if (state.Drawn is not null)
        {
            foreach (var row in refresh.Plan.AllGroups.SelectMany(g => g.Rows))
            {
                if (state.Drawn.TryGetValue(row.Hole.Id, out var drawn) && (drawn.XText != row.XText || drawn.YText != row.YText))
                {
                    moved++;
                }
            }
        }

        var descriptions = refresh.DescriptionChanges.Count + retagged;
        var upToDate = moved == 0 && added.Count == 0 && refresh.GoneIds.Count == 0 && descriptions == 0;
        return new HoleTableStaleness(
            upToDate ? HoleTableStalenessKind.UpToDate : HoleTableStalenessKind.OutOfDate,
            moved, added.Count, refresh.GoneIds.Count, descriptions, analysis.LeftOutCount, state.Drawn is null);
    }

    public static HoleTableStaleness CouldNot(string reason) =>
        new(HoleTableStalenessKind.CouldNotCheck, Reason: string.IsNullOrWhiteSpace(reason) ? "The view could not be read." : reason);
}
