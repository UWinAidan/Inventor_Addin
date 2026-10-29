using InventorAddin.Core.Settings;
using InventorAddin.Core.ViewModels;

namespace InventorAddin.Core.Holes;

/// <summary>Which Hole command has the Hole button on the 3D Model tab and the H shortcut (spec 09, "The command").</summary>
public enum HoleCommandOwner
{
    /// <summary>AWB Hole: the switch "Use Inventor's Hole command" is off, the default.</summary>
    Awb,

    /// <summary>Inventor's Hole: the switch is on, or the add-in is unloading, or taking over failed.</summary>
    Inventor,
}

/// <summary>
/// What the add-in sets on the 3D Model tab's Modify panel and on the two Hole commands' shortcuts.
/// </summary>
/// <param name="Owner">Who the state gives the Hole button and H to.</param>
/// <param name="AwbButtonVisible">Whether AWB Hole's button on the Modify panel shows.</param>
/// <param name="InventorButtonVisible">Whether Inventor's Hole button on the Modify panel shows.</param>
/// <param name="AwbShortcut">AWB Hole's <c>OverrideShortcut</c>; empty clears it.</param>
/// <param name="InventorShortcut">
/// Inventor's Hole's <c>OverrideShortcut</c>, or null to leave it as it is (the user's own shortcut for it).
/// </param>
public sealed record HoleTakeoverState(
    HoleCommandOwner Owner,
    bool AwbButtonVisible,
    bool InventorButtonVisible,
    string AwbShortcut,
    string? InventorShortcut);

/// <summary>
/// The decisions behind AWB Hole replacing Inventor's Hole (spec 09, "The command" and "Settings"; spike 081 section
/// 3; task 091): where the button goes, and who gets the button and H from the setting. The add-in's
/// <c>HoleCommandTakeover</c> reads Inventor, asks this, and applies the state.
/// </summary>
/// <remarks>
/// Giving Hole back must give Inventor's Hole a shortcut again: clearing its override left it with an empty one, so H
/// did nothing (spike 081 round 1). So a give-back sets Inventor's default shortcut whenever its override is empty,
/// and leaves an override the user set on it.
/// </remarks>
public static class HoleTakeoverRules
{
    /// <summary>Inventor's Hole command (spike 081).</summary>
    public const string InventorHoleCommand = "PartHoleCmd";

    /// <summary>
    /// Inventor's Hole on the viewport's mini toolbar, shown on a sketch point, in a part's own window and in a part
    /// edited in place (spike 103). It is not <see cref="InventorHoleCommand"/>, so the ribbon takeover does not cover it.
    /// </summary>
    public const string InventorMiniToolbarHoleCommand = "PartDMHoleCmd";

    /// <summary>The part ribbon, its 3D Model tab and that tab's Modify panel, where Inventor's Hole sits large.</summary>
    public const string RibbonName = "Part";

    /// <inheritdoc cref="RibbonName"/>
    public const string TabId = "id_TabModel";

    /// <inheritdoc cref="RibbonName"/>
    public const string PanelId = "id_PanelP_ModelModify";

    /// <summary>The shortcut AWB Hole takes, and Inventor's Hole's default when Inventor cannot say what it is.</summary>
    public const string Shortcut = "H";

    /// <summary>Who has the Hole button and H under these settings.</summary>
    public static HoleCommandOwner OwnerFor(AddinSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return settings.UseInventorHoleCommand ? HoleCommandOwner.Inventor : HoleCommandOwner.Awb;
    }

    /// <summary>AWB Hole's button shown, Inventor's hidden, H on AWB Hole and taken from Inventor's Hole.</summary>
    public static HoleTakeoverState TakenOver { get; } =
        new(HoleCommandOwner.Awb, AwbButtonVisible: true, InventorButtonVisible: false, Shortcut, string.Empty);

    /// <summary>
    /// Inventor's Hole button shown, AWB Hole's hidden, AWB Hole's shortcut cleared, and Inventor's Hole given its
    /// default shortcut unless the user has set one of their own on it.
    /// </summary>
    /// <param name="inventorOverride">Inventor's Hole's <c>OverrideShortcut</c> now, or null when unreadable.</param>
    /// <param name="inventorDefault">Inventor's Hole's <c>DefaultShortcut</c>, or null when unreadable.</param>
    public static HoleTakeoverState GivenBack(string? inventorOverride, string? inventorDefault) =>
        new(
            HoleCommandOwner.Inventor,
            AwbButtonVisible: false,
            InventorButtonVisible: true,
            AwbShortcut: string.Empty,
            InventorShortcut: string.IsNullOrWhiteSpace(inventorOverride) ? DefaultShortcut(inventorDefault) : null);

