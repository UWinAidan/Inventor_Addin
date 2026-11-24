using System;
using System.Collections.Generic;
using System.Linq;
using Inventor;
using InventorAddin.Core.HoleTable;
using InventorAddin.Core.Logging;

namespace InventorAddin.HoleTables
{
    /// <summary>One hole to mark on the view: its id and tag, whether it gets the precision mark, its centre on the sheet and what the add-in kept of it.</summary>
    internal sealed record HoleMarkTarget(string HoleId, string Tag, bool PrecisionMark, SheetPoint Centre, HoleTableHoleRef Ref);

    /// <summary>One hole whose tag's leader Update sets: its id, its tag text, its centre on the sheet and what the add-in kept of it.</summary>
    internal sealed record HoleLeaderTarget(string HoleId, string Tag, SheetPoint Centre, HoleTableHoleRef Ref);

    /// <summary>What a marker call did: holes marked, and holes left alone because they could not be marked.</summary>
    internal sealed record HoleMarkResult(int Marked, int Skipped);

    /// <summary>
    /// Puts each hole's tag, its precision mark (precision holes) and the one AWB centre mark on the view, as spike 160
    /// round 7 proved (task 165). Tags and marks are sketched symbols placed with a leader whose lines are hidden, so
    /// they follow the view and the model; the centre mark is a drawing centre mark on the hole's outermost circle.
    /// Each object carries an attribute set (<see cref="HoleTableMarks.AttributeSetName"/>) with the hole's id, so
    /// the marks of a hole drawn again are replaced, and Update (task 166) can find them. The caller holds the transaction.
    /// </summary>
    internal sealed class HoleTableMarker
    {
        private const string LogPrefix = "Hole table: ";

        private readonly DrawingDocument _drawing;
        private readonly ILog _log;
        private readonly TransientGeometry _geometry;

        public HoleTableMarker(DrawingDocument drawing, ILog log)
        {
            _drawing = drawing ?? throw new ArgumentNullException(nameof(drawing));
            _log = log ?? NullLog.Instance;
            _geometry = InventorHost.App.TransientGeometry;
        }

        /// <summary>
        /// Marks the holes. A hole that cannot be marked is counted and logged and the rest go on; a failure in the
        /// shared parts (the style, the symbol definitions) throws, so the caller's transaction is aborted.
        /// </summary>
        public HoleMarkResult Place(Sheet sheet, DrawingView view, string viewKey, IReadOnlyList<HoleMarkTarget> targets)
        {
            if (targets.Count == 0)
                return new HoleMarkResult(0, 0);

            CentermarkStyle centreStyle = EnsureCentreMarkStyle();
            TextStyle tagText = ReadTagTextStyle(sheet, view);
            SketchedSymbolDefinition tagDef = FindDefinition(HoleTableMarks.TagDefinitionName) ?? MakeTagDefinition(tagText);
            SketchedSymbolDefinition? markDef = targets.Any(t => t.PrecisionMark)
                ? FindDefinition(HoleTableMarks.PrecisionMarkDefinitionName) ?? MakePrecisionMarkDefinition()
                : null;
            double tagHeight = tagText.FontSize;
            var widths = new Dictionary<string, double>();

            DeleteExisting(sheet, viewKey, targets.Select(t => t.HoleId).ToHashSet(StringComparer.Ordinal));

            int marked = 0, skipped = 0;
            foreach (HoleMarkTarget target in targets)
            {
                try
                {
                    if (PlaceOne(sheet, viewKey, target, tagDef, markDef, centreStyle, tagText, tagHeight, widths))
                        marked++;
                    else
                        skipped++;
                }
                catch (Exception ex)
                {
                    skipped++;
                    _log.Warn(LogPrefix + $"hole '{target.HoleId}' could not be marked: {ex.Message}");
                }
            }

            return new HoleMarkResult(marked, skipped);
        }

