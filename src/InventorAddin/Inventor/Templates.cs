using System;
using Inventor;
using InventorAddin.Core.Numbering;
using InventorAddin.Core.Settings;

namespace InventorAddin
{
    /// <summary>
    /// Inventor's default templates (spec 02, "Templates"), found through <c>FileManager.GetTemplateFile</c> with the
    /// units and the default drafting standard, as spike 032 confirmed in Inventor 2026. Task 044 adds the drawing case.
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
