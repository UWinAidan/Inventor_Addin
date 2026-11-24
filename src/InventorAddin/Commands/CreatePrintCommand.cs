using System;
using System.Collections.Generic;
using System.Linq;
using Inventor;
using InventorAddin.Core.Drawing;
using InventorAddin.Core.Models;
using InventorAddin.Core.Materials;
using InventorAddin.Core.PartProperties;
using InventorAddin.Core.Ribbon;
using InventorAddin.Core.Settings;
using InventorAddin.Core.ViewModels;
using InventorAddin.Extraction;
using InventorAddin.PrintViews;
using InventorAddin.UI;
using Forms = System.Windows.Forms;
using IODirectory = System.IO.Directory;
using IOFile = System.IO.File;
using SysEnvironment = System.Environment;

namespace InventorAddin.Commands
{
    /// <summary>
    /// Create Print (spec 02, "Commands: Create Print"): makes an <c>.idw</c> drawing of the part or assembly being edited (also one edited in place) from
    /// the AWB print template (<see cref="LibraryPaths.PrintTemplate"/>), on its A3 landscape (metric) or ANSI B landscape
    /// (imperial) sheet format (<see cref="PrintPlan.SheetFormatName"/>), with the initial views the user confirmed in the Initial Views window first
    /// (<see cref="InitialViewsPlan"/>: a part gets front, top, right and iso), saves it
    /// under the model's name in the <c>Prints</c> subfolder of the model's folder (created when missing), and leaves it
    /// open. The file name and the messages are decided in <see cref="PrintPlan"/> and the views, their positions and the scale in
    /// <see cref="InitialViewsPlan"/> (both Core); this class reads Inventor and applies the result, by the calls spike 032
    /// confirmed.
    /// </summary>
    /// <remarks>
    /// Not a <see cref="DocumentEditCommand"/>: it never changes the model. It creates and saves a new file, so, like
    /// New Part, its command type is <c>kFileOperationsCmdType</c>.
    /// </remarks>
    public sealed class CreatePrintCommand : AddinCommand
    {
        public override string InternalName => CommandNames.CreatePrint;
        public override string DisplayName => "Create Print";
        public override string Description =>
            "Create a drawing of this part or assembly with the initial views you choose, saved in its folder's Prints folder.";

        protected override CommandTypesEnum CommandType => CommandTypesEnum.kFileOperationsCmdType;

