using InventorAddin.Core.Drawing;

namespace InventorAddin.Core.Tests.Drawing;

public sealed class PrintTemplateLayoutTests
{
    private const double Eps = 1e-6;

    private static IEnumerable<SheetSizeSpec> Every() => PrintTemplateLayout.IsoSheets.Concat(PrintTemplateLayout.AnsiSheets);

    public static IEnumerable<object[]> AllSheets() => Every().Select(s => new object[] { s.Name, s.Units });

    private static SheetSizeSpec Find(string name, PrintUnits units) =>
        PrintTemplateLayout.For(units).SheetSizes.Single(s => s.Name == name);

    [Fact]
    public void SheetSizesAndOrientation()
    {
        Assert.Equal(new[] { "A4", "A3", "A2", "A1", "A0" }, PrintTemplateLayout.Metric.SheetSizes.Select(s => s.Name));
        Assert.Equal(new[] { "A", "B", "C", "D", "E" }, PrintTemplateLayout.Imperial.SheetSizes.Select(s => s.Name));
        Assert.Equal(new[] { "A4Metric", "AImperial" }, Every().Where(s => s.IsPortrait).Select(s => s.Name + s.Units));
        Assert.Equal((210.0, 297.0), (Find("A4", PrintUnits.Metric).WidthMm, Find("A4", PrintUnits.Metric).HeightMm));
        Assert.Equal((1189.0, 841.0), (Find("A0", PrintUnits.Metric).WidthMm, Find("A0", PrintUnits.Metric).HeightMm));
        Assert.Equal(215.9, Find("A", PrintUnits.Imperial).WidthMm, 3);
        Assert.Equal(1117.6, Find("E", PrintUnits.Imperial).WidthMm, 3);
        Assert.Equal(863.6, Find("E", PrintUnits.Imperial).HeightMm, 3);
    }

    [Fact]
    public void BorderMargins()
    {
        var a3 = Find("A3", PrintUnits.Metric);
        Assert.Equal(20, a3.BorderLeftMm);
        Assert.Equal(410, a3.BorderRightMm);
        Assert.Equal(10, a3.BorderBottomMm);
        Assert.Equal(287, a3.BorderTopMm);
        var ansiB = Find("B", PrintUnits.Imperial);
        Assert.Equal(20, ansiB.BorderLeftMm);
        Assert.Equal(12.7, ansiB.BorderBottomMm, 6);
        Assert.Equal(431.8 - 12.7, ansiB.BorderRightMm, 6);
    }

    [Theory]
    [InlineData("A4", PrintUnits.Metric, 4, 6)]
    [InlineData("A3", PrintUnits.Metric, 8, 6)]
    [InlineData("A2", PrintUnits.Metric, 12, 8)]
    [InlineData("A1", PrintUnits.Metric, 16, 12)]
    [InlineData("A0", PrintUnits.Metric, 24, 16)]
    [InlineData("A", PrintUnits.Imperial, 4, 2)]
    [InlineData("B", PrintUnits.Imperial, 4, 4)]
    [InlineData("C", PrintUnits.Imperial, 8, 4)]
    [InlineData("D", PrintUnits.Imperial, 8, 4)]
    [InlineData("E", PrintUnits.Imperial, 16, 8)]
    public void ZoneCountsAndLabels(string name, PrintUnits units, int across, int down)
    {
        var s = Find(name, units);
        Assert.Equal(across, s.ZonesAcross);
        Assert.Equal(down, s.ZonesDown);
        Assert.Equal(across, s.ZoneColumnLabels.Count);
        Assert.Equal("1", s.ZoneColumnLabels[0]);
        Assert.Equal(across.ToString(), s.ZoneColumnLabels[^1]);
        Assert.Equal(down, s.ZoneRowLabels.Count);
        Assert.Equal("A", s.ZoneRowLabels[0]);
        Assert.Equal(((char)('A' + down - 1)).ToString(), s.ZoneRowLabels[^1]);
        // Numbers run left to right, letters from the top.
        Assert.Equal(s.BorderLeftMm, s.ZoneColumnEdgesXMm[0], 6);
        Assert.Equal(s.BorderRightMm, s.ZoneColumnEdgesXMm[^1], 6);
        Assert.Equal(s.BorderTopMm, s.ZoneRowEdgesYMm[0], 6);
        Assert.Equal(s.BorderBottomMm, s.ZoneRowEdgesYMm[^1], 6);
        Assert.True(s.ZoneColumnEdgesXMm.Zip(s.ZoneColumnEdgesXMm.Skip(1), (a, b) => b > a).All(x => x));
        Assert.True(s.ZoneRowEdgesYMm.Zip(s.ZoneRowEdgesYMm.Skip(1), (a, b) => b < a).All(x => x));
    }

