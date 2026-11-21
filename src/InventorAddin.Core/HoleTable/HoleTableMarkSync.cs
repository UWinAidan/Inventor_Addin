namespace InventorAddin.Core.HoleTable;

/// <summary>A hole the plan wants marked: its id, its tag text now, and whether it sits in the Precision table.</summary>
public sealed record HoleMarkWanted(string HoleId, string Tag, bool PrecisionMark);

/// <summary>What Update must do to the marks on a view: the holes to mark again, and the holes whose marks go.</summary>
public sealed record HoleMarkSyncPlan(IReadOnlyList<string> Place, IReadOnlyList<string> Remove);

/// <summary>
/// Decides which holes' tags and marks Update touches (task 166). A hole is marked again when it is new, when its tag
/// text changed (Renumber, a swap, a split), when one of its marks is missing, or when it gained or lost the precision
/// mark. The marks of a hole that is no longer in the plan are removed. Every other hole is left as it is.
/// </summary>
public static class HoleTableMarkSync
{
    /// <param name="wanted">The holes in the refreshed plan.</param>
    /// <param name="previousTags">Each hole's tag text when the marks were last placed (the stored state); null when unknown, so every hole is marked again.</param>
    /// <param name="existingRoles">The roles of the marks now on the view for each hole id (<c>HoleTableMarks.TagRole</c> and so on).</param>
    public static HoleMarkSyncPlan Decide(
        IEnumerable<HoleMarkWanted> wanted,
        IReadOnlyDictionary<string, string>? previousTags,
        IReadOnlyDictionary<string, IReadOnlySet<string>> existingRoles)
    {
        ArgumentNullException.ThrowIfNull(wanted);
        ArgumentNullException.ThrowIfNull(existingRoles);

        var list = wanted.ToList();
        var place = new List<string>();
        foreach (var hole in list)
        {
            existingRoles.TryGetValue(hole.HoleId, out var roles);
            var tagChanged = previousTags is null
                || !previousTags.TryGetValue(hole.HoleId, out var before)
                || before != hole.Tag;
            var complete = roles is not null
                && roles.Contains(HoleTableMarks.TagRole)
                && roles.Contains(HoleTableMarks.CentreMarkRole)
                && roles.Contains(HoleTableMarks.PrecisionMarkRole) == hole.PrecisionMark;
            if (tagChanged || !complete)
            {
                place.Add(hole.HoleId);
            }
        }

        var ids = list.Select(h => h.HoleId).ToHashSet(StringComparer.Ordinal);
        var remove = existingRoles.Keys.Where(id => !ids.Contains(id)).OrderBy(id => id, StringComparer.Ordinal).ToList();
        return new HoleMarkSyncPlan(place, remove);
    }

    /// <summary>The Update status line for the marks: what was placed again and what was removed.</summary>
    public static string Summary(int placed, int skipped, int removed)
    {
        var parts = new List<string>();
        var marks = HoleTableMarks.Summary(placed, skipped);
        if (marks.Length > 0)
        {
            parts.Add(marks);
        }

        if (removed > 0)
        {
            parts.Add(removed == 1 ? "Removed the marks of 1 hole that is gone." : $"Removed the marks of {removed} holes that are gone.");
        }

        return string.Join(" ", parts);
    }
}
