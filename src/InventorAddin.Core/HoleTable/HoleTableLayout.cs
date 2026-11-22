namespace InventorAddin.Core.HoleTable;

/// <summary>The text styles a hole table uses.</summary>
public enum HoleTableTextRole
{
    Title,
    Header,
    Data,
}

/// <summary>Measures formatted text. The add-in implements it by fitting a note in Inventor; tests use a fake.</summary>
public interface IHoleTableTextMeasure
{
    /// <summary>The width in cm of the formatted text in the role's text style.</summary>
    double WidthCm(HoleTableTextRole role, string formattedText);
}

/// <summary>Read by the add-in from Inventor's hole table style.</summary>
/// <param name="TextHeightCm">The data text height.</param>
/// <param name="RowHeightCm">The one-line row height.</param>
public sealed record HoleTableMetrics(double TextHeightCm, double RowHeightCm);

/// <summary>The weight of a grid line: the outside border, or every other line.</summary>
public enum HoleTableLineWeight
{
    Inside,
    Outside,
}

public sealed record HoleTableLine(double X1Cm, double Y1Cm, double X2Cm, double Y2Cm, HoleTableLineWeight Weight);

/// <summary>A text box centred on a point, in the definition's coordinates.</summary>
public sealed record HoleTableText(HoleTableTextRole Role, string FormattedText, double CenterXCm, double CenterYCm, bool HasStack);

/// <summary>What the add-in draws inside one table's sketched symbol definition. The origin is the bottom-right corner (x at most 0, y at least 0).</summary>
public sealed record HoleTableDrawing(
    HoleTableKind Table,
    double WidthCm,
    double HeightCm,
    IReadOnlyList<double> ColumnWidthsCm,
    IReadOnlyList<HoleTableLine> Lines,
    IReadOnlyList<HoleTableText> Texts,
    int Part = 1);

/// <summary>Lays out hole tables as lines and text (task 161), following spike 160, round 6b (<c>Layout</c> and <c>Draw-Table</c>).</summary>
public static class HoleTableLayout
{
    /// <summary>The column headings, left to right.</summary>
    public static readonly IReadOnlyList<string> Columns = ["HOLE", "XDIM", "YDIM", "DESCRIPTION", "NOTES"];

    private const int Description = 3;

    /// <summary>The table title, for example <c>Precision Table: VIEW1</c> (spec 04 "Target output").</summary>
    public static string Title(HoleTableKind table, string viewName)
    {
        var name = table == HoleTableKind.Precision ? "Precision Table" : "Tap and Clearance Table";
        return string.IsNullOrWhiteSpace(viewName) ? name : name + ": " + viewName;
    }

    /// <summary>
    /// One drawing per non-empty table, precision first. Both tables share their column widths. With
    /// <paramref name="clearanceMaxHeightCm"/> a clearance table taller than that is cut into parts (task 220), each with
    /// the title and heading, numbered from 1 in <see cref="HoleTableDrawing.Part"/>.
    /// </summary>
    public static IReadOnlyList<HoleTableDrawing> For(
        HoleTablePlan plan,
        IHoleTableTextMeasure measure,
        HoleTableMetrics metrics,
        string viewName,
        double? clearanceMaxHeightCm = null)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(measure);
        ArgumentNullException.ThrowIfNull(metrics);

        var h = metrics.TextHeightCm;
        var rowH = metrics.RowHeightCm;
        var tables = new List<(HoleTableKind Kind, List<Prepared> Groups)>();
        foreach (var (kind, groups) in new[] { (HoleTableKind.Precision, plan.Precision), (HoleTableKind.Clearance, plan.Clearance) })
        {
            if (groups.Count > 0)
            {
                tables.Add((kind, groups.Select(g => Prepare(g, h, rowH)).ToList()));
            }
        }

        if (tables.Count == 0)
        {
            return [];
        }

