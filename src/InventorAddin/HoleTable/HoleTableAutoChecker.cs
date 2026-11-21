using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Inventor;
using InventorAddin.Core.HoleTable;
using InventorAddin.Core.Logging;
using InventorAddin.Core.Ribbon;
using InventorAddin.Core.ViewModels;
using InventorAddin.UI;
using Forms = System.Windows.Forms;

namespace InventorAddin.HoleTables
{
    /// <summary>
    /// The automatic check of a drawing's stored hole tables (task 176; spec 04, "Update"). It watches Inventor for a
    /// drawing becoming active or updating and tells <see cref="HoleTableAutoCheck"/> (Core), which decides when a check
    /// is due and whether its result is shown. A timer, running only while a check is due, gives the policy the state of
    /// Inventor; when it says to run, this runs <see cref="HoleTableStoredCheck"/> and shows the prompt.
    /// </summary>
    /// <remarks>
    /// The event handlers only tell the policy; the check runs from the timer on Inventor's UI thread. The check reads
    /// only keyed sheets and views, so it adds no undo entry and does not mark the drawing changed. Nothing here throws
    /// into Inventor: every handler is in a try/catch with a log line. The Settings switch is read when an event arrives,
    /// so a drawing with the setting off is neither read nor written.
    /// </remarks>
    public sealed class HoleTableAutoChecker : IDisposable
    {
        private const int TickMs = 500;

        private readonly Application _app;
        private readonly ILog _log;
        private readonly HoleTableAutoCheck _policy = new();
        private readonly HashSet<string> _deferralsLogged = new HashSet<string>(StringComparer.Ordinal);

        // Kept referenced, or the events stop firing.
        private ApplicationEvents? _events;
        private Forms.Timer? _timer;
        private int _wizardsOpen;
        private bool _running;

        public HoleTableAutoChecker(Application app, ILog log)
        {
            _app = app;
            _log = log;
        }

        /// <summary>The checker the add-in started, for the Hole Table command to tell about the wizard. Null before Activate and after Deactivate.</summary>
        public static HoleTableAutoChecker? Current { get; private set; }

        /// <summary>Call once, from <c>Activate</c>.</summary>
        public void Start()
        {
            try
            {
                _events = _app.ApplicationEvents;
                _events.OnActivateDocument += OnActivateDocument;
                _events.OnDocumentChange += OnDocumentChange;
                _events.OnCloseDocument += OnCloseDocument;
                Current = this;
            }
            catch (Exception ex)
            {
                _log.Error("Hole table check: watching for drawings failed; the automatic check is off.", ex);
            }
        }

        public void Dispose()
        {
            try
            {
                if (ReferenceEquals(Current, this))
                    Current = null;
                StopTimer();
                if (_events != null)
                {
                    _events.OnActivateDocument -= OnActivateDocument;
                    _events.OnDocumentChange -= OnDocumentChange;
                    _events.OnCloseDocument -= OnCloseDocument;
                    _events = null;
                }
            }
            catch (Exception ex)
            {
                _log.Error("Hole table check: stopping failed.", ex);
            }
        }

        // ---- Events: they only tell the policy.

        private void OnActivateDocument(_Document documentObject, EventTimingEnum beforeOrAfter, NameValueMap context, out HandlingCodeEnum handlingCode)
        {
            handlingCode = HandlingCodeEnum.kEventNotHandled;
            if (beforeOrAfter != EventTimingEnum.kAfter)
                return;
            try
            {
                if (AddinServices.Settings.CheckHoleTablesAutomatically && DrawingNameOf(documentObject) is { } name)
                    Note(name, updated: false);
            }
            catch (Exception ex)
            {
                _log.Error("Hole table check: handling a drawing becoming active failed.", ex);
            }
        }

        private void OnDocumentChange(_Document documentObject, EventTimingEnum beforeOrAfter, CommandTypesEnum reasonsForChange, NameValueMap context, out HandlingCodeEnum handlingCode)
        {
            handlingCode = HandlingCodeEnum.kEventNotHandled;
            if (beforeOrAfter != EventTimingEnum.kAfter || (reasonsForChange & CommandTypesEnum.kUpdateWithReferencesCmdType) == 0)
                return;
            try
            {
                if (AddinServices.Settings.CheckHoleTablesAutomatically && DrawingNameOf(documentObject) is { } name)
                    Note(name, updated: true);
            }
            catch (Exception ex)
            {
                _log.Error("Hole table check: handling a drawing update failed.", ex);
            }
        }

