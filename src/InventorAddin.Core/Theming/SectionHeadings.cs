using System.Globalization;
using System.Text;

namespace InventorAddin.Core.Theming;

/// <summary>
/// The text a section heading shows (spec 08, "Type": upper case, wide letter spacing).
/// WPF's <c>TextBlock</c> has neither a text transform nor a letter-spacing property, so the
/// section control shows the text this method returns instead of the heading as written.
/// </summary>
public static class SectionHeadings
{
    /// <summary>Put between every two characters to widen the spacing: U+200A HAIR SPACE.</summary>
    public const char LetterSpace = '\u200A'; // hair space

    /// <summary>
    /// <paramref name="heading"/> trimmed, in upper case (invariant culture), with <see cref="LetterSpace"/>
    /// between every two characters. Characters are text elements, so a surrogate pair or a letter with a
    /// combining mark is never split. A null or blank heading gives an empty string.
    /// </summary>
    public static string Format(string? heading)
    {
        if (string.IsNullOrWhiteSpace(heading))
            return string.Empty;

        string upper = heading.Trim().ToUpperInvariant();
        var result = new StringBuilder(upper.Length * 2);
        TextElementEnumerator elements = StringInfo.GetTextElementEnumerator(upper);
        while (elements.MoveNext())
        {
            if (result.Length > 0)
                result.Append(LetterSpace);
            result.Append(elements.GetTextElement());
        }

        return result.ToString();
    }
}
