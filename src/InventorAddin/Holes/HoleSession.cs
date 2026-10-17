using System;
using System.Collections.Generic;
using System.Diagnostics;
using Inventor;
using InventorAddin.Core.Holes;
using InventorAddin.Core.Logging;
using InventorAddin.Core.Ribbon;
using InventorAddin.Core.ViewModels;

namespace InventorAddin.Holes
{
    /// <summary>
    /// What runs while the AWB Hole window is open (task 089; spec 09, "The window"; spike 081 rounds 2, 4 and 6):
    /// it picks the hole centres the way Inventor's Hole does, keeps a real hole feature in step with the
    /// view-model as the live preview, and ends with OK (one Undo step) or Cancel (the part as it was).
    /// <see cref="HoleSessionRules"/> makes the decisions; this reads Inventor, asks it, and applies the answer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The whole session runs inside one outer transaction, named <c>AWB Hole</c> or <c>Edit AWB Hole</c>. Each
    /// preview change runs in its own inner transaction; if Inventor refuses it, or the hole comes out unhealthy, only
    /// that step is aborted, so the hole stays at its last good state and later changes still work (spike 081 round 6).
    /// </para>
    /// <para>
    /// The session listens to the view-model: <see cref="AwbHoleViewModel.PreviewNeeded"/> previews,
    /// <see cref="AwbHoleViewModel.OkRequested"/> is <see cref="Ok"/> and <see cref="AwbHoleViewModel.CancelRequested"/>
    /// is <see cref="Cancel"/>. The window (task 090) closes on <see cref="Ended"/>, and calls <see cref="Cancel"/>
    /// when it is closed any other way. <see cref="Ended"/> is raised once, by every way out: OK, Cancel, Inventor
    /// closing the document, Inventor stopping the picking, and <see cref="Dispose"/>.
    /// </para>
    /// <para>
    /// Inventor stopping the picking cancels the session, except when the command that stopped it is AWB Hole itself
    /// (its button pressed while the window is open, task 090): the picking is then started again
    /// (<see cref="HolePickingWatch"/>, <see cref="ResumePicking"/>), and the command brings the window to the front.
    /// </para>
    /// <para>
    /// Nothing here throws into Inventor's events: a failure there is logged and the session is cancelled.
    /// </para>
    /// </remarks>
    public sealed class HoleSession : IDisposable
    {
        private const string LogPrefix = AwbHoleViewModel.CommandName + ": ";

        private readonly Application _app;
        private readonly PartDocument _doc;
        private readonly AwbHoleViewModel _viewModel;
        private readonly ILog _log;
        private readonly bool _edit;
        private readonly HoleCentrePicks<SketchPoint, PlanarSketch> _picks;

        // Each sketch the session showed, with whether it was visible when the window opened.
        private readonly List<(PlanarSketch Sketch, bool WasVisible)> _shownSketches = new List<(PlanarSketch, bool)>();

        private Transaction? _outer;
        private HoleFeature? _hole;
        private string? _holeName;

        // What the hole in the model was last made from: OK keeps it only when it is what the window shows.
        private HoleSpec? _appliedSpec;
        private int _centresVersion;
        private int _appliedCentresVersion = -1;

        // A new hole's picks moved to another sketch since the preview hole was made: it is removed and made again,
        // never moved across sketches (not tried in Inventor).
        private bool _sketchChanged;

        // Kept referenced, or their events stop firing.
        private InteractionEvents? _interaction;
        private SelectEvents? _select;
        private HighlightSet? _highlight;
        private ApplicationEvents? _appEvents;
        private UserInputEvents? _userInput;

        // Tells whether the picking was stopped by AWB Hole itself (its button pressed while the window is open), so
        // it is started again instead of the session being cancelled (task 090).
        private readonly HolePickingWatch _watch = new HolePickingWatch(CommandNames.AwbHole);

        // Inventor's main thread: the picking is started again after Inventor's terminate event has returned.
        private readonly System.Windows.Threading.Dispatcher _dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;

        private bool _ending;
        private bool _ended;

