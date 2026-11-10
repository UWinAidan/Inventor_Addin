using System.Globalization;

namespace InventorAddin.Core.Holes;

/// <summary>
/// Turns the window's hole depths into the depths Inventor measures, and back (task 114, task 116; spike 108 questions
/// 1 and 3).
/// </summary>
/// <remarks>
/// <para>
/// <b>Where each side measures from.</b> Inventor measures a drilled, counterbore or countersink hole's <c>Depth</c>
/// from the start face, but a spotface hole's from the bottom of the spotface (spike 108: Through All in a 20 mm block
/// reads 1.95 cm with a 0.5 mm spotface; blind 12 with a 0.5 mm spotface ends 12.5 mm below the top). A thread depth is
/// taken to be measured from the same place as the drill depth: from the start face with a counterbore (task 099: an M6
/// thread of 9 under a 6 mm counterbore kept about 3 mm of thread) and a countersink (Default), and from the spotface
/// bottom with a spotface (Default, task 114; neither confirmed in Inventor).
/// </para>
/// <para>
/// The window measures a tapped or taper tapped hole's thread and drill depths from the bottom of the seat (Aidan,
/// 2026-10-24, spec 09 question 12; task 116), and a blind clearance, dowel or Simple hole's Depth from the top face
/// (spec 09 question 16, Default). The seat's depth (<see cref="SeatDepthCm"/>) is the counterbore or spotface depth,
/// or for a countersink the depth where the cone meets the hole diameter (<see cref="CountersinkDepth"/>, Default).
/// </para>
/// <para>
/// So Inventor's depth is the window's plus the seat depth times <see cref="Direction"/>: +1 for a counterbore or
/// countersink on a threaded hole, -1 for a spotface on any other hole, else 0 (a threaded spotface hole, and a hole
/// with no seat, are measured from the same place on both sides).
/// </para>
/// <para>
/// A depth typed as an expression stays one in Inventor: the add-in writes <c>(window) + (seat)</c> or
/// <c>(window) - (seat)</c>, the seat as its own expression or its length with the window's unit, so the hole follows
/// the part's parameters and the seat depth. Reading back, an expression of that form whose second part is the seat's
/// gives its first part again; any other expression gets the seat depth taken off (or added, for a spotface on a hole
/// that is not threaded), so it keeps its parameters.
/// </para>
/// </remarks>
public static class HoleDepthReference
{
    /// <summary>The number of decimals a seat depth worked out from a countersink is written with, in the window's units.</summary>
    public const int CountersinkDepthDecimals = 6;

    /// <summary>True when the window measures the hole's depths from the bottom of the seat: tapped and taper tapped holes.</summary>
    public static bool BelowSeat(HoleKind kind) => HoleStandards.IsThreaded(kind);

    /// <summary>
    /// How the seat depth goes from the window's depth to Inventor's: +1 (added), -1 (taken off) or 0 (the same).
    /// </summary>
    /// <param name="threaded">The hole is tapped or taper tapped (<see cref="BelowSeat"/>).</param>
    public static int Direction(bool threaded, HoleSeat? seat) => (threaded, seat) switch
    {
        (true, HoleSeat.Counterbore or HoleSeat.Countersink) => 1,
        (false, HoleSeat.Spotface) => -1,
        _ => 0,
    };

    /// <summary>
    /// The depth of a countersink's cone, from the top face down to where it meets the hole diameter, in the units the
    /// diameters are in; null when the sizes make no cone (a countersink no wider than the hole, an angle outside 0 to
    /// 180 degrees).
    /// </summary>
    /// <param name="angleDeg">The included angle.</param>
    public static double? CountersinkDepth(double? countersinkDiameter, double? holeDiameter, double? angleDeg)
    {
        if (countersinkDiameter is not { } sink || holeDiameter is not { } hole || angleDeg is not { } angle
            || hole <= 0 || sink <= hole || angle <= 0 || angle >= 180)
        {
            return null;
        }

        return (sink - hole) / 2 / Math.Tan(angle / 2 * Math.PI / 180);
    }

    /// <summary>
    /// The seat's depth in centimetres: the counterbore or spotface depth, the countersink's cone depth
    /// (<see cref="CountersinkDepth"/>, at <paramref name="holeDiameterCm"/>, the modelled diameter: the tap drill for a
    /// tapped hole); null for no seat or a value not known.
    /// </summary>
    public static double? SeatDepthCm(
        HoleSeat? seat,
        double? counterboreDepthCm,
        double? countersinkDiameterCm,
        double? countersinkAngleDeg,
        double? holeDiameterCm,
        double? spotfaceDepthCm) => seat switch
    {
        HoleSeat.Counterbore => counterboreDepthCm,
        HoleSeat.Spotface => spotfaceDepthCm,
        HoleSeat.Countersink => CountersinkDepth(countersinkDiameterCm, holeDiameterCm, countersinkAngleDeg),
        _ => null,
    };

