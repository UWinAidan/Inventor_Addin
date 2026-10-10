using System;
using System.Windows;
using InventorAddin.Core.ViewModels;

namespace InventorAddin.UI
{
    /// <summary>
    /// Save As Copy dialog (spec 02). All behaviour is in <see cref="SaveAsCopyViewModel"/>; this only closes the window
    /// when the view-model asks (Save). Cancel closes through the button's <c>IsCancel</c>, leaving
    /// <see cref="SaveAsCopyViewModel.Request"/> null.
    /// </summary>
    public partial class SaveAsCopyWindow : Window
    {
        private readonly SaveAsCopyViewModel _viewModel;

        public SaveAsCopyWindow(SaveAsCopyViewModel viewModel)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            InitializeComponent();
            Title = SaveAsCopyViewModel.HeaderTitleText;
            DataContext = _viewModel;
            _viewModel.CloseRequested += OnCloseRequested;
            Closed += OnClosed;
            Loaded += OnLoaded;
        }

        // The part name is pre-filled from the original; select it so typing replaces it.
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OnLoaded;
            PartNameBox.Focus();
            PartNameBox.SelectAll();
        }

        // Setting DialogResult closes a window shown with ShowDialog.
        private void OnCloseRequested(object? sender, EventArgs e) => DialogResult = true;

        private void OnClosed(object? sender, EventArgs e)
        {
            _viewModel.CloseRequested -= OnCloseRequested;
            Closed -= OnClosed;
        }
    }
}
