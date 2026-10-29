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
    public void ForeignButtons_AreAwbHoleOnTheModifyPanel()
    {
        ForeignButton button = Assert.Single(AddinUi.ForeignButtons);
        Assert.Equal(HoleTakeoverRules.RibbonName, button.RibbonName);
        Assert.Equal(HoleTakeoverRules.TabId, button.TabId);
        Assert.Equal(HoleTakeoverRules.PanelId, button.PanelId);
        Assert.Equal(CommandNames.AwbHole, button.CommandInternalName);
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
}
