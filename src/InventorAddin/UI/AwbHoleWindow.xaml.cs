using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
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
    /// Text boxes apply as you type (task 130): their bindings update on each keystroke, and the view-model applies the
    /// text after a pause (<see cref="Debouncer"/>) on the timer this window gives it; leaving a box commits it at once.
    /// </summary>
    public partial class AwbHoleWindow : Window
    {
        private readonly AwbHoleViewModel _viewModel;
        private HoleSession? _session;
        private bool _closed;
        private bool _countersinkCustomChosen;

        public AwbHoleWindow(AwbHoleViewModel viewModel)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            InitializeComponent();
            Title = _viewModel.HeaderTitle;
            DataContext = _viewModel;
            _viewModel.UseTypingTimer(new DispatcherDebounceTimer(Dispatcher));
            AddHandler(UIElement.LostFocusEvent, new RoutedEventHandler(OnBoxLostFocus));
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
            if (!UnitsList.FocusChosen())
                UnitsList.Focus();
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
            // Text still waiting for the pause is not applied to a hole that is going away.
            _viewModel.DiscardTyping();
            HoleSession? session = _session;
            if (session != null && !session.IsEnded)
                session.Cancel();
        }

        private void OnClosed(object? sender, EventArgs e)
        {
            _closed = true;
            _viewModel.DiscardTyping();
            if (_session != null)
                _session.Ended -= OnSessionEnded;
            RemoveHandler(UIElement.LostFocusEvent, new RoutedEventHandler(OnBoxLostFocus));
            PreviewKeyDown -= OnPreviewKeyDown;
            Closing -= OnClosing;
            Closed -= OnClosed;
        }

        /// <summary>
        /// Leaving a text box applies its text at once, without waiting for the pause (task 130). The countersink
        /// angle's editable dropdown raises this from the text box inside it.
        /// </summary>
        private void OnBoxLostFocus(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is TextBox)
                _viewModel.CommitTyping();
        }

        /// <summary>
        /// Alt+U focuses the Units toggle (task 118).
        /// Enter does OK only when OK is enabled, after committing the box being typed in at once (task 130). A focused
        /// button takes Enter itself, and an open dropdown uses it to pick an entry. In an editable dropdown (the
        /// countersink angle, task 127) the keys go to the text box inside it, and the binding is on the dropdown's Text.
        /// </summary>
        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Alt+U goes to the chosen Units part. Units sits on the header's line with no label (task 118), so no
            // label's underscore gives it the access key (registering the list itself with AccessKeyManager did nothing).
            if (e.Key == Key.System && e.SystemKey == Key.U && Keyboard.Modifiers == ModifierKeys.Alt)
            {
                e.Handled = true;
                UnitsList.FocusChosen();
                return;
            }

            if (e.Key != Key.Enter || Keyboard.Modifiers != ModifierKeys.None)
                return;
            if (e.OriginalSource is ButtonBase || e.OriginalSource is ComboBoxItem)
                return;
            if (e.OriginalSource is ComboBox { IsDropDownOpen: true })
                return;
            ComboBox? editable = (e.OriginalSource as TextBox)?.TemplatedParent as ComboBox;
            if (editable != null && editable.IsDropDownOpen)
                return;

            e.Handled = true;
            if (editable != null)
                editable.GetBindingExpression(ComboBox.TextProperty)?.UpdateSource();
            else if (e.OriginalSource is TextBox box)
                box.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            _viewModel.CommitTyping();

            if (_viewModel.OkCommand.CanExecute(null))
                _viewModel.OkCommand.Execute(null);
        }

        /// <summary>
        /// Notes when "Custom..." (task 146) is picked from the open countersink angle list, for
        /// <see cref="OnCountersinkAngleDropDownClosed"/>. Read here, not from <c>SelectedItem</c> when the list closes:
        /// the view-model refuses the text and the box reads the angle back, which can move the selection off
        /// "Custom..." first. Stepping through the closed box with the arrow keys sets nothing.
        /// </summary>
        private void OnCountersinkAngleSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            // A change that only removes the selection (the angle read back while the list is open) keeps the flag.
            if (!CountersinkAngleBox.IsDropDownOpen || e.AddedItems.Count == 0)
                return;
            _countersinkCustomChosen = AwbHoleViewModel.IsCountersinkAngleCustom(e.AddedItems[0] as string);
        }

        /// <summary>
        /// An angle picked from the countersink angle's dropdown (task 127) commits at once, so a centre-drill spot is
        /// two clicks after choosing Countersink; a typed angle applies after the pause like every box (task 130), or at
        /// once when the box loses focus or on Enter.
        /// "Custom..." (task 146) is not an angle: the view-model refuses it as text, so the box shows the current angle
        /// again, nothing is committed, the selection is cleared, and the box gets focus with its text selected so
        /// typing replaces it.
        /// </summary>
        private void OnCountersinkAngleDropDownClosed(object? sender, EventArgs e)
        {
            if (_countersinkCustomChosen)
            {
                _countersinkCustomChosen = false;
                ClearCountersinkAngleSelection();
                // After the dropdown has finished closing, which hands focus back to the dropdown itself.
                Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(FocusCountersinkAngleText));
                return;
            }

            CountersinkAngleBox.GetBindingExpression(ComboBox.TextProperty)?.UpdateSource();
            _viewModel.CommitTyping();
        }

        /// <summary>
        /// Takes the selection off "Custom..." so it is not left highlighted. An editable dropdown with no selection
        /// empties its Text, so the Text binding is lifted while the selection is cleared and set again after, which
        /// reads the view-model's angle back without sending the empty text to it.
        /// </summary>
        private void ClearCountersinkAngleSelection()
        {
            BindingBase? binding = BindingOperations.GetBindingBase(CountersinkAngleBox, ComboBox.TextProperty);
            if (binding != null)
                BindingOperations.ClearBinding(CountersinkAngleBox, ComboBox.TextProperty);
            try
            {
                CountersinkAngleBox.SelectedIndex = -1;
            }
            finally
            {
                if (binding != null)
                    CountersinkAngleBox.SetBinding(ComboBox.TextProperty, binding);
            }
        }

        /// <summary>The cursor in the countersink angle's text box, its text selected (task 146).</summary>
        private void FocusCountersinkAngleText()
        {
            if (_closed)
                return;

            if (CountersinkAngleBox.Template?.FindName("PART_EditableTextBox", CountersinkAngleBox) is TextBox text)
            {
                text.Focus();
                text.SelectAll();
            }
            else
            {
                CountersinkAngleBox.Focus();
            }
        }
    }
}