        protected override void Execute()
        {
            // The document being edited (CommandDocuments, task 109): while a part or sub-assembly is edited in place,
            // its print is made, not the assembly's.
            Document? model = CommandTargetDocument();
            if (model == null)
            {
                Show("Open a part or assembly first.", Forms.MessageBoxIcon.Information);
                return;
            }

            string? problem = ModelProblem(model, PrintPlan.WrongDocumentMessage, PrintPlan.UnsavedMessage, out DocumentKind kind, out string modelPath);
            if (problem != null)
            {
                Show(problem, Forms.MessageBoxIcon.Information);
                return;
            }

            // The print starts in the part's units (task 232); the Initial Views window can change them.
            PrintUnits partUnits = MaterialFinishTargets.UnitsOf(model);
            string template = TemplateFor(partUnits);

            // An existing print, with either drawing extension, in Prints or (made before 2026-10-11) beside the model,
            // is opened instead of making a new one, with no question (task 235); an .idw is preferred when both exist.
            string? existing = PrintPlan.FindExistingPrint(modelPath, PrintPlan.NewPrintExtension, IOFile.Exists);
            if (existing != null)
            {
                AddinServices.Log.Info($"Create Print: a print already exists at '{existing}'; opening it.");
                OpenPrint(existing);
                return;
            }

            if (!IOFile.Exists(template))
            {
                string message = PrintPlan.MissingTemplateMessage(template);
                AddinServices.Log.Warn($"Create Print stopped: {message}");
                Show(message, Forms.MessageBoxIcon.Warning);
                return;
            }

            // The range box (X across, Y up, Z deep: spike 032) and, for sheet metal, the flat pattern's size.
            InitialViewsModelInfo? info = InitialViewsPlacer.Read(model, kind);
            if (info == null)
            {
                AddinServices.Log.Warn($"Create Print stopped: no geometry to show in '{modelPath}'.");
                Show(PrintPlan.NoGeometryMessage, Forms.MessageBoxIcon.Warning);
                return;
            }

            // The user confirms or changes the views, the units and the sheet before anything is made (tasks 192, 232); Cancel creates
            // nothing. The real size of each view is read from scratch views made from the chosen units' template (task 227), once per
            // units, so the suggested scale and sheet match what is placed.
            var viewModel = new InitialViewsViewModel(
                info.Kind, info.Box, info.FlatPattern, partUnits,
                (u, front) =>
                {
                    string t = TemplateFor(u);
                    return IOFile.Exists(t) ? InitialViewsPlacer.MeasureWithWaitCursor(model, info, t, u, front) : null;
                });
            InitialViewsResult? plan = InitialViewsWindow.Ask(viewModel);
            if (plan == null)
            {
                AddinServices.Log.Info("Create Print: cancelled in the Initial Views window; nothing was created.");
                return;
            }

            PrintUnits printUnits = viewModel.ChosenUnits;
            SheetSizeSpec sheetSpec = viewModel.ChosenSheet;
            if (printUnits != partUnits)
            {
                template = TemplateFor(printUnits);
                if (!IOFile.Exists(template))
                {
                    string message = PrintPlan.MissingTemplateMessage(template);
                    AddinServices.Log.Warn($"Create Print stopped: {message}");
                    Show(message, Forms.MessageBoxIcon.Warning);
                    return;
                }
            }

            string printsFolder = PrintPlan.PrintsFolder(modelPath);
            string drawingPath = PrintPlan.DrawingFileName(modelPath, PrintPlan.NewPrintExtension);
            string? partNumber = ModelPartNumber(model);

            BackfillPrintProperties(model);

            Forms.Cursor previousCursor = Forms.Cursor.Current;
            Forms.Cursor.Current = Forms.Cursors.WaitCursor;
            try
            {
                Create(model, kind, template, sheetSpec.FormatName, printUnits, printsFolder, drawingPath, plan, partNumber);
            }
            finally
            {
                Forms.Cursor.Current = previousCursor;
            }
        }

        /// <summary>The AWB print template of <paramref name="units"/>, from Settings or the starter folder.</summary>
        private static string TemplateFor(PrintUnits units) =>
            LibraryPaths.PrintTemplate(AddinServices.Settings, units, AddinServices.AddinFolder).Path;

        /// <summary>
        /// The checks Create Print and Open Drawing share (task 193): the model must be a part or assembly (else
        /// <paramref name="wrongDocument"/>) and saved (else <paramref name="unsaved"/>). Returns the message to show, or null
        /// when the model is fine, in which case <paramref name="kind"/> and <paramref name="modelPath"/> are set.
        /// </summary>
        internal static string? ModelProblem(
            Document model, string wrongDocument, string unsaved, out DocumentKind kind, out string modelPath)
        {
            kind = DocumentKinds.Of(model);
            modelPath = string.Empty;
            if (!kind.IsPart() && !kind.IsAssembly())
                return wrongDocument;

            modelPath = ComSafe.Get(() => model.FullFileName) ?? string.Empty;
            return string.IsNullOrWhiteSpace(modelPath) ? unsaved : null;
        }

        /// <summary>
        /// Create Print on the document being edited, run by Open Drawing when the model has no print (task 193). The same
        /// code as the button, so nothing is duplicated.
        /// </summary>
        internal void RunFromOpenDrawing() => Execute();

