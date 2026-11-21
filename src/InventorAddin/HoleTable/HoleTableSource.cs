using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Inventor;
using InventorAddin.Core.HoleTable;
using InventorAddin.Core.Holes;
using InventorAddin.Core.Logging;
using InventorAddin.Core.Settings;
using InventorAddin.Holes;

namespace InventorAddin.HoleTables
{
    /// <summary>
    /// What the add-in keeps for one hole centre after <see cref="HoleTableSource.Analyze"/>, for the tasks that draw
    /// tags and tables (164, 165): the feature, the centre point and the view's drawing curves at that centre.
    /// </summary>
    public sealed class HoleTableHoleRef
    {
        public HoleTableHoleRef(HoleFeature feature, SketchPoint centre, IReadOnlyList<DrawingCurve> curves)
        {
            Feature = feature;
            Centre = centre;
            Curves = curves;
        }

        public HoleFeature Feature { get; }

        public SketchPoint Centre { get; }

        /// <summary>The view's circles (<c>kCircleCurve</c>) whose centre is at this hole's centre on the sheet.</summary>
        public IReadOnlyList<DrawingCurve> Curves { get; }
    }

    /// <summary>
    /// The add-in's <see cref="IHoleTableSource"/> (task 163): lists the active drawing's sheets and views and reads a
    /// part view's holes, ordinate origin, scale and units the way spike 160 proved. Sheets and views are keyed by a
    /// GUID kept in an attribute set (<see cref="HoleTableKeys"/>), written the first time they are listed, so the key
    /// survives a rename or a reorder. Drawing tables is task 164 (HoleTableDrawer); tags and marks are task 165; Update and the stored state
    /// (<see cref="HoleTableStateStore"/>) are task 166.
    /// </summary>
    public sealed class HoleTableSource : IHoleTableSource
    {
        private const string LogPrefix = "Hole table: ";

        private readonly DrawingDocument _drawing;
        private readonly AddinSettings _settings;
        private readonly ILog _log;
        private readonly Dictionary<string, IReadOnlyDictionary<string, HoleTableHoleRef>> _holeRefs = new();
        private HoleWorkbook? _workbook;

        public HoleTableSource(DrawingDocument drawing, AddinSettings settings, ILog log)
        {
            _drawing = drawing ?? throw new ArgumentNullException(nameof(drawing));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _log = log ?? NullLog.Instance;
        }

        public string DrawingName => ComSafe.Get(() => _drawing.DisplayName) ?? "";

        public IReadOnlyList<HoleTableSheetInfo> GetSheets() => ListSheets(assignKeys: true);

        /// <summary>
        /// The sheets that already have a key (task 176), for the automatic check. Unlike <see cref="GetSheets"/> it never
        /// calls <c>WriteKeys</c>, so it starts no transaction: no undo entry, and the drawing is not marked changed. A sheet
        /// with no key cannot hold stored tables, so leaving it out loses nothing.
        /// </summary>
        public IReadOnlyList<HoleTableSheetInfo> GetKeyedSheets() => ListSheets(assignKeys: false);

        private IReadOnlyList<HoleTableSheetInfo> ListSheets(bool assignKeys)
        {
            var warnings = new List<string>();
            var list = new List<HoleTableSheetInfo>();
            Sheet? active = ComSafe.Get(() => _drawing.ActiveSheet, warnings, "Active sheet");
            var sheets = new List<Sheet>();
            foreach (Sheet sheet in _drawing.Sheets)
                sheets.Add(sheet);

            var sets = sheets.Select(s => (Func<AttributeSets>)(() => s.AttributeSets)).ToList();
            List<string?> keys = ReadKeys(sets, warnings, out List<int> needKey);
            if (assignKeys)
                WriteKeys(sets, keys, needKey, warnings, "Sheet key");
            for (int i = 0; i < sheets.Count; i++)
            {
                Sheet sheet = sheets[i];
                if (keys[i] is { } key)
                    list.Add(new HoleTableSheetInfo(key, ComSafe.Get(() => sheet.Name, warnings, "Sheet name") ?? "", sheet == active));
            }

            Report(warnings);
            return list;
        }

        public IReadOnlyList<HoleTableViewInfo> GetViews(string sheetKey) => ListViews(sheetKey, assignKeys: true);

        /// <summary>
        /// The views of the sheet that already have a key (task 172), for the on-open check. Unlike <see cref="GetViews"/> it
        /// never calls <c>WriteKeys</c>, so it starts no transaction: no undo entry, and the drawing is not marked changed.
        /// A view with no key cannot have stored tables, so leaving it out loses nothing. A key already given out earlier in
        /// the list (a copied view) is skipped, as <see cref="GetViews"/> would give that view a new key.
        /// </summary>
        public IReadOnlyList<HoleTableViewInfo> GetKeyedViews(string sheetKey) => ListViews(sheetKey, assignKeys: false);

