using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using InventorAddin.Core.ViewModels;

namespace InventorAddin.UI
{
    /// <summary>
    /// Select Material and Finish window (spec 03, "Target window"; task 075). All behaviour is in
    /// <see cref="MaterialFinishViewModel"/>; this closes the window when the view-model asks (OK), and adds the keyboard
    /// and mouse shortcuts the bindings cannot: Enter in a search box moves to its list, Space on a material toggles its
    /// star, double-clicking a material moves to the Finishes list and double-clicking a finish does OK. Cancel closes
    /// through the button's <c>IsCancel</c>.
    /// </summary>
    public partial class MaterialFinishWindow : Window
    {
        private readonly MaterialFinishViewModel _viewModel;

        public MaterialFinishWindow(MaterialFinishViewModel viewModel)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            InitializeComponent();
            Title = "Select Material and Finish";
            DataContext = _viewModel;
            _viewModel.CloseRequested += OnCloseRequested;
            Loaded += OnLoaded;
            Closed += OnClosed;
        }

        // The current material and finish are preselected when the library has them; bring them into view.
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OnLoaded;
            if (MaterialsList.SelectedItem != null)
                MaterialsList.ScrollIntoView(MaterialsList.SelectedItem);
            if (FinishesList.SelectedItem != null)
                FinishesList.ScrollIntoView(FinishesList.SelectedItem);
            MaterialSearchBox.Focus();
        }

        // Setting DialogResult closes a window shown with ShowDialog.
        private void OnCloseRequested(object? sender, EventArgs e) => DialogResult = true;

        // Enter in a search box goes to the list below it instead of pressing OK.
        private void OnMaterialSearchKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
                return;
            e.Handled = true;
            FocusList(MaterialsList);
        }

        private void OnFinishSearchKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
                return;
            e.Handled = true;
            FocusList(FinishesList);
        }

        // Space on a material entry toggles its star, which is not a Tab stop of its own.
        private void OnMaterialsKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Space || Keyboard.Modifiers != ModifierKeys.None)
                return;
            if (e.OriginalSource is ListBoxItem { DataContext: MaterialRow row } && row.ToggleStarCommand.CanExecute(null))
            {
                e.Handled = true;
                row.ToggleStarCommand.Execute(null);
            }
        }

        // A single click has already selected the material; a double-click also moves to the Finishes list.
        private void OnMaterialDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (EntryUnder(MaterialsList, e) is MaterialRow)
                FocusList(FinishesList);
        }

        // Double-clicking a finish selects it (the first click) and does OK.
        private void OnFinishDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (EntryUnder(FinishesList, e) is FinishRow && _viewModel.OkCommand.CanExecute(null))
            {
                e.Handled = true;
                _viewModel.OkCommand.Execute(null);
            }
        }

        /// <summary>
        /// The entry the mouse event happened on, or null when it was on the scroll bar, the empty part of the list, or a
        /// button inside an entry (the star).
        /// </summary>
        private static object? EntryUnder(ListBox list, MouseButtonEventArgs e)
        {
            for (DependencyObject? node = e.OriginalSource as DependencyObject; node != null && node != list; node = ParentOf(node))
            {
                if (node is ButtonBase || node is ScrollBar)
                    return null;
                if (node is ListBoxItem item)
                    return item.DataContext;
            }

            return null;
        }

        private static DependencyObject? ParentOf(DependencyObject node) =>
            node is Visual || node is System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(node)
                : LogicalTreeHelper.GetParent(node);

        /// <summary>Moves keyboard focus to the list's selected entry, or its first entry, or the list itself.</summary>
        private static void FocusList(ListBox list)
        {
            object? entry = list.SelectedItem ?? (list.Items.Count > 0 ? list.Items[0] : null);
            if (entry == null)
            {
                list.Focus();
                return;
            }

            list.ScrollIntoView(entry);
            list.UpdateLayout();
            if (list.ItemContainerGenerator.ContainerFromItem(entry) is ListBoxItem container)
                container.Focus();
            else
                list.Focus();
        }

        private void OnClosed(object? sender, EventArgs e)
        {
            _viewModel.CloseRequested -= OnCloseRequested;
            Closed -= OnClosed;
        }
    }
}
