namespace InventorAddin.Core.Drawing;

/// <summary>An axis-aligned rectangle on the sheet, in millimetres, Y up.</summary>
public readonly record struct SheetRect(double MinXMm, double MinYMm, double MaxXMm, double MaxYMm);

/// <summary>An item to check: a view or annotation with its name and bounding rectangle.</summary>
public sealed record BoundsItem(string Name, SheetRect Rect);

/// <summary>The edges of the border an item crosses.</summary>
[Flags]
public enum BorderEdges
{
    None = 0,
    Left = 1,
    Right = 2,
    Bottom = 4,
    Top = 8,
}

/// <summary>How an item fails the check. Outside wins over partly outside; over the title block is flagged alongside either.</summary>
public enum BoundsProblem
{
    /// <summary>Wholly outside the border.</summary>
    Outside,
    /// <summary>Crosses one or more edges of the border.</summary>
    PartlyOutside,
    /// <summary>Inside the border but over the title block.</summary>
    OverTitleBlock,
}

/// <summary>One reported item. <see cref="Edges"/> is the edges crossed (empty when wholly outside or only over the title block).</summary>
public sealed record BoundsFinding(
    string Name,
    BoundsProblem Problem,
    BorderEdges Edges,
    bool OverTitleBlock,
    string Message);

/// <summary>
/// Reports which views and annotations fall outside the border or over the title block (spec 05, out-of-bounds check, task 194).
/// Anything within <see cref="DefaultToleranceMm"/> (Default) of the border, or overlapping the title block by no more than that, is not reported.
/// </summary>
public static class OutOfBoundsCheck
{
    /// <summary>Default tolerance in millimetres, so an item touching the border is not reported.</summary>
    public const double DefaultToleranceMm = 0.5;

    public static IReadOnlyList<BoundsFinding> Check(
        SheetRect borderInner,
        SheetRect? titleBlock,
        IEnumerable<BoundsItem> items,
        double toleranceMm = DefaultToleranceMm)
    {
        var results = new List<BoundsFinding>();
        foreach (var item in items)
        {
            var r = item.Rect;
            var edges = BorderEdges.None;
            if (r.MinXMm < borderInner.MinXMm - toleranceMm) edges |= BorderEdges.Left;
            if (r.MaxXMm > borderInner.MaxXMm + toleranceMm) edges |= BorderEdges.Right;
            if (r.MinYMm < borderInner.MinYMm - toleranceMm) edges |= BorderEdges.Bottom;
            if (r.MaxYMm > borderInner.MaxYMm + toleranceMm) edges |= BorderEdges.Top;

            bool wholly = edges != BorderEdges.None
                && (r.MaxXMm <= borderInner.MinXMm || r.MinXMm >= borderInner.MaxXMm
                    || r.MaxYMm <= borderInner.MinYMm || r.MinYMm >= borderInner.MaxYMm);

            bool overTitle = false;
            if (titleBlock is { } t)
            {
                double ox = Math.Min(r.MaxXMm, t.MaxXMm) - Math.Max(r.MinXMm, t.MinXMm);
                double oy = Math.Min(r.MaxYMm, t.MaxYMm) - Math.Max(r.MinYMm, t.MinYMm);
                // A zero-thickness item (a line or leader) has no overlap to measure across its thickness; it counts when it lies within the block.
                bool flatX = r.MaxXMm <= r.MinXMm;
                bool flatY = r.MaxYMm <= r.MinYMm;
                overTitle = (ox > toleranceMm || (flatX && ox >= 0)) && (oy > toleranceMm || (flatY && oy >= 0));
            }

            if (wholly)
            {
                results.Add(new BoundsFinding(item.Name, BoundsProblem.Outside, BorderEdges.None, false,
                    $"{item.Name} is outside the border."));
            }
            else if (edges != BorderEdges.None)
            {
                string msg = $"{item.Name} goes past the border's {EdgeList(edges)} "
                    + (IsMulti(edges) ? "edges" : "edge")
                    + (overTitle ? " and is over the title block." : ".");
                results.Add(new BoundsFinding(item.Name, BoundsProblem.PartlyOutside, edges, overTitle, msg));
            }
            else if (overTitle)
            {
                results.Add(new BoundsFinding(item.Name, BoundsProblem.OverTitleBlock, BorderEdges.None, true,
                    $"{item.Name} is over the title block."));
            }
        }
        return results;
    }

    private static bool IsMulti(BorderEdges e) => ((int)e & ((int)e - 1)) != 0;

    private static string EdgeList(BorderEdges e)
    {
        var names = new List<string>();
        if (e.HasFlag(BorderEdges.Left)) names.Add("left");
        if (e.HasFlag(BorderEdges.Right)) names.Add("right");
        if (e.HasFlag(BorderEdges.Bottom)) names.Add("bottom");
        if (e.HasFlag(BorderEdges.Top)) names.Add("top");
        return string.Join(" and ", names);
    }
}
