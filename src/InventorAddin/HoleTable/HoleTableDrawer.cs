using System;
using System.Collections.Generic;
using System.Linq;
using Inventor;
using InventorAddin.Core.HoleTable;
using InventorAddin.Core.Logging;

namespace InventorAddin.HoleTables
{
    /// <summary>What a drawer call did: its plain-words message, and whether it changed the drawing (an unchanged call aborts its transaction).</summary>
    internal sealed record HoleTableDrawResult(string Message, bool Changed)
    {
        public static HoleTableDrawResult Done(string message) => new(message, true);

        public static HoleTableDrawResult Unchanged(string message) => new(message, false);
    }

    /// <summary>
    /// What the add-in reads from the drawing's hole table style (task 164): the three text styles, the line weights
    /// (cm), the layer and the metrics Core's layout needs.
    /// </summary>
    internal sealed class HoleTableStyleSet
    {
        public HoleTableStyleSet(
            TextStyle data, TextStyle header, TextStyle title, double insideWeightCm, double outsideWeightCm, Layer? layer, HoleTableMetrics metrics)
        {
            Data = data;
            Header = header;
            Title = title;
            InsideWeightCm = insideWeightCm;
            OutsideWeightCm = outsideWeightCm;
            Layer = layer;
            Metrics = metrics;
        }

        public TextStyle Data { get; }
        public TextStyle Header { get; }
        public TextStyle Title { get; }
        public double InsideWeightCm { get; }
        public double OutsideWeightCm { get; }
        public Layer? Layer { get; }
        public HoleTableMetrics Metrics { get; }

        public TextStyle For(HoleTableTextRole role) => role switch
        {
            HoleTableTextRole.Title => Title,
            HoleTableTextRole.Header => Header,
            _ => Data,
        };
    }

    /// <summary>
    /// Measures formatted text the way a fitted general note renders it: adds the note, reads its range box width and
    /// deletes it, even on failure. Cached by text style and text.
    /// </summary>
    internal sealed class HoleTableTextMeasure : IHoleTableTextMeasure
    {
        private readonly Sheet _sheet;
        private readonly TransientGeometry _geometry;
        private readonly HoleTableStyleSet _styles;
        private readonly ILog _log;
        private readonly Dictionary<(string Style, string Text), double> _cache = new();

        public HoleTableTextMeasure(Sheet sheet, TransientGeometry geometry, HoleTableStyleSet styles, ILog log)
        {
            _sheet = sheet;
            _geometry = geometry;
            _styles = styles;
            _log = log;
        }

        public double WidthCm(HoleTableTextRole role, string formattedText)
        {
            if (string.IsNullOrEmpty(formattedText))
                return 0;

            TextStyle style = _styles.For(role);
            var key = (ComSafe.Get(() => style.Name) ?? "", formattedText);
            if (_cache.TryGetValue(key, out double cached))
                return cached;

            GeneralNote? note = null;
            try
            {
                note = _sheet.DrawingNotes.GeneralNotes.AddFitted(_geometry.CreatePoint2d(1, 1), formattedText, style);
                Box2d box = note.RangeBox;
                double width = box.MaxPoint.X - box.MinPoint.X;
                _cache[key] = width;
                return width;
            }
            finally
            {
                if (note != null)
                {
                    try { note.Delete(); }
                    catch (Exception ex) { _log.Warn("Hole table: a measuring note could not be deleted: " + ex.Message); }
                }
            }
        }
    }

    /// <summary>
    /// Draws each hole table as one sketched symbol on the sheet and stacks them on the title block corner (task 164),
    /// as spike 160 round 6b proved: a definition per table holding grid lines and text boxes, one symbol per
    /// definition. The caller holds the transaction.
    /// </summary>
    internal sealed class HoleTableDrawer
    {
        private const string LogPrefix = "Hole table: ";
        private static readonly HoleTableKind[] Kinds = { HoleTableKind.Precision, HoleTableKind.Clearance };

        private readonly DrawingDocument _drawing;
        private readonly ILog _log;
        private readonly TransientGeometry _geometry;

        public HoleTableDrawer(DrawingDocument drawing, ILog log)
        {
            _drawing = drawing ?? throw new ArgumentNullException(nameof(drawing));
            _log = log ?? NullLog.Instance;
            _geometry = InventorHost.App.TransientGeometry;
        }

