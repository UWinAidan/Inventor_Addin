using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Inventor;
using InventorAddin.Core.Drawing;

namespace InventorAddin.PrintViews
{
    /// <summary>
    /// Points the title block fields for the custom Finish Spec and Mass at the drawing's own properties or at the model's (tasks 232 and
    /// 244, spec 05 question 33). Only the drawing's own copy of the title block definition is edited, so the template and other drawings
    /// are not touched. The text change is Core's (<see cref="PrintUnitValues"/>).
    /// </summary>
    internal static class TitleBlockPropertySource
    {
        /// <summary>What <see cref="Repoint"/> found and did: the managed fields on the title block, and how many text boxes it changed.</summary>
        internal readonly struct RepointResult
        {
            public RepointResult(int fields, int changed)
            {
                Fields = fields;
                Changed = changed;
            }

            public int Fields { get; }

            public int Changed { get; }
        }

        /// <summary>
        /// Points the fields in <paramref name="readsDrawing"/> at the drawing and those in <paramref name="readsModel"/> at the model.
        /// <paramref name="materialOnDrawing"/> does the same for MATERIAL, a Design Tracking field whose tag is swapped for a custom one
        /// (task 245); null leaves it alone.
        /// The title block is read first without editing it (spike 244: reading <c>Definition.Sketch</c> leaves the drawing clean), and
        /// the editor is entered only when a text box has to change, so a print that is already right is not marked dirty. A drawing whose
        /// title block has none of these fields reports 0 fields and is not edited. Failures are added to <paramref name="warnings"/> and never thrown.
        /// </summary>
        public static RepointResult Repoint(
            Sheet sheet, IReadOnlyCollection<string> readsDrawing, IReadOnlyCollection<string> readsModel, bool? materialOnDrawing, List<string> warnings)
        {
            TitleBlockDefinition? definition = ComSafe.Get(() => sheet.TitleBlock?.Definition, warnings, "The sheet's title block definition");
            if (definition == null)
                return new RepointResult(0, 0);

            var all = readsDrawing.Concat(readsModel).ToList();
            int fields = 0;
            bool needsEdit = false;
            try
            {
                DrawingSketch? sketch = ComSafe.Get(() => definition.Sketch, warnings, "The title block's sketch");
                if (sketch == null)
                    return new RepointResult(0, 0);

                foreach (TextBox box in sketch.TextBoxes)
                {
                    string? formatted = ComSafe.Get(() => box.FormattedText);
                    if (string.IsNullOrEmpty(formatted))
                        continue;

                    fields += PrintUnitValues.CountFields(formatted, all);
                    if (AssemblyDashFields.HasMaterialField(formatted))
                        fields++;
                    if (!string.Equals(Updated(formatted, readsDrawing, readsModel, materialOnDrawing), formatted, StringComparison.Ordinal))
                        needsEdit = true;
                }
            }
            catch (Exception ex)
            {
                warnings.Add($"Reading the title block's FINISH and WEIGHT fields failed. {ex.Message}");
                return new RepointResult(fields, 0);
            }

            if (!needsEdit)
                return new RepointResult(fields, 0);

            int changed = 0;
            DrawingSketch? editing = null;
            try
            {
                editing = Edit(definition);
                foreach (TextBox box in editing.TextBoxes)
                {
                    string? formatted = ComSafe.Get(() => box.FormattedText);
                    if (string.IsNullOrEmpty(formatted))
                        continue;

                    string updated = Updated(formatted, readsDrawing, readsModel, materialOnDrawing);
                    if (string.Equals(updated, formatted, StringComparison.Ordinal))
                        continue;

                    TextBox target = box;
                    int before = warnings.Count;
                    ComSafe.Run(() => target.FormattedText = updated, warnings, "Pointing a title block field at its values");
                    if (warnings.Count == before)
                        changed++;
                }

                definition.ExitEdit(true);
            }
            catch (Exception ex)
            {
                warnings.Add($"Pointing FINISH and WEIGHT at their values failed. {ex.Message}");
                if (editing != null)
                {
                    try { definition.ExitEdit(false); }
                    catch (Exception exitEx) { warnings.Add($"Leaving the title block editor failed. {exitEx.Message}"); }
                }
            }

            return new RepointResult(fields, changed);
        }

        /// <summary>How many fields reading <paramref name="names"/> the sheet's title block has, whichever document they read. Read-only: never enters the editor.</summary>
        public static int CountFields(Sheet sheet, IReadOnlyCollection<string> names, List<string> warnings)
        {
            TitleBlockDefinition? definition = ComSafe.Get(() => sheet.TitleBlock?.Definition, warnings, "The sheet's title block definition");
            DrawingSketch? sketch = definition == null ? null : ComSafe.Get(() => definition.Sketch, warnings, "The title block's sketch");
            if (sketch == null)
                return 0;

            int fields = 0;
            foreach (TextBox box in sketch.TextBoxes)
                fields += PrintUnitValues.CountFields(ComSafe.Get(() => box.FormattedText), names);
            return fields;
        }

        private static string Updated(
            string formatted, IReadOnlyCollection<string> readsDrawing, IReadOnlyCollection<string> readsModel, bool? materialOnDrawing)
        {
            string text = readsDrawing.Count == 0 ? formatted : PrintUnitValues.PointAtDrawing(formatted, readsDrawing);
            text = readsModel.Count == 0 ? text : PrintUnitValues.PointAtModel(text, readsModel);
            return materialOnDrawing is { } onDrawing ? AssemblyDashFields.PointMaterial(text, onDrawing) : text;
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
