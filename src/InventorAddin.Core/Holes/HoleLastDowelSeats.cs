using System.Globalization;
using System.Text.Json.Serialization;
using InventorAddin.Core.Settings;

namespace InventorAddin.Core.Holes;

/// <summary>
/// The sizes of a seat (spec 09, "Dowel and precision holes"; task 106): the counterbore diameter and depth and the
/// countersink diameter and included angle, in the units of the hole's row (the angle in degrees). Null when not known.
/// Named for the dowel seat it was made for; since task 126 it holds any hole's seat boxes too. Where the seat boxes
/// start (<see cref="HoleRules.DowelSeatStart"/>, <see cref="HoleRules.SeatRowStart"/>) and what the settings file
/// remembers (<see cref="HoleLastDowelSeats"/>). In the settings file a value that is not known is left out.
/// <para>
/// <see cref="CboreDepthExpression"/> is the expression the counterbore depth was typed as (task 125), kept with
/// <see cref="CboreDepth"/>, the value it came to: it travels with that value and is dropped when there is none.
/// </para>
/// </summary>
public sealed record HoleDowelSeat(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] decimal? CboreDiameter,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] decimal? CboreDepth,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] decimal? CsinkDiameter,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] decimal? CsinkAngleDeg)
{
    /// <summary>No value known.</summary>
    public static HoleDowelSeat Empty { get; } = new(null, null, null, null);

    /// <summary>
    /// The expression the counterbore depth was typed as, such as <c>d0/8</c> (task 125); null for a number. Kept only
    /// beside a <see cref="CboreDepth"/>, which holds the value it came to.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CboreDepthExpression { get; init; }

    /// <summary>True when no value is known.</summary>
    [JsonIgnore]
    public bool IsEmpty => this == Empty;

    /// <summary>
    /// Each value of this seat, or <paramref name="fallback"/>'s where this one has none. The counterbore depth's
    /// expression goes with the depth it is taken from.
    /// </summary>
    public HoleDowelSeat Or(HoleDowelSeat? fallback) => fallback is null
        ? this
        : new HoleDowelSeat(
            CboreDiameter ?? fallback.CboreDiameter,
            CboreDepth ?? fallback.CboreDepth,
            CsinkDiameter ?? fallback.CsinkDiameter,
            CsinkAngleDeg ?? fallback.CsinkAngleDeg)
        {
            CboreDepthExpression = CboreDepth is not null ? CboreDepthExpression : fallback.CboreDepthExpression,
        };

    /// <summary>
    /// The seat with each value a seat can take: lengths greater than zero and an angle between 0 and 180 degrees.
    /// Anything else, as a hand-edited settings file might hold, is dropped. The counterbore depth's expression is
    /// trimmed, and dropped with its depth or when blank.
    /// </summary>
    public HoleDowelSeat Valid() => new(
        CboreDiameter is > 0 ? CboreDiameter : null,
        CboreDepth is > 0 ? CboreDepth : null,
        CsinkDiameter is > 0 ? CsinkDiameter : null,
        CsinkAngleDeg is > 0 and < 180 ? CsinkAngleDeg : null)
    {
        CboreDepthExpression = CboreDepth is > 0 ? HoleExpressions.Clean(CboreDepthExpression) : null,
    };
}

/// <summary>
/// The seat of the last dowel hole kept with OK, for each units and dowel size (spec 09 question 14, Aidan; task 106),
/// and of the last tapped hole, for each units and thread (task 126, Default): a tapped hole whose thread has no
/// Counterbore or Countersink row of its size starts its seat boxes at it (<see cref="ForTapped"/>,
/// <see cref="AddinSettings.LastTappedSeatsMm"/>). A clearance hole's seat always has its row, so none is kept for it.
/// A new dowel seat starts at it, value by value, before the Dowel tab's seat columns and the same-size Counterbore or
/// Countersink row. A counterbore depth typed as an expression is kept as that expression with the value it came to
/// (task 125), so the next seat of the size starts at the expression where the part can read it. Kept in the add-in's settings file (<see cref="AddinSettings.LastDowelSeatsMm"/>,
/// <see cref="AddinSettings.LastDowelSeatsIn"/>), keyed by the dowel's Size as written in the workbook (ignoring case),
/// so it survives a restart. A counterbore kept with OK leaves the countersink values of the size as they were, and the
/// other way round.
/// </summary>
public sealed class HoleLastDowelSeats
{
    private readonly IReadOnlyDictionary<string, HoleDowelSeat> _metric;
    private readonly IReadOnlyDictionary<string, HoleDowelSeat> _imperial;
    private readonly IReadOnlyDictionary<string, HoleDowelSeat> _tappedMetric;
    private readonly IReadOnlyDictionary<string, HoleDowelSeat> _tappedImperial;

