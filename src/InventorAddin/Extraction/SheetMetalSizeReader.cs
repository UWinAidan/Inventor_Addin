using Inventor;
using InventorAddin.Commands;
using InventorAddin.Core.PartProperties;

namespace InventorAddin.Extraction
{
    /// <summary>
    /// Reads a sheet metal part's thickness and flat pattern size and asks Core (<see cref="SheetMetalSizeText"/>) for the write that
    /// brings the custom "Material Size" property up to date (task 230). It only reads; the caller writes the result.
    /// </summary>
    internal static class SheetMetalSizeReader
    {
        /// <summary>
        /// The Material Size write for a part: null when it is not sheet metal, has no flat pattern, a size cannot be read, or the stored
        /// text is already right (the property is then left alone). Never throws. Sizes are in centimetres.
        /// </summary>
        public static PropertyWrite? WriteFor(Document doc)
        {
            double? thickness = null;
            double? length = null;
            double? width = null;
            ComSafe.Run(() =>
            {
                if (((PartDocument)doc).ComponentDefinition is SheetMetalComponentDefinition sheetMetal && sheetMetal.HasFlatPattern)
                {
                    thickness = (double)sheetMetal.Thickness.Value;
                    length = sheetMetal.FlatPattern.Length;
                    width = sheetMetal.FlatPattern.Width;
                }
            });
            if (thickness == null)
                return null;

            string? stored = PropertyReader.GetString(
                doc, PartPropertyStorage.MaterialSize.SetName, PartPropertyStorage.MaterialSize.PropertyName);
            return SheetMetalSizeText.WriteFor(thickness, length, width, MaterialFinishTargets.UnitsOf(doc), stored);
        }
    }
}
