using System.Text;

namespace InventorAddin.Core.HoleTable;

/// <summary>The names the add-in gives the sketched symbol definitions that draw the hole tables, and its undo entries (task 164).</summary>
public static class HoleTableNames
{
    /// <summary>Every table definition's name starts with this.</summary>
    public const string DefinitionPrefix = Branding.ShortName + "_HoleTable_";

    /// <summary>The attribute set on a table's definition that records its drawn height, so Align can stack without a range box.</summary>
    public const string DrawingAttributeSetName = Branding.ShortName + "_HoleTableDrawing";

    /// <summary>The attribute (a double, in cm) that holds the table's drawn height.</summary>
    public const string HeightAttributeName = "HeightCm";

    /// <summary>The readable sheet name when the table was drawn, for display only.</summary>
    public const string SheetNameAttributeName = "SheetName";

    /// <summary>The readable view name when the table was drawn, for display only.</summary>
    public const string ViewNameAttributeName = "ViewName";

    public const string CreateTransactionName = Branding.ShortName + " create hole tables";

    public const string AlignTransactionName = Branding.ShortName + " align hole tables";

    public static string GenerateTransactionName(HoleTableKind kind) =>
        Branding.ShortName + " " + Suffix(kind).ToLowerInvariant() + " hole table";

    /// <summary>The definition's name: <c>AWB_HoleTable_&lt;sheetKey&gt;_&lt;viewKey&gt;_Precision</c> (or <c>_Clearance</c>), from the stable keys of task 163, so a rename never changes it.</summary>
    public static string Definition(string sheetKey, string viewKey, HoleTableKind kind) =>
        DefinitionPrefix + Clean(sheetKey) + "_" + Clean(viewKey) + "_" + Suffix(kind);

    /// <summary>
    /// Keeps letters, digits, underscore and hyphen; every other character (a colon, a space, an apostrophe) becomes an
    /// underscore, so Inventor never refuses the name. A blank name becomes <c>_</c>.
    /// </summary>
    public static string Clean(string? name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return "_";
        }

        var sb = new StringBuilder(name.Length);
        foreach (var c in name)
        {
            sb.Append(char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_');
        }

        return sb.ToString();
    }

    private static string Suffix(HoleTableKind kind) => kind == HoleTableKind.Precision ? "Precision" : "Clearance";
}
