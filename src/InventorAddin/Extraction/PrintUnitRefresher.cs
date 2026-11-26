using System;
using System.Collections.Generic;
using System.Linq;
using Inventor;
using InventorAddin.Commands;
using InventorAddin.Core.Drawing;
using InventorAddin.Core.Models;
using InventorAddin.Core.PartProperties;
using InventorAddin.PrintViews;

namespace InventorAddin.Extraction
{
    /// <summary>What <see cref="PrintUnitRefresher.Refresh"/> did, for the caller's log and messages.</summary>
    internal sealed class PrintUnitRefresh
    {
        /// <summary>False when the drawing was left alone: no model, or no AWB FINISH and WEIGHT fields on its title block.</summary>
        public bool Applicable { get; init; }

        public PrintUnits PrintUnits { get; init; }

        public PrintUnits ModelUnits { get; init; }

        /// <summary>The drawing properties that were written (names only).</summary>
        public IReadOnlyList<string> Written { get; init; } = Array.Empty<string>();

        /// <summary>Title block text boxes whose field was repointed.</summary>
        public int FieldsChanged { get; init; }

        /// <summary>The names of the fields that now read the drawing's own values.</summary>
        public IReadOnlyList<string> ReadsDrawing { get; init; } = Array.Empty<string>();

        public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();

        /// <summary>True when something was written or repointed.</summary>
        public bool Changed => Written.Count > 0 || FieldsChanged > 0;
    }

    /// <summary>
    /// Keeps a print's FINISH and WEIGHT right for the print's units (task 244). The values and which fields read the drawing or the model are
    /// Core's decision (<see cref="PrintUnitValues.PlanFor"/>); this reads Inventor, applies the plan, and changes nothing that is already right,
    /// so an up-to-date print is not marked dirty. Used by Create Print and, through <see cref="PrintUnitEvents"/>, on opening and saving a drawing.
    /// </summary>
    internal static class PrintUnitRefresher
    {
        /// <summary>
        /// Refreshes <paramref name="drawing"/> from <paramref name="model"/> (the first part or assembly the drawing references when null).
        /// <paramref name="printUnits"/> overrides the drawing's own length units (Create Print passes the units the user chose). Writes are
        /// made outside a transaction. Failures are collected in <see cref="PrintUnitRefresh.Warnings"/>; only a bad argument throws.
        /// </summary>
        public static PrintUnitRefresh Refresh(DrawingDocument drawing, Document? model, PrintUnits? printUnits)
        {
            ArgumentNullException.ThrowIfNull(drawing);
            // DrawingDocument does not convert to Document implicitly.
            Document drawingDoc = (Document)(object)drawing;
            var warnings = new List<string>();
            var notApplicable = new PrintUnitRefresh { Applicable = false, Warnings = warnings };

            // Only drawings with the AWB fields are touched. Read-only: this never enters the title block editor.
            int fields = 0;
            foreach (Sheet sheet in drawing.Sheets)
                fields += TitleBlockPropertySource.CountFields(sheet, PrintUnitValues.ManagedNames, warnings);
            if (fields == 0)
                return notApplicable;

            model ??= FindModel(drawing);
            if (model == null)
            {
                warnings.Add("The drawing references no part or assembly, so FINISH and WEIGHT were left as they are.");
                return notApplicable;
            }

            PrintUnits print = printUnits ?? MaterialFinishTargets.UnitsOf(drawingDoc);
            PrintUnits part = MaterialFinishTargets.UnitsOf(model);
            bool differs = PrintUnitValues.Differs(part, print);
            DocumentKind kind = ComSafe.Get(() => DocumentKinds.Of(model));

            // The mass, the finish and the finish list are read only when the units differ; a print in the part's own units needs none of them.
            PrintUnitValues.Plan unitsPlan = PrintUnitValues.PlanFor(
                part,
                print,
                kind.IsAssembly(),
                differs ? MassPropertyReader.MassKg(model) : null,
                differs ? Read(model, PartPropertyStorage.Finish) : null,
                differs ? Read(model, PartPropertyStorage.FinishThickness) : null,
                differs ? Read(model, PartPropertyStorage.FinishSpec) : null,
                differs ? FinishSpecBackfiller.LoadFinishList() : Array.Empty<Core.Materials.Finish>(),
                Read(drawingDoc, PartPropertyStorage.Mass),
                Read(drawingDoc, PartPropertyStorage.FinishSpec));

            // An assembly has no material, material size or finish: where its value is blank the print reads a dash held on the drawing
            // (task 245). The model is never written. A part keeps the units plan as it is.
            AssemblyDashFields.Plan? dash = kind.IsAssembly()
                ? AssemblyDashFields.PlanFor(
                    Read(model, PartPropertyStorage.Material),
                    Read(model, PartPropertyStorage.MaterialSize),
                    Read(model, PartPropertyStorage.FinishSpec),
                    Read(drawingDoc, AssemblyDashFields.DrawingMaterial),
                    Read(drawingDoc, PartPropertyStorage.MaterialSize),
                    Read(drawingDoc, PartPropertyStorage.FinishSpec))
                : null;
            PrintUnitValues.Plan plan = AssemblyDashFields.Combine(unitsPlan, dash);
            bool? materialOnDrawing = dash?.MaterialReadsDrawing;
            var readsDrawing = materialOnDrawing == true ? plan.ReadsDrawing.Append("Material").ToList() : plan.ReadsDrawing.ToList();

            var written = new List<string>();
            if (plan.Writes.Count > 0)
            {
                try
                {
                    PropertyWriter.Apply(drawingDoc, plan.Writes);
                    written.AddRange(plan.Writes.Select(w => w.Location.PropertyName));
                }
                catch (Exception ex)
                {
                    warnings.Add("the Finish Spec and Mass could not be written on the drawing. " + ex.Message);
                }
            }

            int changed = 0;
            foreach (Sheet sheet in drawing.Sheets)
                changed += TitleBlockPropertySource.Repoint(sheet, plan.ReadsDrawing, plan.ReadsModel, materialOnDrawing, warnings).Changed;

            if (written.Count > 0 || changed > 0)
                ComSafe.Run(() => drawing.Update());

            return new PrintUnitRefresh
            {
                Applicable = true,
                PrintUnits = print,
                ModelUnits = part,
                Written = written,
                FieldsChanged = changed,
                ReadsDrawing = readsDrawing,
                Warnings = warnings,
            };
        }

        /// <summary>The first part or assembly the drawing references (as Place Initial Views finds it), or null.</summary>
        private static Document? FindModel(DrawingDocument drawing)
        {
            foreach (Document referenced in drawing.ReferencedDocuments)
            {
                DocumentKind kind = ComSafe.Get(() => DocumentKinds.Of(referenced));
                if (kind.IsPart() || kind.IsAssembly())
                    return referenced;
            }

            return null;
        }

        private static string? Read(Document doc, PropertyLocation location) =>
            PropertyReader.GetString(doc, location.SetName, location.PropertyName);
    }
}
