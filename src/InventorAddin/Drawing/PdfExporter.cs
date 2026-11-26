using System;
using System.Collections.Generic;
using Inventor;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using IOFile = System.IO.File;

// The Drawing folder holds the drawing-side helpers; the namespace names the feature, not the folder (Inventor has types named Drawing*).
namespace InventorAddin.PrintViews
{
    /// <summary>
    /// PDF output for Batch Print (task 202). Inventor's PDF translator writes one file per drawing and cannot append (spike 184), so
    /// each drawing is exported to its own file and <see cref="Merge"/> joins them with PDFsharp (MIT licence).
    /// </summary>
    internal static class PdfExporter
    {
        /// <summary>The PDF translator add-in's class id (spike 184).</summary>
        private const string PdfAddInId = "{0AC6FD96-2F4D-42CE-8BE0-8AEA580399E4}";

        /// <summary>
        /// Exports every sheet of <paramref name="drawing"/> to <paramref name="pdfPath"/>, in colour as drawn (spike 184, round 1:
        /// the translator's options are filled with its defaults, then Sheet_Range and All_Color_AS_Black are set). Throws when
        /// the PDF add-in is missing or the export fails; the caller records that as a failed drawing.
        /// </summary>
        public static void Export(Application app, Document drawing, string pdfPath)
        {
            ApplicationAddIn? found = ComSafe.Get(() => app.ApplicationAddIns.ItemById[PdfAddInId]);
            if (found is not TranslatorAddIn translator)
                throw new InvalidOperationException("Inventor's PDF add-in was not found.");
            if (!translator.Activated)
                translator.Activate();

            TranslationContext context = app.TransientObjects.CreateTranslationContext();
            context.Type = IOMechanismEnum.kFileBrowseIOMechanism;
            NameValueMap options = app.TransientObjects.CreateNameValueMap();
            if (translator.HasSaveCopyAsOptions[drawing, context, options])
            {
                options.Value["Sheet_Range"] = PrintRangeEnum.kPrintAllSheets;
                options.Value["All_Color_AS_Black"] = 0;
            }

            DataMedium medium = app.TransientObjects.CreateDataMedium();
            medium.FileName = pdfPath;
            translator.SaveCopyAs(drawing, context, options, medium);

            if (!IOFile.Exists(pdfPath))
                throw new InvalidOperationException("Inventor wrote no PDF.");
        }

        /// <summary>
        /// Joins <paramref name="inputs"/> in order into <paramref name="outputPath"/>. Written to a temporary name beside the
        /// output and moved over it at the end, so a failure leaves an existing PDF as it was.
        /// </summary>
        public static void Merge(IReadOnlyList<string> inputs, string outputPath)
        {
            string temp = outputPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var output = new PdfDocument())
                {
                    foreach (string input in inputs)
                    {
                        using PdfDocument source = PdfReader.Open(input, PdfDocumentOpenMode.Import);
                        foreach (PdfPage page in source.Pages)
                            output.AddPage(page);
                    }

                    output.Save(temp);
                }

                IOFile.Move(temp, outputPath, overwrite: true);
            }
            finally
            {
                try { IOFile.Delete(temp); } catch (Exception) { /* a leftover temp file is harmless */ }
            }
        }
    }
}
