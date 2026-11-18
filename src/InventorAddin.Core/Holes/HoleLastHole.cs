using System.Text.Json.Serialization;
using InventorAddin.Core.Settings;

namespace InventorAddin.Core.Holes;

/// <summary>
/// The choices of the last hole kept with OK in one units (spec 09, "A new hole starts on the last hole"; Aidan,
/// 2026-11-18; task 173): kind (so Type and Standard), size, seat, fastener type, fit, tolerance (the Toleranced tick,
/// a Precision Tol row's name, or Custom with its code or its limits), termination and drill point. Depths, seat sizes
/// and a Simple hole's diameter have their own last values (<see cref="HoleLastDepths"/>,
/// <see cref="HoleLastDowelSeats"/>) and are not here. Kept as text, so the settings file reads without enum
/// converters; a value that does not read back is ignored. In the settings file a value that is not set is left out.
/// </summary>
public sealed record HoleLastChoices
{
    /// <summary>The kind, as <see cref="HoleKind"/>'s name; gives the window's Type and Standard.</summary>
    public string Kind { get; init; } = "";

    /// <summary>The Size as written in the workbook; null for a Simple hole.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Size { get; init; }

    /// <summary>The seat, as <see cref="HoleSeat"/>'s name.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Seat { get; init; }

    /// <summary>The Fastener type, for a seat that takes one.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Fastener { get; init; }

    /// <summary>The Fit as the window lists it: a dowel's precision fit or a clearance hole's fit.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Fit { get; init; }

    /// <summary>The Toleranced checkbox of a clearance or Simple hole.</summary>
    public bool Toleranced { get; init; }

    /// <summary>A Precision Tol row's name; null for Custom or when there is no tolerance.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Tolerance { get; init; }

    /// <summary>True when the Custom tolerance was chosen.</summary>
    public bool CustomTolerance { get; init; }

    /// <summary>A Custom tolerance's ISO 286 code, in the case typed.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CustomCode { get; init; }

    /// <summary>A Custom tolerance's upper limit, in the units' length.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? CustomUpper { get; init; }

    /// <summary>A Custom tolerance's lower limit, in the units' length.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? CustomLower { get; init; }

    /// <summary>The termination, as <see cref="HoleTermination"/>'s name: Distance or ThroughAll.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Termination { get; init; }

    /// <summary>The drill point's name, for a blind hole.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DrillPoint { get; init; }

    /// <summary>The kind, or null when the text is not one (a hand-edited settings file).</summary>
    [JsonIgnore]
    public HoleKind? KindValue => Enum.TryParse<HoleKind>(Kind, out var kind) && Enum.IsDefined(kind) ? kind : null;

    /// <summary>The seat, or null when none is kept or the text is not one.</summary>
    [JsonIgnore]
    public HoleSeat? SeatValue => Enum.TryParse<HoleSeat>(Seat, out var seat) && Enum.IsDefined(seat) ? seat : null;

    /// <summary>The termination, or null when none is kept or the text is not one.</summary>
    [JsonIgnore]
    public HoleTermination? TerminationValue =>
        Enum.TryParse<HoleTermination>(Termination, out var t) && Enum.IsDefined(t) ? t : null;

    /// <summary>The choices of <paramref name="kept"/>; what does not apply to its kind, seat and termination is left out.</summary>
    public static HoleLastChoices Of(HoleSpec kept)
    {
        ArgumentNullException.ThrowIfNull(kept);
        var spec = kept.Normalised();
        static string? Text(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        return new HoleLastChoices
        {
            Kind = spec.Kind.ToString(),
            Size = Text(spec.Size),
            Seat = spec.Seat.ToString(),
            Fastener = Text(spec.Fastener),
            Fit = spec.Kind switch
            {
                HoleKind.Dowel => spec.PrecisionFit.ToString(),
                HoleKind.Clearance => spec.ClearanceFit.ToString(),
                _ => null,
            },
            Toleranced = spec.Toleranced,
            Tolerance = Text(spec.Tolerance),
            CustomTolerance = spec.CustomTolerance,
            CustomCode = Text(spec.CustomCode),
            CustomUpper = spec.CustomUpper,
            CustomLower = spec.CustomLower,
            // A hole ending at a face is not one the window can start a new hole on.
            Termination = spec.Termination == HoleTermination.ToFace ? null : spec.Termination.ToString(),
            DrillPoint = Text(spec.DrillPoint),
        };
    }
}

/// <summary>
/// The last hole kept with OK, for each units (spec 09, "A new hole starts on the last hole"; Aidan, 2026-11-18; task
/// 173): a new AWB Hole starts on it, as the blind depth and seats start at their last values. Kept in the add-in's
/// settings file (<see cref="AddinSettings.LastHoleMm"/>, <see cref="AddinSettings.LastHoleIn"/>), so it survives a
/// restart. Only OK changes it (<see cref="Remember"/>); the window never writes.
/// </summary>
public sealed class HoleLastHole
{
    private readonly HoleLastChoices? _metric;
    private readonly HoleLastChoices? _imperial;

    /// <param name="metric">The last Metric hole.</param>
    /// <param name="imperial">The last Imperial hole.</param>
    public HoleLastHole(HoleLastChoices? metric, HoleLastChoices? imperial)
    {
        _metric = metric;
        _imperial = imperial;
    }

    /// <summary>No hole kept yet in either units.</summary>
    public static HoleLastHole None { get; } = new(null, null);

    /// <summary>The last holes kept in the settings file.</summary>
    public static HoleLastHole From(AddinSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return new HoleLastHole(settings.LastHoleMm, settings.LastHoleIn);
    }

    /// <summary>The last hole kept in <paramref name="units"/>; null when there is none.</summary>
    public HoleLastChoices? For(HoleUnits units) => units == HoleUnits.Imperial ? _imperial : _metric;

    /// <summary>
    /// The settings with <paramref name="kept"/> as the last hole of its units, or null when it is already the one
    /// kept (so the caller has nothing to save).
    /// </summary>
    public static AddinSettings? Remember(AddinSettings settings, HoleSpec kept)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(kept);

        var choices = HoleLastChoices.Of(kept);
        var imperial = kept.Units == HoleUnits.Imperial;
        var updated = imperial ? settings with { LastHoleIn = choices } : settings with { LastHoleMm = choices };
        return updated == settings ? null : updated;
    }

    /// <summary>
    /// Everything a hole kept with OK leaves to remember, in one settings change for one save: its blind depth and
    /// seat (<see cref="HoleLastDowelSeats.RememberKept"/>) and the hole itself (<see cref="Remember"/>). Null when
    /// there is nothing new.
    /// </summary>
    public static AddinSettings? RememberKept(AddinSettings settings, HoleSpec kept)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(kept);

        var withValues = HoleLastDowelSeats.RememberKept(settings, kept) ?? settings;
        var withHole = Remember(withValues, kept) ?? withValues;
        return withHole == settings ? null : withHole;
    }
}
