using InventorAddin.Core.Drawing;

namespace InventorAddin.Core.Tests.Drawing;

/// <summary>Task 215: the mockup's text sizes, aligned tolerance columns, shared baselines, and that no fixed text overflows its cell.</summary>
public sealed class PrintTemplateFitTests
{
    private const double Eps = 1e-6;
    private const string Tol = PrintTemplateLayout.ToleranceCellName;

    private static IEnumerable<PrintTemplateLayout> Both() => new[] { PrintTemplateLayout.Metric, PrintTemplateLayout.Imperial };

    public static IEnumerable<object[]> BothUnits() => new[] { new object[] { PrintUnits.Metric }, new object[] { PrintUnits.Imperial } };

    [Fact]
    public void TextSizesAreTheMockups()
    {
        // docs/ui-mockups/title-block.html: labels 1.8, values 2.8 (project, file name, status 2.5; material size 2.8 since task 223),
        // description 4, part number and revision 7 bold; tolerance heading and note 1.8, units line 2.5 bold, columns 2.4.
        Assert.Equal(1.8, PrintTemplateLayout.LabelSizeMm);
        Assert.Equal(2.8, PrintTemplateLayout.ValueSizeMm);
        Assert.Equal(2.5, PrintTemplateLayout.SmallFieldSizeMm);
        Assert.Equal(4, PrintTemplateLayout.DescriptionSizeMm);
        Assert.Equal(7, PrintTemplateLayout.PartNumberSizeMm);
        var cells = PrintTemplateLayout.Metric.Cells.Where(c => c.Field != null).ToDictionary(c => c.Name);
        foreach (var name in new[] { "Material", "MaterialSize", "Finish", "Designer", "Detailer", "Date", "Weight", "Scale", "Sheet", "Units", "Size" })
            Assert.Equal(2.8, cells[name].ValueSizeMm);
        foreach (var layout in Both())
        {
            var tol = layout.Texts.Where(t => t.CellName == Tol).ToList();
            Assert.Equal(new[] { 1.8, 2.5, 1.8 }, tol.Take(3).Select(t => t.SizeMm));
            Assert.All(tol.Skip(3).Take(8), t => Assert.Equal(2.4, t.SizeMm));
            Assert.All(tol.Skip(11), t => Assert.Equal(1.8, t.SizeMm));
            Assert.True(tol[1].Bold);
        }
    }

    [Theory]
    [MemberData(nameof(BothUnits))]
    public void ToleranceClassesAndValuesAreInAlignedColumns(PrintUnits units)
    {
        var layout = PrintTemplateLayout.For(units);
        var tol = layout.Texts.Where(t => t.CellName == Tol).ToList();
        var classes = new[] { tol[3], tol[5], tol[7] };
        var values = new[] { tol[4], tol[6], tol[8] };
        Assert.Single(classes.Select(t => t.XMm).Distinct());
        Assert.Single(values.Select(t => t.XMm).Distinct());
        for (int i = 0; i < 3; i++)
            Assert.Equal(classes[i].BaselineYMm, values[i].BaselineYMm, 6);
        Assert.True(classes[0].BaselineYMm > classes[1].BaselineYMm && classes[1].BaselineYMm > classes[2].BaselineYMm);
        double widestClass = classes.Max(t => PrintTextFit.WidthMm(t.Text, t.SizeMm));
        Assert.True(values[0].XMm - (classes[0].XMm + widestClass) >= PrintTemplateLayout.ToleranceColumnGapMm - Eps);
        // The angles sit beside the linear tolerances, past the divider.
        var angles = tol[9];
        Assert.Equal(angles.XMm, tol[10].XMm, 6);
        double widestValue = values.Max(t => PrintTextFit.WidthMm(t.Text, t.SizeMm));
        var divider = layout.Lines.Single(l => Math.Abs(l.X1Mm - l.X2Mm) < Eps && l.WeightMm == PrintTemplateLayout.CellLineWeightMm
            && l.X1Mm > values[0].XMm && l.X1Mm < angles.XMm);
        Assert.True(divider.X1Mm > values[0].XMm + widestValue);
        Assert.True(divider.X1Mm < angles.XMm);
    }

