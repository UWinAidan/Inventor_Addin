using InventorAddin.Core.Drawing;
using InventorAddin.Core.PartProperties;

namespace InventorAddin.Core.Materials;

/// <summary>
/// Every print property an older part may be missing, in one call (Aidan, 2026-11-22, tasks 217 and 219): Finish Spec,
/// Revision Number and Status. The add-in runs it when Material and Finish opens, in Create Print and in Mass Update,
/// and writes the result in one transaction.
/// </summary>
public static class PrintPropertyBackfill
{
    /// <summary>
    /// The writes a part needs, in this order, each left out when its value is already set: Finish Spec (see
    /// <see cref="FinishSpecBackfill.For"/>); Revision Number "A" when blank; then Status. A Design Status of 0 (not
    /// set) is written as Work In Progress. A blank User Status is written as the words for the part's Design Status
    /// (Work In Progress when that was 0). A User Status that is already set is never changed. When Design Status is 0 and the
    /// User Status is already one of the three words (<see cref="PartStatuses.FromUserStatusText"/>), Design Status is
    /// written to match it instead of Work In Progress (task 210). A Design Status that could not be read (null) or is not 0 to 3 is left alone and, with it, a
    /// blank User Status, because the words cannot be chosen.
    /// </summary>
    /// <param name="storedRevision">The part's Revision Number, or null.</param>
    /// <param name="storedDesignStatus">The part's Design Status number, or null when it could not be read.</param>
    /// <param name="storedUserStatus">The part's User Status text, or null.</param>
    /// <remarks>The other parameters are those of <see cref="FinishSpecBackfill.For"/>.</remarks>
    public static IReadOnlyList<PropertyWrite> For(
        string? storedFinish,
        string? storedFinishThickness,
        string? storedFinishSpec,
        string? storedRevision,
        int? storedDesignStatus,
        string? storedUserStatus,
        IEnumerable<Finish> finishList,
        PrintUnits units)
    {
        var writes = new List<PropertyWrite>();

        var spec = FinishSpecBackfill.For(storedFinish, storedFinishThickness, storedFinishSpec, finishList, units);
        if (spec is not null)
        {
            writes.Add(spec);
        }

        if (PropertyValues.NormaliseText(storedRevision).Length == 0)
        {
            writes.Add(PropertyWrite.SetText(PartPropertyStorage.RevisionNumber, PartStatuses.FirstRevision));
        }

        var status = PartStatuses.FromDesignStatus(storedDesignStatus);
        if (storedDesignStatus is 0)
        {
            status = PartStatuses.FromUserStatusText(storedUserStatus) ?? PartStatuses.Initial;
            writes.Add(PropertyWrite.SetInteger(PartPropertyStorage.DesignStatus, PartStatuses.DesignStatusOf(status.Value)));
        }

        if (status is { } words && PropertyValues.NormaliseText(storedUserStatus).Length == 0)
        {
            writes.Add(PropertyWrite.SetText(PartPropertyStorage.UserStatus, PartStatuses.UserStatusText(words)));
        }

        return writes.AsReadOnly();
    }
}