        private bool PlaceOne(
            Sheet sheet, string viewKey, HoleMarkTarget target, SketchedSymbolDefinition tagDef, SketchedSymbolDefinition? markDef,
            CentermarkStyle centreStyle, TextStyle tagText, double tagHeight, Dictionary<string, double> widths)
        {
            // The circles at the centre: radius and thread flag for Core's rules (the largest is the outermost, the
            // smallest non-thread one is the hole).
            var circles = new List<HoleCircle>();
            var curves = new List<DrawingCurve>();
            foreach (DrawingCurve curve in target.Ref.Curves)
            {
                double? radius = ReadRadius(curve);
                if (radius == null)
                    continue;
                bool thread = ComSafe.Get(() => curve.EdgeType == DrawingEdgeTypeEnum.kThreadEdge);
                circles.Add(new HoleCircle(radius.Value, thread));
                curves.Add(curve);
            }

            int outerIndex = HoleTableMarks.OutermostIndex(circles);
            int holeIndex = HoleTableMarks.HoleIndex(circles);
            if (outerIndex < 0 || holeIndex < 0)
            {
                _log.Warn(LogPrefix + $"hole '{target.HoleId}' has no circle at its centre in the view; no tag or marks.");
                return false;
            }

            DrawingCurve outer = curves[outerIndex];
            DrawingCurve holeCurve = curves[holeIndex];
            double holeRadius = circles[holeIndex].RadiusCm;
            GeometryIntent centreIntent = sheet.CreateGeometryIntent(holeCurve, PointIntentEnum.kCenterPointIntent);

            // Precision mark first, so the tag can stand clear of it.
            if (target.PrecisionMark && markDef != null)
            {
                SketchedSymbol mark = AddMark(sheet, markDef, centreIntent, target.Centre, holeRadius);
                StampOrDelete(() => mark.AttributeSets, mark.Delete, target.HoleId, viewKey, HoleTableMarks.PrecisionMarkRole);
            }

            double clear = HoleTableMarks.ClearRadiusCm(circles[outerIndex].RadiusCm, target.PrecisionMark, holeRadius);
            double width = TextWidth(sheet, tagText, target.Tag, widths);
            SheetPoint at = HoleTableMarks.TagPoint(target.Centre, clear, tagHeight, width);
            ObjectCollection points = InventorHost.App.TransientObjects.CreateObjectCollection();
            points.Add(_geometry.CreatePoint2d(at.XCm, at.YCm));
            points.Add(centreIntent);
            SketchedSymbol tag = sheet.SketchedSymbols.AddWithLeader(tagDef, points, 0, 1, new[] { target.Tag }, false, false);
            tag.LeaderVisible = false;
            StampOrDelete(() => tag.AttributeSets, tag.Delete, target.HoleId, viewKey, HoleTableMarks.TagRole);

            // Every hole, precision ones included, gets the one centre mark on its outermost visible circle.
            Centermark centre = sheet.Centermarks.Add(sheet.CreateGeometryIntent(outer), true, false, centreStyle);
            StampOrDelete(() => centre.AttributeSets, centre.Delete, target.HoleId, viewKey, HoleTableMarks.CentreMarkRole);
            return true;
        }

        // The mark's circle is the hole's own edge: scale the radius-1 symbol by the hole radius. Spike 160 round 7 tried
        // the centre intent alone and fell back to two points (the centre, then the intent) if that was refused.
        private SketchedSymbol AddMark(Sheet sheet, SketchedSymbolDefinition def, GeometryIntent centreIntent, SheetPoint centre, double scale)
        {
            ObjectCollection one = InventorHost.App.TransientObjects.CreateObjectCollection();
            one.Add(centreIntent);
            SketchedSymbol mark;
            try
            {
                mark = sheet.SketchedSymbols.AddWithLeader(def, one, 0, scale, System.Reflection.Missing.Value, false, false);
            }
            catch (Exception)
            {
                ObjectCollection two = InventorHost.App.TransientObjects.CreateObjectCollection();
                two.Add(_geometry.CreatePoint2d(centre.XCm, centre.YCm));
                two.Add(centreIntent);
                mark = sheet.SketchedSymbols.AddWithLeader(def, two, 0, scale, System.Reflection.Missing.Value, false, false);
            }

            mark.LeaderVisible = false;
            return mark;
        }

