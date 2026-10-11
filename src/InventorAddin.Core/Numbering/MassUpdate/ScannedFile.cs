using InventorAddin.Core.Models;
using InventorAddin.Core.PartProperties;

namespace InventorAddin.Core.Numbering.MassUpdate;

/// <summary>
/// What the add-in read from one file in the Mass Update scan (spec 02, "Commands: Mass Update"). Text fields are
/// never null; a null passed in becomes <c>""</c>.
/// </summary>
/// <param name="FullPath">The file's full path.</param>
/// <param name="PartNumber">The Part Number property as read.</param>
/// <param name="Designer">The Designer property as read.</param>
/// <param name="Detailer">The Detailer property as read.</param>
/// <param name="PartType">The Part Type property as read (a short code, or <c>""</c>).</param>
/// <param name="Kind">Part, sheet metal part, assembly or weldment.</param>
/// <param name="OpenInInventor">True when the file is open in Inventor; it is then skipped.</param>
/// <param name="ReadError">Why the file could not be read, or null when it was read.</param>
/// <param name="Author">The Author property as read (spec 02, Aidan, 2026-10-11).</param>
public sealed record ScannedFile(
    string FullPath,
    string PartNumber,
    string Designer,
    string Detailer,
    string PartType,
    DocumentKind Kind,
    bool OpenInInventor = false,
    string? ReadError = null,
    string Author = "")
{
    private readonly string _fullPath = FullPath ?? "";
    private readonly string _partNumber = PartNumber ?? "";
    private readonly string _designer = Designer ?? "";
    private readonly string _detailer = Detailer ?? "";
    private readonly string _partType = PartType ?? "";
    private readonly string _author = Author ?? "";

    public string FullPath { get => _fullPath; init => _fullPath = value ?? ""; }

    public string PartNumber { get => _partNumber; init => _partNumber = value ?? ""; }

    public string Designer { get => _designer; init => _designer = value ?? ""; }

    public string Detailer { get => _detailer; init => _detailer = value ?? ""; }

    public string PartType { get => _partType; init => _partType = value ?? ""; }

    public string Author { get => _author; init => _author = value ?? ""; }
}

/// <summary>Whether a scanned file has a part number (spec 02, "Has a number").</summary>
public enum NumberState
{
    /// <summary>The Part Number is in the format and the register holds it.</summary>
    Registered,

    /// <summary>The Part Number is in the format but the register does not hold it. Mass Update can register it.</summary>
    WellFormedUnregistered,

    /// <summary>The Part Number is blank or not in the format. Mass Update can assign a new number.</summary>
    None,
}

/// <summary>The actions Mass Update can take on a file. A set of flags, so a row can tick several.</summary>
[Flags]
public enum MassUpdateActions
{
    None = 0,

    /// <summary>Add the file's existing, well-formed number to the register.</summary>
    Register = 1,

    /// <summary>Assign a new number to a file without one. The file is not renamed.</summary>
    Assign = 2,

    /// <summary>
    /// Replace a Designer, Author or Detailer that means the user (the Windows user name or one of the Other names)
    /// with the Default designer.
    /// </summary>
    ReplaceNames = 4,
}

/// <summary>What Mass Update knows about one scanned file: its number state, its names, and which actions apply.</summary>
/// <param name="File">The file as scanned.</param>
/// <param name="NumberState">Whether the file has a number.</param>
/// <param name="Number">The parsed Part Number when it is in the format; null otherwise.</param>
/// <param name="DesignerIsMe">
/// True when Designer means the user: it equals the Windows user name or one of the Other names
/// (<see cref="PersonNames.MeansMe"/>).
/// </param>
/// <param name="DetailerIsMe">True when Detailer means the user, as <paramref name="DesignerIsMe"/>.</param>
/// <param name="AuthorIsMe">True when Author means the user, as <paramref name="DesignerIsMe"/>.</param>
/// <param name="Actions">The actions that apply. None when <see cref="BlockedReason"/> is set.</param>
/// <param name="BlockedReason">Why the file offers no action (open in Inventor, or the read error), or null.</param>
public sealed record ScannedFileState(
    ScannedFile File,
    NumberState NumberState,
    PartNumber? Number,
    bool DesignerIsMe,
    bool DetailerIsMe,
    bool AuthorIsMe,
    MassUpdateActions Actions,
    string? BlockedReason)
{
    public const string OpenInInventorText = "Open in Inventor; close it first.";

    /// <summary>True when <paramref name="action"/> (one flag) applies to the file.</summary>
    public bool Offers(MassUpdateActions action) => action != MassUpdateActions.None && (Actions & action) == action;

    /// <summary>
    /// The state of <paramref name="file"/> against <paramref name="register"/> and
    /// <paramref name="namesThatMeanMe"/> (<see cref="PersonNames.NamesThatMeanMe"/>). Register applies to a
    /// well-formed, unregistered number; Assign to a file with no number; Replace names when Designer, Detailer or
    /// Author means the user (<see cref="PersonNames.MeansMe"/>, whatever the Default designer is). A file open in
    /// Inventor, or with a read error, offers no action and gives the reason, the open file first.
    /// </summary>
    public static ScannedFileState For(ScannedFile file, PartRegister register, IReadOnlyCollection<string> namesThatMeanMe)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(register);
        ArgumentNullException.ThrowIfNull(namesThatMeanMe);

        PartNumber? number = PartNumber.TryParse(file.PartNumber, out var parsed) ? parsed : null;
        var numberState = number is null
            ? NumberState.None
            : register.Contains(number.Value) ? NumberState.Registered : NumberState.WellFormedUnregistered;

        var designerIsMe = PersonNames.MeansMe(file.Designer, namesThatMeanMe);
        var detailerIsMe = PersonNames.MeansMe(file.Detailer, namesThatMeanMe);
        var authorIsMe = PersonNames.MeansMe(file.Author, namesThatMeanMe);

        var blocked = file.OpenInInventor
            ? OpenInInventorText
            : string.IsNullOrWhiteSpace(file.ReadError) ? null : file.ReadError.Trim();

        var actions = MassUpdateActions.None;
        if (blocked is null)
        {
            actions |= numberState switch
            {
                NumberState.WellFormedUnregistered => MassUpdateActions.Register,
                NumberState.None => MassUpdateActions.Assign,
                _ => MassUpdateActions.None,
            };

            if (designerIsMe || detailerIsMe || authorIsMe)
            {
                actions |= MassUpdateActions.ReplaceNames;
            }
        }

        return new ScannedFileState(file, numberState, number, designerIsMe, detailerIsMe, authorIsMe, actions, blocked);
    }
}
