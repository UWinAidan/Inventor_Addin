using InventorAddin.Core.Logging;
using InventorAddin.Core.Models;
using InventorAddin.Core.PartProperties;

namespace InventorAddin.Core.Numbering;

/// <summary>
/// What Save As and Replace read from the selected occurrence (spec 02, "Commands: Save As and Replace"). The add-in
/// fills it in; <see cref="SaveAsReplacer.Check"/> decides whether the command may go on.
/// </summary>
/// <param name="ComponentFullFileName">The component's file; <c>""</c> when it has never been saved or has no file.</param>
/// <param name="ComponentKind">The component's kind.</param>
/// <param name="ComponentBlock">Whether the component's file is read-only or cannot be modified (a library file).</param>
/// <param name="ChangedAssemblyBlock">
/// The same for the assembly the replace edits: the active assembly for a top-level occurrence, or the sub-assembly
/// that holds it.
/// </param>
/// <param name="IsVirtual">True for a virtual component, which has no file to copy.</param>
/// <param name="IsSuppressed">True for a suppressed occurrence, whose component is not loaded.</param>
/// <param name="SubAssemblyFullFileName">
/// For an occurrence inside a sub-assembly (spike 032: a <c>ComponentOccurrenceProxy</c>), the sub-assembly's file,
/// which is replaced in and saved; <c>""</c> when that sub-assembly has never been saved. Null for a top-level occurrence.
/// </param>
/// <param name="SubAssemblyOccurrenceName">The sub-assembly's occurrence name in the browser, for example <c>Sub:1</c>.</param>
public sealed record ReplaceSelection(
    string ComponentFullFileName,
    DocumentKind ComponentKind,
    EditBlock ComponentBlock,
    EditBlock ChangedAssemblyBlock,
    bool IsVirtual = false,
    string? SubAssemblyFullFileName = null,
    string? SubAssemblyOccurrenceName = null,
    bool IsSuppressed = false)
{
    /// <summary>True when the occurrence is inside a sub-assembly, so the sub-assembly is edited and saved.</summary>
    public bool InSubAssembly => SubAssemblyFullFileName is not null;
}

/// <summary>
/// What <see cref="SaveAsReplacer"/> needs from Inventor: the component to copy (as for Save As Copy), and the selected
/// occurrence to replace. The add-in supplies it; tests use a fake.
/// </summary>
public interface ISaveAsReplaceTarget : ISaveAsCopySource
{
    /// <summary>
    /// Closes the copy <see cref="ISaveAsCopySource.OpenCopy"/> opened (invisibly: the copy is not opened in its own
    /// window), now that it is saved, so the replace loads it from disk and no hidden document is left open.
    /// </summary>
    void CloseCopy();

    /// <summary>
    /// Replaces the selected occurrence only with the file at <paramref name="copyFullPath"/>
    /// (spike 032: <c>Replace(path, false)</c>, which keeps the constraints). Throws when it cannot.
    /// </summary>
    void ReplaceOccurrence(string copyFullPath);

    /// <summary>Saves the sub-assembly the replace edited. Called only for an occurrence inside a sub-assembly.</summary>
    void SaveSubAssembly();
}

/// <summary>
/// The Save As and Replace steps from spec 02 ("Commands: Save As and Replace"): <see cref="Check"/> the selection;
/// make the copy exactly as Save As Copy does (<see cref="SaveAsCopyCreator"/>), in the component's folder; close the
/// copy; replace the selected occurrence only with it (Default, open question 16); and, for an occurrence inside a
/// sub-assembly, save the sub-assembly (Aidan, question 25).
/// </summary>
/// <remarks>
/// A failure while making the copy is <see cref="SaveAsCopyCreator"/>'s, with its messages and clean-up, including
/// the check that the copy kept its new Part Number. Once the copy
/// is saved it is a finished, numbered file, so a failure to replace or to save the sub-assembly leaves the copy on disk
/// and its number in the register (Default), and the message says what to do.
/// </remarks>
public sealed class SaveAsReplacer
{
    public const string SelectOneMessage = "Select one component in the assembly first.";

    public const string WrongDocumentMessage = "Save As and Replace works in an assembly.";

    public const string VirtualComponentMessage =
        "This is a virtual component: it has no file to copy, so it cannot be replaced with a copy.";

    public const string SuppressedComponentMessage =
        "This component is suppressed. Unsuppress it first, then Save As and Replace.";

    /// <summary>The add-in could not read the sub-assembly that holds the selected occurrence.</summary>
    public const string SubAssemblyNotReadMessage =
        "The sub-assembly that holds this component could not be read, so the component is not replaced. Nothing was copied.";

