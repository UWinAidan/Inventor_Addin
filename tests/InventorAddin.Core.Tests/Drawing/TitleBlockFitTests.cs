using System.Globalization;
using InventorAddin.Core.Drawing;

namespace InventorAddin.Core.Tests.Drawing;

/// <summary>Task 238: long title block values narrow to 0.6, shrink to 1.8 mm, and Description wraps onto two lines; all invented values.</summary>
public sealed class TitleBlockFitTests
{
    private const string LongDescription =
        "BRACKET, MOUNTING, LEFT HAND, WITH TWO COUNTERBORED HOLES AND A SLOT FOR THE SET SCREW, ZINC PLATED STEEL, INVENTED";

    private static TitleBlockCell Cell(string name) =>
        PrintTemplateLayout.Metric.Cells.Single(c => c.Name == name);

    private static string Long(int words) => string.Join(" ", Enumerable.Repeat("ALPHA", words));

    [Fact]
    public void AShortValueIsLeftAlone()
    {
        var cell = Cell("Finish");
        var r = TitleBlockFit.Fit(cell, new[] { "Zinc plated" });
        Assert.Equal(cell.ValueSizeMm, r.SizeMm);
        Assert.Equal(1, r.WidthScale);
        Assert.Null(r.Warning);
        Assert.Null(r.BoxWidthMm);
        Assert.Equal(PrintTextFit.UpperOriginYMm(cell.ValueBaselineYMm, cell.ValueSizeMm), r.OriginYMm, 6);
    }

    [Fact]
    public void NoValuesGivesTheCellsOwnSizeAndOrigin()
    {
        var cell = Cell("Description");
        var r = TitleBlockFit.Fit(cell, new[] { "", "  " });
        Assert.Equal(4, r.SizeMm);
        Assert.Equal(1, r.WidthScale);
        Assert.Equal(1, r.Lines);
    }

    [Fact]
    public void ALongValueNarrowsAtTheCellsSizeFirst()
    {
        var cell = Cell("Finish");
        double avail = TitleBlockFit.AvailableWidthMm(cell);
        string text = "ZINC PLATED, CLEAR CHROMATE, 0.0003 IN";
        double w = PrintTextFit.WidthMm(text, cell.ValueSizeMm);
        Assert.InRange(w / avail, 1.05, 1.66);
        var r = TitleBlockFit.Fit(cell, new[] { text });
        Assert.Equal(2.8, r.SizeMm);
        Assert.InRange(r.WidthScale, 0.6, 0.99);
        Assert.True(w * r.WidthScale <= avail + 1e-9);
        Assert.Null(r.Warning);
    }

    [Fact]
    public void BelowTheFloorWidthTheTextGetsSmallerAtPointSix()
    {
        var cell = Cell("Finish");
        double avail = TitleBlockFit.AvailableWidthMm(cell);
        string text = "ZINC PLATED, CLEAR CHROMATE, 0.0003 IN MINIMUM, BAKED AFTER";
        Assert.True(PrintTextFit.WidthMm(text, 2.8) * 0.6 > avail);
        var r = TitleBlockFit.Fit(cell, new[] { text });
        Assert.InRange(r.SizeMm, 1.8, 2.8 - 0.05 + 1e-9);
        Assert.InRange(r.WidthScale, 0.6, 1);
        Assert.True(PrintTextFit.WidthMm(text, r.SizeMm) * r.WidthScale <= avail + 1e-9);
        // The next step up would not have fitted at 0.6.
        Assert.True(PrintTextFit.WidthMm(text, r.SizeMm + 0.05) * 0.6 > avail);
        Assert.Null(r.Warning);
        Assert.Equal(PrintTextFit.UpperOriginYMm(cell.ValueBaselineYMm, r.SizeMm), r.OriginYMm, 6);
        Assert.Null(r.BoxWidthMm);
        Assert.Equal(1, r.Lines);
    }

    [Fact]
    public void ASingleLineFieldNeverWrapsAndWarnsAtTheFloor()
    {
        var cell = Cell("Finish");
        var r = TitleBlockFit.Fit(cell, new[] { Long(20) });
        Assert.Equal(1.8, r.SizeMm);
        Assert.Equal(0.6, r.WidthScale);
        Assert.Equal(1, r.Lines);
        Assert.Null(r.BoxWidthMm);
        Assert.NotNull(r.Warning);
        Assert.Contains("FINISH", r.Warning);
        Assert.Contains("mm", r.Warning);
        Assert.Contains("%", r.Warning);
    }

