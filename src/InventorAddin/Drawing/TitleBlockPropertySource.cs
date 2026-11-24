using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using Inventor;
using InventorAddin.Core.Drawing;

namespace InventorAddin.PrintViews
{
    /// <summary>
    /// Points the title block fields named in <c>propertyNames</c> (the custom Finish Spec and Mass) at the drawing's own properties
    /// instead of the model's (task 232, spec 05 question 33). Only the drawing's own copy of the title block definition is edited, so the
    /// template and other drawings are not touched. The text change is Core's (<see cref="PrintUnitValues.PointAtDrawing"/>).
    /// </summary>
    internal static class TitleBlockPropertySource
    {
        /// <summary>Returns how many text boxes were changed; failures are added to <paramref name="warnings"/> and never thrown.</summary>
        public static int PointAtDrawing(Sheet sheet, IReadOnlyCollection<string> propertyNames, List<string> warnings)
        {
            int changed = 0;
            TitleBlockDefinition? definition = ComSafe.Get(() => sheet.TitleBlock?.Definition, warnings, "The sheet's title block definition");
            if (definition == null)
            {
                warnings.Add("The sheet has no title block, so FINISH and WEIGHT were not pointed at the drawing.");
                return 0;
            }

            DrawingSketch? sketch = null;
            try
            {
                sketch = Edit(definition);
                foreach (TextBox box in sketch.TextBoxes)
                {
                    string? formatted = ComSafe.Get(() => box.FormattedText);
                    if (string.IsNullOrEmpty(formatted))
                        continue;

                    string updated = PrintUnitValues.PointAtDrawing(formatted, propertyNames);
                    if (string.Equals(updated, formatted, StringComparison.Ordinal))
                        continue;

                    TextBox target = box;
                    int before = warnings.Count;
                    ComSafe.Run(() => target.FormattedText = updated, warnings, "Pointing a title block field at the drawing");
                    if (warnings.Count == before)
                        changed++;
                }

                definition.ExitEdit(true);
            }
            catch (Exception ex)
            {
                warnings.Add($"Pointing FINISH and WEIGHT at the drawing failed. {ex.Message}");
                if (sketch != null)
                {
                    try { definition.ExitEdit(false); }
                    catch (Exception exitEx) { warnings.Add($"Leaving the title block editor failed. {exitEx.Message}"); }
                }
            }

            return changed;
        }

        // Edit takes the sketch by reference; the typed call first, then the by-ref IDispatch call the template builder falls back to.
        internal static DrawingSketch Edit(TitleBlockDefinition def)
        {
            try
            {
                DrawingSketch sketch;
                def.Edit(out sketch);
                return sketch;
            }
            catch (Exception)
            {
                object?[] args = { new DispatchWrapper(null) };
                var modifier = new ParameterModifier(1);
                modifier[0] = true;
                def.GetType().InvokeMember("Edit", BindingFlags.InvokeMethod, null, def, args, new[] { modifier }, null, null);
                return (DrawingSketch)args[0]!;
            }
        }
    }
}
