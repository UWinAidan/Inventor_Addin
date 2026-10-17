namespace InventorAddin.Core.Materials;

/// <summary>
/// One finish from the finish list (spec 03, "The finish list"): a process applied to the surface after the part is
/// made. Text values are trimmed. Read with <see cref="FinishListCsv"/>.
/// </summary>
/// <param name="Code">Short code for the drawing and parts list, unique in the list (ignoring case).</param>
/// <param name="Name">The name shown in the list.</param>
/// <param name="Kind">The kind of process.</param>
/// <param name="AppliesTo">
/// Material categories or material names the finish suits. Empty means it suits every material (Default, spec 03).
/// </param>
/// <param name="AppearanceName">
/// The display name of the Inventor appearance the finish gives the part, or null to keep the material's appearance.
/// </param>
/// <param name="ThicknessMinMm">The smallest thickness in mm, as written in the file.</param>
/// <param name="ThicknessMaxMm">The largest thickness in mm, as written in the file.</param>
/// <param name="DimensionsAfterFinish">Whether drawing dimensions apply after finishing.</param>
/// <param name="PrintNote">The note for the drawing.</param>
public sealed record Finish(
    string Code,
    string Name,
    FinishKind Kind,
    IReadOnlyList<string> AppliesTo,
    string? AppearanceName,
    string ThicknessMinMm,
    string ThicknessMaxMm,
    bool DimensionsAfterFinish,
    string PrintNote)
{
    /// <summary>True when <see cref="AppliesTo"/> is empty, so the finish suits every material.</summary>
    public bool AppliesToAll => AppliesTo.Count == 0;
}
