using InventorAddin.Core.Holes;

namespace InventorAddin.Core.HoleTable;

/// <summary>What a group of identical holes sorts by.</summary>
/// <param name="TieBreak">Compared ordinally when type and diameter tie; task 156 passes the description text.</param>
public sealed record HoleTableGroupKey(HoleTableType Type, double? DiameterCm, string TieBreak);

/// <summary>
/// How the hole table orders its groups and the instances within a group (spec 04 questions 6, 9 and 11).
/// Groups are never ordered by quantity: letters stay stable when instances are added or removed (Aidan, 2026-11-12).
/// </summary>
public static class HoleTableOrder
{
    /// <summary>Diameters closer than this (centimetres) are equal.</summary>
    public const double DiameterToleranceCm = 0.00001;

    /// <summary>Positions on the sheet closer than this (centimetres) are equal (0.005 mm).</summary>
    public const double PositionToleranceCm = 0.0005;

    /// <summary>
    /// Orders groups. The precision table sorts by drill diameter ascending; the clearance table by
    /// <see cref="HoleTableType"/>, then drill diameter ascending. Ties fall back to an ordinal comparison of the
    /// tie-break string. A null diameter sorts last within its type.
    /// </summary>
    public static IComparer<HoleTableGroupKey> Groups(bool precisionTable) =>
        Comparer<HoleTableGroupKey>.Create((a, b) =>
        {
            if (!precisionTable)
            {
                var byType = a.Type.CompareTo(b.Type);
                if (byType != 0)
                {
                    return byType;
                }
            }

            var byDiameter = CompareDiameter(a.DiameterCm, b.DiameterCm);
            return byDiameter != 0 ? byDiameter : string.CompareOrdinal(a.TieBreak, b.TieBreak);
        });

    /// <summary>Orders instances within a group: Y ascending, then X ascending, on the sheet position, with tolerance.</summary>
    public static IComparer<HoleTableHole> Instances { get; } = Comparer<HoleTableHole>.Create((a, b) =>
    {
        var byY = ComparePosition(a.SheetYCm, b.SheetYCm);
        return byY != 0 ? byY : ComparePosition(a.SheetXCm, b.SheetXCm);
    });

    private static int ComparePosition(double a, double b) =>
        Math.Abs(a - b) <= PositionToleranceCm ? 0 : a.CompareTo(b);

    private static int CompareDiameter(double? a, double? b)
    {
        if (a is not { } x)
        {
            return b is null ? 0 : 1;
        }

        if (b is not { } y)
        {
            return -1;
        }

        return Math.Abs(x - y) <= DiameterToleranceCm ? 0 : x.CompareTo(y);
    }
}

/// <summary>Decimals the hole table shows (Default, spec 04 question 14).</summary>
public static class HoleTableDecimals
{
    /// <summary>Precision table: 2 decimals in millimetres, 3 in inches. Clearance table: 1 and 2.</summary>
    public static int For(bool precisionTable, HoleUnits units) => (precisionTable, units) switch
    {
        (true, HoleUnits.Metric) => 2,
        (true, _) => 3,
        (false, HoleUnits.Metric) => 1,
        _ => 2,
    };
}
