using InventorAddin.Core.Numbering;

namespace InventorAddin.Core.PartProperties;

/// <summary>
/// The part name, which follows the file name (spec 07, "Part name is linked to the file name"; spec 02,
/// "File names and the part name").
/// </summary>
public static class PartName
{
    /// <summary>
    /// The part name read from a file name: the file name without folder or last extension; when the text after its
    /// last comma is a part number, the text before that comma, otherwise all of it (the split
    /// <see cref="PartFileName.TryReadNumber"/> uses too); then underscores shown as spaces, trimmed, case kept. So
    /// <c>C:\Work\MOUNTING_BRACKET,AWB-10005.ipt</c> gives <c>MOUNTING BRACKET</c>; a file in the old number-first
    /// order, <c>AWB-10005,BRACKET.ipt</c>, gives <c>AWB-10005,BRACKET</c> (Default, spec 02); and an old file without
    /// a number, <c>Old_bracket.v2.ipt</c>, gives <c>Old bracket.v2</c>. Null or blank (a never-saved file) gives
    /// <c>""</c>. Both <c>\</c> and <c>/</c> count as folder separators on every platform.
    /// </summary>
    public static string FromFileName(string? fullFileName)
    {
        PartFileName.SplitStem(PartFileName.Stem(fullFileName), out var name);
        return name.Replace('_', ' ').Trim();
    }
}
