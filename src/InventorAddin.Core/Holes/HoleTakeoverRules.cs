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
}
