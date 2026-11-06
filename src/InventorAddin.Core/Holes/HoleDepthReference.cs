using System.Globalization;

namespace InventorAddin.Core.Holes;

/// <summary>
/// Turns the window's hole depths into the depths Inventor measures, and back (task 114; spike 108 question 1).
/// </summary>
/// <remarks>
/// <para>
/// Inventor measures a counterbore or countersink hole's <c>Depth</c> from the start face, as the window does today, but
/// a spotface hole's from the bottom of the spotface (spike 108: Through All in a 20 mm block reads 1.95 cm with a
/// 0.5 mm spotface; blind 12 with a 0.5 mm spotface ends 12.5 mm below the top). The window measures a blind hole's
/// Depth from the top face (spec 09 question 16, Default), and until task 116 also a tapped hole's thread and drill
/// depths. So for a spotface the add-in writes the window's depth less the spotface depth, and reads Inventor's depth
/// plus the spotface depth; every other seat is written and read unchanged.
/// </para>
/// <para>
/// A thread depth on a spotface hole is taken to start where the hole starts, at the bottom of the spotface, as the
/// drill depth does (not confirmed in Inventor; task 114's checklist). Task 116 moves the thread and drill depths of
/// tapped and taper tapped holes below every seat: it changes these rules for threaded holes, in this one place.
/// </para>
/// <para>
/// A depth typed as an expression stays one in Inventor: the add-in writes <c>(window) - (spotface)</c>, with either
/// side an expression or a length with its unit, so the hole follows the part's parameters and the spotface depth.
/// Reading back, an expression of that form gives its first part again; any other expression gets the spotface depth
/// added, so it keeps its parameters.
/// </para>
/// </remarks>
public static class HoleDepthReference
{
    /// <summary>
    /// True when Inventor measures the depths of a hole with <paramref name="seat"/> from somewhere other than where the
    /// window measures them: a spotface, from its bottom.
    /// </summary>
    public static bool Shifts(HoleSeat seat) => seat == HoleSeat.Spotface;

    /// <summary>The depth Inventor takes for a window depth, in centimetres; null stays null.</summary>
    /// <param name="spotfaceDepthCm">The spotface depth; null or another seat changes nothing.</param>
    public static double? ToInventorCm(HoleSeat seat, double? windowDepthCm, double? spotfaceDepthCm) =>
        windowDepthCm is { } depth && Shifts(seat) && spotfaceDepthCm is { } spot ? depth - spot : windowDepthCm;

    /// <summary>The window depth for a depth Inventor reports, in centimetres; null stays null.</summary>
    public static double? ToWindowCm(HoleSeat? seat, double? inventorDepthCm, double? spotfaceDepthCm) =>
        inventorDepthCm is { } depth && seat is { } s && Shifts(s) && spotfaceDepthCm is { } spot
            ? depth + spot
            : inventorDepthCm;

    /// <summary>
    /// The expression Inventor takes for a window depth: null when neither the depth nor the spotface depth is an
    /// expression (the number from <see cref="ToInventorCm"/> is written), the window's own expression for a seat that
    /// does not shift, else <c>(window) - (spotface)</c>, each side its expression or its length with the window's unit.
    /// </summary>
    /// <param name="windowExpression">The depth's expression, or null for a plain number.</param>
    /// <param name="windowDepth">The depth's value in <paramref name="units"/>.</param>
    /// <param name="spotfaceExpression">The spotface depth's expression, or null for a plain number.</param>
    /// <param name="spotfaceDepth">The spotface depth's value in <paramref name="units"/>.</param>
    public static string? ToInventorExpression(
        HoleSeat seat,
        string? windowExpression,
        decimal? windowDepth,
        string? spotfaceExpression,
        decimal? spotfaceDepth,
        HoleUnits units)
    {
        var window = HoleExpressions.Clean(windowExpression);
        if (!Shifts(seat) || spotfaceDepth is null && HoleExpressions.Clean(spotfaceExpression) is null)
        {
            return window;
        }

        var spot = HoleExpressions.Clean(spotfaceExpression);
        if (window is null && spot is null)
        {
            return null;
        }

        var left = window ?? (windowDepth is { } d ? LengthText(d, units) : null);
        var right = spot ?? (spotfaceDepth is { } s ? LengthText(s, units) : null);
        if (left is null || right is null)
        {
            return window;
        }

        return $"({left}) - ({right})";
    }

    /// <summary>
    /// The window expression for an expression Inventor reports on a hole with <paramref name="seat"/>: unchanged for a
    /// seat that does not shift and for null; the first part of <c>(window) - (spotface)</c> as the add-in writes it;
    /// else <c>(expression) + spotface</c>, the spotface depth as its expression or its length in millimetres.
    /// </summary>
    public static string? ToWindowExpression(
        HoleSeat? seat, string? inventorExpression, double? spotfaceDepthCm, string? spotfaceExpression)
    {
        var expression = HoleExpressions.Clean(inventorExpression);
        if (expression is null || seat is not { } s || !Shifts(s) || spotfaceDepthCm is not { } spotCm)
        {
            return expression;
        }

        if (FirstOfDifference(expression) is { } first)
        {
            return first;
        }

        var spot = HoleExpressions.Clean(spotfaceExpression)
            ?? (spotCm * 10).ToString("0.####", CultureInfo.InvariantCulture) + " mm";
        return $"({expression}) + ({spot})";
    }

    /// <summary>
    /// <paramref name="facts"/> with its depths as the window measures them (<see cref="ToWindowCm"/>,
    /// <see cref="ToWindowExpression"/>): the hole depth and the thread depth. Facts of a seat that does not shift are
    /// returned as they are.
    /// </summary>
    public static InventorHoleFacts ToWindow(InventorHoleFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        if (facts.Seat is not { } seat || !Shifts(seat) || facts.SpotfaceDepthCm is not { } spot)
        {
            return facts;
        }

        return facts with
        {
            DepthCm = ToWindowCm(seat, facts.DepthCm, spot),
            ThreadDepthCm = ToWindowCm(seat, facts.ThreadDepthCm, spot),
            DepthExpression = ToWindowExpression(seat, facts.DepthExpression, spot, facts.SpotfaceDepthExpression),
            ThreadDepthExpression =
                ToWindowExpression(seat, facts.ThreadDepthExpression, spot, facts.SpotfaceDepthExpression),
        };
    }

    private static string LengthText(decimal value, HoleUnits units) =>
        value.ToString("0.######", CultureInfo.InvariantCulture) + " " + HoleExpressions.UnitName(units);

    /// <summary>
    /// <c>X</c> when <paramref name="expression"/> is <c>(X) - (Y)</c>, the brackets matched and nothing outside them;
    /// else null.
    /// </summary>
    private static string? FirstOfDifference(string expression)
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
        if (!rest.StartsWith('-'))
        {
            return null;
        }

        rest = rest[1..].TrimStart();
        if (!rest.StartsWith('(') || MatchingClose(rest, 0) != rest.Length - 1)
        {
            return null;
        }

        var first = text[1..close].Trim();
        return first.Length == 0 ? null : first;
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