        private void OnCloseDocument(_Document documentObject, string fullDocumentName, EventTimingEnum beforeOrAfter, NameValueMap context, out HandlingCodeEnum handlingCode)
        {
            handlingCode = HandlingCodeEnum.kEventNotHandled;
            if (beforeOrAfter != EventTimingEnum.kAfter)
                return;
            try
            {
                _policy.DrawingClosed(fullDocumentName);
            }
            catch (Exception ex)
            {
                _log.Error("Hole table check: forgetting a closed drawing failed.", ex);
            }
        }

        /// <summary>The drawing's identifying name, or null when the document is not a drawing or its name cannot be read.</summary>
        private static string? DrawingNameOf(_Document document)
        {
            if (ComSafe.Get(() => (DocumentTypeEnum?)document.DocumentType) != DocumentTypeEnum.kDrawingDocumentObject)
                return null;
            string? name = ComSafe.Get(() => document.FullDocumentName);
            return string.IsNullOrEmpty(name) ? null : name;
        }

        private void Note(string drawing, bool updated)
        {
            // The setting is read here, so a drawing with it off is never read or written.
            _policy.Enabled = AddinServices.Settings.CheckHoleTablesAutomatically;
            if (_wizardsOpen > 0)
                return;

            if (updated)
                _policy.DrawingUpdated(drawing, DateTime.UtcNow);
            else
                _policy.DrawingActivated(drawing, DateTime.UtcNow);
            StartTimer();
        }

        // ---- Timer

        private void StartTimer()
        {
            if (_timer != null || !_policy.AnyPending)
                return;
            _timer = new Forms.Timer { Interval = TickMs };
            _timer.Tick += OnTick;
            _timer.Start();
        }

        private void StopTimer()
        {
            if (_timer == null)
                return;
            _timer.Stop();
            _timer.Tick -= OnTick;
            _timer.Dispose();
            _timer = null;
        }

        private void OnTick(object? sender, EventArgs e)
        {
            if (_running)
                return;

            try
            {
                _policy.Enabled = AddinServices.Settings.CheckHoleTablesAutomatically;
                if (!_policy.AnyPending)
                {
                    StopTimer();
                    return;
                }

                HoleTableAutoCheckContext context = ReadContext(out DrawingDocument? drawing);
                string? due = _policy.TakeDue(context, DateTime.UtcNow);
                if (due != null && drawing != null)
                {
                    _running = true;
                    try
                    {
                        CheckAndPrompt(drawing, due);
                    }
                    finally
                    {
                        _running = false;
                    }
                }

                if (!_policy.AnyPending)
                    StopTimer();
            }
            catch (Exception ex)
            {
                _log.Error("Hole table check: the check failed.", ex);
            }
        }

        /// <summary>
        /// What Inventor is doing now. A value that cannot be read counts as "not ready", so the check waits. The reason a
        /// due check is waiting is logged once per reason.
        /// </summary>
        private HoleTableAutoCheckContext ReadContext(out DrawingDocument? drawing)
        {
            drawing = null;
            _Document? active = ComSafe.Get(() => _app.ActiveDocument);
            string? name = null;
            if (active != null && DrawingNameOf(active) is { } n)
            {
                name = n;
                drawing = active as DrawingDocument;
            }

            bool editingItself = false;
            if (active != null)
            {
                object? edit = ComSafe.Get(() => _app.ActiveEditObject);
                // A plain drawing may report the drawing or its sheet; a sketch or anything else means something is being edited.
                editingItself = edit != null
                    && (Equals(edit, active)
                        || (edit is Sheet sheet && Equals(ComSafe.Get(() => sheet.Parent), active)));
                if (name != null && !editingItself && edit != null)
                    LogWaiting("the active edit object is a " + (ComSafe.Get(() => edit.GetType().Name) ?? "unknown type"));
            }

            CommandIDEnum? command = ComSafe.Get(() => (CommandIDEnum?)_app.CommandManager._ActiveCommand);
            bool commandRunning = command != CommandIDEnum.kDefaultCommand;
            if (name != null && commandRunning)
            {
                string? text = ComSafe.Get(() => _app.CommandManager.ActiveCommand);
                LogWaiting($"a command is running (_ActiveCommand = {command?.ToString() ?? "unreadable"}, ActiveCommand = \"{text ?? "unreadable"}\")");
            }

            return new HoleTableAutoCheckContext(name, editingItself, commandRunning, WindowHost.IsAnyOpen);
        }

