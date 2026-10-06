using System.Collections.Generic;
using Inventor;
using InventorAddin.Core.Models;

namespace InventorAddin.Extraction
{
    public static class DrawingExtractor
    {
        public static void Fill(DrawingData data, DrawingDocument doc, ExtractionOptions options)
        {
            var w = data.Warnings;
            Sheet? active = ComSafe.Get(() => doc.ActiveSheet);

            foreach (Sheet sheet in doc.Sheets)
            {
                ComSafe.Run(() => data.Sheets.Add(ReadSheet(sheet, sheet == active, options, w)), w, $"Sheet '{sheet.Name}'");
            }

            if (options.IncludeDrawingModels)
            {
                // Avoid recursing back into drawings (a drawing can reference another drawing's views in rare cases)
                var modelOptions = new ExtractionOptions
                {
                    IncludeMassProperties = options.IncludeMassProperties,
                    IncludeILogicRuleText = options.IncludeILogicRuleText,
                    EnableBomViews = options.EnableBomViews,
                    IncludeDrawingModels = false,
                    IncludeTableContents = options.IncludeTableContents,
                };

                foreach (Document refDoc in doc.ReferencedDocuments)
                {
                    if (refDoc.DocumentType == DocumentTypeEnum.kDrawingDocumentObject)
                        continue;
                    ModelData? model = ComSafe.Get(() => ModelExtractor.Extract(refDoc, modelOptions), w, $"Referenced model '{refDoc.DisplayName}'");
                    if (model != null)
                        data.ReferencedModels.Add(model);
                }
            }
        }

        private static SheetData ReadSheet(Sheet sheet, bool isActive, ExtractionOptions options, List<string> w)
        {
            var data = new SheetData
            {
                Name = sheet.Name,
                IsActive = isActive,
                Size = ComSafe.Get(() => sheet.Size.ToString()),
                Orientation = ComSafe.Get(() => sheet.Orientation.ToString()),
                WidthCm = sheet.Width,
                HeightCm = sheet.Height,
                BorderName = ComSafe.Get(() => sheet.Border?.Name),
                TitleBlock = ComSafe.Get(() => ReadTitleBlock(sheet.TitleBlock), w, $"Title block on '{sheet.Name}'"),
            };

            foreach (DrawingView view in sheet.DrawingViews)
                ComSafe.Run(() => data.Views.Add(ReadView(view)), w, $"View '{view.Name}'");

            ComSafe.Run(() =>
            {
                foreach (HoleTable t in sheet.HoleTables)
                    data.HoleTables.Add(ReadHoleTable(t, options.IncludeTableContents));
            }, w, $"Hole tables on '{sheet.Name}'");

            ComSafe.Run(() =>
            {
                foreach (PartsList t in sheet.PartsLists)
                    data.PartsLists.Add(ReadPartsList(t, options.IncludeTableContents));
            }, w, $"Parts lists on '{sheet.Name}'");

            ComSafe.Run(() =>
            {
                foreach (RevisionTable t in sheet.RevisionTables)
                    data.RevisionTables.Add(ReadRevisionTable(t, options.IncludeTableContents));
            }, w, $"Revision tables on '{sheet.Name}'");

            ComSafe.Run(() =>
            {
                foreach (SketchedSymbol s in sheet.SketchedSymbols)
                    data.SketchedSymbols.Add(s.Name);
            }, w, $"Sketched symbols on '{sheet.Name}'");

            return data;
        }

        private static TitleBlockData? ReadTitleBlock(TitleBlock? tb)
        {
            if (tb == null)
                return null;

            var data = new TitleBlockData { Name = tb.Name };
            foreach (TextBox box in tb.Definition.Sketch.TextBoxes)
            {
                string formatted = ComSafe.Get(() => box.FormattedText) ?? "";
                data.Fields.Add(new TitleBlockField
                {
                    Source = ComSafe.Get(() => box.Text) ?? "",
                    Value = ComSafe.Get(() => tb.GetResultText(box)) ?? "",
                    IsPrompted = formatted.Contains("<Prompt"),
                });
            }
            return data;
        }

        private static DrawingViewData ReadView(DrawingView view) => new()
        {
            Name = view.Name,
            ViewType = ComSafe.Get(() => view.ViewType.ToString()),
            ViewStyle = ComSafe.Get(() => view.ViewStyle.ToString()),
            Orientation = ComSafe.Get(() => view.Camera.ViewOrientationType.ToString()),
            Scale = ComSafe.Get(() => view.Scale),
            ScaleString = ComSafe.Get(() => view.ScaleString),
            ReferencedFile = ComSafe.Get(() => view.ReferencedDocumentDescriptor.FullDocumentName),
            ParentViewName = ComSafe.Get(() => view.ParentView?.Name),
            IsFlatPatternView = ComSafe.Get(() => view.IsFlatPatternView),
            Suppressed = ComSafe.Get(() => view.Suppressed),
            CenterX = ComSafe.Get(() => view.Center.X),
            CenterY = ComSafe.Get(() => view.Center.Y),
            WidthCm = ComSafe.Get(() => view.Width),
            HeightCm = ComSafe.Get(() => view.Height),
        };

        private static TableSummary ReadHoleTable(HoleTable t, bool includeContents)
        {
            var data = new TableSummary
            {
                Title = ComSafe.Get(() => t.Title),
                StyleName = ComSafe.Get(() => t.Style.Name),
            };
            foreach (HoleTableColumn c in t.HoleTableColumns)
                data.Columns.Add(ComSafe.Get(() => c.Title) ?? "");

            if (includeContents)
            {
                int cols = data.Columns.Count;
                foreach (HoleTableRow row in t.HoleTableRows)
                {
                    var cells = new List<string>();
                    for (int i = 1; i <= cols; i++)
                        cells.Add(ComSafe.Get(() => row[i].Text) ?? "");
                    data.Rows.Add(cells);
                }
            }
            return data;
        }

        private static TableSummary ReadPartsList(PartsList t, bool includeContents)
        {
            var data = new TableSummary
            {
                Title = ComSafe.Get(() => t.Title),
                StyleName = ComSafe.Get(() => t.Style.Name),
            };
            foreach (PartsListColumn c in t.PartsListColumns)
                data.Columns.Add(ComSafe.Get(() => c.Title) ?? "");

            if (includeContents)
            {
                int cols = data.Columns.Count;
                foreach (PartsListRow row in t.PartsListRows)
                {
                    var cells = new List<string>();
                    for (int i = 1; i <= cols; i++)
                        cells.Add(ComSafe.Get(() => row[i].Value) ?? "");
                    data.Rows.Add(cells);
                }
            }
            return data;
        }

        private static TableSummary ReadRevisionTable(RevisionTable t, bool includeContents)
        {
            var data = new TableSummary
            {
                Title = ComSafe.Get(() => t.Title),
                StyleName = ComSafe.Get(() => t.Style.Name),
            };
            foreach (RevisionTableColumn c in t.RevisionTableColumns)
                data.Columns.Add(ComSafe.Get(() => c.Title) ?? "");

            if (includeContents)
            {
                int cols = data.Columns.Count;
                foreach (RevisionTableRow row in t.RevisionTableRows)
                {
                    var cells = new List<string>();
                    for (int i = 1; i <= cols; i++)
                        cells.Add(ComSafe.Get(() => row[i].Text) ?? "");
                    data.Rows.Add(cells);
                }
            }
            return data;
        }
    }
}