    [Fact]
    public void TheWarningNamesHowMuchTooLong()
    {
        var cell = Cell("Material");
        string text = Long(20);
        var r = TitleBlockFit.Fit(cell, new[] { text });
        double over = PrintTextFit.WidthMm(text, 1.8) * 0.6 - TitleBlockFit.AvailableWidthMm(cell);
        Assert.Contains(over.ToString("0.#", CultureInfo.InvariantCulture) + " mm", r.Warning);
        Assert.Contains("MATERIAL", r.Warning);
    }

    [Fact]
    public void ADescriptionThatNarrowsToPointSixStaysOnOneLine()
    {
        var cell = Cell("Description");
        double avail = TitleBlockFit.AvailableWidthMm(cell);
        Assert.Equal(112, avail, 6);
        string text = "BRACKET, MOUNTING, LEFT HAND, WITH TWO COUNTERBORED HOLES";
        double w = PrintTextFit.WidthMm(text, 4);
        Assert.InRange(w / avail, 1.0, 1.6);
        var r = TitleBlockFit.Fit(cell, new[] { text });
        Assert.Equal(4, r.SizeMm);
        Assert.Equal(1, r.Lines);
        Assert.Null(r.BoxWidthMm);
    }

    [Fact]
    public void ADescriptionTooLongForOneLineWrapsOntoTwoAtThreePointFour()
    {
        var cell = Cell("Description");
        double avail = TitleBlockFit.AvailableWidthMm(cell);
        Assert.True(PrintTextFit.WidthMm(LongDescription, 4) * 0.6 > avail);
        var r = TitleBlockFit.Fit(cell, new[] { LongDescription });
        Assert.Equal(3.4, r.SizeMm);
        Assert.Equal(2, r.Lines);
        Assert.Equal(avail, r.BoxWidthMm);
        Assert.Null(r.Warning);
        Assert.InRange(r.WidthScale, 0.6, 1);
        Assert.True(PrintTextFit.Wrap(LongDescription, 3.4, avail / r.WidthScale).Count <= 2);
        // Line 1's capital tops sit 1.3 mm below the label's baseline: 46.3 in the metric block (spike 241).
        Assert.Equal(46.3, r.OriginYMm, 6);
    }

    [Fact]
    public void TwoLinesAreNarrowedAsLittleAsPossible()
    {
        var cell = Cell("Description");
        double avail = TitleBlockFit.AvailableWidthMm(cell);
        var r = TitleBlockFit.Fit(cell, new[] { LongDescription });
        if (r.WidthScale < 1)
            Assert.True(PrintTextFit.Wrap(LongDescription, 3.4, avail / Math.Min(1, r.WidthScale + 0.01)).Count > 2);
    }

    [Fact]
    public void ADescriptionTooLongForTwoLinesAtThreePointFourShrinksAtPointSix()
    {
        var cell = Cell("Description");
        double avail = TitleBlockFit.AvailableWidthMm(cell);
        string text = Long(55);
        Assert.False(PrintTextFit.Wrap(text, 3.4, avail / 0.6).Count <= 2);
        var r = TitleBlockFit.Fit(cell, new[] { text });
        Assert.Equal(0.6, r.WidthScale);
        Assert.InRange(r.SizeMm, 1.8, 3.35 + 1e-9);
        Assert.True(PrintTextFit.Wrap(text, r.SizeMm, avail / 0.6).Count <= 2);
        Assert.False(PrintTextFit.Wrap(text, r.SizeMm + 0.05, avail / 0.6).Count <= 2);
        Assert.Null(r.Warning);
        Assert.Equal(2, r.Lines);
        Assert.Equal(avail, r.BoxWidthMm);
    }

    [Fact]
    public void ADescriptionTooLongForTwoLinesAtTheFloorWarns()
    {
        var cell = Cell("Description");
        var r = TitleBlockFit.Fit(cell, new[] { Long(120) });
        Assert.Equal(1.8, r.SizeMm);
        Assert.Equal(0.6, r.WidthScale);
        Assert.NotNull(r.Warning);
        Assert.Contains("DESCRIPTION", r.Warning);
        Assert.Contains("%", r.Warning);
        Assert.Equal(2, r.Lines);
    }

