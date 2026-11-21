namespace InventorAddin.Core.HoleTable;

/// <summary>
/// The key the add-in writes on each drawing sheet and view the hole table wizard lists (task 163), so a sheet or view
/// is found again after it is renamed or reordered.
/// </summary>
public static class HoleTableKeys
{
    /// <summary>The attribute set's name on the sheet or view.</summary>
    public const string AttributeSetName = Branding.ShortName + "_HoleTableKey";

    /// <summary>The attribute that holds the key.</summary>
    public const string AttributeName = "Key";

    /// <summary>The undo entry's name for writing keys.</summary>
    public const string TransactionName = Branding.ShortName + " hole table keys";
}
