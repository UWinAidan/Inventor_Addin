using System;
using System.Collections.Generic;
using System.Linq;
using Inventor;
using InventorAddin.Core.Drawing;

namespace InventorAddin.PrintViews
{
    /// <summary>
    /// Fits long title block values to their cells (task 238, spec 05 questions 30 to 32, spikes 218 and 241). Every sheet that has a view
    /// is read for what each field resolves to; Core (<see cref="TitleBlockFit"/>) decides one size, width scale and origin per field that
    /// fits every sheet's value (the definition is shared by all sheets); the drawing's own title block definition is then edited once
    /// (<c>Edit</c> ... <c>ExitEdit(true)</c>). Description is also given the cell's width when it needs two lines, so Inventor wraps it
    /// and it stays live. Run it inside the caller's transaction, so one Undo reverts it.
    /// </summary>
    /// <remarks>
    /// Best effort: nothing here throws. A failure is added to the warnings (and the edit is left without saving), and the print goes on.
    /// The template is never opened; only the drawing's copy of the definition changes.
    /// </remarks>
    internal static class TitleBlockFitter
    {
        /// <summary>
        /// Returns how many field text boxes were changed. <paramref name="warnings"/> gets a line for each value too long for its cell at
        /// the smallest size, and for each step that failed.
        /// </summary>
        public static int Apply(DrawingDocument drawing, List<string> warnings)
        {
            Sheet? original = null;
            try
            {
                original = ComSafe.Get(() => drawing.ActiveSheet);
                var cells = PrintTemplateLayout.Metric.Cells.Where(TitleBlockFit.IsFitted).ToDictionary(c => c.Field!.Value);

                // 1. What each field prints on each sheet that has a view.
                var values = new Dictionary<PrintField, List<string>>();
                TitleBlockDefinition? definition = null;
                foreach (Sheet sheet in drawing.Sheets)
                {
                    if ((ComSafe.Get(() => (int?)sheet.DrawingViews.Count) ?? 0) == 0)
                        continue;

                    // The result text is read for the active sheet's model (spike 218).
                    ComSafe.Run(() => sheet.Activate(), warnings, "Activating a sheet to read its title block");
                    TitleBlock? titleBlock = ComSafe.Get(() => sheet.TitleBlock);
                    TitleBlockDefinition? sheetDefinition = ComSafe.Get(() => titleBlock?.Definition);
                    if (titleBlock == null || sheetDefinition == null)
                        continue;
                    definition ??= sheetDefinition;

                    // Only sheets that use the definition being edited feed it; another definition's sheets are left out.
                    string? name = ComSafe.Get(() => sheetDefinition.Name);
                    string? editedName = ComSafe.Get(() => definition.Name);
                    if (!ReferenceEquals(sheetDefinition, definition) && !string.Equals(name, editedName, StringComparison.Ordinal))
                        continue;

                    var seen = new HashSet<PrintField>();
                    foreach (TextBox box in sheetDefinition.Sketch.TextBoxes)
                    {
                        PrintField? field = FieldOf(box, cells, seen);
                        if (field == null)
                            continue;

                        TextBox target = box;
                        string? text = ComSafe.Get(() => titleBlock.GetResultText(target));
                        if (!values.TryGetValue(field.Value, out List<string>? list))
                            values[field.Value] = list = new List<string>();
                        if (!string.IsNullOrWhiteSpace(text))
                            list.Add(text!);
                    }
                }

                if (definition == null || values.Count == 0)
                    return 0;

                // 2. The decision per field, and which boxes differ from it.
                var targets = new Dictionary<PrintField, TitleBlockFitResult>();
                foreach (var pair in values)
                {
                    if (pair.Value.Count == 0)
                        continue;
                    TitleBlockFitResult result = TitleBlockFit.Fit(cells[pair.Key], pair.Value);
                    targets[pair.Key] = result;
                    if (result.Warning != null)
                        warnings.Add(result.Warning);
                }

                var toChange = new HashSet<PrintField>();
                var readSeen = new HashSet<PrintField>();
                foreach (TextBox box in definition.Sketch.TextBoxes)
                {
                    PrintField? field = FieldOf(box, cells, readSeen);
                    if (field == null || !targets.TryGetValue(field.Value, out TitleBlockFitResult? target))
                        continue;

                    TextBox current = box;
                    double? sizeMm = TitleBlockFit.SizeMmOf(ComSafe.Get(() => current.FormattedText));
                    double? scale = ComSafe.Get(() => (double?)current.WidthScale);
                    double? originYCm = ComSafe.Get(() => (double?)current.Origin.Y);
                    double? widthCm = ComSafe.Get(() => (double?)current.Width);
                    if (scale == null || originYCm == null
                        || TitleBlockFit.NeedsChange(target, sizeMm, scale.Value, originYCm.Value * 10, widthCm * 10))
                        toChange.Add(field.Value);
                }

                if (toChange.Count == 0)
                    return 0;

                // 3. One edit of the drawing's own definition.
                return Edit(definition, targets, toChange, cells, warnings);
            }
            catch (Exception ex)
            {
                warnings.Add($"Fitting the title block values failed. {ex.Message}");
                return 0;
            }
            finally
            {
                if (original != null)
                {
                    Sheet toActivate = original;
                    ComSafe.Run(() => toActivate.Activate());
                }
            }
        }