        // ---- Existing marks

        /// <summary>The roles of the marks the wizard placed on this view, by hole id (task 166: Update compares them with the plan).</summary>
        public IReadOnlyDictionary<string, IReadOnlySet<string>> ReadMarks(Sheet sheet, string viewKey)
        {
            var found = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            foreach (SketchedSymbol symbol in sheet.SketchedSymbols)
                Collect(() => symbol.AttributeSets, viewKey, found);
            foreach (Centermark centre in sheet.Centermarks)
                Collect(() => centre.AttributeSets, viewKey, found);
            return found.ToDictionary(p => p.Key, p => (IReadOnlySet<string>)p.Value, StringComparer.Ordinal);
        }

        /// <summary>
        /// Shows each tag's leader when the tag sits more than <see cref="HoleTableMarks.TagLeaderDistanceCm"/> from its hole
        /// and hides it otherwise (task 240). The tag's box is its position and the text's size, so a leader that is showing
        /// never counts as part of it. Moving a tag changes no stored state, so it never makes the tables out of date.
        /// Returns how many tags now show a leader. The caller holds the transaction.
        /// </summary>
        public int SetLeaders(Sheet sheet, DrawingView view, string viewKey, IReadOnlyList<HoleLeaderTarget> targets)
        {
            if (targets.Count == 0)
                return 0;
            var byId = targets.ToDictionary(t => t.HoleId, StringComparer.Ordinal);
            TextStyle tagText = ReadTagTextStyle(sheet, view);
            var widths = new Dictionary<string, double>();
            var radii = new Dictionary<string, double>(StringComparer.Ordinal);
            int shown = 0;
            foreach (SketchedSymbol symbol in sheet.SketchedSymbols)
            {
                (string Id, string Role)? mark = ReadMark(() => symbol.AttributeSets, viewKey);
                if (mark is not { } m || m.Role != HoleTableMarks.TagRole || !byId.TryGetValue(m.Id, out HoleLeaderTarget? target))
                    continue;
                try
                {
                    if (!radii.TryGetValue(m.Id, out double radius))
                    {
                        double? read = OutermostRadius(target.Ref);
                        if (read == null)
                        {
                            _log.Warn(LogPrefix + $"hole '{m.Id}' has no circle in the view; its tag's leader is left as it is.");
                            continue;
                        }

                        radii[m.Id] = radius = read.Value;
                    }

                    // The tag is centred on the symbol's position (spike 160 round 7); its size is the text's, as at placement.
                    Point2d at = symbol.Position;
                    double halfWidth = TextWidth(sheet, tagText, target.Tag, widths) / 2;
                    double halfHeight = tagText.FontSize / 2;
                    bool visible = HoleTableMarks.TagLeaderVisible(
                        new SheetPoint(at.X - halfWidth, at.Y - halfHeight), new SheetPoint(at.X + halfWidth, at.Y + halfHeight), target.Centre, radius);
                    symbol.LeaderVisible = visible;
                    if (visible)
                        shown++;
                }
                catch (Exception ex)
                {
                    _log.Warn(LogPrefix + $"the leader of hole '{m.Id}' could not be set: {ex.Message}");
                }
            }

            return shown;
        }

        private static double? OutermostRadius(HoleTableHoleRef hole)
        {
            var circles = new List<HoleCircle>();
            foreach (DrawingCurve curve in hole.Curves)
            {
                double? radius = ReadRadius(curve);
                if (radius == null)
                    continue;
                circles.Add(new HoleCircle(radius.Value, ComSafe.Get(() => curve.EdgeType == DrawingEdgeTypeEnum.kThreadEdge)));
            }

            int index = HoleTableMarks.OutermostIndex(circles);
            return index < 0 ? null : circles[index].RadiusCm;
        }

