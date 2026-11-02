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
/// <para>
/// Since task 112 it holds a spotface's diameter and depth too (<see cref="SpotfaceDiameter"/>,
/// <see cref="SpotfaceDepth"/>, with <see cref="SpotfaceDepthExpression"/> as the counterbore depth's), as init-only
/// values so a settings file written before keeps reading.
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

    /// <summary>A spotface's diameter (task 112); null when not known.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? SpotfaceDiameter { get; init; }

    /// <summary>A spotface's depth (task 112); 0 is a depth a spotface can take (spike 108). Null when not known.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? SpotfaceDepth { get; init; }

    /// <summary>The expression the spotface depth was typed as; kept only beside a <see cref="SpotfaceDepth"/>.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SpotfaceDepthExpression { get; init; }

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
            SpotfaceDiameter = SpotfaceDiameter ?? fallback.SpotfaceDiameter,
            SpotfaceDepth = SpotfaceDepth ?? fallback.SpotfaceDepth,
            SpotfaceDepthExpression = SpotfaceDepth is not null ? SpotfaceDepthExpression : fallback.SpotfaceDepthExpression,
        };

    /// <summary>
    /// The seat with each value a seat can take: lengths greater than zero (a spotface depth of 0 too, task 112) and an
    /// angle between 0 and 180 degrees. Anything else, as a hand-edited settings file might hold, is dropped. A depth's
    /// expression is trimmed, and dropped with its depth or when blank.
    /// </summary>
    public HoleDowelSeat Valid() => new(
        CboreDiameter is > 0 ? CboreDiameter : null,
        CboreDepth is > 0 ? CboreDepth : null,
        CsinkDiameter is > 0 ? CsinkDiameter : null,
        CsinkAngleDeg is > 0 and < 180 ? CsinkAngleDeg : null)
    {
        CboreDepthExpression = CboreDepth is > 0 ? HoleExpressions.Clean(CboreDepthExpression) : null,
        SpotfaceDiameter = SpotfaceDiameter is > 0 ? SpotfaceDiameter : null,
        SpotfaceDepth = SpotfaceDepth is >= 0 ? SpotfaceDepth : null,
        SpotfaceDepthExpression = SpotfaceDepth is >= 0 ? HoleExpressions.Clean(SpotfaceDepthExpression) : null,
    };

    /// <summary>The seat boxes of <paramref name="spec"/>'s seat, as a seat; the other seats' values are null.</summary>
    public static HoleDowelSeat Of(HoleSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        var s = spec.Normalised();
        return new HoleDowelSeat(s.CboreDiameter, s.CboreDepth, s.CsinkDiameter, s.CsinkAngleDeg)
        {
            CboreDepthExpression = s.CboreDepthExpression,
            SpotfaceDiameter = s.SpotfaceDiameter,
            SpotfaceDepth = s.SpotfaceDepth,
            SpotfaceDepthExpression = s.SpotfaceDepthExpression,
        };
    }
}

/// <summary>
/// The seat of the last dowel hole kept with OK, for each units and dowel size (spec 09 question 14, Aidan; task 106),
/// and of the last tapped hole, for each units and thread (task 126, Default): a tapped hole whose thread has no
/// Counterbore or Countersink row of its size starts its seat boxes at it (<see cref="ForTapped"/>,
/// <see cref="AddinSettings.LastTappedSeatsMm"/>). Since task 112 (spec 09 question 19, Default) also the seat of the
/// last Simple hole, for each units (<see cref="ForSimple"/>), and of the last taper tapped hole, for each units and size
/// (<see cref="ForTaperTapped"/>); each seat's values are kept apart, so these are the last seat per units and seat, and
/// per units, size and seat. A clearance hole's seat always starts from its row (task 137 decides one with no row), so
/// none is kept for it.
/// A new dowel seat starts at it, value by value, before the Dowel tab's seat columns and the same-size Counterbore or
/// Countersink row. A counterbore depth typed as an expression is kept as that expression with the value it came to
/// (task 125), so the next seat of the size starts at the expression where the part can read it. Kept in the add-in's
/// settings file (<see cref="AddinSettings.LastDowelSeatsMm"/>, <see cref="AddinSettings.LastDowelSeatsIn"/> and the
/// like), keyed by the Size as written in the workbook (ignoring case), so it survives a restart. A counterbore kept
/// with OK leaves the countersink and spotface values of the size as they were, and the other way round.
/// </summary>
public sealed class HoleLastDowelSeats
{
    private readonly IReadOnlyDictionary<string, HoleDowelSeat> _metric;
    private readonly IReadOnlyDictionary<string, HoleDowelSeat> _imperial;
    private readonly IReadOnlyDictionary<string, HoleDowelSeat> _tappedMetric;
    private readonly IReadOnlyDictionary<string, HoleDowelSeat> _tappedImperial;
    private readonly IReadOnlyDictionary<string, HoleDowelSeat> _taperMetric;
    private readonly IReadOnlyDictionary<string, HoleDowelSeat> _taperImperial;
    private readonly HoleDowelSeat _simpleMetric;
    private readonly HoleDowelSeat _simpleImperial;

