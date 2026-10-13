namespace InventorAddin.Core.Materials;

/// <summary>
/// One material as read from a material library (spec 03, "Core logic"). Spike 067 found a library material with no
/// category and one whose appearance Inventor cannot read, so both are allowed to be missing.
/// </summary>
/// <param name="DisplayName">The material's display name, which is how materials are compared (spike 067).</param>
/// <param name="Category">The material's category, or <c>""</c> when the library gives none.</param>
/// <param name="AppearanceName">The display name of the material's appearance, or null when it could not be read.</param>
public sealed record LibraryMaterial(string DisplayName, string Category, string? AppearanceName);