        private HoleSession(Application app, PartDocument doc, AwbHoleViewModel viewModel, HoleFeature? existing, ILog log)
        {
            _app = app;
            _doc = doc;
            _viewModel = viewModel;
            _log = log;
            _edit = existing != null;
            _hole = existing;
            _holeName = existing == null ? null : ComSafe.Get(() => existing.Name);
            _picks = new HoleCentrePicks<SketchPoint, PlanarSketch>(_edit, SamePoint, SameSketch);
        }

        /// <summary>
        /// Raised once when the session ends, by any way out. The window closes on it.
        /// </summary>
        public event EventHandler<HoleSessionEndedEventArgs>? Ended;

        /// <summary>True once the session has ended.</summary>
        public bool IsEnded => _ended;

        /// <summary>
        /// Starts a session on <paramref name="doc"/>: for a new hole when <paramref name="existing"/> is null, else to
        /// edit that hole. <paramref name="viewModel"/> must already hold the hole's settings (for an edit, read before
        /// the first preview: spike 081 round 5). A new hole is previewed at once at the default centres; an edited
        /// hole is left as it is until the window changes. If starting fails, everything is put back and the exception
        /// is thrown.
        /// </summary>
        public static HoleSession Start(
            Application app, PartDocument doc, AwbHoleViewModel viewModel, HoleFeature? existing, ILog log)
        {
            if (app == null)
                throw new ArgumentNullException(nameof(app));
            if (doc == null)
                throw new ArgumentNullException(nameof(doc));
            if (viewModel == null)
                throw new ArgumentNullException(nameof(viewModel));
            log ??= NullLog.Instance;

            var session = new HoleSession(app, doc, viewModel, existing, log);
            try
            {
                session.Begin(existing);
            }
            catch (Exception ex)
            {
                log.Error(LogPrefix + "starting the session failed.", ex);
                session.Cancel(HoleSessionEndReason.Failed);
                throw;
            }

            return session;
        }

        /// <summary>
        /// OK: brings the hole up to date with the window when it is not, applies the dowel tolerance, colour and
        /// stored data (or removes them, for a hole that is no longer a dowel), puts the sketches back and ends the
        /// outer transaction, so the whole session is one Undo step. Returns false, with the window's status saying
        /// why, when the hole cannot be kept as the window shows it; the session then stays open.
        /// </summary>
        public bool Ok()
        {
            if (_ended)
                return false;

            try
            {
                if (!_viewModel.CanOk)
                    return false;

                if (!IsApplied)
                    Preview();
                if (!IsApplied || _hole == null)
                {
                    if (!_viewModel.IsRefused)
                        _viewModel.ShowRefused(HoleSessionRules.NotAppliedText);
                    return false;
                }

                HoleFeature hole = _hole;
                HoleBuildResult build = _viewModel.BuildResult;
                Transaction step = StartStep();
                try
                {
                    HoleFitWriter.Finish(_doc, hole, build, _log);
                    step.End();
                }
                catch (Exception ex)
                {
                    AbortStep(step);
                    _log.Error(LogPrefix + $"applying the tolerance, colour and data to {_holeName} failed.", ex);
                    _viewModel.ShowRefused(HoleSessionRules.FinishFailed(ex.Message));
                    return false;
                }

                // Inside the outer transaction, so the one Undo step holds it too.
                RestoreSketches(kept: true);
                _outer?.End();
                _outer = null;
                _log.Info(LogPrefix + $"{(_edit ? "changed" : "made")} {ComSafe.Get(() => hole.Name)} " +
                          $"at {_picks.Count} centre(s).");
                Finish(kept: true, HoleSessionEndReason.Ok);
                return true;
            }
            catch (Exception ex)
            {
                _log.Error(LogPrefix + "OK failed; the part is put back as it was.", ex);
                Cancel(HoleSessionEndReason.Failed);
                return false;
            }
        }

        /// <summary>Cancel: aborts the outer transaction, so the part is as it was, also for an edited hole.</summary>
        public void Cancel() => Cancel(HoleSessionEndReason.Cancel);

        /// <summary>
        /// The AWB Hole command was started again while the window is open (task 090): the picking is started again
        /// if Inventor stopped it, and is left as it is otherwise. Call after Inventor's command has returned. If the
        /// picking cannot be started, the session is cancelled, as when Inventor stops it.
        /// </summary>
        public void ResumePicking()
        {
            if (_ended)
                return;

            _watch.Resumed();
            if (_interaction != null)
                return;

            try
            {
                StartInteraction();
                Highlight();
                _log.Info(LogPrefix + "the hole centre picking was started again.");
            }
            catch (Exception ex)
            {
                _log.Error(LogPrefix + "starting the hole centre picking again failed; the session is cancelled.", ex);
                Cancel(HoleSessionEndReason.Failed);
            }
        }

