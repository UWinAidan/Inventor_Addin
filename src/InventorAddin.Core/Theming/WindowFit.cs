namespace InventorAddin.Core.Theming;

/// <summary>
/// Keeps an add-in window inside the screen's working area (spec 08, "Window layout": Aidan, 2026-10-17). A window is
/// never taller than the working area of the screen it opens on; when its content is taller, the body scrolls and the
/// header and buttons stay visible. Sizes are in WPF device-independent units, converted by the add-in.
/// </summary>
public static class WindowFit
{
    /// <summary>
    /// The tallest the window may be: the working area's height. Null when the height is not a usable number (the
    /// screen could not be read), so the caller leaves the window's own limit alone.
    /// </summary>
    public static double? MaxHeight(double workAreaHeight) =>
        double.IsFinite(workAreaHeight) && workAreaHeight > 0 ? workAreaHeight : null;

    /// <summary>
    /// The top edge that keeps a window of <paramref name="height"/> inside the working area from
    /// <paramref name="workAreaTop"/> to <paramref name="workAreaBottom"/>: unchanged when it already fits, moved up
    /// when its bottom is below the working area, and never above the working area's top, so the title bar stays
    /// reachable. Null when nothing needs to move or a value is not a usable number.
    /// </summary>
    public static double? ClampTop(double top, double height, double workAreaTop, double workAreaBottom)
    {
        if (!double.IsFinite(top) || !double.IsFinite(height) || !double.IsFinite(workAreaTop)
            || !double.IsFinite(workAreaBottom) || height < 0 || workAreaBottom <= workAreaTop)
            return null;

        double clamped = top;
        if (clamped + height > workAreaBottom)
            clamped = workAreaBottom - height;
        if (clamped < workAreaTop)
            clamped = workAreaTop;

        return clamped == top ? null : clamped;
    }
}
