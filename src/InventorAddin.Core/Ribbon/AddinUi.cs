using System.Globalization;
using InventorAddin.Core.Holes;

namespace InventorAddin.Core.Ribbon;

/// <summary>A button the add-in puts on one of Inventor's own ribbon panels, not on the add-in's tab.</summary>
/// <param name="RibbonName">The Inventor ribbon (the key into <c>UserInterfaceManager.Ribbons</c>).</param>
/// <param name="TabId">Inventor's tab, by internal name.</param>
/// <param name="PanelId">Inventor's panel on that tab, by internal name.</param>
/// <param name="CommandInternalName">The add-in command the button runs.</param>
public sealed record ForeignButton(string RibbonName, string TabId, string PanelId, string CommandInternalName);

/// <summary>
/// The add-in's controls in Inventor's ribbons: its tab on every ribbon and its buttons on Inventor's own panels.
/// Unloading the add-in removes them, and loading removes any an earlier load left before building, so a reload builds
/// the ribbon as a start does (task 121).
/// </summary>
/// <remarks>
/// The command definitions are not deleted: <c>Deactivate</c> runs at every Inventor exit, and deleting them each time
/// could drop the Quick Access Toolbar entries and keyboard shortcuts users give our commands. A reload reuses them.
/// </remarks>
public static class AddinUi
{
    /// <summary>
    /// The ribbons the add-in's tab (<see cref="RibbonIds.TabId"/>) is looked for on: every environment, whatever the
    /// layout or settings show now, since a setting changed since the last load may have put the tab on another ribbon.
    /// </summary>
    public static IReadOnlyList<string> TabRibbonNames { get; } =
        RibbonEnvironments.All.Select(e => e.RibbonName()).ToArray();

    /// <summary>AWB Hole on the 3D Model tab's Modify panel (task 091).</summary>
    public static IReadOnlyList<ForeignButton> ForeignButtons { get; } = new[]
    {
        new ForeignButton(HoleTakeoverRules.RibbonName, HoleTakeoverRules.TabId, HoleTakeoverRules.PanelId, CommandNames.AwbHole),
    };
}

/// <summary>When the add-in's user interface is removed.</summary>
public enum AddinUiOccasion
{
    /// <summary>Before the ribbon is built: anything an earlier load left is removed.</summary>
    Loading,

    /// <summary>The add-in is being unloaded.</summary>
    Unloading,
}

/// <summary>Tallies what removing the add-in's user interface removed, and what it could not, for the log.</summary>
public sealed class AddinUiRemovalReport
{
    private readonly List<string> _failures = new();

    public AddinUiRemovalReport(AddinUiOccasion occasion)
    {
        Occasion = occasion;
    }

    public AddinUiOccasion Occasion { get; }

    public int TabsRemoved { get; private set; }

    public int ButtonsRemoved { get; private set; }

    /// <summary>The things that could not be removed, each with Inventor's error.</summary>
    public IReadOnlyList<string> Failures => _failures;

    public void TabRemoved() => TabsRemoved++;

    public void ButtonRemoved() => ButtonsRemoved++;

    /// <param name="what">What could not be removed, for example "the AWB Addin tab on the Part ribbon".</param>
    /// <param name="error">Inventor's error message.</param>
    public void Failed(string what, string error) => _failures.Add($"{what}: {error}");

    private string Prefix => $"Ribbon ({AddinUiOccasions.Text(Occasion)})";

    /// <summary>The info line: what was removed, or that there was nothing to remove.</summary>
    public string Summary()
    {
        bool none = TabsRemoved == 0 && ButtonsRemoved == 0;
        string removed =
            $"{Count(TabsRemoved, "tab", "tabs")}, and {Count(ButtonsRemoved, "button", "buttons")} on Inventor's panels";

        return Occasion switch
        {
            AddinUiOccasion.Loading when none =>
                $"{Prefix}: nothing was left from an earlier load; the ribbon is built from scratch.",
            AddinUiOccasion.Loading =>
                $"{Prefix}: removed what an earlier load left ({removed}) before building the ribbon.",
            _ when none => $"{Prefix}: there was nothing to remove.",
            _ => $"{Prefix}: removed {removed}.",
        };
    }

    /// <summary>One warning line per thing that could not be removed, saying what that means.</summary>
    public IEnumerable<string> FailureMessages()
    {
        string consequence = Occasion == AddinUiOccasion.Loading
            ? "The ribbon is built on what is there; if a button is missing or shown twice, restart Inventor."
            : "It stays until Inventor restarts.";
        return _failures.Select(f => $"{Prefix}: could not remove {f}. {consequence}");
    }

    private static string Count(int n, string one, string many) =>
        string.Create(CultureInfo.InvariantCulture, $"{n} {(n == 1 ? one : many)}");
}

/// <summary>Tallies what building the ribbon added and what it found already there, for the log (task 121).</summary>
public sealed class RibbonBuildReport
{
    private readonly List<string> _tabsAdded = new();
    private readonly List<string> _tabsFound = new();

    public int CommandsRegistered { get; set; }

    public int CommandsFailed { get; set; }

    public IReadOnlyList<string> TabsAdded => _tabsAdded;

    /// <summary>Ribbons where the add-in's tab was already there, which should not happen after the removal on loading.</summary>
    public IReadOnlyList<string> TabsFound => _tabsFound;

    public int PanelsAdded { get; private set; }

    public int PanelsFound { get; private set; }

    public int ButtonsAdded { get; private set; }

    public int ButtonsFound { get; private set; }

    public void Tab(string ribbonName, bool added) => (added ? _tabsAdded : _tabsFound).Add(ribbonName);

    public void Panel(bool added)
    {
        if (added)
            PanelsAdded++;
        else
            PanelsFound++;
    }

    public void Button(bool added)
    {
        if (added)
            ButtonsAdded++;
        else
            ButtonsFound++;
    }

    /// <summary>The info line once the ribbon is built.</summary>
    public string Summary()
    {
        string commands = CommandsFailed == 0
            ? Count(CommandsRegistered, "command", "commands") + " registered"
            : $"{Count(CommandsRegistered, "command", "commands")} registered and {CommandsFailed} failed";

        string tabs = _tabsAdded.Count == 0
            ? "no tab added"
            : $"the tab added on {string.Join(", ", _tabsAdded)}";

        string text =
            $"Ribbon ({AddinUiOccasions.Text(AddinUiOccasion.Loading)}): {commands}; {tabs}; " +
            $"{Count(PanelsAdded, "panel", "panels")} and {Count(ButtonsAdded, "button", "buttons")} added.";

        if (_tabsFound.Count > 0 || PanelsFound > 0 || ButtonsFound > 0)
        {
            string tabsFound = _tabsFound.Count == 0 ? "no tab" : $"the tab on {string.Join(", ", _tabsFound)}";
            text +=
                $" Already there and reused: {tabsFound}, {Count(PanelsFound, "panel", "panels")} and " +
                $"{Count(ButtonsFound, "button", "buttons")}.";
        }

        return text;
    }

    private static string Count(int n, string one, string many) =>
        string.Create(CultureInfo.InvariantCulture, $"{n} {(n == 1 ? one : many)}");
}

internal static class AddinUiOccasions
{
    public static string Text(AddinUiOccasion occasion) => occasion switch
    {
        AddinUiOccasion.Loading => "add-in loading",
        AddinUiOccasion.Unloading => "add-in unloading",
        _ => throw new ArgumentOutOfRangeException(nameof(occasion), occasion, "Unknown occasion."),
    };
}
