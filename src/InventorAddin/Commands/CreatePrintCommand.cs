using System;
using Inventor;
using InventorAddin.Core.Drawing;
using InventorAddin.Core.Models;
using InventorAddin.Core.PartProperties;
using InventorAddin.Core.Ribbon;
using InventorAddin.Core.Settings;
using InventorAddin.Extraction;
using Forms = System.Windows.Forms;
using IOFile = System.IO.File;
using IOPath = System.IO.Path;

namespace InventorAddin.Commands
{
    /// <summary>
    /// Create Print (spec 02, "Commands: Create Print"): makes a drawing of the active part or assembly from Inventor's
    /// drawing template, with one front base view centred on the first sheet at a standard scale that fits, saves it
    /// beside the model under the same name, and leaves it open. The file name, the scale and the messages are decided
    /// in <see cref="PrintPlan"/> (Core); this class reads Inventor and applies the result, by the calls spike 032
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
        public override string Description => "Create a drawing of this part or assembly, with a front view, saved beside it.";

        protected override CommandTypesEnum CommandType => CommandTypesEnum.kFileOperationsCmdType;

        protected override void Execute()
        {
            Document? model = InventorHost.ActiveDocument;
            if (model == null)
            {
                Show("Open a part or assembly first.", Forms.MessageBoxIcon.Information);
                return;
            }

            DocumentKind kind = DocumentKinds.Of(model);
            if (!kind.IsPart() && !kind.IsAssembly())
            {
                Show(PrintPlan.WrongDocumentMessage, Forms.MessageBoxIcon.Information);
                return;
            }

            string modelPath = ComSafe.Get(() => model.FullFileName) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(modelPath))
            {
                Show(PrintPlan.UnsavedMessage, Forms.MessageBoxIcon.Information);
                return;
            }

            UnitsSystem units = AddinServices.Settings.DefaultUnits;
            string? template = Templates.DrawingFor(units);
            string preferredExtension = template != null ? IOPath.GetExtension(template) : PrintPlan.DrawingExtensions[0];

            // An existing print, with either drawing extension, is offered instead of making a new one.
            string? existing = PrintPlan.FindExistingPrint(modelPath, preferredExtension, IOFile.Exists);
            if (existing != null)
            {
                OfferToOpen(existing);
                return;
            }

            if (template == null)
            {
                string message = PrintPlan.MissingTemplateMessage(units);
                AddinServices.Log.Warn($"Create Print stopped: {message}");
                Show(message, Forms.MessageBoxIcon.Warning);
                return;
            }

            // The front view's size at scale 1 is the range box's X and Y extents (spike 032).
            if (!TryReadFrontViewSize(model, kind, out double viewWidthCm, out double viewHeightCm))
            {
                AddinServices.Log.Warn($"Create Print stopped: no geometry to show in '{modelPath}'.");
                Show(PrintPlan.NoGeometryMessage, Forms.MessageBoxIcon.Warning);
                return;
            }

            string drawingPath = PrintPlan.DrawingFileName(modelPath, IOPath.GetExtension(template));
            string? partNumber = ModelPartNumber(model);

            Forms.Cursor previousCursor = Forms.Cursor.Current;
            Forms.Cursor.Current = Forms.Cursors.WaitCursor;
            try
            {
                Create(model, template, drawingPath, viewWidthCm, viewHeightCm, partNumber);
            }
            finally
            {
                Forms.Cursor.Current = previousCursor;
            }
        }

        /// <summary>
        /// Creates, fills and saves the drawing. Any failure closes the half-made drawing without saving and says which
        /// step failed.
        /// </summary>
        private void Create(
            Document model, string template, string drawingPath, double viewWidthCm, double viewHeightCm, string? partNumber)
        {
            string step = "creating the drawing from the template";
            Document? drawing = null;
            try
            {
                // Visible, so the new drawing opens and becomes the active document.
                drawing = InventorHost.App.Documents.Add(DocumentTypeEnum.kDrawingDocumentObject, template, true);

                step = "reading the first sheet";
                Sheet sheet = ((DrawingDocument)drawing).ActiveSheet;
                double scale = PrintPlan.ChooseScale(viewWidthCm, viewHeightCm, sheet.Width, sheet.Height);

                step = "adding the front view";
                Document drawingDoc = drawing;
                RunInTransaction(drawingDoc, () =>
                {
                    var centre = InventorHost.App.TransientGeometry.CreatePoint2d(sheet.Width / 2, sheet.Height / 2);
                    sheet.DrawingViews.AddBaseView((_Document)model, centre, scale,
                        ViewOrientationTypeEnum.kFrontViewOrientation, DrawingViewStyleEnum.kHiddenLineDrawingViewStyle);

                    if (partNumber != null)
                    {
                        step = "writing the Part Number";
                        PropertyWriter.Apply(drawingDoc, new[] { PropertyWrite.SetText(PartPropertyStorage.PartNumber, partNumber) });
                    }
                });

                step = $"saving it as '{drawingPath}'";
                drawing.SaveAs(drawingPath, false);

                AddinServices.Log.Info(
                    $"Create Print: '{drawingPath}' created with a front view at scale {scale} " +
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

        /// <summary>The X and Y extents of the model's range box in cm; false when it cannot be read or is flat.</summary>
        private static bool TryReadFrontViewSize(Document model, DocumentKind kind, out double widthCm, out double heightCm)
        {
            widthCm = 0;
            heightCm = 0;

            Box? box = ComSafe.Get(() => kind.IsPart()
                ? ((PartDocument)model).ComponentDefinition.RangeBox
                : ((AssemblyDocument)model).ComponentDefinition.RangeBox);
            if (box == null)
                return false;

            double? width = ComSafe.Get(() => (double?)(box.MaxPoint.X - box.MinPoint.X));
            double? height = ComSafe.Get(() => (double?)(box.MaxPoint.Y - box.MinPoint.Y));
            // A lifted comparison is false for null and NaN.
            if (!(width > 0) || !(height > 0) || double.IsInfinity(width!.Value) || double.IsInfinity(height!.Value))
                return false;

            widthCm = width.Value;
            heightCm = height.Value;
            return true;
        }

        private void OfferToOpen(string drawingPath)
        {
            AddinServices.Log.Info($"Create Print: a print already exists at '{drawingPath}'.");

            Forms.DialogResult answer = Forms.MessageBox.Show(
                PrintPlan.AlreadyExistsQuestion, DisplayName, Forms.MessageBoxButtons.YesNo, Forms.MessageBoxIcon.Question);
            if (answer != Forms.DialogResult.Yes)
                return;

            Document opened = InventorHost.App.Documents.Open(drawingPath, true);
            // If the drawing was already open, Open returns it; make it the active document either way.
            ComSafe.Run(() => opened.Activate());
        }

        private void Show(string text, Forms.MessageBoxIcon icon) =>
            Forms.MessageBox.Show(text, DisplayName, Forms.MessageBoxButtons.OK, icon);
    }
}