    /// <summary>The state for <paramref name="owner"/>; see <see cref="TakenOver"/> and <see cref="GivenBack"/>.</summary>
    public static HoleTakeoverState For(HoleCommandOwner owner, string? inventorOverride, string? inventorDefault) =>
        owner == HoleCommandOwner.Awb ? TakenOver : GivenBack(inventorOverride, inventorDefault);

    /// <summary>
    /// Who has the Hole button and H after applying <paramref name="requested"/>: AWB Hole only when it was asked for
    /// and taking over worked. A take-over that failed gives Inventor's Hole back, so Inventor's Hole keeps working.
    /// </summary>
    public static HoleCommandOwner OwnerAfterApply(HoleCommandOwner requested, bool takeOverSucceeded) =>
        requested == HoleCommandOwner.Awb && takeOverSucceeded ? HoleCommandOwner.Awb : HoleCommandOwner.Inventor;

    /// <summary>
    /// What is applied, in order, to put the Hole commands right after a shortcut reset (task 120): what toggling
    /// the switch off and on in Settings does, the fix that worked in check 091-3. AWB Hole gives Hole back first and
    /// then takes it over; Inventor's Hole is given back once.
    /// </summary>
    public static IReadOnlyList<HoleCommandOwner> ResetReapplyOrder(HoleCommandOwner owner) =>
        owner == HoleCommandOwner.Awb
            ? [HoleCommandOwner.Inventor, HoleCommandOwner.Awb]
            : [HoleCommandOwner.Inventor];

    /// <summary>
    /// Whether the two Hole commands' shortcuts, as read from Inventor, are what <paramref name="owner"/> having Hole
    /// sets, or null when either could not be read. AWB Hole: H on ours, none on Inventor's. Inventor's Hole: none on
    /// ours, a shortcut on Inventor's (its default, or the user's own).
    /// </summary>
    /// <param name="awbOverride">AWB Hole's <c>OverrideShortcut</c>, or null when unreadable.</param>
    /// <param name="inventorOverride">Inventor's Hole's <c>OverrideShortcut</c>, or null when unreadable.</param>
    public static bool? ShortcutsApplied(HoleCommandOwner owner, string? awbOverride, string? inventorOverride)
    {
        if (awbOverride == null || inventorOverride == null)
        {
            return null;
        }

        return owner == HoleCommandOwner.Awb
            ? string.Equals(awbOverride.Trim(), Shortcut, StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(inventorOverride)
            : string.IsNullOrWhiteSpace(awbOverride) && !string.IsNullOrWhiteSpace(inventorOverride);
    }

    /// <summary>The two Hole commands' shortcuts as read, for the log: "AWB Hole 'H', Inventor's Hole none".</summary>
    public static string ShortcutsMessage(string? awbOverride, string? inventorOverride) =>
        $"{AwbHoleViewModel.CommandName} {Describe(awbOverride)}, Inventor's Hole {Describe(inventorOverride)}";

    private static string Describe(string? shortcut) =>
        shortcut == null ? "unreadable" : string.IsNullOrWhiteSpace(shortcut) ? "none" : $"'{shortcut.Trim()}'";

    /// <summary>Inventor's Hole's default shortcut, or <see cref="Shortcut"/> when it reads blank or not at all.</summary>
    public static string DefaultShortcut(string? inventorDefault) =>
        string.IsNullOrWhiteSpace(inventorDefault) ? Shortcut : inventorDefault.Trim();

    /// <summary>The log line for a state applied, with why it was applied (startup, settings saved, and so on).</summary>
    public static string AppliedMessage(HoleTakeoverState state, string reason)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state.Owner == HoleCommandOwner.Awb
            ? $"Hole takeover ({reason}): {AwbHoleViewModel.CommandName} has the Hole button on the 3D Model tab and the {Shortcut} shortcut."
            : $"Hole takeover ({reason}): Inventor's Hole has its button on the 3D Model tab and its shortcut back.";
    }

    /// <summary>The log line when taking over failed and Inventor's Hole is being given back.</summary>
    public static string TakeOverFailedMessage(string reason) =>
        $"Hole takeover ({reason}): {AwbHoleViewModel.CommandName} could not replace Inventor's Hole, so Inventor's Hole and its shortcut " +
        $"are given back. {AwbHoleViewModel.CommandName} is still on the {Branding.ProductName} tab.";

