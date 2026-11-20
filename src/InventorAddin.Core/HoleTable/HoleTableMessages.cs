using InventorAddin.Core.ViewModels;

namespace InventorAddin.Core.HoleTable;

/// <summary>The hole table wizard's user-facing texts, in one place. Later tasks add to it.</summary>
public static class HoleTableMessages
{
    /// <summary>Spec 04 question 7 (Aidan): the wizard never invents an origin.</summary>
    public const string NoOrdinateOrigin =
        "This view has no ordinate dimension origin. Add one to the view before the hole table wizard can measure the holes.";

    /// <summary>Task 168: the view has an origin or ordinates, but no point could be read from them.</summary>
    public const string OrdinateOriginUnreadable =
        "This view has an ordinate origin, but it could not be read. Delete the view's ordinate dimensions and add them again, then run the wizard again.";

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

    /// <summary>Heading above the list of views whose tables no longer match the model (task 171).</summary>
    public const string OutOfDateHeading = "These views' hole tables are out of date:";

    /// <summary>Heading above the list of views the check could not read.</summary>
    public const string CouldNotCheckHeading = "These views' hole tables could not be checked:";

    /// <summary>One view's reason line, for example <c>2 holes moved, 1 hole added.</c></summary>
    public static string StalenessText(HoleTableStaleness s)
    {
        if (s.Kind == HoleTableStalenessKind.CouldNotCheck)
        {
            return s.Reason ?? "The view could not be read.";
        }

        var parts = new List<string>();
        if (s.Moved > 0) parts.Add(Count(s.Moved, "hole") + " moved");
        if (s.Added > 0) parts.Add(Count(s.Added, "hole") + " added");
        if (s.Removed > 0) parts.Add(Count(s.Removed, "hole") + " removed");

        if (s.DescriptionChanged > 0) parts.Add(Count(s.DescriptionChanged, "description") + " changed");
        var text = parts.Count == 0 ? "Up to date." : string.Join(", ", parts) + ".";
        if (s.Removed > 0 && s.LeftOut > 0)
        {
            // The left-out count is for the whole view, so state it as a fact beside the removals, not as their cause.
            text += " " + HolesLeftOut(s.LeftOut);
        }

        return s.MovesNotChecked ? text + " Moves are not checked for tables stored by an older version." : text;
    }

    /// <summary>A view's line in the list: <c>VIEW1 on Sheet:1: 2 holes moved.</c></summary>
    public static string ViewLine(string viewName, string sheetName, string reason) => $"{viewName} on {sheetName}: {reason}";

    /// <summary>A view has no stored hole tables to update.</summary>
    public const string NoStoredTables = "This view has no stored hole tables. Create them first.";

    /// <summary>Heading of the automatic out-of-date prompt (task 175).</summary>
    public static string OutOfDatePromptHeading(string drawingName) => $"Some hole tables in {drawingName} are out of date:";

    /// <summary>Heading of the prompt after Update now left some views not updated.</summary>
    public static string UpdateFailedHeading(string drawingName) => $"Some hole tables in {drawingName} could not be updated:";

    public const string UpdateNowButton = "Update now";

    public const string LaterButton = "Later";

    /// <summary>The Later button once the prompt is only reporting failures.</summary>
    public const string CloseButton = "Close";

    /// <summary>The prefix a failed update's reason gets.</summary>
    public const string UpdateFailedPrefix = "Update failed. ";

    /// <summary>
    /// The log line for one automatic check, for example
    /// <c>Hole table check: Plate.idw, 1 view out of date (VIEW1 on Sheet:1: 2 holes moved.), 0.4 s</c>.
    /// </summary>
    public static string AutoCheckLogLine(string drawingName, HoleTableStoredCheckResult result, double seconds)
    {
        var text = new System.Text.StringBuilder($"Hole table check: {drawingName}, ");
        text.Append(result.OutOfDate.Count == 0
            ? "no views out of date"
            : $"{Count(result.OutOfDate.Count, "view")} out of date ({string.Join("; ", result.OutOfDate.Select(v => v.Text))})");
        if (result.CouldNotCheck.Count > 0)
        {
            text.Append($", {Count(result.CouldNotCheck.Count, "view")} could not be checked ({string.Join("; ", result.CouldNotCheck.Select(v => v.Text))})");
        }

        text.Append(string.Format(System.Globalization.CultureInfo.InvariantCulture, ", {0:0.0} s", seconds));
        return text.ToString();
    }

    private static string Count(int n, string noun) => n == 1 ? $"1 {noun}" : $"{n} {noun}s";
}