        // Column widths: the widest content of either table, header included, plus a text height each side, rounded to
        // 0.001 with the spike's banker's rounding (spike 160, round 6b, Layout).
        var margin = 1.0 * h;
        var widths = new double[5];
        for (var c = 0; c < 5; c++)
        {
            var w = measure.WidthCm(HoleTableTextRole.Header, Columns[c]);
            foreach (var (_, groups) in tables)
            {
                foreach (var g in groups)
                {
                    switch (c)
                    {
                        case < 3:
                            foreach (var row in g.Group.Rows)
                            {
                                w = Math.Max(w, measure.WidthCm(HoleTableTextRole.Data, HoleTableFormattedText.Escape(Cell(row, c))));
                            }

                            break;
                        case Description:
                            foreach (var t in g.Formatted.Texts)
                            {
                                w = Math.Max(w, measure.WidthCm(HoleTableTextRole.Data, t));
                            }

                            break;
                        default:
                            w = Math.Max(w, measure.WidthCm(HoleTableTextRole.Data, HoleTableFormattedText.Escape(g.Group.NotesText)));
                            break;
                    }
                }
            }

            widths[c] = Math.Round(w + 2 * margin, 3);
        }

        // A wider title takes its room from DESCRIPTION.
        var titleW = tables.Max(t => measure.WidthCm(HoleTableTextRole.Title, HoleTableFormattedText.Escape(Title(t.Kind, viewName)))) + 2 * margin;
        var sum = widths.Sum();
        if (titleW > sum)
        {
            widths[Description] += titleW - sum;
        }

        var result = new List<HoleTableDrawing>();
        foreach (var t in tables)
        {
            var title = HoleTableFormattedText.Escape(Title(t.Kind, viewName));
            if (t.Kind != HoleTableKind.Clearance || clearanceMaxHeightCm is not { } max)
            {
                result.Add(Draw(t.Kind, t.Groups, widths, title, h, rowH));
                continue;
            }

            var part = 1;
            foreach (var chunk in SplitPrepared(t.Groups, h, rowH, max))
            {
                result.Add(Draw(t.Kind, chunk, widths, title, h, rowH) with { Part = part++ });
            }
        }

