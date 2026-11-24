using System;
using System.Collections.Generic;
using System.Linq;
using Inventor;
using InventorAddin.Core.Drawing;
using InventorAddin.Core.Materials;
using InventorAddin.Core.Models;
using InventorAddin.Core.Settings;
using InventorAddin.Core.ViewModels;
using InventorAddin.Extraction;
using InventorAddin.PrintViews;
using InventorAddin.UI;
using Forms = System.Windows.Forms;

namespace InventorAddin.Commands
{
    /// <summary>
    /// Place Initial Views (spec 05, "Decided for M5: Place Initial Views"; task 192): puts the standard views of the drawing's model on the
    /// active sheet, after the Initial Views window lets the user confirm or change them. The model is the first part or assembly the drawing
    /// references; a drawing with none asks for a file. The plan is <see cref="InitialViewsPlan"/>'s (Core), the placing is
    /// <see cref="InitialViewsPlacer"/>'s. The whole placing is one transaction, so one Undo removes the views.
    /// </summary>
    /// <remarks>
    /// Adds drawing views, which are not geometry, so its command type is <c>kNonShapeEditCmdType</c>. The window opens in
    /// <see cref="GatherInput"/>, outside the transaction.
    /// </remarks>
    public sealed class PlaceInitialViewsCommand : DocumentEditCommand
    {
        /// <summary>Shown when the active document is not a drawing.</summary>
        public const string WrongDocumentText = "Open a drawing first. Place Initial Views works on a drawing.";

        /// <summary>Asked when the active sheet already has views.</summary>
        public const string SheetHasViewsQuestion = "This sheet already has views. Place the initial views as well?";

        public override string InternalName => Core.Ribbon.CommandNames.PlaceInitialViews;
        public override string DisplayName => "Place Initial Views";
        public override string Description =>
            "Place the standard views of the drawing's part or assembly on the active sheet.";

        protected override CommandTypesEnum CommandType => CommandTypesEnum.kNonShapeEditCmdType;

        protected override bool CanRunOn(DocumentKind kind) => kind == DocumentKind.Drawing;

        protected override string WrongDocumentMessage(DocumentKind kind) => WrongDocumentText;

        // Read while the window is open and used by Execute.
        private Document? _model;
        private DocumentKind _modelKind;
        private InitialViewsResult? _plan;

        // The model the command opened itself (from the file picker); closed again when nothing is placed. Never a document the user had open.
        private Document? _openedByUs;

        // Set by Execute: values too long for their cells at the smallest text, shown once the command has finished.
        private List<string> _fitWarnings = new List<string>();

        protected override bool GatherInput(Document doc)
        {
            _model = null;
            _plan = null;
            _openedByUs = null;

            var drawing = (DrawingDocument)doc;
            Sheet? sheet = ComSafe.Get(() => drawing.ActiveSheet);
            if (sheet == null)
            {
                Forms.MessageBox.Show("The drawing has no active sheet.", DisplayName, Forms.MessageBoxButtons.OK, Forms.MessageBoxIcon.Information);
                return false;
            }

            if ((ComSafe.Get(() => (int?)sheet.DrawingViews.Count) ?? 0) > 0)
            {
                Forms.DialogResult answer = Forms.MessageBox.Show(
                    SheetHasViewsQuestion, DisplayName, Forms.MessageBoxButtons.YesNo, Forms.MessageBoxIcon.Question);
                if (answer != Forms.DialogResult.Yes)
                    return false;
            }

            Document? model = FindModel(drawing) ?? AskForModel();
            if (model == null)
                return false;

            try
            {
                return Gather(model, sheet);
            }
            catch (Exception)
            {
                CloseOpenedByUs();
                throw;
            }
        }

        private bool Gather(Document model, Sheet sheet)
        {
            DocumentKind kind = DocumentKinds.Of(model);
            InitialViewsModelInfo? info = InitialViewsPlacer.Read(model, kind);
            if (info == null)
            {
                AddinServices.Log.Warn($"Place Initial Views stopped: no geometry to show in '{ComSafe.Get(() => model.FullFileName)}'.");
                Forms.MessageBox.Show(PrintPlan.NoGeometryMessage, DisplayName, Forms.MessageBoxButtons.OK, Forms.MessageBoxIcon.Warning);
                CloseOpenedByUs();
                return false;
            }

            // The views go on the active sheet, laid out on its own size: a template sheet is matched by size, any other sheet gets the
            // border and title block band of the default units.
            PrintUnits units = PrintPlan.PrintUnitsFor(AddinServices.Settings.DefaultUnits);
            SheetSizeSpec spec = InitialViewsSetup.SheetFor(sheet.Width, sheet.Height, units);

            // The real size of each view is read from scratch views made from the AWB template (task 227); without the template the
            // range box estimate is used.
            // The sheet is given, so no units or sheet choice is offered here (task 232, Default); the template of the sheet's units measures.
            string template = LibraryPaths.PrintTemplate(AddinServices.Settings, spec.Units, AddinServices.AddinFolder).Path;
            // The measuring follows the Front view choice (task 233), so the window asks for it, once per choice.
            bool haveTemplate = System.IO.File.Exists(template);
            var viewModel = new InitialViewsViewModel(info.Kind, info.Box, info.FlatPattern, spec, null,
                front => haveTemplate ? InitialViewsPlacer.MeasureWithWaitCursor(model, info, template, spec.Units, front) : null);
            _plan = InitialViewsWindow.Ask(viewModel);
            if (_plan == null)
            {
                AddinServices.Log.Info("Place Initial Views: cancelled in the Initial Views window; nothing was placed.");
                CloseOpenedByUs();
                return false;
            }

            _model = model;
            _modelKind = kind;
            return true;
        }

