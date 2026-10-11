using InventorAddin.Core.Numbering;

namespace InventorAddin.Core.ViewModels;

/// <summary>
/// The file name preview and the first problem with it, shared by the dialogs that make a new numbered file (New Part,
/// Save As Copy), so both check the name, the folder and the path the same way.
/// </summary>
/// <param name="FileName">
/// The file name with <see cref="NumberPlaceholder"/> for the number, for example
/// <c>MOUNTING_BRACKET,AWB-?????.ipt</c>; empty while the name is invalid.
/// </param>
/// <param name="Problem">
/// The first problem: the part name (<see cref="PartFileName.ValidateName"/>), then a blank folder
/// (<see cref="ChooseFolderMessage"/>), then the path length (<see cref="PartFileName.ValidatePath"/>). Null when there
/// is none.
/// </param>
public sealed record NewFileNameCheck(string FileName, string? Problem)
{
    /// <summary>Stands in for the part number in the preview. As long as a real number, so the path check holds.</summary>
    public const string NumberPlaceholder = "AWB-?????";

    public const string ChooseFolderMessage = "Choose a folder.";

    /// <summary>The preview and first problem for <paramref name="partName"/> as typed, in <paramref name="folder"/>.</summary>
    /// <param name="extension">With its dot, for example <c>.ipt</c>.</param>
    public static NewFileNameCheck For(string? partName, string extension, string? folder)
    {
        var trimmedFolder = folder?.Trim() ?? string.Empty;
        var nameProblem = PartFileName.ValidateName(partName);

        var fileName = string.Empty;
        if (nameProblem is null)
        {
            // The name part as Build writes it, then the placeholder where the number goes.
            var withoutNumber = PartFileName.Build(null, partName!, extension);
            fileName = withoutNumber[..^extension.Length] + PartFileName.NumberSeparator + NumberPlaceholder + extension;
        }

        var problem = nameProblem
            ?? (trimmedFolder.Length == 0 ? ChooseFolderMessage : null)
            ?? PartFileName.ValidatePath(trimmedFolder, fileName);

        return new NewFileNameCheck(fileName, problem);
    }
}
