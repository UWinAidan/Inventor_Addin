using InventorAddin.Core.Logging;
using InventorAddin.Core.Models;
using InventorAddin.Core.PartProperties;

namespace InventorAddin.Core.Numbering;

/// <summary>
/// What <see cref="SaveAsCopyCreator"/> needs from Inventor: the open original, and the copy once it is on disk. The
/// add-in supplies it; tests use a fake.
/// </summary>
public interface ISaveAsCopySource
{
    /// <summary>
    /// Saves a copy of the open original, with any unsaved edits, under <paramref name="fullPath"/>. The original keeps
    /// its name and is not changed (spike 032: <c>SaveAs(path, true)</c>).
    /// </summary>
    void SaveCopyAs(string fullPath);

    /// <summary>Opens the copy saved at <paramref name="fullPath"/>, visible, as the active document.</summary>
    ICopiedDocument OpenCopy(string fullPath);
}

/// <summary>The copy <see cref="ISaveAsCopySource.OpenCopy"/> opened.</summary>
public interface ICopiedDocument
{
    /// <summary>Writes the properties; throws when one cannot be written.</summary>
    void WriteProperties(IReadOnlyList<PropertyWrite> writes);

    /// <summary>Saves the copy under its own name.</summary>
    void Save();

    /// <summary>Closes the copy without saving it.</summary>
    void CloseWithoutSaving();
}

/// <summary>
/// What the Save As Copy dialog asks the command to make. <see cref="OriginalFullPath"/> and <see cref="Kind"/> are the
/// open original's; <see cref="PartName"/> is normalised (<see cref="PartFileName.NormaliseName"/>) and
/// <see cref="PartType"/> is the code in the list's spelling, any other code trimmed, or <c>""</c>.
/// </summary>
public sealed record SaveAsCopyRequest(string OriginalFullPath, DocumentKind Kind, string PartName, string PartType);

/// <summary>
/// The Save As Copy steps from spec 02 ("Commands: Save As Copy"), in this order: the original must be a saved part or
/// assembly; check the name and the original's folder; assign the next number through the register, checking that no
/// file with the new name exists before its row is written; check again; save a copy of the original under
/// <see cref="PartFileName.Build"/> in the original's folder; open the copy; write <see cref="CreationProperties"/>
/// on it; save it. Everything else on the copy is kept from the original (Default). The folder check, the assignment
/// and their messages are <see cref="NewPartCreator"/>'s, so both commands fail the same way.
/// </summary>
/// <remarks>
/// Rows are never removed, so a failure after the number was assigned leaves it in the register, logs the exception and
/// says which number was used. The half-made copy is closed without saving and its file deleted (Default), so no file
/// named with the new number is left holding the original's Part Number. The file is deleted only when it was not
/// there before the copy was saved.
/// </remarks>
public sealed class SaveAsCopyCreator
{
    public const string UnsavedMessage = "Save this file first: the copy is made from the saved file, in its folder.";

    public const string WrongDocumentMessage = "Save As Copy works on parts and assemblies.";

    /// <summary>The dialog's note for an assembly (task 045).</summary>
    public const string AssemblyNote =
        "Only the assembly file is copied. Its components are shared with the original, not copied.";

    private readonly RegisterStore _store;
    private readonly ISaveAsCopySource _source;
    private readonly string? _defaultDesigner;
    private readonly ILog _log;
    private readonly Func<string, bool> _folderExists;
    private readonly Action<string> _deleteFile;

    /// <param name="store">The session's register.</param>
    /// <param name="source">The open original in Inventor.</param>
    /// <param name="defaultDesigner">Designer and Detailer for the copy, from Settings; blank writes neither.</param>
    /// <param name="log">Where failures are recorded.</param>
    /// <param name="folderExists">Whether a folder exists; by default <see cref="NewPartCreator"/>'s probe with a timeout.</param>
    /// <param name="deleteFile">Deletes a half-made copy; <see cref="File.Delete"/> by default.</param>
    public SaveAsCopyCreator(
        RegisterStore store,
        ISaveAsCopySource source,
        string? defaultDesigner,
        ILog? log = null,
        Func<string, bool>? folderExists = null,
        Action<string>? deleteFile = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(source);

        _store = store;
        _source = source;
        _defaultDesigner = defaultDesigner;
        _log = log ?? NullLog.Instance;
        _folderExists = folderExists ?? NewPartCreator.ProbeFolder;
        _deleteFile = deleteFile ?? File.Delete;
    }

    /// <summary>True for the kinds Save As Copy accepts: parts, sheet metal parts, assemblies and weldments.</summary>
    public static bool CanCopy(DocumentKind kind) =>
        kind is DocumentKind.Part or DocumentKind.SheetMetalPart or DocumentKind.Assembly or DocumentKind.WeldmentAssembly;