    [Fact]
    public void ToleranceTextsSitAtTheSamePlacesOnBothTemplates()
    {
        Assert.Equal(
            PrintTemplateLayout.Metric.Texts.Where(t => t.CellName == Tol).Select(t => (t.XMm, t.BaselineYMm)),
            PrintTemplateLayout.Imperial.Texts.Where(t => t.CellName == Tol).Select(t => (t.XMm, t.BaselineYMm)));
    }

    [Theory]
    [MemberData(nameof(BothUnits))]
    public void TheEdgeNoteWrapsInsideTheBox(PrintUnits units)
    {
        var layout = PrintTemplateLayout.For(units);
        var box = layout.Cells.Single(c => c.Name == Tol);
        var note = layout.Texts.Where(t => t.CellName == Tol).Skip(11).ToList();
        Assert.True(note.Count >= 1);
        Assert.Equal(PrintTemplateLayout.EdgeNote, string.Join(" ", note.Select(t => t.Text)));
        Assert.All(note, t => Assert.True(
            t.XMm + PrintTextFit.WidthMm(t.Text, t.SizeMm) <= box.RightMm - PrintTemplateLayout.ToleranceBoxPaddingMm + Eps));
        for (int i = 1; i < note.Count; i++)
            Assert.True(note[i - 1].BaselineYMm > note[i].BaselineYMm);
    }

    [Theory]
    [MemberData(nameof(BothUnits))]
    public void InlineValuesShareTheirLabelsBaseline(PrintUnits units)
    {
        var cells = PrintTemplateLayout.For(units).Cells.ToDictionary(c => c.Name);
        foreach (var name in new[] { "Project", "FileName" })
        {
            Assert.True(cells[name].Inline);
            Assert.Equal(cells[name].LabelBaselineYMm, cells[name].ValueBaselineYMm, 6);
        }
        Assert.Equal(cells["Project"].ValueBaselineYMm, cells["FileName"].ValueBaselineYMm, 6);
    }

    [Theory]
    [MemberData(nameof(BothUnits))]
    public void CellsInOneRowShareOneLabelBaseline_AndTheEightMmRowsOneValueBaseline(PrintUnits units)
    {
        var fields = PrintTemplateLayout.For(units).Cells.Where(c => c.Field != null && !c.Inline).ToList();
        foreach (var row in fields.GroupBy(c => Math.Round(c.YMm, 6)))
            Assert.Single(row.Select(c => Math.Round(c.LabelBaselineYMm, 6)).Distinct());
        // (The tall part number row keeps the mockup's lower 2.5 mm status.)
        foreach (var row in fields.Where(c => c.HeightMm == 8).GroupBy(c => Math.Round(c.YMm, 6)))
            Assert.Single(row.Select(c => Math.Round(c.ValueBaselineYMm, 6)).Distinct());
    }

    [Theory]
    [MemberData(nameof(BothUnits))]
    public void EveryLabelAndFixedTextFitsItsCellWithHalfAMillimetrePadding(PrintUnits units)
    {
        var layout = PrintTemplateLayout.For(units);
        const double pad = 0.5;

        void Fits(TitleBlockCell cell, string text, double size, bool bold, double x, double baseline, TextAnchor anchor)
        {
            double w = PrintTextFit.WidthMm(text, size, bold);
            double left = anchor == TextAnchor.Centre ? x - w / 2 : x;
            Assert.True(left >= cell.XMm + pad - Eps, $"{cell.Name} '{text}': left {left} < {cell.XMm + pad}");
            Assert.True(left + w <= cell.RightMm - pad + Eps, $"{cell.Name} '{text}': right {left + w} > {cell.RightMm - pad}");
            Assert.True(baseline >= cell.YMm + pad - Eps, $"{cell.Name} '{text}': baseline {baseline} below the cell");
            Assert.True(baseline + PrintTextFit.CapHeightMm(size) <= cell.TopMm - pad + Eps, $"{cell.Name} '{text}': top above the cell");
        }

        foreach (var cell in layout.Cells.Where(c => c.Label != null))
        {
            string label = cell.Inline ? cell.Label + ":" : cell.Label!;
            Fits(cell, label, PrintTemplateLayout.LabelSizeMm, false, cell.LabelXMm, cell.LabelBaselineYMm, TextAnchor.Start);
        }
        var byName = layout.Cells.ToDictionary(c => c.Name);
        foreach (var t in layout.Texts)
            Fits(byName[t.CellName], t.Text, t.SizeMm, t.Bold, t.XMm, t.BaselineYMm, t.Anchor);
    }