        private static (string Id, string Role)? ReadMark(Func<AttributeSets> sets, string viewKey) =>
            ComSafe.Get(() =>
            {
                AttributeSets all = sets();
                if (!all.NameIsUsed[HoleTableMarks.AttributeSetName])
                    return ((string, string)?)null;
                AttributeSet set = all[HoleTableMarks.AttributeSetName];
                string? view = set.NameIsUsed[HoleTableMarks.ViewKeyAttributeName] ? set[HoleTableMarks.ViewKeyAttributeName].Value as string : null;
                string? id = set.NameIsUsed[HoleTableMarks.HoleIdAttributeName] ? set[HoleTableMarks.HoleIdAttributeName].Value as string : null;
                string? role = set.NameIsUsed[HoleTableMarks.RoleAttributeName] ? set[HoleTableMarks.RoleAttributeName].Value as string : null;
                return view == viewKey && id != null && role != null ? (id, role) : ((string, string)?)null;
            });

        /// <summary>Deletes every tag, precision mark and centre mark of the given holes on this view (Update, for holes that are gone).</summary>
        public void Remove(Sheet sheet, string viewKey, IReadOnlyCollection<string> holeIds)
        {
            if (holeIds.Count > 0)
                DeleteExisting(sheet, viewKey, holeIds.ToHashSet(StringComparer.Ordinal));
        }

        private static void Collect(Func<AttributeSets> sets, string viewKey, Dictionary<string, HashSet<string>> found)
        {
            (string Id, string Role)? mark = ComSafe.Get(() =>
            {
                AttributeSets all = sets();
                if (!all.NameIsUsed[HoleTableMarks.AttributeSetName])
                    return ((string, string)?)null;
                AttributeSet set = all[HoleTableMarks.AttributeSetName];
                string? view = set.NameIsUsed[HoleTableMarks.ViewKeyAttributeName] ? set[HoleTableMarks.ViewKeyAttributeName].Value as string : null;
                string? id = set.NameIsUsed[HoleTableMarks.HoleIdAttributeName] ? set[HoleTableMarks.HoleIdAttributeName].Value as string : null;
                string? role = set.NameIsUsed[HoleTableMarks.RoleAttributeName] ? set[HoleTableMarks.RoleAttributeName].Value as string : null;
                return view == viewKey && id != null && role != null ? (id, role) : ((string, string)?)null;
            });
            if (mark is not { } m)
                return;
            if (!found.TryGetValue(m.Id, out HashSet<string>? roles))
                found[m.Id] = roles = new HashSet<string>(StringComparer.Ordinal);
            roles.Add(m.Role);
        }

        // Removes the tags, precision marks and centre marks the wizard placed earlier for these holes on this view, so a
        // table drawn again does not stack a second set on the first.
        private void DeleteExisting(Sheet sheet, string viewKey, HashSet<string> holeIds)
        {
            var symbols = new List<SketchedSymbol>();
            foreach (SketchedSymbol symbol in sheet.SketchedSymbols)
            {
                if (IsOurs(() => symbol.AttributeSets, viewKey, holeIds))
                    symbols.Add(symbol);
            }

            var centres = new List<Centermark>();
            foreach (Centermark centre in sheet.Centermarks)
            {
                if (IsOurs(() => centre.AttributeSets, viewKey, holeIds))
                    centres.Add(centre);
            }

            foreach (SketchedSymbol symbol in symbols)
                symbol.Delete();
            foreach (Centermark centre in centres)
                centre.Delete();
        }

