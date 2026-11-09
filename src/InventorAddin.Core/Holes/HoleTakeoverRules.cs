using InventorAddin.Core.Ribbon;
using InventorAddin.Core.Settings;
using InventorAddin.Core.ViewModels;

namespace InventorAddin.Core.Holes;

/// <summary>Which Hole command has the Hole button on the 3D Model and Sheet Metal tabs and the H shortcut (spec 09, "The command").</summary>
public enum HoleCommandOwner
{
    /// <summary>AWB Hole: the switch "Use Inventor's Hole command" is off, the default.</summary>
    Awb,

    /// <summary>Inventor's Hole: the switch is on, or the add-in is unloading, or taking over failed.</summary>
    Inventor,
}

/// <summary>The control AWB Hole gets on Inventor's 3D Model and Sheet Metal tabs (task 129).</summary>
public enum HoleButtonKind
{
    /// <summary>
    /// A split button: AWB Hole on top, and a dropdown with AWB Hole, Inventor Hole and "Use Inventor's Hole by
    /// default".
    /// </summary>
    SplitButton,

    /// <summary>The plain AWB Hole button of task 091, used when a dropdown command failed to register.</summary>
    PlainButton,
}

/// <summary>When a sketch point mini toolbar's entries (task 141), or a marking menu ring's slots (task 142), are logged.</summary>
public enum MiniToolbarStage
{
    /// <summary>As Inventor gave them, before AWB Hole is swapped in.</summary>
    BeforeSwap,

    /// <summary>After AWB Hole was swapped in for Inventor's Hole.</summary>
    AfterSwap,

    /// <summary>After the swap broke the list and Inventor's was put back.</summary>
    PutBack,
}

/// <summary>
/// How a broken mini toolbar swap is undone (task 141): only what changed.
/// </summary>
/// <param name="RemoveOurs">AWB Hole's entry is there and is removed.</param>
/// <param name="ReinsertInventors">Inventor's Hole is missing and is put back.</param>
/// <param name="InsertBeforeIndex">
/// The 1-based index, in the list once ours is removed, before which Inventor's goes; null to add it at the end.
/// </param>
public sealed record MiniToolbarUndo(bool RemoveOurs, bool ReinsertInventors, int? InsertBeforeIndex);