        /// <summary>
        /// Draws (or redraws) the tables of <paramref name="kinds"/> from the plan. A new table's symbol goes where Align
        /// would put it; with <paramref name="align"/> every table of the view is also moved there (Create Tables).
        /// </summary>
        public HoleTableDrawResult Draw(
            Sheet sheet, DrawingView view, string sheetKey, string viewKey, string sheetName, string viewName,
            HoleTablePlan plan, IReadOnlyList<HoleTableKind> kinds, bool align)
        {
            SheetPoint? anchor = ReadAnchor(sheet);
            if (anchor == null)
                return HoleTableDrawResult.Unchanged(HoleTableMessages.NoTitleBlockOrBorder);

            HoleTableStyleSet? styles = ReadStyles(sheet, view);
            if (styles == null)
                return HoleTableDrawResult.Unchanged(HoleTableMessages.StyleUnreadable);

            var measure = new HoleTableTextMeasure(sheet, _geometry, styles, _log);
            IReadOnlyList<HoleTableDrawing> drawings = HoleTableLayout.For(plan, measure, styles.Metrics, viewName);

            var weightWarnings = new List<string>();
            var drawn = new List<HoleTableKind>();
            var heights = new Dictionary<HoleTableKind, double?>();
            foreach (HoleTableKind kind in Kinds)
                heights[kind] = null;

            // Heights of tables already on the sheet that this call does not redraw.
            foreach (HoleTableKind kind in Kinds)
            {
                if (kinds.Contains(kind))
                    continue;
                string name = HoleTableNames.Definition(sheetKey, viewKey, kind);
                if (FindSymbol(sheet, name) != null && FindDefinition(name) is { } existing)
                    heights[kind] = ReadHeight(existing);
            }

            foreach (HoleTableKind kind in Kinds.Where(kinds.Contains))
            {
                HoleTableDrawing? drawing = drawings.FirstOrDefault(d => d.Table == kind);
                if (drawing == null)
                    continue;
                DrawDefinition(HoleTableNames.Definition(sheetKey, viewKey, kind), drawing, styles, weightWarnings, sheetName, viewName);
                heights[kind] = drawing.HeightCm;
                drawn.Add(kind);
            }

            foreach (string w in weightWarnings.Distinct())
                _log.Warn(LogPrefix + w);

            HoleTableAlignment positions = HoleTableAlign.Positions(
                anchor.Value.XCm, anchor.Value.YCm, heights[HoleTableKind.Clearance], heights[HoleTableKind.Precision]);
            foreach (HoleTableKind kind in drawn)
            {
                SheetPoint? at = kind == HoleTableKind.Precision ? positions.Precision : positions.Clearance;
                if (at != null)
                    PlaceSymbol(sheet, HoleTableNames.Definition(sheetKey, viewKey, kind), at.Value, styles.Layer, moveExisting: align);
            }

            if (align)
                MoveExisting(sheet, sheetKey, viewKey, positions, drawn);

            return drawn.Count == 0 ? HoleTableDrawResult.Unchanged(HoleTableMessages.Drawn(drawn, viewName)) : HoleTableDrawResult.Done(HoleTableMessages.Drawn(drawn, viewName));
        }

        /// <summary>Moves the view's placed tables to the stack on the title block corner; returns the plain-words result.</summary>
        public HoleTableDrawResult Align(Sheet sheet, string sheetKey, string viewKey)
        {
            SheetPoint? anchor = ReadAnchor(sheet);
            if (anchor == null)
                return HoleTableDrawResult.Unchanged(HoleTableMessages.NoTitleBlockOrBorder);

            var found = new Dictionary<HoleTableKind, SketchedSymbol>();
            var heights = new Dictionary<HoleTableKind, double?>();
            foreach (HoleTableKind kind in Kinds)
            {
                string name = HoleTableNames.Definition(sheetKey, viewKey, kind);
                SketchedSymbol? symbol = FindSymbol(sheet, name);
                if (symbol == null)
                    continue;
                SketchedSymbolDefinition? def = FindDefinition(name);
                double? height = def == null ? null : ReadHeight(def);
                if (height == null)
                    return HoleTableDrawResult.Unchanged(HoleTableMessages.HeightUnreadable(kind));
                found[kind] = symbol;
                heights[kind] = height;
            }

            if (found.Count == 0)
                return HoleTableDrawResult.Unchanged(HoleTableMessages.NoTablesToAlign);

            HoleTableAlignment positions = HoleTableAlign.Positions(
                anchor.Value.XCm, anchor.Value.YCm,
                heights.TryGetValue(HoleTableKind.Clearance, out double? c) ? c : null,
                heights.TryGetValue(HoleTableKind.Precision, out double? p) ? p : null);
            foreach (var pair in found)
            {
                SheetPoint? at = pair.Key == HoleTableKind.Precision ? positions.Precision : positions.Clearance;
                if (at != null)
                    pair.Value.Position = _geometry.CreatePoint2d(at.Value.XCm, at.Value.YCm);
            }

            return HoleTableDrawResult.Done(HoleTableMessages.Aligned(found.Count));
        }

