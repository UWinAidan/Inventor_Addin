namespace InventorAddin.Core.PartProperties;

/// <summary>The fields the user can edit in the Part Properties window, as they stand when Apply is pressed.</summary>
public sealed record PartEditedValues(string? PartType, string? Designer, string? Detailer, decimal? Cost);

/// <summary>
/// Turns the values read from a file and the edited values into the exact iProperty writes to make, so only
/// changed fields are written (spec 07). It takes final values and does not know how they were produced.
/// </summary>
public static class PartPropertiesWritePlan
{
    /// <summary>
    /// The writes, in the spec's field order: Description, Part Type, Designer, Detailer, Cost.
    /// Text is compared after <see cref="PropertyValues.NormaliseText"/> (null and blank are the same).
    /// A changed standard property is Set, to <c>""</c> when cleared. A changed custom property is Set when
    /// not blank, and Removed when cleared. Cost is compared and written as <see cref="PropertyValues.CostToStore"/>.
    /// Description is Set to the part name when the file has a name and it differs. Part Number, Material,
    /// Finish and weight are never written.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="original"/> cannot be edited.</exception>
    public static IReadOnlyList<PropertyWrite> Build(PartPropertiesSnapshot original, PartEditedValues edited)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(edited);

        if (original.EditBlock != EditBlock.None)
        {
            throw new ArgumentException(
                $"The file cannot be edited ({original.EditBlock}); no write plan can be built.", nameof(original));
        }

        var writes = new List<PropertyWrite>();

        var partName = PropertyValues.NormaliseText(PartName.FromFileName(original.FullFileName));
        if (partName.Length > 0 && partName != PropertyValues.NormaliseText(original.Description))
        {
            writes.Add(PropertyWrite.SetText(PartPropertyStorage.Description, partName));
        }

        AddText(writes, PartPropertyStorage.PartType, original.PartType, edited.PartType);
        AddText(writes, PartPropertyStorage.Designer, original.Designer, edited.Designer);
        AddText(writes, PartPropertyStorage.Detailer, original.Detailer, edited.Detailer);

        var originalCost = PropertyValues.CostToStore(original.Cost);
        var editedCost = PropertyValues.CostToStore(edited.Cost);
        if (originalCost != editedCost)
        {
            writes.Add(PropertyWrite.SetCurrency(PartPropertyStorage.Cost, editedCost));
        }

        return writes.AsReadOnly();
    }

    private static void AddText(List<PropertyWrite> writes, PropertyLocation location, string? original, string? edited)
    {
        var before = PropertyValues.NormaliseText(original);
        var after = PropertyValues.NormaliseText(edited);
        if (before == after)
        {
            return;
        }

        writes.Add(location.IsCustom && after.Length == 0
            ? PropertyWrite.Remove(location)
            : PropertyWrite.SetText(location, after));
    }
}
