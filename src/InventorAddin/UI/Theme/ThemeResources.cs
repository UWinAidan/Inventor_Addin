using System;
using System.Windows;
using InventorAddin.Core.Theming;

// Namespace InventorAddin.UI, not InventorAddin.UI.Theme: a namespace named Theme would hide Inventor's Theme type
// from code in InventorAddin.UI (the same reason Inventor/ComSafe.cs is in namespace InventorAddin).
namespace InventorAddin.UI
{
    /// <summary>
    /// Inventor's active theme and the shared resource dictionaries for it (spec 08, "Following Inventor's theme").
    /// The dictionaries are compiled into the add-in and loaded by pack URI, the method spike 020 proved in Inventor.
    /// <see cref="WindowHost"/> applies them to windows; other code (such as the header icons) can load them too.
    /// </summary>
    internal static class ThemeResources
    {
        /// <summary>Key of the window style in <c>Styles.xaml</c>.</summary>
        public const string DialogWindowStyleKey = "DialogWindow";

        private const string ThemeFolder = "UI/Theme/";
        private const string StylesFile = "Styles.xaml";

        /// <summary>
        /// The colour set for Inventor's active theme. Inventor 2026 reports <c>LightTheme</c> or <c>DarkTheme</c>
        /// (spike 020). When the name cannot be read, logs one warning and returns <see cref="UiTheme.Light"/>.
        /// </summary>
        public static UiTheme ReadInventorTheme()
        {
            string? name = ComSafe.Get(() => InventorHost.App.ThemeManager.ActiveTheme.Name);
            if (string.IsNullOrWhiteSpace(name))
            {
                AddinServices.Log.Warn("Could not read Inventor's active theme; the light colours are used.");
                return UiTheme.Light;
            }

            return UiThemes.FromInventorThemeName(name);
        }

        /// <summary>A new instance of the colour dictionary for <paramref name="theme"/>. Throws if it cannot be loaded.</summary>
        public static ResourceDictionary LoadColors(UiTheme theme) =>
            Load($"Colors.{UiThemes.ResourceSuffix(theme)}.xaml");

        /// <summary>A new instance of the shared styles. Throws if it cannot be loaded.</summary>
        public static ResourceDictionary LoadStyles() => Load(StylesFile);

        /// <summary>
        /// Loads the colour dictionary for <paramref name="theme"/> and then the styles, and merges both, in that order,
        /// into <paramref name="target"/>. If either fails to load, logs the error, merges neither and returns false.
        /// </summary>
        public static bool TryMergeInto(ResourceDictionary target, UiTheme theme)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            ResourceDictionary colors;
            ResourceDictionary styles;
            try
            {
                colors = LoadColors(theme);
                styles = LoadStyles();
            }
            catch (Exception ex)
            {
                AddinServices.Log.Error(
                    $"Could not load the shared window styles ({UiThemes.ResourceSuffix(theme)} colours); the window is shown with the default look.",
                    ex);
                return false;
            }

            target.MergedDictionaries.Add(colors);
            target.MergedDictionaries.Add(styles);
            return true;
        }

        /// <summary>The pack URI of a file compiled into this assembly, for example <c>UI/Theme/Styles.xaml</c>.</summary>
        public static Uri PackUri(string pathInAssembly)
        {
            string assemblyName = typeof(ThemeResources).Assembly.GetName().Name ?? "InventorAddin";
            return new Uri($"pack://application:,,,/{assemblyName};component/{pathInAssembly}", UriKind.Absolute);
        }

        private static ResourceDictionary Load(string fileName) =>
            new ResourceDictionary { Source = PackUri(ThemeFolder + fileName) };
    }
}
