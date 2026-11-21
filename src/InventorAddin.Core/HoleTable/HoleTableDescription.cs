using System.Globalization;
using System.Text;
using InventorAddin.Core.Holes;

namespace InventorAddin.Core.HoleTable;

/// <summary>What one part of a description line is: plain text, or a symbol Inventor draws with its own character.</summary>
public enum HoleDescriptionPartKind
{
    Text,
    Diameter,
    Counterbore,
    Countersink,
    Depth,
    Spotface,
}

/// <summary>One part of a description line. A symbol part carries no text.</summary>
public sealed record HoleDescriptionPart(HoleDescriptionPartKind Kind, string Text = "")
{
    /// <summary>Diameter U+2300.</summary>
    public const string DiameterChar = "⌀";

    /// <summary>Counterbore U+2334.</summary>
    public const string CounterboreChar = "⌴";

    /// <summary>Countersink U+2335.</summary>
    public const string CountersinkChar = "⌵";

    /// <summary>Depth U+21A7.</summary>
    public const string DepthChar = "↧";

    public static HoleDescriptionPart Plain(string text) => new(HoleDescriptionPartKind.Text, text);

    /// <summary>The Unicode form: symbols as characters (spotface as <c>SF</c>), text as is.</summary>
    public string PlainText => Kind switch
    {
        HoleDescriptionPartKind.Diameter => DiameterChar,
        HoleDescriptionPartKind.Counterbore => CounterboreChar,
        HoleDescriptionPartKind.Countersink => CountersinkChar,
        HoleDescriptionPartKind.Depth => DepthChar,
        HoleDescriptionPartKind.Spotface => "SF",
        _ => Text,
    };
}

/// <summary>
/// A stacked tolerance on the first line: the deviation texts, with their sign, upper over lower. The stack sits after
/// the first <see cref="PartsBefore"/> parts of the first line.
/// </summary>
public sealed record HoleDescriptionTolerance(string Upper, string Lower, int PartsBefore);

/// <summary>
/// The DESCRIPTION cell of one hole: lines of parts, top to bottom (task 156). Task 161 turns the parts into Inventor
/// formatted text.
/// </summary>
public sealed record HoleTableDescription(
    IReadOnlyList<IReadOnlyList<HoleDescriptionPart>> Lines,
    HoleDescriptionTolerance? Tolerance)
{
    /// <summary>The first line's parts before the tolerance stack (the whole line when there is no stack).</summary>
    public IReadOnlyList<HoleDescriptionPart> FirstLineBeforeStack =>
        Lines.Count == 0 ? [] : Tolerance is { } t ? Lines[0].Take(t.PartsBefore).ToList() : Lines[0];

    /// <summary>The first line's parts after the tolerance stack; empty when there is no stack.</summary>
    public IReadOnlyList<HoleDescriptionPart> FirstLineAfterStack =>
        Lines.Count == 0 || Tolerance is not { } t ? [] : Lines[0].Skip(t.PartsBefore).ToList();

    /// <summary>The Unicode text, one string per line, a stack written as <c>[+0.012/+0.000]</c>.</summary>
    public IReadOnlyList<string> PlainLines
    {
        get
        {
            var result = new List<string>();
            for (var i = 0; i < Lines.Count; i++)
            {
                result.Add(Render(i, $"[{Tolerance?.Upper}/{Tolerance?.Lower}]"));
            }

            return result;
        }
    }

    /// <summary>The Unicode text for the wizard's lists and logs: lines joined with <c>; </c>.</summary>
    public string PlainText => string.Join("; ", PlainLines);

    /// <summary>Grouping key and order tie-break (task 155): the lines and the tolerance, one per line.</summary>
    public string Key => string.Join("\n", PlainLines);

    private string Render(int line, string stack)
    {
        var sb = new StringBuilder();
        var parts = Lines[line];
        for (var i = 0; i < parts.Count; i++)
        {
            if (line == 0 && Tolerance is { } t && i == t.PartsBefore)
            {
                sb.Append(' ').Append(stack);
            }

            sb.Append(parts[i].PlainText);
        }

        if (line == 0 && Tolerance is { } t2 && t2.PartsBefore >= parts.Count)
        {
            sb.Append(' ').Append(stack);
        }

        return sb.ToString();
    }
}

/// <summary>Writes the DESCRIPTION cell for a hole (task 156; spec 04 "Target output", questions 14 and 15).</summary>
public static class HoleTableDescriptions
{
    /// <summary>The description with the decimals of the hole's own table (<see cref="HoleTableDecimals.For"/>).</summary>
    public static HoleTableDescription For(HoleTableHole hole) =>
        For(hole, HoleTableDecimals.For(hole.IsPrecision, hole.Units));

