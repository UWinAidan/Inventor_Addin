using System.Globalization;
using InventorAddin.Core.Settings;

namespace InventorAddin.Core.Drawing;

/// <summary>How the part number is marked on the part (spec 05, "Added for M5", item 2).</summary>
public enum PartMarkingMethod
{
    LaserEngrave,
    Stamp,
    Etch,
    Ink,

    /// <summary>No marking method: the note carries only the extra lines (for example "DO NOT STAMP").</summary>
    None,
}

/// <summary>Where the marking is shown on the drawing.</summary>
public enum PartMarkingTarget
{
    /// <summary>A leader from the note to a face on the iso view.</summary>
    Leader,

    /// <summary>A dashed zone on the chosen view, with the zone text.</summary>
    Zone,
}

/// <summary>The marking options the part marking window collects (spec 05, task 198).</summary>
public sealed record PartMarkingOptions
{
    public PartMarkingMethod Method { get; init; } = PartMarkingMethod.LaserEngrave;

    /// <summary>Extra note lines, in order. Blank lines are ignored when the note is built.</summary>
    public IReadOnlyList<string> ExtraLines { get; init; } = [];

    public PartMarkingTarget Target { get; init; } = PartMarkingTarget.Leader;

    /// <summary>Also add an engraved part number feature to the model. Never on for an assembly.</summary>
    public bool EngraveFeature { get; init; }

    /// <summary>The part's units, which pick the character height, depth and zone size.</summary>
    public UnitsSystem Units { get; init; } = UnitsSystem.Metric;

    public bool IsAssembly { get; init; }

    /// <summary>The highlight colour, <c>#RRGGBB</c>.</summary>
    public string HighlightColour { get; init; } = PartMarking.DefaultHighlightColour;
}

/// <summary>One line of the leader note: either text, or the place for the model's part number (then <see cref="Text"/> is empty).</summary>
public sealed record LeaderNoteLine(string Text, bool IsPartNumber);

/// <summary>The sizes, words and note text of part marking (spec 05, question 16 defaults).</summary>
public static class PartMarking
{
    /// <summary>The default highlight colour (Settings, <c>PartMarkingColour</c>).</summary>
    public const string DefaultHighlightColour = "#0070C0";

    public const string ZoneText = "MARKING MUST BE PLACED INSIDE THIS ZONE";

    public const decimal CharacterHeightMm = 3m;
    public const decimal CharacterHeightIn = 0.12m;
    public const decimal EngraveDepthMm = 0.2m;
    public const decimal EngraveDepthIn = 0.008m;
    public const decimal ZoneWidthMm = 40m;
    public const decimal ZoneHeightMm = 15m;
    public const decimal ZoneWidthIn = 1.5m;
    public const decimal ZoneHeightIn = 0.6m;

    private const decimal MmPerInch = 25.4m;

    /// <summary>The characters' height in the part's units (mm or in).</summary>
    public static decimal CharacterHeight(UnitsSystem units) =>
        units == UnitsSystem.Imperial ? CharacterHeightIn : CharacterHeightMm;

    /// <summary>The engraving depth in the part's units (mm or in).</summary>
    public static decimal EngraveDepth(UnitsSystem units) =>
        units == UnitsSystem.Imperial ? EngraveDepthIn : EngraveDepthMm;

    public static decimal ZoneWidth(UnitsSystem units) =>
        units == UnitsSystem.Imperial ? ZoneWidthIn : ZoneWidthMm;

    public static decimal ZoneHeight(UnitsSystem units) =>
        units == UnitsSystem.Imperial ? ZoneHeightIn : ZoneHeightMm;

    /// <summary>The characters' height in centimetres, Inventor's length unit.</summary>
    public static double CharacterHeightCm(UnitsSystem units) => ToCm(CharacterHeight(units), units);

    public static double EngraveDepthCm(UnitsSystem units) => ToCm(EngraveDepth(units), units);

    public static double ZoneWidthCm(UnitsSystem units) => ToCm(ZoneWidth(units), units);

    public static double ZoneHeightCm(UnitsSystem units) => ToCm(ZoneHeight(units), units);

    private static double ToCm(decimal value, UnitsSystem units) =>
        (double)(units == UnitsSystem.Imperial ? value * MmPerInch : value) / 10.0;

    /// <summary>The unit word after a number: "mm" or "in".</summary>
    public static string UnitWord(UnitsSystem units) => units == UnitsSystem.Imperial ? "in" : "mm";

    /// <summary>A size with its unit, for example "3 mm" or "0.12 in".</summary>
    public static string SizeText(decimal value, UnitsSystem units) =>
        value.ToString("0.###", CultureInfo.InvariantCulture) + " " + UnitWord(units);

    /// <summary>The zone size for display, for example "40 x 15 mm" or "1.5 x 0.6 in".</summary>
    public static string ZoneSizeText(UnitsSystem units) =>
        $"{ZoneWidth(units).ToString("0.###", CultureInfo.InvariantCulture)} x " +
        $"{ZoneHeight(units).ToString("0.###", CultureInfo.InvariantCulture)} {UnitWord(units)}";