    [Theory]
    [MemberData(nameof(AllSheets))]
    public void CentringArrowsAtEdgeMiddles(string name, PrintUnits units)
    {
        var s = Find(name, units);
        double mx = (s.BorderLeftMm + s.BorderRightMm) / 2, my = (s.BorderBottomMm + s.BorderTopMm) / 2;
        Assert.Equal(mx, s.CentringArrow(SheetEdge.Top)[0].X, 6);
        Assert.Equal(mx, s.CentringArrow(SheetEdge.Bottom)[0].X, 6);
        Assert.Equal(my, s.CentringArrow(SheetEdge.Left)[0].Y, 6);
        Assert.Equal(my, s.CentringArrow(SheetEdge.Right)[0].Y, 6);
        // The tip is inside the border, the base outside it.
        Assert.True(s.CentringArrow(SheetEdge.Top)[0].Y < s.BorderTopMm);
        Assert.True(s.CentringArrow(SheetEdge.Top)[1].Y > s.BorderTopMm);
        Assert.True(s.CentringArrow(SheetEdge.Bottom)[1].Y < s.BorderBottomMm);
        Assert.True(s.CentringArrow(SheetEdge.Left)[1].X < s.BorderLeftMm);
        Assert.True(s.CentringArrow(SheetEdge.Right)[1].X > s.BorderRightMm);
        // And the base stays on the sheet.
        Assert.True(s.CentringArrow(SheetEdge.Bottom)[1].Y > 0);
        Assert.True(s.CentringArrow(SheetEdge.Right)[1].X < s.WidthMm);
    }

    [Fact]
    public void TriadLabels()
    {
        Assert.Equal("X", PrintTemplateLayout.Triad.XLabel);
        Assert.Equal("Y", PrintTemplateLayout.Triad.YLabel);
    }

    [Theory]
    [MemberData(nameof(AllSheets))]
    public void TriadLiesInsideTheBorderAndClearOfTheTitleBlock(string name, PrintUnits units)
    {
        var s = Find(name, units);
        var o = s.TriadOriginMm;
        double ext = PrintTemplateLayout.TriadExtentMm;
        Assert.True(o.X > 0 && o.Y > 0);
        Assert.True(o.X + ext <= s.BorderWidthMm + Eps);
        Assert.True(o.Y + ext <= s.BorderHeightMm + Eps);
        // Block in border coordinates.
        double bl = s.TitleBlockOriginMm.X - s.BorderLeftMm, bt = PrintTemplateLayout.TitleBlockHeightMm;
        bool overlap = o.X < bl + PrintTemplateLayout.TitleBlockWidthMm && o.X + ext > bl && o.Y < bt && o.Y + ext > 0;
        Assert.False(overlap);
    }

    [Theory]
    [InlineData("A4", PrintUnits.Metric)]
    [InlineData("A", PrintUnits.Imperial)]
    public void PortraitTriadSitsAboveTheBlocksLeftEnd(string name, PrintUnits units)
    {
        var s = Find(name, units);
        Assert.Equal(PrintTemplateLayout.Triad.OriginXMm, s.TriadOriginMm.X, 6);
        Assert.Equal(PrintTemplateLayout.TitleBlockHeightMm + PrintTemplateLayout.Triad.OriginYMm, s.TriadOriginMm.Y, 6);
    }