    public const string UnsavedComponentMessage =
        "Save this component first: the copy is made from the saved file, in its folder.";

    public const string WrongComponentMessage = "Save As and Replace works on parts and assemblies.";

    public const string ReadOnlyComponentMessage =
        "This component's file is read-only, so it is not copied and replaced here.";

    public const string NotModifiableComponentMessage =
        "This component cannot be modified (for example a library file), so it is not copied and replaced here.";

    public const string ReadOnlyAssemblyMessage =
        "This assembly's file is read-only, so the component cannot be replaced in it.";

    public const string NotModifiableAssemblyMessage =
        "This assembly cannot be modified, so the component cannot be replaced in it.";

    private readonly SaveAsCopyCreator _creator;
    private readonly ISaveAsReplaceTarget _target;
    private readonly ReplaceSelection _selection;
    private readonly ILog _log;

    /// <param name="store">The session's register.</param>
    /// <param name="target">The selected occurrence and its component in Inventor.</param>
    /// <param name="selection">What was read from the selection; it has passed <see cref="Check"/>.</param>
    /// <param name="defaultDesigner">Designer, Author and Detailer for the copy, from Settings; blank writes none.</param>
    /// <param name="log">Where failures are recorded.</param>
    /// <param name="folderExists">Passed to <see cref="SaveAsCopyCreator"/>.</param>
    /// <param name="deleteFile">Passed to <see cref="SaveAsCopyCreator"/>.</param>
    public SaveAsReplacer(
        RegisterStore store,
        ISaveAsReplaceTarget target,
        ReplaceSelection selection,
        string? defaultDesigner,
        ILog? log = null,
        Func<string, bool>? folderExists = null,
        Action<string>? deleteFile = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(selection);

        _target = target;
        _selection = selection;
        _log = log ?? NullLog.Instance;
        _creator = new SaveAsCopyCreator(store, target, defaultDesigner, _log, folderExists, deleteFile);
    }

    /// <summary>
    /// Null when Save As and Replace may run on <paramref name="selection"/>, otherwise the message saying why not. A
    /// read-only or library component, or a read-only or unmodifiable assembly to edit (the sub-assembly for a nested
    /// occurrence), stops here, before the dialog and before any number is assigned.
    /// </summary>
    public static string? Check(ReplaceSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);

        if (selection.IsVirtual)
            return VirtualComponentMessage;

        if (selection.IsSuppressed)
            return SuppressedComponentMessage;

        if (string.IsNullOrWhiteSpace(selection.ComponentFullFileName))
            return UnsavedComponentMessage;

        if (!SaveAsCopyCreator.CanCopy(selection.ComponentKind))
            return WrongComponentMessage;

        var componentProblem = selection.ComponentBlock switch
        {
            EditBlock.ReadOnlyFile => ReadOnlyComponentMessage,
            EditBlock.NotModifiable => NotModifiableComponentMessage,
            _ => null,
        };
        if (componentProblem is not null)
            return componentProblem;

        if (selection.InSubAssembly)
        {
            var name = SubAssemblyName(selection);
            if (string.IsNullOrWhiteSpace(selection.SubAssemblyFullFileName))
                return UnsavedSubAssemblyMessage(name);

            return selection.ChangedAssemblyBlock switch
            {
                EditBlock.ReadOnlyFile => ReadOnlySubAssemblyMessage(name),
                EditBlock.NotModifiable => NotModifiableSubAssemblyMessage(name),
                _ => null,
            };
        }

