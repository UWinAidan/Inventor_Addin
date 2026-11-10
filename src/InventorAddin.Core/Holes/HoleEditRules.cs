using InventorAddin.Core.ViewModels;

namespace InventorAddin.Core.Holes;

/// <summary>
/// The decisions behind every way of editing a hole opening the AWB Hole window (spec 09, "The command": "Editing opens
/// AWB Hole"; spike 081 rounds 2 to 4; task 092): Inventor's Edit Feature on the browser's right-click menu and on the
/// viewport's mini toolbar is swapped for ours, a double-click on a hole opens our window, and Inventor's own hole edit,
/// if it starts anyway, is stopped and our window opened instead. The add-in's <c>HoleEditRedirect</c> reads Inventor,
/// asks this, and applies the answer.
/// </summary>
/// <remarks>
/// Editing follows the Hole command: while AWB Hole has Inventor's Hole button and H (the switch "Use Inventor's Hole
/// command" off), editing opens AWB Hole; while Inventor's Hole is back, Edit Feature and double-click stay Inventor's.
/// Like the AWB Hole command (task 090), it works on the part open in the window only, not on a part edited in place
/// inside an assembly.
/// </remarks>
public static class HoleEditRules
{
    /// <summary>Inventor's Edit Feature for a hole (spike 081), on the browser menu and the mini toolbar.</summary>
    public const string InventorEditCommand = "PartNGxHoleEditCtxCmd";

    /// <summary>Our entry's text, as Inventor's: the mini toolbar shows its icon only, copied from Inventor's.</summary>
    public const string EditFeatureText = "Edit Feature";

    /// <summary>Our entry's tooltip.</summary>
    public const string EditFeatureDescription = "Edit the hole in the " + AwbHoleViewModel.CommandName + " window.";

    /// <summary>
    /// True when an edit of a hole opens AWB Hole: AWB Hole has the Hole command (<paramref name="owner"/>; null when
    /// the takeover is not running) and the document open in the window is a part.
    /// </summary>
    public static bool Redirects(HoleCommandOwner? owner, bool documentIsPart) =>
        owner == HoleCommandOwner.Awb && documentIsPart;

    /// <summary>
    /// The hole a selection is about: each selected item resolved to its hole (a hole itself, or a face a hole made;
    /// null for anything else). The hole when every item is that same hole; null for an empty selection, an item that
    /// is not a hole, or two different holes, so Inventor's own handling is left alone.
    /// </summary>
    /// <param name="holes">Each selected item's hole, or null when the item is not a hole or a hole's face.</param>
    /// <param name="same">Whether two holes are the same feature.</param>
    public static T? SingleHole<T>(IEnumerable<T?> holes, Func<T, T, bool> same)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(holes);
        ArgumentNullException.ThrowIfNull(same);

        T? found = null;
        foreach (var hole in holes)
        {
            if (hole is null)
            {
                return null;
            }

            if (found is null)
            {
                found = hole;
            }
            else if (!ReferenceEquals(found, hole) && !same(found, hole))
            {
                return null;
            }
        }

        return found;
    }

    /// <summary>
    /// The browser's right-click menu (<c>OnLinearMarkingMenu</c>): true when Inventor's Edit Feature is on it and ours
    /// is not yet, so ours is put in its place (before it) and Inventor's deleted.
    /// </summary>
    /// <param name="menuCommands">The internal names of the menu's entries, in order.</param>
    /// <param name="ourCommand">Our Edit Feature's internal name.</param>
    public static bool ReplacesMenuEntry(IReadOnlyList<string?> menuCommands, string ourCommand)
    {
        ArgumentNullException.ThrowIfNull(menuCommands);
        ArgumentException.ThrowIfNullOrWhiteSpace(ourCommand);
        return IndexOf(menuCommands, InventorEditCommand) >= 0 && IndexOf(menuCommands, ourCommand) < 0;
    }

    /// <summary>
    /// The viewport's mini toolbar (<c>OnContextualMiniToolbar</c>, its commands in a <c>NameValueMap</c>): the 1-based
    /// index of Inventor's Edit Feature, before which ours is inserted (spike 081: <c>Insert(ours, value, index,
    /// true)</c>, then <c>Remove</c> Inventor's), or null when Inventor's is not there or ours already is.
    /// </summary>
    /// <param name="toolbarCommands">The names in the map, in order (the map's item 1 first).</param>
    /// <param name="ourCommand">Our Edit Feature's internal name.</param>
    public static int? MiniToolbarIndex(IReadOnlyList<string?> toolbarCommands, string ourCommand)
    {
        ArgumentNullException.ThrowIfNull(toolbarCommands);
        ArgumentException.ThrowIfNullOrWhiteSpace(ourCommand);

        var index = IndexOf(toolbarCommands, InventorEditCommand);
        if (index < 0 || IndexOf(toolbarCommands, ourCommand) >= 0)
        {
            return null;
        }

        return index + 1;
    }

    /// <summary>
    /// The backstop (<c>OnActivateCommand</c>): true when the command starting is Inventor's hole edit and editing
    /// opens AWB Hole, so Inventor's is stopped (<c>StopActiveCommand</c>) once its event has returned and our window
    /// opened on the hole. With no single hole selected (<paramref name="hasHole"/> false), Inventor's edit is left to
    /// run, since our window would have nothing to open on.
    /// </summary>
    public static bool StopsInventorEdit(string? startedCommand, bool redirects, bool hasHole) =>
        redirects && hasHole && IsInventorEdit(startedCommand);

    /// <summary>True for Inventor's hole Edit Feature command; internal names match exactly.</summary>
    public static bool IsInventorEdit(string? command) =>
        string.Equals(command?.Trim(), InventorEditCommand, StringComparison.Ordinal);

    /// <summary>The log line when an edit opens AWB Hole, naming the way in (double-click, Edit Feature, and so on).</summary>
    public static string OpeningMessage(string route, string? holeName) =>
        $"{AwbHoleViewModel.CommandName}: {route} on {Name(holeName)} opens the {AwbHoleViewModel.CommandName} window.";

    /// <summary>The log line when an edit finds the window already open: it is brought to the front instead.</summary>
    public static string AlreadyOpenMessage(string route, string? holeName) =>
        $"{AwbHoleViewModel.CommandName}: {route} on {Name(holeName)} while the window is open; the window is brought to the front.";

    private static string Name(string? holeName) =>
        string.IsNullOrWhiteSpace(holeName) ? "a hole" : holeName.Trim();

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
