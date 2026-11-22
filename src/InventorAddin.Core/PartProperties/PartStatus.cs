namespace InventorAddin.Core.PartProperties;

/// <summary>
/// A part's status. The values are Inventor's Design Status numbers (per the Inventor API reference: 1 Work In
/// Progress, 2 Pending, 3 Released; 0 is "not set" and is not a status here).
/// </summary>
public enum PartStatus
{
    WorkInProgress = 1,
    Pending = 2,
    Released = 3,
}

/// <summary>Maps <see cref="PartStatus"/> to Design Status numbers and to the User Status words (task 219).</summary>
public static class PartStatuses
{
    /// <summary>The revision a new part starts at.</summary>
    public const string FirstRevision = "A";

    /// <summary>The status a new part, or an older part with none set, gets.</summary>
    public const PartStatus Initial = PartStatus.WorkInProgress;

    /// <summary>The Design Status number Inventor stores for the status.</summary>
    public static int DesignStatusOf(PartStatus status) => (int)status;

    /// <summary>The words written to Inventor's own Status field (User Status) for the status.</summary>
    public static string UserStatusText(PartStatus status) => status switch
    {
        PartStatus.WorkInProgress => "WORK IN PROGRESS",
        PartStatus.Pending => "PENDING",
        PartStatus.Released => "RELEASED",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown part status."),
    };

    /// <summary>The status for a Design Status number, or null for 0 (not set), null, or any number Inventor does not define.</summary>
    public static PartStatus? FromDesignStatus(int? designStatus) => designStatus switch
    {
        1 => PartStatus.WorkInProgress,
        2 => PartStatus.Pending,
        3 => PartStatus.Released,
        _ => null,
    };

    /// <summary>The two writes that set a status: Design Status, then the User Status words in step with it.</summary>
    public static IReadOnlyList<PropertyWrite> WritesFor(PartStatus status) =>
    [
        PropertyWrite.SetInteger(PartPropertyStorage.DesignStatus, DesignStatusOf(status)),
        PropertyWrite.SetText(PartPropertyStorage.UserStatus, UserStatusText(status)),
    ];
}
