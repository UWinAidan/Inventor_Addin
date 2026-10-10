using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using InventorAddin.Core.ViewModels;

namespace InventorAddin.UI
{
    /// <summary>
    /// Mass Update window (spec 02, "Commands: Mass Update"; task 048). All behaviour is in
    /// <see cref="MassUpdateViewModel"/> and <c>MassUpdateCommand</c>; this shows the folder picker for Browse and
    /// refuses to close while a scan or apply runs (the progress pump lets input through): closing during a scan asks it
    /// to stop instead. Close closes through the button's <c>IsCancel</c>.
    /// </summary>
    public partial class MassUpdateWindow : Window
    {
        private readonly MassUpdateViewModel _viewModel;

        public MassUpdateWindow(MassUpdateViewModel viewModel)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            InitializeComponent();
            Title = MassUpdateViewModel.HeaderTitleText;
            DataContext = _viewModel;
            Closing += OnClosing;
            Closed += OnClosed;
            Loaded += OnLoaded;
        }

        // The folder is pre-filled with the Work folder; focus it so Enter scans straight away.
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OnLoaded;
            FolderBox.Focus();
            FolderBox.SelectAll();
        }

        private void OnClosing(object? sender, CancelEventArgs e)
        {
            if (!_viewModel.IsBusy)
                return;

            e.Cancel = true;
            if (_viewModel.CancelCommand.CanExecute(null))
                _viewModel.CancelCommand.Execute(null);
        }

        /// <summary>
        /// Browse beside the folder: picks a folder and puts it in the box, as in New Part. The picker starts in the folder
        /// in the box when that is a full path; nothing here checks the disk (spike 032: Directory.Exists can block 8 to
        /// 42 s on a disconnected drive). If the picker fails on that folder, it is shown again without a starting folder.
        /// </summary>
        private void OnBrowseFolder(object sender, RoutedEventArgs e)
        {
            if (_viewModel.IsBusy)
                return;

            string? chosen = null;
            string start = _viewModel.Folder.Trim();
            if (start.Length > 0 && SettingsViewModel.ValidateWorkFolder(start) is null)
            {
                try
                {
                    chosen = ShowFolderPicker(start, out bool cancelled);
                    if (cancelled)
                        return;
                }
                catch (Exception ex) when (ex is ExternalException or IOException or UnauthorizedAccessException)
                {
                    // Fall through to a picker with no starting folder.
                }
            }

            chosen ??= ShowFolderPicker(null, out _);
            if (chosen is not null)
                _viewModel.Folder = chosen;
        }

        private string? ShowFolderPicker(string? initialDirectory, out bool cancelled)
        {
            // OpenFolderDialog is new in .NET 8 WPF (PresentationFramework).
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Choose the folder to scan",
                Multiselect = false,
            };

            if (initialDirectory is not null)
                dialog.InitialDirectory = initialDirectory;

            bool picked = dialog.ShowDialog(this) == true;
            cancelled = !picked;
            return picked ? dialog.FolderName : null;
        }

        private void OnClosed(object? sender, EventArgs e)
        {
            Closing -= OnClosing;
            Closed -= OnClosed;
        }
    }
}