        return result;
    }

    /// <summary>
    /// Cuts a table's groups into parts no taller than <paramref name="maxHeightCm"/>, each part with its own title and
    /// heading rows (task 220). Groups stay whole where they fit; a group that does not fit the room left is cut between
    /// its rows (both pieces keep the description and the note) or starts the next part. A row that alone is taller than a
    /// part still gets a part to itself, so nothing is dropped. Returns one list of groups per part.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<HoleTableGroup>> SplitRows(
        IReadOnlyList<HoleTableGroup> groups, HoleTableMetrics metrics, double maxHeightCm)
    {
        ArgumentNullException.ThrowIfNull(groups);
        ArgumentNullException.ThrowIfNull(metrics);

        var prepared = groups.Select(g => Prepare(g, metrics.TextHeightCm, metrics.RowHeightCm)).ToList();
        return SplitPrepared(prepared, metrics.TextHeightCm, metrics.RowHeightCm, maxHeightCm)
            .Select(chunk => (IReadOnlyList<HoleTableGroup>)chunk.Select(p => p.Group).ToList())
            .ToList();
    }

    private static List<List<Prepared>> SplitPrepared(List<Prepared> groups, double h, double rowH, double maxHeight)
    {
        var parts = new List<List<Prepared>>();
        var current = new List<Prepared>();
        var room = maxHeight - 2 * rowH;
        var used = 0.0;
        foreach (var g in groups)
        {
            var rows = g.Group.Rows.ToList();
            while (rows.Count > 0)
            {
                var whole = Prepare(g.Group with { Rows = rows }, h, rowH);
                if (used + whole.GroupH <= room + 1e-9)
                {
                    current.Add(whole);
                    used += whole.GroupH;
                    break;
                }

                // How many of the rows fit the room left?
                var take = 0;
                for (var k = rows.Count - 1; k >= 1; k--)
                {
                    if (used + Prepare(g.Group with { Rows = rows.Take(k).ToList() }, h, rowH).GroupH <= room + 1e-9)
                    {
                        take = k;
                        break;
                    }
                }

                if (take == 0)
                {
                    if (current.Count > 0)
                    {
                        parts.Add(current);
                        current = [];
                        used = 0;
                        continue;
                    }

                    take = 1; // one row alone is taller than a part: it still goes in
                }

                var piece = Prepare(g.Group with { Rows = rows.Take(take).ToList() }, h, rowH);
                current.Add(piece);
                used += piece.GroupH;
                rows = rows.Skip(take).ToList();
                if (rows.Count > 0)
                {
                    parts.Add(current);
                    current = [];
                    used = 0;
                }
            }
        }

        if (current.Count > 0)
        {
            parts.Add(current);
        }

        return parts;
    }

    private sealed record Prepared(HoleTableGroup Group, HoleTableFormattedDescription Formatted, double GroupH, double RowH);

    private static Prepared Prepare(HoleTableGroup group, double h, double rowH)
    {
        var formatted = HoleTableFormattedText.For(group.Description, h);
        var n = group.Rows.Count;
        // Group height: rows of the style height, or one row plus a line pitch per extra description line, whichever is
        // more (spike 160, round 6b, Layout).
        var groupH = Math.Max(n * rowH, rowH + (formatted.LineCount - 1) * HoleTableFormattedText.LinePitchFactor * h);
        return new Prepared(group, formatted, groupH, groupH / n);
    }

    private static string Cell(HoleTableRow row, int column) => column switch
    {
        0 => row.Tag,
        1 => row.XText,
        _ => row.YText,
    };

    private static HoleTableDrawing Draw(HoleTableKind kind, List<Prepared> groups, double[] widths, string title, double h, double rowH)
    {
        var total = widths.Sum();
        var x0 = -total;
        var height = 2 * rowH + groups.Sum(g => g.GroupH);
        var xs = new double[6];
        xs[0] = x0;
        for (var i = 0; i < 5; i++)
        {
            xs[i + 1] = xs[i] + widths[i];
        }

        xs[5] = 0;

        var lines = new List<HoleTableLine>();
        var texts = new List<HoleTableText>();
        void Line(double x1, double y1, double x2, double y2, HoleTableLineWeight weight) => lines.Add(new(x1, y1, x2, y2, weight));
        void Text(HoleTableTextRole role, string text, double cx, double cy, bool stack = false) => texts.Add(new(role, text, cx, cy, stack));

        // Outside border, title and header rules, column rules below the title (spike 160, round 6b, Draw-Table).
        Line(x0, 0, 0, 0, HoleTableLineWeight.Outside);
        Line(x0, height, 0, height, HoleTableLineWeight.Outside);
        Line(x0, 0, x0, height, HoleTableLineWeight.Outside);
        Line(0, 0, 0, height, HoleTableLineWeight.Outside);
        Line(x0, height - rowH, 0, height - rowH, HoleTableLineWeight.Inside);
        Line(x0, height - 2 * rowH, 0, height - 2 * rowH, HoleTableLineWeight.Inside);
        for (var c = 1; c < 5; c++)
        {
            Line(xs[c], 0, xs[c], height - rowH, HoleTableLineWeight.Inside);
        }

        Text(HoleTableTextRole.Title, title, x0 / 2, height - rowH / 2);
        for (var c = 0; c < 5; c++)
        {
            Text(HoleTableTextRole.Header, Columns[c], (xs[c] + xs[c + 1]) / 2, height - 1.5 * rowH);
        }

        var y = height - 2 * rowH;
        for (var gi = 0; gi < groups.Count; gi++)
        {
            var g = groups[gi];
            var top = y;
            for (var i = 0; i < g.Group.Rows.Count; i++)
            {
                var cy = y - g.RowH / 2;
                for (var c = 0; c < 3; c++)
                {
                    Text(HoleTableTextRole.Data, HoleTableFormattedText.Escape(Cell(g.Group.Rows[i], c)), (xs[c] + xs[c + 1]) / 2, cy);
                }

                y -= g.RowH;

                // Row rules inside a group stop at DESCRIPTION: DESCRIPTION and NOTES are merged per group.
                if (i < g.Group.Rows.Count - 1)
                {
                    Line(x0, y, xs[Description], y, HoleTableLineWeight.Inside);
                }
            }

            // A rule between groups runs across the full width.
            if (gi < groups.Count - 1)
            {
                Line(x0, y, 0, y, HoleTableLineWeight.Inside);
            }

            var mid = (top + y) / 2;
            var descX = (xs[Description] + xs[Description + 1]) / 2;
            var pitch = HoleTableFormattedText.LinePitchFactor * h;
            if (g.Formatted.HasStack)
            {
                // One block centred in the merged cell: the stack takes 2 pitch slots, each later line 1 (task 161 review).
                var k = g.Formatted.LineCount;
                Text(HoleTableTextRole.Data, g.Formatted.Texts[0], descX, mid + ((k - 1) / 2.0 - 0.5) * pitch, true);
                for (var j = 0; j < g.Formatted.Texts.Count - 1; j++)
                {
                    Text(HoleTableTextRole.Data, g.Formatted.Texts[j + 1], descX, mid + ((k - 1) / 2.0 - (j + 2)) * pitch);
                }
            }
            else
            {
                // One text per line at 1.75 x the text height, centred as a block: a multi-line text with AIGDT
                // overrides spaces its lines wider (spike 160, round 6b).
                var count = g.Formatted.Texts.Count;
                for (var i = 0; i < count; i++)
                {
                    Text(HoleTableTextRole.Data, g.Formatted.Texts[i], descX, mid + ((count - 1) / 2.0 - i) * pitch);
                }
            }

            Text(HoleTableTextRole.Data, HoleTableFormattedText.Escape(g.Group.NotesText), (xs[4] + xs[5]) / 2, mid);
        }

        return new HoleTableDrawing(kind, total, height, widths, lines, texts);
    }
}

