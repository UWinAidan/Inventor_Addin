using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using InventorAddin.Core;
using InventorAddin.Core.ViewModels;

namespace InventorAddin.UI
{
    /// <summary>
    /// Settings dialog. All behaviour is in <see cref="SettingsViewModel"/>; this only closes the window
    /// once the view-model reports a successful save, shows the folder pickers for the register and work folders, and
    /// the file pickers for the material library and finish list, and shows the page for the selected tab.
    /// </summary>
    public partial class SettingsWindow : Window
    {
        private readonly SettingsViewModel _viewModel;

        // The body's pages, in the order of the tabs in the XAML.
        private readonly FrameworkElement[] _pages;

        public SettingsWindow(SettingsViewModel viewModel)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            InitializeComponent();
            _pages = new FrameworkElement[] { GeneralPage, PartNumbersPage, FoldersPage, MaterialFinishPage, DeveloperPage };
            if (Tabs.SelectedIndex < 0)
                Tabs.SelectedIndex = 0;
            ShowPage(Tabs.SelectedIndex);
            PreviewKeyDown += OnPreviewKeyDown;
            Title = $"{Branding.ProductName} Settings";
            DataContext = _viewModel;
            _viewModel.Saved += OnSaved;
            Closed += OnClosed;
        }

        /// <summary>
        /// The tab strip changed tab: show its page. The other pages are Hidden rather than Collapsed so they keep
        /// their space and the window's height stays the same. Hidden controls cannot take focus.
        /// </summary>
        private void OnTabChanged(object sender, SelectionChangedEventArgs e)
        {
            // SelectionChanged bubbles, so ignore any from inside the tab strip that are not the strip's own.
            if (!ReferenceEquals(e.OriginalSource, Tabs))
                return;

            ShowPage(Tabs.SelectedIndex);
        }

        private void ShowPage(int index)
        {
            // The tab strip selects its first tab while the XAML loads, before the pages are known.
            if (_pages is null)
                return;

            for (int i = 0; i < _pages.Length; i++)
                _pages[i].Visibility = i == index ? Visibility.Visible : Visibility.Hidden;
        }