        /// <summary>
        /// Creates, fills and saves the drawing. Any failure closes the half-made drawing without saving and says which
        /// step failed. The <paramref name="printsFolder"/> is created, when missing, just before saving.
        /// </summary>
        private void Create(
            Document model, DocumentKind kind, string template, string sheetFormatName, PrintUnits printUnits, string printsFolder,
            string drawingPath, InitialViewsResult plan, string? partNumber)
        {
            string step = "creating the drawing from the template";
            Document? drawing = null;
            try
            {
                // Visible, so the new drawing opens and becomes the active document.
                // SilentOperation around the add only (spike 225): a "Style Conflict" dialog can otherwise stop it.
                bool previousSilent = InventorHost.App.SilentOperation;
                InventorHost.App.SilentOperation = true;
                try
                {
                    drawing = InventorHost.App.Documents.Add(DocumentTypeEnum.kDrawingDocumentObject, template, true);
                }
                finally
                {
                    InventorHost.App.SilentOperation = previousSilent;
                }

                // The template's own first sheet is replaced by a sheet made from the units' format, as spike 183
                // (round 2, B10) showed: the format brings the border, the title block and the triad. The model is
                // not passed: spike 183 found AddUsingSheetFormat refuses it.
                step = $"adding the sheet from the sheet format '{sheetFormatName}'";
                var drawingDocument = (DrawingDocument)drawing;
                Sheet sheet = AddSheetFromFormat(drawingDocument, sheetFormatName, template);

                // The title block's weight reads "N/A" until the model's mass has been computed; reading it before the
                // views go on, then updating the drawing, makes it print (spike 183, round 3).
                step = "reading the model's mass";
                InitialViewsPlacer.ReadMass(model, kind);

                // The views, their positions and the scale come from the confirmed plan (task 192): the scale rule is now Initial Views'
                // (the share of the usable area, task 189), not the share of the whole sheet.
                step = "adding the views";
                Document drawingDoc = drawing;
                IReadOnlyList<string> placed = Array.Empty<string>();
                IReadOnlyList<PropertyWrite> unitWrites = Array.Empty<PropertyWrite>();
                var unitProblems = new List<string>();
                RunInTransaction(drawingDoc, () =>
                {
                    placed = InitialViewsPlacer.Place(sheet, model, plan);

                    // FINISH and WEIGHT follow the print's units (task 232): when they differ from the model's, the print-units values go on the
                    // drawing itself, made by the same Core formatting as the model's own.
                    // Best effort: a refusal is logged and reported, and the views still go on.
                    try
                    {
                        unitWrites = FinishSpecBackfiller.PrintUnitWritesFor(model, printUnits, FinishSpecBackfiller.LoadFinishList());
                        if (unitWrites.Count > 0)
                            PropertyWriter.Apply(drawingDoc, unitWrites);
                    }
                    catch (Exception ex)
                    {
                        unitWrites = Array.Empty<PropertyWrite>();
                        unitProblems.Add("the Finish Spec and Mass could not be written on the drawing");
                        AddinServices.Log.Warn($"Create Print: writing the Finish Spec and Mass in the print's units failed; the views were placed anyway. {ex.Message}");
                    }

                    if (partNumber != null)
                    {
                        step = "writing the Part Number";
                        PropertyWriter.Apply(drawingDoc, new[] { PropertyWrite.SetText(PartPropertyStorage.PartNumber, partNumber) });
                    }
                });

                if (unitWrites.Count > 0)
                {
                    step = "pointing FINISH and WEIGHT at the drawing's own values";
                    var fieldWarnings = new List<string>();
                    int changed = 0;
                    try
                    {
                        changed = TitleBlockPropertySource.PointAtDrawing(
                            sheet, unitWrites.Select(w => w.Location.PropertyName).ToList(), fieldWarnings);
                    }
                    catch (Exception ex)
                    {
                        fieldWarnings.Add(ex.Message);
                    }

                    if (fieldWarnings.Count > 0 || changed < unitWrites.Count)
                        unitProblems.Add("FINISH and WEIGHT could not all be pointed at the drawing's own values");
                    AddinServices.Log.Info(
                        $"Create Print: the print is in {printUnits} units, the model is not; {changed} title block field(s) now read the drawing's {string.Join(", ", unitWrites.Select(w => w.Location.PropertyName))}." +
                        (fieldWarnings.Count == 0 ? string.Empty : " Problems: " + string.Join(" ", fieldWarnings)));
                }

                // Long title block values are narrowed, shrunk or (Description) wrapped to fit their cells (task 238). Best effort: a
                // failure is logged and the print goes on. After the repointing above, so the values read are the ones that print.
                // Updated first, so the values read are current (WEIGHT reads "N/A" until then, and so does the repointed FINISH).
                step = "fitting long title block values";
                ComSafe.Run(() => drawingDocument.Update());
                var fitWarnings = new List<string>();
                int fitted = 0;
                try
                {
                    RunInTransaction(drawingDoc, () => fitted = TitleBlockFitter.Apply(drawingDocument, fitWarnings));
                }
                catch (Exception ex)
                {
                    fitWarnings.Add(ex.Message);
                }

                AddinServices.Log.Info(
                    $"Create Print: {fitted} title block field(s) fitted to their cells." +
                    (fitWarnings.Count == 0 ? string.Empty : " Warnings: " + string.Join(" ", fitWarnings)));

                step = "updating the drawing";
                ComSafe.Run(() => drawingDocument.Update());

                // Does nothing when the folder already exists.
                step = $"creating the folder '{printsFolder}'";
                IODirectory.CreateDirectory(printsFolder);

                step = $"saving it as '{drawingPath}'";
                drawing.SaveAs(drawingPath, false);

                if (fitWarnings.Count > 0)
                {
                    Show(
                        "The print was made, but some title block values do not fit their cells even at the smallest text, or could not be fitted:\n\n"
                        + string.Join("\n", fitWarnings.Select(w => "- " + w)),
                        Forms.MessageBoxIcon.Warning);
                }

                if (unitProblems.Count > 0)
                {
                    Show(
                        $"The print was made, but {string.Join(" and ", unitProblems)}, so FINISH and WEIGHT may show the part's units. See the log.",
                        Forms.MessageBoxIcon.Warning);
                }

                AddinServices.Log.Info(
                    $"Create Print: '{drawingPath}' created from '{template}' (sheet format '{sheetFormatName}') with {placed.Count} view(s) ({string.Join("; ", placed)}) at scale {plan.Scale} " +
                    (partNumber != null ? $"and Part Number {partNumber}." : "and no Part Number (the model has no number)."));
            }
            catch (Exception ex)
            {
                AddinServices.Log.Error($"Create Print failed while {step}.", ex);

                PrintCleanup cleanup = PrintCleanup.NoDrawing;
                if (drawing != null)
                {
                    try
                    {
                        // Close(true) skips saving (spike 032).
                        drawing.Close(true);
                        cleanup = PrintCleanup.Closed;
                    }
                    catch (Exception closeEx)
                    {
                        cleanup = PrintCleanup.CloseFailed;
                        AddinServices.Log.Error("Create Print: closing the half-made drawing failed.", closeEx);
                    }
                }

                Show(PrintPlan.FailedMessage(step, ex.Message, cleanup), Forms.MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Adds a sheet from the drawing's sheet format called <paramref name="formatName"/>, makes it the active sheet, and
        /// deletes the sheets that were there (the template's own first sheet). Throws when the template has no such format.
        /// </summary>
        private static Sheet AddSheetFromFormat(DrawingDocument drawing, string formatName, string templatePath)
        {
            SheetFormat? format = drawing.SheetFormats.Cast<SheetFormat>().FirstOrDefault(f =>
                string.Equals(ComSafe.Get(() => f.Name), formatName, StringComparison.Ordinal));
            if (format == null)
                throw new InvalidOperationException(PrintPlan.MissingSheetFormatMessage(formatName, templatePath));

            List<Sheet> original = drawing.Sheets.Cast<Sheet>().ToList();
            Sheet added = drawing.Sheets.AddUsingSheetFormat(format, Type.Missing, "Sheet", Type.Missing, Type.Missing, Type.Missing);
            added.Activate();
            foreach (Sheet old in original)
                old.Delete();
            return added;
        }

        /// <summary>
        /// Fills in a missing Finish Spec, Revision and Status, and replaces a Windows-login Designer and Author (task 236), on the model before the drawing is made, so the title block
        /// prints them (tasks 217 and 219). One transaction on the model, nothing saved. A model that cannot be changed
        /// (read-only, say) is logged and does not stop the print.
        /// </summary>
        private void BackfillPrintProperties(Document model)
        {
            try
            {
                var writes = new List<PropertyWrite>(FinishSpecBackfiller.WritesFor(model, FinishSpecBackfiller.LoadFinishList()));

                // A Designer that is the Windows login becomes the Settings name; any other name stays (task 236).
                writes.AddRange(DesignerBackfill.For(
                    PropertyReader.GetString(model, PartPropertyStorage.Designer.SetName, PartPropertyStorage.Designer.PropertyName),
                    PropertyReader.GetString(model, PartPropertyStorage.Author.SetName, PartPropertyStorage.Author.PropertyName),
                    SysEnvironment.UserName,
                    AddinServices.Settings.DefaultDesigner));
                if (writes.Count == 0)
                    return;

                RunInTransaction(model, () => PropertyWriter.Apply(model, writes));

                // Names only: a value is not logged.
                AddinServices.Log.Info($"Create Print: {PrintPropertyBackfill.Describe(writes)} on '{ComSafe.Get(() => model.DisplayName)}'.");
            }
            catch (Exception ex)
            {
                AddinServices.Log.Warn($"Create Print: Finish Spec, Revision, Status and Designer could not be filled in on the model. {ex.Message}");
            }
        }

        /// <summary>Runs <paramref name="edit"/> in one transaction on the new drawing, aborted if it throws.</summary>
        private void RunInTransaction(Document doc, Action edit)
        {
            Transaction transaction = InventorHost.App.TransactionManager.StartTransaction((_Document)doc, DisplayName);
            try
            {
                edit();
                transaction.End();
            }
            catch (Exception)
            {
                try
                {
                    transaction.Abort();
                }
                catch (Exception abortEx)
                {
                    AddinServices.Log.Error("Create Print: aborting the transaction failed.", abortEx);
                }

                throw;
            }
        }

        /// <summary>
        /// The model's Part Number in the standard form, only when the register holds it; never Inventor's file-name
        /// fallback (spike 032), which does not parse as a number.
        /// </summary>
        private static string? ModelPartNumber(Document model)
        {
            string? value = PropertyReader.GetString(
                model, PartPropertyStorage.PartNumber.SetName, PartPropertyStorage.PartNumber.PropertyName);
            if (!AddinServices.RegisterStore.HasNumber(value))
                return null;
            return Core.Numbering.PartNumber.Parse(value!).ToString();
        }

        /// <summary>Opens the drawing visibly, or finds it when already open, and makes it the active document (shared with Open Drawing, task 193).</summary>
        internal static void OpenPrint(string drawingPath)
        {
            Document opened = InventorHost.App.Documents.Open(drawingPath, true);
            // If the drawing was already open, Open returns it; make it the active document either way.
            ComSafe.Run(() => opened.Activate());
        }

        private void Show(string text, Forms.MessageBoxIcon icon) =>
            Forms.MessageBox.Show(text, DisplayName, Forms.MessageBoxButtons.OK, icon);
    }
}
