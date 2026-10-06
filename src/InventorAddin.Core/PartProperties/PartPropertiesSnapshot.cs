using InventorAddin.Core.Models;

namespace InventorAddin.Core.PartProperties;

/// <summary>Why the Part Properties window cannot edit a file (spec 07, "Files that cannot be edited").</summary>
public enum EditBlock
{
    /// <summary>The file can be edited.</summary>
    None,

    /// <summary>The file is read-only on disk.</summary>
    ReadOnlyFile,

    /// <summary>Inventor will not let the document be modified, for example a library file.</summary>
    NotModifiable,
}

/// <summary>
/// What the add-in read from a file for the Part Properties window. Values are as read, not normalised.
/// This is deliberately separate from (and smaller than) <see cref="ModelData"/>.
/// </summary>
public sealed record PartPropertiesSnapshot
{
    public const string ReadOnlyFileMessage = "This file is read-only, so its properties cannot be changed here.";
    public const string NotModifiableMessage = "This file cannot be modified (for example a library file), so its properties cannot be changed here.";

    /// <summary>Full path of the file; <c>""</c> for a file that has never been saved.</summary>
    public string FullFileName { get; init; } = string.Empty;

    public DocumentKind DocumentKind { get; init; }

    public string? PartNumber { get; init; }

    public string? Description { get; init; }

    /// <summary>The custom <c>Part Type</c> property: a short code from <see cref="PartTypes"/>, or anything typed elsewhere.</summary>
    public string? PartType { get; init; }

    public string? Designer { get; init; }

    public string? Detailer { get; init; }

    /// <summary>The standard Cost property; null when it could not be read.</summary>
    public decimal? Cost { get; init; }

    public string? Material { get; init; }

    public string? Finish { get; init; }

    /// <summary>Mass formatted in the document's units; null when it could not be read (the window shows a dash).</summary>
    public string? WeightDisplay { get; init; }

    public EditBlock EditBlock { get; init; }

    /// <summary>The line the window shows for <paramref name="block"/>, or null for <see cref="EditBlock.None"/>.</summary>
    public static string? EditBlockMessage(EditBlock block) => block switch
    {
        EditBlock.None => null,
        EditBlock.ReadOnlyFile => ReadOnlyFileMessage,
        EditBlock.NotModifiable => NotModifiableMessage,
        _ => throw new ArgumentOutOfRangeException(nameof(block), block, null),
    };
}