    /// <param name="metric">Dowel seats by Metric dowel size.</param>
    /// <param name="imperial">Dowel seats by Imperial dowel size.</param>
    /// <param name="tappedMetric">Tapped seats by Metric thread (task 126).</param>
    /// <param name="tappedImperial">Tapped seats by Imperial thread (task 126).</param>
    /// <param name="taperTappedMetric">Taper tapped seats by Metric size (task 112).</param>
    /// <param name="taperTappedImperial">Taper tapped seats by Imperial size (task 112).</param>
    /// <param name="simpleMetric">The last Metric Simple hole's seat (task 112).</param>
    /// <param name="simpleImperial">The last Imperial Simple hole's seat (task 112).</param>
    public HoleLastDowelSeats(
        IReadOnlyDictionary<string, HoleDowelSeat>? metric,
        IReadOnlyDictionary<string, HoleDowelSeat>? imperial,
        IReadOnlyDictionary<string, HoleDowelSeat>? tappedMetric = null,
        IReadOnlyDictionary<string, HoleDowelSeat>? tappedImperial = null,
        IReadOnlyDictionary<string, HoleDowelSeat>? taperTappedMetric = null,
        IReadOnlyDictionary<string, HoleDowelSeat>? taperTappedImperial = null,
        HoleDowelSeat? simpleMetric = null,
        HoleDowelSeat? simpleImperial = null)
    {
        _metric = AddinSettings.DowelSeatMap(metric);
        _imperial = AddinSettings.DowelSeatMap(imperial);
        _tappedMetric = AddinSettings.DowelSeatMap(tappedMetric);
        _tappedImperial = AddinSettings.DowelSeatMap(tappedImperial);
        _taperMetric = AddinSettings.DowelSeatMap(taperTappedMetric);
        _taperImperial = AddinSettings.DowelSeatMap(taperTappedImperial);
        _simpleMetric = (simpleMetric ?? HoleDowelSeat.Empty).Valid();
        _simpleImperial = (simpleImperial ?? HoleDowelSeat.Empty).Valid();
    }

    /// <summary>No seat used yet in either units.</summary>
    public static HoleLastDowelSeats None { get; } = new(null, null);

    /// <summary>The last seats kept in the settings file.</summary>
    public static HoleLastDowelSeats From(AddinSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return new HoleLastDowelSeats(
            settings.LastDowelSeatsMm, settings.LastDowelSeatsIn, settings.LastTappedSeatsMm, settings.LastTappedSeatsIn,
            settings.LastTaperTappedSeatsMm, settings.LastTaperTappedSeatsIn, settings.LastSimpleSeatMm,
            settings.LastSimpleSeatIn);
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

    /// <summary>The last seat used on a taper tapped hole of <paramref name="size"/> in <paramref name="units"/> (task 112).</summary>
    public HoleDowelSeat ForTaperTapped(HoleUnits units, string? size) =>
        Find(units == HoleUnits.Imperial ? _taperImperial : _taperMetric, size);

    /// <summary>The last seat used on a Simple hole in <paramref name="units"/> (task 112).</summary>
    public HoleDowelSeat ForSimple(HoleUnits units) => units == HoleUnits.Imperial ? _simpleImperial : _simpleMetric;

    /// <summary>
    /// Where the last values put a hole's seat boxes for <paramref name="kind"/>: a dowel's, a tapped hole's, a taper
    /// tapped hole's or a Simple hole's last seat; <see cref="HoleDowelSeat.Empty"/> for a clearance hole.
    /// </summary>
    public HoleDowelSeat ForKind(HoleUnits units, HoleKind kind, string? size) => kind switch
    {
        HoleKind.Dowel => For(units, size),
        HoleKind.Tapped => ForTapped(units, size),
        HoleKind.TaperTapped => ForTaperTapped(units, size),
        HoleKind.Simple => ForSimple(units),
        _ => HoleDowelSeat.Empty,
    };

    private static HoleDowelSeat Find(IReadOnlyDictionary<string, HoleDowelSeat> map, string? size)
    {
        var key = (size ?? "").Trim();
        return key.Length > 0 && map.TryGetValue(key, out var seat) ? seat.Valid() : HoleDowelSeat.Empty;
    }

    /// <summary>
    /// The seat a kept hole leaves to remember, with its units and size: a dowel, tapped (task 126), taper tapped or
    /// Simple (task 112) hole with a counterbore (its diameter and depth, and the expression the depth was typed as;
    /// task 125), a countersink (its diameter and angle) or a spotface (its diameter and depth), each value one a seat
    /// can take. The other seats' values are null. The size is <c>""</c> for a Simple hole. Null for a clearance hole,
    /// or when the seat has no value to keep.
    /// </summary>
    public static (HoleUnits Units, string Size, HoleDowelSeat Seat)? SeatToRemember(HoleSpec kept)
    {
        ArgumentNullException.ThrowIfNull(kept);
        var spec = kept.Normalised();
        var keyed = spec.Kind is HoleKind.Dowel or HoleKind.Tapped or HoleKind.TaperTapped;
        if (!(keyed || spec.Kind == HoleKind.Simple) || spec.Seat == HoleSeat.None || (keyed && spec.Size.Length == 0))
        {
            return null;
        }

        var seat = HoleDowelSeat.Of(spec).Valid();
        return seat.IsEmpty ? null : (spec.Units, spec.Size, seat);
    }

    /// <summary>
    /// What <see cref="Remember"/> keeps for <paramref name="kept"/>, for the log, so a manual check can see the seat
    /// the next one of the size starts at (task 125): <c>6 mm dowel (Metric): counterbore 9 mm x d0/8 (= 3 mm)</c>,
    /// <c>M6 tapped hole (Metric): countersink 7 mm at 90 deg</c> (task 126), or
    /// <c>Simple hole (Metric): spotface 12 mm x 0.5 mm</c> (task 112).
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
        string Depth(decimal? value, string? expression) => expression is { } text
            ? $"{text} (= {Number(value)}{unit})"
            : Number(value) + unit;
        var parts = new List<string>();
        if (seat.CboreDiameter is not null || seat.CboreDepth is not null)
        {
            parts.Add($"counterbore {Number(seat.CboreDiameter)}{unit} x {Depth(seat.CboreDepth, seat.CboreDepthExpression)}");
        }

        if (seat.CsinkDiameter is not null || seat.CsinkAngleDeg is not null)
        {
            parts.Add($"countersink {Number(seat.CsinkDiameter)}{unit} at {Number(seat.CsinkAngleDeg)} deg");
        }

        if (seat.SpotfaceDiameter is not null || seat.SpotfaceDepth is not null)
        {
            parts.Add(
                $"spotface {Number(seat.SpotfaceDiameter)}{unit} x {Depth(seat.SpotfaceDepth, seat.SpotfaceDepthExpression)}");
        }

        var what = kept.Kind switch
        {
            HoleKind.Tapped => "tapped hole",
            HoleKind.TaperTapped => "taper tapped hole",
            HoleKind.Simple => "Simple hole",
            _ => "dowel",
        };
        var name = size.Trim().Length > 0 ? $"{size.Trim()} {what}" : what;
        return $"{name} ({units}): {string.Join(", ", parts)}";
    }

