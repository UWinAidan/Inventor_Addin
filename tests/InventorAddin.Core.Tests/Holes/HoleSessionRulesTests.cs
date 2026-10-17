using InventorAddin.Core.Holes;

namespace InventorAddin.Core.Tests.Holes;

public sealed class HoleSessionRulesTests
{
    private sealed record Sketch(string Name);

    private sealed record Pt(string Name);

    [Fact]
    public void PickedColour_IsBlue()
    {
        Assert.Equal(new HoleColour(30, 120, 255), HoleSessionRules.PickedColour);
    }

    [Fact]
    public void TransactionName()
    {
        Assert.Equal(Branding.ShortName + " Hole", HoleSessionRules.TransactionName(false));
        Assert.Equal("Edit " + Branding.ShortName + " Hole", HoleSessionRules.TransactionName(true));
    }

    [Theory]
    [InlineData(30, false)]
    [InlineData(500, false)]
    [InlineData(501, true)]
    public void IsSlow_OnlyOver500Ms(int ms, bool slow)
    {
        Assert.Equal(slow, HoleSessionRules.IsSlow(TimeSpan.FromMilliseconds(ms)));
    }

    [Theory]
    // A kept new hole's sketch ends hidden, as Inventor leaves it.
    [InlineData(true, true, true, false)]
    [InlineData(true, true, false, false)]
    // Cancel, and every edit, put it back as it was.
    [InlineData(true, false, true, true)]
    [InlineData(true, false, false, false)]
    [InlineData(false, true, true, true)]
    [InlineData(false, true, false, false)]
    [InlineData(false, false, true, true)]
    [InlineData(false, false, false, false)]
    public void SketchVisibleAfter(bool newHole, bool kept, bool wasVisible, bool expected)
    {
        Assert.Equal(expected, HoleSessionRules.SketchVisibleAfter(newHole, kept, wasVisible));
    }

    private static (Sketch, Func<bool>, Func<bool>) Entry(Sketch sketch, bool visible, bool hasHoleCentres) =>
        (sketch, () => visible, () => hasHoleCentres);

    [Fact]
    public void DefaultSketch_TheSelectedSketchFirst()
    {
        var selected = new Sketch("selected");
        var ofPoint = new Sketch("of point");

        Assert.Same(selected, HoleSessionRules.DefaultSketch(selected, ofPoint, [Entry(new Sketch("newest"), true, true)]));
    }

    [Fact]
    public void DefaultSketch_ThenTheSketchOfTheSelectedPoints()
    {
        var ofPoint = new Sketch("of point");

        Assert.Same(ofPoint, HoleSessionRules.DefaultSketch(null, ofPoint, [Entry(new Sketch("newest"), true, true)]));
    }

    [Fact]
    public void DefaultSketch_ElseTheNewestVisibleSketchWithHoleCentres()
    {
        var newer = new Sketch("newer");
        var sketches = new[]
        {
            Entry(new Sketch("hidden"), false, true),
            Entry(new Sketch("no centres"), true, false),
            Entry(newer, true, true),
            Entry(new Sketch("older"), true, true),
        };

        Assert.Same(newer, HoleSessionRules.DefaultSketch<Sketch>(null, null, sketches));
    }

    [Fact]
    public void DefaultSketch_StopsAtTheFirstThatQualifies()
    {
        var read = new List<string>();
        (Sketch, Func<bool>, Func<bool>) Logged(string name) =>
            (new Sketch(name), () => { read.Add(name); return true; }, () => true);

        HoleSessionRules.DefaultSketch<Sketch>(null, null, [Logged("newest"), Logged("older")]);

        Assert.Equal(["newest"], read);
    }

    [Fact]
    public void DefaultSketch_NoneWhenNoSketchQualifies()
    {
        var sketches = new[] { Entry(new Sketch("hidden"), false, true), Entry(new Sketch("empty"), true, false) };

        Assert.Null(HoleSessionRules.DefaultSketch<Sketch>(null, null, sketches));
        Assert.Null(HoleSessionRules.DefaultSketch<Sketch>(null, null, []));
    }

