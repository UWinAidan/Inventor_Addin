using InventorAddin.Core.Models;

namespace InventorAddin.Core.PartProperties;

/// <summary>One entry in the part type list: the short code stored in the file and its full name.</summary>
public sealed record PartType(string Code, string FullName)
{
    /// <summary>Text for a dropdown entry, for example <c>"PM: Purchased, modified"</c>.</summary>
    public string DisplayText => $"{Code}: {FullName}";
}

/// <summary>
/// The part type codes from spec 07, in the spec's order. The short code is what is stored in the
/// <c>Part Type</c> custom property. In this version the type is only stored; it decides nothing else.
/// </summary>
public static class PartTypes
{
    /// <summary>Shown by <see cref="FullNameFor"/> for a code that is not in <see cref="All"/>.</summary>
    public const string UnknownCodeText = "Unknown code";

    public static readonly PartType Purchased = new("P", "Purchased");
    public static readonly PartType Manufactured = new("M", "Manufactured");
    public static readonly PartType PurchasedModified = new("PM", "Purchased, modified");
    public static readonly PartType Fastener = new("F", "Fastener");
    public static readonly PartType Assembly = new("A", "Assembly");
    public static readonly PartType Weldment = new("W", "Weldment");
    public static readonly PartType Reference = new("R", "Reference: shown for context, not bought or made");
    public static readonly PartType CustomerSupplied = new("C", "Customer supplied");

    private static readonly PartType[] Entries =
    {
        Purchased,
        Manufactured,
        PurchasedModified,
        Fastener,
        Assembly,
        Weldment,
        Reference,
        CustomerSupplied,
    };

    /// <summary>Every part type, in the order the dropdown shows them.</summary>
    public static IReadOnlyList<PartType> All { get; } = Array.AsReadOnly(Entries);

    /// <summary>
    /// The entry for <paramref name="code"/>, ignoring surrounding whitespace and case, or null when the code is
    /// blank or not in the list.
    /// </summary>
    public static PartType? Find(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        var trimmed = code.Trim();
        return Entries.FirstOrDefault(entry => string.Equals(entry.Code, trimmed, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// The type pre-filled when a file's type is blank: A for an assembly, W for a weldment, null otherwise.
    /// </summary>
    public static PartType? DefaultFor(DocumentKind kind) => kind switch
    {
        DocumentKind.Assembly => Assembly,
        DocumentKind.WeldmentAssembly => Weldment,
        _ => null,
    };

    /// <summary>
    /// The full name for <paramref name="code"/>: empty for a blank code, <see cref="UnknownCodeText"/> for a code
    /// not in the list (the property can hold any text if it was edited outside the add-in).
    /// </summary>
    public static string FullNameFor(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return string.Empty;
        }

        return Find(code)?.FullName ?? UnknownCodeText;
    }
}
