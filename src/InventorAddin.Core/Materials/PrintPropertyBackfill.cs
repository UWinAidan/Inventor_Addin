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
    /// The log wording for backfill writes, property names only (a value is never logged): <c>Filled in Revision Number,
    /// Status</c>, <c>Finish Spec refreshed</c>, or both joined with <c>; </c>, filled first (task 224).
    /// </summary>
    public static string Describe(IEnumerable<PropertyWrite> writes)
    {
        ArgumentNullException.ThrowIfNull(writes);

        var list = writes.ToList();
        var parts = new List<string>();
        var filled = list.Where(w => !w.IsRefresh).Select(w => w.Location.PropertyName).ToList();
        if (filled.Count > 0)
        {
            parts.Add("Filled in " + string.Join(", ", filled));
        }

        parts.AddRange(list.Where(w => w.IsRefresh).Select(w => w.Location.PropertyName + " refreshed"));
        return string.Join("; ", parts);
    }

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

        AddRevisionAndStatus(writes, storedRevision, storedDesignStatus, storedUserStatus);
        return writes.AsReadOnly();
    }

    /// <summary>The text printed for a title block field that does not apply to the document (an assembly's material, size and finish).</summary>
    public const string NotApplicable = "—";

    /// <summary>
    /// The writes an assembly needs (task 231; Aidan, 2026-11-23): Revision Number and Status exactly as for a part (see
    /// <see cref="For"/>), then a dash (<see cref="NotApplicable"/>) in each of Material, Material Size and Finish Spec that is blank, so the
    /// print shows a dash. A value already set is never changed. No finish is looked up and no Finish Spec is built.
    /// </summary>
    public static IReadOnlyList<PropertyWrite> ForAssembly(
        string? storedRevision,
        int? storedDesignStatus,
        string? storedUserStatus,
        string? storedMaterial,
        string? storedMaterialSize,
        string? storedFinishSpec)
    {
        var writes = new List<PropertyWrite>();
        AddRevisionAndStatus(writes, storedRevision, storedDesignStatus, storedUserStatus);

        if (PropertyValues.NormaliseText(storedMaterial).Length == 0)
        {
            writes.Add(PropertyWrite.SetText(PartPropertyStorage.Material, NotApplicable));
        }

        if (PropertyValues.NormaliseText(storedMaterialSize).Length == 0)
        {
            writes.Add(PropertyWrite.SetText(PartPropertyStorage.MaterialSize, NotApplicable));
        }

        if (PropertyValues.NormaliseText(storedFinishSpec).Length == 0)
        {
            writes.Add(PropertyWrite.SetText(PartPropertyStorage.FinishSpec, NotApplicable));
        }

        return writes.AsReadOnly();
    }

    private static void AddRevisionAndStatus(
        List<PropertyWrite> writes, string? storedRevision, int? storedDesignStatus, string? storedUserStatus)
    {
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
    }
}
