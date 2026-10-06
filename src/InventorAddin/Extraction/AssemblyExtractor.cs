using System.Collections.Generic;
using Inventor;
using InventorAddin.Core.Models;

namespace InventorAddin.Extraction
{
    public static class AssemblyExtractor
    {
        public static void Fill(AssemblyData data, AssemblyDocument doc, ExtractionOptions options)
        {
            var w = data.Warnings;
            var units = new UnitsFormatter((Document)doc);
            AssemblyComponentDefinition def = doc.ComponentDefinition;

            data.BomStructure = ComSafe.Get(() => def.BOMStructure.ToString(), w, "BOM structure");
            data.RangeBox = ComSafe.Get(() => PartExtractor.ReadBox(def.RangeBox), w, "Range box");
            if (options.IncludeMassProperties)
                data.Physical = ComSafe.Get(() => PartExtractor.ReadMass(def.MassProperties, units), w, "Mass properties");

            data.Finishes = FinishReader.Read(def.Features, w);
            data.Occurrences = ReadOccurrences(def.Occurrences, w);

            ComSafe.Run(() => ReadBom(data, def.BOM, options), w, "BOM");
        }

        private static List<OccurrenceData> ReadOccurrences(ComponentOccurrences occurrences, List<string> w)
        {
            var list = new List<OccurrenceData>();
            foreach (ComponentOccurrence occ in occurrences)
                list.Add(ReadOccurrence(occ, w));
            return list;
        }

        private static OccurrenceData ReadOccurrence(ComponentOccurrence occ, List<string> w)
        {
            var data = new OccurrenceData
            {
                Name = occ.Name,
                Suppressed = occ.Suppressed,
                Visible = ComSafe.Get(() => occ.Visible),
                IsPatternElement = ComSafe.Get(() => occ.IsPatternElement),
                BomStructure = ComSafe.Get(() => occ.BOMStructure.ToString()),
            };

            if (occ.Suppressed)
            {
                data.FullFileName = ComSafe.Get(() => occ.ReferencedDocumentDescriptor.FullDocumentName);
                return data;
            }

            if (occ.Definition is VirtualComponentDefinition virt)
            {
                data.IsVirtual = true;
                data.PartNumber = ComSafe.Get(() => (string)virt.PropertySets[PropertyReader.DesignTrackingSet]["Part Number"].Value);
                data.Description = ComSafe.Get(() => (string)virt.PropertySets[PropertyReader.DesignTrackingSet]["Description"].Value);
                return data;
            }

            Document? refDoc = ComSafe.Get(() => (Document)occ.Definition.Document);
            if (refDoc != null)
            {
                data.FullFileName = refDoc.FullFileName;
                data.PartNumber = PropertyReader.GetString(refDoc, PropertyReader.DesignTrackingSet, "Part Number");
                data.Description = PropertyReader.GetString(refDoc, PropertyReader.DesignTrackingSet, "Description");
            }

            ComSafe.Run(() =>
            {
                foreach (ComponentOccurrence child in occ.SubOccurrences)
                    data.Children.Add(ReadOccurrence(child, w));
            }, w, $"Sub-occurrences of '{occ.Name}'");

            return data;
        }

        private static void ReadBom(AssemblyData data, BOM bom, ExtractionOptions options)
        {
            if (options.EnableBomViews)
            {
                if (!bom.StructuredViewEnabled) bom.StructuredViewEnabled = true;
                if (!bom.PartsOnlyViewEnabled) bom.PartsOnlyViewEnabled = true;
                bom.StructuredViewFirstLevelOnly = false;
            }

            foreach (BOMView view in bom.BOMViews)
            {
                switch (view.ViewType)
                {
                    case BOMViewTypeEnum.kStructuredBOMViewType:
                        data.StructuredBom = ReadRows(view.BOMRows, data.Warnings);
                        break;
                    case BOMViewTypeEnum.kPartsOnlyBOMViewType:
                        data.PartsOnlyBom = ReadRows(view.BOMRows, data.Warnings);
                        break;
                }
            }
        }

        private static List<BomRowData> ReadRows(BOMRowsEnumerator? rows, List<string> w)
        {
            var list = new List<BomRowData>();
            if (rows == null)
                return list;

            foreach (BOMRow row in rows)
            {
                var data = new BomRowData
                {
                    ItemNumber = row.ItemNumber,
                    ItemQuantity = ComSafe.Get(() => row.ItemQuantity),
                    TotalQuantity = ComSafe.Get(() => row.TotalQuantity),
                    BomStructure = ComSafe.Get(() => row.BOMStructure.ToString()),
                    Merged = ComSafe.Get(() => row.Merged),
                };

                ComponentDefinition? compDef = ComSafe.Get(() => row.ComponentDefinitions[1]);
                if (compDef is VirtualComponentDefinition virt)
                {
                    data.PartNumber = ComSafe.Get(() => (string)virt.PropertySets[PropertyReader.DesignTrackingSet]["Part Number"].Value);
                    data.Description = ComSafe.Get(() => (string)virt.PropertySets[PropertyReader.DesignTrackingSet]["Description"].Value);
                }
                else if (ComSafe.Get(() => (Document)compDef!.Document) is Document doc)
                {
                    data.FullFileName = doc.FullFileName;
                    data.PartNumber = PropertyReader.GetString(doc, PropertyReader.DesignTrackingSet, "Part Number");
                    data.Description = PropertyReader.GetString(doc, PropertyReader.DesignTrackingSet, "Description");
                    data.Material = PropertyReader.GetString(doc, PropertyReader.DesignTrackingSet, "Material");
                }

                data.Children = ReadRows(ComSafe.Get(() => row.ChildRows), w);
                list.Add(data);
            }
            return list;
        }
    }
}
