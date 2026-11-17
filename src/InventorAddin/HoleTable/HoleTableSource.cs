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
    /// survives a rename or a reorder. Drawing tables is task 164 (HoleTableDrawer); Update and the stored state are task 166, which still report that
    /// they are not yet available.
    /// </summary>
    public sealed class HoleTableSource : IHoleTableSource
    {
        private const string LogPrefix = "Hole table: ";
        private const string NotYetAvailable = "Not yet available.";

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

        public IReadOnlyList<HoleTableSheetInfo> GetSheets()
        {
            var warnings = new List<string>();
            var list = new List<HoleTableSheetInfo>();
            Sheet? active = ComSafe.Get(() => _drawing.ActiveSheet, warnings, "Active sheet");
            var sheets = new List<Sheet>();
            foreach (Sheet sheet in _drawing.Sheets)
                sheets.Add(sheet);

            var sets = sheets.Select(s => (Func<AttributeSets>)(() => s.AttributeSets)).ToList();
            List<string?> keys = ReadKeys(sets, warnings, out List<int> needKey);
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

        public IReadOnlyList<HoleTableViewInfo> GetViews(string sheetKey)
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
            DrawTables(HoleTableNames.GenerateTransactionName(kind), sheetKey, viewKey, plan, new[] { kind }, align: false);

        public string CreateTables(string sheetKey, string viewKey, HoleTablePlan plan, HoleTableState state) =>
            DrawTables(
                HoleTableNames.CreateTransactionName, sheetKey, viewKey, plan,
                new[] { HoleTableKind.Precision, HoleTableKind.Clearance }, align: true);

        // Task 166: Update and the stored state.
        public string UpdateTables(string sheetKey, string viewKey, HoleTablePlan plan, HoleTableState state) =>
            NotYetAvailable;

        public HoleTableState? ReadState(string sheetKey, string viewKey) => null;

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
            string transactionName, string sheetKey, string viewKey, HoleTablePlan plan, IReadOnlyList<HoleTableKind> kinds, bool align) =>
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
                    string marks = MarkHoles(sheet, view, sheetKey, viewKey, plan, kinds);
                    return HoleTableDrawResult.Done(string.IsNullOrEmpty(marks) ? drawn.Message : drawn.Message + " " + marks);
                }
                finally
                {
                    Report(warnings);
                }
            });

        // Puts the tags, precision marks and centre marks on the view for the holes of the tables just drawn. The view is
        // analysed again first: the COM objects kept by the last Analyze can be stale by now. The caller holds the transaction.
        private string MarkHoles(Sheet sheet, DrawingView view, string sheetKey, string viewKey, HoleTablePlan plan, IReadOnlyList<HoleTableKind> kinds)
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
            return HoleTableMarks.Summary(result.Marked, result.Skipped + missing);
        }

        // One transaction around a table action. If none can be started the action does not run, because a failure
        // half way could not be undone. A failure aborts the transaction and is rethrown for the window's status line.
        // A call that changed nothing (no title block, view not found, style unreadable) aborts too, so no empty Undo entry is left.

        private string InTableTransaction(string name, Func<HoleTableDrawResult> action)
        {
            Transaction transaction = InventorHost.App.TransactionManager.StartTransaction((_Document)_drawing, name);
            try
            {
                HoleTableDrawResult result = action();
                if (result.Changed)
                    transaction.End();
                else
                    transaction.Abort();
                return result.Message;
            }
            catch (Exception ex)
            {
                _log.Warn(LogPrefix + $"'{name}' failed and was undone: {ex.Message}");
                try { transaction.Abort(); } catch (Exception) { }
                throw;
            }
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

            var origins = new List<SheetPoint>();
            bool hasOrigin = ComSafe.Get(() => view.HasOriginIndicator, warnings, "View origin");
            if (hasOrigin)
            {
                Point2d? p = ComSafe.Get(() => view.OriginIndicator.Intent.PointOnSheet, warnings, "View origin point");
                if (p != null)
                    origins.Add(new SheetPoint(p.X, p.Y));
            }

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
            return new HoleTableViewAnalysis(holes.AsReadOnly(), origins.AsReadOnly(), scale, drawingUnits, leftOut);
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

            IReadOnlyList<HoleTableHole> built = HoleTableHoles.From(name, facts, awbSpec, units, centres, taperRow, tolerance);
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
