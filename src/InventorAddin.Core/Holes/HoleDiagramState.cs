using InventorAddin.Core.Settings;

namespace InventorAddin.Core.Holes;

/// <summary>
/// Whether the AWB Hole window's diagram is open (task 104; mockup choice 12), kept in the add-in's settings file
/// (<see cref="AddinSettings.HoleDiagramExpanded"/>) so it is the same in every window and after a restart. It starts
/// open.
/// </summary>
public static class HoleDiagramState
{
    /// <summary>Whether the diagram opens expanded.</summary>
    public static bool IsExpanded(AddinSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return settings.HoleDiagramExpanded;
    }

    /// <summary>
    /// The settings with the diagram <paramref name="expanded"/> or collapsed, or null when that is already what they
    /// hold (so the caller has nothing to save).
    /// </summary>
    public static AddinSettings? Remember(AddinSettings settings, bool expanded)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return settings.HoleDiagramExpanded == expanded ? null : settings with { HoleDiagramExpanded = expanded };
    }
}
