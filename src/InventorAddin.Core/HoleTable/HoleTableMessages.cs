namespace InventorAddin.Core.HoleTable;

/// <summary>The hole table wizard's user-facing texts, in one place. Later tasks add to it.</summary>
public static class HoleTableMessages
{
    /// <summary>Spec 04 question 7 (Aidan): the wizard never invents an origin.</summary>
    public const string NoOrdinateOrigin =
        "This view has no ordinate dimension origin. Add one to the view before the hole table wizard can measure the holes.";

    /// <summary>Spec 04 question 17 (Default).</summary>
    public const string MoreThanOneOrdinateOrigin =
        "This view has more than one ordinate dimension origin. Remove the extra origins so the view has only one, then run the wizard again.";

    /// <summary>Spec 04 question 16: holes not seen end on are left out.</summary>
    public static string HolesLeftOut(int count) => count == 1
        ? "1 hole was left out because it is not seen end on in this view."
        : $"{count} holes were left out because they are not seen end on in this view.";

    /// <summary>The counts Update reports, for the status line and the log.</summary>
    public static string RefreshSummary(HoleTableRefresh refresh)
    {
        var parts = new List<string>
        {
            Count(refresh.AddedIds.Count, "hole") + " added",
            Count(refresh.GoneIds.Count, "hole") + " gone",
            Count(refresh.DescriptionChanges.Count, "description") + " changed",
        };
        return string.Join(", ", parts) + ".";
    }

    /// <summary>Edited holes that are no longer in the view (task 158's stale ids).</summary>
    public static string StaleIdsDropped(int count) => count == 1
        ? "1 edited hole is no longer in the view and was dropped."
        : $"{count} edited holes are no longer in the view and were dropped.";

    private static string Count(int n, string noun) => n == 1 ? $"1 {noun}" : $"{n} {noun}s";
}
