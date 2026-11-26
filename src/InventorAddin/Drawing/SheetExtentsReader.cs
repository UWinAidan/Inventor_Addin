using System;
using System.Collections.Generic;
using Inventor;
using InventorAddin.Core.Drawing;

namespace InventorAddin.PrintViews
{
    /// <summary>One thing on a sheet to check: its name and rectangle for Core, and the Inventor object to select.</summary>
    internal sealed class SheetExtentItem
    {
        public SheetExtentItem(BoundsItem item, object target)
        {
            Item = item;
            Target = target;
        }

        public BoundsItem Item { get; }
        public object Target { get; }
    }

    /// <summary>A sheet's border, title block and the items on it, all in millimetres, Y up (Core's units).</summary>
    internal sealed class SheetExtents
    {
        public SheetExtents(Sheet sheet, string name, SheetRect border, SheetRect? titleBlock, List<SheetExtentItem> items, int skipped)
        {
            Sheet = sheet;
            Name = name;
            Border = border;
            TitleBlock = titleBlock;
            Items = items;
            Skipped = skipped;
        }

        public Sheet Sheet { get; }
        public string Name { get; }
        public SheetRect Border { get; }
        public SheetRect? TitleBlock { get; }
        public List<SheetExtentItem> Items { get; }

        /// <summary>How many items were left out because their extent could not be read.</summary>
        public int Skipped { get; }
    }

    /// <summary>
    /// Reads the extents of what is on a sheet for Check Out of Bounds (task 195, spike 184 question 1). Inventor reports sheet
    /// coordinates in centimetres, origin at the sheet's bottom-left, Y up; <see cref="Mm"/> is the one place they become millimetres.
    /// How each kind of object gives its extent: views have no range box, so their Left, Top, Width and Height are used; dimensions
    /// use their text's range box; balloons use their position and each leader node; sketched symbols only have a position; notes
    /// and tables have a range box. Nothing here throws: an object whose extent cannot be read is left out and a warning is added.
    /// </summary>
    internal static class SheetExtentsReader
    {
        /// <summary>The one conversion from Inventor's centimetres to Core's millimetres.</summary>
        private static double Mm(double cm) => cm * 10.0;

        public static SheetExtents Read(Sheet sheet, List<string> warnings)
        {
            string sheetName = ComSafe.Get(() => sheet.Name, warnings, "sheet name") ?? "Sheet";

            // Border.RangeBox is the inner rectangle. With no border (a sheet made without one), the sheet's own edge is used.
            SheetRect border = ReadBox(ComSafe.Get(() => sheet.Border.RangeBox)) ?? SheetEdge(sheet);
            SheetRect? titleBlock = ReadBox(ComSafe.Get(() => sheet.TitleBlock.RangeBox));

            var c = new Collector();
            ReadViews(sheet, c);
            ReadNotes(sheet, c);
            ReadDimensions(sheet, c);
            ReadBalloons(sheet, c);
            ReadSymbols(sheet, c);
            ReadTables(sheet, c);
            warnings.AddRange(c.Warnings);
            return new SheetExtents(sheet, sheetName, border, titleBlock, c.Items, c.Skipped);
        }

        /// <summary>The sheet's border and title block in millimetres (the border is the sheet's edge when it has none), for placing notes (task 197).</summary>
        public static (SheetRect Border, SheetRect? TitleBlock) ReadFrame(Sheet sheet) =>
            (ReadBox(ComSafe.Get(() => sheet.Border.RangeBox)) ?? SheetEdge(sheet), ReadBox(ComSafe.Get(() => sheet.TitleBlock.RangeBox)));

        private static SheetRect SheetEdge(Sheet sheet)
        {
            double w = ComSafe.Get(() => (double?)sheet.Width) ?? 0;
            double h = ComSafe.Get(() => (double?)sheet.Height) ?? 0;
            return new SheetRect(0, 0, Mm(w), Mm(h));
        }

        private static SheetRect? ReadBox(Box2d? box)
        {
            if (box == null)
                return null;
            double? minX = ComSafe.Get(() => (double?)box.MinPoint.X);
            double? minY = ComSafe.Get(() => (double?)box.MinPoint.Y);
            double? maxX = ComSafe.Get(() => (double?)box.MaxPoint.X);
            double? maxY = ComSafe.Get(() => (double?)box.MaxPoint.Y);
            if (!minX.HasValue || !minY.HasValue || !maxX.HasValue || !maxY.HasValue)
                return null;
            return new SheetRect(Mm(minX.Value), Mm(minY.Value), Mm(maxX.Value), Mm(maxY.Value));
        }

        /// <summary>The items found so far, the warnings, and how many items were skipped for want of an extent.</summary>
        private sealed class Collector
        {
            public List<SheetExtentItem> Items { get; } = new List<SheetExtentItem>();
            public List<string> Warnings { get; } = new List<string>();
            public int Skipped { get; private set; }

