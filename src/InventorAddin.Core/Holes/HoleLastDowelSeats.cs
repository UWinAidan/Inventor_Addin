using System.Text.Json.Serialization;
using InventorAddin.Core.Settings;

namespace InventorAddin.Core.Holes;

/// <summary>
/// The sizes of a dowel's seat (spec 09, "Dowel and precision holes"; task 106): the counterbore diameter and depth and
/// the countersink diameter and included angle, in the units of the dowel's row (the angle in degrees). Null when not
/// known. Where the seat boxes start (<see cref="HoleRules.DowelSeatStart"/>) and what the settings file remembers
/// (<see cref="HoleLastDowelSeats"/>). In the settings file a value that is not known is left out.
/// </summary>
public sealed record HoleDowelSeat(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] decimal? CboreDiameter,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] decimal? CboreDepth,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] decimal? CsinkDiameter,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] decimal? CsinkAngleDeg)
{
    /// <summary>No value known.</summary>
    public static HoleDowelSeat Empty { get; } = new(null, null, null, null);

    /// <summary>True when no value is known.</summary>
    [JsonIgnore]
    public bool IsEmpty => this == Empty;

    /// <summary>Each value of this seat, or <paramref name="fallback"/>'s where this one has none.</summary>
    public HoleDowelSeat Or(HoleDowelSeat? fallback) => fallback is null
        ? this
        : new HoleDowelSeat(
            CboreDiameter ?? fallback.CboreDiameter,
            CboreDepth ?? fallback.CboreDepth,
            CsinkDiameter ?? fallback.CsinkDiameter,
            CsinkAngleDeg ?? fallback.CsinkAngleDeg);

    /// <summary>
    /// The seat with each value a seat can take: lengths greater than zero and an angle between 0 and 180 degrees.
    /// Anything else, as a hand-edited settings file might hold, is dropped.
    /// </summary>
    public HoleDowelSeat Valid() => new(
        CboreDiameter is > 0 ? CboreDiameter : null,
        CboreDepth is > 0 ? CboreDepth : null,
        CsinkDiameter is > 0 ? CsinkDiameter : null,
        CsinkAngleDeg is > 0 and < 180 ? CsinkAngleDeg : null);
}

/// <summary>
/// The seat of the last dowel hole kept with OK, for each units and dowel size (spec 09 question 14, Default; task 106).
/// A new dowel seat starts at it, value by value, before the Dowel tab's seat columns and the same-size Counterbore or
/// Countersink row. Kept in the add-in's settings file (<see cref="AddinSettings.LastDowelSeatsMm"/>,
/// <see cref="AddinSettings.LastDowelSeatsIn"/>), keyed by the dowel's Size as written in the workbook (ignoring case),
/// so it survives a restart. A counterbore kept with OK leaves the countersink values of the size as they were, and the
/// other way round.
/// </summary>
public sealed class HoleLastDowelSeats
{
    private readonly IReadOnlyDictionary<string, HoleDowelSeat> _metric;
    private readonly IReadOnlyDictionary<string, HoleDowelSeat> _imperial;

    public HoleLastDowelSeats(
        IReadOnlyDictionary<string, HoleDowelSeat>? metric, IReadOnlyDictionary<string, HoleDowelSeat>? imperial)
    {
        _metric = AddinSettings.DowelSeatMap(metric);
        _imperial = AddinSettings.DowelSeatMap(imperial);
    }

    /// <summary>No seat used yet in either units.</summary>
    public static HoleLastDowelSeats None { get; } = new(null, null);

    /// <summary>The last seats kept in the settings file.</summary>
    public static HoleLastDowelSeats From(AddinSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return new HoleLastDowelSeats(settings.LastDowelSeatsMm, settings.LastDowelSeatsIn);
    }

    /// <summary>
    /// The last seat used on a dowel of <paramref name="size"/> in <paramref name="units"/>, with only the values a
    /// seat can take; <see cref="HoleDowelSeat.Empty"/> when there is none.
    /// </summary>
    public HoleDowelSeat For(HoleUnits units, string? size)
    {
        var key = (size ?? "").Trim();
        var map = units == HoleUnits.Imperial ? _imperial : _metric;
        return key.Length > 0 && map.TryGetValue(key, out var seat) ? seat.Valid() : HoleDowelSeat.Empty;
    }

    /// <summary>
    /// The seat a kept hole leaves to remember, with its units and size: a dowel with a counterbore (its diameter and
    /// depth) or a countersink (its diameter and angle), each value greater than zero. The other seat's values are
    /// null. Null for any other hole, or when the seat has no value to keep.
    /// </summary>
    public static (HoleUnits Units, string Size, HoleDowelSeat Seat)? SeatToRemember(HoleSpec kept)
    {
        ArgumentNullException.ThrowIfNull(kept);
        var spec = kept.Normalised();
        if (spec.Kind != HoleKind.Dowel || spec.Seat == HoleSeat.None || spec.Size.Length == 0)
        {
            return null;
        }

        var seat = new HoleDowelSeat(spec.CboreDiameter, spec.CboreDepth, spec.CsinkDiameter, spec.CsinkAngleDeg).Valid();
        return seat.IsEmpty ? null : (spec.Units, spec.Size, seat);
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
        var current = units == HoleUnits.Imperial ? settings.LastDowelSeatsIn : settings.LastDowelSeatsMm;
        var map = new Dictionary<string, HoleDowelSeat>(current, StringComparer.OrdinalIgnoreCase);

        // An existing key keeps its spelling; the values of the seat kept replace the old ones.
        var key = map.Keys.FirstOrDefault(k => string.Equals(k, size, StringComparison.OrdinalIgnoreCase)) ?? size;
        map[key] = seat.Or(map.TryGetValue(key, out var old) ? old : null);

        var updated = units == HoleUnits.Imperial
            ? settings with { LastDowelSeatsIn = map }
            : settings with { LastDowelSeatsMm = map };
        return updated == settings ? null : updated;
    }

    /// <summary>
    /// Everything a hole kept with OK leaves to remember, in one settings change for one save: its blind depth
    /// (<see cref="HoleLastDepths.Remember"/>) and its dowel seat (<see cref="Remember"/>). Null when there is nothing new.
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
