namespace InventorAddin.Core.PartProperties;

/// <summary>
/// Where the Part Properties window sends its writes. The add-in implements it by writing iProperties to the
/// document inside one transaction (task 019); tests use a fake.
/// </summary>
public interface IPropertyWriteTarget
{
    /// <summary>
    /// Makes every write in <paramref name="writes"/>, in order, as one step. Throws when any write fails; the
    /// implementation is responsible for undoing a partial write (for example by aborting its transaction).
    /// </summary>
    void Apply(IReadOnlyList<PropertyWrite> writes);
}