        /// <summary>Cancels the session if it has not ended.</summary>
        public void Dispose()
        {
            if (!_ended)
                Cancel(HoleSessionEndReason.Cancel);
        }

        // ---- start ----

        private void Begin(HoleFeature? existing)
        {
            if (existing != null)
                StartEditPicks(existing);
            else
                StartDefaultPicks();

            StartPicking();

            _outer = _app.TransactionManager.StartTransaction((_Document)_doc, HoleSessionRules.TransactionName(_edit));

            // A hole consumes its sketch, which hides it; it stays shown so its points can be clicked (spike 081).
            if (_picks.Sketch != null)
                ShowSketch(_picks.Sketch);

            _appEvents = _app.ApplicationEvents;
            _appEvents.OnCloseDocument += OnCloseDocument;
            _userInput = _app.CommandManager.UserInputEvents;
            _userInput.OnActivateCommand += OnActivateCommand;
            _viewModel.PreviewNeeded += OnPreviewNeeded;
            _viewModel.OkRequested += OnOkRequested;
            _viewModel.CancelRequested += OnCancelRequested;

            // An edited hole whose centres are not sketch points has no sketch to pick in; the window says so.
            _viewModel.CanPickCentres = !(_edit && _picks.Sketch == null);
            _viewModel.CentreCount = _picks.Count;
            Highlight();

            _log.Info(LogPrefix + $"{(_edit ? $"editing {_holeName}" : "new hole")}: " +
                      $"{_picks.Count} centre(s) from sketch {SketchName(_picks.Sketch) ?? "none"}.");

            // An edited hole is left as it is until the window changes; OK brings it up to date.
            if (!_edit)
                Preview();
        }

        /// <summary>
        /// A new hole's centres, as Inventor's Hole picks them: the selected sketch, else the sketch of the selected
        /// points, else the newest visible sketch with hole-centre points; all its hole centres.
        /// </summary>
        private void StartDefaultPicks()
        {
            PlanarSketch? selectedSketch = null;
            PlanarSketch? sketchOfPoint = null;
            SelectSet? selection = ComSafe.Get(() => _doc.SelectSet);
            if (selection != null)
            {
                foreach (object item in selection)
                {
                    if (selectedSketch == null && item is PlanarSketch sketch)
                        selectedSketch = sketch;
                    else if (sketchOfPoint == null && item is SketchPoint point)
                        sketchOfPoint = ComSafe.Get(() => point.Parent as PlanarSketch);
                }
            }

            PlanarSketch? chosen = HoleSessionRules.DefaultSketch(selectedSketch, sketchOfPoint, SketchesNewestFirst());
            var centres = new List<SketchPoint>();
            if (chosen != null)
            {
                foreach (SketchPoint point in chosen.SketchPoints)
                {
                    if (ComSafe.Get(() => point.HoleCenter))
                        centres.Add(point);
                }
            }

            _picks.Start(chosen, centres);
        }

        private IEnumerable<(PlanarSketch, Func<bool>, Func<bool>)> SketchesNewestFirst()
        {
            PlanarSketches sketches = _doc.ComponentDefinition.Sketches;
            int count = ComSafe.Get(() => sketches.Count);
            for (int i = count; i >= 1; i--)
            {
                int index = i;
                PlanarSketch? sketch = ComSafe.Get(() => sketches[index]);
                if (sketch == null)
                    continue;
                yield return (sketch, () => ComSafe.Get(() => sketch.Visible), () => HasHoleCentres(sketch));
            }
        }

        private static bool HasHoleCentres(PlanarSketch sketch) =>
            ComSafe.Get(() =>
            {
                foreach (SketchPoint point in sketch.SketchPoints)
                {
                    if (point.HoleCenter)
                        return true;
                }

                return false;
            });