    /// <summary>The depth Inventor takes for a window depth, in centimetres; null stays null.</summary>
    /// <param name="seatDepthCm">The seat's depth (<see cref="SeatDepthCm"/>); null changes nothing.</param>
    public static double? ToInventorCm(HoleKind kind, HoleSeat seat, double? windowDepthCm, double? seatDepthCm) =>
        Shift(Direction(BelowSeat(kind), seat), windowDepthCm, seatDepthCm);

    /// <summary>The window depth for a depth Inventor reports, in centimetres; null stays null.</summary>
    /// <param name="threaded">The hole is tapped or taper tapped.</param>
    public static double? ToWindowCm(bool threaded, HoleSeat? seat, double? inventorDepthCm, double? seatDepthCm) =>
        Shift(-Direction(threaded, seat), inventorDepthCm, seatDepthCm);

    private static double? Shift(int direction, double? depthCm, double? seatDepthCm) =>
        depthCm is { } depth && seatDepthCm is { } seatDepth && direction != 0 ? depth + direction * seatDepth : depthCm;

    /// <summary>
    /// The expression Inventor takes for a window depth: the window's own expression (or null for a plain number) when
    /// the seat depth does not move it, or when neither the depth nor the seat depth is an expression (the number from
    /// <see cref="ToInventorCm"/> is written); else <c>(window) + (seat)</c> or <c>(window) - (seat)</c>, each side its
    /// expression or its length with the window's unit.
    /// </summary>
    /// <param name="windowExpression">The depth's expression, or null for a plain number.</param>
    /// <param name="windowDepth">The depth's value in <paramref name="units"/>.</param>
    /// <param name="seatExpression">The seat depth's expression, or null for a plain number.</param>
    /// <param name="seatDepth">The seat depth's value in <paramref name="units"/>.</param>
    public static string? ToInventorExpression(
        HoleKind kind,
        HoleSeat seat,
        string? windowExpression,
        decimal? windowDepth,
        string? seatExpression,
        decimal? seatDepth,
        HoleUnits units)
    {
        var window = HoleExpressions.Clean(windowExpression);
        var direction = Direction(BelowSeat(kind), seat);
        var seatText = HoleExpressions.Clean(seatExpression);
        if (direction == 0 || (window is null && seatText is null))
        {
            return window;
        }

        var left = window ?? (windowDepth is { } d ? LengthText(d, units) : null);
        var right = seatText ?? (seatDepth is { } s ? LengthText(s, units) : null);
        if (left is null || right is null)
        {
            return window;
        }

        return $"({left}) {(direction > 0 ? '+' : '-')} ({right})";
    }

    /// <summary>
    /// The window expression for an expression Inventor reports: unchanged for null and when the seat depth does not
    /// move it; the first part of <c>(window) + (seat)</c> or <c>(window) - (seat)</c> as the add-in writes it, when
    /// the second part is this seat's (its expression, or a length equal to its depth), and null when that first part
    /// is a plain length (the depth's number is then shown); else the expression with the seat depth taken back off or
    /// added back, the seat as its expression or its length in millimetres.
    /// </summary>
    /// <param name="threaded">The hole is tapped or taper tapped.</param>
    public static string? ToWindowExpression(
        bool threaded, HoleSeat? seat, string? inventorExpression, double? seatDepthCm, string? seatExpression)
    {
        var expression = HoleExpressions.Clean(inventorExpression);
        var direction = Direction(threaded, seat);
        if (expression is null || direction == 0 || seatDepthCm is not { } seatCm)
        {
            return expression;
        }

        var seatText = HoleExpressions.Clean(seatExpression);
        if (SplitWritten(expression, direction > 0 ? '+' : '-') is var (first, second)
            && (Same(second, seatText) || (ParseLengthCm(second) is { } secondCm
                && Math.Abs(secondCm - seatCm) < HoleStateComparison.LengthToleranceCm)))
        {
            return ParseLengthCm(first) is null ? first : null;
        }

        var seatTerm = seatText ?? (seatCm * 10).ToString("0.######", CultureInfo.InvariantCulture) + " mm";
        return $"({expression}) {(direction > 0 ? '-' : '+')} ({seatTerm})";
    }

