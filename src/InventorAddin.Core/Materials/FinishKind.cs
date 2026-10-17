namespace InventorAddin.Core.Materials;

/// <summary>The kind of process a finish is (spec 03, "The finish list"). The file writes HeatTreat as <c>Heat treat</c>.</summary>
public enum FinishKind
{
    Plating,
    Conversion,
    Anodize,
    HeatTreat,

    /// <summary>Paint or powder coat; the window's <i>Show paints</i> filter hides these.</summary>
    Paint,
}

/// <summary>Reading a <see cref="FinishKind"/> from the finish list.</summary>
public static class FinishKinds
{
    /// <summary>
    /// Reads a kind by member name, ignoring case and any whitespace, so <c>Heat treat</c>, <c>heat treat</c> and
    /// <c>HeatTreat</c> all read as <see cref="FinishKind.HeatTreat"/>. Numbers are not accepted.
    /// </summary>
    public static bool TryParse(string? text, out FinishKind kind)
    {
        kind = default;
        if (text is null)
        {
            return false;
        }

        var compact = string.Concat(text.Where(c => !char.IsWhiteSpace(c)));
        if (compact.Length == 0)
        {
            return false;
        }

        foreach (var value in Enum.GetValues<FinishKind>())
        {
            if (string.Equals(value.ToString(), compact, StringComparison.OrdinalIgnoreCase))
            {
                kind = value;
                return true;
            }
        }

        return false;
    }
}