    /// <summary>
    /// The viewport's mini toolbar (<c>OnContextualMiniToolbar</c>, its commands in a <c>NameValueMap</c>; task 133): the
    /// 1-based index of Inventor's Hole (<see cref="InventorMiniToolbarHoleCommand"/>), before which AWB Hole is
    /// inserted (<c>Insert(ours, value, index, true)</c>, then <c>Remove</c> Inventor's, as for Edit Feature), or null
    /// when Inventor's is not there or ours already is. Internal names match exactly.
    /// </summary>
    /// <param name="toolbarCommands">The names in the map, in order (the map's item 1 first).</param>
    /// <param name="ourCommand">AWB Hole's internal name.</param>
    public static int? MiniToolbarHoleIndex(IReadOnlyList<string?> toolbarCommands, string ourCommand)
    {
        ArgumentNullException.ThrowIfNull(toolbarCommands);
        ArgumentException.ThrowIfNullOrWhiteSpace(ourCommand);

        var index = IndexOf(toolbarCommands, InventorMiniToolbarHoleCommand);
        if (index < 0 || IndexOf(toolbarCommands, ourCommand) >= 0)
        {
            return null;
        }

        return index + 1;
    }

    /// <summary>True for Inventor's mini toolbar Hole command; internal names match exactly.</summary>
    public static bool IsInventorMiniToolbarHole(string? command) =>
        string.Equals(command?.Trim(), InventorMiniToolbarHoleCommand, StringComparison.Ordinal);

    /// <summary>
    /// The mini toolbar backstop (<c>OnActivateCommand</c>; task 133): true when the command starting is Inventor's
    /// mini toolbar Hole, AWB Hole has the Hole command (<paramref name="owner"/>; null when the takeover is not
    /// running), a part is being edited, and the add-in did not ask for this start (<paramref name="askedFor"/>: the
    /// split button's "Inventor Hole", task 129). Inventor's is then stopped once its event has returned and AWB Hole
    /// opened on the part.
    /// </summary>
    public static bool StopsInventorMiniToolbarHole(
        string? startedCommand, HoleCommandOwner? owner, bool documentIsPart, bool askedFor) =>
        owner == HoleCommandOwner.Awb && documentIsPart && !askedFor && IsInventorMiniToolbarHole(startedCommand);

    /// <summary>The log line naming a sheet metal part's mini toolbar entries, so its Hole command can be found.</summary>
    public static string SheetMetalMiniToolbarMessage(IReadOnlyList<string?> toolbarCommands)
    {
        ArgumentNullException.ThrowIfNull(toolbarCommands);
        var names = toolbarCommands.Count == 0
            ? "(none)"
            : string.Join(", ", toolbarCommands.Select(n => string.IsNullOrWhiteSpace(n) ? "(unnamed)" : n.Trim()));
        return $"Hole takeover: the mini toolbar on a sketch point in a sheet metal part shows {names}. " +
               $"Only {InventorMiniToolbarHoleCommand} is replaced by {AwbHoleViewModel.CommandName}.";
    }

    private static int IndexOf(IReadOnlyList<string?> names, string name)
    {
        for (var i = 0; i < names.Count; i++)
        {
            if (string.Equals(names[i]?.Trim(), name, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }
}

/// <summary>
/// Whether a start of Inventor's mini toolbar Hole is one the add-in asked for (task 133), so the backstop lets it
/// run: the split button's "Inventor Hole" (task 129) runs Inventor's Hole once and calls <see cref="Ask"/> first.
/// </summary>
/// <remarks>
/// The request is used up by the next start of <see cref="HoleTakeoverRules.InventorMiniToolbarHoleCommand"/>. A start
/// of Inventor's ribbon Hole (<see cref="HoleTakeoverRules.InventorHoleCommand"/>) keeps it, since that may be how
/// Inventor's Hole comes to run; any other command clears it, so a request whose Hole never started cannot let a later
/// one through.
/// </remarks>
public sealed class InventorHoleRunRequest
{
    private bool _asked;

    /// <summary>True while a request waits for Inventor's Hole to start.</summary>
    public bool IsPending => _asked;

    /// <summary>The add-in is about to run Inventor's Hole itself: its next start is not stopped.</summary>
    public void Ask() => _asked = true;

    /// <summary>
    /// A command started (<c>OnActivateCommand</c>): true when it is Inventor's mini toolbar Hole and a request was
    /// waiting, which this uses up. See the remarks for which starts keep or clear the request.
    /// </summary>
    public bool StartIsAskedFor(string? startedCommand)
    {
        if (HoleTakeoverRules.IsInventorMiniToolbarHole(startedCommand))
        {
            var asked = _asked;
            _asked = false;
            return asked;
        }

        if (!string.Equals(startedCommand?.Trim(), HoleTakeoverRules.InventorHoleCommand, StringComparison.Ordinal))
        {
            _asked = false;
        }

        return false;
    }
}
