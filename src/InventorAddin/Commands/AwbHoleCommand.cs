using System;
using System.Collections.Generic;
using System.ComponentModel;
using Inventor;
using InventorAddin.Core.Holes;
using InventorAddin.Core.Logging;
using InventorAddin.Core.Models;
using InventorAddin.Core.Ribbon;
using InventorAddin.Core.Settings;
using InventorAddin.Core.ViewModels;
using InventorAddin.Holes;
using InventorAddin.UI;
using Forms = System.Windows.Forms;
using IOPath = System.IO.Path;

namespace InventorAddin.Commands
{
    /// <summary>
    /// AWB Hole (spec 09, "The command"; task 090): opens the modeless AWB Hole window on the active part, with the
    /// default hole centres picked and the live preview showing. The window and its <see cref="HoleSession"/> are
    /// <see cref="AwbHoleWindowLauncher"/>; pressing the button while the window is open brings it to the front.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The session runs its own transaction for as long as the window is open (one Undo step on OK), so the command
    /// starts none, and it is an <see cref="AddinCommand"/>, not a <see cref="DocumentEditCommand"/>. The command makes
    /// and changes hole features, so its type is <c>kShapeEditCmdType</c>. It runs on the document being edited
    /// (<c>ActiveEditDocument</c>), which must be a part: the part open in the window, or a part edited in place inside
    /// an assembly (spike 103; task 105). In place, the session's transactions, picking and highlight are on the part,
    /// and one Undo in the assembly window reverts it.
    /// </para>
    /// <para>
    /// Every press first tells the launcher (<see cref="AwbHoleWindowLauncher.CommandPressed"/>), whatever the document
    /// check then says: the open session's <see cref="HolePickingWatch"/> recorded the press, and only a resume clears
    /// it. Without that, a refused press would leave it recorded, and a later Esc or another command would restart the
    /// picking instead of cancelling, letting that command run inside the session's transaction.
    /// </para>
    /// </remarks>
    public sealed class AwbHoleCommand : AddinCommand
    {
        public override string InternalName => CommandNames.AwbHole;
        public override string DisplayName => AwbHoleViewModel.CommandName;
        public override string Description =>
            "Make a hole from the hole workbook at the picked sketch points, with a live preview.";

        protected override CommandTypesEnum CommandType => CommandTypesEnum.kShapeEditCmdType;

        protected override void Execute()
        {
            AwbHoleWindowLauncher.CommandPressed();

            // In a part's own window this is the part; in place, the part being edited, not the assembly.
            Document? doc = ComSafe.Get(() => InventorHost.ActiveEditDocument);
            DocumentKind kind = doc == null ? DocumentKind.Unknown : DocumentKinds.Of(doc);
            if (doc == null || !kind.IsPart())
            {
                AddinServices.Log.Info($"Command {InternalName} not run: document kind {kind} is not accepted.");
                Forms.MessageBox.Show(AwbHoleViewModel.WrongDocumentText, DisplayName,
                    Forms.MessageBoxButtons.OK, Forms.MessageBoxIcon.Information);
                return;
            }

            AwbHoleWindowLauncher.ShowNew((PartDocument)doc);
        }
    }

    /// <summary>
    /// Opens the AWB Hole window and its session: for a new hole (<see cref="ShowNew"/>, the command) or on an
    /// existing hole (<see cref="ShowForEdit"/>, for task 092's Edit Feature and double-click). Only one window is open
    /// at a time; a request while it is open brings it to the front and restarts the hole centre picking if Inventor
    /// stopped it. Every request opens the window after Inventor's event or command has returned
    /// (<c>Dispatcher.BeginInvoke</c>): opened inside one, a window shows but takes no input (spike 081 round 3).
    /// </summary>
    internal static class AwbHoleWindowLauncher
    {
        private const string LogPrefix = AwbHoleViewModel.CommandName + ": ";

        private static AwbHoleWindow? _window;
        private static bool _requested;

        /// <summary>True while the window is open.</summary>
        public static bool IsOpen => _window != null;

        /// <summary>A new hole in <paramref name="doc"/>, at the default centres.</summary>
        public static void ShowNew(PartDocument doc)
        {
            if (doc == null)
                throw new ArgumentNullException(nameof(doc));
            Request(doc, null);
        }

