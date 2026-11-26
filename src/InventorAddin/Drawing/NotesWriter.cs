using System;
using System.Collections.Generic;
using Inventor;
using InventorAddin.Core.Drawing;
using InventorAddin.Core.Materials;
using InventorAddin.Core.Models;
using InventorAddin.Core.PartProperties;
using InventorAddin.Extraction;

// The Drawing folder holds the drawing-side helpers; the namespace names the feature, not the folder (Inventor has types named Drawing*).
namespace InventorAddin.PrintViews
{
    /// <summary>What <see cref="NotesWriter.Update"/> did: how many notes went where, and what to tell the user.</summary>
    internal sealed class NotesWriteResult
    {
        public int ListCount { get; set; }
        public int BottomCount { get; set; }

        /// <summary>Set when the workbook as a whole could not be read: nothing on the sheet was changed.</summary>
        public bool LeftAsTheyWere { get; set; }

        /// <summary>Whether the sheet's notes were replaced (false when the workbook could not be read or the old notes could not be removed).</summary>
        public bool Replaced { get; set; }

        /// <summary>Problems with the notes workbook itself, one line each.</summary>
        public List<string> WorkbookProblems { get; } = new List<string>();

        /// <summary>Anything else that could not be read or placed, one line each.</summary>
        public List<string> Warnings { get; } = new List<string>();

        public bool HasProblems => LeftAsTheyWere || WorkbookProblems.Count > 0 || Warnings.Count > 0;

        /// <summary>The problems as text for a message box: the workbook's, then the others, each under its own heading. Empty when there are none.</summary>
        public string ProblemsText()
        {
            var parts = new List<string>();
            if (LeftAsTheyWere)
                parts.Add(NotesMessages.LeftAsTheyWere);
            if (WorkbookProblems.Count > 0)
                parts.Add(NotesMessages.WorkbookProblems(WorkbookProblems));
            if (Warnings.Count > 0)
                parts.Add(NotesMessages.Warnings(Warnings));
            return string.Join(System.Environment.NewLine + System.Environment.NewLine, parts);
        }
    }

    /// <summary>
    /// Writes AWB's automatic notes on a drawing's first sheet (spec 05, "Added for M5", item 1; task 197): the numbered NOTES list above
    /// the title block and the bold bottom line. The notes come from Core (<see cref="NotesRules"/> reads the model's material, finish and
    /// Finish Note against the notes workbook; <see cref="NotesText"/> and <see cref="NotesPlacement"/> decide the text and the places);
    /// this class reads Inventor and applies the result. Each note carries the attribute set <see cref="NotesText.AttributeSetName"/>,
    /// so an update finds and replaces its own notes and leaves every hand-typed note alone. Calls are those of spike 184, question 2
    /// (<c>AddFitted</c>, <c>Width</c>, <c>AttributeSets.Add</c>, <c>AttributeManager.FindObjects</c>) and spike 197's probe (a note's
    /// <c>Position</c> is its top-left and can be set; <c>FindObjects</c> accepts <c>*</c> and works before a save).
    /// </summary>
    /// <remarks>Run it inside the caller's transaction, so one Undo reverts the update.</remarks>
    internal static class NotesWriter
    {
        // Inventor reports sheet coordinates in centimetres (spike 184); Core works in millimetres.
        private const double CmPerMm = 0.1;

        /// <summary>
        /// Reads the notes workbook, works out the notes for <paramref name="model"/> and writes them on the drawing's first sheet, replacing
        /// the notes AWB wrote there before. Does nothing to any other note.
        /// </summary>
        public static NotesWriteResult Update(DrawingDocument drawing, Document model)
        {
            var result = new NotesWriteResult();

            NotesWorkbookReadResult read = NotesWorkbookLoader.Load(AddinServices.Settings, AddinServices.Log);
            foreach (NotesWorkbookProblem problem in read.Problems)
                result.WorkbookProblems.Add(problem.ToString());

            // A workbook that cannot be read at all leaves the sheet as it is: replacing the notes from nothing would wipe the rule notes.
            if (read.CouldNotRead)
            {
                result.LeftAsTheyWere = true;
                AddinServices.Log.Warn("Update Notes: the notes workbook could not be read; the notes were left as they were.");
                return result;
            }

            var warnings = result.Warnings;
            string? material = ReadMaterial(model, warnings);
            string? finish = PropertyReader.GetString(model, PartPropertyStorage.Finish.SetName, PartPropertyStorage.Finish.PropertyName);
            string? finishNote = PropertyReader.GetString(model, PartPropertyStorage.FinishNote.SetName, PartPropertyStorage.FinishNote.PropertyName);
            NotesPlan plan = NotesRules.Resolve(read.Workbook, material, finish, finishNote);

            Sheet? sheet = ComSafe.Get(() => drawing.Sheets.Count > 0 ? drawing.Sheets[1] : null, warnings, "first sheet");
            if (sheet == null)
            {
                result.Warnings.Add(NotesMessages.NoSheet);
                return result;
            }

            // When the old notes cannot be found or deleted, nothing is added, so there is never a second NOTES list.
            bool removed = Remove(drawing, sheet, warnings);
            result.Replaced = removed;
            if (!removed)
                warnings.Add("The earlier notes could not be replaced, so no notes were added.");
            else
                Place(sheet, plan, result, warnings);

            foreach (string warning in warnings)
                AddinServices.Log.Warn($"Update Notes: {warning}");

            // Names and counts only: the notes' text is not logged.
            AddinServices.Log.Info(
                $"Update Notes: material '{material ?? string.Empty}', finish '{finish ?? string.Empty}'; " +
                $"{result.ListCount} list note(s) and {result.BottomCount} bottom note(s) on '{ComSafe.Get(() => sheet.Name)}'.");
            return result;
        }

