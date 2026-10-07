using InventorAddin.Core.Theming;

namespace InventorAddin.Core.Ribbon;

/// <summary>
/// Which icon each ribbon command gets, and the file name of each icon image (spec 08, "Ribbon icons").
/// The add-in loads the images; the names and the choice are decided here.
/// </summary>
public static class RibbonIcons
{
    /// <summary>Size of the standard (small) ribbon icon, in pixels.</summary>
    public const int SmallSizePx = 16;

    /// <summary>Size of the large ribbon icon, in pixels. Also the size shown in a window's header tile.</summary>
    public const int LargeSizePx = 32;

    /// <summary>
    /// The image file name for an icon: <c>{iconName}.{Light|Dark}.{16|32}.png</c>,
    /// for example <c>About.Dark.16.png</c>.
    /// </summary>
    public static string FileName(string iconName, UiTheme theme, int sizePx)
    {
        if (string.IsNullOrWhiteSpace(iconName))
            throw new ArgumentException("An icon name is required.", nameof(iconName));
        if (sizePx != SmallSizePx && sizePx != LargeSizePx)
            throw new ArgumentOutOfRangeException(nameof(sizePx), sizePx, $"Icons come in {SmallSizePx} and {LargeSizePx} px only.");

        return $"{iconName}.{UiThemes.ResourceSuffix(theme)}.{sizePx}.png";
    }

    /// <summary>
    /// One icon name per command, from the buttons in the order given. A command has one button definition however
    /// many ribbons show it, so its icon is chosen once: the first icon name listed for it wins, and every later,
    /// different name is reported as a conflict (once per distinct name).
    /// </summary>
    public static RibbonIconChoice Choose(IEnumerable<ButtonLayout> buttons)
    {
        if (buttons == null)
            throw new ArgumentNullException(nameof(buttons));

        var chosen = new Dictionary<string, string>(StringComparer.Ordinal);
        var conflicts = new List<IconNameConflict>();

        foreach (ButtonLayout button in buttons)
        {
            if (!chosen.TryGetValue(button.CommandInternalName, out string? kept))
            {
                chosen.Add(button.CommandInternalName, button.IconName);
                continue;
            }

            if (string.Equals(kept, button.IconName, StringComparison.Ordinal))
                continue;

            var conflict = new IconNameConflict(button.CommandInternalName, kept, button.IconName);
            if (!conflicts.Contains(conflict))
                conflicts.Add(conflict);
        }

        return new RibbonIconChoice(chosen, conflicts);
    }

    /// <summary>
    /// <see cref="Choose"/> over every button the layout shows, in every environment in the order the add-in builds them.
    /// A command the layout does not show (for example a developer command with developer tools off) gets no icon.
    /// </summary>
    public static RibbonIconChoice ForLayout(bool showDeveloperTools) =>
        Choose(RibbonEnvironments.All
            .SelectMany(e => RibbonLayout.For(e, showDeveloperTools))
            .SelectMany(p => p.Buttons));
}

/// <summary>The icon name chosen for each command, and the layout entries that disagreed with the choice.</summary>
public sealed class RibbonIconChoice
{
    private readonly IReadOnlyDictionary<string, string> _iconNameByCommand;

    public RibbonIconChoice(IReadOnlyDictionary<string, string> iconNameByCommand, IReadOnlyList<IconNameConflict> conflicts)
    {
        _iconNameByCommand = iconNameByCommand ?? throw new ArgumentNullException(nameof(iconNameByCommand));
        Conflicts = conflicts ?? throw new ArgumentNullException(nameof(conflicts));
    }

    /// <summary>Each command's internal name and the icon name chosen for it.</summary>
    public IReadOnlyDictionary<string, string> IconNameByCommand => _iconNameByCommand;

    /// <summary>Commands the layout gave a second, different icon name. The first name is used.</summary>
    public IReadOnlyList<IconNameConflict> Conflicts { get; }

    /// <summary>The icon name for <paramref name="commandInternalName"/>, or null when no button shows the command.</summary>
    public string? IconNameFor(string commandInternalName) =>
        _iconNameByCommand.TryGetValue(commandInternalName, out string? name) ? name : null;
}

/// <summary>A command listed with two different icon names. <see cref="KeptIconName"/> is used.</summary>
public sealed record IconNameConflict(string CommandInternalName, string KeptIconName, string IgnoredIconName);
