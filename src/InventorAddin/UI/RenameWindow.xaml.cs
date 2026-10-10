using System;
using System.ComponentModel;
using System.Windows;
using InventorAddin.Core.ViewModels;

namespace InventorAddin.UI
{
    /// <summary>
    /// Rename dialog (spec 02, "Commands: Rename"; task 047). All behaviour is in <see cref="RenameViewModel"/>; this
    /// selects the pre-filled name, closes when the view-model asks, and refuses to close while the rename runs (the
    /// progress pump in <c>RenameService</c> lets input through). Cancel and Close close through the button's
    /// <c>IsCancel</c>.
    /// </summary>
    public partial class RenameWindow : Window
    {
        private readonly RenameViewModel _viewModel;

        public RenameWindow(RenameViewModel viewModel)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            InitializeComponent();
            Title = _viewModel.HeaderTitle;
            DataContext = _viewModel;
            _viewModel.CloseRequested += OnCloseRequested;
            Closing += OnClosing;
            Closed += OnClosed;
            Loaded += OnLoaded;
        }

        // The new name is pre-filled with the current one; select it so typing replaces it.
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OnLoaded;
            NewNameBox.Focus();
            NewNameBox.SelectAll();
        }

        private void OnClosing(object? sender, CancelEventArgs e)
        {
            if (!_viewModel.CanClose)
                e.Cancel = true;
        }

        // Setting DialogResult closes a window shown with ShowDialog.
        private void OnCloseRequested(object? sender, EventArgs e) => DialogResult = true;

        private void OnClosed(object? sender, EventArgs e)
        {
            _viewModel.CloseRequested -= OnCloseRequested;
            Closing -= OnClosing;
            Closed -= OnClosed;
        }
    }
}