        private IReadOnlyList<HoleTableViewInfo> ListViews(string sheetKey, bool assignKeys)
        {
            var warnings = new List<string>();
            var list = new List<HoleTableViewInfo>();
            Sheet? sheet = FindSheet(sheetKey, warnings);
            if (sheet == null)
            {
                Report(warnings);
                return list;
            }

            var views = new List<DrawingView>();
            foreach (DrawingView view in sheet.DrawingViews)
                views.Add(view);

            var sets = views.Select(v => (Func<AttributeSets>)(() => v.AttributeSets)).ToList();
            List<string?> keys = ReadKeys(sets, warnings, out List<int> needKey);
            if (assignKeys)
                WriteKeys(sets, keys, needKey, warnings, "View key");
            for (int i = 0; i < views.Count; i++)
            {
                DrawingView view = views[i];
                if (keys[i] is { } key)
                    list.Add(new HoleTableViewInfo(key, ComSafe.Get(() => view.Name, warnings, "View name") ?? ""));
            }

            Report(warnings);
            return list;
        }

        public HoleTableViewAnalysis Analyze(string sheetKey, string viewKey)
        {
            var warnings = new List<string>();
            try
            {
                return AnalyzeCore(sheetKey, viewKey, warnings);
            }
            finally
            {
                Report(warnings);
            }
        }

        /// <summary>
        /// The feature, centre point and drawing curves of a hole read by the last <see cref="Analyze"/> of that view, by
        /// the hole's <see cref="HoleTableHole.Id"/>; null when the hole was not read.
        /// </summary>
        public HoleTableHoleRef? FindHole(string sheetKey, string viewKey, string holeId) =>
            _holeRefs.TryGetValue(sheetKey + "|" + viewKey, out var map) && map.TryGetValue(holeId, out var found) ? found : null;

        // Each table action is one transaction, so one Undo removes what it drew; a failure aborts it (task 164).
        public string GenerateTable(string sheetKey, string viewKey, HoleTableKind kind, HoleTablePlan plan, HoleTableState state) =>
            DrawTables(HoleTableNames.GenerateTransactionName(kind), sheetKey, viewKey, plan, state, new[] { kind }, align: false);

        public string CreateTables(string sheetKey, string viewKey, HoleTablePlan plan, HoleTableState state) =>
            DrawTables(
                HoleTableNames.CreateTransactionName, sheetKey, viewKey, plan, state,
                new[] { HoleTableKind.Precision, HoleTableKind.Clearance }, align: true);

        public string UpdateTables(string sheetKey, string viewKey, HoleTablePlan plan, HoleTableState state) =>
            InTableTransaction(HoleTableNames.UpdateTransactionName, () =>
            {
                var warnings = new List<string>();
                try
                {
                    Sheet? sheet = FindSheet(sheetKey, warnings);
                    DrawingView? view = sheet == null ? null : FindView(sheet, viewKey, warnings);
                    if (sheet == null || view == null)
                        return HoleTableDrawResult.Unchanged("That view could not be found on the sheet.");
                    return UpdateCore(sheet, view, sheetKey, viewKey, plan, state);
                }
                finally
                {
                    Report(warnings);
                }
            });

        // The stored state is read from the view's placed table symbols; null when the view has none.
        public HoleTableState? ReadState(string sheetKey, string viewKey)
        {
            var warnings = new List<string>();
            try
            {
                Sheet? sheet = FindSheet(sheetKey, warnings);
                return sheet == null ? null : ReadStored(sheet, sheetKey, viewKey);
            }
            finally
            {
                Report(warnings);
            }
        }

        // The stored state from the table symbols, else the sheet's record. A stored text that cannot be read throws with
        // the reason, so the window says so instead of "no stored tables".
        private HoleTableState? ReadStored(Sheet sheet, string sheetKey, string viewKey)
        {
            HoleTableStoredState stored = new HoleTableStateStore(_log).Read(sheet, sheetKey, viewKey);
            if (stored.State == null && stored.Reason != null)
                throw new InvalidOperationException(HoleTableMessages.StoredStateUnreadable(stored.Reason));
            return stored.State;
        }

        public string AlignTables(string sheetKey, string viewKey) =>
            InTableTransaction(HoleTableNames.AlignTransactionName, () =>
            {
                var warnings = new List<string>();
                try
                {
                    Sheet? sheet = FindSheet(sheetKey, warnings);
                    DrawingView? view = sheet == null ? null : FindView(sheet, viewKey, warnings);
                    if (sheet == null || view == null)
                        return HoleTableDrawResult.Unchanged("That view could not be found on the sheet.");
                    string sheetName = ComSafe.Get(() => sheet.Name) ?? "";
                    string viewName = ComSafe.Get(() => view.Name) ?? "";
                    return new HoleTableDrawer(_drawing, _log).Align(sheet, sheetKey, viewKey);
                }
                finally
                {
                    Report(warnings);
                }
            });