        /// <summary>An edited hole's own centres, in their sketch (spike 081 round 4).</summary>
        private void StartEditPicks(HoleFeature hole)
        {
            var centres = new List<SketchPoint>();
            ObjectCollection? points = ComSafe.Get(() => hole.HoleCenterPoints);
            if (points != null)
            {
                foreach (object item in points)
                {
                    if (item is SketchPoint point)
                        centres.Add(point);
                }
            }

            PlanarSketch? sketch = centres.Count == 0 ? null : ComSafe.Get(() => centres[0].Parent as PlanarSketch);
            _picks.Start(sketch, centres);
        }

        /// <summary>
        /// Picking as Inventor's Hole does (spike 081 round 2): sketch points only, one at a time; each click toggles
        /// a point, without Ctrl. The picked centres are highlighted blue.
        /// </summary>
        private void StartPicking()
        {
            _highlight = _doc.CreateHighlightSet();
            HoleColour blue = HoleSessionRules.PickedColour;
            _highlight.SetColor(blue.R, blue.G, blue.B);

            StartInteraction();
        }

        /// <summary>The interaction events that pick the centres; made again when AWB Hole restarts the picking.</summary>
        private void StartInteraction()
        {
            _interaction = _app.CommandManager.CreateInteractionEvents();
            _interaction.StatusBarText = HoleSessionRules.StatusBarText;
            _select = _interaction.SelectEvents;
            _select.AddSelectionFilter(SelectionFilterEnum.kSketchPointFilter);
            _select.SingleSelectEnabled = true;
            _select.OnSelect += OnSelect;
            _interaction.OnTerminate += OnTerminate;
            _interaction.Start();
        }

        /// <summary>
        /// Lets go of the interaction events: their handlers detached, and, when <paramref name="stop"/>, stopped. Not
        /// stopped when Inventor has already terminated them.
        /// </summary>
        private void DropInteraction(bool stop)
        {
            SelectEvents? select = _select;
            InteractionEvents? interaction = _interaction;
            _select = null;
            _interaction = null;

            if (select != null)
                ComSafe.Run(() => select.OnSelect -= OnSelect);
            if (interaction != null)
            {
                ComSafe.Run(() => interaction.OnTerminate -= OnTerminate);
                if (stop)
                    ComSafe.Run(() => interaction.Stop());
            }
        }

        // ---- events ----

        private void OnSelect(ObjectsEnumerator justSelected, SelectionDeviceEnum device, Point modelPosition, Point2d viewPosition, View view)
        {
            if (_ended)
                return;

            try
            {
                bool changed = false;
                foreach (object item in justSelected)
                {
                    if (item is not SketchPoint point)
                        continue;

                    PlanarSketch? sketch = ComSafe.Get(() => point.Parent as PlanarSketch);
                    HoleCentrePick pick = _picks.Toggle(point, sketch);
                    if (pick == HoleCentrePick.Ignored)
                        continue;

                    changed = true;
                    if (pick == HoleCentrePick.SketchChanged)
                    {
                        _sketchChanged = true;
                        if (sketch != null)
                            ShowSketch(sketch);
                    }
                }

                ComSafe.Run(() => _select?.ResetSelections());
                if (!changed)
                    return;

                _centresVersion++;
                _viewModel.CentreCount = _picks.Count;
                Highlight();
                Preview();
            }
            catch (Exception ex)
            {
                _log.Error(LogPrefix + "picking a hole centre failed.", ex);
            }
        }

        private void OnPreviewNeeded(object? sender, EventArgs e)
        {
            if (_ended)
                return;

            try
            {
                Preview();
            }
            catch (Exception ex)
            {
                _log.Error(LogPrefix + "the preview failed.", ex);
                _viewModel.ShowRefused(HoleFeatureMessages.Refused(ex.Message));
            }
        }

        private void OnOkRequested(object? sender, EventArgs e) => Ok();

        private void OnCancelRequested(object? sender, EventArgs e) => Cancel();