        // ---- Anchor

        // The title block's max corner, or the border's bottom-right; null with neither.
        private static SheetPoint? ReadAnchor(Sheet sheet)
        {
            SheetBox? title = ComSafe.Get(() =>
            {
                Box2d b = sheet.TitleBlock.RangeBox;
                return (SheetBox?)new SheetBox(b.MinPoint.X, b.MinPoint.Y, b.MaxPoint.X, b.MaxPoint.Y);
            });
            SheetBox? border = ComSafe.Get(() =>
            {
                Box2d b = sheet.Border.RangeBox;
                return (SheetBox?)new SheetBox(b.MinPoint.X, b.MinPoint.Y, b.MaxPoint.X, b.MaxPoint.Y);
            });
            return HoleTableAlign.Anchor(title, border);
        }

        // ---- Styles

        // A temporary hole table on the view gives the text styles, row height, line weights and layer (spike 160, rounds
        // 3 to 6); it is deleted even on failure. The style object exposes the text styles and weights too, but not the
        // row height or the layer.
        private HoleTableStyleSet? ReadStyles(Sheet sheet, DrawingView view)
        {
            HoleTable? temp = null;
            try
            {
                temp = sheet.HoleTables.Add(view, _geometry.CreatePoint2d(2.0, 2.0));
                TextStyle data = temp.DataTextStyle;
                TextStyle header = temp.ColumnHeaderTextStyle;
                TextStyle title = temp.TitleTextStyle;
                double textHeight = data.FontSize;
                double rowHeight = temp.HoleTableRows[1].Height;
                double inside = temp.Style.InsideLineWeight;
                double outside = temp.Style.OutsideLineWeight;
                HoleTable table = temp;
                Layer? layer = ComSafe.Get(() => table.Layer);
                return new HoleTableStyleSet(data, header, title, inside, outside, layer, new HoleTableMetrics(textHeight, rowHeight));
            }
            catch (Exception ex)
            {
                _log.Warn(LogPrefix + "the hole table style could not be read: " + ex.Message);
                return null;
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

        // ---- Definitions

        private SketchedSymbolDefinition? FindDefinition(string name)
        {
            foreach (SketchedSymbolDefinition def in _drawing.SketchedSymbolDefinitions)
            {
                if (ComSafe.Get(() => def.Name) == name)
                    return def;
            }

            return null;
        }

        private void DrawDefinition(
            string name, HoleTableDrawing drawing, HoleTableStyleSet styles, List<string> warnings, string sheetName, string viewName)
        {
            SketchedSymbolDefinition? def = FindDefinition(name);
            bool isNew = def == null;
            def ??= _drawing.SketchedSymbolDefinitions.Add(name);

            DrawingSketch sketch;
            def.Edit(out sketch);
            try
            {
                if (isNew)
                {
                    // The insertion point (0, 0) is the table's bottom-right corner.
                    SketchPoint insertion = sketch.SketchPoints.Add(_geometry.CreatePoint2d(0, 0), false);
                    insertion.InsertionPoint = true;
                }
                else
                {
                    foreach (TextBox box in sketch.TextBoxes.Cast<TextBox>().ToList())
                        box.Delete();
                    foreach (SketchLine line in sketch.SketchLines.Cast<SketchLine>().ToList())
                        line.Delete();
                }

                foreach (HoleTableLine line in drawing.Lines)
                {
                    SketchLine made = sketch.SketchLines.AddByTwoPoints(
                        _geometry.CreatePoint2d(line.X1Cm, line.Y1Cm), _geometry.CreatePoint2d(line.X2Cm, line.Y2Cm));
                    double weight = line.Weight == HoleTableLineWeight.Outside ? styles.OutsideWeightCm : styles.InsideWeightCm;
                    ComSafe.Run(() => made.LineWeight = weight, warnings, "Line weight");
                }

                foreach (HoleTableText text in drawing.Texts)
                    AddText(sketch, text, styles.For(text.Role), warnings);

                def.ExitEdit(true);
            }
            catch
            {
                try { def.ExitEdit(false); } catch (Exception) { }
                throw;
            }

            SketchedSymbolDefinition written = def;
            ComSafe.Run(() => WriteHeight(written, drawing.HeightCm, sheetName, viewName), warnings, "Table height attribute");
        }

        // A text box centred on the asked point: justified to its centre, then shifted by what its range box says.
        private void AddText(DrawingSketch sketch, HoleTableText text, TextStyle style, List<string> warnings)
        {
            TextBox box = sketch.TextBoxes.AddFitted(_geometry.CreatePoint2d(text.CenterXCm, text.CenterYCm), text.FormattedText, style);
            box.HorizontalJustification = HorizontalTextAlignmentEnum.kAlignTextCenter;
            box.VerticalJustification = VerticalTextAlignmentEnum.kAlignTextMiddle;
            if (text.HasStack)
                ComSafe.Run(() => box.StackedTextPosition = VerticalTextAlignmentEnum.kAlignTextMiddle, warnings, "Stacked text position");

            Box2d range = box.RangeBox;
            Point2d origin = box.Origin;
            box.Origin = _geometry.CreatePoint2d(
                origin.X + text.CenterXCm - (range.MinPoint.X + range.MaxPoint.X) / 2,
                origin.Y + text.CenterYCm - (range.MinPoint.Y + range.MaxPoint.Y) / 2);
        }

        // The drawn height, kept on the definition: a sketched symbol has no range box, and Align needs it.
        private static void WriteHeight(SketchedSymbolDefinition def, double heightCm, string sheetName, string viewName)
        {
            AttributeSets sets = def.AttributeSets;
            if (sets.NameIsUsed[HoleTableNames.DrawingAttributeSetName])
                sets[HoleTableNames.DrawingAttributeSetName].Delete();
            AttributeSet set = sets.Add(HoleTableNames.DrawingAttributeSetName, false);
            set.Add(HoleTableNames.HeightAttributeName, ValueTypeEnum.kDoubleType, heightCm);
            set.Add(HoleTableNames.SheetNameAttributeName, ValueTypeEnum.kStringType, sheetName);
            set.Add(HoleTableNames.ViewNameAttributeName, ValueTypeEnum.kStringType, viewName);
        }

        // The attribute first; failing that, the highest point of the definition's lines (the bottom is y = 0).
        private static double? ReadHeight(SketchedSymbolDefinition def)
        {
            double? fromAttribute = ComSafe.Get(() =>
            {
                AttributeSets sets = def.AttributeSets;
                if (!sets.NameIsUsed[HoleTableNames.DrawingAttributeSetName])
                    return (double?)null;
                AttributeSet set = sets[HoleTableNames.DrawingAttributeSetName];
                return set.NameIsUsed[HoleTableNames.HeightAttributeName] ? set[HoleTableNames.HeightAttributeName].Value as double? : null;
            });
            if (fromAttribute is > 0)
                return fromAttribute;

            return ComSafe.Get(() =>
            {
                double top = 0;
                foreach (SketchLine line in def.Sketch.SketchLines)
                    top = Math.Max(top, Math.Max(line.StartSketchPoint.Geometry.Y, line.EndSketchPoint.Geometry.Y));
                return top > 0 ? (double?)top : null;
            });
        }

        // ---- Symbols

        private static SketchedSymbol? FindSymbol(Sheet sheet, string definitionName)
        {
            foreach (SketchedSymbol symbol in sheet.SketchedSymbols)
            {
                if (ComSafe.Get(() => symbol.Definition.Name) == definitionName)
                    return symbol;
            }

            return null;
        }

        private void PlaceSymbol(Sheet sheet, string definitionName, SheetPoint at, Layer? layer, bool moveExisting)
        {
            Point2d point = _geometry.CreatePoint2d(at.XCm, at.YCm);
            SketchedSymbol? symbol = FindSymbol(sheet, definitionName);
            if (symbol != null)
            {
                if (moveExisting)
                    symbol.Position = point;
                return;
            }

            SketchedSymbolDefinition def = FindDefinition(definitionName)
                ?? throw new InvalidOperationException("The table's definition was not found after drawing it.");
            SketchedSymbol placed = sheet.SketchedSymbols.Add(def, point, 0, 1);
            if (layer != null)
                ComSafe.Run(() => placed.Layer = layer);
        }

        // Create Tables aligns once: the view's tables that were not redrawn this time also go to the stack.
        private void MoveExisting(Sheet sheet, string sheetKey, string viewKey, HoleTableAlignment positions, List<HoleTableKind> drawn)
        {
            foreach (HoleTableKind kind in Kinds.Where(k => !drawn.Contains(k)))
            {
                SketchedSymbol? symbol = FindSymbol(sheet, HoleTableNames.Definition(sheetKey, viewKey, kind));
                SheetPoint? at = kind == HoleTableKind.Precision ? positions.Precision : positions.Clearance;
                if (symbol != null && at != null)
                    symbol.Position = _geometry.CreatePoint2d(at.Value.XCm, at.Value.YCm);
            }
        }
    }
}
