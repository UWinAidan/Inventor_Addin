namespace InventorAddin.Core.Materials;

/// <summary>
/// Where the Material and Finish window applies a choice (spec 03): the part, or the selected parts of an assembly.
/// The add-in implements it (task 074); tests use a fake.
/// </summary>
public interface IMaterialFinishTarget
{
    /// <summary>
    /// Applies <paramref name="choice"/> to every target part as one step: the material, then the finish's appearance
    /// when it has one, then <see cref="MaterialFinishChoice.PropertyWrites"/>. Returns what happened. A failure is
    /// reported as a failed result or an exception; either way the implementation undoes any partial change (for
    /// example by aborting its transaction).
    /// </summary>
    MaterialFinishResult Apply(MaterialFinishChoice choice);
}