        /// <summary>
        /// Inventor stopped the picking (another command started, or Esc in the model): the session is cancelled, so
        /// no other command runs inside its transaction. Our own <c>Stop</c> raises this too, and is ignored. When the
        /// command that stopped it is AWB Hole itself (<see cref="HolePickingWatch"/>), the picking is started again
        /// once Inventor's event has returned, and the command brings the window to the front (task 090).
        /// </summary>
        private void OnTerminate()
        {
            if (_ending || _ended)
                return;

            try
            {
                if (_watch.PickingStopped() == HolePickingStop.Restart)
                {
                    _log.Info(LogPrefix + AwbHoleViewModel.CommandName + " was started while its window is open; the picking is started again.");
                    DropInteraction(stop: false);
                    _dispatcher.BeginInvoke(new Action(ResumePicking));
                    return;
                }
            }
            catch (Exception ex)
            {
                _log.Error(LogPrefix + "restarting the hole centre picking failed; the session is cancelled.", ex);
            }

            _log.Info(LogPrefix + "Inventor stopped the hole centre picking; the session is cancelled.");
            Cancel(HoleSessionEndReason.ByInventor);
        }

        /// <summary>Each command Inventor starts, so the picking stopped by AWB Hole itself can be told apart.</summary>
        private void OnActivateCommand(string commandName, NameValueMap context)
        {
            if (_ended)
                return;

            _watch.CommandStarted(commandName);
        }

        private void OnCloseDocument(
            _Document document, string fullDocumentName, EventTimingEnum beforeOrAfter, NameValueMap context,
            out HandlingCodeEnum handlingCode)
        {
            handlingCode = HandlingCodeEnum.kEventNotHandled;
            if (_ended || beforeOrAfter != EventTimingEnum.kBefore || !IsOurDocument(document, fullDocumentName))
                return;

            _log.Info(LogPrefix + "the part is closing; the session is cancelled.");
            Cancel(HoleSessionEndReason.ByInventor);
        }

        private bool IsOurDocument(_Document document, string fullDocumentName)
        {
            if (ReferenceEquals(document, _doc))
                return true;
            string? ours = ComSafe.Get(() => _doc.FullDocumentName);
            return !string.IsNullOrEmpty(ours)
                && string.Equals(ours, fullDocumentName, StringComparison.OrdinalIgnoreCase);
        }

        // ---- preview ----

        /// <summary>True when the hole in the model was made from what the window shows now.</summary>
        private bool IsApplied =>
            _hole != null
            && _appliedSpec != null
            && _appliedSpec == _viewModel.Spec
            && _appliedCentresVersion == _centresVersion;

        /// <summary>Brings the hole in step with the window: one inner transaction, aborted alone on a refusal.</summary>
        private void Preview()
        {
            if (_ended || _outer == null)
                return;

            var watch = Stopwatch.StartNew();
            HolePreviewAction action = HoleSessionRules.Plan(
                _hole != null, _edit, _picks.Count, _viewModel.Geometry != null, _sketchChanged);
            switch (action)
            {
                case HolePreviewAction.None:
                    return;
                case HolePreviewAction.Remove:
                    RemovePreviewHole();
                    break;
                case HolePreviewAction.RemoveThenCreate:
                    if (RemovePreviewHole())
                        Write(HolePreviewAction.Create);
                    break;
                default:
                    Write(action);
                    break;
            }

            // Making a hole hides its sketch again; the highlight is redrawn over the new model.
            if (_picks.Sketch != null)
                ShowSketch(_picks.Sketch);
            Highlight();

            watch.Stop();
            if (HoleSessionRules.IsSlow(watch.Elapsed))
                _log.Info(LogPrefix + $"preview {action} took {watch.ElapsedMilliseconds} ms.");
        }