        private string DrawTables(
            string transactionName, string sheetKey, string viewKey, HoleTablePlan plan, HoleTableState state, IReadOnlyList<HoleTableKind> kinds, bool align) =>
            InTableTransaction(transactionName, () =>
            {
                var warnings = new List<string>();
                try
                {
                    Sheet? sheet = FindSheet(sheetKey, warnings);
                    DrawingView? view = sheet == null ? null : FindView(sheet, viewKey, warnings);
                    if (sheet == null || view == null)
                        return HoleTableDrawResult.Unchanged("That view could not be found on the sheet.");
                    string sheetName = ComSafe.Get(() => sheet.Name) ?? "";
                    string viewName = ComSafe.Get(() => view.Name) ?? "";
                    HoleTableDrawResult drawn = new HoleTableDrawer(_drawing, _log).Draw(sheet, view, sheetKey, viewKey, sheetName, viewName, plan, kinds, align);
                    if (!drawn.Changed)
                        return drawn;

                    // Tags and marks join the tables' transaction (task 165), so one Undo removes everything.
                    string marks = MarkHoles(sheet, view, sheetKey, viewKey, plan, kinds, null, 0);

                    // The state goes on the placed symbols in the same transaction (task 166).
                    new HoleTableStateStore(_log).Write(sheet, sheetKey, viewKey, state);
                    return HoleTableDrawResult.Done(string.IsNullOrEmpty(marks) ? drawn.Message : drawn.Message + " " + marks);
                }
                finally
                {
                    Report(warnings);
                }
            });

        // Puts the tags, precision marks and centre marks on the view for the holes of the tables just drawn. The view is
        // analysed again first: the COM objects kept by the last Analyze can be stale by now. The caller holds the transaction.
        private string MarkHoles(Sheet sheet, DrawingView view, string sheetKey, string viewKey, HoleTablePlan plan, IReadOnlyList<HoleTableKind> kinds,
            ISet<string>? only, int removedCount)
        {
            HoleTableViewAnalysis fresh = Analyze(sheetKey, viewKey);
            if (fresh.FailureMessage != null)
                throw new InvalidOperationException(fresh.FailureMessage);

            var centres = fresh.Holes.ToDictionary(h => h.Id, h => new SheetPoint(h.SheetXCm, h.SheetYCm), StringComparer.Ordinal);
            var targets = new List<HoleMarkTarget>();
            int missing = 0;
            foreach (HoleTableKind kind in kinds)
            {
                IEnumerable<HoleTableGroup> groups = kind == HoleTableKind.Precision ? plan.Precision : plan.Clearance;
                foreach (var row in groups.SelectMany(g => g.Rows))
                {
                    string id = row.Hole.Id;
                    if (only != null && !only.Contains(id))
                        continue;
                    HoleTableHoleRef? found = FindHole(sheetKey, viewKey, id);
                    if (found == null || !centres.TryGetValue(id, out SheetPoint centre))
                    {
                        missing++;
                        _log.Warn(LogPrefix + $"hole '{id}' is no longer in the view; no tag or marks.");
                        continue;
                    }

                    targets.Add(new HoleMarkTarget(id, row.Tag, HoleTableMarks.HasPrecisionMark(kind), centre, found));
                }
            }

            HoleMarkResult result = new HoleTableMarker(_drawing, _log).Place(sheet, view, viewKey, targets);
            return removedCount > 0 || only != null
                ? HoleTableMarkSync.Summary(result.Marked, result.Skipped + missing, removedCount)
                : HoleTableMarks.Summary(result.Marked, result.Skipped + missing);
        }

