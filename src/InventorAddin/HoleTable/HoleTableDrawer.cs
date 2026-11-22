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

    /// <summary>A table action that changed nothing (its transaction was aborted); its message is the reason, for the window's "Could not ..." line.</summary>
    internal sealed class HoleTableUnchangedException : InvalidOperationException
    {
        public HoleTableUnchangedException(string message) : base(message)
        {
        }
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
        /// Every table stays inside the border (task 220): a clearance table too tall for the space above the title block
        /// is cut into parts that stand side by side to the left, and when something still does not fit the call changes
        /// nothing and says so (the caller's transaction is aborted, so no tags, marks or state are left behind).
        /// An Update whose redrawn table would stick out of the border is placed again the same way.
        /// </summary>
        public HoleTableDrawResult Draw(
            Sheet sheet, DrawingView view, string sheetKey, string viewKey, string sheetName, string viewName,
            HoleTablePlan plan, IReadOnlyList<HoleTableKind> kinds, bool align, HoleTablePlacement? deletedClearance = null)
        {
            SheetPoint? anchorOrNull = ReadAnchor(sheet);
            if (anchorOrNull == null)
                return HoleTableDrawResult.Unchanged(HoleTableMessages.NoTitleBlockOrBorder);
            SheetPoint anchor = anchorOrNull.Value;

            SheetBox? border = ReadBorder(sheet);

            HoleTableStyleSet? styles = ReadStyles(sheet, view);
            if (styles == null)
                return HoleTableDrawResult.Unchanged(HoleTableMessages.StyleUnreadable);

            var measure = new HoleTableTextMeasure(sheet, _geometry, styles, _log);
            IReadOnlyList<HoleTableDrawing> drawings = HoleTableLayout.For(plan, measure, styles.Metrics, viewName);

            // Where the tables were before this call redraws anything (task 178). A clearance table that Update deleted
            // just before is passed in, since its symbol is gone.
            HoleTablePlacement? placedClearance = ReadPlacement(sheet, sheetKey, viewKey, HoleTableKind.Clearance);
            HoleTablePlacement? clearanceBefore = placedClearance ?? deletedClearance;
            SheetPoint? precisionBefore = ReadPlacement(sheet, sheetKey, viewKey, HoleTableKind.Precision)?.Position;

            bool clearanceDrawn = kinds.Contains(HoleTableKind.Clearance) && drawings.Any(d => d.Table == HoleTableKind.Clearance);
            bool precisionDrawn = kinds.Contains(HoleTableKind.Precision) && drawings.Any(d => d.Table == HoleTableKind.Precision);

            // The sizes the tables will have: the new drawings, or what is on the sheet for a table this call leaves alone.
            List<HoleTableSize> ClearanceSizes() => clearanceDrawn
                ? drawings.Where(d => d.Table == HoleTableKind.Clearance).OrderBy(d => d.Part).Select(d => new HoleTableSize(d.WidthCm, d.HeightCm)).ToList()
                : ReadSizes(FindParts(sheet, sheetKey, viewKey, HoleTableKind.Clearance)) ?? new List<HoleTableSize>();
            HoleTableSize? PrecisionSize()
            {
                HoleTableDrawing? d = drawings.FirstOrDefault(x => x.Table == HoleTableKind.Precision);
                if (precisionDrawn && d != null)
                    return new HoleTableSize(d.WidthCm, d.HeightCm);
                return ReadSizes(FindParts(sheet, sheetKey, viewKey, HoleTableKind.Precision)) is { Count: > 0 } list ? list[0] : null;
            }

            List<HoleTableSize> clearanceSizes = ClearanceSizes();
            HoleTableSize? precisionSize = PrecisionSize();

            // Create Tables places everything again. Update leaves a table where it is unless the redrawn clearance table
            // would stick out of the border there.
            bool refit = align;
            if (!refit && clearanceDrawn)
            {
                SheetPoint first = placedClearance?.Position ?? anchor;
                refit = HoleTableAlign.Fit(first, border, clearanceSizes, null).Warning != null;
            }

            HoleTableAlignment positions = HoleTableAlign.Fit(anchor, border, clearanceSizes, precisionSize);
            if (refit && positions.Clearance == null && clearanceSizes.Count > 0 && clearanceDrawn && border is { } b)
            {
                // Too tall for one column: continue the table in more columns to the left (task 220).
                IReadOnlyList<HoleTableDrawing> split = HoleTableLayout.For(plan, measure, styles.Metrics, viewName, b.MaxYCm - anchor.YCm);
                if (split.Count(d => d.Table == HoleTableKind.Clearance) > 1)
                {
                    drawings = split;
                    clearanceSizes = ClearanceSizes();
                    positions = HoleTableAlign.Fit(anchor, border, clearanceSizes, precisionSize);
                }
            }

            if (refit)
            {
                var misfits = new List<HoleTableKind>();
                if (clearanceDrawn && positions.Clearance == null)
                    misfits.Add(HoleTableKind.Clearance);
                if (precisionDrawn && positions.Precision == null)
                    misfits.Add(HoleTableKind.Precision);
                if (misfits.Count > 0)
                    return HoleTableDrawResult.Unchanged(HoleTableMessages.TablesDoNotFit(misfits));
            }

            var weightWarnings = new List<string>();
            var drawn = new List<HoleTableKind>();
            foreach (HoleTableKind kind in Kinds.Where(kinds.Contains))
            {
                List<HoleTableDrawing> parts = drawings.Where(d => d.Table == kind).OrderBy(d => d.Part).ToList();
                if (parts.Count == 0)
                    continue;
                foreach (HoleTableDrawing part in parts)
                    DrawDefinition(HoleTableNames.Definition(sheetKey, viewKey, kind, part.Part), part, styles, weightWarnings, sheetName, viewName);
                DeleteLeftoverParts(sheet, sheetKey, viewKey, kind, parts.Count);
                drawn.Add(kind);
            }

            foreach (string w in weightWarnings.Distinct())
                _log.Warn(LogPrefix + w);

            if (refit)
            {
                foreach (HoleTableKind kind in drawn)
                    PlaceKind(sheet, sheetKey, viewKey, kind, positions, styles.Layer, moveExisting: true);

                MoveExisting(sheet, sheetKey, viewKey, positions, drawn);
            }
            else
            {
                // A new clearance table goes on the anchor; one already placed stays where it is. Its later parts follow it.
                if (clearanceDrawn)
                {
                    SheetPoint first = placedClearance?.Position ?? anchor;
                    IReadOnlyList<SheetPoint> chain = HoleTableAlign.Fit(first, null, clearanceSizes, null).ClearanceParts ?? Array.Empty<SheetPoint>();
                    for (int i = 0; i < chain.Count; i++)
                        PlaceSymbol(sheet, HoleTableNames.Definition(sheetKey, viewKey, HoleTableKind.Clearance, i + 1), chain[i], styles.Layer, moveExisting: i > 0);
                }

                // The precision table follows the clearance table's top edge (or its left edge, when it stood beside it) if
                // it sat there; one the user moved stays.
                HoleTablePlacement? clearanceAfter = null;
                if (clearanceSizes.Count > 0
                    && ReadPlacement(sheet, sheetKey, viewKey, HoleTableKind.Clearance) is { } placed)
                {
                    clearanceAfter = new HoleTablePlacement(placed.Position, clearanceSizes[0].HeightCm, HoleTableAlign.BlockWidth(clearanceSizes));
                }

                string precisionName = HoleTableNames.Definition(sheetKey, viewKey, HoleTableKind.Precision);
                SketchedSymbol? precisionSymbol = FindSymbol(sheet, precisionName);
                if (precisionDrawn || precisionSymbol != null)
                {
                    SheetPoint? to = HoleTableAlign.PrecisionAfterRedraw(anchor, clearanceBefore, precisionBefore, clearanceAfter);

                    // Never outside the border (task 220): checked at the spot it will have, against its new size.
                    SheetPoint? current = precisionSymbol == null ? null : SymbolPosition(precisionSymbol);
                    SheetPoint? wanted = to ?? current;
                    SheetPoint? target = wanted is { } w && precisionSize is { } size
                        ? HoleTableAlign.KeepInside(border, w, size, positions.Precision)
                        : wanted ?? positions.Precision;
                    if (target is not { } place)
                        return HoleTableDrawResult.Unchanged(HoleTableMessages.TablesDoNotFit(new[] { HoleTableKind.Precision }));

                    if (precisionSymbol != null)
                        precisionSymbol.Position = _geometry.CreatePoint2d(place.XCm, place.YCm);
                    else
                        PlaceSymbol(sheet, precisionName, place, styles.Layer, moveExisting: false);
                }
            }

            string message = HoleTableMessages.Drawn(drawn, viewName);
            int clearanceParts = clearanceSizes.Count;
            if (clearanceDrawn && clearanceParts > 1)
                message += " " + HoleTableMessages.ClearanceContinued(clearanceParts);
            return drawn.Count == 0 ? HoleTableDrawResult.Unchanged(message) : HoleTableDrawResult.Done(message);
        }

        /// <summary>Moves the view's placed tables to the stack on the title block corner; returns the plain-words result.</summary>
        public HoleTableDrawResult Align(Sheet sheet, string sheetKey, string viewKey)
        {
            SheetPoint? anchor = ReadAnchor(sheet);
            if (anchor == null)
                return HoleTableDrawResult.Unchanged(HoleTableMessages.NoTitleBlockOrBorder);

            SheetBox? border = ReadBorder(sheet);
            var found = new Dictionary<HoleTableKind, List<PlacedPart>>();
            var sizes = new Dictionary<HoleTableKind, List<HoleTableSize>>();
            foreach (HoleTableKind kind in Kinds)
            {
                List<PlacedPart> parts = FindParts(sheet, sheetKey, viewKey, kind);
                if (parts.Count == 0)
                    continue;
                List<HoleTableSize>? partSizes = ReadSizes(parts);
                if (partSizes == null)
                    return HoleTableDrawResult.Unchanged(HoleTableMessages.HeightUnreadable(kind));
                found[kind] = parts;
                sizes[kind] = partSizes;
            }

            if (found.Count == 0)
                return HoleTableDrawResult.Unchanged(HoleTableMessages.NoTablesToAlign);

            List<HoleTableSize> clearanceSizes = sizes.TryGetValue(HoleTableKind.Clearance, out List<HoleTableSize>? c) ? c : new List<HoleTableSize>();
            HoleTableSize? precisionSize = sizes.TryGetValue(HoleTableKind.Precision, out List<HoleTableSize>? p) ? p[0] : null;
            HoleTableAlignment positions = HoleTableAlign.Fit(anchor.Value, border, clearanceSizes, precisionSize, moving: true);

            int moved = 0;
            if (positions.ClearanceParts is { Count: > 0 } chain && found.TryGetValue(HoleTableKind.Clearance, out List<PlacedPart>? clearanceParts))
            {
                for (int i = 0; i < clearanceParts.Count && i < chain.Count; i++)
                {
                    clearanceParts[i].Symbol.Position = _geometry.CreatePoint2d(chain[i].XCm, chain[i].YCm);
                    moved++;
                }
            }

            if (positions.Precision is { } at && found.TryGetValue(HoleTableKind.Precision, out List<PlacedPart>? precisionParts))
            {
                precisionParts[0].Symbol.Position = _geometry.CreatePoint2d(at.XCm, at.YCm);
                moved++;
            }

            string warning = positions.Warning ?? "";
            if (positions.Clearance == null && clearanceSizes.Count == 1)
                warning += " " + HoleTableMessages.CreateCanContinue;
            return moved == 0
                ? HoleTableDrawResult.Unchanged(WithWarning(HoleTableMessages.Aligned(0), warning.Trim()))
                : HoleTableDrawResult.Done(WithWarning(HoleTableMessages.Aligned(moved), warning.Trim()));
        }

        /// <summary>
        /// Deletes a table that has no group any more: every placed symbol of its definition (every part), then the
        /// definitions (Update, task 166). False when there was nothing to delete.
        /// </summary>
        public bool DeleteTable(Sheet sheet, string sheetKey, string viewKey, HoleTableKind kind) =>
            DeleteLeftoverParts(sheet, sheetKey, viewKey, kind, 0);

        // Deletes the symbols and definitions of the table's parts numbered above keep (all parts when keep is 0).
        private bool DeleteLeftoverParts(Sheet sheet, string sheetKey, string viewKey, HoleTableKind kind, int keep)
        {
            var symbols = new List<SketchedSymbol>();
            foreach (SketchedSymbol symbol in sheet.SketchedSymbols)
            {
                if (HoleTableNames.PartOf(ComSafe.Get(() => symbol.Definition.Name), sheetKey, viewKey, kind) is int part && part > keep)
                    symbols.Add(symbol);
            }

            foreach (SketchedSymbol symbol in symbols)
                symbol.Delete();

            // The symbols go first: a definition that is still placed cannot be deleted.
            var definitions = new List<SketchedSymbolDefinition>();
            foreach (SketchedSymbolDefinition def in _drawing.SketchedSymbolDefinitions)
            {
                if (HoleTableNames.PartOf(ComSafe.Get(() => def.Name), sheetKey, viewKey, kind) is int part && part > keep)
                    definitions.Add(def);
            }

            foreach (SketchedSymbolDefinition def in definitions)
                def.Delete();
            return symbols.Count > 0 || definitions.Count > 0;
        }

        // ---- Anchor

        private static string WithWarning(string message, string? warning) =>
            string.IsNullOrEmpty(warning) ? message : message + " " + warning;

        private static SheetBox? ReadBorder(Sheet sheet) => ComSafe.Get(() =>
        {
            Box2d b = sheet.Border.RangeBox;
            return (SheetBox?)new SheetBox(b.MinPoint.X, b.MinPoint.Y, b.MaxPoint.X, b.MaxPoint.Y);
        });

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
            ComSafe.Run(() => WriteHeight(written, drawing.HeightCm, drawing.WidthCm, sheetName, viewName), warnings, "Table height attribute");
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
        private static void WriteHeight(SketchedSymbolDefinition def, double heightCm, double widthCm, string sheetName, string viewName)
        {
            AttributeSets sets = def.AttributeSets;
            if (sets.NameIsUsed[HoleTableNames.DrawingAttributeSetName])
                sets[HoleTableNames.DrawingAttributeSetName].Delete();
            AttributeSet set = sets.Add(HoleTableNames.DrawingAttributeSetName, false);
            set.Add(HoleTableNames.HeightAttributeName, ValueTypeEnum.kDoubleType, heightCm);
            set.Add(HoleTableNames.WidthAttributeName, ValueTypeEnum.kDoubleType, widthCm);
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

        // The drawn size: the attributes first; a definition drawn before task 220 has no width attribute, so its width is
        // the furthest left point of its lines (the table's right edge is x = 0). Null when the height is unreadable.
        private static HoleTableSize? ReadSize(SketchedSymbolDefinition def)
        {
            double? height = ReadHeight(def);
            if (height == null)
                return null;

            double? width = ComSafe.Get(() =>
            {
                AttributeSets sets = def.AttributeSets;
                if (!sets.NameIsUsed[HoleTableNames.DrawingAttributeSetName])
                    return (double?)null;
                AttributeSet set = sets[HoleTableNames.DrawingAttributeSetName];
                return set.NameIsUsed[HoleTableNames.WidthAttributeName] ? set[HoleTableNames.WidthAttributeName].Value as double? : null;
            });
            if (width is > 0)
                return new HoleTableSize(width.Value, height.Value);

            double? fromLines = ComSafe.Get(() =>
            {
                double left = 0;
                foreach (SketchLine line in def.Sketch.SketchLines)
                    left = Math.Min(left, Math.Min(line.StartSketchPoint.Geometry.X, line.EndSketchPoint.Geometry.X));
                return left < 0 ? (double?)(-left) : null;
            });
            return new HoleTableSize(fromLines ?? 0, height.Value);
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

        /// <summary>One placed part of a table: its number, definition name and symbol.</summary>
        private sealed record PlacedPart(int Part, string Name, SketchedSymbol Symbol);

        // The placed symbols of a table's parts, part 1 first (task 220).
        private static List<PlacedPart> FindParts(Sheet sheet, string sheetKey, string viewKey, HoleTableKind kind)
        {
            var found = new List<PlacedPart>();
            foreach (SketchedSymbol symbol in sheet.SketchedSymbols)
            {
                string? name = ComSafe.Get(() => symbol.Definition.Name);
                int part = HoleTableNames.PartOf(name, sheetKey, viewKey, kind);
                if (part > 0 && name != null)
                    found.Add(new PlacedPart(part, name, symbol));
            }

            found.Sort((a, b) => a.Part.CompareTo(b.Part));
            return found;
        }

        // The sizes of the parts' definitions; null when one cannot be read.
        private List<HoleTableSize>? ReadSizes(List<PlacedPart> parts)
        {
            var sizes = new List<HoleTableSize>();
            foreach (PlacedPart part in parts)
            {
                SketchedSymbolDefinition? def = FindDefinition(part.Name);
                HoleTableSize? size = def == null ? null : ReadSize(def);
                if (size == null)
                    return null;
                sizes.Add(size.Value);
            }

            return sizes;
        }

        private static SheetPoint? SymbolPosition(SketchedSymbol symbol) => ComSafe.Get(() =>
        {
            Point2d p = symbol.Position;
            return (SheetPoint?)new SheetPoint(p.X, p.Y);
        });

        /// <summary>A placed table's first part's position and height (and the block width of all its parts); null when it is not on the sheet or either cannot be read. Public for Update, which reads a clearance table before deleting it.</summary>
        public HoleTablePlacement? ReadPlacement(Sheet sheet, string sheetKey, string viewKey, HoleTableKind kind)
        {
            List<PlacedPart> parts = FindParts(sheet, sheetKey, viewKey, kind);
            if (parts.Count == 0 || parts[0].Part != 1)
                return null;
            List<HoleTableSize>? sizes = ReadSizes(parts);
            if (sizes == null)
                return null;
            SheetPoint? position = SymbolPosition(parts[0].Symbol);
            return position is { } at ? new HoleTablePlacement(at, sizes[0].HeightCm, HoleTableAlign.BlockWidth(sizes)) : null;
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

        // Puts every part of a drawn table on its fitted spot.
        private void PlaceKind(Sheet sheet, string sheetKey, string viewKey, HoleTableKind kind, HoleTableAlignment positions, Layer? layer, bool moveExisting)
        {
            if (kind == HoleTableKind.Precision)
            {
                if (positions.Precision is { } at)
                    PlaceSymbol(sheet, HoleTableNames.Definition(sheetKey, viewKey, kind), at, layer, moveExisting);
                return;
            }

            IReadOnlyList<SheetPoint> chain = positions.ClearanceParts ?? Array.Empty<SheetPoint>();
            for (int i = 0; i < chain.Count; i++)
                PlaceSymbol(sheet, HoleTableNames.Definition(sheetKey, viewKey, kind, i + 1), chain[i], layer, moveExisting);
        }

        // Create Tables aligns once: the view's tables that were not redrawn this time also go to the stack.
        private void MoveExisting(Sheet sheet, string sheetKey, string viewKey, HoleTableAlignment positions, List<HoleTableKind> drawn)
        {
            foreach (HoleTableKind kind in Kinds.Where(k => !drawn.Contains(k)))
            {
                List<PlacedPart> parts = FindParts(sheet, sheetKey, viewKey, kind);
                if (kind == HoleTableKind.Precision)
                {
                    if (parts.Count > 0 && positions.Precision is { } at)
                        parts[0].Symbol.Position = _geometry.CreatePoint2d(at.XCm, at.YCm);
                    continue;
                }

                IReadOnlyList<SheetPoint> chain = positions.ClearanceParts ?? Array.Empty<SheetPoint>();
                if (chain.Count == parts.Count)
                {
                    for (int i = 0; i < parts.Count; i++)
                        parts[i].Symbol.Position = _geometry.CreatePoint2d(chain[i].XCm, chain[i].YCm);
                }
            }
        }
    }
}
