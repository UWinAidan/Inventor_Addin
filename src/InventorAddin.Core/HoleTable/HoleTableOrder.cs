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

    /// <summary>
    /// Orders instances within a group: Y ascending, then X ascending, on the sheet position (task 155's rule). Y values
    /// within <see cref="PositionToleranceCm"/> of a neighbour in a chain share a row band; the band is ordered by X,
    /// then <see cref="HoleTableHole.Id"/>. Pairwise comparison with a tolerance is not transitive, so this clusters
    /// instead: the result never depends on the input order.
    /// </summary>
    public static IReadOnlyList<HoleTableHole> SortInstances(IEnumerable<HoleTableHole> holes)
    {
        ArgumentNullException.ThrowIfNull(holes);

        var byY = holes.OrderBy(h => h.SheetYCm).ThenBy(h => h.Id, StringComparer.Ordinal).ToList();
        var result = new List<HoleTableHole>(byY.Count);
        var band = new List<HoleTableHole>();
        foreach (var hole in byY)
        {
            if (band.Count > 0 && hole.SheetYCm - band[^1].SheetYCm > PositionToleranceCm)
            {
                Flush();
            }

            band.Add(hole);
        }

        Flush();
        return result;

        void Flush()
        {
            result.AddRange(band.OrderBy(h => h.SheetXCm).ThenBy(h => h.Id, StringComparer.Ordinal));
            band.Clear();
        }
    }

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
