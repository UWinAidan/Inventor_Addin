using InventorAddin.Core.Ribbon;
using InventorAddin.Core.ViewModels;

namespace InventorAddin.Core.Holes;

/// <summary>One panel of the Part ribbon as read from Inventor: its tab, its own id and its top-level controls.</summary>
/// <param name="TabId">The tab's internal name.</param>
/// <param name="TabName">The tab's display name, or null when unreadable.</param>
/// <param name="PanelId">The panel's internal name.</param>
/// <param name="Controls">The internal names of the panel's top-level controls, in order; null where unreadable.</param>
public sealed record RibbonPanelControls(string TabId, string? TabName, string PanelId, IReadOnlyList<string?> Controls);

/// <summary>
/// Where AWB Hole goes on the Sheet Metal tab (task 128), as <see cref="SheetMetalHolePanel.Find"/> decides it from
/// the Part ribbon read at run time.
/// </summary>
/// <param name="PanelId">The Sheet Metal tab's panel holding Inventor's Hole, or null when there is none.</param>
/// <param name="Message">The log line: every panel of the Part ribbon with a Hole command, and what was chosen.</param>
/// <param name="IsWarning">True when the line should be a warning: no panel found, or another Hole command there.</param>
public sealed record SheetMetalHolePanelChoice(string? PanelId, string Message, bool IsWarning);

/// <summary>
/// Finds the Sheet Metal tab's panel that holds Inventor's Hole (<see cref="HoleTakeoverRules.InventorHoleCommand"/>),
/// by the control's command rather than by a fixed panel name (task 128). The Flat Pattern tab is never chosen: it
/// keeps Inventor's Hole (Aidan, 2026-10-25).
/// </summary>
/// <remarks>
/// Spike 081 saw <c>PartHoleCmd</c> small on the Sheet Metal tab's <see cref="HoleTakeoverRules.SheetMetalPanelId"/>;
/// that panel is preferred when several panels on the tab hold it. The tab's internal name,
/// <see cref="HoleTakeoverRules.SheetMetalTabId"/>, is not confirmed by a spike; the log line lists every panel with a
/// Hole command, so a different name shows up there.
/// </remarks>
public static class SheetMetalHolePanel
{
    /// <summary>Chooses the panel from <paramref name="panels"/> (the whole Part ribbon) and says why, for the log.</summary>
    public static SheetMetalHolePanelChoice Find(IReadOnlyList<RibbonPanelControls> panels)
    {
        ArgumentNullException.ThrowIfNull(panels);

        var onSheetMetalTab = panels
            .Where(p => p is not null && IsSheetMetalTab(p.TabId) && Contains(p.Controls, HoleTakeoverRules.InventorHoleCommand))
            .ToList();

        var chosen = onSheetMetalTab.FirstOrDefault(p => string.Equals(p.PanelId, HoleTakeoverRules.SheetMetalPanelId, StringComparison.Ordinal))
            ?? onSheetMetalTab.FirstOrDefault();

        // Hole commands on the Sheet Metal tab other than Inventor's ribbon Hole: not taken over, so worth a warning.
        var otherHoles = panels
            .Where(p => p is not null && IsSheetMetalTab(p.TabId))
            .SelectMany(p => HoleCommands(p.Controls))
            .Where(c => !string.Equals(c, HoleTakeoverRules.InventorHoleCommand, StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var found = $"Hole takeover: Hole commands on the {HoleTakeoverRules.RibbonName} ribbon: {Describe(panels)}.";

        string decision;
        if (chosen is null)
        {
            decision =
                $" No panel on the Sheet Metal tab ({HoleTakeoverRules.SheetMetalTabId}) holds {HoleTakeoverRules.InventorHoleCommand}, " +
                "so Inventor's Hole there is left as it is.";
        }
        else
        {
            decision =
                $" On the Sheet Metal tab, {AwbHoleViewModel.CommandName} goes before {HoleTakeoverRules.InventorHoleCommand} on panel {chosen.PanelId}.";
        }

        if (otherHoles.Count > 0)
        {
            decision +=
                $" The Sheet Metal tab also has {string.Join(", ", otherHoles)}, which is not taken over; " +
                $"the {HoleTakeoverRules.Shortcut} shortcut is moved from {HoleTakeoverRules.InventorHoleCommand} only.";
        }

        return new SheetMetalHolePanelChoice(chosen?.PanelId, found + decision, chosen is null || otherHoles.Count > 0);
    }

    /// <summary>True for the Sheet Metal tab's internal name; the Flat Pattern tab and every other tab are false.</summary>
    public static bool IsSheetMetalTab(string? tabId) =>
        string.Equals(tabId?.Trim(), HoleTakeoverRules.SheetMetalTabId, StringComparison.Ordinal);

    /// <summary>
    /// Inventor's controls whose internal name names a hole command ("Hole" in it, any case), trimmed, in order. The
    /// add-in's own commands (AWB Hole, put there by the takeover) are left out.
    /// </summary>
    public static IEnumerable<string> HoleCommands(IReadOnlyList<string?> controls)
    {
        ArgumentNullException.ThrowIfNull(controls);
        return controls
            .Where(c => !string.IsNullOrWhiteSpace(c) && c.Contains("Hole", StringComparison.OrdinalIgnoreCase))
            .Select(c => c!.Trim())
            .Where(c => !c.StartsWith(OurCommandPrefix, StringComparison.Ordinal));
    }

    private const string OurCommandPrefix = Branding.IdPrefix + "_";

    private static bool Contains(IReadOnlyList<string?> controls, string command) =>
        controls.Any(c => string.Equals(c?.Trim(), command, StringComparison.Ordinal));

    private static string Describe(IReadOnlyList<RibbonPanelControls> panels)
    {
        var parts = panels
            .Where(p => p is not null && !string.Equals(p.TabId, RibbonIds.TabId, StringComparison.Ordinal))
            .Select(p => (Panel: p, Holes: HoleCommands(p.Controls).ToList()))
            .Where(x => x.Holes.Count > 0)
            .Select(x =>
                $"{x.Panel.TabId}{(string.IsNullOrWhiteSpace(x.Panel.TabName) ? "" : $" ({x.Panel.TabName.Trim()})")} / " +
                $"{x.Panel.PanelId}: {string.Join(", ", x.Holes)}")
            .ToList();

        return parts.Count == 0 ? "none" : string.Join("; ", parts);
    }
}
