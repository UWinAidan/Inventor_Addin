using InventorAddin.Core.PartProperties;

namespace InventorAddin.Core.Numbering;

/// <summary>
/// The properties written on a file when it is created or copied under a new number (spec 02, "Written on
/// creation"). New Part and Save As Copy both use it. Nothing else is written.
/// </summary>
public static class CreationProperties
{
    /// <summary>
    /// The writes, in this order: Part Number; Description (the part name normalised by
    /// <see cref="PartFileName.NormaliseName"/>, with spaces); Designer, Author and Detailer, all
    /// <paramref name="defaultDesigner"/> trimmed, all left out when it is blank; Part Type, left out when blank.
    /// A part type in <see cref="PartTypes.All"/> is written in the list's spelling; any other code is written trimmed.
    /// Each goes to its <see cref="PartPropertyStorage"/> location.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="partName"/> fails <see cref="PartFileName.ValidateName"/>.</exception>
    public static IReadOnlyList<PropertyWrite> Build(
        PartNumber number,
        string partName,
        string? partType,
        string? defaultDesigner)
    {
        var problem = PartFileName.ValidateName(partName);
        if (problem is not null)
        {
            throw new ArgumentException(problem, nameof(partName));
        }

        var writes = new List<PropertyWrite>
        {
            PropertyWrite.SetText(PartPropertyStorage.PartNumber, number.ToString()),
            PropertyWrite.SetText(PartPropertyStorage.Description, PartFileName.NormaliseName(partName)),
        };

        var designer = PropertyValues.NormaliseText(defaultDesigner);
        if (designer.Length > 0)
        {
            writes.Add(PropertyWrite.SetText(PartPropertyStorage.Designer, designer));
            writes.Add(PropertyWrite.SetText(PartPropertyStorage.Author, designer));
            writes.Add(PropertyWrite.SetText(PartPropertyStorage.Detailer, designer));
        }

        var type = PropertyValues.NormaliseText(partType);
        type = PartTypes.Find(type)?.Code ?? type;
        if (type.Length > 0)
        {
            writes.Add(PropertyWrite.SetText(PartPropertyStorage.PartType, type));
        }

        return writes.AsReadOnly();
    }
}