    /// <summary>The method's words, for example "LASER ENGRAVE"; null for None.</summary>
    public static string? MethodPhrase(PartMarkingMethod method) => method switch
    {
        PartMarkingMethod.LaserEngrave => "LASER ENGRAVE",
        PartMarkingMethod.Stamp => "STAMP",
        PartMarkingMethod.Etch => "ETCH",
        PartMarkingMethod.Ink => "INK MARK",
        _ => null,
    };

    /// <summary>The method's name for a list.</summary>
    public static string MethodName(PartMarkingMethod method) => method switch
    {
        PartMarkingMethod.LaserEngrave => "Laser engrave",
        PartMarkingMethod.Stamp => "Stamp",
        PartMarkingMethod.Etch => "Etch",
        PartMarkingMethod.Ink => "Ink",
        _ => "None",
    };

    /// <summary>
    /// The note's lines: the method line, for example "LASER ENGRAVE PART NUMBER, 3 mm CHARACTERS" (none for method
    /// None), then each non-blank extra line, trimmed.
    /// </summary>
    public static IReadOnlyList<string> NoteLines(PartMarkingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var lines = new List<string>();
        string? phrase = MethodPhrase(options.Method);
        if (phrase is not null)
            lines.Add($"{phrase} PART NUMBER, {SizeText(CharacterHeight(options.Units), options.Units)} CHARACTERS");

        foreach (string line in options.ExtraLines ?? [])
        {
            string trimmed = (line ?? string.Empty).Trim();
            if (trimmed.Length > 0)
                lines.Add(trimmed);
        }

        return lines;
    }

    /// <summary>
    /// The leader note's lines: <see cref="NoteLines"/> with the part number line after the method line. The part number line is
    /// marked by <see cref="LeaderNoteLine.IsPartNumber"/>; there is none when the method is None.
    /// </summary>
    public static IReadOnlyList<LeaderNoteLine> LeaderNoteLines(PartMarkingOptions options)
    {
        IReadOnlyList<string> lines = NoteLines(options);
        bool hasMethodLine = MethodPhrase(options.Method) is not null;
        var result = new List<LeaderNoteLine>();
        for (int i = 0; i < lines.Count; i++)
        {
            result.Add(new LeaderNoteLine(lines[i], false));

            // The first line is the method line unless the method is None; the number follows it.
            if (i == 0 && hasMethodLine)
                result.Add(new LeaderNoteLine(string.Empty, true));
        }

        return result;
    }

    /// <summary>The leader note as the window previews it: the part number line reads <see cref="PartNumberPlaceholder"/>.</summary>
    public static string LeaderPreviewText(PartMarkingOptions options) =>
        string.Join("\n", LeaderNoteLines(options).Select(l => l.IsPartNumber ? PartNumberPlaceholder : l.Text));

    /// <summary>The preview's stand-in for the model's part number.</summary>
    public const string PartNumberPlaceholder = "<part number>";

    /// <summary>The note text, the lines joined with <c>\n</c>. Empty when there is nothing to say.</summary>
    public static string NoteText(PartMarkingOptions options) => string.Join("\n", NoteLines(options));

    /// <summary>Splits typed text into extra lines, one per line, blanks dropped.</summary>
    public static IReadOnlyList<string> SplitExtraLines(string? text) =>
        (text ?? string.Empty)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .ToList();

    /// <summary>The engrave feature is offered for a part with a method; never for an assembly.</summary>
    public static bool CanEngraveFeature(PartMarkingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return !options.IsAssembly && options.Method != PartMarkingMethod.None;
    }

    /// <summary>Whether the engrave feature will be added: asked for and allowed.</summary>
    public static bool WillEngraveFeature(PartMarkingOptions options) =>
        options.EngraveFeature && CanEngraveFeature(options);

    /// <summary>
    /// A colour as <c>#RRGGBB</c> (upper case), or <see cref="DefaultHighlightColour"/> when the text is not a
    /// 6-digit hex colour (an optional leading <c>#</c>).
    /// </summary>
    public static string NormaliseColour(string? text)
    {
        string t = (text ?? string.Empty).Trim();
        if (t.StartsWith('#'))
            t = t[1..];
        if (t.Length != 6 || !t.All(Uri.IsHexDigit))
            return DefaultHighlightColour;
        return "#" + t.ToUpperInvariant();
    }

    /// <summary>The red, green and blue bytes of a colour (an invalid one reads as the default).</summary>
    public static (byte R, byte G, byte B) ColourRgb(string? text)
    {
        string hex = NormaliseColour(text)[1..];
        return (
            byte.Parse(hex[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            byte.Parse(hex[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            byte.Parse(hex[4..], NumberStyles.HexNumber, CultureInfo.InvariantCulture));
    }
}
