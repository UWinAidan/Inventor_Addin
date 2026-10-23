using InventorAddin.Core.Settings;

namespace InventorAddin.Core.Holes;

/// <summary>
/// The depth of the last blind clearance or dowel hole kept with OK, for each units (spec 09, "Blind holes"; task 097):
/// <see cref="Metric"/> in mm, <see cref="Imperial"/> in inches, null when none has been made in those units. A new
/// blind hole starts at it, else at the workbook's "Blind depth multiplier" x the nominal diameter. Kept in the add-in's
/// settings file (<see cref="AddinSettings.LastBlindDepthMm"/>, <see cref="AddinSettings.LastBlindDepthIn"/>), so it
/// survives a restart.
/// </summary>
public sealed record HoleLastDepths(decimal? Metric, decimal? Imperial)
{
    /// <summary>No depth used yet in either units.</summary>
    public static HoleLastDepths None { get; } = new(null, null);

    /// <summary>The last depth used in <paramref name="units"/>, in its length; null when there is none.</summary>
    public decimal? For(HoleUnits units)
    {
        var depth = units == HoleUnits.Imperial ? Imperial : Metric;
        return depth > 0 ? depth : null;
    }

    /// <summary>The last depths kept in the settings file.</summary>
    public static HoleLastDepths From(AddinSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return new HoleLastDepths(settings.LastBlindDepthMm, settings.LastBlindDepthIn);
    }

    /// <summary>
    /// The depth a kept hole leaves to remember, with its units: a blind clearance or dowel hole with a depth greater
    /// than zero. Null for a tapped hole, a hole that is not blind, or one with no depth.
    /// </summary>
    public static (HoleUnits Units, decimal Depth)? DepthToRemember(HoleSpec kept)
    {
        ArgumentNullException.ThrowIfNull(kept);
        var spec = kept.Normalised();
        return spec.Kind != HoleKind.Tapped && spec.IsBlind && spec.Depth is { } depth && depth > 0
            ? (spec.Units, depth)
            : null;
    }

    /// <summary>
    /// The settings with <paramref name="kept"/>'s depth as the last depth of its units, or null when the hole leaves
    /// nothing to remember or the depth is already the one kept (so the caller has nothing to save).
    /// </summary>
    public static AddinSettings? Remember(AddinSettings settings, HoleSpec kept)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (DepthToRemember(kept) is not { } remembered)
        {
            return null;
        }

        var (units, depth) = remembered;

        var updated = units == HoleUnits.Imperial
            ? settings with { LastBlindDepthIn = depth }
            : settings with { LastBlindDepthMm = depth };
        return updated == settings ? null : updated;
    }
}