    [Fact]
    public void OneSizeFitsEverySheetsValue()
    {
        var cell = Cell("Finish");
        double avail = TitleBlockFit.AvailableWidthMm(cell);
        const string shortOne = "Zinc plated";
        const string longOne = "ZINC PLATED, CLEAR CHROMATE, 0.0003 IN MINIMUM, BAKED AFTER";
        var both = TitleBlockFit.Fit(cell, new[] { shortOne, longOne });
        var alone = TitleBlockFit.Fit(cell, new[] { longOne });
        Assert.Equal(alone.SizeMm, both.SizeMm);
        Assert.Equal(alone.WidthScale, both.WidthScale);
        Assert.True(PrintTextFit.WidthMm(shortOne, both.SizeMm) * both.WidthScale <= avail);
    }

    [Fact]
    public void ADescriptionTwoLineOnOneSheetStillHoldsAShortOneOnAnother()
    {
        var cell = Cell("Description");
        var r = TitleBlockFit.Fit(cell, new[] { "PIN, PIVOT", LongDescription });
        Assert.Equal(2, r.Lines);
        Assert.Equal(3.4, r.SizeMm);
    }

    [Fact]
    public void ABoldValueIsMeasuredBold()
    {
        var cell = Cell("PartNumber");
        Assert.True(cell.Bold);
        string text = "AWB-123456789012345678901234567890";
        double avail = TitleBlockFit.AvailableWidthMm(cell);
        var r = TitleBlockFit.Fit(cell, new[] { text });
        Assert.True(PrintTextFit.WidthMm(text, r.SizeMm, true) * r.WidthScale <= avail + 1e-9);
    }

    [Fact]
    public void TheInlineFieldsAreMeasuredFromWhereTheirValueStarts()
    {
        var cell = Cell("Project");
        Assert.Equal(cell.RightMm - cell.ValueXMm - 1.5, TitleBlockFit.AvailableWidthMm(cell), 6);
    }

    [Fact]
    public void EveryFittedCellHasRoomForItsSampleValue()
    {
        foreach (var cell in PrintTemplateLayout.Metric.Cells.Where(TitleBlockFit.IsFitted))
        {
            var r = TitleBlockFit.Fit(cell, new[] { PrintTemplateLayout.SampleValue(cell.Field!.Value) });
            Assert.True(r.WidthScale == 1 && r.SizeMm == cell.ValueSizeMm && r.Warning == null, cell.Name);
        }
    }

    [Fact]
    public void TheCentredRevisionIsNotFitted() =>
        Assert.False(TitleBlockFit.IsFitted(Cell("Revision")));

    [Fact]
    public void BothTemplatesShareTheFittedCellGeometry()
    {
        foreach (var cell in PrintTemplateLayout.Metric.Cells.Where(TitleBlockFit.IsFitted))
        {
            var other = PrintTemplateLayout.Imperial.Cells.Single(c => c.Name == cell.Name);
            Assert.Equal(TitleBlockFit.AvailableWidthMm(cell), TitleBlockFit.AvailableWidthMm(other), 6);
            Assert.Equal(cell.ValueBaselineYMm, other.ValueBaselineYMm, 6);
            Assert.Equal(cell.LabelBaselineYMm, other.LabelBaselineYMm, 6);
        }
    }

    [Theory]
    [InlineData("<Property Document='model' PropertySet='x' Property='Description' FormatID='f' PropertyID='29'>L</Property>", PrintField.Description)]
    [InlineData("<StyleOverride FontSize='0.2'><Property Document='drawing' PropertySet='x' Property='Finish Spec' FormatID='f'>L</Property></StyleOverride>", PrintField.FinishSpec)]
    [InlineData("<Property Document='model' PropertySet='x' Property='Material Size' FormatID='f'>L</Property>", PrintField.MaterialSize)]
    [InlineData("<Property Document='drawing' PropertySet='x' Property='Author' FormatID='f' PropertyID='4'>L</Property>", PrintField.DrawingAuthor)]
    [InlineData("<Property Document='model' PropertySet='x' Property='Mass' FormatID='f'>L</Property>", PrintField.Mass)]
    [InlineData("<DerivedProperty DerivedID='29697'>FILE</DerivedProperty>", PrintField.FileName)]
    [InlineData("<DerivedProperty DerivedID='29704'>SHEET</DerivedProperty> OF <DerivedProperty DerivedID='29703'>N</DerivedProperty>", PrintField.SheetNumberOfCount)]
    public void FieldOfReadsTheProperty(string formatted, PrintField expected) =>
        Assert.Equal(expected, TitleBlockFit.FieldOf(formatted));