        return selection.ChangedAssemblyBlock switch
        {
            EditBlock.ReadOnlyFile => ReadOnlyAssemblyMessage,
            EditBlock.NotModifiable => NotModifiableAssemblyMessage,
            _ => null,
        };
    }

    /// <summary>
    /// The dialog's warning for an occurrence inside a sub-assembly (Aidan, question 25), naming the sub-assembly; null
    /// for a top-level occurrence.
    /// </summary>
    public static string? SubAssemblyWarning(ReplaceSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        if (!selection.InSubAssembly)
            return null;

        var name = SubAssemblyName(selection);
        return $"This component is inside the sub-assembly {name}. It is replaced there and {name} is saved, so " +
               $"every assembly that uses {name} changes too, and Undo cannot put it back.";
    }

    /// <summary>
    /// The sub-assembly as the messages name it: its file name, with its occurrence name when known, for example
    /// <c>FRAME,AWB-10005.iam (Sub:1)</c>. Empty for a top-level occurrence.
    /// </summary>
    public static string SubAssemblyName(ReplaceSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        if (!selection.InSubAssembly)
            return string.Empty;

        var path = selection.SubAssemblyFullFileName!.Trim();
        var file = path[(path.LastIndexOfAny(['\\', '/']) + 1)..];
        var occurrence = selection.SubAssemblyOccurrenceName?.Trim() ?? string.Empty;

        if (file.Length == 0)
            return occurrence.Length == 0 ? "(unnamed)" : occurrence;

        return occurrence.Length == 0 ? file : $"{file} ({occurrence})";
    }

    /// <summary>
    /// The log line written when the selection is not one component (task 060), so a report can be diagnosed from the
    /// log: how many items were selected and the type of each, in order.
    /// </summary>
    /// <param name="count">The selection's count; negative when it could not be read.</param>
    /// <param name="itemTypes">Each selected item's type name, in order; null or blank entries read as <c>(unknown)</c>.</param>
    public static string SelectionNotOneComponentLog(int count, IReadOnlyList<string?> itemTypes)
    {
        ArgumentNullException.ThrowIfNull(itemTypes);

        if (count < 0)
            return "Save As and Replace: the selection could not be read.";

        var prefix = $"Save As and Replace: the selection holds {count} item{(count == 1 ? "" : "s")}";
        if (itemTypes.Count == 0)
            return prefix + ".";

        var types = itemTypes.Select(t => string.IsNullOrWhiteSpace(t) ? "(unknown)" : t.Trim());
        return $"{prefix}: {string.Join(", ", types)}.";
    }

    public static string UnsavedSubAssemblyMessage(string name) =>
        $"Save the sub-assembly {name} first: it is saved after the component in it is replaced.";

    public static string ReadOnlySubAssemblyMessage(string name) =>
        $"The sub-assembly {name} is read-only, so the component cannot be replaced in it. Nothing was copied.";

    public static string NotModifiableSubAssemblyMessage(string name) =>
        $"The sub-assembly {name} cannot be modified (for example a library file), so the component cannot be " +
        "replaced in it. Nothing was copied.";

    public static string ReplaceFailedMessage(string number, string copyPath, string reason) =>
        $"Part number {number} was assigned and the copy '{copyPath}' was made, but the component could not be " +
        $"replaced: {reason} The copy and its number stay; replace the component with it by hand.";

    public static string SubAssemblySaveFailedMessage(string number, string name, string reason) =>
        $"The component was replaced with {number}, but the sub-assembly {name} could not be saved: {reason} " +
        "It is changed in Inventor but not on disk; save it yourself.";

    /// <summary>Runs the steps for <paramref name="request"/>, whose original is the selected component.</summary>
    /// <exception cref="NewPartException">A step failed; the message says why and whether a number was used.</exception>
    public NewPartResult Replace(SaveAsCopyRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var problem = Check(_selection);
        if (problem is not null)
            throw new NewPartException(problem);

        // 1. The copy, as Save As Copy makes it. Its failures carry their own messages and clean-up.
        var result = _creator.Copy(request);
        var assigned = result.PartNumber;

        // 2. Close the saved copy; the replace loads it from disk. A copy that will not close is not a reason to stop.
        try
        {
            _target.CloseCopy();
        }
        catch (Exception ex)
        {
            _log.Warn($"Save As and Replace: the copy '{result.FullPath}' could not be closed before the replace. {ex.Message}");
        }

        // 3. Replace the selected occurrence only.
        try
        {
            _target.ReplaceOccurrence(result.FullPath);
        }
        catch (Exception ex)
        {
            _log.Error(
                $"Save As and Replace: the copy '{result.FullPath}' ({assigned}) was made, but the occurrence could not be " +
                "replaced. The copy and its register entry stay.", ex);
            throw new NewPartException(ReplaceFailedMessage(assigned, result.FullPath, ex.Message), assigned, ex);
        }

        // 4. Save the sub-assembly the replace edited.
        if (_selection.InSubAssembly)
        {
            var name = SubAssemblyName(_selection);
            try
            {
                _target.SaveSubAssembly();
            }
            catch (Exception ex)
            {
                _log.Error($"Save As and Replace: the sub-assembly '{_selection.SubAssemblyFullFileName}' could not be saved.", ex);
                throw new NewPartException(SubAssemblySaveFailedMessage(assigned, name, ex.Message), assigned, ex);
            }

            _log.Info($"Save As and Replace: '{_selection.SubAssemblyFullFileName}' saved with {assigned} in place of '{request.OriginalFullPath}'.");
        }
        else
        {
            _log.Info($"Save As and Replace: the occurrence of '{request.OriginalFullPath}' replaced with {assigned}.");
        }

        return result;
    }
}
