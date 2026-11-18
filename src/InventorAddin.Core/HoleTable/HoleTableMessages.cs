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

    /// <summary>Placing and Align need a title block or a border to stack on.</summary>
    public const string NoTitleBlockOrBorder =
        "This sheet has no title block and no border, so there is nothing to stack the hole tables on.";

    /// <summary>Align found no hole tables for the view.</summary>
    public const string NoTablesToAlign = "This view has no hole tables on the sheet to align.";

    public static string Drawn(IReadOnlyList<HoleTableKind> kinds, string viewName)
    {
        if (kinds.Count == 0)
        {
            return "There are no holes to put in a table.";
        }

        var names = string.Join(" and ", kinds.Select(k => k == HoleTableKind.Precision ? "Precision" : "Clearance"));
        return $"Drew the {names} {(kinds.Count == 1 ? "table" : "tables")} for {viewName}.";
    }

    public static string HeightUnreadable(HoleTableKind kind) =>
        $"The {kind} table's height could not be read. Use Create Tables to draw it again.";

    /// <summary>The temporary hole table that gives the style could not be made or read.</summary>
    public const string StyleUnreadable =
        "The drawing's hole table style could not be read, so no table was drawn. Check that the view can take a hole table.";

    /// <summary>A state is stored on the view but cannot be read; shown after "Could not read the stored tables: " so it is never taken for "no stored tables".</summary>
    public static string StoredStateUnreadable(string? reason) =>
        string.IsNullOrWhiteSpace(reason) ? "The stored hole table data could not be read." : reason;

    public static string Aligned(int count) => count == 1 ? "Aligned 1 table." : $"Aligned {count} tables.";

    private static string Count(int n, string noun) => n == 1 ? $"1 {noun}" : $"{n} {noun}s";
}
