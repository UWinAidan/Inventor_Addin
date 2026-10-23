using InventorAddin.Core.Holes;
using InventorAddin.Core.Ribbon;

namespace InventorAddin.Core.Tests.Holes;

public sealed class HoleEditRulesTests
{
    private const string Ours = CommandNames.AwbHoleEdit;
    private const string Inventors = HoleEditRules.InventorEditCommand;

    private sealed class Hole(string name)
    {
        public string Name { get; } = name;
    }

    private static bool SameName(Hole a, Hole b) => a.Name == b.Name;

    [Fact]
    public void InventorsEditFeature_IsTheSpikesCommand()
    {
        Assert.Equal("PartNGxHoleEditCtxCmd", HoleEditRules.InventorEditCommand);
        Assert.Equal("Edit Feature", HoleEditRules.EditFeatureText);
        Assert.Equal("Awb_AwbHoleEdit", CommandNames.AwbHoleEdit);
    }

    [Theory]
    [InlineData(HoleCommandOwner.Awb, true, true)]
    [InlineData(HoleCommandOwner.Awb, false, false)]
    [InlineData(HoleCommandOwner.Inventor, true, false)]
    [InlineData(null, true, false)]
    public void Redirects_OnlyWhileAwbHoleHasTheHoleCommand_InAPart(HoleCommandOwner? owner, bool part, bool expected)
    {
        Assert.Equal(expected, HoleEditRules.Redirects(owner, part));
    }

    [Fact]
    public void SingleHole_IsTheHoleEveryItemIs()
    {
        var hole = new Hole("Hole1");

        Assert.Same(hole, HoleEditRules.SingleHole([hole], SameName));
        Assert.Same(hole, HoleEditRules.SingleHole([hole, hole], SameName));

        // A face resolves to a new wrapper of the same feature.
        Assert.Same(hole, HoleEditRules.SingleHole([hole, new Hole("Hole1")], SameName));
    }

    [Fact]
    public void SingleHole_IsNull_ForNothing_ANonHole_OrTwoHoles()
    {
        var hole = new Hole("Hole1");

        Assert.Null(HoleEditRules.SingleHole(Array.Empty<Hole?>(), SameName));
        Assert.Null(HoleEditRules.SingleHole([hole, null], SameName));
        Assert.Null(HoleEditRules.SingleHole([null, hole], SameName));
        Assert.Null(HoleEditRules.SingleHole([hole, new Hole("Hole2")], SameName));
    }

    [Fact]
    public void Menu_ReplacesInventorsEntry_OnceOnly()
    {
        Assert.True(HoleEditRules.ReplacesMenuEntry(["AppCopyCmd", Inventors, "PartSuppressCtxCmd"], Ours));
        Assert.False(HoleEditRules.ReplacesMenuEntry(["AppCopyCmd", Ours, Inventors], Ours));
        Assert.False(HoleEditRules.ReplacesMenuEntry(["AppCopyCmd", "PartSuppressCtxCmd"], Ours));
        Assert.False(HoleEditRules.ReplacesMenuEntry([], Ours));
        Assert.True(HoleEditRules.ReplacesMenuEntry([null, " " + Inventors + " "], Ours));
    }

    [Fact]
    public void MiniToolbar_InsertsOursBeforeInventors_AtItsOneBasedIndex()
    {
        Assert.Equal(1, HoleEditRules.MiniToolbarIndex([Inventors, "SketchEditSketchCtxCmd"], Ours));
        Assert.Equal(3, HoleEditRules.MiniToolbarIndex(["A", "B", Inventors], Ours));
        Assert.Null(HoleEditRules.MiniToolbarIndex(["A", "B"], Ours));
        Assert.Null(HoleEditRules.MiniToolbarIndex([Ours, Inventors], Ours));
    }

    [Theory]
    [InlineData(HoleEditRules.InventorEditCommand, true, true, true)]
    [InlineData(HoleEditRules.InventorEditCommand, false, true, false)]
    [InlineData(HoleEditRules.InventorEditCommand, true, false, false)]
    [InlineData("PartHoleCmd", true, true, false)]
    [InlineData("partngxholeeditctxcmd", true, true, false)]
    [InlineData(null, true, true, false)]
    public void Backstop_StopsInventorsEdit_OnlyWhenOursCanOpen(string? command, bool redirects, bool hasHole, bool expected)
    {
        Assert.Equal(expected, HoleEditRules.StopsInventorEdit(command, redirects, hasHole));
    }

    [Fact]
    public void Messages_NameTheRouteAndTheHole()
    {
        Assert.Equal(
            "AWB Hole: double-click on Hole3 opens the AWB Hole window.",
            HoleEditRules.OpeningMessage("double-click", "Hole3"));
        Assert.Equal(
            "AWB Hole: Edit Feature on a hole while the window is open; the window is brought to the front.",
            HoleEditRules.AlreadyOpenMessage("Edit Feature", null));
    }

    [Fact]
    public void BadArguments_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => HoleEditRules.SingleHole<Hole>(null!, SameName));
        Assert.Throws<ArgumentNullException>(() => HoleEditRules.SingleHole<Hole>([], null!));
        Assert.Throws<ArgumentNullException>(() => HoleEditRules.ReplacesMenuEntry(null!, Ours));
        Assert.Throws<ArgumentException>(() => HoleEditRules.MiniToolbarIndex([], " "));
    }
}