    /// <param name="metric">Dowel seats by Metric dowel size.</param>
    /// <param name="imperial">Dowel seats by Imperial dowel size.</param>
    /// <param name="tappedMetric">Tapped seats by Metric thread (task 126).</param>
    /// <param name="tappedImperial">Tapped seats by Imperial thread (task 126).</param>
    public HoleLastDowelSeats(
        IReadOnlyDictionary<string, HoleDowelSeat>? metric,
        IReadOnlyDictionary<string, HoleDowelSeat>? imperial,
        IReadOnlyDictionary<string, HoleDowelSeat>? tappedMetric = null,
        IReadOnlyDictionary<string, HoleDowelSeat>? tappedImperial = null)
    {
        _metric = AddinSettings.DowelSeatMap(metric);
        _imperial = AddinSettings.DowelSeatMap(imperial);
        _tappedMetric = AddinSettings.DowelSeatMap(tappedMetric);
        _tappedImperial = AddinSettings.DowelSeatMap(tappedImperial);
    }

    /// <summary>No seat used yet in either units.</summary>
    public static HoleLastDowelSeats None { get; } = new(null, null);

    /// <summary>The last seats kept in the settings file.</summary>
    public static HoleLastDowelSeats From(AddinSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return new HoleLastDowelSeats(
            settings.LastDowelSeatsMm, settings.LastDowelSeatsIn, settings.LastTappedSeatsMm, settings.LastTappedSeatsIn);
    }

    /// <summary>
    /// The last seat used on a dowel of <paramref name="size"/> in <paramref name="units"/>, with only the values a
    /// seat can take; <see cref="HoleDowelSeat.Empty"/> when there is none.
    /// </summary>
    public HoleDowelSeat For(HoleUnits units, string? size) =>
        Find(units == HoleUnits.Imperial ? _imperial : _metric, size);

    /// <summary>
    /// The last seat used on a tapped hole of <paramref name="thread"/> in <paramref name="units"/> (task 126), as
    /// <see cref="For"/> gives a dowel's.
    /// </summary>
    public HoleDowelSeat ForTapped(HoleUnits units, string? thread) =>
        Find(units == HoleUnits.Imperial ? _tappedImperial : _tappedMetric, thread);

    private static HoleDowelSeat Find(IReadOnlyDictionary<string, HoleDowelSeat> map, string? size)
    {
        var key = (size ?? "").Trim();
        return key.Length > 0 && map.TryGetValue(key, out var seat) ? seat.Valid() : HoleDowelSeat.Empty;
    }

    /// <summary>
    /// The seat a kept hole leaves to remember, with its units and size: a dowel or tapped hole (task 126) with a
    /// counterbore (its diameter and depth, and the expression the depth was typed as; task 125) or a countersink (its
    /// diameter and angle), each value greater than zero. The other seat's values are null. Null for any other hole, or
    /// when the seat has no value to keep.
    /// </summary>
    public static (HoleUnits Units, string Size, HoleDowelSeat Seat)? SeatToRemember(HoleSpec kept)
    {
        ArgumentNullException.ThrowIfNull(kept);
        var spec = kept.Normalised();
        if (spec.Kind is not (HoleKind.Dowel or HoleKind.Tapped) || spec.Seat == HoleSeat.None || spec.Size.Length == 0)
        {
            return null;
        }

        var seat = new HoleDowelSeat(spec.CboreDiameter, spec.CboreDepth, spec.CsinkDiameter, spec.CsinkAngleDeg)
        {
            CboreDepthExpression = spec.CboreDepthExpression,
        }.Valid();
        return seat.IsEmpty ? null : (spec.Units, spec.Size, seat);
    }