        /// <summary>
        /// Ctrl+Tab and Ctrl+Shift+Tab move to the next and previous tab from anywhere in the window. The tab strip
        /// handles these keys itself only while it has focus, and the pages are outside it.
        /// </summary>
        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Tab || (Keyboard.Modifiers & ModifierKeys.Control) == 0)
                return;

            int count = Tabs.Items.Count;
            int step = (Keyboard.Modifiers & ModifierKeys.Shift) != 0 ? -1 : 1;
            Tabs.SelectedIndex = (Tabs.SelectedIndex + step + count) % count;
            // Focus may have been on a control of the page just hidden; put it on the new tab.
            (Tabs.SelectedItem as TabItem)?.Focus();
            e.Handled = true;
        }

        /// <summary>
        /// Browse beside the register folder: picks a folder and puts it in the box. The view-model trims and validates
        /// it like typed text.
        /// </summary>
        private void OnBrowseRegisterFolder(object sender, RoutedEventArgs e)
        {
            var chosen = PickFolder("Choose the register folder", _viewModel.RegisterFolder, SettingsViewModel.ValidateRegisterFolder);
            if (chosen is not null)
                _viewModel.RegisterFolder = chosen;
        }

        /// <summary>Browse beside the work folder: as <see cref="OnBrowseRegisterFolder"/>, for the work folder.</summary>
        private void OnBrowseWorkFolder(object sender, RoutedEventArgs e)
        {
            var chosen = PickFolder("Choose the work folder", _viewModel.WorkFolder, SettingsViewModel.ValidateWorkFolder);
            if (chosen is not null)
                _viewModel.WorkFolder = chosen;
        }

        /// <summary>
        /// Shows a folder picker and returns the chosen folder, or null when cancelled. The picker starts in the folder
        /// in the box when that is a valid full path. Nothing here checks the disk: spike 032 measured
        /// Directory.Exists blocking 8 to 42 s on a disconnected drive, so a folder that does not exist is left to the
        /// picker. If the picker fails on that folder (the shell cannot make an item for a stale or unmapped path), it
        /// is shown again without a starting folder, so Browse still works and nothing escapes to Inventor.
        /// </summary>
        private string? PickFolder(string title, string current, Func<string?, string?> validate)
        {
            var start = current.Trim();
            if (start.Length > 0 && validate(start) is null)
            {
                try
                {
                    return ShowFolderPicker(title, start);
                }
                catch (Exception ex) when (ex is ExternalException or IOException or UnauthorizedAccessException)
                {
                    // Fall through to a picker with no starting folder.
                }
            }

            return ShowFolderPicker(title, null);
        }

        private string? ShowFolderPicker(string title, string? initialDirectory)
        {
            // OpenFolderDialog is new in .NET 8 WPF (PresentationFramework).
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = title,
                Multiselect = false,
            };

            if (initialDirectory is not null)
                dialog.InitialDirectory = initialDirectory;

            return dialog.ShowDialog(this) == true ? dialog.FolderName : null;
        }

        /// <summary>
        /// Browse beside the material library: picks an .adsklib file and puts it in the box. The view-model trims and
        /// validates it like typed text.
        /// </summary>
        private void OnBrowseMaterialLibrary(object sender, RoutedEventArgs e)
        {
            var chosen = PickFile(
                "Choose the material library",
                "Material libraries (*.adsklib)|*.adsklib",
                _viewModel.MaterialLibraryPath,
                SettingsViewModel.ValidateMaterialLibraryPath);
            if (chosen is not null)
                _viewModel.MaterialLibraryPath = chosen;
        }

        /// <summary>Browse beside the finish list: as <see cref="OnBrowseMaterialLibrary"/>, for a .csv file.</summary>
        private void OnBrowseFinishList(object sender, RoutedEventArgs e)
        {
            var chosen = PickFile(
                "Choose the finish list",
                "Finish lists (*.csv)|*.csv",
                _viewModel.FinishListPath,
                SettingsViewModel.ValidateFinishListPath);
            if (chosen is not null)
                _viewModel.FinishListPath = chosen;
        }

        /// <summary>
        /// Shows an Open File dialog and returns the chosen file, or null when cancelled. The dialog starts in the
        /// folder of the file in the box when that is a valid path, without checking the disk (see
        /// <see cref="PickFolder"/>); if the dialog fails on that folder it is shown again without a starting folder.
        /// </summary>
        private string? PickFile(string title, string filter, string current, Func<string?, string?> validate)
        {
            var start = current.Trim();
            if (start.Length > 0 && validate(start) is null)
            {
                try
                {
                    return ShowFilePicker(title, filter, System.IO.Path.GetDirectoryName(start));
                }
                catch (Exception ex) when (ex is ExternalException or IOException or UnauthorizedAccessException or ArgumentException)
                {
                    // Fall through to a picker with no starting folder.
                }
            }

            return ShowFilePicker(title, filter, null);
        }

        private string? ShowFilePicker(string title, string filter, string? initialDirectory)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = title,
                Filter = filter,
                Multiselect = false,
                CheckFileExists = true,
            };

            if (!string.IsNullOrEmpty(initialDirectory))
                dialog.InitialDirectory = initialDirectory;

            return dialog.ShowDialog(this) == true ? dialog.FileName : null;
        }

        // Setting DialogResult closes a window shown with ShowDialog.
        private void OnSaved(object? sender, EventArgs e) => DialogResult = true;

        private void OnClosed(object? sender, EventArgs e)
        {
            _viewModel.Saved -= OnSaved;
            PreviewKeyDown -= OnPreviewKeyDown;
            Closed -= OnClosed;
        }
    }
}
