using InventorAddin.Core.Logging;
using InventorAddin.Core.Numbering;

namespace InventorAddin.Core.PartProperties;

/// <summary>
/// The Generate part number steps from spec 02 for one open file: sync with the register, re-check that the file
/// still has no registered number, assign the next number, then write it as the file's Part Number. The register entry
/// is made before the write, and rows are never removed, so a failed write leaves the number in the register; the
/// error and the log say so.
/// </summary>
public sealed class RegisterPartNumberAssigner : IPartNumberAssigner
{
    public const string NotSavedMessage = "Save the file before giving it a part number.";
    public const string AlreadyRegisteredMessage = "This file already has a registered number.";

    private readonly RegisterStore _store;
    private readonly PartPropertiesSnapshot _snapshot;
    private readonly IPropertyWriteTarget _target;
    private readonly ILog _log;

    /// <param name="store">The session's register.</param>
    /// <param name="snapshot">What was read from the file: its path, kind, part type and current Part Number.</param>
    /// <param name="target">Writes the Part Number to the file, as one undo step.</param>
    /// <param name="log">Where the failure after assigning is recorded.</param>
    public RegisterPartNumberAssigner(
        RegisterStore store,
        PartPropertiesSnapshot snapshot,
        IPropertyWriteTarget target,
        ILog? log = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(target);

        _store = store;
        _snapshot = snapshot;
        _target = target;
        _log = log ?? NullLog.Instance;
    }

    /// <summary>
    /// The register row for <paramref name="snapshot"/>: the part name read from the file name, the part type as the
    /// file holds it (a listed code in the list's spelling), the kind and the full path.
    /// </summary>
    public static RegisterRequest RequestFor(PartPropertiesSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var partType = PropertyValues.NormaliseText(snapshot.PartType);
        partType = PartTypes.Find(partType)?.Code ?? partType;
        return new RegisterRequest(
            PartName.FromFileName(snapshot.FullFileName),
            partType,
            snapshot.DocumentKind,
            snapshot.FullFileName);
    }

    /// <inheritdoc />
    /// <exception cref="RegisterBusyException">Another PC holds the register lock. Nothing was assigned.</exception>
    /// <exception cref="RegisterUnreadableException">The register cannot be read in full. Nothing was assigned.</exception>
    /// <exception cref="InvalidOperationException">
    /// The file is not saved, already has a registered number, the sequence is full, the register could not be
    /// written, or the Part Number could not be written after the number was assigned. The message says which.
    /// </exception>
    public PartNumberAssignment Assign()
    {
        if (string.IsNullOrWhiteSpace(_snapshot.FullFileName))
        {
            throw new InvalidOperationException(NotSavedMessage);
        }

        RegisterSyncResult sync;
        try
        {
            sync = _store.Sync();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log.Error("Could not sync the part register before assigning a number.", ex);
            throw new InvalidOperationException(
                $"The part register could not be synced: {ex.Message} Nothing was assigned.", ex);
        }

        // Another PC may have registered this file's number since the window opened.
        if (_store.HasNumber(_snapshot.PartNumber))
        {
            throw new InvalidOperationException(AlreadyRegisteredMessage);
        }

        RegisterAssignment assignment;
        try
        {
            assignment = _store.Assign(RequestFor(_snapshot));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The shared file may already hold the new row (for example when only the local copy failed).
            _log.Error("Could not update the part register while assigning a number.", ex);
            throw new InvalidOperationException(
                $"The part register could not be updated: {ex.Message} A number may already have been taken in the " +
                "register; check it before trying again.", ex);
        }

        var number = assignment.PartNumber.ToString();
        try
        {
            _target.Apply([PropertyWrite.SetText(PartPropertyStorage.PartNumber, number)]);
        }
        catch (Exception ex)
        {
            _log.Error(
                $"Part number {number} was assigned in the register but could not be written to the file. " +
                "The register entry stays.", ex);
            throw new InvalidOperationException(
                $"Part number {number} was assigned in the register but could not be written to the file: " +
                $"{ex.Message} The number stays in the register.", ex);
        }

        var duplicates = sync.Duplicates
            .Concat(assignment.Duplicates)
            .Select(PartNumberAssignment.DescribeDuplicate)
            .ToList();

        return new PartNumberAssignment(number, assignment.Offline, duplicates.AsReadOnly());
    }
}
