namespace InventorAddin.Core.Materials;

/// <summary>Which finishes suit a material, for the Finishes list of the Material and Finish window (spec 03).</summary>
public static class FinishMatching
{
    /// <summary>
    /// True when the finish suits the material: its AppliesTo is empty, or one of its entries equals the material's
    /// category or display name, ignoring case and surrounding spaces. Blank entries match nothing.
    /// </summary>
    public static bool Suits(Finish finish, LibraryMaterial material)
    {
        ArgumentNullException.ThrowIfNull(finish);
        ArgumentNullException.ThrowIfNull(material);

        if (finish.AppliesToAll)
        {
            return true;
        }

        var category = (material.Category ?? "").Trim();
        var name = (material.DisplayName ?? "").Trim();
        foreach (var entry in finish.AppliesTo)
        {
            var target = (entry ?? "").Trim();
            if (target.Length == 0)
            {
                continue;
            }

            if (string.Equals(target, category, StringComparison.OrdinalIgnoreCase)
                || string.Equals(target, name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The finishes to list for a material, in file order: those that <see cref="Suits"/> it, without Kind
    /// <see cref="FinishKind.Paint"/> when <paramref name="showPaints"/> is false, and whose Name or Code contains the
    /// trimmed <paramref name="search"/> text, ignoring case (blank keeps everything). With no material selected
    /// (null) the list is empty, as the window's Finishes list is until a material is chosen. "None" is not part of
    /// this list; the window adds it.
    /// </summary>
    public static IReadOnlyList<Finish> For(
        IEnumerable<Finish> finishes,
        LibraryMaterial? material,
        bool showPaints,
        string? search)
    {
        ArgumentNullException.ThrowIfNull(finishes);

        if (material is null)
        {
            return Array.Empty<Finish>();
        }

        var text = (search ?? "").Trim();
        return finishes
            .Where(f => Suits(f, material))
            .Where(f => showPaints || f.Kind != FinishKind.Paint)
            .Where(f => text.Length == 0
                || f.Name.Contains(text, StringComparison.OrdinalIgnoreCase)
                || f.Code.Contains(text, StringComparison.OrdinalIgnoreCase))
            .ToList()
            .AsReadOnly();
    }
}
