using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace InventorAddin.UI.Controls
{
    /// <summary>
    /// One row of a <see cref="FormSection"/>: a label in the label column and a value (the content) beside it.
    /// <see cref="Label"/> may carry an access key ("_Designer"); <see cref="Target"/> is the control the key focuses.
    /// The look is the implicit <c>FieldRow</c> style in <c>UI/Theme/Styles.xaml</c>.
    /// </summary>
    public sealed class FieldRow : ContentControl
    {
        public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
            nameof(Label), typeof(string), typeof(FieldRow), new FrameworkPropertyMetadata(null));

        public static readonly DependencyProperty TargetProperty = DependencyProperty.Register(
            nameof(Target), typeof(UIElement), typeof(FieldRow), new FrameworkPropertyMetadata(null));

        static FieldRow()
        {
            FocusableProperty.OverrideMetadata(typeof(FieldRow), new FrameworkPropertyMetadata(false));
            KeyboardNavigation.IsTabStopProperty.OverrideMetadata(typeof(FieldRow), new FrameworkPropertyMetadata(false));
        }

        /// <summary>The label text. An underscore marks the access key, as in a WPF <c>Label</c>.</summary>
        public string? Label
        {
            get => (string?)GetValue(LabelProperty);
            set => SetValue(LabelProperty, value);
        }

        /// <summary>The control that the label's access key focuses, usually bound with <c>ElementName</c>.</summary>
        public UIElement? Target
        {
            get => (UIElement?)GetValue(TargetProperty);
            set => SetValue(TargetProperty, value);
        }
    }
}