    [Theory]
    [InlineData("MM")]
    [InlineData("<StyleOverride FontSize='0.2'>IN</StyleOverride>")]
    [InlineData("")]
    [InlineData(null)]
    public void FieldOfIsNullForPlainText(string? formatted) =>
        Assert.Null(TitleBlockFit.FieldOf(formatted));

    [Fact]
    public void FontSizeIsTheCapHeightInCentimetres()
    {
        // The template's 2.8 mm values are FontSize 0.2036 and Description's 4 mm is 0.2908 (spike 241).
        Assert.Equal(0.2036, TitleBlockFit.FontSizeCm(2.8), 4);
        Assert.Equal(0.2908, TitleBlockFit.FontSizeCm(4), 4);
        Assert.Equal(0.13086, TitleBlockFit.FontSizeCm(1.8), 5);
    }

    [Fact]
    public void SizeMmOfReadsTheFirstFontSize()
    {
        Assert.Equal(2.8, TitleBlockFit.SizeMmOf("<StyleOverride Font='Tahoma' FontSize='0.2036'><Property Property='Material'>M</Property></StyleOverride>")!.Value, 2);
        Assert.Null(TitleBlockFit.SizeMmOf("<StyleOverride Font='Tahoma'>x</StyleOverride>"));
        Assert.Null(TitleBlockFit.SizeMmOf(""));
    }

    [Fact]
    public void WithSizeReplacesEveryFontSizeAndKeepsTheRest()
    {
        const string text = "<StyleOverride Font='Tahoma' FontSize='0.2036' Bold='True'><Property Property='Material'>M</Property></StyleOverride>";
        string result = TitleBlockFit.WithSizeMm(text, 1.8);
        Assert.Equal("<StyleOverride Font='Tahoma' FontSize='0.13086' Bold='True'><Property Property='Material'>M</Property></StyleOverride>", result);
        Assert.Equal(1.8, TitleBlockFit.SizeMmOf(result)!.Value, 3);

        const string twoRuns = "<StyleOverride FontSize='0.2'>A</StyleOverride><Br/><StyleOverride FontSize=\"0.2\">B</StyleOverride>";
        string both = TitleBlockFit.WithSizeMm(twoRuns, 2.8);
        Assert.Equal(2, both.Split("0.20356").Length - 1);
    }

    [Fact]
    public void WithSizeAddsASizeWhereThereIsNone()
    {
        Assert.Equal("<StyleOverride FontSize='0.20356' Font='Tahoma'>x</StyleOverride>",
            TitleBlockFit.WithSizeMm("<StyleOverride Font='Tahoma'>x</StyleOverride>", 2.8));
        Assert.Equal("<StyleOverride FontSize='0.20356'>plain</StyleOverride>", TitleBlockFit.WithSizeMm("plain", 2.8));
    }

    [Fact]
    public void NeedsChangeIgnoresInventorsRounding()
    {
        var cell = Cell("Material");
        var target = TitleBlockFit.Fit(cell, new[] { "Steel" });
        Assert.False(TitleBlockFit.NeedsChange(target, 2.8004, 1, target.OriginYMm + 0.005, null));
        Assert.True(TitleBlockFit.NeedsChange(target, 2.6, 1, target.OriginYMm, null));
        Assert.True(TitleBlockFit.NeedsChange(target, 2.8, 0.8, target.OriginYMm, null));
        Assert.True(TitleBlockFit.NeedsChange(target, 2.8, 1, target.OriginYMm + 0.5, null));
        Assert.True(TitleBlockFit.NeedsChange(target, null, 1, target.OriginYMm, null));
    }

    [Fact]
    public void AWrappingBoxNeedsItsWidthButAFittedOneIsNotWidened()
    {
        var cell = Cell("Description");
        var wrapped = TitleBlockFit.Fit(cell, new[] { Long(55) });
        Assert.NotNull(wrapped.BoxWidthMm);
        Assert.True(TitleBlockFit.NeedsChange(wrapped, wrapped.SizeMm, wrapped.WidthScale, wrapped.OriginYMm, 30.9));
        Assert.False(TitleBlockFit.NeedsChange(wrapped, wrapped.SizeMm, wrapped.WidthScale, wrapped.OriginYMm, wrapped.BoxWidthMm));

        var single = TitleBlockFit.Fit(cell, new[] { "PIN, PIVOT" });
        Assert.Null(single.BoxWidthMm);
        Assert.False(TitleBlockFit.NeedsChange(single, 4, 1, single.OriginYMm, 112));
    }
}
