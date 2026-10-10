using InventorAddin.Core.Numbering;

namespace InventorAddin.Core.PartProperties;

/// <summary>
/// The part name, which follows the file name (spec 07, "Part name is linked to the file name"; spec 02,
/// "File names and the part name").
/// </summary>
public static class PartName
{
    /// <summary>
    /// The part name read from a file name: the file name without folder or last extension; if it contains a comma,
    /// the text after the first comma; then underscores shown as spaces, trimmed. So
    /// <c>C:\Work\AWB-10001,mounting_bracket.ipt</c> gives <c>mounting bracket</c>, and an old file without a comma,
    /// <c>Old_bracket.v2.ipt</c>, gives <c>Old bracket.v2</c>. Null or blank (a never-saved file) gives <c>""</c>.
    /// Both <c>\</c> and <c>/</c> count as folder separators on every platform.
    /// </summary>
    public static string FromFileName(string? fullFileName)
    {
        var stem = PartFileName.Stem(fullFileName);
        var comma = stem.IndexOf(PartFileName.NumberSeparator);
        var name = comma >= 0 ? stem[(comma + 1)..] : stem;
        return name.Replace('_', ' ').Trim();
    }
}