/// <summary>Where the two tables go on Align (spec 04, question 4).</summary>
/// <param name="Clearance">The clearance table's bottom-right corner; null when it has none or none fits.</param>
/// <param name="Precision">The precision table's bottom-right corner; null when it has none or none fits.</param>
/// <param name="Warning">Plain words when a table could not go inside the border (task 220); null otherwise.</param>
/// <param name="ClearanceParts">Every part of the clearance table, first part first; empty when none fits.</param>
public sealed record HoleTableAlignment(SheetPoint? Clearance, SheetPoint? Precision, string? Warning = null, IReadOnlyList<SheetPoint>? ClearanceParts = null);

/// <summary>A table's drawn size, in cm.</summary>
/// <param name="WidthCm">0 when the width could not be read; such a table never gets a neighbour placed beside it.</param>
public readonly record struct HoleTableSize(double WidthCm, double HeightCm)
{
    public bool WidthKnown => WidthCm > 0;
}

/// <summary>A box on the sheet, in cm.</summary>
public readonly record struct SheetBox(double MinXCm, double MinYCm, double MaxXCm, double MaxYCm);

public static class HoleTableAlign
{
    /// <summary>
    /// The corner the tables stack on: the title block's top-right (max) corner; without a title block, the border's
    /// bottom-right corner; with neither, null (task 164).
    /// </summary>
    public static SheetPoint? Anchor(SheetBox? titleBlock, SheetBox? border) =>
        titleBlock is { } t ? new SheetPoint(t.MaxXCm, t.MaxYCm)
        : border is { } b ? new SheetPoint(b.MaxXCm, b.MinYCm)
        : null;

    /// <summary>
    /// The placement points (each table's bottom-right corner): the clearance table on the title block's top-right
    /// corner, the precision table straight above it. A table that does not exist gets no point and takes no room, so
    /// a lone table goes on the corner (spike 160, round 6b, <c>Build-Tables</c>).
    /// </summary>
    /// <param name="clearanceHeightCm">The clearance table's height; null when there is none.</param>
    /// <param name="precisionHeightCm">The precision table's height; null when there is none.</param>
    public static HoleTableAlignment Positions(double titleBlockMaxXCm, double titleBlockMaxYCm, double? clearanceHeightCm, double? precisionHeightCm) =>
        new(
            clearanceHeightCm is null ? null : new SheetPoint(titleBlockMaxXCm, titleBlockMaxYCm),
            precisionHeightCm is null ? null : new SheetPoint(titleBlockMaxXCm, titleBlockMaxYCm + (clearanceHeightCm ?? 0)));

