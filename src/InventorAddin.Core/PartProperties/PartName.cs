namespace InventorAddin.Core.PartProperties;

/// <summary>The part name, which follows the file name (spec 07, "Part name is linked to the file name").</summary>
public static class PartName
{
    /// <summary>
    /// The file name without folder or extension, or <c>""</c> for null or blank (a never-saved file).
    /// Both <c>\</c> and <c>/</c> count as folder separators on every platform, so
    /// <c>C:\Work\Bracket.v2.ipt</c> gives <c>Bracket.v2</c> on Linux too. Only the last extension is removed.
    /// </summary>
    public static string FromFileName(string? fullFileName)
    {
        if (string.IsNullOrWhiteSpace(fullFileName))
        {
            return string.Empty;
        }

        var name = fullFileName[(fullFileName.LastIndexOfAny(new[] { '\\', '/' }) + 1)..];
        var dot = name.LastIndexOf('.');
        return dot >= 0 ? name[..dot] : name;
    }
}
