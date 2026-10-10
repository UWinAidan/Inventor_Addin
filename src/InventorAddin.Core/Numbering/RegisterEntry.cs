using InventorAddin.Core.Models;

namespace InventorAddin.Core.Numbering;

/// <summary>
/// One row of the part register (spec 02, "The register"): a number handed out, with what it was given to, when and
/// by whom. Text fields are never null; a null passed in, by the constructor or by <c>with</c>, becomes <c>""</c>.
/// </summary>
/// <param name="Number">The number handed out.</param>
/// <param name="Name">The part name, with spaces.</param>
/// <param name="PartType">The part type short code, or <c>""</c>.</param>
/// <param name="Kind">Part, sheet metal part or assembly.</param>
/// <param name="FilePath">Full path of the file when the number was assigned.</param>
/// <param name="AssignedAt">When the number was assigned. The CSV stores it in UTC to the second.</param>
/// <param name="AssignedBy">Windows user name.</param>
/// <param name="Computer">Computer name.</param>
public sealed record RegisterEntry(
    PartNumber Number,
    string Name,
    string PartType,
    DocumentKind Kind,
    string FilePath,
    DateTimeOffset AssignedAt,
    string AssignedBy,
    string Computer)
{
    private readonly string _name = Name ?? "";
    private readonly string _partType = PartType ?? "";
    private readonly string _filePath = FilePath ?? "";
    private readonly string _assignedBy = AssignedBy ?? "";
    private readonly string _computer = Computer ?? "";

    public string Name { get => _name; init => _name = value ?? ""; }

    public string PartType { get => _partType; init => _partType = value ?? ""; }

    public string FilePath { get => _filePath; init => _filePath = value ?? ""; }

    public string AssignedBy { get => _assignedBy; init => _assignedBy = value ?? ""; }

    public string Computer { get => _computer; init => _computer = value ?? ""; }
}
