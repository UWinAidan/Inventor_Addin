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
    IReadOnlyList<HoleTableText> Texts);

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

    /// <summary>One drawing per non-empty table, precision first. Both tables share their column widths.</summary>
    public static IReadOnlyList<HoleTableDrawing> For(
        HoleTablePlan plan,
        IHoleTableTextMeasure measure,
        HoleTableMetrics metrics,
        string viewName)
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

        return tables.Select(t => Draw(t.Kind, t.Groups, widths, HoleTableFormattedText.Escape(Title(t.Kind, viewName)), h, rowH)).ToList();
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
public sealed record HoleTableAlignment(SheetPoint? Clearance, SheetPoint? Precision);

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
}