    [Fact]
    public void A3TriadIsInTheCorner()
    {
        var o = Find("A3", PrintUnits.Metric).TriadOriginMm;
        Assert.Equal((5.0, 5.0), (o.X, o.Y));
    }

    [Theory]
    [MemberData(nameof(AllSheets))]
    public void TitleBlockFitsInsideTheBorder(string name, PrintUnits units)
    {
        var s = Find(name, units);
        Assert.True(s.TitleBlockFits);
        Assert.True(s.TitleBlockOriginMm.X >= s.BorderLeftMm - Eps);
        Assert.Equal(s.BorderRightMm, s.TitleBlockOriginMm.X + PrintTemplateLayout.TitleBlockWidthMm, 6);
        Assert.Equal(s.BorderBottomMm, s.TitleBlockOriginMm.Y, 6);
        Assert.True(s.TitleBlockOriginMm.Y + PrintTemplateLayout.TitleBlockHeightMm <= s.BorderTopMm + Eps);
        // Everything in the block lies inside its 180 by 55 frame.
        foreach (var c in PrintTemplateLayout.For(units).Cells)
        {
            Assert.True(c.XMm >= -Eps && c.YMm >= -Eps && c.RightMm <= 180 + Eps && c.TopMm <= 55 + Eps, c.Name);
        }
    }

    [Fact]
    public void A4PortraitIsExactly180Wide()
    {
        var a4 = Find("A4", PrintUnits.Metric);
        Assert.Equal(180, a4.BorderWidthMm, 6);
        Assert.Equal(20, a4.TitleBlockOriginMm.X, 6);
        Assert.True(a4.IsPortrait);
        var ansiA = Find("A", PrintUnits.Imperial);
        Assert.True(ansiA.IsPortrait);
        Assert.True(ansiA.BorderWidthMm >= 180);
    }

    [Theory]
    [InlineData(PrintUnits.Metric)]
    [InlineData(PrintUnits.Imperial)]
    public void CellsDoNotOverlapAndFillTheBlock(PrintUnits units)
    {
        var cells = PrintTemplateLayout.For(units).Cells;
        for (int i = 0; i < cells.Count; i++)
        {
            for (int j = i + 1; j < cells.Count; j++)
            {
                var a = cells[i];
                var b = cells[j];
                bool overlap = a.XMm < b.RightMm - Eps && b.XMm < a.RightMm - Eps && a.YMm < b.TopMm - Eps && b.YMm < a.TopMm - Eps;
                Assert.False(overlap, $"{a.Name} overlaps {b.Name}");
            }
        }
        // The cells tile the 180 by 55 block exactly.
        Assert.Equal(180 * 55, cells.Sum(c => c.WidthMm * c.HeightMm), 6);
    }

    [Theory]
    [InlineData(PrintUnits.Metric)]
    [InlineData(PrintUnits.Imperial)]
    public void EveryCellHasAFieldOrFixedText(PrintUnits units)
    {
        var cells = PrintTemplateLayout.For(units).Cells;
        foreach (var c in cells)
        {
            Assert.True(c.Field != null || !string.IsNullOrEmpty(c.FixedText), c.Name);
            Assert.False(c.Field != null && c.FixedText != null, c.Name);
        }
        var names = cells.Select(c => c.Name).ToList();
        Assert.Equal(names.Count, names.Distinct().Count());
        Assert.Equal(21, names.Count);
    }

    [Fact]
    public void EveryFieldIsUsed()
    {
        var used = PrintTemplateLayout.Metric.Cells.Where(c => c.Field != null).Select(c => c.Field!.Value).ToHashSet();
        Assert.Equal(Enum.GetValues<PrintField>().ToHashSet(), used);
        Assert.Equal(PrintField.PartNumber, PrintTemplateLayout.Metric.SmallPartNumber.Field);
    }

