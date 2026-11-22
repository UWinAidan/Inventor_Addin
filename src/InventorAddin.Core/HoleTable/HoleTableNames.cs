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

    /// <summary>The attribute (a double, in cm) that holds the table's drawn width (task 220).</summary>
    public const string WidthAttributeName = "WidthCm";

    /// <summary>The readable sheet name when the table was drawn, for display only.</summary>
    public const string SheetNameAttributeName = "SheetName";

    /// <summary>The readable view name when the table was drawn, for display only.</summary>
    public const string ViewNameAttributeName = "ViewName";

    /// <summary>The attribute set on each placed table symbol that holds the wizard's state (task 166). Not the same set as <see cref="DrawingAttributeSetName"/> (on the definition) or <c>HoleTableMarks.AttributeSetName</c> (on tags and marks).</summary>
    public const string StateAttributeSetName = Branding.ShortName + "_HoleTable";

    /// <summary>The attribute (a string) that holds the text <c>HoleTableStateJson.Write</c> makes.</summary>
    public const string StateAttributeName = "State";

    /// <summary>
    /// The attribute set on the sheet that keeps each view's state, one attribute per view, so Edit Table still lists a
    /// view whose tables were all deleted (task 166). Distinct from the table state set, the key set and the mark set.
    /// </summary>
    public const string SheetRecordAttributeSetName = Branding.ShortName + "_HoleTableViews";

    /// <summary>The sheet record's attribute for a view: the view key with a letter in front, so a key that starts with a digit is still a safe name.</summary>
    public static string SheetRecordAttributeName(string viewKey) => "View_" + Clean(viewKey);

    public const string UpdateTransactionName = Branding.ShortName + " update hole tables";

    public const string CreateTransactionName = Branding.ShortName + " create hole tables";

    public const string AlignTransactionName = Branding.ShortName + " align hole tables";

    public static string GenerateTransactionName(HoleTableKind kind) =>
        Branding.ShortName + " " + Suffix(kind).ToLowerInvariant() + " hole table";

    /// <summary>
    /// The definition's name: <c>AWB_HoleTable_&lt;sheetKey&gt;_&lt;viewKey&gt;_Precision</c> (or <c>_Clearance</c>), from the stable keys of task 163, so a rename never changes it.
    /// A clearance table cut into parts (task 220) names part 2 onward with a suffix: <c>..._Clearance_2</c>; part 1 keeps the plain name.
    /// </summary>
    public static string Definition(string sheetKey, string viewKey, HoleTableKind kind, int part = 1) =>
        DefinitionPrefix + Clean(sheetKey) + "_" + Clean(viewKey) + "_" + Suffix(kind) + (part > 1 ? "_" + part : "");

    /// <summary>The part number (1 for the plain name) when <paramref name="definitionName"/> is a part of the view's table of this kind; 0 when it is not.</summary>
    public static int PartOf(string? definitionName, string sheetKey, string viewKey, HoleTableKind kind)
    {
        var plain = Definition(sheetKey, viewKey, kind);
        if (definitionName == plain)
        {
            return 1;
        }

        if (definitionName is not null && definitionName.StartsWith(plain + "_", StringComparison.Ordinal)
            && int.TryParse(definitionName.AsSpan(plain.Length + 1), out var part) && part >= 2
            && definitionName == Definition(sheetKey, viewKey, kind, part))
        {
            return part;
        }

        return 0;
    }

    /// <summary>True when the name is any part of either of the view's tables.</summary>
    public static bool IsViewTable(string? definitionName, string sheetKey, string viewKey) =>
        PartOf(definitionName, sheetKey, viewKey, HoleTableKind.Precision) > 0
        || PartOf(definitionName, sheetKey, viewKey, HoleTableKind.Clearance) > 0;

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
