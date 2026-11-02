using InventorAddin.Core.Settings;

namespace InventorAddin.Core.Holes;

/// <summary>
/// The depth of the last blind clearance, dowel or Simple hole kept with OK, for each units (spec 09, "Blind holes";
/// task 097): <see cref="Metric"/> in mm, <see cref="Imperial"/> in inches, null when none has been made in those units.
/// A new blind hole starts at it, else at the workbook's "Blind depth multiplier" x the nominal diameter. Since task 112
/// also the diameter of the last Simple hole kept with OK, for each units (<see cref="SimpleDiameterFor"/>; spec 09,
/// "Simple holes", Default), as a number. Kept in the add-in's settings file (<see cref="AddinSettings.LastBlindDepthMm"/>,
/// <see cref="AddinSettings.LastBlindDepthIn"/>, <see cref="AddinSettings.LastSimpleDiameterMm"/>,
/// <see cref="AddinSettings.LastSimpleDiameterIn"/>), so it survives a restart.
/// </summary>
public sealed record HoleLastDepths(decimal? Metric, decimal? Imperial)
{
    /// <summary>No depth used yet in either units.</summary>
    public static HoleLastDepths None { get; } = new(null, null);

    /// <summary>The last Simple hole's diameter in Metric, in mm (task 112); null when there is none.</summary>
    public decimal? SimpleDiameterMetric { get; init; }

    /// <summary>The last Simple hole's diameter in Imperial, in inches (task 112); null when there is none.</summary>
    public decimal? SimpleDiameterImperial { get; init; }

    /// <summary>The last depth used in <paramref name="units"/>, in its length; null when there is none.</summary>
    public decimal? For(HoleUnits units)
    {
        var depth = units == HoleUnits.Imperial ? Imperial : Metric;
        return depth > 0 ? depth : null;
    }

    /// <summary>The last Simple hole's diameter in <paramref name="units"/>, in its length; null when there is none.</summary>
    public decimal? SimpleDiameterFor(HoleUnits units)
    {
        var diameter = units == HoleUnits.Imperial ? SimpleDiameterImperial : SimpleDiameterMetric;
        return diameter > 0 ? diameter : null;
    }

    /// <summary>The last depths kept in the settings file.</summary>
    public static HoleLastDepths From(AddinSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return new HoleLastDepths(settings.LastBlindDepthMm, settings.LastBlindDepthIn)
        {
            SimpleDiameterMetric = settings.LastSimpleDiameterMm,
            SimpleDiameterImperial = settings.LastSimpleDiameterIn,
        };
    }

    /// <summary>
    /// The depth a kept hole leaves to remember, with its units: a blind clearance, dowel or Simple hole with a depth
    /// greater than zero. Null for a tapped or taper tapped hole, a hole that is not blind, or one with no depth.
    /// </summary>
    public static (HoleUnits Units, decimal Depth)? DepthToRemember(HoleSpec kept)
    {
        ArgumentNullException.ThrowIfNull(kept);
        var spec = kept.Normalised();
        return !HoleStandards.IsThreaded(spec.Kind) && spec.IsBlind && spec.Depth is { } depth && depth > 0
            ? (spec.Units, depth)
            : null;
    }

    /// <summary>
    /// The diameter a kept Simple hole leaves to remember, with its units (task 112): its diameter, greater than zero, as
    /// a number. Null for every other kind.
    /// </summary>
    public static (HoleUnits Units, decimal Diameter)? DiameterToRemember(HoleSpec kept)
    {
        ArgumentNullException.ThrowIfNull(kept);
        var spec = kept.Normalised();
        return spec.Kind == HoleKind.Simple && spec.Diameter is { } diameter && diameter > 0
            ? (spec.Units, diameter)
            : null;
    }

    /// <summary>
    /// The settings with <paramref name="kept"/>'s depth as the last depth of its units, and a Simple hole's diameter as
    /// the last Simple diameter of its units (task 112), or null when the hole leaves nothing to remember or the values
    /// are already the ones kept (so the caller has nothing to save).
    /// </summary>
    public static AddinSettings? Remember(AddinSettings settings, HoleSpec kept)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(kept);

        var updated = settings;
        if (DepthToRemember(kept) is { } depth)
        {
            updated = depth.Units == HoleUnits.Imperial
                ? updated with { LastBlindDepthIn = depth.Depth }
                : updated with { LastBlindDepthMm = depth.Depth };
        }

        if (DiameterToRemember(kept) is { } diameter)
        {
            updated = diameter.Units == HoleUnits.Imperial
                ? updated with { LastSimpleDiameterIn = diameter.Diameter }
                : updated with { LastSimpleDiameterMm = diameter.Diameter };
        }

        return updated == settings ? null : updated;
    }
}
