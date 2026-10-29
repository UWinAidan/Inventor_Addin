using InventorAddin.Core.Holes;
using InventorAddin.Core.Ribbon;
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

    [Fact]
    public void Messages_NameTheSheetMetalTab_WhenItWasSet()
    {
        // Task 128.
        Assert.Contains("on the 3D Model tab and", HoleTakeoverRules.AppliedMessage(HoleTakeoverRules.TakenOver, "startup"));
        Assert.Contains(
            "on the 3D Model and Sheet Metal tabs and the H shortcut",
            HoleTakeoverRules.AppliedMessage(HoleTakeoverRules.TakenOver, "startup", sheetMetalTab: true));
        Assert.Contains(
            "its button on the 3D Model and Sheet Metal tabs and its shortcut back",
            HoleTakeoverRules.AppliedMessage(HoleTakeoverRules.GivenBack("", "H"), "unload", sheetMetalTab: true));
    }

    [Fact]
    public void SheetMetalIds_AreSpike081sPanelOnTheSheetMetalTab()
    {
        Assert.Equal("id_TabSheetMetal", HoleTakeoverRules.SheetMetalTabId);
        Assert.Equal("id_PanelP_SheetMetalModify", HoleTakeoverRules.SheetMetalPanelId);
    }

    // ---- task 133: the mini toolbar's Hole ----

    private const string Ours = CommandNames.AwbHole;
    private const string InventorsMiniHole = HoleTakeoverRules.InventorMiniToolbarHoleCommand;

    [Fact]
    public void MiniToolbarHole_IdMatchesTheSpike() =>
        Assert.Equal("PartDMHoleCmd", HoleTakeoverRules.InventorMiniToolbarHoleCommand);

    [Fact]
    public void MiniToolbarHole_InsertsOursBeforeInventors_AtItsOneBasedIndex()
    {
        // Spike 103's toolbar on a sketch point in place.
        Assert.Equal(1, HoleTakeoverRules.MiniToolbarHoleIndex(
            [InventorsMiniHole, "SketchEditSketchCtxCmd", "AssemblyVisibilityCtxCmd"], Ours));
        Assert.Equal(3, HoleTakeoverRules.MiniToolbarHoleIndex(["A", "B", InventorsMiniHole], Ours));
        Assert.Equal(2, HoleTakeoverRules.MiniToolbarHoleIndex(["A", " PartDMHoleCmd "], Ours));
    }

    [Fact]
    public void MiniToolbarHole_NoInventorEntry_IsNull()
    {
        Assert.Null(HoleTakeoverRules.MiniToolbarHoleIndex([], Ours));
        Assert.Null(HoleTakeoverRules.MiniToolbarHoleIndex(["SketchEditSketchCtxCmd", null], Ours));
        // The ribbon's Hole is not the mini toolbar's, and names match exactly.
        Assert.Null(HoleTakeoverRules.MiniToolbarHoleIndex([HoleTakeoverRules.InventorHoleCommand], Ours));
        Assert.Null(HoleTakeoverRules.MiniToolbarHoleIndex(["partdmholecmd"], Ours));
    }

    [Fact]
    public void MiniToolbarHole_OursAlreadyThere_IsNull()
    {
        Assert.Null(HoleTakeoverRules.MiniToolbarHoleIndex([Ours, InventorsMiniHole], Ours));
        Assert.Null(HoleTakeoverRules.MiniToolbarHoleIndex([InventorsMiniHole, Ours], Ours));
    }

    [Fact]
    public void MiniToolbarHole_BadArguments_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => HoleTakeoverRules.MiniToolbarHoleIndex(null!, Ours));
        Assert.Throws<ArgumentException>(() => HoleTakeoverRules.MiniToolbarHoleIndex([], " "));
    }

    [Theory]
    [InlineData("PartDMHoleCmd", HoleCommandOwner.Awb, true, false, true)]
    [InlineData(" PartDMHoleCmd ", HoleCommandOwner.Awb, true, false, true)]
    [InlineData("PartDMHoleCmd", HoleCommandOwner.Inventor, true, false, false)]
    [InlineData("PartDMHoleCmd", null, true, false, false)]
    [InlineData("PartDMHoleCmd", HoleCommandOwner.Awb, false, false, false)]
    [InlineData("PartDMHoleCmd", HoleCommandOwner.Awb, true, true, false)]
    [InlineData("PartHoleCmd", HoleCommandOwner.Awb, true, false, false)]
    [InlineData("partdmholecmd", HoleCommandOwner.Awb, true, false, false)]
    [InlineData(null, HoleCommandOwner.Awb, true, false, false)]
    public void MiniToolbarBackstop_StopsInventorsHole_OnlyWhenAwbOwnsItAndNotAskedFor(
        string? command, HoleCommandOwner? owner, bool isPart, bool askedFor, bool expected) =>
        Assert.Equal(expected, HoleTakeoverRules.StopsInventorMiniToolbarHole(command, owner, isPart, askedFor));

    [Fact]
    public void RunRequest_NothingAsked_NoStartIsAskedFor()
    {
        var request = new InventorHoleRunRequest();

        Assert.False(request.IsPending);
        Assert.False(request.StartIsAskedFor(InventorsMiniHole));
    }

    [Fact]
    public void RunRequest_Asked_LetsTheNextMiniToolbarHoleThroughOnce()
    {
        var request = new InventorHoleRunRequest();
        request.Ask();

        Assert.True(request.IsPending);
        Assert.True(request.StartIsAskedFor(InventorsMiniHole));
        Assert.False(request.IsPending);
        Assert.False(request.StartIsAskedFor(InventorsMiniHole));
    }

    [Fact]
    public void RunRequest_RibbonHoleStartOnTheWay_KeepsTheRequest()
    {
        var request = new InventorHoleRunRequest();
        request.Ask();

        Assert.False(request.StartIsAskedFor(HoleTakeoverRules.InventorHoleCommand));
        Assert.True(request.IsPending);
        Assert.True(request.StartIsAskedFor(InventorsMiniHole));
    }

    [Theory]
    [InlineData("SketchEditSketchCtxCmd")]
    [InlineData(CommandNames.AwbHole)]
    [InlineData(null)]
    public void RunRequest_AnotherCommandStarts_ClearsTheRequest(string? other)
    {
        var request = new InventorHoleRunRequest();
        request.Ask();

        Assert.False(request.StartIsAskedFor(other));
        Assert.False(request.IsPending);
        Assert.False(request.StartIsAskedFor(InventorsMiniHole));
    }

    [Fact]
    public void SheetMetalMessage_NamesTheEntries()
    {
        string message = HoleTakeoverRules.SheetMetalMiniToolbarMessage(["SheetMetalHoleCmd", null, " X "]);

        Assert.Contains("SheetMetalHoleCmd, (unnamed), X", message);
        Assert.Contains("PartDMHoleCmd", message);
        Assert.Contains("(none)", HoleTakeoverRules.SheetMetalMiniToolbarMessage([]));
        Assert.Throws<ArgumentNullException>(() => HoleTakeoverRules.SheetMetalMiniToolbarMessage(null!));
    }

    [Theory]
    [InlineData(HoleCommandOwner.Awb, true, HoleCommandOwner.Awb)]
    [InlineData(HoleCommandOwner.Awb, false, HoleCommandOwner.Inventor)]
    [InlineData(HoleCommandOwner.Inventor, true, HoleCommandOwner.Inventor)]
    [InlineData(HoleCommandOwner.Inventor, false, HoleCommandOwner.Inventor)]
    public void OwnerAfterApply_FailedTakeOverGivesHoleBack(HoleCommandOwner requested, bool succeeded, HoleCommandOwner expected) =>
        Assert.Equal(expected, HoleTakeoverRules.OwnerAfterApply(requested, succeeded));

    [Fact]
    public void ResetReapplyOrder_Awb_GivesBackThenTakesOver_AsTogglingTheSwitchDoes() =>
        Assert.Equal(
            [HoleCommandOwner.Inventor, HoleCommandOwner.Awb],
            HoleTakeoverRules.ResetReapplyOrder(HoleCommandOwner.Awb));

    [Fact]
    public void ResetReapplyOrder_Inventor_GivesBackOnce() =>
        Assert.Equal([HoleCommandOwner.Inventor], HoleTakeoverRules.ResetReapplyOrder(HoleCommandOwner.Inventor));

    [Theory]
    [InlineData(HoleCommandOwner.Awb, "H", "", true)]
    [InlineData(HoleCommandOwner.Awb, " h ", "  ", true)]
    [InlineData(HoleCommandOwner.Awb, "", "H", false)]
    [InlineData(HoleCommandOwner.Awb, "H", "H", false)]
    [InlineData(HoleCommandOwner.Awb, "", "", false)]
    [InlineData(HoleCommandOwner.Inventor, "", "H", true)]
    [InlineData(HoleCommandOwner.Inventor, "", "Ctrl+J", true)]
    [InlineData(HoleCommandOwner.Inventor, "H", "", false)]
    [InlineData(HoleCommandOwner.Inventor, "", "", false)]
    public void ShortcutsApplied_ComparesWhatWasRead(HoleCommandOwner owner, string awb, string inventor, bool expected) =>
        Assert.Equal(expected, HoleTakeoverRules.ShortcutsApplied(owner, awb, inventor));

    [Theory]
    [InlineData(null, "")]
    [InlineData("H", null)]
    public void ShortcutsApplied_Unreadable_IsNull(string? awb, string? inventor)
    {
        Assert.Null(HoleTakeoverRules.ShortcutsApplied(HoleCommandOwner.Awb, awb, inventor));
        Assert.Null(HoleTakeoverRules.ShortcutsApplied(HoleCommandOwner.Inventor, awb, inventor));
    }

    [Fact]
    public void ShortcutsMessage_NamesBothCommands() =>
        Assert.Equal(
            $"{AwbHoleViewModel.CommandName} 'H', Inventor's Hole none",
            HoleTakeoverRules.ShortcutsMessage(" H ", ""));

    [Fact]
    public void ShortcutsMessage_Unreadable() =>
        Assert.Contains("Inventor's Hole unreadable", HoleTakeoverRules.ShortcutsMessage("H", null));

    // ---- The split button (task 129) ----

    [Fact]
    public void SplitButtonCommands_AwbHoleFirst_ThenInventorHole_ThenTheToggle() =>
        Assert.Equal(
            new[] { CommandNames.AwbHole, CommandNames.InventorHole, CommandNames.UseInventorHoleByDefault },
            HoleTakeoverRules.SplitButtonCommands);

    [Theory]
    [InlineData(true, true, HoleButtonKind.SplitButton)]
    [InlineData(false, true, HoleButtonKind.PlainButton)]
    [InlineData(true, false, HoleButtonKind.PlainButton)]
    [InlineData(false, false, HoleButtonKind.PlainButton)]
    public void ButtonKind_SplitOnlyWithBothDropdownCommands(bool inventorHole, bool toggle, HoleButtonKind expected) =>
        Assert.Equal(expected, HoleTakeoverRules.ButtonKind(inventorHole, toggle));

    [Fact]
    public void InventorHole_RunsTheRibbonHoleCommand_OnBothTabs() =>
        Assert.Equal("PartHoleCmd", HoleTakeoverRules.InventorHoleRunCommand);

    [Fact]
    public void InventorHole_StartOfItsCommand_KeepsTheRunRequest_ForTheMiniToolbarBackstop()
    {
        // Task 133's follow-up: Inventor Hole asks first, then runs PartHoleCmd; that start must not use up or clear the
        // request, and the mini toolbar's Hole is let through once.
        var request = new InventorHoleRunRequest();
        request.Ask();

        Assert.False(request.StartIsAskedFor(HoleTakeoverRules.InventorHoleRunCommand));
        Assert.True(request.IsPending);
        Assert.True(request.StartIsAskedFor(HoleTakeoverRules.InventorMiniToolbarHoleCommand));
        Assert.False(request.IsPending);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TogglePressed_FollowsTheSetting(bool useInventorHole) =>
        Assert.Equal(
            useInventorHole,
            HoleTakeoverRules.TogglePressed(new AddinSettings { UseInventorHoleCommand = useInventorHole }));

    [Fact]
    public void TogglePressed_NullSettings_Throws() =>
        Assert.Throws<ArgumentNullException>(() => HoleTakeoverRules.TogglePressed(null!));

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void Toggled_FlipsTheSetting_AndKeepsTheRest(bool before, bool after)
    {
        var settings = new AddinSettings { UseInventorHoleCommand = before, DefaultDesigner = "Test Designer" };

        AddinSettings toggled = HoleTakeoverRules.Toggled(settings);

        Assert.Equal(after, toggled.UseInventorHoleCommand);
        Assert.Equal(settings with { UseInventorHoleCommand = after }, toggled);
        Assert.Equal(before, settings.UseInventorHoleCommand);
    }

    [Fact]
    public void Toggled_ThenOwnerFor_GivesTheOtherHole()
    {
        var settings = new AddinSettings();

        Assert.Equal(HoleCommandOwner.Awb, HoleTakeoverRules.OwnerFor(settings));
        Assert.Equal(HoleCommandOwner.Inventor, HoleTakeoverRules.OwnerFor(HoleTakeoverRules.Toggled(settings)));
    }

    [Fact]
    public void ToggledMessage_SaysOnOrOff_AndWhere()
    {
        Assert.Equal(
            "\"Use Inventor's Hole by default\" turned on; settings saved to 'C:\\data\\settings.json'.",
            HoleTakeoverRules.ToggledMessage(new AddinSettings { UseInventorHoleCommand = true }, @"C:\data\settings.json"));
        Assert.Contains("turned off", HoleTakeoverRules.ToggledMessage(new AddinSettings(), "x"));
    }

    [Fact]
    public void ToggleSaveFailedText_NamesTheFileAndTheError() =>
        Assert.Equal(
            "The setting was not changed: settings could not be saved to 'C:\\data\\settings.json'. Access denied.",
            HoleTakeoverRules.ToggleSaveFailedText(@"C:\data\settings.json", "Access denied."));

    [Fact]
    public void DropdownTexts_AreAidansNames()
    {
        Assert.Equal("Inventor Hole", HoleTakeoverRules.InventorHoleText);
        Assert.Equal("Use Inventor's Hole by default", HoleTakeoverRules.UseInventorHoleText);
    }

    [Fact]
    public void PlainButtonMessage_NamesTheReasonAndBothEntries()
    {
        string message = HoleTakeoverRules.PlainButtonMessage("startup");

        Assert.StartsWith("Hole takeover (startup): ", message);
        Assert.Contains("Inventor Hole", message);
        Assert.Contains("Use Inventor's Hole by default", message);
    }

    [Theory]
    [InlineData(CommandNames.InventorHole, IconNames.AwbHole)]
    [InlineData(CommandNames.UseInventorHoleByDefault, IconNames.Settings)]
    [InlineData(CommandNames.AwbHole, null)]
    [InlineData(CommandNames.Settings, null)]
    public void DropdownIconName_OnlyForTheDropdownCommands(string command, string? expected) =>
        Assert.Equal(expected, HoleTakeoverRules.DropdownIconName(command));
}
