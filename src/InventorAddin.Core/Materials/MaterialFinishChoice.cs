using InventorAddin.Core.PartProperties;

namespace InventorAddin.Core.Materials;

/// <summary>What the Material and Finish window asks the target to apply (spec 03).</summary>
/// <param name="MaterialName">The library material's display name, which is how materials are found (spike 067).</param>
/// <param name="Finish">
/// The chosen finish, or null for None. Its <see cref="Finish.AppearanceName"/> is the appearance to set; null keeps
/// the material's appearance.
/// </param>
/// <param name="PropertyWrites">The four finish iProperties, from <see cref="FinishProperties.For"/>.</param>
public sealed record MaterialFinishChoice(string MaterialName, Finish? Finish, IReadOnlyList<PropertyWrite> PropertyWrites)
{
    /// <summary>The choice for <paramref name="materialName"/> and <paramref name="finish"/>, with its property writes.</summary>
    public static MaterialFinishChoice For(string materialName, Finish? finish)
    {
        ArgumentNullException.ThrowIfNull(materialName);
        return new MaterialFinishChoice(materialName, finish, FinishProperties.For(finish));
    }
}
