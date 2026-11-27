using System;
using System.Collections.Generic;
using Inventor;
using InventorAddin.Core.Drawing;
using InventorAddin.Core.PartProperties;
using InventorAddin.Extraction;
using IOPath = System.IO.Path;

// The Drawing folder holds the drawing-side helpers; the namespace names the feature, not the folder (Inventor has types named Drawing*).
namespace InventorAddin.PrintViews
{
    /// <summary>What <see cref="PartsListMakeBuyWriter.Update"/> did.</summary>
    internal sealed class PartsListMakeBuyResult
    {
        public int PartsLists { get; set; }
        public int ColumnsAdded { get; set; }
        public int Make { get; set; }
        public int Buy { get; set; }
        public int Blank { get; set; }
        public int Cleared { get; set; }
        public int FailedLists { get; set; }
        public int FailedCells { get; set; }
    }

    /// <summary>
    /// Fills the Make/Buy cells of every parts list on a sheet (spec 05, "Added for M5", item 6; task 208). The decision is Core's
    /// (<see cref="MakeBuy"/>, <see cref="PartsListMakeBuy"/>); this class reads each row's file and applies the text. Calls are those of
    /// spike 184, question 7 (<c>PartsListRow.Item</c>, <c>PartsListCell.Value</c> making the cell static, <c>PartsListRow.ReferencedFiles</c>,
    /// <c>ReferencedFileDescriptor.FullFileName</c> and <c>ReferencedDocument</c>, <c>PartComponentDefinition.IsContentMember</c>) and spike 183
    /// (<c>PartsListColumn.CustomPropertyName</c>), plus <c>PartsListColumns.Add</c>, <c>PartsListColumn.Title</c> and <c>PartsListRows.Count</c>.
    /// </summary>
    /// <remarks>
    /// Run it inside the caller's transaction, so one Undo reverts every cell and any added column. A row with no referenced file (a virtual
    /// component) is skipped and counted as blank. A blank decision writes nothing, so a typed value in that cell stays.
    /// </remarks>
    internal static class PartsListMakeBuyWriter
    {
        public static PartsListMakeBuyResult Update(Sheet sheet, IReadOnlyList<string> libraryFolders, List<string> warnings)
        {
            var result = new PartsListMakeBuyResult();
            PartsLists? lists = ComSafe.Get(() => sheet.PartsLists, warnings, "parts lists");
            int listCount = lists == null ? 0 : ComSafe.Get(() => (int?)lists.Count, warnings, "parts list count") ?? 0;
            for (int i = 1; lists != null && i <= listCount; i++)
            {
                int index = i;
                PartsList? list = ComSafe.Get(() => lists[index], warnings, $"parts list {index}");
                if (list != null)
                    UpdateList(list, libraryFolders, warnings, result);
            }

            return result;
        }

        private static void UpdateList(PartsList list, IReadOnlyList<string> libraryFolders, List<string> warnings, PartsListMakeBuyResult result)
        {
            int column = FindColumn(list, warnings);
            if (column == 0)
            {
                column = AddColumn(list, warnings);
                if (column == 0)
                {
                    result.FailedLists++;
                    return;
                }

                result.ColumnsAdded++;
            }

            result.PartsLists++;
            PartsListRows? rows = ComSafe.Get(() => list.PartsListRows, warnings, "parts list rows");
            int rowCount = rows == null ? 0 : ComSafe.Get(() => (int?)rows.Count, warnings, "parts list row count") ?? 0;
            var documentCache = new Dictionary<string, (string? PartNumber, bool ContentCenter)>(StringComparer.OrdinalIgnoreCase);
            for (int r = 1; rows != null && r <= rowCount; r++)
            {
                int rowIndex = r;
                PartsListRow? row = ComSafe.Get(() => rows[rowIndex], warnings, $"parts list row {rowIndex}");
                if (row != null)
                    UpdateRow(row, column, libraryFolders, documentCache, warnings, result);
            }
        }

        private static int FindColumn(PartsList list, List<string> warnings)
        {
            PartsListColumns? columns = ComSafe.Get(() => list.PartsListColumns, warnings, "parts list columns");
            int count = columns == null ? 0 : ComSafe.Get(() => (int?)columns.Count, warnings, "parts list column count") ?? 0;
            for (int c = 1; columns != null && c <= count; c++)
            {
                int index = c;
                PartsListColumn? column = ComSafe.Get(() => columns[index], warnings, $"parts list column {index}");
                if (column == null) continue;
                string? property = ComSafe.Get(() => column.CustomPropertyName);
                string? title = ComSafe.Get(() => column.Title);
                if (PartsListMakeBuy.IsMakeBuyColumn(property, title))
                    return index;
            }

            return 0;
        }

