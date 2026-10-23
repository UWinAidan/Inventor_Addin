using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace InventorAddin.UI.Controls
{
    /// <summary>
    /// One of the AWB Hole window's icons (task 104): the part in section, its material shaded and the hole left empty,
    /// in the control's <see cref="Control.Foreground"/>, so the icon follows the button's state and the theme.
    /// <see cref="Icon"/> is a <c>HoleIcons</c> name from Core; the drawings are geometry resources in
    /// <c>UI/Theme/Styles.xaml</c>, keyed <c>HoleIcon.{name}.Material</c> (shaded and outlined), <c>.Line</c> (thin lines
    /// such as arrows) and <c>.Thick</c> (a face drawn heavy). An icon without a part leaves that part empty. The look
    /// (24 square, a 20 by 20 drawing) is the implicit <c>HoleIcon</c> style.
    /// </summary>
    public sealed class HoleIcon : Control
    {
        private const string KeyPrefix = "HoleIcon.";

        public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
            nameof(Icon), typeof(string), typeof(HoleIcon),
            new FrameworkPropertyMetadata(null, (d, _) => ((HoleIcon)d).ApplyIcon()));

        public static readonly DependencyProperty MaterialGeometryProperty = DependencyProperty.Register(
            nameof(MaterialGeometry), typeof(Geometry), typeof(HoleIcon), new FrameworkPropertyMetadata(null));

        public static readonly DependencyProperty LineGeometryProperty = DependencyProperty.Register(
            nameof(LineGeometry), typeof(Geometry), typeof(HoleIcon), new FrameworkPropertyMetadata(null));

        public static readonly DependencyProperty ThickGeometryProperty = DependencyProperty.Register(
            nameof(ThickGeometry), typeof(Geometry), typeof(HoleIcon), new FrameworkPropertyMetadata(null));

        static HoleIcon()
        {
            FocusableProperty.OverrideMetadata(typeof(HoleIcon), new FrameworkPropertyMetadata(false));
            IsHitTestVisibleProperty.OverrideMetadata(typeof(HoleIcon), new FrameworkPropertyMetadata(false));
        }

        /// <summary>The icon's name, for example <c>Counterbore</c>; null draws nothing.</summary>
        public string? Icon
        {
            get => (string?)GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }

        /// <summary>The shaded material, filled and outlined.</summary>
        public Geometry? MaterialGeometry
        {
            get => (Geometry?)GetValue(MaterialGeometryProperty);
            set => SetValue(MaterialGeometryProperty, value);
        }

        /// <summary>Thin lines: arrows and dimension marks.</summary>
        public Geometry? LineGeometry
        {
            get => (Geometry?)GetValue(LineGeometryProperty);
            set => SetValue(LineGeometryProperty, value);
        }

        /// <summary>A heavy line: the face a hole ends at.</summary>
        public Geometry? ThickGeometry
        {
            get => (Geometry?)GetValue(ThickGeometryProperty);
            set => SetValue(ThickGeometryProperty, value);
        }

        // Resource references, so the drawings are found once the window's theme is merged, whenever that is.
        private void ApplyIcon()
        {
            string? name = Icon;
            if (string.IsNullOrWhiteSpace(name))
            {
                ClearValue(MaterialGeometryProperty);
                ClearValue(LineGeometryProperty);
                ClearValue(ThickGeometryProperty);
                return;
            }

            SetResourceReference(MaterialGeometryProperty, KeyPrefix + name + ".Material");
            SetResourceReference(LineGeometryProperty, KeyPrefix + name + ".Line");
            SetResourceReference(ThickGeometryProperty, KeyPrefix + name + ".Thick");
        }
    }
}