    /// <summary>The gap between the stack's column and the second column (task 220, Default).</summary>
    public const double ColumnGapCm = 0.5;

    /// <summary>Slack allowed when a table touches the border.</summary>
    public const double BorderToleranceCm = 0.001;

    /// <summary>
    /// True when a table whose bottom-right corner is <paramref name="at"/> lies inside the border (task 220).
    /// Without a border there is nothing to stay inside.
    /// </summary>
    public static bool Inside(SheetBox? border, SheetPoint at, HoleTableSize size) =>
        border is not { } b
        || (at.XCm <= b.MaxXCm + BorderToleranceCm
            && at.XCm - size.WidthCm >= b.MinXCm - BorderToleranceCm
            && at.YCm >= b.MinYCm - BorderToleranceCm
            && at.YCm + size.HeightCm <= b.MaxYCm + BorderToleranceCm);

    /// <summary>
    /// <see cref="Positions"/> that also keeps every table inside the border (task 220). The clearance table's parts
    /// (one part when it is not split) stand side by side from the anchor to the left, each <see cref="ColumnGapCm"/> from
    /// the last. The precision table goes above the first part while that fits, else beside the last part. A table that
    /// does not fit gets no point and the result carries a warning suggesting a bigger sheet. A width of 0 (unknown) never
    /// has anything placed beside it.
    /// </summary>
    /// <param name="moving">The caller moves existing tables (Align) rather than drawing them; only changes the warning's wording.</param>
    public static HoleTableAlignment Fit(
        SheetPoint anchor, SheetBox? border, IReadOnlyList<HoleTableSize> clearanceParts, HoleTableSize? precision, bool moving = false)
    {
        var misfits = new List<HoleTableKind>();
        var partPoints = new List<SheetPoint>();
        var x = anchor.XCm;
        var clearancePlaced = clearanceParts.Count > 0;
        for (var i = 0; i < clearanceParts.Count && clearancePlaced; i++)
        {
            if (i > 0)
            {
                if (!clearanceParts[i - 1].WidthKnown)
                {
                    clearancePlaced = false;
                    break;
                }

                x -= clearanceParts[i - 1].WidthCm + ColumnGapCm;
            }

            var at = new SheetPoint(x, anchor.YCm);
            if (!Inside(border, at, clearanceParts[i]))
            {
                clearancePlaced = false;
                break;
            }

            partPoints.Add(at);
        }

        if (clearanceParts.Count > 0 && !clearancePlaced)
        {
            partPoints.Clear();
            misfits.Add(HoleTableKind.Clearance);
        }

        SheetPoint? precisionAt = null;
        if (precision is { } p)
        {
            var offset = clearancePlaced ? clearanceParts[0].HeightCm : 0;
            var above = new SheetPoint(anchor.XCm, anchor.YCm + offset);
            if (Inside(border, above, p))
            {
                precisionAt = above;
            }
            else if (clearancePlaced && clearanceParts[^1].WidthKnown)
            {
                var beside = new SheetPoint(partPoints[^1].XCm - clearanceParts[^1].WidthCm - ColumnGapCm, anchor.YCm);
                if (Inside(border, beside, p))
                {
                    precisionAt = beside;
                }
            }

            if (precisionAt is null)
            {
                misfits.Add(HoleTableKind.Precision);
            }
        }

        return new HoleTableAlignment(
            partPoints.Count > 0 ? partPoints[0] : null,
            precisionAt,
            misfits.Count == 0 ? null : HoleTableMessages.TablesDoNotFit(misfits, moving),
            partPoints);
    }

    /// <summary>One whole clearance table; see the list overload.</summary>
    public static HoleTableAlignment Fit(
        SheetPoint anchor, SheetBox? border, HoleTableSize? clearance, HoleTableSize? precision, bool moving = false) =>
        Fit(anchor, border, clearance is { } c ? new[] { c } : Array.Empty<HoleTableSize>(), precision, moving);

