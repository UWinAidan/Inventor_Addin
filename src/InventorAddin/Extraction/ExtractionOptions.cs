namespace InventorAddin.Extraction
{
    public class ExtractionOptions
    {
        /// <summary>Mass properties can force a recompute on large models.</summary>
        public bool IncludeMassProperties { get; set; } = true;

        public bool IncludeILogicRuleText { get; set; } = true;

        /// <summary>
        /// Reading the BOM requires its Structured / Parts Only views to be enabled.
        /// Enabling them marks the assembly as modified.
        /// </summary>
        public bool EnableBomViews { get; set; } = true;

        /// <summary>For drawings: fully extract each model shown on the drawing.</summary>
        public bool IncludeDrawingModels { get; set; } = true;

        /// <summary>Read cell contents of hole tables / parts lists / revision tables.</summary>
        public bool IncludeTableContents { get; set; } = true;

        public static ExtractionOptions Default => new();
    }
}
