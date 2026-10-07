using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace InventorAddin.UI.Controls
{
    /// <summary>
    /// The header band of a dialog (spec 08, "Window layout"): an icon tile, a title and one muted line under it.
    /// A lookless control; its template is the implicit <c>DialogHeader</c> style in <c>UI/Theme/Styles.xaml</c>.
    /// </summary>
    public sealed class DialogHeader : Control
    {
        public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
            nameof(Icon), typeof(ImageSource), typeof(DialogHeader), new FrameworkPropertyMetadata(null));

        public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
            nameof(Title), typeof(string), typeof(DialogHeader), new FrameworkPropertyMetadata(null));

        public static readonly DependencyProperty SublineProperty = DependencyProperty.Register(
            nameof(Subline), typeof(string), typeof(DialogHeader), new FrameworkPropertyMetadata(null));

        static DialogHeader()
        {
            // Not a stop for Tab or focus, as for UserControl.
            FocusableProperty.OverrideMetadata(typeof(DialogHeader), new FrameworkPropertyMetadata(false));
            KeyboardNavigation.IsTabStopProperty.OverrideMetadata(typeof(DialogHeader), new FrameworkPropertyMetadata(false));
        }

        /// <summary>The picture in the icon tile. Empty until the header icons are added (task 030); the tile is drawn either way.</summary>
        public ImageSource? Icon
        {
            get => (ImageSource?)GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }

        public string? Title
        {
            get => (string?)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        /// <summary>The muted line under the title. Not drawn when empty.</summary>
        public string? Subline
        {
            get => (string?)GetValue(SublineProperty);
            set => SetValue(SublineProperty, value);
        }
    }
}
