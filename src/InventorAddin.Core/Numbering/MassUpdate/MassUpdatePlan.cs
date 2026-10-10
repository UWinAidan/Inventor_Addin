using InventorAddin.Core.PartProperties;

namespace InventorAddin.Core.Numbering.MassUpdate;

/// <summary>What Mass Update does to a file's number: register the one it has, or assign a new one.</summary>
public abstract record RegisterAction;

/// <summary>Add the file's existing number to the register. Nothing is written to the file for this.</summary>
/// <param name="Number">The number the file carries.</param>
public sealed record RegisterExisting(PartNumber Number) : RegisterAction;

/// <summary>
/// Assign the next number from the register and write it as the file's Part Number (<see cref="FileUpdate.PartNumberWrite"/>).
/// The file is not renamed.
/// </summary>
public sealed record AssignNew : RegisterAction
{
    public static AssignNew Instance { get; } = new();
}

/// <summary>A number carried by two scanned files: the second is not registered.</summary>
/// <param name="Number">The number both files carry.</param>
/// <param name="FirstPath">The file that keeps the number (the first scanned that carries it).</param>
/// <param name="DuplicatePath">The file that was ticked to register it and is not registered.</param>
public sealed record MassUpdateDuplicate(PartNumber Number, string FirstPath, string DuplicatePath);

/// <summary>
/// The changes to make to one file. The add-in opens the file, does <see cref="Register"/> (if any), makes
/// <see cref="Writes"/>, and saves.
/// </summary>
/// <param name="File">The file as scanned.</param>
/// <param name="Register">The number action, or null when only names are replaced.</param>
/// <param name="Writes">Property writes for name replacement; empty when no names are replaced.</param>
public sealed record FileUpdate(ScannedFile File, RegisterAction? Register, IReadOnlyList<PropertyWrite> Writes)
{
    /// <summary>The write that sets a file's Part Number to <paramref name="number"/>, for <see cref="AssignNew"/>.</summary>
    public static PropertyWrite PartNumberWrite(PartNumber number) =>
        PropertyWrite.SetText(PartPropertyStorage.PartNumber, number.ToString());

    /// <summary>
    /// The register row for this file holding <paramref name="number"/>: the existing number for
    /// <see cref="RegisterExisting"/>, or the number picked for <see cref="AssignNew"/>. Name is
    /// <see cref="PartName.FromFileName"/> of the path; the part type is the listed code when the file's code is in
    /// <see cref="PartTypes"/> (any case), otherwise the file's code trimmed.
    /// </summary>
    public RegisterEntry EntryFor(PartNumber number, DateTimeOffset assignedAt, string assignedBy, string computer)
    {
        var request = RequestFor();
        return new(number, request.Name, request.PartType, request.Kind, request.FilePath, assignedAt, assignedBy, computer);
    }

    /// <summary>
    /// What <see cref="RegisterStore"/> needs to register or assign a number for this file: the name, part type, kind
    /// and path that <see cref="EntryFor"/> puts in the row.
    /// </summary>
    public RegisterRequest RequestFor() =>
        new(
            PartName.FromFileName(File.FullPath),
            PartTypes.Find(File.PartType)?.Code ?? PropertyValues.NormaliseText(File.PartType),
            File.Kind,
            File.FullPath);

    /// <summary>True when the file itself must be opened and saved: a number to write, or names to replace.</summary>
    public bool ChangesFile => Register is AssignNew || Writes.Count > 0;
}