        /// <summary>
        /// Runs the command; on any failure, closes a model this command opened. The close comes after the base class has aborted the
        /// transaction, so no placed view still references the model.
        /// </summary>
        protected override void Run(Document doc)
        {
            _fitWarnings = new List<string>();
            try
            {
                base.Run(doc);
                if (_fitWarnings.Count > 0)
                {
                    Forms.MessageBox.Show(
                        "The views were placed, but some title block values do not fit their cells even at the smallest text, or could not be fitted:\n\n"
                        + string.Join("\n", _fitWarnings.Select(w => "- " + w)),
                        DisplayName, Forms.MessageBoxButtons.OK, Forms.MessageBoxIcon.Warning);
                }
            }
            catch (Exception)
            {
                CloseOpenedByUs();
                throw;
            }
        }

        protected override void Execute(Document doc)
        {
            if (_model == null || _plan == null)
                return;

            var drawing = (DrawingDocument)doc;
            Sheet sheet = drawing.ActiveSheet;

            // The title block's weight field shows "N/A" until the model's mass has been computed (spike 183, round 3).
            InitialViewsPlacer.ReadMass(_model, _modelKind);

            IReadOnlyList<string> placed = InitialViewsPlacer.Place(sheet, _model, _plan);
            ComSafe.Run(() => drawing.Update());

            // Long title block values are fitted to their cells (task 238); best effort, inside this command's transaction.
            _fitWarnings = new List<string>();
            int fitted = TitleBlockFitter.Apply(drawing, _fitWarnings);
            AddinServices.Log.Info(
                $"Place Initial Views: {fitted} title block field(s) fitted to their cells." +
                (_fitWarnings.Count == 0 ? string.Empty : " Warnings: " + string.Join(" ", _fitWarnings)));

            AddinServices.Log.Info(
                $"Place Initial Views: {placed.Count} view(s) ({string.Join("; ", placed)}) at scale {_plan.Scale} on sheet '{ComSafe.Get(() => sheet.Name)}'.");
        }

        /// <summary>The first part or assembly the drawing references, or null when it references none.</summary>
        private static Document? FindModel(DrawingDocument drawing)
        {
            foreach (Document referenced in drawing.ReferencedDocuments)
            {
                DocumentKind kind = DocumentKinds.Of(referenced);
                if (kind.IsPart() || kind.IsAssembly())
                    return referenced;
            }

            return null;
        }

        /// <summary>Closes the model this command opened itself, without saving. Does nothing for a document that was already open.</summary>
        private void CloseOpenedByUs()
        {
            Document? opened = _openedByUs;
            _openedByUs = null;
            if (opened != null)
                ComSafe.Run(() => opened.Close(true));
        }

        /// <summary>
        /// Asks for a part or assembly file and opens it without showing it; null when the user cancels or it is not a model. A file that is
        /// already open is used as it is and is never closed by this command.
        /// </summary>
        private Document? AskForModel()
        {
            var picker = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Choose the part or assembly to place views of",
                Filter = "Inventor parts and assemblies (*.ipt;*.iam)|*.ipt;*.iam",
                CheckFileExists = true,
            };
            if (picker.ShowDialog() != true)
                return null;

            bool wasOpen = IsOpen(picker.FileName);
            Document opened = InventorHost.App.Documents.Open(picker.FileName, false);
            if (!wasOpen)
                _openedByUs = opened;

            DocumentKind kind = DocumentKinds.Of(opened);
            if (!kind.IsPart() && !kind.IsAssembly())
            {
                Forms.MessageBox.Show(PrintPlan.WrongDocumentMessage, DisplayName, Forms.MessageBoxButtons.OK, Forms.MessageBoxIcon.Information);
                CloseOpenedByUs();
                return null;
            }

            return opened;
        }

        private static bool IsOpen(string fullFileName)
        {
            foreach (Document open in InventorHost.App.Documents)
            {
                string? name = ComSafe.Get(() => open.FullFileName);
                if (string.Equals(name, fullFileName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}
