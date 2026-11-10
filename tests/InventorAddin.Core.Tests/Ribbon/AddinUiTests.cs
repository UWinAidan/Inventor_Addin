using InventorAddin.Core.Holes;
using InventorAddin.Core.Ribbon;

namespace InventorAddin.Core.Tests.Ribbon;

public sealed class AddinUiTests
{
    [Fact]
    public void TabRibbonNames_AreEveryEnvironment_WhateverTheLayoutShows()
    {
        Assert.Equal(new[] { "ZeroDoc", "Part", "Assembly", "Drawing" }, AddinUi.TabRibbonNames);
    }

    [Fact]
    public void ForeignButtons_AreAwbHoleOnTheModifyPanelAndTheSheetMetalTab()
    {
        Assert.Equal(2, AddinUi.ForeignButtons.Count);

        ForeignButton model = AddinUi.ForeignButtons[0];
        Assert.Equal(HoleTakeoverRules.RibbonName, model.RibbonName);
        Assert.Equal(HoleTakeoverRules.TabId, model.TabId);
        Assert.Equal(HoleTakeoverRules.PanelId, model.PanelId);
        Assert.Equal(CommandNames.AwbHole, model.CommandInternalName);

        // Task 128: the Sheet Metal tab's panel is looked up at run time, so every panel on the tab is searched.
        ForeignButton sheetMetal = AddinUi.ForeignButtons[1];
        Assert.Equal(HoleTakeoverRules.RibbonName, sheetMetal.RibbonName);
        Assert.Equal(HoleTakeoverRules.SheetMetalTabId, sheetMetal.TabId);
        Assert.Null(sheetMetal.PanelId);
        Assert.Equal(CommandNames.AwbHole, sheetMetal.CommandInternalName);
    }

    [Fact]
    public void Removal_Loading_Nothing_SaysBuiltFromScratch()
    {
        var report = new AddinUiRemovalReport(AddinUiOccasion.Loading);

        Assert.Equal(
            "Ribbon (add-in loading): nothing was left from an earlier load; the ribbon is built from scratch.",
            report.Summary());
        Assert.Empty(report.FailureMessages());
    }

    [Fact]
    public void Removal_Loading_Leftovers_CountsThem()
    {
        var report = new AddinUiRemovalReport(AddinUiOccasion.Loading);
        report.TabRemoved();
        report.ButtonRemoved();

        Assert.Equal(
            "Ribbon (add-in loading): removed what an earlier load left (1 tab, and 1 button on Inventor's panels) " +
            "before building the ribbon.",
            report.Summary());
    }

    [Fact]
    public void Removal_Unloading_CountsWhatWasRemoved()
    {
        var report = new AddinUiRemovalReport(AddinUiOccasion.Unloading);
        for (int i = 0; i < 4; i++)
            report.TabRemoved();
        report.ButtonRemoved();

        Assert.Equal(4, report.TabsRemoved);
        Assert.Equal(1, report.ButtonsRemoved);
        Assert.Equal(
            "Ribbon (add-in unloading): removed 4 tabs, and 1 button on Inventor's panels.",
            report.Summary());
    }

    [Fact]
    public void Removal_Unloading_OnlyButtons()
    {
        var report = new AddinUiRemovalReport(AddinUiOccasion.Unloading);
        report.ButtonRemoved();

        Assert.Equal(
            "Ribbon (add-in unloading): removed 0 tabs, and 1 button on Inventor's panels.",
            report.Summary());
    }

    [Fact]
    public void Removal_Unloading_Nothing()
    {
        var report = new AddinUiRemovalReport(AddinUiOccasion.Unloading);

        Assert.Equal("Ribbon (add-in unloading): there was nothing to remove.", report.Summary());
    }

    [Theory]
    [InlineData(AddinUiOccasion.Loading,
        "Ribbon (add-in loading): could not remove the tab on the Part ribbon: Unspecified error. " +
        "The ribbon is built on what is there; if a button is missing or shown twice, restart Inventor.")]
    [InlineData(AddinUiOccasion.Unloading,
        "Ribbon (add-in unloading): could not remove the tab on the Part ribbon: Unspecified error. " +
        "It stays until Inventor restarts.")]
    public void Removal_Failures_AreOneWarningEach(AddinUiOccasion occasion, string expected)
    {
        var report = new AddinUiRemovalReport(occasion);
        report.Failed("the tab on the Part ribbon", "Unspecified error");

        Assert.Equal(expected, Assert.Single(report.FailureMessages()));
        Assert.Single(report.Failures);
    }