    /// <summary>
    /// What <see cref="Remember"/> keeps for <paramref name="kept"/>, for the log, so a manual check can see the seat
    /// the next one of the size starts at (task 125): <c>6 mm dowel (Metric): counterbore 9 mm x d0/8 (= 3 mm)</c>, or
    /// <c>M6 tapped hole (Metric): countersink 7 mm at 90 deg</c> (task 126).
    /// Null when the hole leaves no seat to remember.
    /// </summary>
    public static string? RememberedText(HoleSpec kept)
    {
        ArgumentNullException.ThrowIfNull(kept);
        if (SeatToRemember(kept) is not { } remembered)
        {
            return null;
        }

        var (units, size, seat) = remembered;
        var unit = " " + HoleExpressions.UnitName(units);
        string Number(decimal? value) => value is { } v ? v.ToString("0.######", CultureInfo.InvariantCulture) : "?";
        var parts = new List<string>();
        if (seat.CboreDiameter is not null || seat.CboreDepth is not null)
        {
            var depth = seat.CboreDepthExpression is { } expression
                ? $"{expression} (= {Number(seat.CboreDepth)}{unit})"
                : Number(seat.CboreDepth) + unit;
            parts.Add($"counterbore {Number(seat.CboreDiameter)}{unit} x {depth}");
        }

        if (seat.CsinkDiameter is not null || seat.CsinkAngleDeg is not null)
        {
            parts.Add($"countersink {Number(seat.CsinkDiameter)}{unit} at {Number(seat.CsinkAngleDeg)} deg");
        }

        var what = kept.Kind == HoleKind.Tapped ? "tapped hole" : "dowel";
        return $"{size.Trim()} {what} ({units}): {string.Join(", ", parts)}";
    }

    /// <summary>
    /// The settings with <paramref name="kept"/>'s seat as the last seat of its units and size, merged with the values
    /// kept for that size before (the other seat's values stay), or null when the hole leaves nothing to remember or the
    /// values are already the ones kept (so the caller has nothing to save).
    /// </summary>
    public static AddinSettings? Remember(AddinSettings settings, HoleSpec kept)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (SeatToRemember(kept) is not { } remembered)
        {
            return null;
        }

        var (units, size, seat) = remembered;
        var tapped = kept.Kind == HoleKind.Tapped;
        var current = (tapped, units) switch
        {
            (true, HoleUnits.Imperial) => settings.LastTappedSeatsIn,
            (true, _) => settings.LastTappedSeatsMm,
            (false, HoleUnits.Imperial) => settings.LastDowelSeatsIn,
            _ => settings.LastDowelSeatsMm,
        };
        var map = new Dictionary<string, HoleDowelSeat>(current, StringComparer.OrdinalIgnoreCase);

        // An existing key keeps its spelling; the values of the seat kept replace the old ones.
        var key = map.Keys.FirstOrDefault(k => string.Equals(k, size, StringComparison.OrdinalIgnoreCase)) ?? size;
        map[key] = seat.Or(map.TryGetValue(key, out var old) ? old : null);

        var updated = (tapped, units) switch
        {
            (true, HoleUnits.Imperial) => settings with { LastTappedSeatsIn = map },
            (true, _) => settings with { LastTappedSeatsMm = map },
            (false, HoleUnits.Imperial) => settings with { LastDowelSeatsIn = map },
            _ => settings with { LastDowelSeatsMm = map },
        };
        return updated == settings ? null : updated;
    }

    /// <summary>
    /// Everything a hole kept with OK leaves to remember, in one settings change for one save: its blind depth
    /// (<see cref="HoleLastDepths.Remember"/>) and its dowel or tapped seat (<see cref="Remember"/>). Null when there is
    /// nothing new.
    /// </summary>
    public static AddinSettings? RememberKept(AddinSettings settings, HoleSpec kept)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(kept);

        var withDepth = HoleLastDepths.Remember(settings, kept) ?? settings;
        var withSeat = Remember(withDepth, kept) ?? withDepth;
        return withSeat == settings ? null : withSeat;
    }
}
