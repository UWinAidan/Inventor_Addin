using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using InventorAddin.Core.ViewModels;
using InventorAddin.Holes;

namespace InventorAddin.UI
{
    /// <summary>
    /// The AWB Hole window (spec 09, "The window"; task 090), shown modeless by <see cref="WindowHost.ShowModeless"/>.
    /// All behaviour is in <see cref="AwbHoleViewModel"/> and the <see cref="HoleSession"/> (task 089), which handles
    /// the view-model's OK and Cancel. This closes the window when the session ends, cancels the session when the
    /// window is closed any other way, and adds the keyboard handling the bindings cannot: Enter commits the box being
    /// typed in and does OK when OK is enabled. Esc presses Cancel through the button's <c>IsCancel</c>.
    /// </summary>
    public partial class AwbHoleWindow : Window
    {
        private readonly AwbHoleViewModel _viewModel;
        private HoleSession? _session;
        private bool _closed;

        public AwbHoleWindow(AwbHoleViewModel viewModel)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            InitializeComponent();
            Title = _viewModel.HeaderTitle;
            DataContext = _viewModel;
            PreviewKeyDown += OnPreviewKeyDown;
            Loaded += OnLoaded;
            Closing += OnClosing;
            Closed += OnClosed;
        }

        /// <summary>
        /// The session this window shows. The window closes when it ends, by any way out (OK, Cancel, Inventor closing
        /// the part or stopping the picking); the close is deferred, since <see cref="HoleSession.Ended"/> can be raised
        /// inside Inventor's events (task 089).
        /// </summary>
        internal void Attach(HoleSession session)
        {
            if (_session != null)
                throw new InvalidOperationException("The AWB Hole window already shows a session.");

            _session = session ?? throw new ArgumentNullException(nameof(session));
            _session.Ended += OnSessionEnded;
        }

        /// <summary>The session, while the window is open.</summary>
        internal HoleSession? Session => _session;

        /// <summary>Brings the window to the front, as when AWB Hole is pressed while it is open.</summary>
        internal void BringToFront()
        {
            if (_closed)
                return;

            if (WindowState == WindowState.Minimized)
                WindowState = WindowState.Normal;
            Activate();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OnLoaded;
            UnitsBox.Focus();
        }

        private void OnSessionEnded(object? sender, HoleSessionEndedEventArgs e)
        {
            // Not inside Inventor's event (spike 081 round 3): after it has returned.
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!_closed)
                    Close();
            }));
        }

        // Closed by its close button, Alt+F4 or the add-in shutting down: the part goes back as it was.
        private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            HoleSession? session = _session;
            if (session != null && !session.IsEnded)
                session.Cancel();
        }

        private void OnClosed(object? sender, EventArgs e)
        {
            _closed = true;
            if (_session != null)
                _session.Ended -= OnSessionEnded;
            PreviewKeyDown -= OnPreviewKeyDown;
            Closing -= OnClosing;
            Closed -= OnClosed;
        }

        /// <summary>
        /// Enter does OK only when OK is enabled, after committing the box being typed in (its binding updates when it
        /// loses focus). A focused button takes Enter itself, and an open dropdown uses it to pick an entry.
        /// </summary>
        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter || Keyboard.Modifiers != ModifierKeys.None)
                return;
            if (e.OriginalSource is ButtonBase || e.OriginalSource is ComboBoxItem)
                return;
            if (e.OriginalSource is ComboBox { IsDropDownOpen: true })
                return;

            e.Handled = true;
            if (e.OriginalSource is TextBox box)
                box.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();

            if (_viewModel.OkCommand.CanExecute(null))
                _viewModel.OkCommand.Execute(null);
        }
    }
}