    /// <summary>
    /// <paramref name="facts"/> with its depths as the window measures them (<see cref="ToWindowCm"/>,
    /// <see cref="ToWindowExpression"/>): the hole depth and the thread depth. Facts whose seat depth does not move them
    /// are returned as they are.
    /// </summary>
    /// <param name="holeDiameterCm">
    /// The modelled diameter for a countersink's cone depth when the facts carry none: Inventor does not report a tapped
    /// hole's diameter (task 099), so the caller gives the tap drill the add-in modelled it at.
    /// </param>
    public static InventorHoleFacts ToWindow(InventorHoleFacts facts, double? holeDiameterCm = null)
    {
        ArgumentNullException.ThrowIfNull(facts);
        var threaded = facts.Tapped;
        var seatDepth = SeatDepthOf(facts, holeDiameterCm);
        if (Direction(threaded, facts.Seat) == 0 || seatDepth is not { } seatCm)
        {
            return facts;
        }

        var seatExpression = SeatExpressionOf(facts);
        return facts with
        {
            DepthCm = ToWindowCm(threaded, facts.Seat, facts.DepthCm, seatCm),
            ThreadDepthCm = ToWindowCm(threaded, facts.Seat, facts.ThreadDepthCm, seatCm),
            DepthExpression = ToWindowExpression(threaded, facts.Seat, facts.DepthExpression, seatCm, seatExpression),
            ThreadDepthExpression =
                ToWindowExpression(threaded, facts.Seat, facts.ThreadDepthExpression, seatCm, seatExpression),
        };
    }

    /// <summary>
    /// The seat depth of the hole <paramref name="facts"/> describe, in centimetres (<see cref="SeatDepthCm"/>); a
    /// countersink's cone at the facts' diameter, else at <paramref name="holeDiameterCm"/>. On a threaded hole the
    /// given diameter comes first, since it is the one the window worked the cone out at.
    /// </summary>
    public static double? SeatDepthOf(InventorHoleFacts facts, double? holeDiameterCm = null)
    {
        ArgumentNullException.ThrowIfNull(facts);
        var diameter = facts.Tapped ? holeDiameterCm ?? facts.HoleDiameterCm : facts.HoleDiameterCm ?? holeDiameterCm;
        return SeatDepthCm(
            facts.Seat,
            facts.CounterboreDepthCm,
            facts.CountersinkDiameterCm,
            facts.CountersinkAngleDeg,
            diameter,
            facts.SpotfaceDepthCm);
    }

    private static string? SeatExpressionOf(InventorHoleFacts facts) => facts.Seat switch
    {
        HoleSeat.Counterbore => facts.CounterboreDepthExpression,
        HoleSeat.Spotface => facts.SpotfaceDepthExpression,
        _ => null,
    };

    /// <summary>
    /// A spec stored before task 116 (<see cref="HoleStoredData.DepthsBelowSeatVersion"/>) with its depths as the window
    /// measures them now: a seated blind tapped or taper tapped hole's drill and thread depths were from the top face,
    /// and are now below the seat, so the seat depth is taken off each (a number less the seat's, an expression as
    /// <c>(expression) - (seat)</c>, the seat as its expression or its length). Any other spec, and one whose seat depth
    /// is not known or is 0, is returned as it is. The hole in Inventor is the same either way.
    /// </summary>
    /// <param name="seatDepthCm">The seat depth the hole was made with (<see cref="SeatDepthCm"/>).</param>
    /// <param name="seatExpression">The seat depth's expression, when it was typed as one; else null.</param>
    public static HoleSpec FromTopFace(HoleSpec spec, double? seatDepthCm, string? seatExpression)
    {
        ArgumentNullException.ThrowIfNull(spec);
        if (!BelowSeat(spec.Kind) || spec.Seat == HoleSeat.None || !spec.IsBlind
            || seatDepthCm is not { } seatCm || seatCm <= 0)
        {
            return spec;
        }

        var units = spec.Units;
        // To 6 decimals in the spec's units, as HoleRules writes a countersink's cone: FromCm's 5 decimals of an inch
        // can be out by 1.27e-5 cm, more than a re-apply check allows, on an irrational cone depth.
        var seat = Math.Round(
            (decimal)(units == HoleUnits.Metric ? seatCm * 10 : seatCm / 2.54), CountersinkDepthDecimals);
        var seatText = HoleExpressions.Clean(seatExpression) ?? LengthText(seat, units);

        string? Expression(string? expression) =>
            HoleExpressions.Clean(expression) is { } text ? $"({text}) - ({seatText})" : null;

        return spec with
        {
            DrillDepth = spec.DrillDepth - seat,
            DrillDepthExpression = Expression(spec.DrillDepthExpression),
            ThreadDepth = spec.Kind == HoleKind.Tapped ? spec.ThreadDepth - seat : spec.ThreadDepth,
            ThreadDepthExpression = spec.Kind == HoleKind.Tapped
                ? Expression(spec.ThreadDepthExpression)
                : spec.ThreadDepthExpression,
        };
    }

