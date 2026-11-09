using System;
using System.Collections.Generic;
using System.Diagnostics;
using Inventor;
using InventorAddin.Core.Holes;
using InventorAddin.Core.Logging;
using InventorAddin.Core.Ribbon;
using InventorAddin.Core.Settings;
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
    /// While the window is open a faint, see-through red copy of the hole volume is drawn over the model with client
    /// graphics (task 110; spike 107), made outside the Undo list. No face is painted. The graphics are removed before
    /// every change to the part (a preview step, OK's tolerance and colour) and drawn again from the hole the model
    /// holds afterwards, so after a refused change they follow the last good hole; they are removed on every way out.
    /// </para>
    /// <para>
    /// The session listens to the view-model: <see cref="AwbHoleViewModel.PreviewNeeded"/> previews,
    /// <see cref="AwbHoleViewModel.OkRequested"/> is <see cref="Ok"/> and <see cref="AwbHoleViewModel.CancelRequested"/>
    /// is <see cref="Cancel"/>. The window (task 090) closes on <see cref="Ended"/>, and calls <see cref="Cancel"/>
    /// when it is closed any other way. <see cref="Ended"/> is raised once, by every way out: OK, Cancel, Inventor
    /// closing the document, the user switching to another document, Inventor stopping the picking, and
    /// <see cref="Dispose"/>.
    /// </para>
    /// <para>
    /// The part may be edited in place inside an assembly (spike 103; task 105). Everything still happens on the part:
    /// the transactions, the highlight set and the hole. Inventor then gives picked and selected sketch points and
    /// sketches as proxies in the assembly; each is taken as its native object in the part before it is used. The
    /// session ends as Cancel does when the document that was active when it began is deactivated: the assembly in
    /// place, the part in its own window.
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

        // Each sketch the session showed, with whether it was visible and shared when the window opened (task 094).
        private readonly List<(PlanarSketch Sketch, bool WasVisible, bool WasShared)> _shownSketches =
            new List<(PlanarSketch, bool, bool)>();

        // How a new hole's sketch was chosen, for the start log line: selected, of the selected points, or newest.
        private string? _defaultSketchFrom;

        private Transaction? _outer;

        // The document active when the session began (the assembly when the part is edited in place, else the part),
        // and its full name: switching away from it ends the session (task 105).
        private Document? _startDoc;
        private string? _startDocName;

        // True while the preview graphics may be drawn on the part (task 110).
        private bool _drawn;
        private HoleFeature? _hole;
        private string? _holeName;

        // What an edited hole was when the window opened, for the face reset on OK (task 096): a precision hole (a
        // dowel, or a toleranced Clearance or Simple hole, task 113), and whether a face carried a fit colour.
        private readonly bool _openedPrecision;
        private readonly bool _openedFitColoured;

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

        // When the part is edited in place, the occurrence being edited: the highlight set is then on the assembly and
        // takes each point's proxy through it (task 143). Null in the part's own window.
        private ComponentOccurrence? _highlightOccurrence;

        // A point whose proxy could not be made is logged once per session.
        private bool _proxyWarned;
        private UserInputEvents? _userInput;

        // Tells whether the picking was stopped by AWB Hole itself (its button pressed while the window is open, or
        // our Edit Feature, task 092), so it is started again instead of the session being cancelled (task 090).
        private readonly HolePickingWatch _watch = new HolePickingWatch(CommandNames.AwbHole, CommandNames.AwbHoleEdit);

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
            _openedPrecision = HolePrecision.IsPrecision(viewModel.Spec);
            _openedFitColoured = existing != null && HoleFeatureReader.IsColoured(existing);
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

                // The step removes the preview graphics first; they are drawn again only if OK fails here.
                Transaction step = StartStep();
                try
                {
                    // By the precision rule (task 115): a hole kept toleranced is recoloured, not reset first.
                    bool resetFaces = HoleSessionRules.ResetsFaceAppearances(
                        _edit, _openedPrecision, _openedFitColoured, HolePrecision.IsPrecision(build.Resolved));
                    HoleFitWriter.Finish(_doc, hole, build, resetFaces, _log);
                    step.End();
                }
                catch (Exception ex)
                {
                    AbortStep(step);
                    _log.Error(LogPrefix + $"applying the tolerance, colour and data to {_holeName} failed.", ex);
                    _viewModel.ShowRefused(HoleSessionRules.FinishFailed(ex.Message));
                    DrawPreview();
                    return false;
                }

                // Inside the outer transaction, so the one Undo step holds it too.
                RestoreSketches(kept: true);
                _outer?.End();
                _outer = null;
                _log.Info(LogPrefix + $"{(_edit ? "changed" : "made")} {ComSafe.Get(() => hole.Name)} " +
                          $"at {_picks.Count} centre(s).");
                RememberDepth(build.Resolved);
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

        /// <summary>
        /// Keeps the depth of a blind clearance or dowel hole kept with OK as the last depth of its units (task 097),
        /// and a dowel's or tapped hole's seat as the last seat of its units and size (tasks 106 and 126), in the
        /// settings file, so the next hole starts at them, also after a restart. A failure to save is logged; the hole is kept either way.
        /// </summary>
        private void RememberDepth(HoleSpec kept)
        {
            try
            {
                AddinSettings? updated = HoleLastDowelSeats.RememberKept(AddinServices.Settings, kept);
                if (updated == null)
                    return;

                string? folder = AddinServices.DataFolder;
                if (folder == null)
                {
                    _log.Warn(LogPrefix + "the hole depth and seat are not remembered: the add-in has no data folder.");
                    return;
                }

                new SettingsStore(folder).Save(updated);
                AddinServices.UpdateSettings(updated);

                // Task 125: say which seat the next dowel (or, task 126, tapped hole) of the size starts at, so a
                // manual check can follow it.
                string? seat = HoleLastDowelSeats.RememberedText(kept);
                if (seat != null)
                    _log.Info(LogPrefix + "remembered the seat for the next " + seat + ".");
            }
            catch (Exception ex)
            {
                _log.Error(LogPrefix + "saving the hole depth and seat to the settings file failed.", ex);
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
            _startDoc = ComSafe.Get(() => _app.ActiveDocument);
            Document? startDoc = _startDoc;
            _startDocName = startDoc == null ? null : ComSafe.Get(() => startDoc.FullDocumentName);

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
            _appEvents.OnDeactivateDocument += OnDeactivateDocument;
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
                      $"{_picks.Count} centre(s) from sketch {SketchName(_picks.Sketch) ?? "none"}" +
                      (_picks.Sketch == null ? "" : SketchStateText(_picks.Sketch)) + ".");

            // An edited hole is left as it is until the window changes; OK brings it up to date. Its preview is drawn
            // at once, as a new hole's is by its first preview.
            if (!_edit)
                Preview();
            else
                DrawPreview();
        }

        /// <summary>
        /// A new hole's centres, as Inventor's Hole picks them (task 117): the selected sketch, else the sketch of the
        /// selected points, with all their hole centres; else, per point, the newest visible sketch with hole centres
        /// no hole feature uses yet, and only those.
        /// </summary>
        private void StartDefaultPicks()
        {
            PlanarSketch? selectedSketch = null;
            PlanarSketch? sketchOfPoint = null;
            foreach (object raw in SelectedItems())
            {
                object? item = NativeOf(raw);
                if (selectedSketch == null && item is PlanarSketch sketch)
                    selectedSketch = sketch;
                else if (sketchOfPoint == null && item is SketchPoint point)
                    sketchOfPoint = ComSafe.Get(() => point.Parent as PlanarSketch);
            }

            List<UsedCentre>? used = null;
            DefaultCentres<PlanarSketch, SketchPoint> chosen = HoleSessionRules.DefaultCentres(
                selectedSketch,
                sketchOfPoint,
                SketchesNewestFirst(),
                HoleCentres,
                point => IsUsed(point, used ??= CentresUsedByHoles()));
            _defaultSketchFrom = chosen.Describe();
            _picks.Start(chosen.Sketch, chosen.Points);
        }

        /// <summary>
        /// What is selected now: the part's selection, then, when the part is edited in place, the active document's,
        /// where Inventor keeps it (spike 103). Items may be proxies; <see cref="NativeOf"/> takes them into the part.
        /// </summary>
        private List<object> SelectedItems()
        {
            var items = new List<object>();
            AddSelected(items, ComSafe.Get(() => _doc.SelectSet));
            Document? active = ComSafe.Get(() => _app.ActiveDocument);
            if (active != null && !ReferenceEquals(active, _doc))
                AddSelected(items, ComSafe.Get(() => active.SelectSet));
            return items;
        }

        private static void AddSelected(List<object> items, SelectSet? selection)
        {
            if (selection == null)
                return;
            ComSafe.Run(() =>
            {
                foreach (object item in selection)
                    items.Add(item);
            });
        }

        private IEnumerable<(PlanarSketch, Func<bool>)> SketchesNewestFirst()
        {
            PlanarSketches sketches = _doc.ComponentDefinition.Sketches;
            int count = ComSafe.Get(() => sketches.Count);
            for (int i = count; i >= 1; i--)
            {
                int index = i;
                PlanarSketch? sketch = ComSafe.Get(() => sketches[index]);
                if (sketch == null)
                    continue;
                yield return (sketch, () => ComSafe.Get(() => sketch.Visible));
            }
        }

        /// <summary>A sketch's points marked as hole centres, in sketch order.</summary>
        private static List<SketchPoint> HoleCentres(PlanarSketch sketch)
        {
            var centres = new List<SketchPoint>();
            ComSafe.Run(() =>
            {
                foreach (SketchPoint point in sketch.SketchPoints)
                {
                    if (ComSafe.Get(() => point.HoleCenter))
                        centres.Add(point);
                }
            });
            return centres;
        }

        /// <summary>A hole centre a hole feature uses, with its sketch's name and position read once.</summary>
        private sealed class UsedCentre
        {
            public UsedCentre(SketchPoint point)
            {
                Centre = point;
                Sketch = SketchName(ComSafe.Get(() => point.Parent as PlanarSketch));
                Position = ComSafe.Get(() => point.Geometry3d);
            }

            public SketchPoint Centre { get; }

            public string? Sketch { get; }

            public Point? Position { get; }
        }

        /// <summary>
        /// The sketch points the part's hole features already use as centres (task 117). Only hole features are read:
        /// they are what uses hole-centre points (Default, task 117). Read once, when the first point is asked about.
        /// </summary>
        private List<UsedCentre> CentresUsedByHoles()
        {
            var used = new List<UsedCentre>();
            ComSafe.Run(() =>
            {
                foreach (HoleFeature hole in _doc.ComponentDefinition.Features.HoleFeatures)
                {
                    ObjectCollection? points = ComSafe.Get(() => hole.HoleCenterPoints);
                    if (points == null)
                        continue;
                    foreach (object item in points)
                    {
                        if (item is SketchPoint point)
                            used.Add(new UsedCentre(point));
                    }
                }
            });
            return used;
        }

        /// <summary>Whether a hole feature uses <paramref name="point"/>: the same object, or the same sketch and
        /// position, as <see cref="SamePoint"/> compares.</summary>
        private static bool IsUsed(SketchPoint point, List<UsedCentre> used)
        {
            if (used.Count == 0)
                return false;
            if (used.Exists(u => ReferenceEquals(u.Centre, point)))
                return true;

            string? sketchName = SketchName(ComSafe.Get(() => point.Parent as PlanarSketch));
            Point? position = ComSafe.Get(() => point.Geometry3d);
            if (sketchName == null || position == null)
                return false;
            return used.Exists(u =>
                u.Position is Point usedPosition
                && string.Equals(u.Sketch, sketchName, StringComparison.Ordinal)
                && ComSafe.Get(() => usedPosition.IsEqualTo(position)));
        }

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
        /// Picking as Inventor's Hole does (spike 081 round 2): sketch points, one at a time; each click toggles a
        /// point, without Ctrl. A sketch clicked in the browser picks its hole centres instead (task 145). The picked
        /// centres are highlighted blue.
        /// </summary>
        private void StartPicking()
        {
            _highlight = CreateHighlightSet();
            HoleColour blue = HoleSessionRules.PickedColour;
            _highlight.SetColor(blue.R, blue.G, blue.B);

            StartInteraction();
        }

        /// <summary>
        /// The highlight set on the document Inventor shows (task 143): in place, a part's highlight set is not drawn in
        /// the assembly's view, so the set is made on the assembly and <see cref="Highlight"/> adds each point's proxy
        /// through the occurrence being edited. In the part's own window, or when no edited occurrence is found, the
        /// set is on the part, as before.
        /// </summary>
        private HighlightSet CreateHighlightSet()
        {
            _highlightOccurrence = null;
            if (ComSafe.Get(() => _app.ActiveDocument) is AssemblyDocument assembly && !ReferenceEquals(assembly, _doc))
            {
                ComponentOccurrence? occurrence = ComSafe.Get(() => assembly.ComponentDefinition.ActiveOccurrence);
                HighlightSet? set = occurrence == null ? null : ComSafe.Get(() => assembly.CreateHighlightSet());
                if (occurrence != null && set != null)
                {
                    _highlightOccurrence = occurrence;
                    return set;
                }

                _log.Warn(LogPrefix + "the part is shown in an assembly but no edited occurrence or highlight set was " +
                          "found there; the picked centres are highlighted on the part.");
            }

            return _doc.CreateHighlightSet();
        }

        /// <summary>
        /// The interaction events that pick the centres; made again when AWB Hole restarts the picking. Sketch points
        /// are picked anywhere; whole sketches are let through too, so a sketch clicked in the browser reaches
        /// <see cref="OnSelect"/> (task 145), and <see cref="OnPreSelect"/> keeps them from being picked in the
        /// graphics window, where only points are picked, as before.
        /// </summary>
        private void StartInteraction()
        {
            _interaction = _app.CommandManager.CreateInteractionEvents();
            _interaction.StatusBarText = HoleSessionRules.StatusBarText;
            _select = _interaction.SelectEvents;
            _select.AddSelectionFilter(SelectionFilterEnum.kSketchPointFilter);
            _select.AddSelectionFilter(SelectionFilterEnum.kSketchObjectFilter);
            _select.SingleSelectEnabled = true;
            _select.OnPreSelect += OnPreSelect;
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
            {
                ComSafe.Run(() => select.OnPreSelect -= OnPreSelect);
                ComSafe.Run(() => select.OnSelect -= OnSelect);
            }
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
                foreach (object raw in justSelected)
                {
                    // In place, Inventor gives the point or sketch as a proxy in the assembly; the part's own is used.
                    object? item = NativeOf(raw);
                    if (item is PlanarSketch clicked)
                    {
                        if (PickSketch(clicked, device))
                            changed = true;
                        continue;
                    }

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

        /// <summary>
        /// A sketch clicked in the browser (task 145): all its hole centres replace the picks, as for a sketch
        /// selected before the window opens, and the sketch is shown as the default sketch is (<see cref="HoleSessionRules.CentresOfPickedSketch"/>). A
        /// sketch picked in the graphics window is not taken: there only points are picked. Returns true when the
        /// picks changed. What happened is logged either way, with the selection device Inventor reported.
        /// </summary>
        private bool PickSketch(PlanarSketch sketch, SelectionDeviceEnum device)
        {
            string? name = SketchName(sketch);
            if (device == SelectionDeviceEnum.kGraphicsWindowSelection)
            {
                _log.Info(LogPrefix + $"{name ?? "a sketch"} was picked in the graphics window; only a click in the " +
                          "browser picks a sketch's centres.");
                return false;
            }

            PickedSketchCentres<SketchPoint> chosen = HoleSessionRules.CentresOfPickedSketch(
                _edit, InSessionPart(sketch), () => HoleCentres(sketch));
            _log.Info(LogPrefix + chosen.Describe(name) + $" (selection device {device})");
            if (!chosen.Replaces)
                return false;

            if (_picks.Replace(sketch, chosen.Points))
                _sketchChanged = true;
            ShowSketch(sketch);
            return true;
        }

        /// <summary>
        /// Whether <paramref name="sketch"/> (already the native object) is a sketch of the session's part: its parent
        /// component definition's document is the part, compared as <see cref="IsOurDocument"/> does (the same object,
        /// else the same full file name). In place, a sketch of another part or of the assembly itself is not (task 145
        /// review): taking it would pick another document's points and change its visibility outside the session's
        /// transaction. False when the owner cannot be read.
        /// </summary>
        private bool InSessionPart(PlanarSketch sketch)
        {
            object? owner = ComSafe.Get(() => (sketch.Parent as ComponentDefinition)?.Document);
            if (owner == null)
                return false;
            if (ReferenceEquals(owner, _doc))
                return true;
            if (owner is not Document document)
                return false;

            string? ours = ComSafe.Get(() => _doc.FullDocumentName);
            string? theirs = ComSafe.Get(() => document.FullDocumentName);
            return !string.IsNullOrEmpty(ours)
                && string.Equals(ours, theirs, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Keeps whole sketches from being highlighted, and so picked, in the graphics window (task 145): there a click
        /// picks points only, as before the sketch filter was added. Anything else is highlighted as Inventor would.
        /// </summary>
        private void OnPreSelect(
            ref object preSelectEntity, out bool doHighlight, ref ObjectCollection morePreSelectEntities,
            SelectionDeviceEnum selectionDevice, Point modelPosition, Point2d viewPosition, View view)
        {
            doHighlight = true;
            if (_ended || selectionDevice != SelectionDeviceEnum.kGraphicsWindowSelection)
                return;

            object entity = preSelectEntity;
            if (entity is PlanarSketch || entity is PlanarSketchProxy)
                doHighlight = false;
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

        /// <summary>
        /// The user is switching away from the document that was active when the session began (the assembly when the
        /// part is edited in place, the part in its own window): the session is cancelled before the switch, as
        /// Inventor's own commands are (spike 103 round 2; task 105).
        /// </summary>
        private void OnDeactivateDocument(
            _Document document, EventTimingEnum beforeOrAfter, NameValueMap context, out HandlingCodeEnum handlingCode)
        {
            handlingCode = HandlingCodeEnum.kEventNotHandled;
            if (_ended || _ending)
                return;

            try
            {
                bool before = beforeOrAfter == EventTimingEnum.kBefore;
                bool ours = before && _startDoc != null && ReferenceEquals(document, _startDoc);
                if (!ours)
                {
                    string? name = ComSafe.Get(() => document.FullDocumentName);
                    ours = HoleSessionRules.EndsOnDeactivate(_startDocName, name, before);
                }

                if (!ours)
                    return;

                _log.Info(LogPrefix + "another document is being activated; the session is cancelled.");
                Cancel(HoleSessionEndReason.ByInventor);
            }
            catch (Exception ex)
            {
                _log.Error(LogPrefix + "following a document switch failed; the session is cancelled.", ex);
                Cancel(HoleSessionEndReason.Failed);
            }
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
                    // Nothing changed; the preview is drawn again only if something removed it.
                    if (!_drawn)
                        DrawPreview();
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

            // Making a hole hides its sketch again; the highlight is redrawn over the new model. The preview graphics
            // are drawn from the hole the model holds now: the new one, or the last good one after a refusal.
            if (_picks.Sketch != null)
                ShowSketch(_picks.Sketch);
            long drawMs = DrawPreview();
            Highlight();

            watch.Stop();
            if (HoleSessionRules.IsSlow(watch.Elapsed))
                _log.Info(LogPrefix + $"preview {action} took {watch.ElapsedMilliseconds} ms ({drawMs} ms of it the preview graphics).");
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

        /// <summary>
        /// An inner transaction for one change to the part. The preview graphics are removed first; the caller draws
        /// them again from whatever hole the model holds afterwards (spike 107).
        /// </summary>
        private Transaction StartStep()
        {
            ClearPreview();
            return _app.TransactionManager.StartTransaction((_Document)_doc, HoleSessionRules.StepTransactionName);
        }

        // ---- preview graphics ----

        /// <summary>
        /// Draws the held hole's preview with client graphics (task 110), replacing any drawn before. A failure is
        /// logged and the preview goes on without graphics. Returns the milliseconds it took.
        /// </summary>
        private long DrawPreview()
        {
            ClearPreview();
            HoleFeature? hole = _hole;
            if (_ended || _outer == null || hole == null)
                return 0;

            var watch = Stopwatch.StartNew();
            // Set first, so graphics left by a draw that failed part way are still removed.
            _drawn = true;
            try
            {
                HolePreviewWriter.Draw(_app, _doc, hole, _log);
            }
            catch (Exception ex)
            {
                _log.Warn(LogPrefix + $"the preview of {_holeName} could not be drawn ({ex.Message}).");
                ClearPreview();
            }

            watch.Stop();
            if (HoleSessionRules.IsSlowDraw(watch.Elapsed))
                _log.Info(LogPrefix + $"drawing the preview took {watch.ElapsedMilliseconds} ms.");
            return watch.ElapsedMilliseconds;
        }

        /// <summary>Removes the preview graphics, if drawn. Called before every change and on every way out.</summary>
        private void ClearPreview()
        {
            if (!_drawn)
                return;
            _drawn = false;
            HolePreviewWriter.Clear(_doc);
        }

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
                _shownSketches.Add((sketch, ComSafe.Get(() => sketch.Visible), ComSafe.Get(() => sketch.Shared)));
            if (!ComSafe.Get(() => sketch.Visible))
                ComSafe.Run(() => sketch.Visible = true);
        }

        /// <summary>
        /// Puts every sketch the session showed back as it was; a kept new hole's own sketch ends hidden under the
        /// hole, as Inventor leaves it. Showing a consumed sketch does not share it (task 094's spike), but a hole on a
        /// sketch another feature already consumes does, and removing that hole again leaves it shared: such a sketch
        /// is set back to not shared, which Inventor refuses while two features use it.
        /// </summary>
        private void RestoreSketches(bool kept)
        {
            ClearPreview();
            foreach ((PlanarSketch sketch, bool wasVisible, bool wasShared) in _shownSketches)
            {
                if (HoleSessionRules.UnshareAfter(wasShared, ComSafe.Get(() => sketch.Shared)))
                {
                    ComSafe.Run(() => sketch.Shared = false);
                    if (ComSafe.Get(() => sketch.Shared))
                        _log.Info(LogPrefix + $"{SketchName(sketch)} stays shared: more than one feature uses it.");
                }

                bool holeSketch = _picks.Sketch != null && SameSketch(sketch, _picks.Sketch);
                bool sharedAfter = ComSafe.Get(() => sketch.Shared);
                bool visible = HoleSessionRules.SketchVisibleAfter(!_edit && holeSketch, kept, wasVisible, sharedAfter);
                if (ComSafe.Get(() => sketch.Visible) != visible)
                    ComSafe.Run(() => sketch.Visible = visible);
            }
        }

        /// <summary>
        /// The start log line's note on the sketch: how it was chosen, and whether it was hidden, consumed or shared
        /// before the session showed it.
        /// </summary>
        private string SketchStateText(PlanarSketch sketch)
        {
            var notes = new List<string>();
            if (_defaultSketchFrom != null)
                notes.Add(_defaultSketchFrom);
            int shown = _shownSketches.FindIndex(s => SameSketch(s.Sketch, sketch));
            bool wasVisible = shown >= 0 ? _shownSketches[shown].WasVisible : ComSafe.Get(() => sketch.Visible);
            bool wasShared = shown >= 0 ? _shownSketches[shown].WasShared : ComSafe.Get(() => sketch.Shared);
            notes.Add(wasVisible ? "visible" : "hidden");
            if (ComSafe.Get(() => sketch.Consumed))
                notes.Add("consumed");
            if (wasShared)
                notes.Add("shared");
            return " (" + string.Join(", ", notes) + ")";
        }

        private void Highlight()
        {
            HighlightSet? highlight = _highlight;
            if (highlight == null)
                return;

            ComSafe.Run(() => highlight.Clear());
            foreach (SketchPoint point in _picks.Points)
            {
                object? item = InViewOf(point);
                if (item != null)
                    ComSafe.Run(() => highlight.AddItem(item));
            }
            ComSafe.Run(() => _app.ActiveView.Update());
        }

        /// <summary>
        /// The object to highlight for <paramref name="point"/>: the point itself in the part's own window, its proxy
        /// through the edited occurrence in place (task 143). A proxy that cannot be made is logged once and skipped.
        /// </summary>
        private object? InViewOf(SketchPoint point)
        {
            ComponentOccurrence? occurrence = _highlightOccurrence;
            if (occurrence == null)
                return point;

            object? proxy = ComSafe.Get(() =>
            {
                occurrence.CreateGeometryProxy(point, out object result);
                return result;
            });
            if (proxy == null && !_proxyWarned)
            {
                _proxyWarned = true;
                _log.Warn(LogPrefix + "a picked centre could not be found in the assembly; it is not highlighted.");
            }
            return proxy;
        }

        /// <summary>
        /// A sketch point or sketch picked or selected in the assembly while the part is edited in place is a proxy;
        /// the part's own object is used instead (spike 103). Anything else is returned as it is. A proxy that gives no
        /// native object is logged and skipped (null).
        /// </summary>
        private object? NativeOf(object item)
        {
            object? native = item switch
            {
                SketchPointProxy point => ComSafe.Get(() => point.NativeObject),
                PlanarSketchProxy sketch => ComSafe.Get(() => sketch.NativeObject),
                _ => item,
            };
            if (native == null)
                _log.Warn(LogPrefix + "a sketch point or sketch picked in the assembly gave no object in the part; it is skipped.");
            return native;
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

            ClearPreview();
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
                ClearPreview();
                DropInteraction(stop: true);
                ComSafe.Run(() => _highlight?.Delete());
                if (_appEvents != null)
                {
                    ComSafe.Run(() => _appEvents.OnCloseDocument -= OnCloseDocument);
                    ComSafe.Run(() => _appEvents.OnDeactivateDocument -= OnDeactivateDocument);
                }

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
                _highlightOccurrence = null;
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
