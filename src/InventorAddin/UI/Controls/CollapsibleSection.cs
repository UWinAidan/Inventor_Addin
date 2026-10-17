using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using InventorAddin.Core.Theming;

namespace InventorAddin.UI.Controls
{
    /// <summary>
    /// A section of a dialog's body whose heading collapses and expands it (spec 08, "Collapsible sections"; task 079).
    /// Laid out like <see cref="FormSection"/>: a heading with a divider under it, then its rows given directly as
    /// children. The heading is a button with a chevron; while collapsed, the rows are hidden and
    /// <see cref="Summary"/> shows on the heading's line, so the section's values can be read without opening it.
    /// Sections start expanded. A window shown by <c>WindowHost</c> with a state key remembers which of its named
    /// sections were collapsed (by <c>x:Name</c>). The look is the implicit <c>CollapsibleSection</c> style in
    /// <c>UI/Theme/Styles.xaml</c>.
    /// </summary>
    public sealed class CollapsibleSection : ItemsControl
    {
        public static readonly DependencyProperty HeadingProperty = DependencyProperty.Register(
            nameof(Heading), typeof(string), typeof(CollapsibleSection),
            new FrameworkPropertyMetadata(null, OnHeadingChanged));

        private static readonly DependencyPropertyKey DisplayHeadingPropertyKey = DependencyProperty.RegisterReadOnly(
            nameof(DisplayHeading), typeof(string), typeof(CollapsibleSection), new FrameworkPropertyMetadata(string.Empty));

        public static readonly DependencyProperty DisplayHeadingProperty = DisplayHeadingPropertyKey.DependencyProperty;

        public static readonly DependencyProperty IsExpandedProperty = DependencyProperty.Register(
            nameof(IsExpanded), typeof(bool), typeof(CollapsibleSection),
            new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public static readonly DependencyProperty SummaryProperty = DependencyProperty.Register(
            nameof(Summary), typeof(string), typeof(CollapsibleSection), new FrameworkPropertyMetadata(null));

        static CollapsibleSection()
        {
            FocusableProperty.OverrideMetadata(typeof(CollapsibleSection), new FrameworkPropertyMetadata(false));
            KeyboardNavigation.IsTabStopProperty.OverrideMetadata(typeof(CollapsibleSection), new FrameworkPropertyMetadata(false));
        }

        /// <summary>The heading as written, for example "Folders".</summary>
        public string? Heading
        {
            get => (string?)GetValue(HeadingProperty);
            set => SetValue(HeadingProperty, value);
        }

        /// <summary>The heading as shown: upper case with wide letter spacing (<see cref="SectionHeadings.Format"/>).</summary>
        public string DisplayHeading => (string)GetValue(DisplayHeadingProperty);

        /// <summary>Whether the rows are shown. True by default.</summary>
        public bool IsExpanded
        {
            get => (bool)GetValue(IsExpandedProperty);
            set => SetValue(IsExpandedProperty, value);
        }

        /// <summary>One line describing the section's values, shown beside the heading while collapsed.</summary>
        public string? Summary
        {
            get => (string?)GetValue(SummaryProperty);
            set => SetValue(SummaryProperty, value);
        }

        private static void OnHeadingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
            d.SetValue(DisplayHeadingPropertyKey, SectionHeadings.Format(e.NewValue as string));
    }
}