        /// <summary>
        /// Edit mode on <paramref name="hole"/>, a hole feature of <paramref name="doc"/>: its settings are read first
        /// (<see cref="HoleFeatureReader.ReadForEdit"/>), from its stored AWB data or, for a hole made by Inventor's Hole,
        /// the closest settings with a note that OK makes it an AWB hole. While the window is open (on any hole), it is
        /// brought to the front instead and false is returned, so the caller can say so; true means the window will
        /// open (or say why it cannot).
        /// </summary>
        public static bool ShowForEdit(PartDocument doc, HoleFeature hole)
        {
            if (doc == null)
                throw new ArgumentNullException(nameof(doc));
            if (hole == null)
                throw new ArgumentNullException(nameof(hole));

            // A request already queued (the window about to open) counts as open: this one would be dropped.
            bool open = IsOpen || _requested;
            Request(doc, hole);
            return !open;
        }

        /// <summary>
        /// AWB Hole was pressed (its button or shortcut), before the document check. While a session is open, its
        /// <see cref="HoleSession.ResumePicking"/> is queued for after Inventor's command has returned: that restarts the
        /// picking if the press stopped it, and in every case clears the press the session recorded, so a refused
        /// press (no part being edited) cannot turn a later Esc or command into a restart.
        /// </summary>
        public static void CommandPressed()
        {
            HoleSession? running = _window?.Session;
            if (running == null || running.IsEnded)
                return;

            System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    running.ResumePicking();
                }
                catch (Exception ex)
                {
                    AddinServices.Log.Error(LogPrefix + "resuming the hole centre picking failed.", ex);
                }
            }));
        }

        /// <summary>The add-in is unloading: an open session is cancelled, so the part is as it was, and the window closed.</summary>
        public static void Shutdown()
        {
            AwbHoleWindow? window = _window;
            _window = null;
            if (window == null)
                return;

            try
            {
                window.Session?.Dispose();
                window.Close();
            }
            catch (Exception ex)
            {
                AddinServices.Log.Error(LogPrefix + "closing the window as the add-in stops failed.", ex);
            }
        }

        private static void Request(PartDocument doc, HoleFeature? hole)
        {
            if (_requested)
                return;

            _requested = true;
            System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() =>
            {
                _requested = false;
                try
                {
                    OpenNow(doc, hole);
                }
                catch (Exception ex)
                {
                    AddinServices.Log.Error(LogPrefix + "opening the window failed.", ex);
                    string text = ErrorMessages.CommandFailed(AwbHoleViewModel.CommandName, ex, AddinServices.LogFilePath);
                    ShowMessage(text, Forms.MessageBoxIcon.Error);
                }
            }));
        }

        private static void OpenNow(PartDocument doc, HoleFeature? hole)
        {
            AwbHoleWindow? open = _window;
            if (open != null)
            {
                HoleSession? running = open.Session;
                if (running != null && !running.IsEnded)
                {
                    AddinServices.Log.Info(LogPrefix + "the window is open; it is brought to the front.");
                    open.BringToFront();
                    running.ResumePicking();
                    return;
                }

                // The session has ended and the window's close is still queued: close it now and open a new one.
                _window = null;
                open.Close();
            }

            ILog log = AddinServices.Log;
            HoleWorkbookReadResult read;
            Forms.Cursor? previousCursor = Forms.Cursor.Current;
            Forms.Cursor.Current = Forms.Cursors.WaitCursor;
            try
            {
                read = HoleWorkbookLoader.Load(AddinServices.Settings, log);
            }
            finally
            {
                Forms.Cursor.Current = previousCursor;
            }

            string? stop = AwbHoleViewModel.WorkbookStopMessage(read);
            if (stop != null)
            {
                log.Warn(LogPrefix + "the window is not opened: " + stop);
                ShowMessage(stop, Forms.MessageBoxIcon.Warning);
                return;
            }

            string partName = FileName(doc);
            AwbHoleViewModel viewModel;
            HoleLastDepths lastDepths = HoleLastDepths.From(AddinServices.Settings);
            HoleLastDowelSeats lastDowelSeats = HoleLastDowelSeats.From(AddinServices.Settings);
            var expressionChecker = new HoleExpressionChecker(doc);
            if (hole == null)
            {
                viewModel = new AwbHoleViewModel(
                    read.Workbook, partName, read.Problems, lastDepths, expressionChecker, lastDowelSeats);
            }
            else
            {
                var warnings = new List<string>();
                (HoleReadBackResult readBack, HoleReapplyOffer reapply) = HoleFeatureReader.ReadForEdit(
                    hole, read.Workbook, read.Workbook.Settings.DefaultUnits, expressionChecker, warnings);
                foreach (string warning in warnings)
                    log.Warn(LogPrefix + warning);

                // An AWB hole changed outside AWB Hole (task 092): the window says what differs; OK re-applies.
                if (reapply.IsOffered)
                {
                    string holeName = ComSafe.Get(() => hole.Name) ?? "the hole";
                    log.Info(LogPrefix + $"{holeName} no longer matches its stored data, so re-applying is offered: " +
                             string.Join(" ", reapply.Differences));
                }

                viewModel = AwbHoleViewModel.ForReadBack(
                    read.Workbook, partName, readBack, read.Problems, lastDepths, expressionChecker, lastDowelSeats,
                    reapply);
            }

            // The diagram opens as it was left (task 104), and is remembered whenever it is opened or collapsed.
            viewModel.IsDiagramExpanded = HoleDiagramState.IsExpanded(AddinServices.Settings);
            viewModel.PropertyChanged += OnViewModelPropertyChanged;

            // The session previews a new hole at once; if it cannot start, it has put everything back and throws.
            HoleSession session = HoleSession.Start(InventorHost.App, doc, viewModel, hole, log);

            AwbHoleWindow window;
            try
            {
                window = new AwbHoleWindow(viewModel);
                window.Attach(session);
                window.Closed += OnWindowClosed;
                _window = window;
                WindowHost.ShowModeless(window, IconNames.AwbHole);
            }
            catch (Exception)
            {
                _window = null;
                session.Cancel();
                throw;
            }
        }

        private static void OnWindowClosed(object? sender, EventArgs e)
        {
            if (sender is AwbHoleWindow window)
            {
                window.Closed -= OnWindowClosed;
                if (window.DataContext is AwbHoleViewModel viewModel)
                    viewModel.PropertyChanged -= OnViewModelPropertyChanged;
                if (ReferenceEquals(_window, window))
                    _window = null;
            }
        }

        private static void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AwbHoleViewModel.IsDiagramExpanded) && sender is AwbHoleViewModel viewModel)
                RememberDiagramState(viewModel.IsDiagramExpanded);
        }

        /// <summary>
        /// Keeps whether the diagram is open in the settings file (task 104), so the next window, also after a restart,
        /// opens it the same way. A failure to save is logged; the window carries on either way.
        /// </summary>
        private static void RememberDiagramState(bool expanded)
        {
            try
            {
                AddinSettings? updated = HoleDiagramState.Remember(AddinServices.Settings, expanded);
                if (updated == null)
                    return;

                AddinServices.UpdateSettings(updated);
                string? folder = AddinServices.DataFolder;
                if (folder == null)
                {
                    AddinServices.Log.Warn(LogPrefix + "the diagram's state is kept for this session only: the add-in has no data folder.");
                    return;
                }

                new SettingsStore(folder).Save(updated);
            }
            catch (Exception ex)
            {
                AddinServices.Log.Error(LogPrefix + "saving the diagram's state to the settings file failed.", ex);
            }
        }

        /// <summary>The part's file name, or its display name when it has never been saved.</summary>
        private static string FileName(PartDocument doc)
        {
            string path = ComSafe.Get(() => doc.FullFileName) ?? string.Empty;
            string name = path.Length == 0 ? string.Empty : IOPath.GetFileName(path);
            return name.Length > 0 ? name : ComSafe.Get(() => doc.DisplayName) ?? string.Empty;
        }

        private static void ShowMessage(string text, Forms.MessageBoxIcon icon) =>
            Forms.MessageBox.Show(text, AwbHoleViewModel.CommandName, Forms.MessageBoxButtons.OK, icon);
    }
}