    /// <summary>
    /// <paramref name="data"/> as data of <see cref="HoleStoredData.CurrentVersion"/>: data stored before
    /// <see cref="HoleStoredData.DepthsBelowSeatVersion"/> has its spec converted (<see cref="FromTopFace"/>), the seat
    /// depth taken from the stored spec, else from <paramref name="facts"/>; a countersink's cone at the diameter the
    /// add-in applied. Newer data is returned as it is.
    /// </summary>
    public static HoleStoredData Upgrade(HoleStoredData data, InventorHoleFacts facts)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(facts);
        if (data.Version >= HoleStoredData.DepthsBelowSeatVersion)
        {
            return data;
        }

        var spec = data.Spec;
        var units = spec.Units;
        double? Cm(decimal? value) => value is { } v ? HoleRules.ToCm(v, units) : null;

        var seatDepth = SeatDepthCm(
            spec.Seat,
            Cm(spec.CboreDepth) ?? facts.CounterboreDepthCm,
            Cm(spec.CsinkDiameter) ?? facts.CountersinkDiameterCm,
            (double?)spec.CsinkAngleDeg ?? facts.CountersinkAngleDeg,
            data.Applied.HoleDiameterCm,
            Cm(spec.SpotfaceDepth) ?? facts.SpotfaceDepthCm);
        var seatExpression = spec.Seat switch
        {
            HoleSeat.Counterbore when spec.CboreDepth is not null => spec.CboreDepthExpression,
            HoleSeat.Spotface when spec.SpotfaceDepth is not null => spec.SpotfaceDepthExpression,
            _ => null,
        };

        return data with
        {
            Spec = FromTopFace(spec, seatDepth, seatExpression),
            Version = HoleStoredData.CurrentVersion,
        };
    }

    private static string LengthText(decimal value, HoleUnits units) =>
        value.ToString("0.######", CultureInfo.InvariantCulture) + " " + HoleExpressions.UnitName(units);

    private static bool Same(string a, string? b) =>
        b is not null && string.Equals(Squeeze(a), Squeeze(b), StringComparison.Ordinal);

    private static string Squeeze(string text) => string.Concat(text.Where(c => !char.IsWhiteSpace(c)));

    /// <summary>
    /// A plain length, <c>6 mm</c>, <c>0.6 cm</c>, <c>0.25 in</c>, in centimetres; null for anything else, a number
    /// with no unit included (the add-in always writes the unit).
    /// </summary>
    private static double? ParseLengthCm(string text)
    {
        var trimmed = text.Trim();
        (string Unit, double Cm)[] units = [("mm", 0.1), ("cm", 1), ("in", 2.54)];
        foreach (var (unit, cm) in units)
        {
            if (trimmed.EndsWith(unit, StringComparison.OrdinalIgnoreCase)
                && double.TryParse(
                    trimmed[..^unit.Length].TrimEnd(),
                    NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture,
                    out var value))
            {
                return value * cm;
            }
        }

        return null;
    }

    /// <summary>
    /// <c>(X, Y)</c> when <paramref name="expression"/> is <c>(X) op (Y)</c>, the brackets matched and nothing outside
    /// them; else null.
    /// </summary>
    private static (string First, string Second)? SplitWritten(string expression, char op)
    {
        var text = expression.Trim();
        if (!text.StartsWith('(') || !text.EndsWith(')'))
        {
            return null;
        }

        var close = MatchingClose(text, 0);
        if (close < 0)
        {
            return null;
        }

        var rest = text[(close + 1)..].TrimStart();
        if (!rest.StartsWith(op))
        {
            return null;
        }

        rest = rest[1..].TrimStart();
        if (!rest.StartsWith('(') || MatchingClose(rest, 0) != rest.Length - 1)
        {
            return null;
        }

        var first = text[1..close].Trim();
        var second = rest[1..^1].Trim();
        return first.Length == 0 || second.Length == 0 ? null : (first, second);
    }

    private static int MatchingClose(string text, int open)
    {
        var depth = 0;
        for (var i = open; i < text.Length; i++)
        {
            if (text[i] == '(')
            {
                depth++;
            }
            else if (text[i] == ')')
            {
                depth--;
                if (depth == 0)
                {
                    return i;
                }
            }
        }

        return -1;
    }
}
