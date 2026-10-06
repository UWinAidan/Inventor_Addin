using System.Globalization;

namespace InventorAddin.Core.PartProperties;

/// <summary>
/// Normalises, validates, parses and formats the values typed into the Part Properties window, so the
/// view-model and the write plan compare and write clean values. Messages are short, name the field and say
/// what to do; the window shows them on its status line.
/// </summary>
public static class PropertyValues
{
    /// <summary>The longest text value, in characters after trimming.</summary>
    public const int MaxTextLength = 255;

    /// <summary>A COM currency value holds 4 decimal places.</summary>
    public const int MaxCostDecimalPlaces = 4;

    public const string CostNotNumberMessage = "Cost must be a number, for example 12.50.";
    public const string CostNegativeMessage = "Cost must be zero or more.";
    public const string CostTooManyDecimalsMessage = "Cost can have at most 4 decimal places.";
    public const string CostTooLargeMessage = "Cost is too large.";

    /// <summary>
    /// The largest cost accepted: the largest value a COM currency (a 64-bit integer scaled by 10,000) holds.
    /// </summary>
    public const decimal MaxCost = 922_337_203_685_477.5807m;

    /// <summary>Displayed with at least 2 and at most 4 decimal places, with group separators.</summary>
    private const string CostFormat = "#,0.00##";

    /// <summary>
    /// Parsing styles: the culture's decimal and group separators, its currency symbol, a sign (so a negative
    /// value gets its own message rather than "not a number"), and surrounding whitespace. No exponent.
    /// </summary>
    private const NumberStyles CostStyles = NumberStyles.Number | NumberStyles.AllowCurrencySymbol;

    /// <summary>Trims surrounding whitespace; null or blank gives <c>""</c>. Inner whitespace is kept.</summary>
    public static string NormaliseText(string? text) => text?.Trim() ?? string.Empty;

    /// <summary>
    /// Null when <paramref name="text"/> is valid after trimming, otherwise a message naming
    /// <paramref name="fieldName"/>: the text is longer than <see cref="MaxTextLength"/>, or contains a line break
    /// or another control character. Blank is valid.
    /// </summary>
    public static string? ValidateText(string fieldName, string? text)
    {
        var normalised = NormaliseText(text);

        foreach (var c in normalised)
        {
            if (IsLineBreak(c))
            {
                return $"{fieldName} must be on one line. Remove the line break.";
            }

            if (char.IsControl(c))
            {
                return $"{fieldName} cannot contain control characters such as tabs. Remove them.";
            }
        }

        if (normalised.Length > MaxTextLength)
        {
            return $"{fieldName} must be {MaxTextLength} characters or fewer.";
        }

        return null;
    }

    /// <summary>
    /// Parses a cost typed in <paramref name="culture"/>'s number format. Blank is valid and gives a null cost.
    /// The culture's decimal and group separators are accepted, with or without its currency symbol. When the
    /// culture's group separator is a space (for example <c>fr-FR</c>), any kind of space between digits is
    /// accepted for it.
    /// Negative values, text that is not a number, and more than <see cref="MaxCostDecimalPlaces"/> decimal
    /// places are rejected with a message saying which. Trailing zeros do not count as decimal places.
    /// </summary>
    public static bool TryParseCost(string? text, CultureInfo culture, out decimal? cost, out string? error)
    {
        ArgumentNullException.ThrowIfNull(culture);

        cost = null;
        error = null;

        var normalised = NormaliseText(text);
        if (normalised.Length == 0)
        {
            return true;
        }

        var format = culture.NumberFormat;
        normalised = UnifySpaceGroupSeparators(normalised, format);

        if (!decimal.TryParse(normalised, CostStyles, format, out var value))
        {
            error = CostNotNumberMessage;
            return false;
        }

        if (value < 0)
        {
            error = CostNegativeMessage;
            return false;
        }

        if (decimal.Round(value, MaxCostDecimalPlaces) != value)
        {
            error = CostTooManyDecimalsMessage;
            return false;
        }

        if (value > MaxCost)
        {
            error = CostTooLargeMessage;
            return false;
        }

        // Drop trailing zeros so "12.50" and "12.5" are the same stored value and compare equal everywhere.
        cost = value / 1.0000000000000000000000000000m;
        return true;
    }

    /// <summary>
    /// The text shown for a cost: <c>""</c> for null and for 0 (a stored 0 shows as blank, spec 07 open
    /// question 4), otherwise the number in <paramref name="culture"/>'s format with group separators, no
    /// currency symbol, and 2 to 4 decimal places (<c>12.5</c> shows as <c>12.50</c>, <c>12.3456</c> as
    /// <c>12.3456</c>).
    /// </summary>
    public static string FormatCost(decimal? cost, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);

        if (cost is not { } value || value == 0)
        {
            return string.Empty;
        }

        return value.ToString(CostFormat, culture.NumberFormat);
    }

    /// <summary>The value written to the standard Cost property: 0 for null (clearing the field writes 0).</summary>
    public static decimal CostToStore(decimal? cost) => cost ?? 0m;

    private static bool IsLineBreak(char c) =>
        c is '\n' or '\r' or '\u0085' or '\u2028' or '\u2029' or '\v' or '\f';

    /// <summary>
    /// When the culture groups digits with a space (often a non-breaking or narrow non-breaking space that
    /// nobody types), any kind of space between two digits becomes the culture's group separator, so a plain
    /// space works. Any other space, for example between the number and a currency symbol, becomes a plain
    /// space, which the parser accepts as surrounding whitespace.
    /// </summary>
    private static string UnifySpaceGroupSeparators(string text, NumberFormatInfo format)
    {
        var group = format.NumberGroupSeparator;
        if (group.Length != 1 || !IsSpace(group[0]))
        {
            return text;
        }

        var chars = text.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (!IsSpace(chars[i]))
            {
                continue;
            }

            var betweenDigits = i > 0 && i < chars.Length - 1
                && char.IsAsciiDigit(chars[i - 1]) && char.IsAsciiDigit(chars[i + 1]);
            chars[i] = betweenDigits ? group[0] : ' ';
        }

        return new string(chars);
    }

    private static bool IsSpace(char c) => c is ' ' or '\u00A0' or '\u202F' or '\u2009';
}