        private static bool IsOurs(Func<AttributeSets> sets, string viewKey, HashSet<string> holeIds) =>
            ComSafe.Get(() =>
            {
                AttributeSets all = sets();
                if (!all.NameIsUsed[HoleTableMarks.AttributeSetName])
                    return false;
                AttributeSet set = all[HoleTableMarks.AttributeSetName];
                string? view = set.NameIsUsed[HoleTableMarks.ViewKeyAttributeName] ? set[HoleTableMarks.ViewKeyAttributeName].Value as string : null;
                string? id = set.NameIsUsed[HoleTableMarks.HoleIdAttributeName] ? set[HoleTableMarks.HoleIdAttributeName].Value as string : null;
                return view == viewKey && id != null && holeIds.Contains(id);
            });

        // A mark that cannot be stamped could not be found again by Update, which would then add a second one every time:
        // it is deleted and the failure rethrown, so the hole counts as could-not-be-marked.
        private void StampOrDelete(Func<AttributeSets> sets, Action delete, string holeId, string viewKey, string role)
        {
            try
            {
                Stamp(sets(), holeId, viewKey, role);
            }
            catch (Exception ex)
            {
                _log.Warn(LogPrefix + $"the {role} of hole '{holeId}' could not be stamped and was removed: {ex.Message}");
                try { delete(); }
                catch (Exception deleteEx) { _log.Warn(LogPrefix + $"the unstamped {role} could not be deleted: {deleteEx.Message}"); }
                throw;
            }
        }

        private static void Stamp(AttributeSets sets, string holeId, string viewKey, string role)
        {
            if (sets.NameIsUsed[HoleTableMarks.AttributeSetName])
                sets[HoleTableMarks.AttributeSetName].Delete();
            AttributeSet set = sets.Add(HoleTableMarks.AttributeSetName, false);
            set.Add(HoleTableMarks.HoleIdAttributeName, ValueTypeEnum.kStringType, holeId);
            set.Add(HoleTableMarks.ViewKeyAttributeName, ValueTypeEnum.kStringType, viewKey);
            set.Add(HoleTableMarks.RoleAttributeName, ValueTypeEnum.kStringType, role);
        }

        // ---- Circles

        // The circle's radius on the sheet, from its first segment's geometry (spike 160 round 7).
        private static double? ReadRadius(DrawingCurve curve) =>
            ComSafe.Get(() =>
            {
                object geometry = curve.Segments[1].Geometry;
                return geometry switch
                {
                    Circle2d circle => (double?)circle.Radius,
                    Arc2d arc => arc.Radius,
                    _ => null,
                };
            });

        // ---- Centre mark style

        // The one AWB centre mark style, kept in the drawing: a local copy of the standard's centre mark style when it
        // is missing. Its values are set every time, so a changed style is put right (spike 160 round 7).
        private CentermarkStyle EnsureCentreMarkStyle()
        {
            DrawingStylesManager styles = _drawing.StylesManager;
            CentermarkStyle? style = null;
            foreach (CentermarkStyle candidate in styles.CentermarkStyles)
            {
                if (ComSafe.Get(() => candidate.Name) == HoleTableMarks.CentreMarkStyleName)
                {
                    style = candidate;
                    break;
                }
            }

            if (style == null)
            {
                CentermarkStyle standard = styles.ActiveStandardStyle.ActiveObjectDefaults.CentermarkStyle;
                style = (CentermarkStyle)standard.Copy(HoleTableMarks.CentreMarkStyleName);
            }

            // A gap of 0 is refused; 0.0001 cm works and cannot be seen. Overshoot, not extension length, sets where the
            // lines end; both are set.
            style.GapDistance = HoleTableMarks.CentreMarkGapCm;
            style.MarkSize = HoleTableMarks.CentreMarkSizeCm;
            style.ExtensionLength = HoleTableMarks.CentreMarkExtensionLengthCm;
            style.OvershootDistance = HoleTableMarks.CentreMarkOvershootCm;
            return style;
        }

        // ---- Symbol definitions

        private SketchedSymbolDefinition? FindDefinition(string name)
        {
            foreach (SketchedSymbolDefinition def in _drawing.SketchedSymbolDefinitions)
            {
                if (ComSafe.Get(() => def.Name) == name)
                    return def;
            }

            return null;
        }

