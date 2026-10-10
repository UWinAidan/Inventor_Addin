using System;
using System.Collections.Generic;
using System.Windows;
using InventorAddin.Core.ViewModels;

namespace InventorAddin.UI
{
    /// <summary>
    /// Part Properties dialog (spec 07). All behaviour is in <see cref="PartPropertiesViewModel"/>; this only
    /// closes the window when the view-model asks (OK), and shows duplicate part numbers found by Generate in a message
    /// box. Cancel closes through the button's <c>IsCancel</c>.
    /// </summary>
    public partial class PartPropertiesWindow : Window
    {
        private readonly PartPropertiesViewModel _viewModel;

        public PartPropertiesWindow(PartPropertiesViewModel viewModel)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            InitializeComponent();
            Title = "Part Properties";
            DataContext = _viewModel;
            _viewModel.CloseRequested += OnCloseRequested;
            _viewModel.DuplicatesReported += OnDuplicatesReported;
            Closed += OnClosed;
        }

        // Setting DialogResult closes a window shown with ShowDialog.
        private void OnCloseRequested(object? sender, EventArgs e) => DialogResult = true;

        // The register has already logged each duplicate as a warning.
        private void OnDuplicatesReported(object? sender, IReadOnlyList<string> duplicates) =>
            MessageBox.Show(this, PartPropertiesViewModel.FormatDuplicates(duplicates), Title,
                MessageBoxButton.OK, MessageBoxImage.Warning);

        private void OnClosed(object? sender, EventArgs e)
        {
            _viewModel.CloseRequested -= OnCloseRequested;
            _viewModel.DuplicatesReported -= OnDuplicatesReported;
            Closed -= OnClosed;
        }
    }
}