        /// <summary>Appends a custom-property column "Make/Buy" with the heading "MAKE/BUY"; returns its 1-based index, or 0 on failure.</summary>
        private static int AddColumn(PartsList list, List<string> warnings)
        {
            int added = 0;
            ComSafe.Run(() =>
            {
                PartsListColumns columns = list.PartsListColumns;
                // Appended: TargetIndex is left out, so the column goes after the last one (assumed; see task 208 Verification).
                PartsListColumn column = columns.Add(PropertyTypeEnum.kCustomProperty, PropertyIdentifier: PartsListMakeBuy.CustomPropertyName);
                added = columns.Count;
                // The heading is set on its own: a heading that fails must not undo the added column.
                ComSafe.Run(() => column.Title = PartsListMakeBuy.ColumnTitle, warnings, "setting the Make/Buy column heading");
            }, warnings, "adding the Make/Buy column");
            return added;
        }

        private static void UpdateRow(
            PartsListRow row,
            int column,
            IReadOnlyList<string> libraryFolders,
            Dictionary<string, (string? PartNumber, bool ContentCenter)> documentCache,
            List<string> warnings,
            PartsListMakeBuyResult result)
        {
            ReferencedFileDescriptor? file = ComSafe.Get(() => row.ReferencedFiles[1]);
            string? fullFileName = file == null ? null : ComSafe.Get(() => file.FullFileName);

            // A row with no file (a virtual component) is blank, like any part that is neither Make nor Buy.
            string text = string.Empty;
            if (file != null && !string.IsNullOrWhiteSpace(fullFileName))
            {
                if (!documentCache.TryGetValue(fullFileName!, out (string? PartNumber, bool ContentCenter) info))
                {
                    info = ReadDocument(file, fullFileName!);
                    documentCache[fullFileName!] = info;
                }

                text = MakeBuy.CellText(info.PartNumber, info.ContentCenter, LibraryFolderRule.IsUnder(fullFileName, libraryFolders));
            }

            PartsListCell? cell = ComSafe.Get(() => row[column], warnings, "Make/Buy cell");
            if (cell == null)
            {
                result.FailedCells++;
                return;
            }

            string? current = ComSafe.Get(() => cell.Value);
            if (text.Length == 0)
            {
                // Blank: leave typed text alone, but clear a MAKE or BUY an earlier run wrote.
                if (PartsListMakeBuy.ShouldClear(text, current))
                {
                    ComSafe.Run(() => cell.Value = string.Empty, warnings, $"clearing Make/Buy for '{fullFileName}'");
                    if (PartsListMakeBuy.ShouldClear(text, ComSafe.Get(() => cell.Value)))
                    {
                        result.FailedCells++;
                        return;
                    }

                    result.Cleared++;
                }

                result.Blank++;
                return;
            }

            if (PartsListMakeBuy.ShouldWrite(text, current))
            {
                ComSafe.Run(() => cell.Value = text, warnings, $"writing Make/Buy for '{fullFileName}'");
                if (ComSafe.Get(() => cell.Value) != text)
                {
                    result.FailedCells++;
                    return;
                }
            }

            if (text == MakeBuy.MakeText) result.Make++;
            else result.Buy++;
        }

        /// <summary>The file's Part Number (its file name without the extension when the document is not available) and whether it is a Content Center part.</summary>
        private static (string? PartNumber, bool ContentCenter) ReadDocument(ReferencedFileDescriptor file, string fullFileName)
        {
            string? partNumber = null;
            bool contentCenter = false;
            Document? doc = ComSafe.Get(() => file.ReferencedDocument as Document);
            if (doc != null)
            {
                partNumber = PropertyReader.GetString(doc, PartPropertyStorage.PartNumber.SetName, PartPropertyStorage.PartNumber.PropertyName);
                contentCenter = ComSafe.Get(() => doc is PartDocument part && part.ComponentDefinition.IsContentMember);
            }

            if (string.IsNullOrWhiteSpace(partNumber))
                partNumber = IOPath.GetFileNameWithoutExtension(fullFileName);
            return (partNumber, contentCenter);
        }
    }
}
