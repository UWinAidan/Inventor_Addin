using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using InventorAddin.Core.Theming;

namespace InventorAddin.UI.Controls
{
    /// <summary>
    /// A section of a dialog's body (spec 08, "Window layout"): an optional heading with a divider under it,
    /// then its rows, usually <see cref="FieldRow"/>s given directly as children.
    /// An <see cref="ItemsControl"/> so the rows stay in the window's logical tree (bindings and
    /// <c>ElementName</c> work as usual). The look is the implicit <c>FormSection</c> style in <c>UI/Theme/Styles.xaml</c>.
    /// </summary>
    public sealed class FormSection : ItemsControl
    {
        public static readonly DependencyProperty HeadingProperty = DependencyProperty.Register(
            nameof(Heading), typeof(string), typeof(FormSection),
            new FrameworkPropertyMetadata(null, OnHeadingChanged));

        private static readonly DependencyPropertyKey DisplayHeadingPropertyKey = DependencyProperty.RegisterReadOnly(
            nameof(DisplayHeading), typeof(string), typeof(FormSection), new FrameworkPropertyMetadata(string.Empty));

        public static readonly DependencyProperty DisplayHeadingProperty = DisplayHeadingPropertyKey.DependencyProperty;

        private static readonly DependencyPropertyKey HasHeadingPropertyKey = DependencyProperty.RegisterReadOnly(
            nameof(HasHeading), typeof(bool), typeof(FormSection), new FrameworkPropertyMetadata(false));

        public static readonly DependencyProperty HasHeadingProperty = HasHeadingPropertyKey.DependencyProperty;

        static FormSection()
        {
            FocusableProperty.OverrideMetadata(typeof(FormSection), new FrameworkPropertyMetadata(false));
            KeyboardNavigation.IsTabStopProperty.OverrideMetadata(typeof(FormSection), new FrameworkPropertyMetadata(false));
        }

        /// <summary>The heading as written, for example "Identity". Null or blank draws no heading and no divider.</summary>
        public string? Heading
        {
            get => (string?)GetValue(HeadingProperty);
            set => SetValue(HeadingProperty, value);
        }

        /// <summary>The heading as shown: upper case with wide letter spacing (<see cref="SectionHeadings.Format"/>).</summary>
        public string DisplayHeading => (string)GetValue(DisplayHeadingProperty);

        public bool HasHeading => (bool)GetValue(HasHeadingProperty);

        private static void OnHeadingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var section = (FormSection)d;
            string display = SectionHeadings.Format(e.NewValue as string);
            section.SetValue(DisplayHeadingPropertyKey, display);
            section.SetValue(HasHeadingPropertyKey, display.Length > 0);
        }
    }
}