    [Fact]
    public void Removal_TabNotFound_IsOneInfoLineNamingTheId_NotAFailure()
    {
        // Task 128: the Sheet Metal tab's id is not confirmed; a wrong one must not read as a failure.
        var report = new AddinUiRemovalReport(AddinUiOccasion.Unloading);
        report.TabNotFound("Part", "id_TabSheetMetal");
        report.TabNotFound("Part", "id_TabSheetMetal");

        Assert.Equal(
            "Ribbon (add-in unloading): tab id_TabSheetMetal was not found on the Part ribbon, so there is nothing to " +
            "remove there. If AWB Hole shows on that tab, its id is wrong.",
            Assert.Single(report.NotFoundMessages()));
        Assert.Equal(new[] { "Part: id_TabSheetMetal" }, report.TabsNotFound);
        Assert.Empty(report.Failures);
        Assert.Empty(report.FailureMessages());
        Assert.Equal("Ribbon (add-in unloading): there was nothing to remove.", report.Summary());
    }

    [Fact]
    public void Build_Clean_SaysWhatWasAdded()
    {
        var report = new RibbonBuildReport { CommandsRegistered = 13 };
        report.Tab("ZeroDoc", added: true);
        report.Tab("Part", added: true);
        report.Panel(added: true);
        report.Panel(added: true);
        report.Button(added: true);

        Assert.Equal(
            "Ribbon (add-in loading): 13 commands registered; the tab added on ZeroDoc, Part; 2 panels and 1 button added.",
            report.Summary());
    }

    [Fact]
    public void Build_WithFailuresAndReusedItems_SaysSo()
    {
        var report = new RibbonBuildReport { CommandsRegistered = 12, CommandsFailed = 1 };
        report.Tab("Part", added: false);
        report.Panel(added: false);
        report.Button(added: false);
        report.Button(added: false);

        Assert.Equal(
            "Ribbon (add-in loading): 12 commands registered and 1 failed; no tab added; 0 panels and 0 buttons added. " +
            "Already there and reused: the tab on Part, 1 panel and 2 buttons.",
            report.Summary());
        Assert.Equal(new[] { "Part" }, report.TabsFound);
    }

    // ---- Finding the split button (task 129) ----

    [Theory]
    [InlineData(CommandNames.AwbHole, null)]
    [InlineData(null, CommandNames.AwbHole)]
    [InlineData("SomeSplitName", CommandNames.AwbHole)]
    public void IsControlOf_ByNameOrTopCommand(string? controlName, string? displayed) =>
        Assert.True(AddinUi.IsControlOf(CommandNames.AwbHole, controlName, displayed, []));

    [Fact]
    public void IsControlOf_ByADropdownEntry() =>
        Assert.True(AddinUi.IsControlOf(
            CommandNames.AwbHole, "SomeSplitName", null, [null, CommandNames.InventorHole, CommandNames.AwbHole]));

    [Fact]
    public void IsControlOf_InventorsOwnHole_IsNotOurs() =>
        Assert.False(AddinUi.IsControlOf(CommandNames.AwbHole, "PartHoleCmd", "PartHoleCmd", ["PartHoleCmd"]));

    [Fact]
    public void IsControlOf_OtherAwbCommandOnly_IsNotAwbHole() =>
        Assert.False(AddinUi.IsControlOf(CommandNames.AwbHole, null, null, [CommandNames.InventorHole]));

    [Fact]
    public void IsControlOf_NamesMatchExactly() =>
        Assert.False(AddinUi.IsControlOf(CommandNames.AwbHole, "awb_awbhole", null, []));

    [Fact]
    public void ForeignButton_Matches_UsesItsCommand()
    {
        ForeignButton model = AddinUi.ForeignButtons[0];

        Assert.True(model.Matches(null, CommandNames.AwbHole, []));
        Assert.False(model.Matches(null, null, []));
    }
}
