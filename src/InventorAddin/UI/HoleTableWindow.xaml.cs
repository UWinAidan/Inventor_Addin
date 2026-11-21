using System;
using System.Linq;
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

        private readonly HoleTableWizardViewModel _viewModel;
        private bool _closing;
        private bool _checked;

        public HoleTableWindow(HoleTableWizardViewModel viewModel)
        {
            if (viewModel == null)
                throw new ArgumentNullException(nameof(viewModel));

            InitializeComponent();
            Title = TitleText;
            _viewModel = viewModel;
            DataContext = viewModel;
            ContentRendered += OnContentRendered;
            viewModel.CancelRequested += OnCancelRequested;
            Closing += (_, _) => _closing = true;
        }

        private void OnCancelRequested(object? sender, EventArgs e)
        {
            if (!_closing)
                Close();
        }

        /// <summary>
        /// Task 172: once the window is on screen, shows the busy bar, then checks every stored view's tables against the
        /// model. The check writes nothing to the drawing (the source lists only keyed views). A message panel at the top of
        /// the body shows only when something is out of date or could not be checked.
        /// </summary>
        private void OnContentRendered(object? sender, EventArgs e)
        {
            if (_checked)
                return;
            _checked = true;

            BusyIndicator.Visibility = Visibility.Visible;
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                try
                {
                    _viewModel.CheckStoredTables();
                    LogCheck();
                }
                catch (Exception ex)
                {
                    AddinServices.Log.Warn("Hole table: the check of the stored tables failed: " + ex.Message);
                }
                finally
                {
                    BusyIndicator.Visibility = Visibility.Collapsed;
                }
            }));
        }

        private void LogCheck()
        {
            int outOfDate = _viewModel.OutOfDateViews.Count;
            int couldNot = _viewModel.CouldNotCheckViews.Count;
            if (outOfDate == 0 && couldNot == 0)
            {
                AddinServices.Log.Info("Hole table: every stored table is up to date.");
                return;
            }

            if (outOfDate > 0)
                AddinServices.Log.Info($"Hole table: {outOfDate} {(outOfDate == 1 ? "view" : "views")} out of date: "
                    + string.Join("; ", _viewModel.OutOfDateViews.Select(v => v.Text)));
            if (couldNot > 0)
                AddinServices.Log.Info($"Hole table: {couldNot} {(couldNot == 1 ? "view" : "views")} could not be checked: "
                    + string.Join("; ", _viewModel.CouldNotCheckViews.Select(v => v.Text)));
        }

        /// <summary>Update these views: the busy bar as for any table command, then the per-view results go to the log.</summary>
        private void OnUpdateStoredClick(object sender, RoutedEventArgs e)
        {
            OnWorkClick(sender, e);
            // Queued after the command (which runs on this click) and after the bar is hidden.
            Dispatcher.BeginInvoke(DispatcherPriority.Background,
                new Action(() => AddinServices.Log.Info("Hole table: Update of the out-of-date views: " + _viewModel.Status)));
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