        private static int Edit(
            TitleBlockDefinition definition, Dictionary<PrintField, TitleBlockFitResult> targets, HashSet<PrintField> toChange,
            Dictionary<PrintField, TitleBlockCell> cells, List<string> warnings)
        {
            int changed = 0;
            DrawingSketch? sketch = null;
            try
            {
                TransientGeometry geometry = InventorHost.App.TransientGeometry;
                sketch = TitleBlockPropertySource.Edit(definition);
                var seen = new HashSet<PrintField>();
                foreach (TextBox box in sketch.TextBoxes)
                {
                    PrintField? field = FieldOf(box, cells, seen);
                    if (field == null || !toChange.Contains(field.Value))
                        continue;

                    TitleBlockFitResult target = targets[field.Value];
                    string formatted = box.FormattedText;
                    string updated = TitleBlockFit.WithSizeMm(formatted, target.SizeMm);
                    if (!string.Equals(updated, formatted, StringComparison.Ordinal))
                        box.FormattedText = updated;

                    // Width only for a value that wraps (Description); on a single-line field it would make the box wrap (spike 241).
                    if (target.BoxWidthMm is { } widthMm)
                        box.Width = widthMm / 10;
                    box.WidthScale = target.WidthScale;

                    Point2d origin = box.Origin;
                    box.Origin = geometry.CreatePoint2d(origin.X, target.OriginYMm / 10);
                    changed++;
                }

                definition.ExitEdit(true);
                sketch = null;
                return changed;
            }
            catch (Exception ex)
            {
                warnings.Add($"Fitting the title block values failed, so none were changed. {ex.Message}");
                if (sketch != null)
                {
                    try { definition.ExitEdit(false); }
                    catch (Exception exitEx) { warnings.Add($"Leaving the title block editor failed. {exitEx.Message}"); }
                }

                return 0;
            }
        }

        /// <summary>
        /// The field a definition text box prints, when it is a fitted one: a property field that is not rotated (the small part number in
        /// the margin reads the Part Number too) and the first box of its field.
        /// </summary>
        private static PrintField? FieldOf(TextBox box, Dictionary<PrintField, TitleBlockCell> cells, HashSet<PrintField> seen)
        {
            double? rotation = ComSafe.Get(() => (double?)box.Rotation);
            if (rotation == null || Math.Abs(rotation.Value) > 1e-6)
                return null;

            PrintField? field = TitleBlockFit.FieldOf(ComSafe.Get(() => box.FormattedText));
            if (field == null || !cells.ContainsKey(field.Value) || !seen.Add(field.Value))
                return null;
            return field;
        }
    }
}
