using System;
using System.Windows;
using System.Windows.Threading;
using InventorAddin.Core.ViewModels;

namespace InventorAddin.UI
{
    /// <summary>
    /// Hole Table Wizard window (spec 04, "Target dialog"; task 167). All behaviour is in
    /// <see cref="HoleTableWizardViewModel"/> and the source behind it; this closes the window when the view-model
    /// reports Cancel, and shows the busy bar while a table command runs.
    /// </summary>
    /// <remarks>
    /// The table commands run on the UI thread, inside the button's click, so the window cannot repaint while they
    /// run. The bar is shown, and drawn once, before the command starts (the button's <c>Click</c> is raised first),
    /// and hidden when the command returns. It does not report a percentage.
    /// </remarks>
    public partial class HoleTableWindow : Window
    {
        public const string TitleText = "Hole Table";

        private bool _closing;

        public HoleTableWindow(HoleTableWizardViewModel viewModel)
        {
            if (viewModel == null)
                throw new ArgumentNullException(nameof(viewModel));

            InitializeComponent();
            Title = TitleText;
            DataContext = viewModel;
            viewModel.CancelRequested += OnCancelRequested;
            Closing += (_, _) => _closing = true;
        }

        private void OnCancelRequested(object? sender, EventArgs e)
        {
            if (!_closing)
                Close();
        }

        /// <summary>Raised before a table command runs: draws the busy bar now and hides it once the command is done.</summary>
        private void OnWorkClick(object sender, RoutedEventArgs e)
        {
            BusyIndicator.Visibility = Visibility.Visible;
            // Lets WPF draw the bar before the command takes the thread.
            Dispatcher.Invoke(DispatcherPriority.Render, new Action(() => { }));
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => BusyIndicator.Visibility = Visibility.Collapsed));
        }
    }
}
