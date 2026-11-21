using System.Globalization;
using System.Text;

namespace InventorAddin.Core.HoleTable;

/// <summary>A description as Inventor formatted text: one text per line, or one text when it has a stack.</summary>
/// <param name="Texts">One formatted text per line. When <paramref name="HasStack"/>, the first text is the stack line.</param>
/// <param name="HasStack">True when the first text carries a stacked tolerance; every later line is its own text.</param>
/// <param name="LineCount">How many line slots the cell needs: the texts, and one more for a stack (it takes 2).</param>
public sealed record HoleTableFormattedDescription(IReadOnlyList<string> Texts, bool HasStack, int LineCount);

/// <summary>Builds Inventor formatted text from description parts (task 161).</summary>
public static class HoleTableFormattedText
{
    /// <summary>Line pitch as a multiple of the text height (spike 160, round 6b, <c>$linePitch</c>).</summary>
    public const double LinePitchFactor = 1.75;

    /// <summary>Spotface character in the AIGDT font, U+00C2 (spike 160, round 6b, <c>$SYM.Spot</c>).</summary>
    public const char SpotfaceAigdt = 'Â';

    /// <summary>
    /// The formatted text of a description. Each symbol is one AIGDT character in its own override at the data text
    /// height; a toleranced first line is one text with a native stack (spike 160, rounds 5q and 6b, <c>DescFt</c>).
    /// </summary>
    public static HoleTableFormattedDescription For(HoleTableDescription description, double textHeightCm)
    {
        ArgumentNullException.ThrowIfNull(description);

        var texts = new List<string>();
        for (var i = 0; i < description.Lines.Count; i++)
        {
            if (i == 0 && description.Tolerance is { } tol)
            {
                var pre = Join(description.FirstLineBeforeStack, textHeightCm).Trim();
                var post = Join(description.FirstLineAfterStack, textHeightCm).Trim();
                texts.Add($"{pre} <Stack FractionalTextScale='1.0'>{Escape(tol.Upper)}^{Escape(tol.Lower)}</Stack> {post}");
            }
            else
            {
                texts.Add(Join(description.Lines[i], textHeightCm));
            }
        }

        // Later lines stay separate texts: a multi-line text with AIGDT overrides spaces its lines too wide (spike 160, round 6b).
        if (description.Tolerance is not null && texts.Count > 0)
        {
            return new HoleTableFormattedDescription(texts, true, texts.Count + 1);
        }

        return new HoleTableFormattedDescription(texts, false, texts.Count);
    }

    /// <summary>Formats parts in order. A symbol is followed by a space, so a plain part right after it loses one leading space.</summary>
    public static string Join(IReadOnlyList<HoleDescriptionPart> parts, double textHeightCm)
    {
        var sb = new StringBuilder();
        var afterSymbol = false;
        foreach (var part in parts)
        {
            if (part.Kind == HoleDescriptionPartKind.Text)
            {
                var text = afterSymbol && part.Text.StartsWith(' ') ? part.Text[1..] : part.Text;
                sb.Append(Escape(text));
                afterSymbol = false;
            }
            else
            {
                sb.Append(Symbol(part.Kind, textHeightCm));
                afterSymbol = part.Kind != HoleDescriptionPartKind.Diameter;
            }
        }

        return sb.ToString();
    }

    /// <summary>One symbol in its AIGDT override. Counterbore, countersink, spotface and depth end with a space (spike 160, round 6b, <c>$SYM</c>).</summary>
    public static string Symbol(HoleDescriptionPartKind kind, double textHeightCm)
    {
        var (character, space) = kind switch
        {
            HoleDescriptionPartKind.Diameter => ("n", ""),
            HoleDescriptionPartKind.Counterbore => ("v", " "),
            HoleDescriptionPartKind.Countersink => ("w", " "),
            HoleDescriptionPartKind.Depth => ("x", " "),
            HoleDescriptionPartKind.Spotface => (SpotfaceAigdt.ToString(), " "),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Not a symbol."),
        };
        var height = textHeightCm.ToString("F4", CultureInfo.InvariantCulture);
        return $"<StyleOverride Font='AIGDT' FontSize='{height}'>{character}</StyleOverride>{space}";
    }

    /// <summary>Escapes ampersand, less-than, greater-than and the apostrophe.</summary>
    public static string Escape(string text) =>
        text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("'", "&apos;");
}