    [Theory]
    // No centres: a new hole's preview goes; an edited hole stays.
    [InlineData(true, false, 0, true, HolePreviewAction.Remove)]
    [InlineData(false, false, 0, true, HolePreviewAction.None)]
    [InlineData(true, true, 0, true, HolePreviewAction.None)]
    [InlineData(true, false, 0, false, HolePreviewAction.Remove)]
    // Values that do not build: the hole stays at its last good state.
    [InlineData(true, false, 2, false, HolePreviewAction.None)]
    [InlineData(false, false, 2, false, HolePreviewAction.None)]
    [InlineData(true, true, 2, false, HolePreviewAction.None)]
    // Centres and values: make or change.
    [InlineData(false, false, 1, true, HolePreviewAction.Create)]
    [InlineData(true, false, 3, true, HolePreviewAction.Change)]
    [InlineData(true, true, 1, true, HolePreviewAction.Change)]
    public void Plan(bool hasHole, bool edit, int centres, bool hasGeometry, HolePreviewAction expected)
    {
        Assert.Equal(expected, HoleSessionRules.Plan(hasHole, edit, centres, hasGeometry));
    }

    [Theory]
    // Centres moved to another sketch on a new hole: the preview hole is removed and made again.
    [InlineData(true, false, 1, true, HolePreviewAction.RemoveThenCreate)]
    [InlineData(true, false, 1, false, HolePreviewAction.Remove)]
    [InlineData(true, false, 0, true, HolePreviewAction.Remove)]
    // With no hole held, it is an ordinary create.
    [InlineData(false, false, 1, true, HolePreviewAction.Create)]
    [InlineData(false, false, 1, false, HolePreviewAction.None)]
    // An edit never moves sketch; the flag changes nothing.
    [InlineData(true, true, 1, true, HolePreviewAction.Change)]
    public void Plan_SketchChanged(bool hasHole, bool edit, int centres, bool hasGeometry, HolePreviewAction expected)
    {
        Assert.Equal(expected, HoleSessionRules.Plan(hasHole, edit, centres, hasGeometry, sketchChanged: true));
    }

    [Theory]
    [InlineData(HolePreviewAction.Create, true, false, true, true, false)]
    [InlineData(HolePreviewAction.Change, true, false, true, false, false)]
    [InlineData(HolePreviewAction.Change, true, true, true, true, false)]
    // A refused or unhealthy step is aborted, also when a hole was made.
    [InlineData(HolePreviewAction.Create, false, false, false, false, false)]
    [InlineData(HolePreviewAction.Change, false, false, false, false, false)]
    // A failed recreate: abort, then find the hole again.
    [InlineData(HolePreviewAction.Change, false, true, false, false, true)]
    public void AfterWrite(
        HolePreviewAction action, bool succeeded, bool recreated, bool endStep, bool holdNew, bool findAgain)
    {
        Assert.Equal(
            new HolePreviewStep(endStep, holdNew, findAgain),
            HoleSessionRules.AfterWrite(action, succeeded, recreated));
    }