        /// <summary>The model's material name: a part's active material, else the Material property (an assembly has none, so this is blank).</summary>
        private static string? ReadMaterial(Document model, List<string> warnings)
        {
            if (DocumentKinds.Of(model).IsPart())
            {
                string? name = ComSafe.Get(() => ((PartDocument)model).ActiveMaterial.DisplayName);
                if (!string.IsNullOrWhiteSpace(name))
                    return name;
            }

            return PropertyReader.GetString(model, PartPropertyStorage.Material.SetName, PartPropertyStorage.Material.PropertyName);
        }

        /// <summary>
        /// Deletes the AWB notes on <paramref name="sheet"/>: the notes found by their attribute that belong to this sheet. A note on another
        /// sheet, and every note without the attribute, stays. False when the notes could not be found or one could not be deleted.
        /// </summary>
        private static bool Remove(DrawingDocument drawing, Sheet sheet, List<string> warnings)
        {
            string? sheetName = ComSafe.Get(() => sheet.Name, warnings, "sheet name");
            ObjectCollection? found = ComSafe.Get(
                () => drawing.AttributeManager.FindObjects(NotesText.AttributeSetName, NotesText.KindAttributeName, "*"),
                warnings, "AWB notes");
            if (sheetName == null || found == null)
                return false;

            // Collected first: deleting while the collection is walked would change it.
            var mine = new List<GeneralNote>();
            foreach (object item in found)
            {
                if (item is GeneralNote note && ComSafe.Get(() => (note.Parent as Sheet)?.Name) == sheetName)
                    mine.Add(note);
            }

            bool all = true;
            foreach (GeneralNote note in mine)
            {
                try
                {
                    note.Delete();
                }
                catch (Exception ex)
                {
                    warnings.Add($"an old AWB note could not be deleted. {ex.Message}");
                    all = false;
            }
                }

            return all;
        }

        private static void Place(Sheet sheet, NotesPlan plan, NotesWriteResult result, List<string> warnings)
        {
            (SheetRect border, SheetRect? titleBlock) = SheetExtentsReader.ReadFrame(sheet);

            double listTopMm = titleBlock?.MaxYMm ?? border.MinYMm;
            string listText = NotesText.ListFormatted(plan.ListNotes);
            if (listText.Length > 0)
            {
                double? top = Add(sheet, listText, AwbNoteKind.List, NotesPlacement.List(border, titleBlock));
                if (top.HasValue)
                {
                    listTopMm = top.Value;
                    result.ListCount = plan.ListNotes.Count;
                }
            }

            string bottomText = NotesText.BottomFormatted(plan.BottomNotes);
            if (bottomText.Length > 0)
            {
                if (Add(sheet, bottomText, AwbNoteKind.Bottom, NotesPlacement.Bottom(border, titleBlock, listTopMm)).HasValue)
                    result.BottomCount = plan.BottomNotes.Count;
            }
        }

        /// <summary>
        /// Adds one note with its lower-left corner and width from <paramref name="box"/> and tags it. The note is added, measured, then moved
        /// so its bottom sits on the box's bottom (it grows upward). Returns the note's top in millimetres.
        /// </summary>
        private static double? Add(Sheet sheet, string formattedText, AwbNoteKind kind, NoteBox box)
        {
            TransientGeometry geometry = InventorHost.App.TransientGeometry;
            double left = box.LeftMm * CmPerMm;
            double bottom = box.BottomMm * CmPerMm;

            // The note's height is not known until it is wrapped to its width, so it goes in at the bottom first and is lifted by its height.
            GeneralNote note = sheet.DrawingNotes.GeneralNotes.AddFitted(geometry.CreatePoint2d(left, bottom), formattedText, Type.Missing);
            note.Width = box.WidthMm * CmPerMm;

            Box2d range = note.RangeBox;
            double height = range.MaxPoint.Y - range.MinPoint.Y;
            note.Position = geometry.CreatePoint2d(left, bottom + height);

            AttributeSet set = note.AttributeSets.Add(NotesText.AttributeSetName);
            set.Add(NotesText.KindAttributeName, ValueTypeEnum.kStringType, NotesText.KindValue(kind));

            return (bottom + height) / CmPerMm;
        }
    }
}