    /// <summary>
    /// The settings with <paramref name="kept"/>'s seat as the last seat of its kind, units and size, merged with the
    /// values kept for that size before (the other seats' values stay), or null when the hole leaves nothing to remember
    /// or the values are already the ones kept (so the caller has nothing to save).
    /// </summary>
    public static AddinSettings? Remember(AddinSettings settings, HoleSpec kept)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (SeatToRemember(kept) is not { } remembered)
        {
            return null;
        }

        var (units, size, seat) = remembered;
        var imperial = units == HoleUnits.Imperial;
        AddinSettings updated;
        if (kept.Kind == HoleKind.Simple)
        {
            var old = imperial ? settings.LastSimpleSeatIn : settings.LastSimpleSeatMm;
            var merged = seat.Or(old);
            updated = imperial ? settings with { LastSimpleSeatIn = merged } : settings with { LastSimpleSeatMm = merged };
        }
        else
        {
            var current = (kept.Kind, imperial) switch
            {
                (HoleKind.Tapped, true) => settings.LastTappedSeatsIn,
                (HoleKind.Tapped, false) => settings.LastTappedSeatsMm,
                (HoleKind.TaperTapped, true) => settings.LastTaperTappedSeatsIn,
                (HoleKind.TaperTapped, false) => settings.LastTaperTappedSeatsMm,
                (_, true) => settings.LastDowelSeatsIn,
                _ => settings.LastDowelSeatsMm,
            };
            var map = new Dictionary<string, HoleDowelSeat>(current, StringComparer.OrdinalIgnoreCase);

            // An existing key keeps its spelling; the values of the seat kept replace the old ones.
            var key = map.Keys.FirstOrDefault(k => string.Equals(k, size, StringComparison.OrdinalIgnoreCase)) ?? size;
            map[key] = seat.Or(map.TryGetValue(key, out var old) ? old : null);

            updated = (kept.Kind, imperial) switch
            {
                (HoleKind.Tapped, true) => settings with { LastTappedSeatsIn = map },
                (HoleKind.Tapped, false) => settings with { LastTappedSeatsMm = map },
                (HoleKind.TaperTapped, true) => settings with { LastTaperTappedSeatsIn = map },
                (HoleKind.TaperTapped, false) => settings with { LastTaperTappedSeatsMm = map },
                (_, true) => settings with { LastDowelSeatsIn = map },
                _ => settings with { LastDowelSeatsMm = map },
            };
        }

        return updated == settings ? null : updated;
    }

    /// <summary>
    /// Everything a hole kept with OK leaves to remember, in one settings change for one save: its blind depth and a
    /// Simple hole's diameter (<see cref="HoleLastDepths.Remember"/>) and its seat (<see cref="Remember"/>). Null when
    /// there is nothing new.
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
