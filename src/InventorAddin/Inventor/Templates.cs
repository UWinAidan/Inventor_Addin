using System;
using Inventor;
using InventorAddin.Core.Numbering;
using InventorAddin.Core.Settings;

namespace InventorAddin
{
    /// <summary>
    /// Inventor's default templates (spec 02, "Templates"), found through <c>FileManager.GetTemplateFile</c> with the
    /// units and the default drafting standard, as spike 032 confirmed in Inventor 2026. Drawings need a named drafting
    /// standard (<see cref="DrawingFor"/>).
    /// </summary>
    public static class Templates
    {
        /// <summary>
        /// The full path of the default template for <paramref name="kind"/> in <paramref name="units"/>, or null when
        /// Inventor has none. Spike 032: the lookup returns <c>""</c>, not an exception, when there is no template; an
        /// exception is logged and also read as "none".
        /// </summary>
        public static string? DefaultFor(NewFileKind kind, UnitsSystem units)
        {
            DocumentTypeEnum documentType = DocumentTypeFor(kind);
            SystemOfMeasureEnum system = SystemOfMeasureFor(units);

            // Sheet metal is a part with the sheet metal subtype; parts and assemblies pass no subtype.
            object subType = kind == NewFileKind.SheetMetalPart ? DocumentKinds.SheetMetalSubType : Type.Missing;

            try
            {
                string path = InventorHost.App.FileManager.GetTemplateFile(
                    documentType, system, DraftingStandardEnum.kDefault_DraftingStandard, subType);
                return string.IsNullOrWhiteSpace(path) ? null : path;
            }
            catch (Exception ex)
            {
                AddinServices.Log.Error($"Looking up the default {kind} template ({units}) failed.", ex);
                return null;
            }
        }

        /// <summary>
        /// The full path of the drawing template for <paramref name="units"/>, or null when Inventor has none.
        /// Spike 032: with the default drafting standard the drawing lookup returns <c>""</c>, so it asks for ANSI
        /// (Aidan, spec 02 question 24; ANSI is the only standard Inventor has in both units). When that is blank it
        /// falls back to the units-neutral default drawing template (<c>standard.dwg</c> on Aidan's install).
        /// </summary>
        public static string? DrawingFor(UnitsSystem units)
        {
            SystemOfMeasureEnum system = SystemOfMeasureFor(units);
            var files = InventorHost.App.FileManager;

            string? path = Lookup(
                $"the ANSI drawing template ({units})",
                () => files.GetTemplateFile(
                    DocumentTypeEnum.kDrawingDocumentObject, system, DraftingStandardEnum.kANSI_DraftingStandard, Type.Missing));
            if (path != null)
                return path;

            AddinServices.Log.Warn($"Inventor has no ANSI drawing template for {units} units; trying its default drawing template.");
            return Lookup(
                "the default drawing template",
                () => files.GetTemplateFileWithOptions(
                    DocumentTypeEnum.kDrawingDocumentObject, Type.Missing, Type.Missing, Type.Missing, Type.Missing));
        }

        /// <summary>The path <paramref name="lookup"/> returns, or null when it is blank or throws (logged).</summary>
        private static string? Lookup(string what, Func<string> lookup)
        {
            try
            {
                string path = lookup();
                return string.IsNullOrWhiteSpace(path) ? null : path;
            }
            catch (Exception ex)
            {
                AddinServices.Log.Error($"Looking up {what} failed.", ex);
                return null;
            }
        }

        /// <summary>The document type <c>Documents.Add</c> takes for <paramref name="kind"/>: a sheet metal part is a part.</summary>
        public static DocumentTypeEnum DocumentTypeFor(NewFileKind kind) => kind switch
        {
            NewFileKind.Part or NewFileKind.SheetMetalPart => DocumentTypeEnum.kPartDocumentObject,
            NewFileKind.Assembly => DocumentTypeEnum.kAssemblyDocumentObject,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Not a kind of file New Part creates."),
        };

        private static SystemOfMeasureEnum SystemOfMeasureFor(UnitsSystem units) => units switch
        {
            UnitsSystem.Metric => SystemOfMeasureEnum.kMetricSystemOfMeasure,
            UnitsSystem.Imperial => SystemOfMeasureEnum.kEnglishSystemOfMeasure,
            _ => throw new ArgumentOutOfRangeException(nameof(units), units, "Unknown units."),
        };
    }
}
