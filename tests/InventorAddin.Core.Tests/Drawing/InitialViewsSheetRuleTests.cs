using InventorAddin.Core.Drawing;

namespace InventorAddin.Core.Tests.Drawing;

public sealed class InitialViewsSheetRuleTests
{
    private static readonly InitialViewsBoxCm Small = new(5, 3, 2);
    private static readonly InitialViewsBoxCm Large = new(120, 80, 60);
    private static readonly InitialViewsBoxCm Mid = new(20, 12, 8);

    private static InitialViewsResult PlanOn(SheetSizeSpec sheet, InitialViewsBoxCm box, double? scale = null) =>
        InitialViewsPlan.Plan(InitialViewModelKind.Part, box, null, InitialViewsPlan.UsableArea(sheet), null, scale);

    [Fact]
    public void Sheets_AreSmallestFirst_AndHoldTheTitleBlock()
    {
        var metric = InitialViewsSheetRule.SheetsFor(PrintUnits.Metric);
        Assert.All(metric, s => Assert.True(s.TitleBlockFits));
        Assert.Equal(metric.OrderBy(s => s.WidthMm * s.HeightMm).Select(s => s.Name), metric.Select(s => s.Name));
        Assert.Equal("A", InitialViewsSheetRule.SheetsFor(PrintUnits.Imperial)[0].Name);
        Assert.Equal("E", InitialViewsSheetRule.SheetsFor(PrintUnits.Imperial)[^1].Name);
    }

    [Fact]
    public void SmallPart_GetsTheSmallestSheet()
    {
        var sheets = InitialViewsSheetRule.SheetsFor(PrintUnits.Metric);
        var chosen = InitialViewsSheetRule.Smallest(sheets, s => PlanOn(s, Small), suggestedScale: true);
        Assert.Equal(sheets[0].Name, chosen.Name);
    }

    [Fact]
    public void LargePart_GetsALargerSheet_WhereTheScaleIsAtLeastHalf()
    {
        var sheets = InitialViewsSheetRule.SheetsFor(PrintUnits.Metric);
        var chosen = InitialViewsSheetRule.Smallest(sheets, s => PlanOn(s, Large), suggestedScale: true);
        Assert.NotEqual(sheets[0].Name, chosen.Name);
        Assert.True(PlanOn(chosen, Large).Scale >= InitialViewsSheetRule.MinimumSuggestedScale || chosen == sheets[^1]);
        int index = sheets.ToList().FindIndex(s => s.Name == chosen.Name);
        Assert.True(index > 0);
        Assert.True(PlanOn(sheets[index - 1], Large).Scale < InitialViewsSheetRule.MinimumSuggestedScale);
    }

    [Fact]
    public void FixedScale_PicksTheSmallestSheetWhereItFits()
    {
        var sheets = InitialViewsSheetRule.SheetsFor(PrintUnits.Metric);
        var chosen = InitialViewsSheetRule.Smallest(sheets, s => PlanOn(s, Mid, 1.0), suggestedScale: false);
        Assert.True(PlanOn(chosen, Mid, 1.0).Fits);
        int index = sheets.ToList().FindIndex(s => s.Name == chosen.Name);
        if (index > 0)
            Assert.False(PlanOn(sheets[index - 1], Mid, 1.0).Fits);
    }

    [Fact]
    public void NothingFits_GetsTheLargestSheet()
    {
        var sheets = InitialViewsSheetRule.SheetsFor(PrintUnits.Metric);
        var huge = new InitialViewsBoxCm(5000, 4000, 3000);
        Assert.Equal(sheets[^1].Name, InitialViewsSheetRule.Smallest(sheets, s => PlanOn(s, huge, 1.0), false).Name);
    }

    [Fact]
    public void NextThatFits_OffersTheNextSheetUp_OrNullWhenFitting()
    {
        var sheets = InitialViewsSheetRule.SheetsFor(PrintUnits.Metric);
        var a3 = sheets.Single(s => s.Name == "A3");
        var next = InitialViewsSheetRule.NextThatFits(sheets, a3, s => PlanOn(s, Mid, 1.0));
        Assert.NotNull(next);
        Assert.True(PlanOn(next!, Mid, 1.0).Fits);
        Assert.True(next!.WidthMm * next.HeightMm > a3.WidthMm * a3.HeightMm);

        Assert.Null(InitialViewsSheetRule.NextThatFits(sheets, a3, s => PlanOn(s, Small, 1.0)));
    }
}