        private void LogWaiting(string reason)
        {
            if (_deferralsLogged.Add(reason))
                _log.Info($"Hole table check: waiting, {reason}.");
        }

        // ---- The check

        private void CheckAndPrompt(DrawingDocument drawing, string key)
        {
            var source = new HoleTableSource(drawing, AddinServices.Settings, _log);
            string name = source.DrawingName;
            HoleTableStoredCheckResult? result = RunCheck(source, name);
            if (result == null || !_policy.ShouldPrompt(key, result))
                return;

            var viewModel = new HoleTableOutOfDatePromptViewModel(source, name, result.OutOfDate);
            var outcome = HoleTableOutOfDatePromptOutcome.Later;
            var window = new HoleTableOutOfDateWindow(viewModel);
            viewModel.CloseRequested += o => outcome = o;
            WindowHost.ShowDialog(window, IconNames.HoleTable);

            if (viewModel.ResultText.Length > 0)
                _log.Info($"Hole table check: Update now in {name}: {viewModel.ResultText}");

            if (outcome == HoleTableOutOfDatePromptOutcome.Updated)
            {
                _log.Info($"Hole table check: {name}, answered Update now.");
                _policy.UpdatedNow(key);
                return;
            }

            // Later, or Close after some views failed: remember the views still out of date, with their original reasons.
            var left = result.OutOfDate
                .Where(o => viewModel.Views.Any(v => v.SheetKey == o.SheetKey && v.ViewKey == o.ViewKey))
                .ToList();
            _log.Info($"Hole table check: {name}, answered Later ({left.Count} view(s) left).");
            _policy.Later(key, left);
        }

        /// <summary>Runs the write-free check with the wait cursor and logs the result. Returns null, with a log line, when it cannot run at all.</summary>
        private HoleTableStoredCheckResult? RunCheck(HoleTableSource source, string name)
        {
            Forms.Cursor? previous = Forms.Cursor.Current;
            Forms.Cursor.Current = Forms.Cursors.WaitCursor;
            var timer = Stopwatch.StartNew();
            try
            {
                HoleTableStoredCheckResult result = HoleTableStoredCheck.Run(source, source.GetKeyedSheets());
                timer.Stop();
                _log.Info(HoleTableMessages.AutoCheckLogLine(name, result, timer.Elapsed.TotalSeconds));
                return result;
            }
            catch (Exception ex)
            {
                _log.Error($"Hole table check: {name} could not be checked.", ex);
                return null;
            }
            finally
            {
                Forms.Cursor.Current = previous;
            }
        }

        // ---- The wizard

        /// <summary>The Hole Table window is about to open on this drawing. No automatic check runs until it closes.</summary>
        public void WizardOpened(DrawingDocument drawing)
        {
            try
            {
                _wizardsOpen++;
                if (DrawingNameOf((_Document)drawing) is { } key)
                    _policy.DrawingClosed(key);
            }
            catch (Exception ex)
            {
                _log.Error("Hole table check: noting that the wizard opened failed.", ex);
            }
        }

        /// <summary>
        /// The Hole Table window closed. It has already asked about its stored tables, so the check runs once more without a
        /// prompt, and the result counts as answered with Later (task 175's <c>WizardClosed</c>).
        /// </summary>
        public void WizardClosed(DrawingDocument drawing)
        {
            try
            {
                if (_wizardsOpen > 0)
                    _wizardsOpen--;
                _policy.Enabled = AddinServices.Settings.CheckHoleTablesAutomatically;
                if (!_policy.Enabled || DrawingNameOf((_Document)drawing) is not { } key)
                    return;

                var source = new HoleTableSource(drawing, AddinServices.Settings, _log);
                HoleTableStoredCheckResult? result = RunCheck(source, source.DrawingName);
                if (result != null)
                    _policy.WizardClosed(key, result);
            }
            catch (Exception ex)
            {
                _log.Error("Hole table check: the check after the wizard closed failed.", ex);
            }
        }
    }
}