/// <summary>
/// What the add-in sets on the 3D Model tab's Modify panel, on the Sheet Metal tab's Hole panel (task 128), and on the
/// two Hole commands' shortcuts.
/// </summary>
/// <param name="Owner">Who the state gives the Hole button and H to.</param>
/// <param name="AwbButtonVisible">Whether AWB Hole's buttons on Inventor's panels show.</param>
/// <param name="InventorButtonVisible">Whether Inventor's Hole buttons on those panels show.</param>
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

    /// <summary>
    /// The Part ribbon's Sheet Metal tab, where AWB Hole replaces Inventor's Hole too (Aidan, 2026-10-25; task 128).
    /// Not confirmed by a spike: the add-in logs every panel with a Hole command when it applies the takeover.
    /// </summary>
    public const string SheetMetalTabId = "id_TabSheetMetal";

    /// <summary>
    /// The Sheet Metal tab's panel spike 081 saw Inventor's Hole on (small). The panel is looked up at run time by the
    /// control's command (<see cref="SheetMetalHolePanel"/>); this one is preferred when several hold it.
    /// </summary>
    public const string SheetMetalPanelId = "id_PanelP_SheetMetalModify";

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
    /// <param name="sheetMetalTab">Whether the Sheet Metal tab's Hole button was set too (task 128).</param>
    public static string AppliedMessage(HoleTakeoverState state, string reason, bool sheetMetalTab = false)
    {
        ArgumentNullException.ThrowIfNull(state);
        var tabs = sheetMetalTab ? "the 3D Model and Sheet Metal tabs" : "the 3D Model tab";
        return state.Owner == HoleCommandOwner.Awb
            ? $"Hole takeover ({reason}): {AwbHoleViewModel.CommandName} has the Hole button on {tabs} and the {Shortcut} shortcut."
            : $"Hole takeover ({reason}): Inventor's Hole has its button on {tabs} and its shortcut back.";
    }

    /// <summary>The log line when taking over failed and Inventor's Hole is being given back.</summary>
    public static string TakeOverFailedMessage(string reason) =>
        $"Hole takeover ({reason}): {AwbHoleViewModel.CommandName} could not replace Inventor's Hole, so Inventor's Hole and its shortcut " +
        $"are given back. {AwbHoleViewModel.CommandName} is still on the {Branding.ProductName} tab.";

    // ---- The split button (task 129) ----

    /// <summary>The dropdown entry that runs Inventor's Hole once (Aidan, 2026-10-25).</summary>
    public const string InventorHoleText = "Inventor Hole";

    /// <summary>Its tooltip.</summary>
    public const string InventorHoleDescription =
        "Run Inventor's Hole command once. The next Hole or H opens " + AwbHoleViewModel.CommandName + " again.";

    /// <summary>The dropdown toggle for the setting "Use Inventor's Hole command" (Aidan, 2026-10-25).</summary>
    public const string UseInventorHoleText = "Use Inventor's Hole by default";

    /// <summary>Its tooltip.</summary>
    public const string UseInventorHoleDescription =
        "Give the Hole button and the H shortcut back to Inventor's Hole. The same setting as \"Use Inventor's Hole " +
        "command\" in " + Branding.ProductName + " Settings; turn it off there.";

    /// <summary>
    /// The split button's commands, in order: AWB Hole, shown on top and first in the dropdown, then Inventor Hole,
    /// then the toggle. Inventor's <c>AddSplitButton</c> takes the top command and this list.
    /// </summary>
    public static IReadOnlyList<string> SplitButtonCommands { get; } =
        [CommandNames.AwbHole, CommandNames.InventorHole, CommandNames.UseInventorHoleByDefault];

    /// <summary>
    /// The split button when both dropdown commands are registered; else AWB Hole's plain button, so the takeover
    /// still works without them.
    /// </summary>
    public static HoleButtonKind ButtonKind(bool inventorHoleRegistered, bool toggleRegistered) =>
        inventorHoleRegistered && toggleRegistered ? HoleButtonKind.SplitButton : HoleButtonKind.PlainButton;

    /// <summary>The log line when the split button cannot be used and AWB Hole's plain button is put there instead.</summary>
    public static string PlainButtonMessage(string reason) =>
        $"Hole takeover ({reason}): {InventorHoleText} or \"{UseInventorHoleText}\" is not registered, so " +
        $"{AwbHoleViewModel.CommandName} is a plain button on Inventor's tabs, with no dropdown.";

    /// <summary>
    /// The icon a dropdown command registers with, as it has no layout entry: AWB Hole's for Inventor Hole, replaced by
    /// Inventor's Hole icon when that can be read (as task 092 does for Edit Feature), and the Settings icon for the
    /// toggle (Default). Null for any other command.
    /// </summary>
    public static string? DropdownIconName(string commandInternalName) => commandInternalName switch
    {
        CommandNames.InventorHole => IconNames.AwbHole,
        CommandNames.UseInventorHoleByDefault => IconNames.Settings,
        _ => null,
    };

    /// <summary>
    /// The command Inventor Hole runs, on the 3D Model tab and on the Sheet Metal tab alike: the Sheet Metal tab's Hole
    /// button runs <see cref="InventorHoleCommand"/> too (task 128). The backstop (<see cref="StopsInventorHole"/>)
    /// lets this start through, and the mini toolbar's Hole that may follow it (<see cref="InventorHoleRunRequest"/>).
    /// </summary>
    public const string InventorHoleRunCommand = InventorHoleCommand;

    /// <summary>Whether the toggle shows pressed: while the setting "Use Inventor's Hole command" is on.</summary>
    public static bool TogglePressed(AddinSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return settings.UseInventorHoleCommand;
    }

    /// <summary>The settings after the toggle is pressed: "Use Inventor's Hole command" flipped, the rest unchanged.</summary>
    public static AddinSettings Toggled(AddinSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return settings with { UseInventorHoleCommand = !settings.UseInventorHoleCommand };
    }

    /// <summary>Why the takeover is applied after the toggle was pressed, for the log.</summary>
    public const string ToggledReason = "Use Inventor's Hole by default pressed";

    /// <summary>The log line once the toggled setting is saved.</summary>
    public static string ToggledMessage(AddinSettings saved, string filePath)
    {
        ArgumentNullException.ThrowIfNull(saved);
        var state = saved.UseInventorHoleCommand ? "on" : "off";
        return $"\"{UseInventorHoleText}\" turned {state}; settings saved to '{filePath}'.";
    }

    /// <summary>The message when the toggled setting could not be saved; nothing is changed.</summary>
    public static string ToggleSaveFailedText(string filePath, string error) =>
        $"The setting was not changed: settings could not be saved to '{filePath}'. {error}";

    /// <summary>The message when the add-in has no data folder, so the toggled setting cannot be saved.</summary>
    public const string ToggleNoDataFolderText =
        "The setting was not changed: the add-in could not set up its data folder when Inventor started, so it cannot save settings.";

    /// <summary>The log line when Inventor Hole runs Inventor's Hole.</summary>
    public const string InventorHoleRunMessage =
        "Hole takeover: " + InventorHoleText + " pressed; Inventor's Hole runs once and the setting is unchanged.";

    /// <summary>
    /// The viewport's mini toolbar (<c>OnContextualMiniToolbar</c>, its commands in a <c>NameValueMap</c>; task 133): the
    /// 1-based index of Inventor's Hole (<see cref="InventorMiniToolbarHoleCommand"/>), before which AWB Hole is
    /// inserted (<c>Insert(ours, value, index, true)</c>, then <c>Remove</c> Inventor's, as for Edit Feature), or null
    /// when Inventor's is not there, ours already is, or an entry's name could not be read (task 141: Inventor's list
    /// could not then be put back if the swap broke it). Internal names match exactly.
    /// </summary>
    /// <param name="toolbarCommands">The names in the map, in order (the map's item 1 first).</param>
    /// <param name="ourCommand">AWB Hole's internal name.</param>
    public static int? MiniToolbarHoleIndex(IReadOnlyList<string?> toolbarCommands, string ourCommand)
    {
        ArgumentNullException.ThrowIfNull(toolbarCommands);
        ArgumentException.ThrowIfNullOrWhiteSpace(ourCommand);

        var index = IndexOf(toolbarCommands, InventorMiniToolbarHoleCommand);
        if (index < 0 || IndexOf(toolbarCommands, ourCommand) >= 0 || toolbarCommands.Any(string.IsNullOrWhiteSpace))
        {
            return null;
        }

        return index + 1;
    }

    /// <summary>True for Inventor's mini toolbar Hole command; internal names match exactly.</summary>
    public static bool IsInventorMiniToolbarHole(string? command) =>
        string.Equals(command?.Trim(), InventorMiniToolbarHoleCommand, StringComparison.Ordinal);

    /// <summary>
    /// True for either of Inventor's Hole commands the backstop stops: the mini toolbar's
    /// (<see cref="InventorMiniToolbarHoleCommand"/>; task 133) and the ribbon's (<see cref="InventorHoleCommand"/>),
    /// which is also the radial marking menu's Hole (task 142). Internal names match exactly.
    /// </summary>
    public static bool IsInventorHole(string? command) =>
        IsInventorMiniToolbarHole(command) ||
        string.Equals(command?.Trim(), InventorHoleCommand, StringComparison.Ordinal);

    /// <summary>
    /// The Flat Pattern tab keeps Inventor's Hole (Aidan, 2026-10-25 and 2026-11-09; task 128), and its Hole is
    /// <see cref="InventorHoleCommand"/> too. True while a flat pattern is being edited: Inventor's
    /// <c>ActiveEditObject</c> is the <c>FlatPattern</c>, or the active environment's
    /// internal name names the flat pattern (<see cref="IsFlatPatternEnvironment"/>).
    /// </summary>
    /// <param name="editObjectIsFlatPattern">Whether <c>Application.ActiveEditObject</c> is a <c>FlatPattern</c>.</param>
    /// <param name="activeEnvironment">The active environment's <c>InternalName</c>, or null when unreadable.</param>
    public static bool InFlatPattern(bool editObjectIsFlatPattern, string? activeEnvironment) =>
        editObjectIsFlatPattern || IsFlatPatternEnvironment(activeEnvironment);

    /// <summary>
    /// True when an environment's internal name contains "FlatPattern", ignoring case. The flat pattern environment's
    /// exact name is not confirmed in Inventor 2026 (task 142); this is only the fallback to
    /// <c>ActiveEditObject</c>.
    /// </summary>
    public static bool IsFlatPatternEnvironment(string? environmentInternalName) =>
        environmentInternalName?.Contains("FlatPattern", StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>
    /// Whether the mini toolbar's and the marking menu ring's Hole entries are swapped for AWB Hole now (tasks 133, 141,
    /// 142): AWB Hole has the Hole command, a part is being edited, and not its flat pattern.
    /// </summary>
    public static bool TakesOverHoleEntries(HoleCommandOwner? owner, bool documentIsPart, bool inFlatPattern) =>
        owner == HoleCommandOwner.Awb && documentIsPart && !inFlatPattern;

    /// <summary>
    /// The backstop (<c>OnActivateCommand</c>; tasks 133 and 142): true when the command starting is one of Inventor's
    /// Hole commands (<see cref="IsInventorHole"/>), the Hole entries are taken over (<see cref="TakesOverHoleEntries"/>:
    /// AWB Hole has Hole, a part is being edited, not in its flat pattern), and the add-in did not ask for this start
    /// (<paramref name="askedFor"/>: the split button's "Inventor Hole", task 129). Inventor's is then stopped once its
    /// event has returned and AWB Hole opened on the part.
    /// </summary>
    /// <remarks>
    /// While AWB Hole has Hole, Inventor's Hole button is hidden on the 3D Model and Sheet Metal tabs and H is AWB
    /// Hole's, so outside the flat pattern a start of <see cref="InventorHoleCommand"/> the add-in did not ask for comes
    /// from somewhere the takeover did not reach: the marking menu's ring when its swap did not hold, a shortcut the user
    /// set on it, or the command search. The Flat Pattern tab's Hole stays visible and is Inventor's, so nothing is
    /// stopped there.
    /// </remarks>
    public static bool StopsInventorHole(
        string? startedCommand, HoleCommandOwner? owner, bool documentIsPart, bool inFlatPattern, bool askedFor) =>
        TakesOverHoleEntries(owner, documentIsPart, inFlatPattern) && !askedFor && IsInventorHole(startedCommand);

    /// <summary>The log line when the backstop stops one of Inventor's Hole commands and opens AWB Hole instead.</summary>
    public static string InventorHoleStoppedMessage(string? startedCommand)
    {
        var from = IsInventorMiniToolbarHole(startedCommand)
            ? "from the mini toolbar"
            : $"({startedCommand?.Trim()}: the marking menu, a shortcut or the command search)";
        return $"Hole takeover: Inventor's Hole {from} started; {AwbHoleViewModel.CommandName} opens instead.";
    }

    /// <summary>
    /// The mini toolbar's entries the swap should leave (task 141): Inventor's, in order, with
    /// <see cref="InventorMiniToolbarHoleCommand"/> replaced by <paramref name="ourCommand"/> at the same position and
    /// nothing else changed. Names are trimmed; an unreadable name stays null.
    /// </summary>
    /// <param name="before">The names in the map before the swap, in order.</param>
    /// <param name="ourCommand">AWB Hole's internal name.</param>
    public static IReadOnlyList<string?> SwappedMiniToolbar(IReadOnlyList<string?> before, string ourCommand)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentException.ThrowIfNullOrWhiteSpace(ourCommand);

        var index = IndexOf(before, InventorMiniToolbarHoleCommand);
        var expected = new List<string?>(before.Count);
        for (var i = 0; i < before.Count; i++)
        {
            expected.Add(i == index ? ourCommand.Trim() : before[i]?.Trim());
        }

        return expected;
    }

    /// <summary>
    /// True when the mini toolbar after the swap is exactly <see cref="SwappedMiniToolbar"/> of the list before: the
    /// same entries in the same order, with only Inventor's Hole replaced by ours (task 141). Anything else (an entry
    /// lost, added or moved, ours missing, Inventor's still there) is false, and Inventor's list is put back, since a
    /// broken toolbar is worse than Inventor's Hole. Names are trimmed and match exactly; an unreadable name matches
    /// nothing.
    /// </summary>
    /// <param name="before">The names in the map before the swap, in order.</param>
    /// <param name="after">The names in the map after the swap, in order.</param>
    /// <param name="ourCommand">AWB Hole's internal name.</param>
    public static bool MiniToolbarSwapHeld(IReadOnlyList<string?> before, IReadOnlyList<string?> after, string ourCommand)
    {
        ArgumentNullException.ThrowIfNull(after);
        var expected = SwappedMiniToolbar(before, ourCommand);
        if (IndexOf(before, InventorMiniToolbarHoleCommand) < 0 || expected.Count != after.Count)
        {
            return false;
        }

        for (var i = 0; i < expected.Count; i++)
        {
            var name = after[i]?.Trim();
            if (string.IsNullOrEmpty(name) || !string.Equals(expected[i], name, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// The Info line naming a sketch point mini toolbar's entries, in order, at <paramref name="stage"/> (task 141),
    /// each with the kind of its value (the entity Inventor highlights when the entry is hovered) when given.
    /// </summary>
    /// <param name="stage">Before the swap, after it, or after Inventor's list was put back.</param>
    /// <param name="toolbarCommands">The names in the map, in order.</param>
    /// <param name="valueKinds">
    /// Each entry's value kind (its type name, or null for no value), in the same order, or null to leave them out.
    /// </param>
    /// <param name="sheetMetal">Whether the part is a sheet metal part.</param>
    public static string MiniToolbarMessage(
        MiniToolbarStage stage, IReadOnlyList<string?> toolbarCommands, IReadOnlyList<string?>? valueKinds, bool sheetMetal)
    {
        ArgumentNullException.ThrowIfNull(toolbarCommands);
        var entries = new List<string>(toolbarCommands.Count);
        for (var i = 0; i < toolbarCommands.Count; i++)
        {
            var entry = Entry(toolbarCommands[i]);
            if (valueKinds is not null)
            {
                var kind = i < valueKinds.Count ? valueKinds[i] : null;
                entry += $" [{(string.IsNullOrWhiteSpace(kind) ? "no value" : kind.Trim())}]";
            }

            entries.Add(entry);
        }

        var when = stage switch
        {
            MiniToolbarStage.BeforeSwap => "before the swap",
            MiniToolbarStage.AfterSwap => "after the swap",
            _ => "after Inventor's list was put back",
        };
        var part = sheetMetal ? "a sheet metal part" : "a part";
        var list = entries.Count == 0 ? "(none)" : string.Join(", ", entries);
        return $"Hole takeover: the mini toolbar on a sketch point in {part}, {when}: {list}.";
    }

    /// <summary>
    /// The warning when the swap did not leave the expected list (task 141): Inventor's list is put back, so the mini
    /// toolbar shows Inventor's Hole (whose start the backstop still turns into AWB Hole).
    /// </summary>
    /// <param name="expected">The list the swap should have left (<see cref="SwappedMiniToolbar"/>).</param>
    /// <param name="after">The list it left.</param>
    /// <param name="error">The swap's exception message, if it threw.</param>
    public static string MiniToolbarSwapFailedMessage(
        IReadOnlyList<string?> expected, IReadOnlyList<string?> after, string? error = null)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(after);
        var threw = string.IsNullOrWhiteSpace(error) ? string.Empty : $" The swap threw: {error.Trim()}";
        return $"Hole takeover: replacing Hole on the mini toolbar should have left {Names(expected)} but left " +
               $"{Names(after)}.{threw} Inventor's list is being put back, so the toolbar shows Inventor's Hole.";
    }

    /// <summary>
    /// The warning when the swap left Inventor's list as it was (task 141; for example <c>Insert</c> refused the value
    /// and threw before anything changed): the map is left alone, so the toolbar shows Inventor's Hole.
    /// </summary>
    /// <param name="before">Inventor's list, unchanged.</param>
    /// <param name="error">The swap's exception message, if it threw.</param>
    public static string MiniToolbarSwapLeftAloneMessage(IReadOnlyList<string?> before, string? error = null)
    {
        ArgumentNullException.ThrowIfNull(before);
        var threw = string.IsNullOrWhiteSpace(error) ? string.Empty : $" The swap threw: {error.Trim()}";
        return $"Hole takeover: replacing Hole on the mini toolbar changed nothing; {Names(before)} is left as Inventor " +
               $"gave it, so the toolbar shows Inventor's Hole.{threw}";
    }

    /// <summary>
    /// True when two mini toolbar lists hold the same entries in the same order (task 141): names trimmed and matched
    /// exactly; an unreadable name matches nothing. Used to see that the swap changed nothing, so Inventor's list is not
    /// touched, and that a put-back gave Inventor's list again.
    /// </summary>
    public static bool SameMiniToolbar(IReadOnlyList<string?> first, IReadOnlyList<string?> second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        if (first.Count != second.Count)
        {
            return false;
        }

        for (var i = 0; i < first.Count; i++)
        {
            var a = first[i]?.Trim();
            if (string.IsNullOrEmpty(a) || !string.Equals(a, second[i]?.Trim(), StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// How a broken swap is undone (task 141), touching only what changed: whether AWB Hole's entry is removed (it is
    /// there), and whether Inventor's Hole is put back (it is missing) and where. Inventor's goes back before the entry
    /// now at its old 1-based index, or at the end (<see cref="MiniToolbarUndo.InsertBeforeIndex"/> null) when the list
    /// is now shorter than that. If the list after the undo is still not Inventor's, the caller falls back to clearing
    /// the map and adding Inventor's entries again.
    /// </summary>
    /// <param name="before">Inventor's list before the swap.</param>
    /// <param name="now">The list now.</param>
    /// <param name="ourCommand">AWB Hole's internal name.</param>
    public static MiniToolbarUndo UndoMiniToolbarSwap(IReadOnlyList<string?> before, IReadOnlyList<string?> now, string ourCommand)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(now);
        ArgumentException.ThrowIfNullOrWhiteSpace(ourCommand);

        var removeOurs = IndexOf(now, ourCommand) >= 0;
        var oldIndex = IndexOf(before, InventorMiniToolbarHoleCommand);
        if (oldIndex < 0 || IndexOf(now, InventorMiniToolbarHoleCommand) >= 0)
        {
            return new MiniToolbarUndo(removeOurs, false, null);
        }

        // The count once ours is removed.
        var count = now.Count - (removeOurs ? 1 : 0);
        int? before1Based = oldIndex < count ? oldIndex + 1 : null;
        return new MiniToolbarUndo(removeOurs, true, before1Based);
    }

    /// <summary>The Error when the last-resort put-back could not add some of Inventor's entries again.</summary>
    /// <param name="failed">Each entry that could not be added, with the reason.</param>
    /// <param name="valueLost">Entries added back with no value, because their value could not be read before the swap.</param>
    public static string MiniToolbarPutBackMessage(IReadOnlyList<string> failed, IReadOnlyList<string?> valueLost)
    {
        ArgumentNullException.ThrowIfNull(failed);
        ArgumentNullException.ThrowIfNull(valueLost);
        var notAdded = failed.Count == 0 ? "every entry was added back" : $"not added back: {string.Join("; ", failed)}";
        var lost = valueLost.Count == 0 ? string.Empty : $"; added back with no value (unreadable before): {Names(valueLost)}";
        return $"Hole takeover: Inventor's mini toolbar list was cleared and added back entry by entry; {notAdded}{lost}.";
    }

    // ---- The radial marking menu (task 142) ----

    /// <summary>
    /// The radial marking menu's eight slots (<c>RadialMarkingMenu</c>'s <c>NorthControl</c> to
    /// <c>NorthwestControl</c>), clockwise from north: the order the add-in reads them in, and their names in the log.
    /// </summary>
    public static IReadOnlyList<string> RingSlots { get; } =
        ["north", "north-east", "east", "south-east", "south", "south-west", "west", "north-west"];

    /// <summary>
    /// The 0-based slots of the ring that hold Inventor's Hole (<see cref="IsInventorHole"/>; in a part's default ring,
    /// <see cref="InventorHoleCommand"/> in the south-east slot, read in Inventor 2026), where AWB Hole's button goes.
    /// Empty when no slot holds it, ours is already there, the list is not the eight slots, or a filled slot's name
    /// could not be read (the ring could not then be checked after the swap).
    /// </summary>
    /// <param name="slots">
    /// Each slot's command in <see cref="RingSlots"/> order: null for an empty slot, an empty string for a filled slot
    /// whose name could not be read.
    /// </param>
    /// <param name="ourCommand">AWB Hole's internal name.</param>
    public static IReadOnlyList<int> RingHoleSlots(IReadOnlyList<string?> slots, string ourCommand)
    {
        ArgumentNullException.ThrowIfNull(slots);
        ArgumentException.ThrowIfNullOrWhiteSpace(ourCommand);

        if (slots.Count != RingSlots.Count
            || IndexOf(slots, ourCommand) >= 0
            || slots.Any(s => s is not null && string.IsNullOrWhiteSpace(s)))
        {
            return [];
        }

        var found = new List<int>();
        for (var i = 0; i < slots.Count; i++)
        {
            if (IsInventorHole(slots[i]))
            {
                found.Add(i);
            }
        }

        return found;
    }

    /// <summary>
    /// The ring the swap should leave: <paramref name="before"/> with each slot of <see cref="RingHoleSlots"/> holding
    /// <paramref name="ourCommand"/> and the rest as they were. Names are trimmed.
    /// </summary>
    public static IReadOnlyList<string?> SwappedRing(IReadOnlyList<string?> before, string ourCommand)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentException.ThrowIfNullOrWhiteSpace(ourCommand);

        var swap = RingHoleSlots(before, ourCommand);
        var expected = new List<string?>(before.Count);
        for (var i = 0; i < before.Count; i++)
        {
            expected.Add(swap.Contains(i) ? ourCommand.Trim() : before[i]?.Trim());
        }

        return expected;
    }

    /// <summary>
    /// True when the ring after the swap is exactly <see cref="SwappedRing"/> of the ring before, and there was a Hole
    /// to swap. Anything else (Inventor's Hole still there, ours missing, another slot changed) is false, and the slots
    /// that changed are put back (<see cref="RingSlotsToRestore"/>).
    /// </summary>
    public static bool RingSwapHeld(IReadOnlyList<string?> before, IReadOnlyList<string?> after, string ourCommand)
    {
        ArgumentNullException.ThrowIfNull(after);
        if (RingHoleSlots(before, ourCommand).Count == 0)
        {
            return false;
        }

        return SameRing(SwappedRing(before, ourCommand), after);
    }

    /// <summary>
    /// True when two rings hold the same thing in each slot: names trimmed and matched exactly, two empty slots alike,
    /// and an unreadable name (an empty string) matching nothing.
    /// </summary>
    public static bool SameRing(IReadOnlyList<string?> first, IReadOnlyList<string?> second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        if (first.Count != second.Count)
        {
            return false;
        }

        for (var i = 0; i < first.Count; i++)
        {
            if (!SameSlot(first[i], second[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// The 0-based slots whose contents differ between Inventor's ring and the ring now (as <see cref="SameRing"/>
    /// compares them), each of which is set back to what Inventor gave when the swap did not hold. Empty when the
    /// rings are alike.
    /// </summary>
    public static IReadOnlyList<int> RingSlotsToRestore(IReadOnlyList<string?> before, IReadOnlyList<string?> now)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(now);

        var restore = new List<int>();
        for (var i = 0; i < before.Count; i++)
        {
            if (i >= now.Count || !SameSlot(before[i], now[i]))
            {
                restore.Add(i);
            }
        }

        return restore;
    }

    /// <summary>
    /// The Info line naming a ring's slots at <paramref name="stage"/> (task 142): "north PartFilletCmd, ..., west
    /// (empty)".
    /// </summary>
    /// <param name="stage">Before the swap, after it, or after Inventor's ring was put back.</param>
    /// <param name="ringName">The ring's <c>InternalName</c>, or null when unreadable.</param>
    /// <param name="slots">Each slot's command, as for <see cref="RingHoleSlots"/>.</param>
    /// <param name="sheetMetal">Whether the part is a sheet metal part.</param>
    public static string RingMessage(MiniToolbarStage stage, string? ringName, IReadOnlyList<string?> slots, bool sheetMetal)
    {
        ArgumentNullException.ThrowIfNull(slots);
        var when = stage switch
        {
            MiniToolbarStage.BeforeSwap => "before the swap",
            MiniToolbarStage.AfterSwap => "after the swap",
            _ => "after Inventor's ring was put back",
        };
        var part = sheetMetal ? "a sheet metal part" : "a part";
        var name = string.IsNullOrWhiteSpace(ringName) ? "(unnamed)" : ringName.Trim();
        return $"Hole takeover: the marking menu ring '{name}' in {part}, {when}: {RingSlotList(slots)}.";
    }

    /// <summary>
    /// The key under which a ring is logged once per session (task 142): its name and the part kind, since Inventor
    /// gives a different ring for each kind of selection.
    /// </summary>
    public static string RingLogKey(string? ringName, bool sheetMetal) =>
        $"{(sheetMetal ? "sheet metal" : "part")}|{ringName?.Trim()}";

    /// <summary>
    /// The warning when the ring swap did not leave the expected ring (task 142): the slots that changed are put back,
    /// so the ring shows Inventor's Hole, whose start the backstop still turns into AWB Hole.
    /// </summary>
    /// <param name="expected">The ring the swap should have left (<see cref="SwappedRing"/>).</param>
    /// <param name="after">The ring it left.</param>
    /// <param name="error">The swap's exception message, if it threw.</param>
    public static string RingSwapFailedMessage(IReadOnlyList<string?> expected, IReadOnlyList<string?> after, string? error = null)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(after);
        var threw = string.IsNullOrWhiteSpace(error) ? string.Empty : $" The swap threw: {error.Trim()}";
        return $"Hole takeover: replacing Hole on the marking menu ring should have left {RingSlotList(expected)} but left " +
               $"{RingSlotList(after)}.{threw} The slots that changed are put back, so the ring shows Inventor's Hole, " +
               $"which opens {AwbHoleViewModel.CommandName} through the backstop.";
    }

    private static bool SameSlot(string? a, string? b)
    {
        if (a is null || b is null)
        {
            return a is null && b is null;
        }

        var x = a.Trim();
        return x.Length > 0 && string.Equals(x, b.Trim(), StringComparison.Ordinal);
    }

    private static string RingSlotList(IReadOnlyList<string?> slots)
    {
        var entries = new List<string>(slots.Count);
        for (var i = 0; i < slots.Count; i++)
        {
            var where = i < RingSlots.Count ? RingSlots[i] : $"slot {i + 1}";
            var what = slots[i] is null ? "(empty)" : Entry(slots[i]);
            entries.Add($"{where} {what}");
        }

        return entries.Count == 0 ? "(none)" : string.Join(", ", entries);
    }

    private static string Names(IReadOnlyList<string?> names) =>
        names.Count == 0 ? "(none)" : string.Join(", ", names.Select(Entry));

    private static string Entry(string? name) => string.IsNullOrWhiteSpace(name) ? "(unnamed)" : name.Trim();

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
/// Whether a start of one of Inventor's Hole commands is one the add-in asked for (tasks 133 and 142), so the backstop
/// lets it run: the split button's "Inventor Hole" (task 129) runs Inventor's Hole once and calls <see cref="Ask"/>
/// first.
/// </summary>
/// <remarks>
/// The request is used up by the next start of <see cref="HoleTakeoverRules.InventorMiniToolbarHoleCommand"/>. A start
/// of Inventor's ribbon Hole (<see cref="HoleTakeoverRules.InventorHoleCommand"/>, the command "Inventor Hole" runs) is
/// let through and keeps the request, since it may in turn start the mini toolbar's Hole; any other command clears it,
/// so a request whose Hole never started cannot let a later one through.
/// </remarks>
public sealed class InventorHoleRunRequest
{
    private bool _asked;

    /// <summary>True while a request waits for Inventor's Hole to start.</summary>
    public bool IsPending => _asked;

    /// <summary>The add-in is about to run Inventor's Hole itself: its next start is not stopped.</summary>
    public void Ask() => _asked = true;

    /// <summary>
    /// A command started (<c>OnActivateCommand</c>): true when it is one of Inventor's Hole commands and a request was
    /// waiting. The mini toolbar's Hole uses the request up; the ribbon's keeps it. See the remarks.
    /// </summary>
    public bool StartIsAskedFor(string? startedCommand)
    {
        if (HoleTakeoverRules.IsInventorMiniToolbarHole(startedCommand))
        {
            var asked = _asked;
            _asked = false;
            return asked;
        }

        if (string.Equals(startedCommand?.Trim(), HoleTakeoverRules.InventorHoleCommand, StringComparison.Ordinal))
        {
            return _asked;
        }

        _asked = false;
        return false;
    }
}
