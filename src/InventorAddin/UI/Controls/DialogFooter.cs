using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace InventorAddin.UI.Controls
{
    /// <summary>
    /// The footer band of a dialog (spec 08, "Window layout"): the status line on the left and the buttons,
    /// given directly as children, on the right, in one row.
    /// An <see cref="ItemsControl"/> so the buttons stay in the window's logical tree. Each button is wrapped in a
    /// <see cref="ContentPresenter"/> container so the footer's style can space the buttons without touching the
    /// buttons' own styles. The look is the implicit <c>DialogFooter</c> style in <c>UI/Theme/Styles.xaml</c>.
    /// </summary>
    public sealed class DialogFooter : ItemsControl
    {
        public static readonly DependencyProperty StatusProperty = DependencyProperty.Register(
            nameof(Status), typeof(string), typeof(DialogFooter), new FrameworkPropertyMetadata(null));

        public static readonly DependencyProperty IsErrorProperty = DependencyProperty.Register(
            nameof(IsError), typeof(bool), typeof(DialogFooter), new FrameworkPropertyMetadata(false));

        static DialogFooter()
        {
            FocusableProperty.OverrideMetadata(typeof(DialogFooter), new FrameworkPropertyMetadata(false));
            KeyboardNavigation.IsTabStopProperty.OverrideMetadata(typeof(DialogFooter), new FrameworkPropertyMetadata(false));
        }

        /// <summary>The status line text. Not drawn when empty.</summary>
        public string? Status
        {
            get => (string?)GetValue(StatusProperty);
            set => SetValue(StatusProperty, value);
        }

        /// <summary>True shows the status line in the error style instead of the muted one.</summary>
        public bool IsError
        {
            get => (bool)GetValue(IsErrorProperty);
            set => SetValue(IsErrorProperty, value);
        }

        // Always wrap, so ItemContainerStyle (the gap between buttons) applies even to a button with its own Style.
        protected override bool IsItemItsOwnContainerOverride(object item) => false;
    }
}
