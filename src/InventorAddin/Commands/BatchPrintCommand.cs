using System;
using System.Collections.Generic;
using Inventor;
using InventorAddin.Core.Drawing;
using InventorAddin.Core.Models;
using InventorAddin.PrintViews;
using Forms = System.Windows.Forms;
using IODirectory = System.IO.Directory;
using IOFile = System.IO.File;
using IOPath = System.IO.Path;

namespace InventorAddin.Commands
{
    /// <summary>
    /// Batch Print (spec 05, "Added for M5: New tools", item 3; task 202): one PDF of the assembly's drawing and every drawing under it,
    /// the assembly's first, saved beside the assembly as <c>&lt;assembly name&gt;.pdf</c>. The plan (which drawings, in what order, what
    /// is skipped, the report) is Core's <see cref="BatchPrintPlan"/>; this reads the tree, exports each drawing to a temporary PDF
    /// (<see cref="PdfExporter"/>) and merges them. An existing PDF is replaced only after asking.
    /// </summary>
    /// <remarks>
    /// Changes no document: drawings are opened hidden, exported and closed without saving (a drawing already open is left open and
    /// untouched). It writes a file, so its command type is <c>kFileOperationsCmdType</c>, like Create Print and Open Drawing.
    /// </remarks>
    public sealed class BatchPrintCommand : AddinCommand
    {
        public override string InternalName => Core.Ribbon.CommandNames.BatchPrint;
        public override string DisplayName => "Batch Print";
        public override string Description =>
            "Make one PDF of this assembly's drawing and every drawing under it, saved next to the assembly.";

        protected override CommandTypesEnum CommandType => CommandTypesEnum.kFileOperationsCmdType;

        protected override void Execute()
        {
            Document? doc = CommandTargetDocument();
            if (doc == null || !DocumentKinds.Of(doc).IsAssembly())
            {
                Show(BatchPrintPlan.WrongDocumentMessage, Forms.MessageBoxIcon.Information);
                return;
            }

            string path = ComSafe.Get(() => doc.FullFileName) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(path))
            {
                Show(BatchPrintPlan.UnsavedMessage, Forms.MessageBoxIcon.Information);
                return;
            }

            // Default (task 202): an assembly that is itself a library file is refused up front.
            IReadOnlyList<string> libraryFolders = BatchPrintTreeReader.LibraryFolders(InventorHost.App);
            if (BatchPrintPlan.IsUnderAnyFolder(path, libraryFolders))
            {
                Show(BatchPrintPlan.LibraryRootMessage, Forms.MessageBoxIcon.Information);
                return;
            }

            BatchPrintComponent tree = BatchPrintTreeReader.Read((AssemblyDocument)doc, path, libraryFolders);
            BatchPrintPlan plan = BatchPrintPlan.Create(
                tree, model => PrintPlan.FindExistingPrint(model, PrintPlan.NewPrintExtension, IOFile.Exists));

            if (plan.IsEmpty)
            {
                Show(plan.ReportText(), Forms.MessageBoxIcon.Information);
                return;
            }

            if (IOFile.Exists(plan.PdfFullFileName))
            {
                Forms.DialogResult answer = Forms.MessageBox.Show(
                    BatchPrintPlan.ReplaceQuestion(plan.PdfFullFileName), DisplayName,
                    Forms.MessageBoxButtons.YesNo, Forms.MessageBoxIcon.Question);
                if (answer != Forms.DialogResult.Yes)
                    return;
            }

            AddinServices.Log.Info($"Batch Print: {plan.Drawings.Count} drawing(s) for '{path}' to '{plan.PdfFullFileName}'.");

            var failures = new List<BatchPrintFailure>();
            var exported = new List<string>();
            string tempFolder = IOPath.Combine(IOPath.GetTempPath(), Core.Branding.IdPrefix + "BatchPrint", Guid.NewGuid().ToString("N"));
            Application app = InventorHost.App;
            Forms.Cursor previousCursor = Forms.Cursor.Current;
            bool previousSilent = app.SilentOperation;
            string previousStatus = ComSafe.Get(() => app.StatusBarText) ?? string.Empty;
            try
            {
                Forms.Cursor.Current = Forms.Cursors.WaitCursor;
                app.SilentOperation = true;
                IODirectory.CreateDirectory(tempFolder);

                for (int i = 0; i < plan.Drawings.Count; i++)
                {
                    BatchPrintDrawing item = plan.Drawings[i];
                    int number = i + 1;
                    ComSafe.Run(() => app.StatusBarText = $"Batch Print: drawing {number} of {plan.Drawings.Count}...");
                    string pdf = IOPath.Combine(tempFolder, $"{number:D4}.pdf");
                    try
                    {
                        ExportOne(app, item.DrawingFullFileName, pdf);
                        exported.Add(pdf);
                    }
                    catch (Exception ex)
                    {
                        AddinServices.Log.Warn($"Batch Print: '{item.DrawingFullFileName}' was not printed. {ex.Message}");
                        failures.Add(new BatchPrintFailure(item.DrawingFullFileName, ex.Message));
                    }
                }

                if (exported.Count > 0)
                {
                    ComSafe.Run(() => app.StatusBarText = "Batch Print: joining the PDFs...");
                    try
                    {
                        PdfExporter.Merge(exported, plan.PdfFullFileName);
                    }
                    catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
                    {
                        AddinServices.Log.Error("Batch Print: the PDF could not be written.", ex);
                        Show(BatchPrintPlan.PdfNotWrittenMessage(plan.PdfFullFileName), Forms.MessageBoxIcon.Warning);
                        return;
                    }
                    catch (Exception ex)
                    {
                        AddinServices.Log.Error("Batch Print: joining the PDFs failed.", ex);
                        Show(BatchPrintPlan.PdfMergeFailedMessage(plan.PdfFullFileName), Forms.MessageBoxIcon.Warning);
                        return;
                    }
                }
            }
            finally
            {
                app.SilentOperation = previousSilent;
                ComSafe.Run(() => app.StatusBarText = previousStatus);
                Forms.Cursor.Current = previousCursor;
                try { IODirectory.Delete(tempFolder, true); } catch (Exception) { /* temporary files, left for the system to clean up */ }
            }

            Show(
                plan.ReportText(failures),
                failures.Count > 0 || plan.ModelsWithoutDrawing.Count > 0 ? Forms.MessageBoxIcon.Warning : Forms.MessageBoxIcon.Information);
        }

        /// <summary>
        /// Exports one drawing. A drawing that is not open is opened hidden and closed again without saving, also when the export fails;
        /// one the user already has open is exported where it is and left open.
        /// </summary>
        private static void ExportOne(Application app, string drawingPath, string pdfPath)
        {
            bool wasOpen = IsOpen(app, drawingPath);
            Document? drawing = null;
            try
            {
                drawing = app.Documents.Open(drawingPath, false);
                PdfExporter.Export(app, drawing, pdfPath);
            }
            finally
            {
                if (drawing != null && !wasOpen)
                    ComSafe.Run(() => drawing.Close(true));
            }
        }

        private static bool IsOpen(Application app, string fullFileName)
        {
            foreach (Document open in app.Documents)
            {
                string? name = ComSafe.Get(() => open.FullFileName);
                if (name != null && string.Equals(name, fullFileName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private void Show(string text, Forms.MessageBoxIcon icon) =>
            Forms.MessageBox.Show(text, DisplayName, Forms.MessageBoxButtons.OK, icon);
    }
}