    [Fact]
    public void FieldsAndSizesMatchTheMockup()
    {
        var cells = PrintTemplateLayout.Metric.Cells.ToDictionary(c => c.Name);
        Assert.Equal(PrintField.Status, cells["Status"].Field);
        Assert.Equal(PrintField.FinishSpec, cells["Finish"].Field);
        Assert.Equal(PrintField.DrawingAuthor, cells["Detailer"].Field);
        Assert.Equal(PrintField.Mass, cells["Weight"].Field);
        Assert.Equal((40.0, 30.0, 45.0), (cells["Material"].WidthMm, cells["MaterialSize"].WidthMm, cells["Finish"].WidthMm));
        Assert.Equal(cells["Material"].RightMm, cells["MaterialSize"].XMm, 6);
        Assert.Equal(cells["MaterialSize"].RightMm, cells["Finish"].XMm, 6);
        Assert.Equal(5, cells["Project"].HeightMm);
        Assert.Equal(50, cells["Project"].YMm, 6);
        Assert.Equal(0, cells["Scale"].YMm, 6);
        Assert.Equal(4, cells["Description"].ValueSizeMm);
        Assert.Equal(7, cells["PartNumber"].ValueSizeMm);
        Assert.True(cells["PartNumber"].Bold && cells["Revision"].Bold);
        Assert.Equal(2.5, cells["Status"].ValueSizeMm);
        Assert.Equal(2.5, cells["Project"].ValueSizeMm);
        Assert.Equal(2.5, cells["FileName"].ValueSizeMm);
        Assert.Equal(2.5, cells["MaterialSize"].ValueSizeMm);
        Assert.Equal(2.8, cells["Material"].ValueSizeMm);
        Assert.Equal("MATERIAL SIZE", cells["MaterialSize"].Label);
        Assert.Equal(1.8, PrintTemplateLayout.LabelSizeMm);
        Assert.Equal("Tahoma", PrintTemplateLayout.FontName);
        // No FINISH line: nothing is labelled as one beyond the 45 mm field.
        Assert.Single(PrintTemplateLayout.Metric.Cells, c => c.Label == "FINISH");
    }

    [Fact]
    public void LeftSectionSizesAndLogo()
    {
        var cells = PrintTemplateLayout.Metric.Cells.ToDictionary(c => c.Name);
        Assert.Equal((22.0, 22.0), (cells["Logo"].WidthMm, cells["Logo"].HeightMm));
        Assert.Equal((43.0, 22.0), (cells["Tolerances"].WidthMm, cells["Tolerances"].HeightMm));
        Assert.Equal((65.0, 20.0), (cells["Projection"].WidthMm, cells["Projection"].HeightMm));
        Assert.Equal((65.0, 8.0), (cells["Url"].WidthMm, cells["Url"].HeightMm));
        Assert.Equal(0, cells["Url"].YMm, 6);
        Assert.Equal(Branding.WebsiteUrl, cells["Url"].FixedText);
        Assert.Equal("aidanwbrown.com", Branding.WebsiteUrl);
        Assert.Equal("THIRD ANGLE PROJECTION", cells["Projection"].FixedText);
        var logo = PrintTemplateLayout.Metric.Logo;
        var logoCell = cells["Logo"];
        Assert.Equal(Branding.PrintLogoFileName, logo.FileName);
        Assert.Equal(Branding.PrintLogoFileName, logoCell.FixedText);
        Assert.Equal((19.0, 19.0), (logo.WidthMm, logo.HeightMm));
        // 1.5 mm margin on every side of the mark.
        Assert.Equal(1.5, logo.XMm - logoCell.XMm, 6);
        Assert.Equal(1.5, logoCell.RightMm - (logo.XMm + logo.WidthMm), 6);
        Assert.Equal(1.5, logo.YMm - logoCell.YMm, 6);
        Assert.Equal(1.5, logoCell.TopMm - (logo.YMm + logo.HeightMm), 6);
    }

