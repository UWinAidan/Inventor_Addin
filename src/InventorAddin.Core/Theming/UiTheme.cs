namespace InventorAddin.Core.Theming;

/// <summary>The colour set a window uses, following Inventor's theme (spec 08, "Following Inventor's theme").</summary>
public enum UiTheme
{
    Light,
    Dark,
}

public static class UiThemes
{
    /// <summary>
    /// The colour set for the theme name Inventor reports.
    /// A name containing "dark" in any case selects <see cref="UiTheme.Dark"/>; anything else, including a null,
    /// blank or unknown name, selects <see cref="UiTheme.Light"/> (the spec's fallback).
    /// </summary>
    /// <remarks>
    /// Default rule (DECISIONS.md, spec 08 open question 6) until spike 020 records the names Inventor 2026 reports.
    /// Keep the rule in this one method so changing it is one line.
    /// </remarks>
    public static UiTheme FromInventorThemeName(string? name) =>
        name is not null && name.Trim().Contains("dark", StringComparison.OrdinalIgnoreCase)
            ? UiTheme.Dark
            : UiTheme.Light;

    /// <summary>
    /// The suffix the add-in uses to build resource dictionary and icon file names for <paramref name="theme"/>:
    /// <c>"Light"</c> or <c>"Dark"</c>.
    /// </summary>
    public static string ResourceSuffix(UiTheme theme) => theme switch
    {
        UiTheme.Light => "Light",
        UiTheme.Dark => "Dark",
        _ => throw new ArgumentOutOfRangeException(nameof(theme), theme, "Unknown UI theme."),
    };
}
