using System.Globalization;

namespace InventorAddin.Core.Holes;

/// <summary>
/// Checks that the seat Inventor reports for a hole is the seat its <see cref="HoleGeometry"/> asked for, after every
/// create or change (task 093: a counterbore that did not follow a size change). Lengths match when closer than
/// <see cref="HoleStateComparison.LengthToleranceCm"/>; angles within <see cref="HoleReadBack.AngleToleranceDeg"/>.
/// </summary>
public static class HoleSeatCheck
{
    /// <summary>One line per seat value that differs; empty when the seat matches.</summary>
    public static IReadOnlyList<string> Differences(HoleGeometry geometry, InventorHoleFacts facts)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(facts);

        var differences = new List<string>();
        if (facts.Seat != geometry.Seat)
        {
            differences.Add($"the seat is {SeatName(facts.Seat)}; it should be {SeatName(geometry.Seat)}.");
            return differences.AsReadOnly();
        }

        switch (geometry.Seat)
        {
            case HoleSeat.Counterbore:
                CheckLength("counterbore diameter", geometry.CounterboreDiameterCm, facts.CounterboreDiameterCm, differences);
                CheckLength("counterbore depth", geometry.CounterboreDepthCm, facts.CounterboreDepthCm, differences);
                break;
            case HoleSeat.Countersink:
                CheckLength("countersink diameter", geometry.CountersinkDiameterCm, facts.CountersinkDiameterCm, differences);
                CheckAngle("countersink angle", geometry.CountersinkAngleDeg, facts.CountersinkAngleDeg, differences);
                break;
        }

        return differences.AsReadOnly();
    }

    private static void CheckLength(string name, double? wanted, double? actual, List<string> differences)
    {
        if (wanted is not { } w)
            return;
        if (actual is { } a && Math.Abs(a - w) < HoleStateComparison.LengthToleranceCm)
            return;
        differences.Add($"the {name} is {Mm(actual)}; it should be {Mm(w)}.");
    }

    private static void CheckAngle(string name, double? wanted, double? actual, List<string> differences)
    {
        if (wanted is not { } w)
            return;
        if (actual is { } a && Math.Abs(a - w) < HoleReadBack.AngleToleranceDeg)
            return;
        differences.Add($"the {name} is {Deg(actual)}; it should be {Deg(w)}.");
    }

    private static string SeatName(HoleSeat? seat) => seat switch
    {
        HoleSeat.None => "none",
        HoleSeat.Counterbore => "a counterbore",
        HoleSeat.Countersink => "a countersink",
        _ => "another kind (a spot face)",
    };

    private static string Mm(double? cm) =>
        cm is { } v ? (v * 10).ToString("0.####", CultureInfo.InvariantCulture) + " mm" : "unreadable";

    private static string Deg(double? deg) =>
        deg is { } v ? v.ToString("0.##", CultureInfo.InvariantCulture) + " deg" : "unreadable";
}
