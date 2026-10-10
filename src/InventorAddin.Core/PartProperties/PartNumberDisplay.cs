namespace InventorAddin.Core.PartProperties;

/// <summary>
/// The placeholder the Part Properties window shows when a file has no part number to show (spec 02, "Display in
/// Part Properties"; spec 08, "Part number display").
/// </summary>
/// <remarks>
/// The rule itself lives in <see cref="ViewModels.PartPropertiesViewModel"/>: a Part Number in the
/// <see cref="Numbering.PartNumber"/> format is shown as the number, anything else (blank, or Inventor's file-name
/// fallback) as <see cref="NotAssignedText"/>. The stand-in rule that compared the Part Number with the file name was
/// removed in task 039.
/// </remarks>
public static class PartNumberDisplay
{
    public const string NotAssignedText = "Not assigned";
}
