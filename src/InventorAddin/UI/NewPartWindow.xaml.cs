using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using InventorAddin.Core.ViewModels;

namespace InventorAddin.UI
{
    /// <summary>
    /// New Part dialog (spec 02). All behaviour is in <see cref="NewPartViewModel"/>; this only closes the window when
    /// the view-model asks (Create), and shows the folder picker for Browse. Cancel closes through the button's
    /// <c>IsCancel</c>, leaving <see cref="NewPartViewModel.Request"/> null.
    /// </summary>
    public partial class NewPartWindow : Window
    {
        private readonly NewPartViewModel _viewModel;

        public NewPartWindow(NewPartViewModel viewModel)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            InitializeComponent();
            Title = NewPartViewModel.HeaderTitleText;
            DataContext = _viewModel;
            _viewModel.CloseRequested += OnCloseRequested;
            Closed += OnClosed;
            Loaded += OnLoaded;
        }

        // The part name is the field to fill first.
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OnLoaded;
            PartNameBox.Focus();
        }

        /// <summary>
        /// Browse beside the folder: picks a folder and puts it in the box. The picker starts in the folder in the box
        /// when that is a full path. Nothing here checks the disk: spike 032 measured Directory.Exists blocking 8 to 42 s
        /// on a disconnected drive, so a folder that does not exist is left to the picker. If the picker fails on that
        /// folder, it is shown again without a starting folder, as in Settings.
        /// </summary>
        private void OnBrowseFolder(object sender, RoutedEventArgs e)
        {
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
                Title = "Choose the folder for the new file",
                Multiselect = false,
            };

            if (initialDirectory is not null)
                dialog.InitialDirectory = initialDirectory;

            bool picked = dialog.ShowDialog(this) == true;
            cancelled = !picked;
            return picked ? dialog.FolderName : null;
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