            public void Add(string name, SheetRect? rect, object target)
            {
                if (rect.HasValue)
                    Items.Add(new SheetExtentItem(new BoundsItem(name, rect.Value), target));
                else
                    Skip(name);
            }

            public void Skip(string name)
            {
                Skipped++;
                Warnings.Add(OutOfBoundsReport.NotCheckedWarning(name));
            }
        }

        private static void ReadViews(Sheet sheet, Collector c)
        {
            DrawingViews? views = ComSafe.Get(() => sheet.DrawingViews, c.Warnings, "drawing views");
            int count = views == null ? 0 : ComSafe.Get(() => (int?)views.Count) ?? 0;
            for (int i = 1; i <= count; i++)
            {
                DrawingView? view = ComSafe.Get(() => views![i], c.Warnings, $"view {i}");
                if (view == null)
                    continue;
                double? left = ComSafe.Get(() => (double?)view.Left);
                double? top = ComSafe.Get(() => (double?)view.Top);
                double? width = ComSafe.Get(() => (double?)view.Width);
                double? height = ComSafe.Get(() => (double?)view.Height);
                string name = ComSafe.Get(() => view.Name) ?? $"{i}";
                if (!left.HasValue || !top.HasValue || !width.HasValue || !height.HasValue)
                {
                    c.Warnings.Add($"View {name} was not checked: its size could not be read.");
                    continue;
                }

                // Top is the view's top edge; the view extends down by Height.
                c.Add($"View {name}",
                    new SheetRect(Mm(left.Value), Mm(top.Value - height.Value), Mm(left.Value + width.Value), Mm(top.Value)), view);
            }
        }

        private static void ReadNotes(Sheet sheet, Collector c)
        {
            DrawingNotes? notes = ComSafe.Get(() => sheet.DrawingNotes, c.Warnings, "drawing notes");
            if (notes == null)
                return;

            GeneralNotes? general = ComSafe.Get(() => notes.GeneralNotes);
            int generalCount = general == null ? 0 : ComSafe.Get(() => (int?)general.Count) ?? 0;
            for (int i = 1; i <= generalCount; i++)
            {
                GeneralNote? note = ComSafe.Get(() => general![i], c.Warnings, $"note {i}");
                if (note != null)
                    c.Add($"Note \"{Shorten(ComSafe.Get(() => note.Text), $"{i}")}\"", ReadBox(ComSafe.Get(() => note.RangeBox)), note);
            }

            LeaderNotes? leaders = ComSafe.Get(() => notes.LeaderNotes);
            int leaderCount = leaders == null ? 0 : ComSafe.Get(() => (int?)leaders.Count) ?? 0;
            for (int i = 1; i <= leaderCount; i++)
            {
                LeaderNote? note = ComSafe.Get(() => leaders![i], c.Warnings, $"leader note {i}");
                if (note != null)
                    c.Add($"Leader note \"{Shorten(ComSafe.Get(() => note.Text), $"{i}")}\"", ReadBox(ComSafe.Get(() => note.RangeBox)), note);
            }
        }

        private static void ReadDimensions(Sheet sheet, Collector c)
        {
            GeneralDimensions? dims = ComSafe.Get(() => sheet.DrawingDimensions.GeneralDimensions, c.Warnings, "dimensions");
            int count = dims == null ? 0 : ComSafe.Get(() => (int?)dims.Count) ?? 0;
            for (int i = 1; i <= count; i++)
            {
                GeneralDimension? dim = ComSafe.Get(() => dims![i], c.Warnings, $"dimension {i}");
                if (dim == null)
                    continue;
                DimensionText? text = ComSafe.Get(() => dim.Text);
                if (text == null)
                {
                    c.Skip($"Dimension {i}");
                    continue;
                }
                c.Add($"Dimension \"{Shorten(ComSafe.Get(() => text.Text), $"{i}")}\"", ReadBox(ComSafe.Get(() => text.RangeBox)), dim);
            }
        }

        private static void ReadBalloons(Sheet sheet, Collector c)
        {
            Balloons? balloons = ComSafe.Get(() => sheet.Balloons, c.Warnings, "balloons");
            int count = balloons == null ? 0 : ComSafe.Get(() => (int?)balloons.Count) ?? 0;
            for (int i = 1; i <= count; i++)
            {
                Balloon? balloon = ComSafe.Get(() => balloons![i], c.Warnings, $"balloon {i}");
                if (balloon == null)
                    continue;

                var xs = new List<double>();
                var ys = new List<double>();
                double? px = ComSafe.Get(() => (double?)balloon.Position.X);
                double? py = ComSafe.Get(() => (double?)balloon.Position.Y);
                if (px.HasValue && py.HasValue)
                {
                    xs.Add(px.Value);
                    ys.Add(py.Value);
                }

                LeaderNodesEnumerator? nodes = ComSafe.Get(() => balloon.Leader.AllNodes);
                int nodeCount = nodes == null ? 0 : ComSafe.Get(() => (int?)nodes.Count) ?? 0;
                for (int n = 1; n <= nodeCount; n++)
                {
                    LeaderNode? node = ComSafe.Get(() => nodes![n]);
                    double? nx = node == null ? null : ComSafe.Get(() => (double?)node.Position.X);
                    double? ny = node == null ? null : ComSafe.Get(() => (double?)node.Position.Y);
                    if (nx.HasValue && ny.HasValue)
                    {
                        xs.Add(nx.Value);
                        ys.Add(ny.Value);
                    }
                }

                if (xs.Count == 0)
                {
                    c.Warnings.Add($"Balloon {i} was not checked: its position could not be read.");
                    continue;
                }

                c.Add($"Balloon {i}", new SheetRect(Mm(Min(xs)), Mm(Min(ys)), Mm(Max(xs)), Mm(Max(ys))), balloon);
            }
        }

