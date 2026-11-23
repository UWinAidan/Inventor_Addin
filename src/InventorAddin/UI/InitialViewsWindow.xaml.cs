using System;
using System.Windows;
using InventorAddin.Core.Drawing;
using InventorAddin.Core.Ribbon;
using InventorAddin.Core.ViewModels;

namespace InventorAddin.UI
{
    /// <summary>
    /// The Initial Views window (spec 05, "Decided for M5: Place Initial Views"; task 192). All behaviour is in
    /// <see cref="InitialViewsViewModel"/>; this closes when the view-model asks. <see cref="Ask"/> shows it over Inventor.
    /// </summary>
    public partial class InitialViewsWindow : Window
    {
        private readonly InitialViewsViewModel _viewModel;

        public InitialViewsWindow(InitialViewsViewModel viewModel)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            InitializeComponent();
            Title = InitialViewsViewModel.HeaderTitleText;
            DataContext = _viewModel;
            _viewModel.CloseRequested += OnCloseRequested;
            Closed += OnClosed;
        }

        /// <summary>Shows the window and returns the plan the user confirmed, or null when they cancelled or closed it.</summary>
        public static InitialViewsResult? Ask(InitialViewsViewModel viewModel)
        {
            WindowHost.ShowDialog(new InitialViewsWindow(viewModel), IconNames.PlaceInitialViews, stateKey: "InitialViews");
            return viewModel.Result;
        }

        // Setting DialogResult closes a window shown with ShowDialog; Result tells OK from Cancel.
        private void OnCloseRequested(object? sender, EventArgs e) => DialogResult = true;

        private void OnClosed(object? sender, EventArgs e)
        {
            _viewModel.CloseRequested -= OnCloseRequested;
            Closed -= OnClosed;
        }
    }
}
