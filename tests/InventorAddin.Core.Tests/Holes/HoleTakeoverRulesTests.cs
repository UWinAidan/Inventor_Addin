using InventorAddin.Core.Holes;
using InventorAddin.Core.Settings;
using InventorAddin.Core.ViewModels;

namespace InventorAddin.Core.Tests.Holes;

/// <summary>Who gets the Hole button and H from the setting, and what giving them back sets (task 091).</summary>
public sealed class HoleTakeoverRulesTests
{
    [Fact]
    public void OwnerFor_DefaultSettings_IsAwb() =>
        Assert.Equal(HoleCommandOwner.Awb, HoleTakeoverRules.OwnerFor(new AddinSettings()));

    [Fact]
    public void OwnerFor_UseInventorHoleCommand_IsInventor() =>
        Assert.Equal(
            HoleCommandOwner.Inventor,
            HoleTakeoverRules.OwnerFor(new AddinSettings { UseInventorHoleCommand = true }));

    [Fact]
    public void OwnerFor_NullSettings_Throws() =>
        Assert.Throws<ArgumentNullException>(() => HoleTakeoverRules.OwnerFor(null!));

    [Fact]
    public void TakenOver_ShowsOursHidesInventorsAndMovesH()
    {
        HoleTakeoverState state = HoleTakeoverRules.TakenOver;

        Assert.Equal(HoleCommandOwner.Awb, state.Owner);
        Assert.True(state.AwbButtonVisible);
        Assert.False(state.InventorButtonVisible);
        Assert.Equal("H", state.AwbShortcut);
        Assert.Equal(string.Empty, state.InventorShortcut);
    }

    [Theory]
    [InlineData("", "H", "H")]
    [InlineData(null, "H", "H")]
    [InlineData("  ", "H", "H")]
    [InlineData("", "J", "J")]
    public void GivenBack_EmptyOverride_GetsDefaultShortcut(string? current, string? defaultShortcut, string expected)
    {
        // Spike 081 round 1: an empty override left H doing nothing, so the default is always put back.
        HoleTakeoverState state = HoleTakeoverRules.GivenBack(current, defaultShortcut);

        Assert.Equal(HoleCommandOwner.Inventor, state.Owner);
        Assert.False(state.AwbButtonVisible);
        Assert.True(state.InventorButtonVisible);
        Assert.Equal(string.Empty, state.AwbShortcut);
        Assert.Equal(expected, state.InventorShortcut);
    }

    [Theory]
    [InlineData("", null)]
    [InlineData("", "")]
    [InlineData(null, "   ")]
    public void GivenBack_UnreadableDefault_FallsBackToH(string? current, string? defaultShortcut) =>
        Assert.Equal("H", HoleTakeoverRules.GivenBack(current, defaultShortcut).InventorShortcut);

    [Fact]
    public void GivenBack_UsersOwnOverride_IsLeftAlone() =>
        Assert.Null(HoleTakeoverRules.GivenBack("Shift+H", "H").InventorShortcut);

    [Fact]
    public void For_PicksTheStateForTheOwner()
    {
        Assert.Same(HoleTakeoverRules.TakenOver, HoleTakeoverRules.For(HoleCommandOwner.Awb, "", "H"));
        Assert.Equal(HoleTakeoverRules.GivenBack("", "H"), HoleTakeoverRules.For(HoleCommandOwner.Inventor, "", "H"));
    }

    [Theory]
    [InlineData(" H ", "H")]
    [InlineData("Ctrl+H", "Ctrl+H")]
    [InlineData(null, "H")]
    public void DefaultShortcut_TrimsOrFallsBack(string? read, string expected) =>
        Assert.Equal(expected, HoleTakeoverRules.DefaultShortcut(read));

    [Fact]
    public void Ids_MatchTheSpike()
    {
        Assert.Equal("PartHoleCmd", HoleTakeoverRules.InventorHoleCommand);
        Assert.Equal("Part", HoleTakeoverRules.RibbonName);
        Assert.Equal("id_TabModel", HoleTakeoverRules.TabId);
        Assert.Equal("id_PanelP_ModelModify", HoleTakeoverRules.PanelId);
    }

    [Fact]
    public void Messages_NameTheReasonAndWhoHasHole()
    {
        Assert.Contains("startup", HoleTakeoverRules.AppliedMessage(HoleTakeoverRules.TakenOver, "startup"));
        Assert.Contains(AwbHoleViewModel.CommandName, HoleTakeoverRules.AppliedMessage(HoleTakeoverRules.TakenOver, "startup"));
        Assert.Contains("Inventor's Hole", HoleTakeoverRules.AppliedMessage(HoleTakeoverRules.GivenBack("", "H"), "unload"));
        Assert.Contains("given back", HoleTakeoverRules.TakeOverFailedMessage("settings saved"));
        Assert.Throws<ArgumentNullException>(() => HoleTakeoverRules.AppliedMessage(null!, "x"));
    }
}
