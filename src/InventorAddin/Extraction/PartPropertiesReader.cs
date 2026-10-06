using System;
using System.Globalization;
using Inventor;
using InventorAddin.Core.Models;
using InventorAddin.Core.PartProperties;

namespace InventorAddin.Extraction
{
    /// <summary>
    /// Reads what the Part Properties window shows (spec 07) from an open document into a
    /// <see cref="PartPropertiesSnapshot"/>. Read-only: it never changes the document, and it never throws
    /// for a property that is missing or cannot be read.
    /// </summary>
    public static class PartPropertiesReader
    {
        public static PartPropertiesSnapshot Read(Document doc)
        {
            string fullFileName = ComSafe.Get(() => doc.FullFileName) ?? string.Empty;
            DocumentKind kind = ComSafe.Get(() => DocumentKinds.Of(doc));

            return new PartPropertiesSnapshot
            {
                FullFileName = fullFileName,
                DocumentKind = kind,
                PartNumber = ReadText(doc, PartPropertyStorage.PartNumber),
                Description = ReadText(doc, PartPropertyStorage.Description),
                PartType = ReadText(doc, PartPropertyStorage.PartType),
                Designer = ReadText(doc, PartPropertyStorage.Designer),
                Detailer = ReadText(doc, PartPropertyStorage.Detailer),
                Cost = ReadCost(doc, PartPropertyStorage.Cost),
                Material = ReadMaterial(doc, kind),
                Finish = ReadText(doc, PartPropertyStorage.Finish),
                WeightDisplay = ReadWeight(doc, kind),
                EditBlock = ReadEditBlock(doc, fullFileName),
            };
        }

        private static string? ReadText(Document doc, PropertyLocation location) =>
            PropertyReader.GetString(doc, location.SetName, location.PropertyName);

        /// <summary>The Cost value as a decimal; null when it is missing or cannot be converted.</summary>
        private static decimal? ReadCost(Document doc, PropertyLocation location)
        {
            object? value = PropertyReader.Get(doc, location.SetName, location.PropertyName);
            if (value == null)
                return null; // Convert.ToDecimal(null) would give 0, which is not "unreadable".
            return ComSafe.Get(() => (decimal?)Convert.ToDecimal(value, CultureInfo.InvariantCulture));
        }

        /// <summary>The part's material display name, as <see cref="PartExtractor"/> reads it; empty for anything that is not a part.</summary>
        private static string? ReadMaterial(Document doc, DocumentKind kind)
        {
            if (!kind.IsPart())
                return string.Empty;
            return ComSafe.Get(() => ((PartDocument)doc).ActiveMaterial.DisplayName);
        }

        /// <summary>
        /// Mass in the document's units, through the same <see cref="PartExtractor.ReadMass"/> path the
        /// extractors use. Null on any failure, and for document kinds that have no mass.
        /// </summary>
        private static string? ReadWeight(Document doc, DocumentKind kind)
        {
            return ComSafe.Get(() =>
            {
                MassProperties? massProperties =
                    kind.IsPart() ? ((PartDocument)doc).ComponentDefinition.MassProperties
                    : kind.IsAssembly() ? ((AssemblyDocument)doc).ComponentDefinition.MassProperties
                    : null;
                if (massProperties == null)
                    return null;
                var units = new UnitsFormatter(doc);
                return PartExtractor.ReadMass(massProperties, units).MassDisplay;
            });
        }

        private static EditBlock ReadEditBlock(Document doc, string fullFileName)
        {
            if (ComSafe.Get(() => ModelExtractor.IsFileReadOnly(fullFileName)))
                return EditBlock.ReadOnlyFile;

            bool? modifiable = ComSafe.Get(() => (bool?)doc.IsModifiable);
            if (modifiable == null)
            {
                // Log no file name or property values: the log stays free of user data.
                AddinServices.Log.Warn("Part properties: could not read Document.IsModifiable; treating the document as modifiable.");
                return EditBlock.None;
            }

            return modifiable.Value ? EditBlock.None : EditBlock.NotModifiable;
        }
    }
}
