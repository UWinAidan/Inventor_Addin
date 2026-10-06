using System;
using System.Collections.Generic;
using Inventor;
using InventorAddin.Core.Models;
using InventorAddin.Core.PartProperties;

namespace InventorAddin.Extraction
{
    /// <summary>Reads and writes iProperties.</summary>
    public static class PropertyReader
    {
        // Internal (language-independent) property set names
        public const string SummarySet = PropertySetNames.Summary;
        public const string DocumentSummarySet = PropertySetNames.DocumentSummary;
        public const string DesignTrackingSet = PropertySetNames.DesignTracking;
        public const string UserDefinedSet = PropertySetNames.UserDefined;

        /// <summary>All property sets: display name → property name → value. Non-primitive values (thumbnails) are skipped.</summary>
        public static Dictionary<string, Dictionary<string, object?>> ReadAll(Document doc, List<string> warnings)
        {
            var result = new Dictionary<string, Dictionary<string, object?>>();
            foreach (PropertySet set in doc.PropertySets)
            {
                var props = new Dictionary<string, object?>();
                foreach (Property prop in set)
                {
                    object? value = ComSafe.Get(() => prop.Value);
                    if (value == null || IsSimple(value))
                        props[prop.Name] = value;
                }
                result[ComSafe.Get(() => set.DisplayName) ?? set.Name] = props;
            }
            return result;
        }

        public static CommonProperties ReadCommon(Document doc) => new()
        {
            PartNumber = GetString(doc, DesignTrackingSet, "Part Number"),
            StockNumber = GetString(doc, DesignTrackingSet, "Stock Number"),
            Description = GetString(doc, DesignTrackingSet, "Description"),
            Project = GetString(doc, DesignTrackingSet, "Project"),
            Designer = GetString(doc, DesignTrackingSet, "Designer"),
            Engineer = GetString(doc, DesignTrackingSet, "Engineer"),
            Vendor = GetString(doc, DesignTrackingSet, "Vendor"),
            Material = GetString(doc, DesignTrackingSet, "Material"),
            CostCenter = GetString(doc, DesignTrackingSet, "Cost Center"),
            CreationDate = Get(doc, DesignTrackingSet, "Creation Time") as DateTime?,
            Revision = GetString(doc, SummarySet, "Revision Number"),
            Title = GetString(doc, SummarySet, "Title"),
            Author = GetString(doc, SummarySet, "Author"),
        };

        public static object? Get(Document doc, string setName, string propName) =>
            ComSafe.Get(() => doc.PropertySets[setName][propName].Value);

        public static string? GetString(Document doc, string setName, string propName) =>
            Get(doc, setName, propName)?.ToString();

        /// <summary>Sets a property, creating it in the user-defined set if it doesn't exist.</summary>
        public static void Set(Document doc, string setName, string propName, object value)
        {
            PropertySet set = doc.PropertySets[setName];
            Property? existing = ComSafe.Get(() => set[propName]);
            if (existing != null)
                existing.Value = value;
            else if (setName == UserDefinedSet)
                set.Add(value, propName);
            else
                throw new ArgumentException($"Property '{propName}' does not exist in '{setName}'.");
        }

        private static bool IsSimple(object v) =>
            v is string || v is bool || v is DateTime || v is decimal || v.GetType().IsPrimitive;
    }
}