    [Theory]
    [MemberData(nameof(BothUnits))]
    public void ValuesStartInsideTheirCells_AndInlineValuesClearTheirLabels(PrintUnits units)
    {
        foreach (var cell in PrintTemplateLayout.For(units).Cells.Where(c => c.Field != null))
        {
            Assert.True(cell.ValueXMm >= cell.XMm + 0.5 - Eps, cell.Name);
            Assert.True(cell.ValueXMm < cell.RightMm - 5, cell.Name);
            if (cell.Inline)
                Assert.True(cell.ValueXMm >= cell.LabelXMm + PrintTextFit.WidthMm(cell.Label + ":", PrintTemplateLayout.LabelSizeMm) + 1);
        }
    }

    [Fact]
    public void PrintTextFit_WidthsComeFromTahoma()
    {
        Assert.Equal(0.546 * 2.5, PrintTextFit.WidthMm("0", 2.5), 6);
        Assert.Equal(0.546 * 3 * 2.5, PrintTextFit.WidthMm("012", 2.5), 6);
        Assert.Equal(0.637 * 10, PrintTextFit.WidthMm("7", 10, bold: true), 6);
        Assert.Equal(0.674 * 2, PrintTextFit.WidthMm("&", 2), 6);
        Assert.Equal(0.313 * 2, PrintTextFit.WidthMm(" ", 2), 6);
        Assert.Equal(0.600 * 2, PrintTextFit.WidthMm("A", 2), 6);
        Assert.Equal(0.902 * 2, PrintTextFit.WidthMm("W", 2), 6);
        Assert.Equal(0.728 * 2, PrintTextFit.WidthMm("±", 2), 6);
        Assert.Equal(0.471 * 2, PrintTextFit.WidthMm("°", 2), 6);
        Assert.Equal(PrintTextFit.FallbackAdvanceEm * 2, PrintTextFit.WidthMm("€", 2), 6);
        Assert.Equal(0, PrintTextFit.WidthMm(string.Empty, 2.5));
        Assert.Equal(0.727 * 2.8, PrintTextFit.CapHeightMm(2.8), 6);
        const string sample = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789.,:-/±° ";
        Assert.True(PrintTextFit.WidthMm(sample, 1, true) > PrintTextFit.WidthMm(sample, 1));
    }

    [Fact]
    public void UpperOrigin_is_a_cap_height_above_the_baseline()
    {
        Assert.Equal(10 + 0.727 * 2.8, PrintTextFit.UpperOriginYMm(10, 2.8), 6);
        Assert.Equal(-3, PrintTextFit.UpperOriginYMm(-3, 0), 6);
    }

    [Theory]
    [MemberData(nameof(BothUnits))]
    public void UpperOrigin_keeps_every_value_in_a_row_on_the_row_baseline(PrintUnits units)
    {
        // The origin depends only on the baseline and the size, never on the text, so values with and without descenders agree.
        foreach (TitleBlockCell cell in PrintTemplateLayout.For(units).Cells.Where(c => c.Field != null))
            Assert.Equal(cell.ValueBaselineYMm + PrintTextFit.CapHeightMm(cell.ValueSizeMm), PrintTextFit.UpperOriginYMm(cell.ValueBaselineYMm, cell.ValueSizeMm), 9);
    }