        private void Write(HolePreviewAction action)
        {
            HoleGeometry geometry = _viewModel.Geometry!;
            HoleSpec spec = _viewModel.Spec;
            int centresVersion = _centresVersion;
            HoleFeature? held = _hole;

            Transaction step = StartStep();
            HoleWriteResult result;
            try
            {
                ObjectCollection centres = HoleFeatureWriter.Centres(_app, _picks.Points);
                result = action == HolePreviewAction.Create
                    ? HoleFeatureWriter.Create(_doc, geometry, centres, null, _log)
                    : HoleFeatureWriter.Change(_doc, held!, geometry, centres, null, _log);
            }
            catch (Exception ex)
            {
                AbortStep(step);
                _log.Error(LogPrefix + $"the preview {action} failed.", ex);
                if (action == HolePreviewAction.Change)
                    FindHoleAgain();
                _viewModel.ShowRefused(HoleFeatureMessages.Refused(ex.Message));
                return;
            }

            HolePreviewStep plan = HoleSessionRules.AfterWrite(action, result.Succeeded, result.Recreated);
            if (!plan.EndStep)
            {
                AbortStep(step);
                if (plan.FindHoleAgain)
                    FindHoleAgain();
                _viewModel.ShowRefused(result.Refusal);
                return;
            }

            try
            {
                step.End();
            }
            catch (Exception ex)
            {
                AbortStep(step);
                _log.Error(LogPrefix + "ending a preview step failed.", ex);
                if (action == HolePreviewAction.Change)
                    FindHoleAgain();
                _viewModel.ShowRefused(HoleFeatureMessages.Refused(ex.Message));
                return;
            }

            if (plan.HoldNewHole)
                _hole = result.Hole;
            if (action == HolePreviewAction.Create)
                _sketchChanged = false;
            _holeName = ComSafe.Get(() => _hole?.Name) ?? _holeName;
            _appliedSpec = spec;
            _appliedCentresVersion = centresVersion;
            foreach (string note in result.Notes)
                _log.Info(LogPrefix + note);

            // Clears a refusal too; the notes (the tap drill fallback, a hole made again) show in the status line.
            _viewModel.ShowNotes(result.Notes);
        }

        /// <summary>
        /// A new hole with no centres left, or with centres moved to another sketch: the preview hole is deleted, its
        /// sketch kept. False when Inventor refused; the hole is then found again, as after a refused change.
        /// </summary>
        private bool RemovePreviewHole()
        {
            HoleFeature hole = _hole!;
            Transaction step = StartStep();
            try
            {
                hole.Delete(true, true, true);
                ComSafe.Run(() => _doc.Update());
                step.End();
            }
            catch (Exception ex)
            {
                AbortStep(step);
                _log.Error(LogPrefix + "removing the preview hole failed.", ex);
                FindHoleAgain();
                _viewModel.ShowRefused(HoleFeatureMessages.Refused(ex.Message));
                return false;
            }

            _hole = null;
            _holeName = null;
            _appliedSpec = null;
            _appliedCentresVersion = -1;
            _sketchChanged = false;
            return true;
        }

        /// <summary>
        /// After a step that deleted the held hole was aborted, the old object is not trusted (task 088): the hole
        /// is found again by its name, which a recreate keeps.
        /// </summary>
        private void FindHoleAgain()
        {
            if (string.IsNullOrEmpty(_holeName))
                return;

            HoleFeature? found = null;
            ComSafe.Run(() =>
            {
                foreach (HoleFeature hole in _doc.ComponentDefinition.Features.HoleFeatures)
                {
                    if (string.Equals(ComSafe.Get(() => hole.Name), _holeName, StringComparison.Ordinal))
                    {
                        found = hole;
                        break;
                    }
                }
            });

            if (found == null)
            {
                _log.Warn(LogPrefix + $"{_holeName} could not be found again after a refused change.");
                if (!_edit)
                {
                    _hole = null;
                    _appliedSpec = null;
                }

                return;
            }

            _hole = found;
        }

        private Transaction StartStep() =>
            _app.TransactionManager.StartTransaction((_Document)_doc, HoleSessionRules.StepTransactionName);

        private void AbortStep(Transaction step)
        {
            try
            {
                step.Abort();
            }
            catch (Exception ex)
            {
                _log.Error(LogPrefix + "aborting a preview step failed.", ex);
            }

            ComSafe.Run(() => _doc.Update());
        }

        // ---- sketches and highlight ----

        private void ShowSketch(PlanarSketch sketch)
        {
            if (!_shownSketches.Exists(s => SameSketch(s.Sketch, sketch)))
                _shownSketches.Add((sketch, ComSafe.Get(() => sketch.Visible)));
            if (!ComSafe.Get(() => sketch.Visible))
                ComSafe.Run(() => sketch.Visible = true);
        }

        /// <summary>
        /// Puts every sketch the session showed back as it was; a kept new hole's own sketch ends hidden, as Inventor
        /// leaves it.
        /// </summary>
        private void RestoreSketches(bool kept)
        {
            foreach ((PlanarSketch sketch, bool wasVisible) in _shownSketches)
            {
                bool holeSketch = _picks.Sketch != null && SameSketch(sketch, _picks.Sketch);
                bool visible = HoleSessionRules.SketchVisibleAfter(!_edit && holeSketch, kept, wasVisible);
                if (ComSafe.Get(() => sketch.Visible) != visible)
                    ComSafe.Run(() => sketch.Visible = visible);
            }
        }

