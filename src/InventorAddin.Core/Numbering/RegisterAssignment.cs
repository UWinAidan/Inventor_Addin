using InventorAddin.Core.Models;

namespace InventorAddin.Core.Numbering;

/// <summary>What a number is being assigned to. Null text becomes <c>""</c> in the register entry.</summary>
/// <param name="Name">The part name, with spaces.</param>
/// <param name="PartType">The part type short code, or <c>""</c>.</param>
/// <param name="Kind">Part, sheet metal part or assembly.</param>
/// <param name="FilePath">Full path of the file the number is for.</param>
public sealed record RegisterRequest(string Name, string PartType, DocumentKind Kind, string FilePath);

/// <summary>What <see cref="RegisterStore.Assign"/> did.</summary>
/// <param name="PartNumber">The number assigned.</param>
/// <param name="Offline">
/// True when the shared register could not be reached: the number came from this PC's copy and waits in the pending
/// entries until the next sync. The user should be told.
/// </param>
/// <param name="Duplicates">Duplicates found by the sync done before assigning; empty when offline.</param>
public sealed record RegisterAssignment(PartNumber PartNumber, bool Offline, IReadOnlyList<RegisterDuplicate> Duplicates);

/// <summary>What <see cref="RegisterStore.Sync"/> did.</summary>
/// <param name="Reached">False when the shared register could not be reached; nothing was synced.</param>
/// <param name="Duplicates">Pending numbers the shared register already held. Both rows are kept.</param>
public sealed record RegisterSyncResult(bool Reached, IReadOnlyList<RegisterDuplicate> Duplicates);

/// <summary>Another PC held the register lock for longer than <see cref="RegisterStore.LockWait"/>. Nothing was assigned or changed.</summary>
public sealed class RegisterBusyException : Exception
{
    public RegisterBusyException(string lockFilePath)
        : base("Another PC is writing the part register. Nothing was assigned; try again in a moment. " +
               $"If this keeps happening, check the lock file '{lockFilePath}'.")
    {
        LockFilePath = lockFilePath;
    }

    public string LockFilePath { get; }
}

/// <summary>
/// A register file that cannot be read in full: it does not parse, or some rows or columns could not be read (spec 02
/// question 19). The add-in refuses to write it back, because that would drop the unread rows. Nothing was assigned
/// and no file was changed.
/// </summary>
public sealed class RegisterUnreadableException : Exception
{
    public RegisterUnreadableException(string filePath, IReadOnlyList<string> problems, Exception? inner = null)
        : base(BuildMessage(filePath, problems), inner)
    {
        FilePath = filePath;
        Problems = problems;
    }

    /// <summary>The register file that could not be read.</summary>
    public string FilePath { get; }

    /// <summary>What is wrong, one item per problem; row problems name the line to fix.</summary>
    public IReadOnlyList<string> Problems { get; }

    private static string BuildMessage(string filePath, IReadOnlyList<string> problems) =>
        $"The part register '{filePath}' cannot be read in full, so the add-in will not change it. " +
        "Nothing was assigned. Fix the file and try again:" +
        string.Concat(problems.Select(p => Environment.NewLine + "- " + p));
}
