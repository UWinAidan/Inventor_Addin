using InventorAddin.Core.HoleTable;
using InventorAddin.Core.Holes;

namespace InventorAddin.Core.Tests.HoleTable;

public class HoleTableLayoutTests
{
    private const double H = 0.3;
    private const double RowH = 0.6;
    private const double Pitch = 1.75 * H;
    private const double Tol = 1e-9;

    private static readonly HoleTableMetrics Metrics = new(H, RowH);

    /// <summary>0.2 cm per character for data and headers, 0.3 for titles.</summary>
    private sealed class FakeMeasure : IHoleTableTextMeasure
    {
        public double WidthCm(HoleTableTextRole role, string formattedText) =>
            formattedText.Length * (role == HoleTableTextRole.Title ? 0.3 : 0.2);
    }

    private static HoleTableHole Plain(string id, double dia, double x, double y, bool precision = false, HoleTolerance? tol = null) => new(
        id, 1, "F", HoleUnits.Metric, HoleTableType.Plain, precision, dia, HoleTermination.ThroughAll, null, false, null,
        null, null, null, null, null, null, false, tol, x, y);

    private static HoleTableHole Tapped(string id, double drill, string thread, double x, double y) => new(
        id, 1, "T", HoleUnits.Metric, HoleTableType.Tapped, false, drill, HoleTermination.ThroughAll, null, false, null,
        null, null, null, thread, "Metric", null, false, null, x, y);

    /// <summary>Two H7 holes; three plain; one M6 tapped; two M8 tapped.</summary>
    private static List<HoleTableHole> Plate()
    {
        var tol = new HoleTolerance("H7", "H7", 0.001, 0);
        return
        [
            Plain("P#1", 0.6, 1, 1, true, tol),
            Plain("P#2", 0.6, 2, 1, true, tol),
            Plain("B#1", 0.5, 1, 5),
            Plain("B#2", 0.5, 2, 5),
            Plain("B#3", 0.5, 3, 5),
            Tapped("T#1", 0.5, "M6x1", 1, 8),
            Tapped("U#1", 0.68, "M8x1.25", 1, 9),
            Tapped("U#2", 0.68, "M8x1.25", 2, 9),
        ];
    }

    private static HoleTablePlan Plan(IEnumerable<HoleTableHole> holes) =>
        HoleTablePlan.Build(holes, null, new SheetPoint(0, 0), 1, HoleUnits.Metric);

    private static IReadOnlyList<HoleTableDrawing> Layout(IEnumerable<HoleTableHole> holes, string view = "V1") =>
        HoleTableLayout.For(Plan(holes), new FakeMeasure(), Metrics, view);

    private static HoleTableDrawing Of(IReadOnlyList<HoleTableDrawing> d, HoleTableKind kind) => d.Single(x => x.Table == kind);

    private static double Round3(double v) => Math.Round(v, 3);

    // ---- formatted text

    private static string Fmt(HoleTableHole h) =>
        string.Join("|", HoleTableFormattedText.For(HoleTableDescriptions.For(h), H).Texts);

    private const string Dia = "<StyleOverride Font='AIGDT' FontSize='0.3000'>n</StyleOverride>";
    private const string Depth = "<StyleOverride Font='AIGDT' FontSize='0.3000'>x</StyleOverride> ";
    private const string Cbore = "<StyleOverride Font='AIGDT' FontSize='0.3000'>v</StyleOverride> ";
    private const string Csink = "<StyleOverride Font='AIGDT' FontSize='0.3000'>w</StyleOverride> ";
    private const string Spot = "<StyleOverride Font='AIGDT' FontSize='0.3000'>Â</StyleOverride> ";

    [Fact]
    public void Formatted_plain_through() => Assert.Equal(Dia + "5.0 THRU", Fmt(Plain("a", 0.5, 0, 0)));

    [Fact]
    public void Formatted_blind_has_the_depth_symbol()
    {
        var h = Plain("a", 0.5, 0, 0) with { Termination = HoleTermination.Distance, DepthCm = 1.2 };
        Assert.Equal(Dia + "5.0 " + Depth + "12.0", Fmt(h));
    }

    [Fact]
    public void Formatted_counterbore()
    {
        var h = Plain("a", 0.5, 0, 0) with { Type = HoleTableType.Counterbore, SeatDiameterCm = 1.1, SeatDepthCm = 0.64 };
        Assert.Equal(Dia + "5.0 THRU|" + Cbore + Dia + "11.0 " + Depth + "6.4", Fmt(h));
    }