    /// <summary>
    /// The description. Lengths convert from centimetres to the hole's units and show <paramref name="decimals"/>
    /// decimals (invariant culture, trailing zeros kept); the countersink angle is a whole number of degrees.
    /// </summary>
    public static HoleTableDescription For(HoleTableHole hole, int decimals)
    {
        ArgumentNullException.ThrowIfNull(hole);

        var lines = new List<IReadOnlyList<HoleDescriptionPart>>();
        var metric = hole.Units == HoleUnits.Metric;
        string Len(double cm) => Number(metric ? cm * 10 : cm / 2.54, decimals);

        // Hole line.
        var first = new List<HoleDescriptionPart>();
        if (hole.DrillDiameterCm is { } drill)
        {
            first.Add(new(HoleDescriptionPartKind.Diameter));
            first.Add(HoleDescriptionPart.Plain(Len(drill)));
        }

        var stackAt = first.Count;
        var tolerance = default(HoleDescriptionTolerance);
        if (hole.Tolerance is { } tol)
        {
            var places = metric ? 3 : 4;
            string Dev(double cm) => Signed(metric ? cm * 10 : cm / 2.54, places);
            if (!string.IsNullOrEmpty(tol.IsoCode))
            {
                first.Add(HoleDescriptionPart.Plain(" " + tol.IsoCode));
            }

            stackAt = first.Count;
            tolerance = new HoleDescriptionTolerance(Dev(tol.UpperCm), Dev(tol.LowerCm), stackAt);
        }

        var tapped = hole.Type is HoleTableType.Tapped or HoleTableType.TaperTapped;
        // A hole with no drill diameter (taper tapped, tap drill unknown) has no hole line, not even a termination.
        if (hole.DrillDiameterCm is not null)
        {
            AppendTermination(first, hole.Termination, hole.DepthCm, Len, false);
            lines.Add(first);
        }

        // Seat line: the seat kind is the hole's own Seat, else the table type's.
        var seatKind = hole.Seat ?? hole.Type switch
        {
            HoleTableType.Counterbore => HoleSeat.Counterbore,
            HoleTableType.Countersink => HoleSeat.Countersink,
            HoleTableType.Spotface => HoleSeat.Spotface,
            _ => HoleSeat.None,
        };
        if (hole.SeatDiameterCm is { } seat && seatKind != HoleSeat.None)
        {
            var line = new List<HoleDescriptionPart>();
            switch (seatKind)
            {
                case HoleSeat.Counterbore:
                    SeatLine(line, HoleDescriptionPartKind.Counterbore, seat, hole.SeatDepthCm, Len);
                    break;
                case HoleSeat.Spotface:
                    line.Add(new(HoleDescriptionPartKind.Spotface));
                    line.Add(HoleDescriptionPart.Plain(" "));
                    SeatLine(line, null, seat, hole.SeatDepthCm, Len);
                    break;
                default:
                    SeatLine(line, HoleDescriptionPartKind.Countersink, seat, null, Len, hole.SeatAngleDeg);
                    break;
            }

            lines.Add(line);
        }

        // Thread line.
        if (tapped && !string.IsNullOrWhiteSpace(hole.ThreadDesignation))
        {
            var line = new List<HoleDescriptionPart>();
            var name = hole.ThreadDesignation!;
            if (hole.Type == HoleTableType.Tapped && !string.IsNullOrWhiteSpace(hole.ThreadClass))
            {
                name += " - " + hole.ThreadClass;
            }

            line.Add(HoleDescriptionPart.Plain(name));
            if (hole.Type == HoleTableType.TaperTapped)
            {
                if (hole.TaperEngagementCm is { } engagement)
                {
                    line.Add(HoleDescriptionPart.Plain(" "));
                    line.Add(new(HoleDescriptionPartKind.Depth));
                    line.Add(HoleDescriptionPart.Plain(Len(engagement)));
                }
            }
            else if (hole.ThreadDepthCm is { } threadDepth)
            {
                line.Add(HoleDescriptionPart.Plain(" "));
                line.Add(new(HoleDescriptionPartKind.Depth));
                line.Add(HoleDescriptionPart.Plain(Len(threadDepth)));
            }
            else if (hole.Termination == HoleTermination.ThroughAll)
            {
                line.Add(HoleDescriptionPart.Plain(" THRU"));
            }
            else if (hole.Termination == HoleTermination.ToFace)
            {
                line.Add(HoleDescriptionPart.Plain(" TO FACE"));
            }

            lines.Add(line);
        }

        return new HoleTableDescription(lines, lines.Count > 0 ? tolerance : null);
    }

    private static void SeatLine(
        List<HoleDescriptionPart> line,
        HoleDescriptionPartKind? symbol,
        double diameterCm,
        double? depthCm,
        Func<double, string> len,
        double? angleDeg = null)
    {
        if (symbol is { } s)
        {
            line.Add(new(s));
        }

        line.Add(new(HoleDescriptionPartKind.Diameter));
        if (angleDeg is { } angle)
        {
            line.Add(HoleDescriptionPart.Plain(len(diameterCm) + " X " + Math.Round(angle, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture) + "°"));
        }
        else if (depthCm is { } depth)
        {
            line.Add(HoleDescriptionPart.Plain(len(diameterCm) + " "));
            line.Add(new(HoleDescriptionPartKind.Depth));
            line.Add(HoleDescriptionPart.Plain(len(depth)));
        }
        else
        {
            line.Add(HoleDescriptionPart.Plain(len(diameterCm)));
        }
    }

    private static void AppendTermination(
        List<HoleDescriptionPart> line,
        HoleTermination? termination,
        double? depthCm,
        Func<double, string> len,
        bool first)
    {
        var lead = first ? "" : " ";
        switch (termination)
        {
            case HoleTermination.ThroughAll:
                line.Add(HoleDescriptionPart.Plain(lead + "THRU"));
                break;
            case HoleTermination.ToFace:
                line.Add(HoleDescriptionPart.Plain(lead + "TO FACE"));
                break;
            case HoleTermination.Distance when depthCm is { } depth:
                line.Add(HoleDescriptionPart.Plain(lead));
                line.Add(new(HoleDescriptionPartKind.Depth));
                line.Add(HoleDescriptionPart.Plain(len(depth)));
                break;
        }
    }

    private static string Number(double value, int decimals)
    {
        var text = Math.Round(value, decimals, MidpointRounding.AwayFromZero).ToString("F" + decimals, CultureInfo.InvariantCulture);
        return text.StartsWith('-') && text.Trim('-', '0', '.').Length == 0 ? text[1..] : text;
    }

    private static string Signed(double value, int decimals)
    {
        var text = Number(value, decimals);
        return text.StartsWith('-') ? text : "+" + text;
    }
}
