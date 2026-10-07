using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using InventorAddin.Core.Ribbon;
using InventorAddin.Core.Theming;

// Namespace InventorAddin.UI, not InventorAddin.UI.Icons, to match ThemeResources and keep one namespace for the UI helpers.
namespace InventorAddin.UI
{
    /// <summary>
    /// Loads the add-in's icons (task 024's PNGs, embedded in this assembly) by icon name, theme and size:
    /// as OLE pictures for the ribbon's button definitions, and as WPF images for the window headers.
    /// The file names come from <see cref="RibbonIcons.FileName"/>; every load failure throws, and the caller logs it.
    /// </summary>
    internal static class IconLoader
    {
        // Matches the LogicalName of the icons' EmbeddedResource items in InventorAddin.csproj.
        private const string ResourcePrefix = "InventorAddin.UI.Icons.";

        /// <summary>The manifest resource name of an icon, for example <c>InventorAddin.UI.Icons.About.Dark.16.png</c>.</summary>
        public static string ResourceName(string iconName, UiTheme theme, int sizePx) =>
            ResourcePrefix + RibbonIcons.FileName(iconName, theme, sizePx);

        /// <summary>
        /// The ribbon pictures for <paramref name="iconName"/>: the 16 px icon as the standard icon and the 32 px icon as
        /// the large one, in <paramref name="theme"/>. Throws if either is missing or cannot be converted.
        /// </summary>
        public static ButtonPictures LoadButtonPictures(string iconName, UiTheme theme)
        {
            object standard = LoadPicture(iconName, theme, RibbonIcons.SmallSizePx);
            object large = LoadPicture(iconName, theme, RibbonIcons.LargeSizePx);
            return new ButtonPictures(iconName, standard, large);
        }

        /// <summary>One icon as an OLE picture (<c>IPictureDisp</c>), for a button definition.</summary>
        public static object LoadPicture(string iconName, UiTheme theme, int sizePx)
        {
            using Stream stream = OpenStream(iconName, theme, sizePx);
            using var bitmap = new Bitmap(stream);
            return PictureDispConverter.FromBitmap(bitmap);
        }

        /// <summary>One icon as a frozen WPF image, for a window header.</summary>
        public static ImageSource LoadImageSource(string iconName, UiTheme theme, int sizePx)
        {
            using Stream stream = OpenStream(iconName, theme, sizePx);

            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad; // read the stream now, so it can be closed
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }

        private static Stream OpenStream(string iconName, UiTheme theme, int sizePx)
        {
            string name = ResourceName(iconName, theme, sizePx);
            Assembly assembly = typeof(IconLoader).Assembly;
            return assembly.GetManifestResourceStream(name)
                ?? throw new FileNotFoundException($"The add-in has no embedded icon '{name}'.", name);
        }
    }

    /// <summary>The two pictures for one button definition, with the icon name they were loaded from.</summary>
    public sealed class ButtonPictures
    {
        public ButtonPictures(string iconName, object standard, object large)
        {
            IconName = iconName;
            Standard = standard;
            Large = large;
        }

        public string IconName { get; }

        /// <summary>The 16 px picture (<c>IPictureDisp</c>).</summary>
        public object Standard { get; }

        /// <summary>The 32 px picture (<c>IPictureDisp</c>).</summary>
        public object Large { get; }
    }
}