    [Fact]
    public void Formatted_countersink()
    {
        var h = Plain("a", 0.5, 0, 0) with { Type = HoleTableType.Countersink, SeatDiameterCm = 1.24, SeatAngleDeg = 90 };
        Assert.Equal(Dia + "5.0 THRU|" + Csink + Dia + "12.4 X 90°", Fmt(h));
    }

    [Fact]
    public void Formatted_spotface_has_one_space_after_the_symbol()
    {
        var h = Plain("a", 0.5, 0, 0) with { Type = HoleTableType.Spotface, SeatDiameterCm = 1.4, SeatDepthCm = 0.1 };
        Assert.Equal(Dia + "5.0 THRU|" + Spot + Dia + "14.0 " + Depth + "1.0", Fmt(h));
    }

    [Fact]
    public void Formatted_tapped()
    {
        var h = Tapped("a", 0.5, "M6x1", 0, 0) with { ThreadClass = "6H", ThreadDepthCm = 1.0 };
        Assert.Equal(Dia + "5.0 THRU|M6x1 - 6H " + Depth + "10.0", Fmt(h));
    }

    [Fact]
    public void Formatted_taper_tapped()
    {
        var h = Tapped("a", 0.5, "Rc 1/8", 0, 0) with { Type = HoleTableType.TaperTapped, TaperEngagementCm = 0.65 };
        Assert.Equal(Dia + "5.0 THRU|Rc 1/8 " + Depth + "6.5", Fmt(h));
    }

    [Fact]
    public void Formatted_toleranced_is_one_text_with_a_stack()
    {
        var h = Plain("a", 0.6, 0, 0, true, new HoleTolerance("H7", "H7", 0.0012, 0));
        var f = HoleTableFormattedText.For(HoleTableDescriptions.For(h), H);
        Assert.True(f.HasStack);
        Assert.Equal(2, f.LineCount);
        Assert.Equal([Dia + "6.00 H7 <Stack FractionalTextScale='1.0'>+0.012^+0.000</Stack> THRU"], f.Texts);
    }

    [Fact]
    public void Formatted_toleranced_blind_puts_the_termination_after_the_stack()
    {
        var h = Plain("a", 0.6, 0, 0, true, new HoleTolerance("H7", "H7", 0.0012, 0)) with { Termination = HoleTermination.Distance, DepthCm = 1 };
        var f = HoleTableFormattedText.For(HoleTableDescriptions.For(h), H);
        Assert.Equal([Dia + "6.00 H7 <Stack FractionalTextScale='1.0'>+0.012^+0.000</Stack> " + Depth + "10.00"], f.Texts);
    }