    /// <summary>True for an assembly or weldment, whose copy shares its components with the original.</summary>
    public static bool IsAssembly(DocumentKind kind) => kind is DocumentKind.Assembly or DocumentKind.WeldmentAssembly;

    public static string FailedAfterAssignMessage(string number, string reason) =>
        $"Part number {number} was assigned, but the copy could not be made: {reason} The number stays in the register.";

    public static string NotRemovedMessage(string fullPath) =>
        $"The unfinished copy '{fullPath}' could not be removed; delete it by hand.";

    /// <summary>
    /// The extension of <paramref name="fullFileName"/> with its dot, for example <c>.ipt</c>, or <c>""</c>. Both
    /// <c>\</c> and <c>/</c> count as folder separators.
    /// </summary>
    public static string ExtensionOf(string? fullFileName)
    {
        var path = fullFileName?.Trim() ?? string.Empty;
        var name = path[(path.LastIndexOfAny(['\\', '/']) + 1)..];
        var dot = name.LastIndexOf('.');
        return dot > 0 && dot < name.Length - 1 ? name[dot..] : string.Empty;
    }

    /// <summary>Runs the steps for <paramref name="request"/>. The copy is left open; the original is not touched.</summary>
    /// <exception cref="NewPartException">A step failed; the message says why and whether a number was used.</exception>
    public NewPartResult Copy(SaveAsCopyRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        // 1. A saved part or assembly.
        var original = request.OriginalFullPath?.Trim() ?? string.Empty;
        var folder = NewPartCreator.DefaultFolder(original, null);
        var extension = ExtensionOf(original);
        if (original.Length == 0 || folder.Length == 0 || extension.Length == 0)
        {
            throw new NewPartException(UnsavedMessage);
        }

        if (!CanCopy(request.Kind))
        {
            throw new NewPartException(WrongDocumentMessage);
        }

        // 2. The name, then the folder.
        var nameProblem = PartFileName.ValidateName(request.PartName);
        if (nameProblem is not null)
        {
            throw new NewPartException(nameProblem + " Nothing was assigned.");
        }

        var partName = PartFileName.NormaliseName(request.PartName);
        var partType = request.PartType ?? string.Empty;
        NewPartCreator.CheckFolder(folder, _folderExists);

        // 3. The number, with the file name checked before its row is written.
        var (assignment, path) = NewPartCreator.AssignNumber(
            _store,
            _log,
            "a copy",
            number => NewPartCreator.FullPathFor(folder, number, partName, extension),
            fullPath => new RegisterRequest(partName, partType, request.Kind, fullPath));

        var assigned = assignment.PartNumber.ToString();

        // 4. Check again, save the copy, open it, write, save. Any failure from here leaves the number in the register.
        var mayRemove = false;
        ICopiedDocument? copy = null;
        try
        {
            if (File.Exists(path))
            {
                throw new NewPartException(NewPartCreator.ExistsAfterAssignMessage(assigned, path), assigned);
            }

            mayRemove = true;
            _source.SaveCopyAs(path);
            copy = _source.OpenCopy(path);
            copy.WriteProperties(CreationProperties.Build(assignment.PartNumber, partName, partType, _defaultDesigner));
            copy.Save();
        }
        catch (Exception ex)
        {
            _log.Error(
                $"Part number {assigned} was assigned for the copy '{path}', but the copy could not be made. " +
                "The register entry stays.", ex);

            var leftOver = CleanUp(copy, mayRemove, path, assigned);

            if (ex is NewPartException)
            {
                throw;
            }

            var message = FailedAfterAssignMessage(assigned, ex.Message);
            if (leftOver)
            {
                message += " " + NotRemovedMessage(path);
            }

            throw new NewPartException(message, assigned, ex);
        }

        _log.Info($"Save As Copy: '{original}' copied to '{path}' as {assigned}.");
        var duplicates = assignment.Duplicates.Select(PartNumberAssignment.DescribeDuplicate).ToList();
        return new NewPartResult(assigned, path, assignment.Offline, duplicates.AsReadOnly());
    }

    /// <summary>
    /// Closes the half-made copy and deletes its file when this command made it. True when a file is left behind.
    /// </summary>
    private bool CleanUp(ICopiedDocument? copy, bool mayRemove, string path, string assigned)
    {
        if (copy is not null)
        {
            try
            {
                copy.CloseWithoutSaving();
            }
            catch (Exception closeEx)
            {
                _log.Error($"The half-made copy for part number {assigned} could not be closed.", closeEx);
            }
        }

        if (!mayRemove || !File.Exists(path))
        {
            return false;
        }

        try
        {
            _deleteFile(path);
            return false;
        }
        catch (Exception deleteEx)
        {
            _log.Error($"The half-made copy '{path}' could not be deleted.", deleteEx);
            return true;
        }
    }
}
