using InventorAddin.Core.Models;
using InventorAddin.Core.PartProperties;

namespace InventorAddin.Core.Numbering;

/// <summary>The kinds of file New Part creates (spec 02, "Commands: New Part").</summary>
public enum NewFileKind
{
    Part,
    SheetMetalPart,
    Assembly,
}

/// <summary>The display name, file extension and <see cref="DocumentKind"/> of each <see cref="NewFileKind"/>.</summary>
public static class NewFileKinds
{
    public const string PartExtension = ".ipt";
    public const string AssemblyExtension = ".iam";

    /// <summary>Every kind, in the order the dialog lists them.</summary>
    public static IReadOnlyList<NewFileKind> All { get; } =
        Array.AsReadOnly(new[] { NewFileKind.Part, NewFileKind.SheetMetalPart, NewFileKind.Assembly });

    /// <summary>The matching <see cref="DocumentKind"/>. Throws for an undefined value.</summary>
    public static DocumentKind DocumentKindOf(this NewFileKind kind) => kind switch
    {
        NewFileKind.Part => DocumentKind.Part,
        NewFileKind.SheetMetalPart => DocumentKind.SheetMetalPart,
        NewFileKind.Assembly => DocumentKind.Assembly,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Not a kind of file New Part creates."),
    };

    /// <summary><c>Part</c>, <c>Sheet metal part</c> or <c>Assembly</c>, as <see cref="DocumentKindNames"/> names them.</summary>
    public static string DisplayName(this NewFileKind kind) => DocumentKindNames.For(kind.DocumentKindOf());

    /// <summary>The file extension with its dot: <c>.ipt</c> for both kinds of part, <c>.iam</c> for an assembly.</summary>
    public static string Extension(this NewFileKind kind) => kind switch
    {
        NewFileKind.Part or NewFileKind.SheetMetalPart => PartExtension,
        NewFileKind.Assembly => AssemblyExtension,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Not a kind of file New Part creates."),
    };
}
