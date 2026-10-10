using System;
using System.IO;
using System.Windows;
using InventorAddin.Core;
using InventorAddin.Core.ViewModels;

namespace InventorAddin.UI
{
    /// <summary>
    /// Settings dialog. All behaviour is in <see cref="SettingsViewModel"/>; this only closes the window
    /// once the view-model reports a successful save, and shows the folder picker for the register folder.
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

        /// <summary>
        /// Browse: picks a folder and puts it in the register folder box. The view-model trims and validates it like
        /// typed text. The picker starts in the folder in the box when that is a valid full path that exists.
        /// </summary>
        private void OnBrowseRegisterFolder(object sender, RoutedEventArgs e)
        {
            // OpenFolderDialog is new in .NET 8 WPF (PresentationFramework).
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Choose the register folder",
                Multiselect = false,
            };

            var current = _viewModel.RegisterFolder.Trim();
            if (current.Length > 0 && SettingsViewModel.ValidateRegisterFolder(current) is null && Directory.Exists(current))
                dialog.InitialDirectory = current;

            if (dialog.ShowDialog(this) == true)
                _viewModel.RegisterFolder = dialog.FolderName;
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
