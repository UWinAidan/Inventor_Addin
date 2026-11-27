namespace InventorAddin.Core.Materials;

/// <summary>
/// Which parts the Stock Size command works on (spec 05, "Added for M5", item 5; task 206). A sheet metal part gets its Material Size from
/// its flat pattern (task 230: "1.5 x 60 x 120 mm"), and that value replaces a hand-typed one, so the Stock Size command leaves every sheet
/// metal part alone: it would write a "PL ..." text that the next save of the part overwrites. The command says so and writes nothing.
/// (Default, task 206; logged in DECISIONS.md by the parent.)
/// </summary>
public static class StockSizeEligibility
{
    /// <summary>Shown when the active document is not a part.</summary>
    public const string WrongDocument = "Stock Size works on a part. Open a part first.";

    /// <summary>Shown for a sheet metal part that has a flat pattern.</summary>
    public const string SheetMetal =
        "This is a sheet metal part. Its Material Size is worked out from its flat pattern (thickness, width and length) " +
        "and refreshed by the add-in, so Stock Size does not write one.";

    /// <summary>Shown for a sheet metal part that has no flat pattern yet.</summary>
    public const string SheetMetalWithoutFlatPattern =
        "This is a sheet metal part. Its Material Size is worked out from its flat pattern, which it does not have yet. " +
        "Create the flat pattern, then its Material Size is filled in; Stock Size does not write one.";

    /// <summary>
    /// Why Stock Size should not run on the part, or null when it may. <paramref name="isSheetMetal"/> is whether the part's component
    /// definition is a sheet metal one; <paramref name="hasFlatPattern"/> whether that sheet metal part has a flat pattern.
    /// </summary>
    public static string? DeclineReason(bool isSheetMetal, bool hasFlatPattern) =>
        !isSheetMetal ? null : hasFlatPattern ? SheetMetal : SheetMetalWithoutFlatPattern;
}