        private static void ReadSymbols(Sheet sheet, Collector c)
        {
            SketchedSymbols? symbols = ComSafe.Get(() => sheet.SketchedSymbols, c.Warnings, "sketched symbols");
            int count = symbols == null ? 0 : ComSafe.Get(() => (int?)symbols.Count) ?? 0;
            for (int i = 1; i <= count; i++)
            {
                SketchedSymbol? symbol = ComSafe.Get(() => symbols![i], c.Warnings, $"sketched symbol {i}");
                if (symbol == null)
                    continue;
                // Only the centre is known (spike 184), so the item is a point.
                double? x = ComSafe.Get(() => (double?)symbol.Position.X);
                double? y = ComSafe.Get(() => (double?)symbol.Position.Y);
                string name = ComSafe.Get(() => symbol.Name) ?? $"{i}";
                if (!x.HasValue || !y.HasValue)
                {
                    c.Skip($"Symbol {name}");
                    continue;
                }
                c.Add($"Symbol {name}", new SheetRect(Mm(x.Value), Mm(y.Value), Mm(x.Value), Mm(y.Value)), symbol);
            }
        }

        private static void ReadTables(Sheet sheet, Collector c)
        {
            PartsLists? parts = ComSafe.Get(() => sheet.PartsLists, c.Warnings, "parts lists");
            int partsCount = parts == null ? 0 : ComSafe.Get(() => (int?)parts.Count) ?? 0;
            for (int i = 1; i <= partsCount; i++)
            {
                PartsList? table = ComSafe.Get(() => parts![i]);
                if (table != null)
                    c.Add(partsCount == 1 ? "Parts list" : $"Parts list {i}", ReadBox(ComSafe.Get(() => table.RangeBox)), table);
            }

            RevisionTables? revisions = ComSafe.Get(() => sheet.RevisionTables, c.Warnings, "revision tables");
            int revisionCount = revisions == null ? 0 : ComSafe.Get(() => (int?)revisions.Count) ?? 0;
            for (int i = 1; i <= revisionCount; i++)
            {
                RevisionTable? table = ComSafe.Get(() => revisions![i]);
                if (table != null)
                    c.Add(revisionCount == 1 ? "Revision table" : $"Revision table {i}", ReadBox(ComSafe.Get(() => table.RangeBox)), table);
            }

            Inventor.HoleTables? holes = ComSafe.Get(() => sheet.HoleTables, c.Warnings, "hole tables");
            int holeCount = holes == null ? 0 : ComSafe.Get(() => (int?)holes.Count) ?? 0;
            for (int i = 1; i <= holeCount; i++)
            {
                HoleTable? table = ComSafe.Get(() => holes![i]);
                if (table != null)
                    c.Add(holeCount == 1 ? "Hole table" : $"Hole table {i}", ReadBox(ComSafe.Get(() => table.RangeBox)), table);
            }

            CustomTables? customs = ComSafe.Get(() => sheet.CustomTables, c.Warnings, "custom tables");
            int customCount = customs == null ? 0 : ComSafe.Get(() => (int?)customs.Count) ?? 0;
            for (int i = 1; i <= customCount; i++)
            {
                CustomTable? table = ComSafe.Get(() => customs![i]);
                if (table != null)
                    c.Add(customCount == 1 ? "Table" : $"Table {i}", ReadBox(ComSafe.Get(() => table.RangeBox)), table);
            }
        }

        private static double Min(List<double> values)
        {
            double m = values[0];
            foreach (double v in values)
                m = Math.Min(m, v);
            return m;
        }

        private static double Max(List<double> values)
        {
            double m = values[0];
            foreach (double v in values)
                m = Math.Max(m, v);
            return m;
        }

        /// <summary>The text on one line, cut to about 30 characters; <paramref name="fallback"/> when there is none.</summary>
        private static string Shorten(string? text, string fallback)
        {
            string flat = (text ?? "").Replace("\r", " ").Replace("\n", " ").Trim();
            if (flat.Length == 0)
                return fallback;
            return flat.Length <= 30 ? flat : flat.Substring(0, 30) + "...";
        }
    }
}