    private static string ToleranceReading(PrintTemplateLayout layout)
    {
        var t = layout.Texts.Where(x => x.CellName == PrintTemplateLayout.ToleranceCellName).Select(x => x.Text).ToList();
        // heading, units, "TOLERANCES:", three name/value pairs, "ANGLES" and its value, the edge note
        string linear = string.Join(", ", new[] { 3, 5, 7 }.Select(i => $"{t[i]} {t[i + 1]}"));
        return $"{t[0]} {t[1]}. {t[2]} {linear}, {t[9]} {t[10]}. {t[11]}";
    }

    [Fact]
    public void ToleranceTextsWordForWord()
    {
        Assert.Equal(
            "UNLESS OTHERWISE SPECIFIED: DIMENSIONS IN MM. TOLERANCES: X ±0.5, X.X ±0.2, X.XX ±0.05, ANGLES ±0.5°. REMOVE BURRS, BREAK SHARP EDGES.",
            PrintTemplateLayout.MetricToleranceText);
        Assert.Equal(
            "UNLESS OTHERWISE SPECIFIED: DIMENSIONS IN INCHES. TOLERANCES: .X ±.03, .XX ±.01, .XXX ±.005, ANGLES ±0.5°. REMOVE BURRS, BREAK SHARP EDGES.",
            PrintTemplateLayout.ImperialToleranceText);
        // The separate texts of the box, read in order, say the same words.
        Assert.Equal(PrintTemplateLayout.MetricToleranceText, ToleranceReading(PrintTemplateLayout.Metric));
        Assert.Equal(PrintTemplateLayout.ImperialToleranceText, ToleranceReading(PrintTemplateLayout.Imperial));
        Assert.Equal(PrintTemplateLayout.MetricToleranceText,
            PrintTemplateLayout.Metric.Cells.Single(c => c.Name == "Tolerances").FixedText);
        Assert.Equal(PrintTemplateLayout.ImperialToleranceText,
            PrintTemplateLayout.Imperial.Cells.Single(c => c.Name == "Tolerances").FixedText);
    }

    [Fact]
    public void MetricAndImperialDifferOnlyInTolerancesUnitsAndSizes()
    {
        var m = PrintTemplateLayout.Metric;
        var i = PrintTemplateLayout.Imperial;
        static IEnumerable<TitleBlockCell> Blanked(PrintTemplateLayout l) =>
            l.Cells.Select(c => c.Name == "Tolerances" ? c with { FixedText = "" } : c);
        Assert.Equal(Blanked(m), Blanked(i));
        Assert.Equal(m.Lines, i.Lines);
        Assert.Equal(m.Logo, i.Logo);
        Assert.Equal(m.SmallPartNumber, i.SmallPartNumber);
        Assert.Equal(m.Symbol.Frustum, i.Symbol.Frustum);
        Assert.Equal(m.Texts.Where(t => t.CellName != "Tolerances"), i.Texts.Where(t => t.CellName != "Tolerances"));
        // Same positions in the tolerance box, only the words differ.
        Assert.Equal(m.Texts.Where(t => t.CellName == "Tolerances").Select(t => (t.XMm, t.BaselineYMm, t.SizeMm)),
            i.Texts.Where(t => t.CellName == "Tolerances").Select(t => (t.XMm, t.BaselineYMm, t.SizeMm)));
        Assert.NotEqual(m.ToleranceText, i.ToleranceText);
        Assert.Equal(("MM", "kg"), (m.UnitsText, m.MassUnit));
        Assert.Equal(("IN", "lb"), (i.UnitsText, i.MassUnit));
        Assert.All(m.SheetSizes, s => Assert.Equal(PrintUnits.Metric, s.Units));
        Assert.All(i.SheetSizes, s => Assert.Equal(PrintUnits.Imperial, s.Units));
        Assert.Empty(m.SheetSizes.Select(s => s.Name).Intersect(i.SheetSizes.Select(s => s.Name)));
    }