    [Fact]
    public void PrintTextFit_WrapBreaksAtSpacesAndKeepsEveryWord()
    {
        const string text = "REMOVE BURRS, BREAK SHARP EDGES.";
        Assert.Equal(new[] { text }, PrintTextFit.Wrap(text, 1.8, 1000));
        double half = PrintTextFit.WidthMm(text, 1.8) / 2;
        var lines = PrintTextFit.Wrap(text, 1.8, half);
        Assert.True(lines.Count >= 2);
        Assert.Equal(text, string.Join(" ", lines));
        Assert.All(lines, l => Assert.True(PrintTextFit.WidthMm(l, 1.8) <= half || !l.Contains(' ')));
        Assert.Equal(new[] { "ABCDEFGH" }, PrintTextFit.Wrap("ABCDEFGH", 2, 1));
        Assert.Empty(PrintTextFit.Wrap(string.Empty, 2, 10));
    }

    [Theory]
    [MemberData(nameof(BothUnits))]
    public void EveryValueInAnEightMmRowHasTheSameSizeAndBaseline(PrintUnits units)
    {
        // Inventor puts a text's bottom edge at its point; the baseline sits a size-dependent distance above that, so values of
        // different sizes in one row would print on different baselines (task 223: Material Size sat lower than Material).
        var rows = PrintTemplateLayout.For(units).Cells
            .Where(c => c.Field != null && !c.Inline && c.HeightMm == 8)
            .GroupBy(c => Math.Round(c.YMm, 6)).ToList();
        Assert.Equal(3, rows.Count);
        foreach (var row in rows)
        {
            Assert.Single(row.Select(c => Math.Round(c.ValueSizeMm, 6)).Distinct());
            Assert.Single(row.Select(c => Math.Round(c.ValueBaselineYMm, 6)).Distinct());
        }
    }

    [Theory]
    [MemberData(nameof(BothUnits))]
    public void TheLastToleranceLineClearsTheEdgeNote(PrintUnits units)
    {
        var tol = PrintTemplateLayout.For(units).Texts.Where(t => t.CellName == Tol).ToList();
        var note = tol.Where(t => t.SizeMm == PrintTemplateLayout.LabelSizeMm && PrintTemplateLayout.EdgeNote.Contains(t.Text)).ToList();
        var lastLine = tol.Where(t => t.SizeMm == PrintTemplateLayout.ToleranceTextSizeMm).Min(t => t.BaselineYMm);
        double noteTop = note.Max(t => t.BaselineYMm) + PrintTextFit.CapHeightMm(PrintTemplateLayout.LabelSizeMm);
        Assert.True(lastLine - noteTop >= PrintTemplateLayout.ToleranceEdgeNoteMinGapMm - Eps,
            $"gap {lastLine - noteTop:0.00} mm");
        // Nothing leaves the box.
        var box = PrintTemplateLayout.For(units).Cells.Single(c => c.Name == Tol);
        Assert.All(tol, t => Assert.True(t.BaselineYMm >= box.YMm + 0.5 - Eps, t.Text));
    }

    [Theory]
    [MemberData(nameof(BothUnits))]
    public void ASampleValueFitsEveryFieldCell(PrintUnits units)
    {
        foreach (var cell in PrintTemplateLayout.For(units).Cells.Where(c => c.Field != null))
        {
            Assert.NotEmpty(PrintTemplateLayout.SampleValue(cell.Field!.Value));
            Assert.Equal(0, PrintTemplateLayout.SampleOverflowMm(cell));
        }
    }

    [Fact]
    public void SampleOverflowSeesAValueTooWideForItsCell()
    {
        var narrow = PrintTemplateLayout.Metric.Cells.Single(c => c.Name == "Description") with { WidthMm = 20 };
        Assert.True(PrintTemplateLayout.SampleOverflowMm(narrow) > 5);
    }
}
