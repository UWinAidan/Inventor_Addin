namespace InventorAddin.Core.Numbering;

/// <summary>
/// A part number in the format from spec 02: <c>AWB-</c> followed by exactly five digits, <c>AWB-10001</c> to
/// <c>AWB-99999</c>. Parts, sheet metal parts and assemblies share one sequence, so the number carries no part type.
/// Equality and ordering are by <see cref="Value"/>.
/// </summary>
public readonly record struct PartNumber : IComparable<PartNumber>
{
    /// <summary>The letters before the separator. Always written in capitals; matched ignoring case when parsing.</summary>
    public const string Prefix = "AWB";

    /// <summary>The character between <see cref="Prefix"/> and the digits.</summary>
    public const char Separator = '-';

    /// <summary>How many digits follow the separator.</summary>
    public const int DigitCount = 5;

    /// <summary>The first number in the sequence, <c>AWB-10001</c>.</summary>
    public const int FirstValue = 10001;

    /// <summary>The last number in the sequence, <c>AWB-99999</c>.</summary>
    public const int LastValue = 99999;

    /// <summary>
    /// The number for <paramref name="value"/>. Throws <see cref="ArgumentOutOfRangeException"/> when the value is
    /// outside <see cref="FirstValue"/> to <see cref="LastValue"/>.
    /// </summary>
    public PartNumber(int value)
    {
        if (value < FirstValue || value > LastValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                $"A part number value must be between {FirstValue} and {LastValue}.");
        }

        Value = value;
    }

    /// <summary>The numeric part, for example <c>10001</c> for <c>AWB-10001</c>.</summary>
    public int Value { get; }

    /// <summary>The number as written in files and the register, for example <c>AWB-10001</c>.</summary>
    public override string ToString() =>
        string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{Prefix}{Separator}{Value:D5}");

    /// <inheritdoc />
    public int CompareTo(PartNumber other) => Value.CompareTo(other.Value);

    public static bool operator <(PartNumber left, PartNumber right) => left.CompareTo(right) < 0;

    public static bool operator >(PartNumber left, PartNumber right) => left.CompareTo(right) > 0;

    public static bool operator <=(PartNumber left, PartNumber right) => left.CompareTo(right) <= 0;

    public static bool operator >=(PartNumber left, PartNumber right) => left.CompareTo(right) >= 0;

    /// <summary>
    /// Reads <paramref name="text"/> as a part number. True only for <see cref="Prefix"/>, <see cref="Separator"/>
    /// and exactly <see cref="DigitCount"/> ASCII digits whose value is between <see cref="FirstValue"/> and
    /// <see cref="LastValue"/>. Surrounding white space is ignored and the prefix is matched ignoring case.
    /// </summary>
    public static bool TryParse(string? text, out PartNumber number)
    {
        number = default;
        if (text is null)
        {
            return false;
        }

        var span = text.AsSpan().Trim();
        if (span.Length != Prefix.Length + 1 + DigitCount)
        {
            return false;
        }

        if (!span[..Prefix.Length].Equals(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (span[Prefix.Length] != Separator)
        {
            return false;
        }

        var value = 0;
        foreach (var c in span[(Prefix.Length + 1)..])
        {
            if (!char.IsAsciiDigit(c))
            {
                return false;
            }

            value = (value * 10) + (c - '0');
        }

        if (value < FirstValue || value > LastValue)
        {
            return false;
        }

        number = new PartNumber(value);
        return true;
    }

    /// <summary>
    /// Reads <paramref name="text"/> as a part number, as <see cref="TryParse"/> does. Throws
    /// <see cref="FormatException"/>, naming the text, when it is not one.
    /// </summary>
    public static PartNumber Parse(string text)
    {
        if (TryParse(text, out var number))
        {
            return number;
        }

        throw new FormatException(
            $"\"{text}\" is not a part number. Expected {Prefix}{Separator} and {DigitCount} digits, from {new PartNumber(FirstValue)} to {new PartNumber(LastValue)}.");
    }

    /// <summary>True when <paramref name="text"/> is a part number in the format; see <see cref="TryParse"/>.</summary>
    public static bool IsWellFormed(string? text) => TryParse(text, out _);

    /// <summary>
    /// The number after the highest in <paramref name="issued"/>, or <c>AWB-10001</c> when none have been issued.
    /// Gaps are never filled. Throws <see cref="InvalidOperationException"/> when <c>AWB-99999</c> has been used.
    /// </summary>
    public static PartNumber Next(IEnumerable<PartNumber> issued)
    {
        ArgumentNullException.ThrowIfNull(issued);

        // default(PartNumber) has Value 0; it is below every real number, so it never wins as the highest.
        var highest = 0;
        foreach (var number in issued)
        {
            if (number.Value > highest)
            {
                highest = number.Value;
            }
        }

        if (highest == 0)
        {
            return new PartNumber(FirstValue);
        }

        if (highest >= LastValue)
        {
            throw new InvalidOperationException(
                $"The part number sequence is full: {new PartNumber(LastValue)} has been used.");
        }

        return new PartNumber(highest + 1);
    }
}
