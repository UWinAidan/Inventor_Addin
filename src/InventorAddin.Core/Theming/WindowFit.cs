using InventorAddin.Core.Settings;

namespace InventorAddin.Core.Theming;

/// <summary>A screen's working area (the screen less the taskbar), in WPF device-independent units.</summary>
public readonly record struct ScreenArea(double Left, double Top, double Width, double Height)
{
    public double Right => Left + Width;
    public double Bottom => Top + Height;
}

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

    /// <summary>
    /// Where to put a resizable window whose last placement was <paramref name="saved"/>, given the screens there are
    /// now (task 079). The window goes on the screen it overlaps most, made no bigger than that screen's working area
    /// and no smaller than the minimum size, and moved so it lies wholly inside it. Null when the placement is not
    /// usable or overlaps no screen (a screen that was unplugged), so the window opens at its usual size instead.
    /// </summary>
    public static WindowPlacement? Restore(WindowPlacement? saved, IReadOnlyList<ScreenArea> screens, double minWidth, double minHeight)
    {
        ArgumentNullException.ThrowIfNull(screens);
        if (saved is null || !double.IsFinite(saved.Left) || !double.IsFinite(saved.Top)
            || !double.IsFinite(saved.Width) || !double.IsFinite(saved.Height) || saved.Width <= 0 || saved.Height <= 0)
            return null;

        ScreenArea? best = null;
        double bestOverlap = 0;
        foreach (var screen in screens)
        {
            if (!double.IsFinite(screen.Width) || !double.IsFinite(screen.Height) || screen.Width <= 0 || screen.Height <= 0)
                continue;

            double overlapWidth = Math.Min(saved.Left + saved.Width, screen.Right) - Math.Max(saved.Left, screen.Left);
            double overlapHeight = Math.Min(saved.Top + saved.Height, screen.Bottom) - Math.Max(saved.Top, screen.Top);
            double overlap = overlapWidth > 0 && overlapHeight > 0 ? overlapWidth * overlapHeight : 0;
            if (overlap > bestOverlap)
            {
                bestOverlap = overlap;
                best = screen;
            }
        }

        if (best is not { } area)
            return null;

        double width = Math.Min(Math.Max(saved.Width, Math.Max(minWidth, 0)), area.Width);
        double height = Math.Min(Math.Max(saved.Height, Math.Max(minHeight, 0)), area.Height);
        double left = Math.Min(Math.Max(saved.Left, area.Left), area.Right - width);
        double top = Math.Min(Math.Max(saved.Top, area.Top), area.Bottom - height);
        return new WindowPlacement(left, top, width, height, saved.Maximized);
    }
}