        private void Highlight()
        {
            HighlightSet? highlight = _highlight;
            if (highlight == null)
                return;

            ComSafe.Run(() => highlight.Clear());
            foreach (SketchPoint point in _picks.Points)
                ComSafe.Run(() => highlight.AddItem(point));
            ComSafe.Run(() => _app.ActiveView.Update());
        }

        private static bool SamePoint(SketchPoint a, SketchPoint b) =>
            ReferenceEquals(a, b)
            || (SameSketch(ComSafe.Get(() => a.Parent as PlanarSketch), ComSafe.Get(() => b.Parent as PlanarSketch))
                && ComSafe.Get(() => a.Geometry3d.IsEqualTo(b.Geometry3d)));

        private static bool SameSketch(PlanarSketch? a, PlanarSketch? b)
        {
            if (a == null || b == null)
                return false;
            if (ReferenceEquals(a, b))
                return true;
            string? nameA = SketchName(a);
            return nameA != null && string.Equals(nameA, SketchName(b), StringComparison.Ordinal);
        }

        private static string? SketchName(PlanarSketch? sketch) => sketch == null ? null : ComSafe.Get(() => sketch.Name);

        // ---- end ----

        private void Cancel(HoleSessionEndReason reason)
        {
            if (_ended)
                return;

            try
            {
                if (_outer != null)
                {
                    Transaction outer = _outer;
                    _outer = null;
                    outer.Abort();
                }
            }
            catch (Exception ex)
            {
                _log.Error(LogPrefix + "aborting the session's transaction failed.", ex);
            }

            ComSafe.Run(() => _doc.Update());
            RestoreSketches(kept: false);
            if (reason != HoleSessionEndReason.Failed)
                _log.Info(LogPrefix + $"cancelled ({reason}); the part is as it was.");
            Finish(kept: false, reason);
        }

        /// <summary>
        /// Every way out ends here: picking stopped, highlight deleted, events dropped, view updated, then
        /// <see cref="Ended"/>.
        /// </summary>
        private void Finish(bool kept, HoleSessionEndReason reason)
        {
            if (_ended)
                return;
            _ending = true;

            try
            {
                DropInteraction(stop: true);
                ComSafe.Run(() => _highlight?.Delete());
                if (_appEvents != null)
                    ComSafe.Run(() => _appEvents.OnCloseDocument -= OnCloseDocument);
                if (_userInput != null)
                    ComSafe.Run(() => _userInput.OnActivateCommand -= OnActivateCommand);
                _viewModel.PreviewNeeded -= OnPreviewNeeded;
                _viewModel.OkRequested -= OnOkRequested;
                _viewModel.CancelRequested -= OnCancelRequested;
                ComSafe.Run(() => _app.ActiveView.Update());
            }
            finally
            {
                _select = null;
                _interaction = null;
                _highlight = null;
                _appEvents = null;
                _userInput = null;
                _ended = true;
                _ending = false;
            }

            try
            {
                Ended?.Invoke(this, new HoleSessionEndedEventArgs(kept, reason));
            }
            catch (Exception ex)
            {
                _log.Error(LogPrefix + "closing the window after the session failed.", ex);
            }
        }
    }

    /// <summary>How an AWB Hole session ended.</summary>
    public enum HoleSessionEndReason
    {
        /// <summary>OK: the hole is kept.</summary>
        Ok,

        /// <summary>Cancel, the window closed, or the session was disposed.</summary>
        Cancel,

        /// <summary>Inventor ended it: the part closed, or the picking was stopped by another command.</summary>
        ByInventor,

        /// <summary>Something failed; the part is put back as it was.</summary>
        Failed,
    }

    /// <summary>The <see cref="HoleSession.Ended"/> event: whether the hole was kept, and why the session ended.</summary>
    public sealed class HoleSessionEndedEventArgs : EventArgs
    {
        public HoleSessionEndedEventArgs(bool kept, HoleSessionEndReason reason)
        {
            Kept = kept;
            Reason = reason;
        }

        public bool Kept { get; }

        public HoleSessionEndReason Reason { get; }
    }
}