    [Fact]
    public void SmallPartNumberInTheRightMarginLevelWithTheBorderBottom()
    {
        var f = PrintTemplateLayout.Metric.SmallPartNumber;
        Assert.Equal(2.5, f.SizeMm);
        Assert.Equal(90, f.RotationDegrees);
        Assert.Equal(PrintField.PartNumber, f.Field);
        foreach (var s in Every())
        {
            var p = s.SmallPartNumberMm;
            Assert.Equal(s.WidthMm - 3, p.X, 6);
            Assert.Equal(s.BorderBottomMm, p.Y, 6);
            // The glyphs run left of the baseline: that band must lie between the zone frame and the sheet edge.
            double frameRight = s.BorderRightMm + PrintTemplateLayout.ZoneFrameOffsetMm;
            Assert.True(p.X - PrintTemplateLayout.SmallPartNumberGlyphHeightMm > frameRight, s.Name);
            Assert.True(p.X < s.WidthMm, s.Name);
        }
    }

    [Fact]
    public void TableColumns()
    {
        Assert.Equal(new[] { "Item", "Quantity", "Part Number", "Description", "Make/Buy", "Vendor", "Stock Number" },
            PrintTemplateLayout.PartsListColumns.Select(c => c.Source));
        Assert.Equal(new[] { "Revision", "Description", "Date", "By" }, PrintTemplateLayout.RevisionTableColumns.Select(c => c.Source));
    }

    [Fact]
    public void LinesAreInsideTheBlock()
    {
        foreach (var l in PrintTemplateLayout.Metric.Lines)
        {
            Assert.InRange(l.X1Mm, -Eps, 180 + Eps);
            Assert.InRange(l.X2Mm, -Eps, 180 + Eps);
            Assert.InRange(l.Y1Mm, -Eps, 55 + Eps);
            Assert.InRange(l.Y2Mm, -Eps, 55 + Eps);
        }
    }

    [Fact]
    public void TriadExtent_IsDerivedFromTheLabels_SoTheDrawingAndTheOriginCannotDrift()
    {
        var t = PrintTemplateLayout.Triad;
        Assert.Equal(17.5, PrintTemplateLayout.TriadExtentMm, 6);
        Assert.Equal(t.LabelDistanceMm + t.LabelSizeMm, t.ExtentMm, 6);
        // Both labels end at the extent along their axis, and nothing else reaches further.
        Assert.Equal(t.ExtentMm, t.XLabelMm.X + t.LabelSizeMm, 6);
        Assert.Equal(t.ExtentMm, t.YLabelMm.Y + t.LabelSizeMm, 6);
        Assert.True(t.ArrowLengthMm <= t.ExtentMm);
        Assert.Equal(0.4, t.AxisWeightMm, 6);
    }

    [Theory]
    [InlineData("A3", PrintUnits.Metric, "AWB ISO A3")]
    [InlineData("A4", PrintUnits.Metric, "AWB ISO A4")]
    [InlineData("B", PrintUnits.Imperial, "AWB ANSI B")]
    [InlineData("A", PrintUnits.Imperial, "AWB ANSI A")]
    public void FormatNames(string name, PrintUnits units, string expected) =>
        Assert.Equal(expected, Find(name, units).FormatName);

    [Fact]
    public void TheDefaultSheetIsA3OrB_AndIsOneOfTheSizes()
    {
        Assert.Equal("A3", PrintTemplateLayout.Metric.DefaultSheetName);
        Assert.Equal("B", PrintTemplateLayout.Imperial.DefaultSheetName);
        Assert.Contains(PrintTemplateLayout.Metric.SheetSizes, s => s.Name == "A3");
        Assert.Contains(PrintTemplateLayout.Imperial.SheetSizes, s => s.Name == "B");
    }
}
