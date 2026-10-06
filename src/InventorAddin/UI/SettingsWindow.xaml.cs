using System;
using System.Windows;
using InventorAddin.Core;
using InventorAddin.Core.ViewModels;

namespace InventorAddin.UI
{
    /// <summary>
    /// Settings dialog. All behaviour is in <see cref="SettingsViewModel"/>; this only closes the window
    /// once the view-model reports a successful save.
    /// </summary>
    public partial class SettingsWindow : Window
    {
        private readonly SettingsViewModel _viewModel;

        public SettingsWindow(SettingsViewModel viewModel)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            InitializeComponent();
            Title = $"{Branding.ProductName} Settings";
            DataContext = _viewModel;
            _viewModel.Saved += OnSaved;
            Closed += OnClosed;
        }

        // Setting DialogResult closes a window shown with ShowDialog.
        private void OnSaved(object? sender, EventArgs e) => DialogResult = true;

        private void OnClosed(object? sender, EventArgs e)
        {
            _viewModel.Saved -= OnSaved;
            Closed -= OnClosed;
        }
    }
}
