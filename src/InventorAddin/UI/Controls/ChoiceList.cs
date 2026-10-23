using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace InventorAddin.UI.Controls
{
    /// <summary>
    /// A row of buttons of which one is chosen: the AWB Hole window's icon buttons (Type, Seat, Termination, Drill point)
    /// and its Units toggle (task 104). A <see cref="ListBox"/> with one selection, so it binds like a dropdown
    /// (<c>SelectedValuePath="Value"</c>, <c>SelectedValue</c> to the field), Tab moves into and out of it in one step,
    /// and the arrow keys or Space choose. When the list itself gets keyboard focus (a label's access key, or a click
    /// on its edge) the focus moves on to the chosen button, so the keyboard works at once and the focus outline shows
    /// where it is. The look is the <c>IconChoiceList</c> and <c>SegmentedChoiceList</c> styles in
    /// <c>UI/Theme/Styles.xaml</c>.
    /// </summary>
    public sealed class ChoiceList : ListBox
    {
        static ChoiceList()
        {
            SelectionModeProperty.OverrideMetadata(typeof(ChoiceList), new FrameworkPropertyMetadata(SelectionMode.Single));
        }

        /// <summary>Moves keyboard focus to the chosen button, else the first. False when no button is there yet.</summary>
        public bool FocusChosen()
        {
            object? item = SelectedItem ?? (Items.Count > 0 ? Items[0] : null);
            if (item == null)
                return false;

            if (ItemContainerGenerator.ContainerFromItem(item) is ListBoxItem container)
                return container.Focus();

            return false;
        }

        protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
        {
            base.OnGotKeyboardFocus(e);
            if (ReferenceEquals(e.NewFocus, this))
                FocusChosen();
        }
    }
}
