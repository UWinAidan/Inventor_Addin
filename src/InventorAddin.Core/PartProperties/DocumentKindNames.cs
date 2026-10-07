using InventorAddin.Core.Models;

namespace InventorAddin.Core.PartProperties;

/// <summary>The name of each kind of file, as the window headers show it (spec 08, "Part Properties").</summary>
public static class DocumentKindNames
{
    /// <summary>The name for a kind the add-in does not recognise.</summary>
    public const string UnknownText = "Document";

    /// <summary>
    /// <c>Part</c>, <c>Sheet metal part</c>, <c>Assembly</c>, <c>Weldment</c>, <c>Drawing</c> or
    /// <c>Presentation</c>; <see cref="UnknownText"/> for <see cref="DocumentKind.Unknown"/> or an undefined value.
    /// </summary>
    public static string For(DocumentKind kind) => kind switch
    {
        DocumentKind.Part => "Part",
        DocumentKind.SheetMetalPart => "Sheet metal part",
        DocumentKind.Assembly => "Assembly",
        DocumentKind.WeldmentAssembly => "Weldment",
        DocumentKind.Drawing => "Drawing",
        DocumentKind.Presentation => "Presentation",
        _ => UnknownText,
    };
}