/// <summary>
/// The list of changes Mass Update makes (spec 02, "Commands: Mass Update"): one <see cref="FileUpdate"/> per file
/// with something to do, in the order the files were given, and the numbers not registered because another file
/// carries them.
/// </summary>
public sealed record MassUpdatePlan(IReadOnlyList<FileUpdate> Updates, IReadOnlyList<MassUpdateDuplicate> Duplicates)
{
    /// <summary>
    /// The plan for <paramref name="files"/>, each with the actions ticked on it. An action that does not apply to
    /// the file (see <see cref="ScannedFileState.For"/>) is ignored. Replace names sets Designer, and Detailer, to
    /// <paramref name="defaultDesigner"/>, each only where it meant the user (the Windows user name or one of the Other names); with a blank
    /// <paramref name="defaultDesigner"/> it is not offered and is ignored. A number carried, unregistered, by an
    /// earlier file in <paramref name="files"/> (ticked or not) is not registered for a later one; the later file is
    /// reported in <see cref="Duplicates"/> instead. A file gets a <see cref="FileUpdate"/> only when it has at least
    /// one action left.
    /// </summary>
    public static MassUpdatePlan Build(
        IEnumerable<(ScannedFileState state, MassUpdateActions ticked)> files,
        string? defaultDesigner)
    {
        ArgumentNullException.ThrowIfNull(files);

        var designer = PropertyValues.NormaliseText(defaultDesigner);
        var updates = new List<FileUpdate>();
        var duplicates = new List<MassUpdateDuplicate>();
        var firstCarrier = new Dictionary<PartNumber, string>();

        foreach (var (state, ticked) in files)
        {
            ArgumentNullException.ThrowIfNull(state, nameof(files));

            var duplicate = CheckCarrier(state, firstCarrier);
            var isFirstCarrier = duplicate is null;
            if (duplicate is not null && IsTickedAndOffered(state, ticked, MassUpdateActions.Register))
            {
                duplicates.Add(duplicate);
            }

            RegisterAction? register = null;
            if (isFirstCarrier && IsTickedAndOffered(state, ticked, MassUpdateActions.Register))
            {
                register = new RegisterExisting(state.Number!.Value);
            }
            else if (IsTickedAndOffered(state, ticked, MassUpdateActions.Assign))
            {
                register = AssignNew.Instance;
            }

            var writes = new List<PropertyWrite>();
            if (designer.Length > 0 && IsTickedAndOffered(state, ticked, MassUpdateActions.ReplaceNames))
            {
                if (state.DesignerIsMe)
                {
                    writes.Add(PropertyWrite.SetText(PartPropertyStorage.Designer, designer));
                }

                if (state.DetailerIsMe)
                {
                    writes.Add(PropertyWrite.SetText(PartPropertyStorage.Detailer, designer));
                }
            }

            if (register is not null || writes.Count > 0)
            {
                updates.Add(new FileUpdate(state.File, register, writes.AsReadOnly()));
            }
        }

        return new MassUpdatePlan(updates.AsReadOnly(), duplicates.AsReadOnly());
    }

    /// <summary>
    /// The scanned files that carry an unregistered number an earlier file in <paramref name="states"/> already
    /// carries, by path (ignoring case), each with the file that keeps the number. The same rule as
    /// <see cref="Build"/>: the first file in scan order keeps the number, whether or not it can be changed (it may be
    /// open in Inventor). Mass Update shows these at scan time and offers no Register on them (task 048).
    /// </summary>
    public static IReadOnlyDictionary<string, MassUpdateDuplicate> ScanDuplicates(IEnumerable<ScannedFileState> states)
    {
        ArgumentNullException.ThrowIfNull(states);

        var firstCarrier = new Dictionary<PartNumber, string>();
        var duplicates = new Dictionary<string, MassUpdateDuplicate>(StringComparer.OrdinalIgnoreCase);
        foreach (var state in states)
        {
            ArgumentNullException.ThrowIfNull(state, nameof(states));
            if (CheckCarrier(state, firstCarrier) is { } duplicate)
            {
                duplicates[state.File.FullPath] = duplicate;
            }
        }

        return duplicates;
    }

    /// <summary>
    /// For a file carrying an unregistered number: the duplicate when an earlier file carries it, otherwise null, and
    /// the file is recorded as the number's first carrier. Null for any other file.
    /// </summary>
    private static MassUpdateDuplicate? CheckCarrier(ScannedFileState state, Dictionary<PartNumber, string> firstCarrier)
    {
        if (state.NumberState != NumberState.WellFormedUnregistered || state.Number is not { } carried)
        {
            return null;
        }

        if (firstCarrier.TryGetValue(carried, out var firstPath))
        {
            return new MassUpdateDuplicate(carried, firstPath, state.File.FullPath);
        }

        firstCarrier.Add(carried, state.File.FullPath);
        return null;
    }

    private static bool IsTickedAndOffered(ScannedFileState state, MassUpdateActions ticked, MassUpdateActions action) =>
        (ticked & action) == action && state.Offers(action);
}