        // One prompted text in the hole tag text style, justified left and bottom at the origin.
        private SketchedSymbolDefinition MakeTagDefinition(TextStyle tagText)
        {
            SketchedSymbolDefinition def = _drawing.SketchedSymbolDefinitions.Add(HoleTableMarks.TagDefinitionName);
            DrawingSketch sketch;
            def.Edit(out sketch);
            try
            {
                TextBox box = sketch.TextBoxes.AddFitted(_geometry.CreatePoint2d(0, 0), HoleTableMarks.TagPromptText, tagText);
                box.HorizontalJustification = HorizontalTextAlignmentEnum.kAlignTextLeft;
                box.VerticalJustification = VerticalTextAlignmentEnum.kAlignTextLower;
                def.ExitEdit(true);
            }
            catch
            {
                try { def.ExitEdit(false); } catch (Exception) { }
                throw;
            }

            return def;
        }

        // A circle of radius 1 and its top right quadrant filled: two radii and an arc as a profile. No centre lines;
        // the hole's centre mark draws them.
        private SketchedSymbolDefinition MakePrecisionMarkDefinition()
        {
            SketchedSymbolDefinition def = _drawing.SketchedSymbolDefinitions.Add(HoleTableMarks.PrecisionMarkDefinitionName);
            DrawingSketch sketch;
            def.Edit(out sketch);
            try
            {
                Point2d origin = _geometry.CreatePoint2d(0, 0);
                sketch.SketchCircles.AddByCenterRadius(origin, 1.0);
                SketchLine along = sketch.SketchLines.AddByTwoPoints(origin, _geometry.CreatePoint2d(1, 0));
                SketchArc arc = sketch.SketchArcs.AddByCenterStartEndPoint(origin, along.EndSketchPoint, _geometry.CreatePoint2d(0, 1), true);
                SketchLine back = sketch.SketchLines.AddByTwoPoints(arc.EndSketchPoint, along.StartSketchPoint);
                ObjectCollection segments = InventorHost.App.TransientObjects.CreateObjectCollection();
                segments.Add(along);
                segments.Add(arc);
                segments.Add(back);
                Profile profile = sketch.Profiles.AddForSolid(false, segments);
                sketch.SketchFillRegions.Add(profile);
                def.ExitEdit(true);
            }
            catch
            {
                try { def.ExitEdit(false); } catch (Exception) { }
                throw;
            }

            return def;
        }

        // ---- Text

        // The hole tag's text style, read from a temporary hole table on the view (spike 160 round 7), deleted even on failure.
        private TextStyle ReadTagTextStyle(Sheet sheet, DrawingView view)
        {
            HoleTable? temp = null;
            try
            {
                temp = sheet.HoleTables.Add(view, _geometry.CreatePoint2d(2.0, 2.0));
                return temp.HoleTableRows[1].HoleTag.DimensionStyle.TextStyle;
            }
            finally
            {
                if (temp != null)
                {
                    try { temp.Delete(); }
                    catch (Exception ex) { _log.Warn(LogPrefix + "the temporary hole table could not be deleted: " + ex.Message); }
                }
            }
        }

        // The tag's width as a fitted general note renders it (deleted after, even on failure); cached by text.
        private double TextWidth(Sheet sheet, TextStyle style, string text, Dictionary<string, double> cache)
        {
            if (cache.TryGetValue(text, out double cached))
                return cached;

            GeneralNote? note = null;
            try
            {
                note = sheet.DrawingNotes.GeneralNotes.AddFitted(_geometry.CreatePoint2d(1, 1), text, style);
                Box2d box = note.RangeBox;
                double width = box.MaxPoint.X - box.MinPoint.X;
                cache[text] = width;
                return width;
            }
            finally
            {
                if (note != null)
                {
                    try { note.Delete(); }
                    catch (Exception ex) { _log.Warn(LogPrefix + "a measuring note could not be deleted: " + ex.Message); }
                }
            }
        }
    }
}