    [Theory]
    [InlineData(HolePreviewAction.None)]
    [InlineData(HolePreviewAction.Remove)]
    [InlineData(HolePreviewAction.RemoveThenCreate)]
    public void AfterWrite_OnlyForAWrite(HolePreviewAction action)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => HoleSessionRules.AfterWrite(action, true, false));
    }

    [Theory]
    [InlineData(null, "The hole's tolerance, colour or data could not be applied.")]
    [InlineData("Bad face.", "The hole's tolerance, colour or data could not be applied (Bad face).")]
    public void FinishFailed(string? reason, string expected)
    {
        Assert.Equal(expected, HoleSessionRules.FinishFailed(reason));
    }

    [Fact]
    public void Picks_StartTakesThePointsOnce()
    {
        var sketch = new Sketch("s");
        var a = new Pt("a");
        var picks = new HoleCentrePicks<Pt, Sketch>(edit: false);

        picks.Start(sketch, [a, a, new Pt("b")]);

        Assert.Same(sketch, picks.Sketch);
        Assert.Equal(2, picks.Count);
    }

    [Fact]
    public void Picks_AClickTogglesAPoint()
    {
        var sketch = new Sketch("s");
        var a = new Pt("a");
        var b = new Pt("b");
        var picks = new HoleCentrePicks<Pt, Sketch>(edit: false);
        picks.Start(sketch, [a]);

        Assert.Equal(HoleCentrePick.Added, picks.Toggle(b, sketch));
        Assert.Equal([a, b], picks.Points);
        Assert.Equal(HoleCentrePick.Removed, picks.Toggle(a, sketch));
        Assert.Equal([b], picks.Points);
    }

    [Fact]
    public void Picks_WithNoSketchTheFirstClickGivesIt()
    {
        var sketch = new Sketch("s");
        var picks = new HoleCentrePicks<Pt, Sketch>(edit: false);
        picks.Start(null, []);

        Assert.Equal(HoleCentrePick.Added, picks.Toggle(new Pt("a"), sketch));
        Assert.Same(sketch, picks.Sketch);
    }

    [Fact]
    public void Picks_NewHole_APointOfAnotherSketchStartsAgainThere()
    {
        var first = new Sketch("first");
        var other = new Sketch("other");
        var c = new Pt("c");
        var picks = new HoleCentrePicks<Pt, Sketch>(edit: false);
        picks.Start(first, [new Pt("a"), new Pt("b")]);

        Assert.Equal(HoleCentrePick.SketchChanged, picks.Toggle(c, other));
        Assert.Same(other, picks.Sketch);
        Assert.Equal([c], picks.Points);
    }

    [Fact]
    public void Picks_Edit_APointOfAnotherSketchIsIgnored()
    {
        var own = new Sketch("own");
        var a = new Pt("a");
        var picks = new HoleCentrePicks<Pt, Sketch>(edit: true);
        picks.Start(own, [a]);

        Assert.Equal(HoleCentrePick.Ignored, picks.Toggle(new Pt("c"), new Sketch("other")));
        Assert.Same(own, picks.Sketch);
        Assert.Equal([a], picks.Points);
    }

    [Fact]
    public void Picks_Edit_AnyPickedPointCanBeRemoved()
    {
        var own = new Sketch("own");
        var a = new Pt("a");
        var picks = new HoleCentrePicks<Pt, Sketch>(edit: true);
        picks.Start(own, [a]);

        Assert.Equal(HoleCentrePick.Removed, picks.Toggle(a, null));
        Assert.Empty(picks.Points);
    }

    [Fact]
    public void Picks_UseTheGivenComparers()
    {
        // Two objects for one point (as two COM wrappers may be), and for one sketch.
        var picks = new HoleCentrePicks<Pt, Sketch>(
            edit: true,
            samePoint: (x, y) => x.Name == y.Name,
            sameSketch: (x, y) => x.Name == y.Name);
        picks.Start(new Sketch("s"), [new Pt("a")]);

        Assert.Equal(HoleCentrePick.Removed, picks.Toggle(new Pt("a"), new Sketch("s")));
        Assert.Equal(HoleCentrePick.Added, picks.Toggle(new Pt("b"), new Sketch("s")));
    }

    [Fact]
    public void Picks_Edit_WithNoSketchEveryClickIsIgnored()
    {
        var picks = new HoleCentrePicks<Pt, Sketch>(edit: true);
        picks.Start(null, []);

        Assert.Equal(HoleCentrePick.Ignored, picks.Toggle(new Pt("a"), new Sketch("s")));
        Assert.Equal(HoleCentrePick.Ignored, picks.Toggle(new Pt("b"), null));
        Assert.Null(picks.Sketch);
        Assert.Empty(picks.Points);
    }
}
