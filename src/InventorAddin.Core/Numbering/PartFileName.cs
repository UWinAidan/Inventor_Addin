using System.Globalization;
using System.Text;

namespace InventorAddin.Core.Numbering;

/// <summary>
/// The file name rule from spec 02, "File names and the part name": <c>{number},{name}{extension}</c> with the
/// part name's spaces written as underscores, for example <c>AWB-10001,mounting_bracket.ipt</c>. A file without a
/// number is <c>{name}{extension}</c>. Only the first comma separates the number, so a comma inside the name is allowed.
/// </summary>
public static class PartFileName
{
    /// <summary>Separates the part number from the part name in a file name.</summary>
    public const char NumberSeparator = ',';

    /// <summary>The longest full path Windows accepts: a path must be shorter than 260 characters.</summary>
    public const int MaxPathLength = 259;

    /// <summary>Characters Windows does not allow in a file name, besides control characters.</summary>
    private static readonly char[] InvalidCharacters = { '\\', '/', ':', '*', '?', '"', '<', '>', '|' };

    /// <summary>Names Windows keeps for devices, matched ignoring case, alone or before a dot.</summary>
    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    private static readonly char[] FolderSeparators = { '\\', '/' };

    /// <summary>
    /// The part name as typed, tidied: trimmed, each underscore turned into a space, and each run of white space
    /// collapsed to one space. Case is kept. Null gives <c>""</c>.
    /// </summary>
    public static string NormaliseName(string? typed)
    {
        if (string.IsNullOrEmpty(typed))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(typed.Length);
        var pendingSpace = false;
        foreach (var c in typed)
        {
            if (c == '_' || char.IsWhiteSpace(c))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(c);
        }

        return builder.ToString();
    }

    /// <summary>
    /// Null when <paramref name="typed"/> can be used as a part name, otherwise a message for the first problem
    /// found: blank, a character Windows does not allow, a trailing dot, or a Windows reserved name. Characters are
    /// checked in the text as typed, so a control character such as a line break is named rather than collapsed.
    /// </summary>
    public static string? ValidateName(string? typed)
    {
        var name = NormaliseName(typed);
        if (name.Length == 0)
        {
            return "Enter a part name.";
        }

        foreach (var c in typed!)
        {
            if (char.IsControl(c))
            {
                return $"A part name cannot contain the character '{CodePoint(c)}'.";
            }

            if (Array.IndexOf(InvalidCharacters, c) >= 0)
            {
                return $"A part name cannot contain the character '{c}'.";
            }
        }

        if (name.EndsWith('.'))
        {
            return "A part name cannot end with a dot.";
        }

        var dot = name.IndexOf('.');
        var stem = dot >= 0 ? name[..dot] : name;
        if (ReservedNames.TryGetValue(stem, out var reserved))
        {
            return $"'{reserved}' is a name Windows keeps for itself.";
        }

        return null;
    }

    /// <summary>
    /// The file name for a part: <c>{number},{name}{extension}</c>, or <c>{name}{extension}</c> when
    /// <paramref name="number"/> is null. The name is normalised (<see cref="NormaliseName"/>) and its spaces written
    /// as underscores. <paramref name="extension"/> includes its dot, for example <c>.ipt</c>. Throws
    /// <see cref="ArgumentException"/> for an extension without a leading dot, or a name that fails
    /// <see cref="ValidateName"/>.
    /// </summary>
    public static string Build(PartNumber? number, string name, string extension)
    {
        if (string.IsNullOrEmpty(extension) || extension[0] != '.' || extension.Length < 2)
        {
            throw new ArgumentException(
                $"The extension must start with a dot, for example .ipt. Got \"{extension}\".",
                nameof(extension));
        }

        var problem = ValidateName(name);
        if (problem is not null)
        {
            throw new ArgumentException(problem, nameof(name));
        }

        var fileName = NormaliseName(name).Replace(' ', '_') + extension;
        return number is { } n ? $"{n}{NumberSeparator}{fileName}" : fileName;
    }

    /// <summary>
    /// Null when <paramref name="folder"/>, a separator and <paramref name="fileName"/> together are no longer than
    /// <see cref="MaxPathLength"/>, otherwise a message. No separator is counted when the folder is blank or already
    /// ends with one.
    /// </summary>
    public static string? ValidatePath(string folder, string fileName)
    {
        ArgumentNullException.ThrowIfNull(folder);
        ArgumentNullException.ThrowIfNull(fileName);

        var separator = folder.Length == 0 || Array.IndexOf(FolderSeparators, folder[^1]) >= 0 ? 0 : 1;
        return folder.Length + separator + fileName.Length > MaxPathLength
            ? "The full path is too long for Windows. Choose a shorter name or folder."
            : null;
    }

    /// <summary>
    /// True when the file name in <paramref name="fullFileName"/> (without folder and extension) has a comma and the
    /// text before the first comma is a part number (<see cref="PartNumber.TryParse"/>).
    /// </summary>
    public static bool TryReadNumber(string? fullFileName, out PartNumber number)
    {
        number = default;
        var stem = Stem(fullFileName);
        var comma = stem.IndexOf(NumberSeparator);
        return comma >= 0 && PartNumber.TryParse(stem[..comma], out number);
    }

    /// <summary>
    /// The file name without folder or last extension, or <c>""</c> for null or blank. Both <c>\</c> and <c>/</c>
    /// count as folder separators on every platform.
    /// </summary>
    internal static string Stem(string? fullFileName)
    {
        if (string.IsNullOrWhiteSpace(fullFileName))
        {
            return string.Empty;
        }

        var name = fullFileName[(fullFileName.LastIndexOfAny(FolderSeparators) + 1)..];
        var dot = name.LastIndexOf('.');
        return dot >= 0 ? name[..dot] : name;
    }

    private static string CodePoint(char c) =>
        string.Create(CultureInfo.InvariantCulture, $"U+{(int)c:X4}");
}