    [Fact]
    public void Formatted_text_height_is_invariant_and_four_decimals()
    {
        var prior = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
            Assert.Equal("<StyleOverride Font='AIGDT' FontSize='0.3048'>n</StyleOverride>", HoleTableFormattedText.Symbol(HoleDescriptionPartKind.Diameter, 0.3048));
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = prior;
        }
    }

    [Fact]
    public void Plain_text_is_escaped() => Assert.Equal("A &amp; B &lt;C&gt; &apos;D&apos;", HoleTableFormattedText.Escape("A & B <C> 'D'"));

    // ---- layout

    [Fact]
    public void Plate_gives_two_drawings_precision_first()
    {
        var d = Layout(Plate());
        Assert.Equal([HoleTableKind.Precision, HoleTableKind.Clearance], d.Select(x => x.Table).ToArray());
    }

    [Fact]
    public void An_empty_table_gives_no_drawing()
    {
        var d = Layout(Plate().Where(h => !h.IsPrecision));
        Assert.Equal([HoleTableKind.Clearance], d.Select(x => x.Table).ToArray());
        Assert.Empty(Layout([]));
    }

    [Fact]
    public void Heights_follow_the_group_rule()
    {
        var d = Layout(Plate());
        // Precision: a stacked group of 2 rows is max(2 x 0.6, 0.6 + 1 x pitch).
        Assert.Equal(2 * RowH + Math.Max(2 * RowH, RowH + Pitch), Of(d, HoleTableKind.Precision).HeightCm, Tol);
        // Clearance: 3 one-line rows; a lone two-line hole; a two-line group of 2 rows.
        var expected = 2 * RowH
            + 3 * RowH
            + Math.Max(RowH, RowH + Pitch)
            + Math.Max(2 * RowH, RowH + Pitch);
        Assert.Equal(expected, Of(d, HoleTableKind.Clearance).HeightCm, Tol);
        Assert.Equal(RowH + Pitch, Math.Max(RowH, RowH + Pitch), Tol);
    }

    [Fact]
    public void Column_widths_are_shared_and_rounded()
    {
        var d = Layout(Plate());
        var p = Of(d, HoleTableKind.Precision);
        var c = Of(d, HoleTableKind.Clearance);
        Assert.Equal(p.ColumnWidthsCm, c.ColumnWidthsCm);
        // HOLE: header 4 chars at 0.2, plus a text height each side. NOTES: header 5 chars.
        Assert.Equal(Round3(0.8 + 2 * H), p.ColumnWidthsCm[0], Tol);
        Assert.Equal(Round3(1.0 + 2 * H), p.ColumnWidthsCm[4], Tol);
        Assert.All(p.ColumnWidthsCm, w => Assert.Equal(Math.Round(w, 3), w, Tol));
        Assert.Equal(p.ColumnWidthsCm.Sum(), p.WidthCm, Tol);
        // XDIM: widest value is the precision table's "20.00" (5 chars) or the header.
        Assert.Equal(Round3(1.0 + 2 * H), p.ColumnWidthsCm[1], Tol);
    }

    [Fact]
    public void Description_width_is_the_widest_text_of_either_table()
    {
        var d = Layout(Plate());
        var widest = Plan(Plate()).AllGroups
            .SelectMany(g => HoleTableFormattedText.For(g.Description, H).Texts)
            .Max(t => t.Length * 0.2);
        Assert.Equal(Round3(widest + 2 * H), d[0].ColumnWidthsCm[3], Tol);
    }

    [Fact]
    public void A_wide_clearance_description_widens_the_precision_table()
    {
        var alone = Layout(Plate().Where(h => h.IsPrecision));
        var wide = Plate().Append(Tapped("W#1", 0.5, "M6x1 " + new string('X', 300), 9, 9)).ToList();
        var both = Layout(wide);
        Assert.True(Of(both, HoleTableKind.Precision).ColumnWidthsCm[3] > Of(alone, HoleTableKind.Precision).ColumnWidthsCm[3]);
    }

    [Fact]
    public void A_long_title_widens_description()
    {
        var plain = Layout(Plate());
        var long_ = Layout(Plate(), new string('W', 400));
        Assert.True(long_[0].ColumnWidthsCm[3] > plain[0].ColumnWidthsCm[3]);
        Assert.Equal(long_[0].ColumnWidthsCm.Take(3).Concat(long_[0].ColumnWidthsCm.Skip(4)), plain[0].ColumnWidthsCm.Take(3).Concat(plain[0].ColumnWidthsCm.Skip(4)));
        var longest = Math.Max(
            HoleTableLayout.Title(HoleTableKind.Precision, new string('W', 400)).Length,
            HoleTableLayout.Title(HoleTableKind.Clearance, new string('W', 400)).Length) * 0.3;
        Assert.Equal(longest + 2 * H, long_[0].WidthCm, 1e-6);
    }

    [Fact]
    public void Origin_is_the_bottom_right_corner()
    {
        foreach (var t in Layout(Plate()))
        {
            Assert.All(t.Lines, l =>
            {
                Assert.InRange(l.X1Cm, -t.WidthCm - Tol, Tol);
                Assert.InRange(l.X2Cm, -t.WidthCm - Tol, Tol);
                Assert.InRange(l.Y1Cm, -Tol, t.HeightCm + Tol);
                Assert.InRange(l.Y2Cm, -Tol, t.HeightCm + Tol);
            });
        }
    }

    [Fact]
    public void Clearance_line_and_text_counts()
    {
        var c = Of(Layout(Plate()), HoleTableKind.Clearance);
        // 4 border + title and header rules + 4 column rules, 3 row rules in groups, 2 rules between groups.
        Assert.Equal(4 + 2 + 4 + 3 + 2, c.Lines.Count);
        Assert.Equal(4, c.Lines.Count(l => l.Weight == HoleTableLineWeight.Outside));
        // title, 5 headers, 6 rows x 3, descriptions 1 + 2 + 2, 3 notes.
        Assert.Equal(1 + 5 + 18 + 5 + 3, c.Texts.Count);
        Assert.Equal(1, c.Texts.Count(t => t.Role == HoleTableTextRole.Title));
    }

    [Fact]
    public void Precision_line_and_text_counts()
    {
        var p = Of(Layout(Plate()), HoleTableKind.Precision);
        Assert.Equal(4 + 2 + 4 + 1, p.Lines.Count);
        Assert.Equal(1 + 5 + 6 + 1 + 1, p.Texts.Count);
        Assert.Equal(1, p.Texts.Count(t => t.HasStack));
    }

    [Fact]
    public void Text_centres()
    {
        var c = Of(Layout(Plate()), HoleTableKind.Clearance);
        var w = c.ColumnWidthsCm;
        var x0 = -c.WidthCm;
        var title = c.Texts.Single(t => t.Role == HoleTableTextRole.Title);
        Assert.Equal(x0 / 2, title.CenterXCm, Tol);
        Assert.Equal(c.HeightCm - RowH / 2, title.CenterYCm, Tol);

        var tag = c.Texts.First(t => t.FormattedText == "B1");
        Assert.Equal(x0 + w[0] / 2, tag.CenterXCm, Tol);
        Assert.Equal(c.HeightCm - 2 * RowH - RowH / 2, tag.CenterYCm, Tol);

        // Lone two-line hole (group C): two texts a pitch apart, centred on the group's middle.
        var top = c.HeightCm - 2 * RowH - 3 * RowH;
        var groupH = RowH + Pitch;
        var mid = top - groupH / 2;
        var descX = -w[4] - w[3] / 2;
        var first = c.Texts.Single(t => t.FormattedText.EndsWith("5.0 THRU") && t.CenterYCm < top && t.CenterYCm > top - groupH);
        var second = c.Texts.Single(t => t.FormattedText.StartsWith("M6x1"));
        Assert.Equal(descX, first.CenterXCm, Tol);
        Assert.Equal(mid + Pitch / 2, first.CenterYCm, Tol);
        Assert.Equal(mid - Pitch / 2, second.CenterYCm, Tol);
        var note = c.Texts.First(t => t.FormattedText == "-" && Math.Abs(t.CenterYCm - mid) < Tol);
        Assert.Equal(-w[4] / 2, note.CenterXCm, Tol);
    }

    [Fact]
    public void A_stacked_description_is_one_text_at_the_group_centre()
    {
        var p = Of(Layout(Plate()), HoleTableKind.Precision);
        var stack = p.Texts.Single(t => t.HasStack);
        var groupH = Math.Max(2 * RowH, RowH + Pitch);
        Assert.Equal(p.HeightCm - 2 * RowH - groupH / 2, stack.CenterYCm, Tol);
    }

    [Fact]
    public void Row_rules_stop_at_description_and_group_rules_cross_the_full_width()
    {
        foreach (var t in Layout(Plate()))
        {
            var descLeft = -t.ColumnWidthsCm[3] - t.ColumnWidthsCm[4];
            var headerY = t.HeightCm - 2 * RowH;
            var horizontals = t.Lines
                .Where(l => Math.Abs(l.Y1Cm - l.Y2Cm) < Tol && l.Y1Cm > Tol && l.Y1Cm < headerY - Tol)
                .ToList();
            var groups = Plan(Plate()).AllGroups.Count(g => g.Table == t.Table);
            var full = horizontals.Where(l => Math.Abs(l.X1Cm + t.WidthCm) < Tol && Math.Abs(l.X2Cm) < Tol).ToList();
            var inside = horizontals.Except(full).ToList();
            Assert.Equal(groups - 1, full.Count);
            Assert.All(inside, l =>
            {
                Assert.Equal(-t.WidthCm, Math.Min(l.X1Cm, l.X2Cm), Tol);
                Assert.Equal(descLeft, Math.Max(l.X1Cm, l.X2Cm), Tol);
            });
            Assert.All(horizontals, l => Assert.Equal(HoleTableLineWeight.Inside, l.Weight));
        }
    }

    [Fact]
    public void Column_rules_run_from_the_bottom_to_the_title_rule()
    {
        var c = Of(Layout(Plate()), HoleTableKind.Clearance);
        var columns = c.Lines.Where(l => Math.Abs(l.X1Cm - l.X2Cm) < Tol && l.X1Cm < -Tol && l.X1Cm > -c.WidthCm + Tol).ToList();
        Assert.Equal(4, columns.Count);
        Assert.All(columns, l =>
        {
            Assert.Equal(0, Math.Min(l.Y1Cm, l.Y2Cm), Tol);
            Assert.Equal(c.HeightCm - RowH, Math.Max(l.Y1Cm, l.Y2Cm), Tol);
        });
    }

    [Fact]
    public void Notes_show_the_group_note_or_a_dash()
    {
        var edits = HoleTableEdits.None.WithNote(HoleTableKind.Clearance, HoleTableDescriptions.For(Plain("B#1", 0.5, 1, 5)).Key, "DRILL FIRST");
        var plan = HoleTablePlan.Build(Plate(), edits, new SheetPoint(0, 0), 1, HoleUnits.Metric);
        var c = Of(HoleTableLayout.For(plan, new FakeMeasure(), Metrics, "V1"), HoleTableKind.Clearance);
        Assert.Single(c.Texts, t => t.FormattedText == "DRILL FIRST");
        Assert.Equal(2, c.Texts.Count(t => t.FormattedText == "-"));
        Assert.True(c.ColumnWidthsCm[4] >= Round3("DRILL FIRST".Length * 0.2 + 2 * H));
    }

    [Fact]
    public void Note_and_view_name_are_escaped_where_drawn_and_measured()
    {
        var edits = HoleTableEdits.None.WithNote(HoleTableKind.Clearance, HoleTableDescriptions.For(Plain("B#1", 0.5, 1, 5)).Key, "A&B <1>");
        var plan = HoleTablePlan.Build(Plate(), edits, new SheetPoint(0, 0), 1, HoleUnits.Metric);
        var c = Of(HoleTableLayout.For(plan, new FakeMeasure(), Metrics, "O'NEIL"), HoleTableKind.Clearance);
        Assert.Single(c.Texts, t => t.FormattedText == "A&amp;B &lt;1&gt;");
        Assert.DoesNotContain(c.Texts, t => t.FormattedText.Contains("A&B"));
        Assert.Equal("Tap and Clearance Table: O&apos;NEIL", c.Texts.Single(t => t.Role == HoleTableTextRole.Title).FormattedText);
        Assert.True(c.ColumnWidthsCm[4] >= Round3("A&amp;B &lt;1&gt;".Length * 0.2 + 2 * H));
    }

    [Fact]
    public void Toleranced_counterbore_is_a_stack_text_and_one_text_per_later_line()
    {
        var tol = new HoleTolerance("H7", "H7", 0.0012, 0);
        var h = Plain("S#1", 0.6, 1, 1, true, tol) with { Type = HoleTableType.Counterbore, SeatDiameterCm = 1.1, SeatDepthCm = 0.64 };
        var f = HoleTableFormattedText.For(HoleTableDescriptions.For(h), H);
        Assert.True(f.HasStack);
        Assert.Equal(2, f.Texts.Count);
        Assert.Equal(3, f.LineCount);

        var p = Of(Layout([h]), HoleTableKind.Precision);
        var descTexts = p.Texts.Where(t => t.FormattedText == f.Texts[0] || t.FormattedText == f.Texts[1]).ToList();
        Assert.Equal(2, descTexts.Count);
        var groupH = Math.Max(RowH, RowH + 2 * Pitch);
        Assert.Equal(2 * RowH + groupH, p.HeightCm, Tol);
        var mid = p.HeightCm - 2 * RowH - groupH / 2;
        var stack = descTexts.Single(t => t.HasStack);
        var seat = descTexts.Single(t => !t.HasStack);
        Assert.Equal(mid + Pitch / 2, stack.CenterYCm, Tol);
        Assert.Equal(mid - Pitch, seat.CenterYCm, Tol);
        Assert.Equal(stack.CenterXCm, seat.CenterXCm, Tol);
    }

    // ---- align

    [Fact]
    public void Align_with_both_tables_stacks_precision_above_clearance()
    {
        var a = HoleTableAlign.Positions(40, 5, 6, 3);
        Assert.Equal(new SheetPoint(40, 5), a.Clearance);
        Assert.Equal(new SheetPoint(40, 11), a.Precision);
    }

    [Fact]
    public void Align_with_only_clearance_puts_it_on_the_corner()
    {
        var a = HoleTableAlign.Positions(40, 5, 6, null);
        Assert.Equal(new SheetPoint(40, 5), a.Clearance);
        Assert.Null(a.Precision);
    }

    [Fact]
    public void Align_with_only_precision_puts_it_on_the_corner()
    {
        var a = HoleTableAlign.Positions(40, 5, null, 3);
        Assert.Null(a.Clearance);
        Assert.Equal(new SheetPoint(40, 5), a.Precision);
    }
}
