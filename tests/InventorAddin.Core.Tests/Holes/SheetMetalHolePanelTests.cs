using InventorAddin.Core.Holes;
using InventorAddin.Core.Ribbon;

namespace InventorAddin.Core.Tests.Holes;

/// <summary>Finding the Sheet Metal tab's panel that holds Inventor's Hole, by command (task 128).</summary>
public sealed class SheetMetalHolePanelTests
{
    private const string Hole = HoleTakeoverRules.InventorHoleCommand;
    private const string FlatPatternTab = "id_TabFlatPattern";

    private static RibbonPanelControls Panel(string tab, string panel, params string?[] controls) =>
        new(tab, null, panel, controls);

    [Fact]
    public void Find_ChoosesTheSheetMetalPanelHoldingInventorsHole()
    {
        SheetMetalHolePanelChoice choice = SheetMetalHolePanel.Find(
        [
            Panel(HoleTakeoverRules.TabId, HoleTakeoverRules.PanelId, "PartExtrudeCmd", Hole),
            Panel(HoleTakeoverRules.SheetMetalTabId, "id_SomeCreatePanel", "SomeFlangeCmd"),
            Panel(HoleTakeoverRules.SheetMetalTabId, "id_SomeModifyPanel", "SomeCutCmd", Hole),
            Panel(FlatPatternTab, "id_FlatModify", Hole),
        ]);

        Assert.Equal("id_SomeModifyPanel", choice.PanelId);
        Assert.False(choice.IsWarning);
        Assert.Contains("goes before PartHoleCmd on panel id_SomeModifyPanel", choice.Message);
    }

    [Fact]
    public void Find_NeverChoosesFlatPatternOrThe3DModelTab()
    {
        SheetMetalHolePanelChoice choice = SheetMetalHolePanel.Find(
        [
            Panel(HoleTakeoverRules.TabId, HoleTakeoverRules.PanelId, Hole),
            Panel(FlatPatternTab, "id_FlatModify", Hole),
        ]);

        Assert.Null(choice.PanelId);
        Assert.True(choice.IsWarning);
        Assert.Contains("left as it is", choice.Message);
    }

    [Fact]
    public void Find_SeveralPanels_PrefersTheOneSpike081Saw()
    {
        SheetMetalHolePanelChoice choice = SheetMetalHolePanel.Find(
        [
            Panel(HoleTakeoverRules.SheetMetalTabId, "id_Other", Hole),
            Panel(HoleTakeoverRules.SheetMetalTabId, HoleTakeoverRules.SheetMetalPanelId, Hole),
        ]);

        Assert.Equal(HoleTakeoverRules.SheetMetalPanelId, choice.PanelId);
    }

    [Fact]
    public void Find_MatchesTheCommandExactly_AfterTrimming()
    {
        Assert.Equal("p", SheetMetalHolePanel.Find([Panel(HoleTakeoverRules.SheetMetalTabId, "p", " PartHoleCmd ")]).PanelId);
        Assert.Null(SheetMetalHolePanel.Find([Panel(HoleTakeoverRules.SheetMetalTabId, "p", "parthOLEcmd")]).PanelId);
        Assert.Null(SheetMetalHolePanel.Find([Panel(HoleTakeoverRules.SheetMetalTabId, "p", null, "")]).PanelId);
    }

    [Fact]
    public void Find_AnotherHoleCommandOnTheSheetMetalTab_Warns()
    {
        SheetMetalHolePanelChoice choice = SheetMetalHolePanel.Find(
        [
            Panel(HoleTakeoverRules.SheetMetalTabId, "p", "SheetMetalHoleCmd"),
        ]);

        Assert.Null(choice.PanelId);
        Assert.True(choice.IsWarning);
        Assert.Contains("also has SheetMetalHoleCmd, which is not taken over", choice.Message);
    }

    [Fact]
    public void Find_OurOwnHoleButton_IsNotAnotherHoleCommand()
    {
        // After a takeover AWB Hole sits beside Inventor's on the panel; re-applying must not warn about it.
        SheetMetalHolePanelChoice choice = SheetMetalHolePanel.Find(
        [
            Panel(HoleTakeoverRules.SheetMetalTabId, "p", CommandNames.AwbHole, Hole),
            Panel(RibbonIds.TabId, "ours", CommandNames.AwbHole),
        ]);

        Assert.Equal("p", choice.PanelId);
        Assert.False(choice.IsWarning);
        Assert.DoesNotContain(CommandNames.AwbHole, choice.Message);
    }

    [Fact]
    public void Find_Message_ListsEveryPanelWithAHoleCommand()
    {
        SheetMetalHolePanelChoice choice = SheetMetalHolePanel.Find(
        [
            new RibbonPanelControls(HoleTakeoverRules.TabId, "3D Model", HoleTakeoverRules.PanelId, ["PartExtrudeCmd", Hole]),
            Panel(HoleTakeoverRules.SheetMetalTabId, "p", "SomeCutCmd"),
            Panel(FlatPatternTab, "id_FlatModify", Hole),
        ]);

        Assert.Contains($"{HoleTakeoverRules.TabId} (3D Model) / {HoleTakeoverRules.PanelId}: PartHoleCmd", choice.Message);
        Assert.Contains($"{FlatPatternTab} / id_FlatModify: PartHoleCmd", choice.Message);
        Assert.DoesNotContain("SomeCutCmd", choice.Message);
    }

    [Fact]
    public void Find_NoPanels_SaysNone()
    {
        SheetMetalHolePanelChoice choice = SheetMetalHolePanel.Find([]);

        Assert.Null(choice.PanelId);
        Assert.Contains("ribbon: none.", choice.Message);
        Assert.Throws<ArgumentNullException>(() => SheetMetalHolePanel.Find(null!));
    }

    [Theory]
    [InlineData(HoleTakeoverRules.SheetMetalTabId, true)]
    [InlineData(" " + HoleTakeoverRules.SheetMetalTabId + " ", true)]
    [InlineData(FlatPatternTab, false)]
    [InlineData(HoleTakeoverRules.TabId, false)]
    [InlineData(null, false)]
    public void IsSheetMetalTab(string? tabId, bool expected) =>
        Assert.Equal(expected, SheetMetalHolePanel.IsSheetMetalTab(tabId));

    [Fact]
    public void HoleCommands_KeepsInventorsHoleNamesOnly()
    {
        Assert.Equal(
            new[] { "PartHoleCmd", "SheetMetalHoleCmd" },
            SheetMetalHolePanel.HoleCommands(["PartExtrudeCmd", " PartHoleCmd ", null, "SheetMetalHoleCmd", CommandNames.AwbHole]));
    }
}
