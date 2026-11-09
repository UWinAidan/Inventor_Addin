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
    [InlineData("PartDMHoleCmd", HoleCommandOwner.Awb, true, false, false, true)]
    [InlineData(" PartDMHoleCmd ", HoleCommandOwner.Awb, true, false, false, true)]
    [InlineData("PartDMHoleCmd", HoleCommandOwner.Inventor, true, false, false, false)]
    [InlineData("PartDMHoleCmd", null, true, false, false, false)]
    [InlineData("PartDMHoleCmd", HoleCommandOwner.Awb, false, false, false, false)]
    [InlineData("PartDMHoleCmd", HoleCommandOwner.Awb, true, false, true, false)]
    [InlineData("PartHoleCmd", HoleCommandOwner.Awb, true, false, false, true)]
    [InlineData("PartHoleCmd", HoleCommandOwner.Awb, true, false, true, false)]
    [InlineData("PartHoleCmd", HoleCommandOwner.Inventor, true, false, false, false)]
    [InlineData("PartHoleCmd", HoleCommandOwner.Awb, false, false, false, false)]
    [InlineData("PartExtrudeCmd", HoleCommandOwner.Awb, true, false, false, false)]
    [InlineData("partdmholecmd", HoleCommandOwner.Awb, true, false, false, false)]
    [InlineData(null, HoleCommandOwner.Awb, true, false, false, false)]
    [InlineData("PartHoleCmd", HoleCommandOwner.Awb, true, true, false, false)]
    [InlineData("PartDMHoleCmd", HoleCommandOwner.Awb, true, true, false, false)]
    public void Backstop_StopsInventorsHole_OnlyWhenAwbOwnsItNotInFlatPatternAndNotAskedFor(
        string? command, HoleCommandOwner? owner, bool isPart, bool inFlatPattern, bool askedFor, bool expected) =>
        Assert.Equal(expected, HoleTakeoverRules.StopsInventorHole(command, owner, isPart, inFlatPattern, askedFor));

    [Theory]
    [InlineData(HoleCommandOwner.Awb, true, false, true)]
    [InlineData(HoleCommandOwner.Awb, true, true, false)]
    [InlineData(HoleCommandOwner.Awb, false, false, false)]
    [InlineData(HoleCommandOwner.Inventor, true, false, false)]
    [InlineData(null, true, false, false)]
    public void TakesOverHoleEntries_OnlyWhenAwbOwnsItInAPartOutsideTheFlatPattern(
        HoleCommandOwner? owner, bool isPart, bool inFlatPattern, bool expected) =>
        Assert.Equal(expected, HoleTakeoverRules.TakesOverHoleEntries(owner, isPart, inFlatPattern));

    [Theory]
    [InlineData(true, "PMxPartEnvironment", true)]
    [InlineData(true, null, true)]
    [InlineData(false, "PMxPartEnvironment", false)]
    [InlineData(false, "MBxSheetMetalEnvironment", false)]
    [InlineData(false, "SomeFlatPatternEnvironment", true)]
    [InlineData(false, "someflatpatternenv", true)]
    [InlineData(false, null, false)]
    public void InFlatPattern_EditObjectOrEnvironmentName(bool editObjectIsFlatPattern, string? environment, bool expected) =>
        Assert.Equal(expected, HoleTakeoverRules.InFlatPattern(editObjectIsFlatPattern, environment));

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
    public void RunRequest_RibbonHoleStartOnTheWay_IsLetThroughAndKeepsTheRequest()
    {
        var request = new InventorHoleRunRequest();
        request.Ask();

        Assert.True(request.StartIsAskedFor(HoleTakeoverRules.InventorHoleCommand));
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

    // ---- task 141: the mini toolbar keeps every entry ----

    // The list Inventor gave on a sketch point in a sheet metal part (Aidan's log, task 133).
    private static readonly string?[] SketchPointToolbar =
        [InventorsMiniHole, "SketchEditSketchCtxCmd", "PartVisibilityCtxCmd"];

    [Fact]
    public void MiniToolbarHole_AnUnreadableName_IsNull()
    {
        // Inventor's list could not be put back if the swap broke it.
        Assert.Null(HoleTakeoverRules.MiniToolbarHoleIndex([InventorsMiniHole, null], Ours));
        Assert.Null(HoleTakeoverRules.MiniToolbarHoleIndex(["A", " ", InventorsMiniHole], Ours));
    }

    [Fact]
    public void SwappedMiniToolbar_ReplacesInventorsHoleInPlace_AndKeepsTheRest()
    {
        Assert.Equal(
            [Ours, "SketchEditSketchCtxCmd", "PartVisibilityCtxCmd"],
            HoleTakeoverRules.SwappedMiniToolbar(SketchPointToolbar, Ours));
        Assert.Equal(["A", Ours, "B"], HoleTakeoverRules.SwappedMiniToolbar(["A", " PartDMHoleCmd ", " B "], Ours));
        Assert.Equal(["A", "B"], HoleTakeoverRules.SwappedMiniToolbar(["A", "B"], Ours));
        Assert.Empty(HoleTakeoverRules.SwappedMiniToolbar([], Ours));
    }

    [Fact]
    public void SwapHeld_TheExpectedList_IsTrue()
    {
        Assert.True(HoleTakeoverRules.MiniToolbarSwapHeld(
            SketchPointToolbar, [Ours, "SketchEditSketchCtxCmd", " PartVisibilityCtxCmd "], Ours));
        Assert.True(HoleTakeoverRules.MiniToolbarSwapHeld(["A", InventorsMiniHole], ["A", Ours], Ours));
    }

    [Fact]
    public void SwapHeld_EntriesAfterHoleLost_IsFalse() =>
        // Aidan's checks 133-1, 133-2 and 133-5: only Extrude and Revolve, which are not in the map, were left.
        Assert.False(HoleTakeoverRules.MiniToolbarSwapHeld(SketchPointToolbar, [Ours], Ours));

    [Fact]
    public void SwapHeld_AnythingButTheOneReplacement_IsFalse()
    {
        // Nothing left at all.
        Assert.False(HoleTakeoverRules.MiniToolbarSwapHeld(SketchPointToolbar, [], Ours));
        // Inventor's still there, ours before it (Remove failed).
        Assert.False(HoleTakeoverRules.MiniToolbarSwapHeld(
            SketchPointToolbar, [Ours, InventorsMiniHole, "SketchEditSketchCtxCmd", "PartVisibilityCtxCmd"], Ours));
        // Ours not added (Insert failed), Inventor's removed.
        Assert.False(HoleTakeoverRules.MiniToolbarSwapHeld(
            SketchPointToolbar, ["SketchEditSketchCtxCmd", "PartVisibilityCtxCmd"], Ours));
        // Ours at the wrong place.
        Assert.False(HoleTakeoverRules.MiniToolbarSwapHeld(
            SketchPointToolbar, ["SketchEditSketchCtxCmd", Ours, "PartVisibilityCtxCmd"], Ours));
        // An entry unreadable afterwards.
        Assert.False(HoleTakeoverRules.MiniToolbarSwapHeld(
            SketchPointToolbar, [Ours, null, "PartVisibilityCtxCmd"], Ours));
        // Nothing to swap: unchanged is not a held swap.
        Assert.False(HoleTakeoverRules.MiniToolbarSwapHeld(["A"], ["A"], Ours));
        // Names match exactly.
        Assert.False(HoleTakeoverRules.MiniToolbarSwapHeld(
            SketchPointToolbar, ["awb_awbhole", "SketchEditSketchCtxCmd", "PartVisibilityCtxCmd"], Ours));
    }

    [Fact]
    public void SwapHeld_BadArguments_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => HoleTakeoverRules.MiniToolbarSwapHeld(null!, [], Ours));
        Assert.Throws<ArgumentNullException>(() => HoleTakeoverRules.MiniToolbarSwapHeld([], null!, Ours));
        Assert.Throws<ArgumentException>(() => HoleTakeoverRules.MiniToolbarSwapHeld([], [], " "));
        Assert.Throws<ArgumentNullException>(() => HoleTakeoverRules.SwappedMiniToolbar(null!, Ours));
    }

    [Fact]
    public void MiniToolbarMessage_NamesTheStageAndEntriesInOrder()
    {
        string before = HoleTakeoverRules.MiniToolbarMessage(
            MiniToolbarStage.BeforeSwap, SketchPointToolbar, ["SketchPoint", null, " "], sheetMetal: false);
        string after = HoleTakeoverRules.MiniToolbarMessage(
            MiniToolbarStage.AfterSwap, [Ours, null, " X "], null, sheetMetal: true);
        string putBack = HoleTakeoverRules.MiniToolbarMessage(MiniToolbarStage.PutBack, [], null, sheetMetal: false);

        Assert.Contains("in a part, before the swap: ", before);
        Assert.Contains(
            "PartDMHoleCmd [SketchPoint], SketchEditSketchCtxCmd [no value], PartVisibilityCtxCmd [no value].", before);
        Assert.Contains("in a sheet metal part, after the swap: ", after);
        Assert.Contains($"{Ours}, (unnamed), X.", after);
        Assert.DoesNotContain("[", after);
        Assert.Contains("put back: (none).", putBack);
        Assert.Throws<ArgumentNullException>(() =>
            HoleTakeoverRules.MiniToolbarMessage(MiniToolbarStage.BeforeSwap, null!, null, false));
    }

    [Fact]
    public void MiniToolbarMessage_FewerKindsThanNames_ShowsNoValue() =>
        Assert.Contains(
            "A [Face], B [no value]",
            HoleTakeoverRules.MiniToolbarMessage(MiniToolbarStage.BeforeSwap, ["A", "B"], ["Face"], false));

    [Fact]
    public void SwapFailedMessage_NamesBothListsAndThePutBack()
    {
        string message = HoleTakeoverRules.MiniToolbarSwapFailedMessage(
            HoleTakeoverRules.SwappedMiniToolbar(SketchPointToolbar, Ours), [Ours], "Unspecified error");

        Assert.Contains($"should have left {Ours}, SketchEditSketchCtxCmd, PartVisibilityCtxCmd but left {Ours}.", message);
        Assert.Contains("The swap threw: Unspecified error", message);
        Assert.Contains("Inventor's list is being put back", message);
        Assert.DoesNotContain("threw", HoleTakeoverRules.MiniToolbarSwapFailedMessage(["A"], [], null));
        Assert.Contains("but left (none).", HoleTakeoverRules.MiniToolbarSwapFailedMessage(["A"], []));
    }

    [Fact]
    public void SameMiniToolbar_MatchesOnlyTheSameEntriesInOrder()
    {
        // Insert refused the value and threw before changing anything: the map is left alone.
        Assert.True(HoleTakeoverRules.SameMiniToolbar(SketchPointToolbar, [" PartDMHoleCmd", "SketchEditSketchCtxCmd", "PartVisibilityCtxCmd"]));
        Assert.True(HoleTakeoverRules.SameMiniToolbar([], []));
        Assert.False(HoleTakeoverRules.SameMiniToolbar(SketchPointToolbar, [InventorsMiniHole]));
        Assert.False(HoleTakeoverRules.SameMiniToolbar(["A", "B"], ["B", "A"]));
        Assert.False(HoleTakeoverRules.SameMiniToolbar([null], [null]));
        Assert.False(HoleTakeoverRules.SameMiniToolbar(["a"], ["A"]));
        Assert.Throws<ArgumentNullException>(() => HoleTakeoverRules.SameMiniToolbar(null!, []));
        Assert.Throws<ArgumentNullException>(() => HoleTakeoverRules.SameMiniToolbar([], null!));
    }

    [Fact]
    public void SwapLeftAloneMessage_SaysNothingChanged()
    {
        string message = HoleTakeoverRules.MiniToolbarSwapLeftAloneMessage(SketchPointToolbar, "Invalid argument");

        Assert.Contains("changed nothing; PartDMHoleCmd, SketchEditSketchCtxCmd, PartVisibilityCtxCmd is left", message);
        Assert.Contains("The swap threw: Invalid argument", message);
        Assert.DoesNotContain("threw", HoleTakeoverRules.MiniToolbarSwapLeftAloneMessage([]));
        Assert.Throws<ArgumentNullException>(() => HoleTakeoverRules.MiniToolbarSwapLeftAloneMessage(null!));
    }

    [Fact]
    public void Undo_InsertedButNotRemoved_RemovesOursOnly() =>
        Assert.Equal(
            new MiniToolbarUndo(true, false, null),
            HoleTakeoverRules.UndoMiniToolbarSwap(
                SketchPointToolbar, [Ours, InventorsMiniHole, "SketchEditSketchCtxCmd", "PartVisibilityCtxCmd"], Ours));

    [Fact]
    public void Undo_SwappedButBroken_RemovesOursAndPutsInventorsBackAtItsIndex()
    {
        // Entries after Hole lost: Inventor's goes back at the end of what is left.
        Assert.Equal(
            new MiniToolbarUndo(true, true, null),
            HoleTakeoverRules.UndoMiniToolbarSwap(SketchPointToolbar, [Ours], Ours));
        // Ours ended up elsewhere: Inventor's goes back before the entry now at its old index.
        Assert.Equal(
            new MiniToolbarUndo(true, true, 1),
            HoleTakeoverRules.UndoMiniToolbarSwap(
                SketchPointToolbar, ["SketchEditSketchCtxCmd", Ours, "PartVisibilityCtxCmd"], Ours));
        Assert.Equal(
            new MiniToolbarUndo(false, true, 2),
            HoleTakeoverRules.UndoMiniToolbarSwap(["A", InventorsMiniHole, "B"], ["A", "B"], Ours));
        Assert.Equal(
            new MiniToolbarUndo(false, true, null),
            HoleTakeoverRules.UndoMiniToolbarSwap(["A", InventorsMiniHole], ["A"], Ours));
    }

    [Fact]
    public void Undo_NothingToUndo_TouchesNothing()
    {
        Assert.Equal(new MiniToolbarUndo(false, false, null), HoleTakeoverRules.UndoMiniToolbarSwap(SketchPointToolbar, SketchPointToolbar, Ours));
        Assert.Equal(new MiniToolbarUndo(false, false, null), HoleTakeoverRules.UndoMiniToolbarSwap(["A"], [], Ours));
    }

    [Fact]
    public void Undo_BadArguments_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => HoleTakeoverRules.UndoMiniToolbarSwap(null!, [], Ours));
        Assert.Throws<ArgumentNullException>(() => HoleTakeoverRules.UndoMiniToolbarSwap([], null!, Ours));
        Assert.Throws<ArgumentException>(() => HoleTakeoverRules.UndoMiniToolbarSwap([], [], ""));
    }

    [Fact]
    public void PutBackMessage_NamesWhatFailedAndWhatLostItsValue()
    {
        string message = HoleTakeoverRules.MiniToolbarPutBackMessage(["SketchEditSketchCtxCmd: Invalid argument"], ["PartVisibilityCtxCmd"]);

        Assert.Contains("not added back: SketchEditSketchCtxCmd: Invalid argument", message);
        Assert.Contains("added back with no value (unreadable before): PartVisibilityCtxCmd.", message);
        Assert.Contains("every entry was added back.", HoleTakeoverRules.MiniToolbarPutBackMessage([], []));
        Assert.Throws<ArgumentNullException>(() => HoleTakeoverRules.MiniToolbarPutBackMessage(null!, []));
        Assert.Throws<ArgumentNullException>(() => HoleTakeoverRules.MiniToolbarPutBackMessage([], null!));
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
        // Task 133's follow-up: Inventor Hole asks first, then runs PartHoleCmd; that start is let through (task 142's
        // backstop) and must not use up or clear the request, and the mini toolbar's Hole is let through once.
        var request = new InventorHoleRunRequest();
        request.Ask();

        Assert.True(request.StartIsAskedFor(HoleTakeoverRules.InventorHoleRunCommand));
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

    // ---- task 142: the radial marking menu ----

    // A part's default ring as read in Inventor 2026 (task 142), clockwise from north.
    private static readonly string?[] PartRing =
    [
        "PartFilletCmd", "PartExtrudeCmd", "PartRevolveCmd", "PartHoleCmd",
        "AppSketch2DWrapperCmd", "PartWorkPlaneCmd", "AppUndoCmd", "AppMeasureDistanceCmd",
    ];

    private static string?[] RingWith(int slot, string? command)
    {
        var ring = (string?[])PartRing.Clone();
        ring[slot] = command;
        return ring;
    }

    [Fact]
    public void RingSlots_AreTheEightCompassPointsClockwiseFromNorth() =>
        Assert.Equal(
            ["north", "north-east", "east", "south-east", "south", "south-west", "west", "north-west"],
            HoleTakeoverRules.RingSlots);

    [Theory]
    [InlineData("PartHoleCmd", true)]
    [InlineData("PartDMHoleCmd", true)]
    [InlineData(" PartHoleCmd ", true)]
    [InlineData("partholecmd", false)]
    [InlineData("PartNGxHoleEditCtxCmd", false)]
    [InlineData(null, false)]
    public void IsInventorHole_EitherHoleCommand(string? command, bool expected) =>
        Assert.Equal(expected, HoleTakeoverRules.IsInventorHole(command));

    [Fact]
    public void RingHoleSlots_PartRing_IsTheSouthEastSlot() =>
        Assert.Equal([3], HoleTakeoverRules.RingHoleSlots(PartRing, Ours));

    [Fact]
    public void RingHoleSlots_MiniToolbarHoleInASlot_IsFoundToo() =>
        Assert.Equal([3], HoleTakeoverRules.RingHoleSlots(RingWith(3, InventorsMiniHole), Ours));

    [Fact]
    public void RingHoleSlots_EmptySlotsAreFine()
    {
        string?[] ring = [null, null, null, "PartHoleCmd", null, null, "AppUndoCmd", null];

        Assert.Equal([3], HoleTakeoverRules.RingHoleSlots(ring, Ours));
    }

    [Fact]
    public void RingHoleSlots_NoHole_None() =>
        Assert.Empty(HoleTakeoverRules.RingHoleSlots(RingWith(3, "PartChamferCmd"), Ours));

    [Fact]
    public void RingHoleSlots_OursAlreadyThere_None()
    {
        var ring = RingWith(0, Ours);

        Assert.Empty(HoleTakeoverRules.RingHoleSlots(ring, Ours));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void RingHoleSlots_AnUnreadableSlotName_None(string unreadable) =>
        Assert.Empty(HoleTakeoverRules.RingHoleSlots(RingWith(0, unreadable), Ours));

    [Fact]
    public void RingHoleSlots_NotEightSlots_None() =>
        Assert.Empty(HoleTakeoverRules.RingHoleSlots(["PartHoleCmd", "AppUndoCmd"], Ours));

    [Fact]
    public void RingHoleSlots_TwoHoles_BothSlots()
    {
        var ring = RingWith(0, InventorsMiniHole);

        Assert.Equal([0, 3], HoleTakeoverRules.RingHoleSlots(ring, Ours));
    }

    [Fact]
    public void RingHoleSlots_BadArguments_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => HoleTakeoverRules.RingHoleSlots(null!, Ours));
        Assert.Throws<ArgumentException>(() => HoleTakeoverRules.RingHoleSlots(PartRing, " "));
    }

    [Fact]
    public void SwappedRing_OursInHolesSlot_TheRestUnchanged() =>
        Assert.Equal(RingWith(3, Ours), HoleTakeoverRules.SwappedRing(PartRing, Ours));

    [Fact]
    public void SwappedRing_NoHole_Unchanged()
    {
        var ring = RingWith(3, null);

        Assert.Equal(ring, HoleTakeoverRules.SwappedRing(ring, Ours));
    }

    [Fact]
    public void RingSwapHeld_OursInHolesSlot_True() =>
        Assert.True(HoleTakeoverRules.RingSwapHeld(PartRing, RingWith(3, Ours), Ours));

    [Fact]
    public void RingSwapHeld_InventorsHoleStillThere_False() =>
        Assert.False(HoleTakeoverRules.RingSwapHeld(PartRing, PartRing, Ours));

    [Fact]
    public void RingSwapHeld_SlotLeftEmpty_False() =>
        Assert.False(HoleTakeoverRules.RingSwapHeld(PartRing, RingWith(3, null), Ours));

    [Fact]
    public void RingSwapHeld_AnotherSlotChanged_False()
    {
        var after = RingWith(3, Ours);
        after[0] = null;

        Assert.False(HoleTakeoverRules.RingSwapHeld(PartRing, after, Ours));
    }

    [Fact]
    public void RingSwapHeld_UnreadableNameAfter_False() =>
        Assert.False(HoleTakeoverRules.RingSwapHeld(PartRing, RingWith(3, ""), Ours));

    [Fact]
    public void RingSwapHeld_NothingToSwap_False()
    {
        var ring = RingWith(3, null);

        Assert.False(HoleTakeoverRules.RingSwapHeld(ring, ring, Ours));
    }

    [Fact]
    public void SameRing_EmptySlotsMatch_UnreadableNamesDoNot()
    {
        Assert.True(HoleTakeoverRules.SameRing(RingWith(1, null), RingWith(1, null)));
        Assert.True(HoleTakeoverRules.SameRing(RingWith(1, " PartExtrudeCmd "), PartRing));
        Assert.False(HoleTakeoverRules.SameRing(RingWith(1, ""), RingWith(1, "")));
        Assert.False(HoleTakeoverRules.SameRing(RingWith(1, null), PartRing));
        Assert.False(HoleTakeoverRules.SameRing(PartRing, PartRing[..7]));
    }

    [Fact]
    public void RingSlotsToRestore_OnlyTheSlotsThatChanged()
    {
        var now = RingWith(3, null);
        now[6] = "PartChamferCmd";

        Assert.Equal([3, 6], HoleTakeoverRules.RingSlotsToRestore(PartRing, now));
    }

    [Fact]
    public void RingSlotsToRestore_Alike_None() =>
        Assert.Empty(HoleTakeoverRules.RingSlotsToRestore(PartRing, PartRing));

    [Fact]
    public void RingSlotsToRestore_ShorterNow_TheMissingSlots() =>
        Assert.Equal([6, 7], HoleTakeoverRules.RingSlotsToRestore(PartRing, PartRing[..6]));

    [Fact]
    public void RingMessage_NamesEachSlotAndTheStage()
    {
        string message = HoleTakeoverRules.RingMessage(MiniToolbarStage.BeforeSwap, " None ", RingWith(6, null), sheetMetal: false);

        Assert.Equal(
            "Hole takeover: the marking menu ring 'None' in a part, before the swap: north PartFilletCmd, " +
            "north-east PartExtrudeCmd, east PartRevolveCmd, south-east PartHoleCmd, south AppSketch2DWrapperCmd, " +
            "south-west PartWorkPlaneCmd, west (empty), north-west AppMeasureDistanceCmd.",
            message);
    }

    [Theory]
    [InlineData(MiniToolbarStage.AfterSwap, "after the swap")]
    [InlineData(MiniToolbarStage.PutBack, "after Inventor's ring was put back")]
    public void RingMessage_OtherStagesAndSheetMetal(MiniToolbarStage stage, string when)
    {
        string message = HoleTakeoverRules.RingMessage(stage, null, RingWith(4, ""), sheetMetal: true);

        Assert.StartsWith($"Hole takeover: the marking menu ring '(unnamed)' in a sheet metal part, {when}: ", message);
        Assert.Contains("south (unnamed)", message);
    }

    [Fact]
    public void RingLogKey_DiffersByRingAndPartKind()
    {
        Assert.Equal(HoleTakeoverRules.RingLogKey("None", false), HoleTakeoverRules.RingLogKey(" None ", false));
        Assert.NotEqual(HoleTakeoverRules.RingLogKey("None", false), HoleTakeoverRules.RingLogKey("None", true));
        Assert.NotEqual(HoleTakeoverRules.RingLogKey("None", false), HoleTakeoverRules.RingLogKey("Face", false));
    }

    [Fact]
    public void RingSwapFailedMessage_NamesBothRingsAndTheError()
    {
        string message = HoleTakeoverRules.RingSwapFailedMessage(RingWith(3, Ours), PartRing, " refused ");

        Assert.StartsWith("Hole takeover: replacing Hole on the marking menu ring should have left ", message);
        Assert.Contains($"south-east {Ours}", message);
        Assert.Contains("but left north PartFilletCmd", message);
        Assert.Contains("The swap threw: refused", message);
        Assert.Contains(AwbHoleViewModel.CommandName, message);
    }

    [Theory]
    [InlineData("PartDMHoleCmd", "from the mini toolbar")]
    [InlineData("PartHoleCmd", "(PartHoleCmd: the marking menu, a shortcut or the command search)")]
    public void InventorHoleStoppedMessage_SaysWhereItCameFrom(string command, string from) =>
        Assert.Equal(
            $"Hole takeover: Inventor's Hole {from} started; {AwbHoleViewModel.CommandName} opens instead.",
            HoleTakeoverRules.InventorHoleStoppedMessage(command));
}