    /// <summary>
    /// The width from the first part's right edge to the last part's left edge: every part but the last with the gap
    /// after it, then the last part (task 220). 0 when a width is unknown or there are no parts.
    /// </summary>
    public static double BlockWidth(IReadOnlyList<HoleTableSize> parts)
    {
        if (parts.Count == 0 || parts.Any(p => !p.WidthKnown))
        {
            return 0;
        }

        return parts.Take(parts.Count - 1).Sum(p => p.WidthCm + ColumnGapCm) + parts[^1].WidthCm;
    }

    /// <summary>
    /// Where a table that was redrawn in place ends up (task 220): where it is when that is inside the border, else
    /// <paramref name="fitted"/> (which is null when no spot fits).
    /// </summary>
    public static SheetPoint? KeepInside(SheetBox? border, SheetPoint current, HoleTableSize size, SheetPoint? fitted) =>
        Inside(border, current, size) ? current : fitted;

    /// <summary>How close (cm) the precision table must be to the stack point to count as stacked.</summary>
    public const double StackToleranceCm = 0.01;

    /// <summary>
    /// Where the precision table goes after a redraw that did not align (Update, task 178). A table redrawn in place
    /// grows upward from its bottom-right corner, so a precision table that sat on the clearance table moves with the
    /// clearance table's top edge. A precision table the user moved is left alone (null), and so is one that need not move.
    /// A precision table that did not exist (null before) goes on top of the clearance table where it now is, or on the
    /// anchor when there is none.
    /// </summary>
    /// <param name="anchor">The lone-table spot (task 164).</param>
    /// <param name="clearanceBefore">The clearance table before the redraw; null when it did not exist.</param>
    /// <param name="precisionBefore">The precision table's position before; null when it did not exist.</param>
    /// <param name="clearanceAfter">The clearance table after the redraw; null when it was deleted or does not exist.</param>
    public static SheetPoint? PrecisionAfterRedraw(
        SheetPoint anchor, HoleTablePlacement? clearanceBefore, SheetPoint? precisionBefore, HoleTablePlacement? clearanceAfter)
    {
        // A precision table beside the clearance table (task 220) moves with its left edge.
        var wasBeside = precisionBefore is { } pb && clearanceBefore is { } cb && Beside(cb) is { } bs && Near(pb, bs);
        SheetPoint target;
        if (clearanceAfter is { } after)
        {
            target = wasBeside && Beside(after) is { } follow
                ? follow
                : new SheetPoint(after.Position.XCm, after.Position.YCm + after.HeightCm);
        }
        else
        {
            // The clearance table is gone: a stacked precision table drops to where it was.
            target = precisionBefore is not null && clearanceBefore is { } gone ? gone.Position : anchor;
        }

        if (precisionBefore is not { } old)
        {
            return target;
        }

        var stackPoint = clearanceBefore is { } before
            ? new SheetPoint(before.Position.XCm, before.Position.YCm + before.HeightCm)
            : anchor;
        if (!wasBeside && !Near(old, stackPoint))
        {
            return null;
        }

        return Near(old, target) ? null : target;
    }

    /// <summary>The spot beside a clearance table (after its last part); null when its block width is unknown.</summary>
    private static SheetPoint? Beside(HoleTablePlacement clearance) =>
        clearance.BlockWidthCm > 0
            ? new SheetPoint(clearance.Position.XCm - clearance.BlockWidthCm - ColumnGapCm, clearance.Position.YCm)
            : null;

    private static bool Near(SheetPoint a, SheetPoint b) =>
        Math.Abs(a.XCm - b.XCm) <= StackToleranceCm && Math.Abs(a.YCm - b.YCm) <= StackToleranceCm;
}

/// <summary>A placed table: its bottom-right corner and its height, in cm.</summary>
/// <param name="BlockWidthCm">From the first part's right edge to the last part's left edge (all parts and the gaps); 0 when unknown.</param>
public readonly record struct HoleTablePlacement(SheetPoint Position, double HeightCm, double BlockWidthCm = 0);
