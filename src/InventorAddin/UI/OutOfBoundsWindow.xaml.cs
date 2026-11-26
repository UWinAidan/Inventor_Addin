using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace InventorAddin.UI
{
    /// <summary>
    /// The list Check Out of Bounds shows (task 195): one line per finding. Picking a line calls <c>onPick</c> with its index so the
    /// command can select that item in the drawing. The list holds only text; what to select is the command's business.
    /// </summary>
    public partial class OutOfBoundsWindow : Window
    {
        private readonly Action<int> _onPick;

        public OutOfBoundsWindow(string heading, IReadOnlyList<string> lines, Action<int> onPick)
        {
            InitializeComponent();
            Title = "Check Out of Bounds";
            _onPick = onPick ?? throw new ArgumentNullException(nameof(onPick));
            HeadingText.Text = heading;
            FindingsList.ItemsSource = lines;
        }

        // The keyboard picks through SelectionChanged. A mouse click picks on button up instead, so clicking the line that is already
        // selected (which raises no SelectionChanged) selects its item again, for example after the user clicked elsewhere in Inventor.
        private void FindingsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Mouse.LeftButton == MouseButtonState.Pressed)
                return;
            int index = FindingsList.SelectedIndex;
            if (index >= 0)
                _onPick(index);
        }

        private void FindingsList_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            DependencyObject? node = e.OriginalSource as DependencyObject;
            while (node != null && node is not ListBoxItem)
                node = node is Visual or System.Windows.Media.Media3D.Visual3D
                    ? System.Windows.Media.VisualTreeHelper.GetParent(node)
                    : LogicalTreeHelper.GetParent(node);
            if (node is ListBoxItem item)
            {
                int index = FindingsList.ItemContainerGenerator.IndexFromContainer(item);
                if (index >= 0)
                    _onPick(index);
            }
        }
    }
}