        // Update (task 166), inside the caller's one transaction. The plan is the refreshed one (tags kept); this redraws
        // the tables in place, deletes a table that has no group left, then brings the tags and marks in line with it.
        private HoleTableDrawResult UpdateCore(
            Sheet sheet, DrawingView view, string sheetKey, string viewKey, HoleTablePlan plan, HoleTableState state)
        {
            var store = new HoleTableStateStore(_log);
            HoleTableState? previous = ReadStored(sheet, sheetKey, viewKey);
            if (previous == null)
                return HoleTableDrawResult.Unchanged("This view has no stored hole tables. Create them first.");

            string sheetName = ComSafe.Get(() => sheet.Name) ?? "";
            string viewName = ComSafe.Get(() => view.Name) ?? "";
            var drawer = new HoleTableDrawer(_drawing, _log);

            // The clearance table's place before it can be deleted below, so Draw can drop a stacked precision table onto it (task 178).
            HoleTablePlacement? clearanceBefore = drawer.ReadPlacement(sheet, sheetKey, viewKey, HoleTableKind.Clearance);

            var live = new List<HoleTableKind>();
            var removed = new List<HoleTableKind>();
            foreach (HoleTableKind kind in new[] { HoleTableKind.Precision, HoleTableKind.Clearance })
            {
                if (GroupsOf(plan, kind).Count > 0)
                    live.Add(kind);
                else if (drawer.DeleteTable(sheet, sheetKey, viewKey, kind))
                    removed.Add(kind);
            }

            string message = "";
            if (live.Count > 0)
            {
                HoleTableDrawResult drawn = drawer.Draw(sheet, view, sheetKey, viewKey, sheetName, viewName, plan, live, align: false, deletedClearance: clearanceBefore);
                if (!drawn.Changed)
                    throw new InvalidOperationException(drawn.Message); // aborts the transaction, so nothing is left half done
                message = drawn.Message;
            }
            else
            {
                message = removed.Count > 0 ? "There are no holes left to put in a table." : HoleTableMessages.Drawn(live, viewName);
            }

            if (removed.Count > 0)
            {
                string names = string.Join(" and ", removed);
                message += $" Deleted the {names} {(removed.Count == 1 ? "table" : "tables")}, which had no holes left.";
            }

            // Tags and marks: only the holes that need it (new, a changed tag, a missing mark); the gone ones' marks go.
            var marker = new HoleTableMarker(_drawing, _log);
            var wanted = new List<HoleMarkWanted>();
            foreach (HoleTableKind kind in live)
            {
                foreach (InventorAddin.Core.HoleTable.HoleTableRow row in GroupsOf(plan, kind).SelectMany(g => g.Rows))
                    wanted.Add(new HoleMarkWanted(row.Hole.Id, row.Tag, HoleTableMarks.HasPrecisionMark(kind)));
            }

            var previousTags = previous.Tags.ByHole.ToDictionary(p => p.Key, p => p.Value.Text, StringComparer.Ordinal);
            HoleMarkSyncPlan sync = HoleTableMarkSync.Decide(wanted, previousTags, marker.ReadMarks(sheet, viewKey));
            marker.Remove(sheet, viewKey, sync.Remove.ToList());
            string marks = sync.Place.Count > 0
                ? MarkHoles(sheet, view, sheetKey, viewKey, plan, live, sync.Place.ToHashSet(StringComparer.Ordinal), sync.Remove.Count)
                : HoleTableMarkSync.Summary(0, 0, sync.Remove.Count);

            // Also when both tables are gone: the sheet's record keeps the view's tags, notes and edits.
            store.Write(sheet, sheetKey, viewKey, state);
            return HoleTableDrawResult.Done((message + " " + marks).Trim());
        }

        private static IReadOnlyList<HoleTableGroup> GroupsOf(HoleTablePlan plan, HoleTableKind kind) =>
            kind == HoleTableKind.Precision ? plan.Precision : plan.Clearance;

        // One transaction around a table action. If none can be started the action does not run, because a failure
        // half way could not be undone. A failure aborts the transaction and is rethrown for the window's status line.
        // A call that changed nothing (no title block, view not found, style unreadable) aborts too, so no empty Undo entry is left.

        private string InTableTransaction(string name, Func<HoleTableDrawResult> action)
        {
            Transaction transaction = InventorHost.App.TransactionManager.StartTransaction((_Document)_drawing, name);
            try
            {
                HoleTableDrawResult result = action();
                if (!result.Changed)
                {
                    // Nothing changed: abort, and throw so the window shows "Could not ..." and keeps its tags (task 166 review).
                    transaction.Abort();
                    throw new HoleTableUnchangedException(result.Message);
                }

                transaction.End();
                return result.Message;
            }
            catch (HoleTableUnchangedException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _log.Warn(LogPrefix + $"'{name}' failed and was undone: {ex.Message}");
                try { transaction.Abort(); } catch (Exception) { }
                throw;
            }
        }

        // Task 169. COM pumps messages while Inventor answers, so an exception from our own UI (a UI Automation
        // request to a dropdown) can unwind out through this read. That is not an Inventor error: log its type, and
        // read once more. A COMException (or an Inventor error) is not retried.
        private Point2d? ReadIndicatorPoint(DrawingView view, List<string> warnings)
        {
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    return view.OriginIndicator.Intent.PointOnSheet;
                }
                catch (Exception ex)
                {
                    bool fromInventor = ex is System.Runtime.InteropServices.COMException;
                    if (fromInventor || attempt >= 2)
                    {
                        warnings.Add($"View origin point: {ex.Message}");
                        if (!fromInventor)
                            _log.Warn(LogPrefix + $"View origin point failed twice with {ex.GetType().FullName}");
                        return null;
                    }
                    _log.Warn(LogPrefix + $"View origin point read hit {ex.GetType().FullName}, not a COMException; retrying once");
                }
            }
        }

        // Task 168. The view's ordinate origin, tried in order: (1) the origin indicator's point; (2) skipped, since
        // RelativeX/RelativeY and the intent's geometry are not confirmed; (3a) an ordinate set's OriginMember on this
        // view; (3b) a point where zero-valued single ordinates of two different DimensionTypes (X and Y) meet, because
        // one zero ordinate alone can be a hole lined up with the origin on one axis. Nothing read, but an indicator
        // or ordinates exist (or the indicator could not even be asked): unreadable, not "no origin".
        private List<SheetPoint> ReadOrigins(Sheet sheet, DrawingView view, List<string> warnings, out bool unreadable)
        {
            var origins = new List<SheetPoint>();
            string viewName = ComSafe.Get(() => view.Name) ?? "(view)";
            bool? hasIndicator = ComSafe.Get(() => (bool?)view.HasOriginIndicator, warnings, "View origin");
            if (hasIndicator == true)
            {
                Point2d? p = ReadIndicatorPoint(view, warnings);
                if (p != null)
                {
                    origins.Add(new SheetPoint(p.X, p.Y));
                    _log.Info(LogPrefix + $"{viewName} origin from the origin indicator");
                    unreadable = false;
                    return origins;
                }
            }

            bool sawOrdinates = false;
            string why = hasIndicator == true ? " (indicator point unreadable)"
                : hasIndicator == null ? " (origin indicator could not be asked)" : " (no origin indicator)";

            ComSafe.Run(() =>
            {
                foreach (OrdinateDimensionSet set in sheet.DrawingDimensions.OrdinateDimensionSets)
                {
                    OrdinateDimension? member = ComSafe.Get(() => set.OriginMember, warnings, "Ordinate set origin member");
                    if (member == null || OrdinateOnView(member, view, warnings) != true)
                        continue;
                    sawOrdinates = true;
                    Point2d? p = ComSafe.Get(() => member.Intent.PointOnSheet, warnings, "Ordinate set origin point");
                    if (p != null)
                        origins.Add(new SheetPoint(p.X, p.Y));
                }
            }, warnings, "Ordinate dimension sets");

            if (origins.Count > 0)
            {
                _log.Info(LogPrefix + $"{viewName} origin from an ordinate set's origin member{why}");
            }
            else
            {
                var zeros = new List<(SheetPoint Point, DimensionTypeEnum Type)>();
                ComSafe.Run(() =>
                {
                    foreach (OrdinateDimension dim in sheet.DrawingDimensions.OrdinateDimensions)
                    {
                        if (OrdinateOnView(dim, view, warnings) != true)
                            continue;
                        sawOrdinates = true;
                        double? value = ComSafe.Get(() => (double?)dim.ModelValue, warnings, "Ordinate value");
                        if (value == null || Math.Abs(value.Value) > 1e-6)
                            continue;
                        Point2d? p = ComSafe.Get(() => dim.Intent.PointOnSheet, warnings, "Ordinate origin point");
                        DimensionTypeEnum? type = ComSafe.Get(() => (DimensionTypeEnum?)dim.DimensionType, warnings, "Ordinate type");
                        if (p != null && type != null)
                            zeros.Add((new SheetPoint(p.X, p.Y), type.Value));
                    }
                }, warnings, "Ordinate dimensions");

                foreach (var a in zeros)
                {
                    bool met = zeros.Any(b => b.Type != a.Type
                        && Math.Abs(b.Point.XCm - a.Point.XCm) <= HoleTableOriginResult.SamePointToleranceCm
                        && Math.Abs(b.Point.YCm - a.Point.YCm) <= HoleTableOriginResult.SamePointToleranceCm);
                    if (met)
                        origins.Add(a.Point);
                }

                if (origins.Count > 0)
                    _log.Info(LogPrefix + $"{viewName} origin from zero-valued ordinate dimensions of two types{why}");
            }

            unreadable = origins.Count == 0 && (hasIndicator != false || sawOrdinates);
            if (origins.Count == 0)
                _log.Info(LogPrefix + $"{viewName} origin: none read from the indicator or ordinate dimensions" + (unreadable ? " (unreadable)" : " (none exist)"));
            return origins;
        }

        // True when the ordinate belongs to this view (its geometry intent's curve, or a centre mark's attached curve,
        // has this view as parent); null, with a warning, when that cannot be told.
        private static bool? OrdinateOnView(OrdinateDimension dim, DrawingView view, List<string> warnings)
        {
            DrawingView? parent = ComSafe.Get(() =>
            {
                object? geometry = dim.Intent.Geometry;
                if (geometry is Centermark centermark)
                    geometry = centermark.AttachedEntity;
                return (geometry as DrawingCurve)?.Parent;
            }, warnings, "Ordinate's view");
            if (parent == null)
            {
                warnings.Add("Ordinate dimension skipped: its view could not be determined.");
                return null;
            }

            return parent == view;
        }

        private HoleTableViewAnalysis AnalyzeCore(string sheetKey, string viewKey, List<string> warnings)
        {
            Sheet? sheet = FindSheet(sheetKey, warnings);
            if (sheet == null)
                return HoleTableViewAnalysis.Failed("That sheet could not be found in the drawing.");
            DrawingView? view = FindView(sheet, viewKey, warnings);
            if (view == null)
                return HoleTableViewAnalysis.Failed("That view could not be found on the sheet.");

            Document? referenced = ComSafe.Get(() => view.ReferencedDocumentDescriptor.ReferencedDocument as Document, warnings, "View's model");
            if (referenced == null)
                return HoleTableViewAnalysis.Failed("The model this view shows could not be read.");
            if (referenced is not PartDocument part)
                return HoleTableViewAnalysis.Failed("Hole tables work on views of a single part for now. This view shows an assembly.");

            HoleUnits drawingUnits = ReadDrawingUnits(warnings);
            double scale = ComSafe.Get(() => view.Scale, warnings, "View scale");
            if (scale <= 0)
                scale = 1;

            // The view's direction: from the camera's eye to its target.
            double[]? dir = ComSafe.Get(() => ViewDirection(view), warnings, "View direction");
            if (dir == null)
                return HoleTableViewAnalysis.Failed("The view's direction could not be read.");

            var origins = ReadOrigins(sheet, view, warnings, out bool originUnreadable);

            var holes = new List<HoleTableHole>();
            var refs = new Dictionary<string, HoleTableHoleRef>();
            int leftOut = 0;
            List<(DrawingCurve Curve, Point2d Centre)>? curves = null;

            HoleFeatures? features = ComSafe.Get(() => part.ComponentDefinition.Features.HoleFeatures, warnings, "Hole features");
            if (features != null)
            {
                foreach (HoleFeature hole in features)
                {
                    string name = ComSafe.Get(() => hole.Name) ?? "(hole)";
                    try
                    {
                        if (ComSafe.Get(() => hole.Suppressed))
                            continue;

                        List<SketchPoint> centrePoints = ReadCentrePoints(hole);
                        bool? endOn = EndOn(hole, dir, warnings, name);
                        if (endOn != true)
                        {
                            leftOut += centrePoints.Count;
                            continue;
                        }

                        curves ??= ReadCurves(view, warnings);
                        ReadHole(part, view, hole, name, centrePoints, curves, drawingUnits, holes, refs, warnings);
                    }
                    catch (Exception ex)
                    {
                        // A hole that throws is skipped; the analysis goes on.
                        _log.Warn(LogPrefix + $"hole '{name}' skipped: {ex.Message}");
                    }
                }
            }

            _holeRefs[sheetKey + "|" + viewKey] = refs;
            return new HoleTableViewAnalysis(holes.AsReadOnly(), origins.AsReadOnly(), scale, drawingUnits, leftOut, null, originUnreadable);
        }

        private void ReadHole(
            PartDocument part,
            DrawingView view,
            HoleFeature hole,
            string name,
            List<SketchPoint> centrePoints,
            List<(DrawingCurve Curve, Point2d Centre)> curves,
            HoleUnits drawingUnits,
            List<HoleTableHole> holes,
            Dictionary<string, HoleTableHoleRef> refs,
            List<string> warnings)
        {
            InventorHoleFacts facts = HoleFeatureReader.ReadFacts(hole, warnings);
            HoleStoredDataReadResult? stored = ComSafe.Get(() => HoleStoredDataStore.Read(hole), warnings, "AWB hole data");
            HoleSpec? awbSpec = stored?.Data?.Spec;
            HoleUnits units = awbSpec?.Units ?? drawingUnits;

            HoleTolerance? tolerance = facts.HasTolerance
                ? new HoleTolerance(
                    facts.ToleranceIsoCode ?? HolePrecision.CustomName,
                    facts.ToleranceIsoCode,
                    facts.ToleranceUpperCm ?? 0,
                    facts.ToleranceLowerCm ?? 0)
                : null;

            // The thread class and a taper thread's engagement length are not in the facts (task 156 follow-up).
            string? threadClass = null;
            double? engagementCm = null;
            if (facts.Tapped)
            {
                object? tapInfo = ComSafe.Get(() => hole.TapInfo, warnings, "Thread");
                if (tapInfo is HoleTapInfo tap)
                {
                    string? cls = ComSafe.Get(() => tap.Class, warnings, "Thread class");
                    threadClass = string.IsNullOrWhiteSpace(cls) ? null : cls.Trim();
                }
                else if (tapInfo is TaperedThreadInfo taper)
                {
                    // EngagementLength is a double in millimetres (spike 160).
                    double? mm = ComSafe.Get(() => (double?)taper.EngagementLength, warnings, "Taper thread engagement");
                    engagementCm = mm is > 0 ? mm / 10 : null;
                }
            }

            TaperTappedRow? taperRow = facts.TaperedThread ? FindTaperRow(facts) : null;
            bool straightTapped = facts.Tapped && !facts.TaperedThread;
            TappedRow? tappedRow = straightTapped ? FindTappedRow(facts) : null;
            double? storedDiameterCm = straightTapped ? stored?.Data?.Applied.HoleDiameterCm : null;

            // The centres: sheet position, reference key and 1-based index.
            var centres = new List<HoleTableCentre>();
            var kept = new List<SketchPoint>();
            int index = 0;
            foreach (SketchPoint point in centrePoints)
            {
                index++;
                Point2d? sheetPoint = ComSafe.Get(() => view.ModelToSheetSpace(point.Geometry3d), warnings, $"{name} centre {index}");
                string? key = ReferenceKeyOf(part, point, warnings, $"{name} centre {index} key");
                if (sheetPoint == null || key == null)
                    continue;
                centres.Add(new HoleTableCentre(key, index, sheetPoint.X, sheetPoint.Y));
                kept.Add(point);
            }

            IReadOnlyList<HoleTableHole> built = HoleTableHoles.From(name, facts, awbSpec, units, centres, taperRow, tolerance, storedDiameterCm, tappedRow);
            if (straightTapped && built.Count > 0 && built[0].DrillDiameterCm == null)
                _log.Warn(LogPrefix + $"{name} is tapped but its tap drill could not be read; its hole line is left out.");
            for (int i = 0; i < built.Count; i++)
            {
                HoleTableHole item = built[i] with { ThreadClass = threadClass, TaperEngagementCm = engagementCm };
                holes.Add(item);
                refs[item.Id] = new HoleTableHoleRef(hole, kept[i], CurvesAt(curves, centres[i].SheetXCm, centres[i].SheetYCm));
            }
        }

        /// <summary>
        /// Whether the hole is seen end on (<see cref="HoleTableViewRules.IsEndOn"/>); null when its axis cannot be read.
        /// The caller counts such a hole's centres as left out (a Default), with the warning logged here.
        /// </summary>
        private bool? EndOn(HoleFeature hole, double[] dir, List<string> warnings, string name)
        {
            double[]? axis = ComSafe.Get(() => HoleAxis(hole), warnings, $"{name} axis");
            if (axis == null)
            {
                _log.Warn(LogPrefix + $"hole '{name}' counted as left out: no cylinder or cone side face to take its axis from.");
                return null;
            }

            return HoleTableViewRules.IsEndOn(axis[0], axis[1], axis[2], dir[0], dir[1], dir[2]);
        }

        // The axis of the first side face that is a cylinder or a cone (a taper tapped hole has no cylinder).
        private static double[]? HoleAxis(HoleFeature hole)
        {
            foreach (Face face in hole.SideFaces)
            {
                SurfaceTypeEnum type = face.SurfaceType;
                if (type == SurfaceTypeEnum.kCylinderSurface)
                {
                    UnitVector v = ((Cylinder)face.Geometry).AxisVector;
                    return new[] { v.X, v.Y, v.Z };
                }

                if (type == SurfaceTypeEnum.kConeSurface)
                {
                    UnitVector v = ((Cone)face.Geometry).AxisVector;
                    return new[] { v.X, v.Y, v.Z };
                }
            }

            return null;
        }

        private static double[]? ViewDirection(DrawingView view)
        {
            Camera camera = view.Camera;
            Point eye = camera.Eye;
            Point target = camera.Target;
            double x = target.X - eye.X, y = target.Y - eye.Y, z = target.Z - eye.Z;
            double length = Math.Sqrt(x * x + y * y + z * z);
            return length < 1e-12 ? null : new[] { x / length, y / length, z / length };
        }

        private static List<SketchPoint> ReadCentrePoints(HoleFeature hole)
        {
            var list = new List<SketchPoint>();
            foreach (object o in hole.HoleCenterPoints)
            {
                if (o is SketchPoint p)
                    list.Add(p);
            }

            return list;
        }

        // The view's circles (kCircleCurve, as spike 160 round 7 used), each with its centre read once. C# can leave out
        // the optional argument that late-bound callers pass Missing for. Arcs (kCircularArcCurve) are left out until
        // task 164 or 165 needs them.
        private static List<(DrawingCurve Curve, Point2d Centre)> ReadCurves(DrawingView view, List<string> warnings)
        {
            var list = new List<(DrawingCurve, Point2d)>();
            ComSafe.Run(() =>
            {
                foreach (DrawingCurve curve in view.DrawingCurves)
                {
                    if (ComSafe.Get(() => curve.CurveType) != CurveTypeEnum.kCircleCurve)
                        continue;
                    Point2d? centre = ComSafe.Get(() => curve.CenterPoint);
                    if (centre != null)
                        list.Add((curve, centre));
                }
            }, warnings, "View curves");
            return list;
        }

        private static IReadOnlyList<DrawingCurve> CurvesAt(List<(DrawingCurve Curve, Point2d Centre)> curves, double x, double y)
        {
            const double tolerance = 1e-3;
            var found = new List<DrawingCurve>();
            foreach (var (curve, c) in curves)
            {
                if (Math.Abs(c.X - x) < tolerance && Math.Abs(c.Y - y) < tolerance)
                    found.Add(curve);
            }

            return found;
        }

        // spike 160 C11: the sketch point's reference key, from the part's manager, as a string.
        private static string? ReferenceKeyOf(PartDocument part, SketchPoint point, List<string> warnings, string context) =>
            ComSafe.Get(() =>
            {
                byte[] key = new byte[0];
                point.GetReferenceKey(ref key, 0);
                return part.ReferenceKeyManager.KeyToString(ref key);
            }, warnings, context);

        private TaperTappedRow? FindTaperRow(InventorHoleFacts facts)
        {
            HoleWorkbook? workbook = LoadWorkbook();
            return workbook == null ? null : HoleReadBack.TaperTappedRowFor(facts, workbook);
        }

        private TappedRow? FindTappedRow(InventorHoleFacts facts)
        {
            HoleWorkbook? workbook = LoadWorkbook();
            return workbook == null ? null : HoleReadBack.TappedRowFor(facts, workbook);
        }

        private HoleWorkbook? LoadWorkbook()
        {
            if (_workbook != null)
                return _workbook;
            try
            {
                _workbook = HoleWorkbookLoader.Load(_settings, _log).Workbook;
            }
            catch (Exception ex)
            {
                _log.Warn(LogPrefix + "the hole workbook could not be read; taper tap drills come from Inventor: " + ex.Message);
            }

            return _workbook;
        }

        private HoleUnits ReadDrawingUnits(List<string> warnings)
        {
            UnitsTypeEnum? units = ComSafe.Get(() => (UnitsTypeEnum?)_drawing.UnitsOfMeasure.LengthUnits, warnings, "Drawing units");
            return HoleTableViewRules.UnitsFor(units?.ToString());
        }

        // ---- Keys

        private Sheet? FindSheet(string sheetKey, List<string> warnings)
        {
            foreach (Sheet sheet in _drawing.Sheets)
            {
                if (ReadKey(() => sheet.AttributeSets, warnings) == sheetKey)
                    return sheet;
            }

            return null;
        }

        private static DrawingView? FindView(Sheet sheet, string viewKey, List<string> warnings)
        {
            foreach (DrawingView view in sheet.DrawingViews)
            {
                if (ReadKey(() => view.AttributeSets, warnings) == viewKey)
                    return view;
            }

            return null;
        }

        private static string? ReadKey(Func<AttributeSets> sets, List<string> warnings) =>
            ComSafe.Get(() =>
            {
                AttributeSets all = sets();
                if (!all.NameIsUsed[HoleTableKeys.AttributeSetName])
                    return null;
                AttributeSet set = all[HoleTableKeys.AttributeSetName];
                return set.NameIsUsed[HoleTableKeys.AttributeName] ? set[HoleTableKeys.AttributeName].Value as string : null;
            }, warnings, "Hole table key");

        // Reads each object's key and collects those that need a new one: none yet, or one already given out in this
        // listing (a copy carries its original's key). Returns the keys in order, null where a key is still to be written.
        private static List<string?> ReadKeys(
            IReadOnlyList<Func<AttributeSets>> sets, List<string> warnings, out List<int> needKey)
        {
            var keys = new List<string?>();
            needKey = new List<int>();
            var used = new HashSet<string>();
            for (int i = 0; i < sets.Count; i++)
            {
                string? key = ReadKey(sets[i], warnings);
                if (key != null && used.Add(key))
                    keys.Add(key);
                else
                {
                    keys.Add(null);
                    needKey.Add(i);
                }
            }

            return keys;
        }

        // Writes the missing keys, in one transaction, and only when there are any (so a listing that changes nothing
        // adds no undo entry and does not mark the drawing modified). A write that fails inside ComSafe is logged and
        // that object gets no key; the writes made before it stay, and the transaction still commits them.
        private void WriteKeys(
            IReadOnlyList<Func<AttributeSets>> sets, List<string?> keys, List<int> needKey, List<string> warnings, string context)
        {
            if (needKey.Count == 0)
                return;

            WithKeyTransaction(() =>
            {
                foreach (int i in needKey)
                {
                    string newKey = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
                    bool written = ComSafe.Get(() => { WriteKey(sets[i](), newKey); return true; }, warnings, context);
                    if (written)
                        keys[i] = newKey;
                }
            });
        }

        private static void WriteKey(AttributeSets all, string key)
        {
            if (all.NameIsUsed[HoleTableKeys.AttributeSetName])
                all[HoleTableKeys.AttributeSetName].Delete();
            AttributeSet set;
            try
            {
                set = all.Add(HoleTableKeys.AttributeSetName, true);
            }
            catch (Exception)
            {
                // Spike 160 C10: some objects refuse the parent-removed flag; try without it.
                set = all.Add(HoleTableKeys.AttributeSetName, false);
            }

            set.Add(HoleTableKeys.AttributeName, ValueTypeEnum.kStringType, key);
        }

        // Writing keys changes the drawing; one transaction keeps it to one undo step.
        private void WithKeyTransaction(Action action)
        {
            Transaction? transaction = null;
            try
            {
                transaction = InventorHost.App.TransactionManager.StartTransaction((_Document)_drawing, HoleTableKeys.TransactionName);
            }
            catch (Exception ex)
            {
                _log.Warn(LogPrefix + "could not start a transaction for the view keys: " + ex.Message);
            }

            try
            {
                action();
                transaction?.End();
            }
            catch
            {
                try { transaction?.Abort(); } catch (Exception) { }
                throw;
            }
        }

        private void Report(List<string> warnings)
        {
            foreach (string warning in warnings)
                _log.Warn(LogPrefix + warning);
        }
    }
}
