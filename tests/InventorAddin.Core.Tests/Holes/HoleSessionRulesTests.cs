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
    [InlineData(@"C:\Work\Gearbox.iam", @"C:\Work\Gearbox.iam", true, true)]
    [InlineData(@"C:\Work\Gearbox.iam", @"c:\work\GEARBOX.IAM", true, true)]
    [InlineData(@"C:\Work\Bracket.ipt", @"C:\Work\Bracket.ipt", true, true)]
    [InlineData(@"C:\Work\Gearbox.iam", @"C:\Work\Gearbox.iam", false, false)]
    [InlineData(@"C:\Work\Gearbox.iam", @"C:\Work\Bracket.ipt", true, false)]
    [InlineData(@"C:\Work\Gearbox.iam", null, true, false)]
    [InlineData("", "", true, false)]
    [InlineData(null, null, true, false)]
    public void EndsOnDeactivate_OnlyTheSessionsDocumentBeforeTheSwitch(
        string? session, string? deactivating, bool before, bool ends)
    {
        Assert.Equal(ends, HoleSessionRules.EndsOnDeactivate(session, deactivating, before));
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
    [InlineData(5, false)]
    [InlineData(30, false)]
    [InlineData(31, true)]
    public void IsSlowDraw_OnlyOver30Ms(int ms, bool slow)
    {
        Assert.Equal(slow, HoleSessionRules.IsSlowDraw(TimeSpan.FromMilliseconds(ms)));
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

    [Theory]
    // A sketch shared at the end keeps its visibility, also for a kept new hole (task 094).
    [InlineData(true, true, true, true)]
    [InlineData(true, true, false, false)]
    [InlineData(true, false, true, true)]
    [InlineData(false, true, true, true)]
    [InlineData(false, true, false, false)]
    public void SketchVisibleAfter_ASharedSketchKeepsItsVisibility(bool newHole, bool kept, bool wasVisible, bool expected)
    {
        Assert.Equal(expected, HoleSessionRules.SketchVisibleAfter(newHole, kept, wasVisible, sharedAfter: true));
    }

    [Theory]
    // Shared by the session (a second feature consumed it): set back to not shared.
    [InlineData(false, true, true)]
    // Shared before the window opened: stays shared.
    [InlineData(true, true, false)]
    // Not shared now: nothing to do.
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    public void UnshareAfter(bool wasShared, bool isShared, bool expected)
    {
        Assert.Equal(expected, HoleSessionRules.UnshareAfter(wasShared, isShared));
    }

    [Theory]
    // A dowel kept as another kind: every face goes back to the part's appearance.
    [InlineData(true, HoleKind.Dowel, true, HoleKind.Clearance, true)]
    [InlineData(true, HoleKind.Dowel, true, HoleKind.Tapped, true)]
    // Stored as a dowel but no fit colour left on a face: still a dowel that stops being one.
    [InlineData(true, HoleKind.Dowel, false, HoleKind.Clearance, true)]
    // Not stored as a dowel, but a face carries a fit colour: treated as a dowel.
    [InlineData(true, HoleKind.Clearance, true, HoleKind.Tapped, true)]
    // Still a dowel: the fit colour is applied instead.
    [InlineData(true, HoleKind.Dowel, true, HoleKind.Dowel, false)]
    // Never a dowel: colours the user set are kept.
    [InlineData(true, HoleKind.Clearance, false, HoleKind.Clearance, false)]
    [InlineData(true, HoleKind.Clearance, false, HoleKind.Tapped, false)]
    [InlineData(true, HoleKind.Tapped, false, HoleKind.Clearance, false)]
    [InlineData(true, HoleKind.Clearance, false, HoleKind.Dowel, false)]
    // A new hole has nothing to reset.
    [InlineData(false, HoleKind.Dowel, false, HoleKind.Clearance, false)]
    [InlineData(false, HoleKind.Clearance, false, HoleKind.Tapped, false)]
    public void ResetsFaceAppearances(
        bool edit, HoleKind openedKind, bool openedFitColoured, HoleKind keptKind, bool expected)
    {
        Assert.Equal(expected, HoleSessionRules.ResetsFaceAppearances(edit, openedKind, openedFitColoured, keptKind));
    }

    // Task 117: hole centres per point. A sketch is described by its hole-centre points; a point named "used" is
    // one a feature already uses.
    private sealed record PtSketch(string Name, Pt[] Centres);

    private static (PtSketch, Func<bool>) Entry(PtSketch sketch, bool visible = true) => (sketch, () => visible);

    private static PtSketch WithCentres(string name, params string[] points) =>
        new(name, points.Select(p => new Pt(p)).ToArray());

    private static DefaultCentres<PtSketch, Pt> Default(
        PtSketch? selected, PtSketch? ofPoint, params (PtSketch, Func<bool>)[] sketches) =>
        HoleSessionRules.DefaultCentres(selected, ofPoint, sketches, s => s.Centres, p => p.Name.StartsWith("used"));

    [Fact]
    public void DefaultCentres_OnlyTheCentresNoFeatureUses()
    {
        // Four hole centres, two used by an earlier hole: the other two are picked.
        var sketch = WithCentres("sketch", "used a", "free b", "used c", "free d");

        var result = Default(null, null, Entry(sketch));

        Assert.Same(sketch, result.Sketch);
        Assert.Equal(["free b", "free d"], result.Points.Select(p => p.Name));
        Assert.Equal(DefaultCentresFrom.NewestWithFreeCentres, result.From);
        Assert.Equal(4, result.HoleCentresInSketch);
        Assert.Equal("newest visible with free centres, 2 of 4 hole centres", result.Describe());
    }

    [Fact]
    public void DefaultCentres_ASketchWhoseCentresAreAllUsedIsSkipped()
    {
        var older = WithCentres("older", "free a", "used b");
        var sketches = new[]
        {
            Entry(WithCentres("all used", "used a", "used b")),
            Entry(older),
        };

        var result = Default(null, null, sketches);

        Assert.Same(older, result.Sketch);
        Assert.Equal(["free a"], result.Points.Select(p => p.Name));
    }

    [Fact]
    public void DefaultCentres_SkipsHiddenSketchesAndSketchesWithoutCentres()
    {
        var newer = WithCentres("newer", "free a");
        var sketches = new[]
        {
            Entry(WithCentres("hidden", "free a"), visible: false),
            Entry(WithCentres("no centres")),
            Entry(newer),
            Entry(WithCentres("older", "free a")),
        };

        Assert.Same(newer, Default(null, null, sketches).Sketch);
    }

    [Fact]
    public void DefaultCentres_TheSelectedSketchFirst_WithOnlyItsFreeCentres()
    {
        // Task 154: a selected sketch gives the centres no feature uses yet.
        var selected = WithCentres("selected", "used a", "free b", "used c", "free d", "used e");
        var ofPoint = WithCentres("of point", "free a");

        var result = Default(selected, ofPoint, Entry(WithCentres("newest", "free a")));

        Assert.Same(selected, result.Sketch);
        Assert.Equal(["free b", "free d"], result.Points.Select(p => p.Name));
        Assert.Equal(DefaultCentresFrom.SelectedSketch, result.From);
        Assert.Equal(5, result.HoleCentresInSketch);
        Assert.False(result.AllUsed);
        Assert.Equal("selected, 2 free of 5 hole centres", result.Describe());
    }

    [Fact]
    public void DefaultCentres_ASelectedSketchWithEveryCentreUsed_GivesThemAll()
    {
        var selected = WithCentres("selected", "used a", "used b", "used c");

        var result = Default(selected, null, Entry(WithCentres("newest", "free a")));

        Assert.Same(selected, result.Sketch);
        Assert.Equal(["used a", "used b", "used c"], result.Points.Select(p => p.Name));
        Assert.Equal(DefaultCentresFrom.SelectedSketch, result.From);
        Assert.True(result.AllUsed);
        Assert.Equal("selected, all 3 hole centres are used, so all are picked", result.Describe());
    }

    [Fact]
    public void DefaultCentres_ASelectedSketchWithEveryCentreFree_GivesThemAll()
    {
        var selected = WithCentres("selected", "free a", "free b");

        var result = Default(selected, null);

        Assert.Equal(["free a", "free b"], result.Points.Select(p => p.Name));
        Assert.False(result.AllUsed);
        Assert.Equal("selected, 2 free of 2 hole centres", result.Describe());
    }

    [Fact]
    public void DefaultCentres_ThenTheSketchOfTheSelectedPoints_WithAllItsCentres()
    {
        // A selected point's sketch is unchanged by task 154: all its centres, used or not.
        var ofPoint = WithCentres("of point", "used a", "free b", "used c");

        var result = Default(null, ofPoint, Entry(WithCentres("newest", "free a")));

        Assert.Same(ofPoint, result.Sketch);
        Assert.Equal(["used a", "free b", "used c"], result.Points.Select(p => p.Name));
        Assert.Equal(DefaultCentresFrom.SketchOfSelectedPoints, result.From);
        Assert.False(result.AllUsed);
        Assert.Equal("of the selected points, all 3 hole centres", result.Describe());
    }

    [Fact]
    public void DefaultCentres_ASelectionIsTakenEvenWithoutCentres()
    {
        var selected = WithCentres("selected");

        var result = Default(selected, null, Entry(WithCentres("newest", "free a")));

        Assert.Same(selected, result.Sketch);
        Assert.Empty(result.Points);
        Assert.False(result.AllUsed);
        Assert.Equal("selected, no hole centres", result.Describe());
    }

    [Fact]
    public void DefaultCentres_NoneWhenNoSketchQualifies()
    {
        var sketches = new[]
        {
            Entry(WithCentres("hidden", "free a"), visible: false),
            Entry(WithCentres("empty")),
            Entry(WithCentres("all used", "used a")),
        };

        var result = Default(null, null, sketches);

        Assert.Null(result.Sketch);
        Assert.Empty(result.Points);
        Assert.Equal(DefaultCentresFrom.None, result.From);
        Assert.Null(result.Describe());
        Assert.Null(Default(null, null).Sketch);
    }

    [Fact]
    public void DefaultCentres_ReadsOnlyAsFarAsTheFirstThatQualifies()
    {
        var read = new List<string>();
        (PtSketch, Func<bool>) Logged(PtSketch sketch, bool visible) =>
            (sketch, () => { read.Add("visible " + sketch.Name); return visible; });
        var sketches = new[]
        {
            Logged(WithCentres("hidden", "free a"), false),
            Logged(WithCentres("newest", "free a"), true),
            Logged(WithCentres("older", "free a"), true),
        };

        HoleSessionRules.DefaultCentres(
            null,
            null,
            sketches,
            s => { read.Add("centres " + s.Name); return s.Centres; },
            p => { read.Add("used " + p.Name); return false; });

        Assert.Equal(["visible hidden", "visible newest", "centres newest", "used free a"], read);
    }

    [Fact]
    public void DefaultCentres_ASelectedPointsSketchDoesNotAskWhichPointsAreUsed()
    {
        var asked = false;

        HoleSessionRules.DefaultCentres(
            null,
            WithCentres("of point", "a"),
            Array.Empty<(PtSketch, Func<bool>)>(),
            s => s.Centres,
            _ => asked = true);

        Assert.False(asked);
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

    // Tasks 145 and 154: a sketch clicked in the browser. A point named "used" is one a feature already uses.
    private static PickedSketchCentres<Pt> Clicked(PtSketch sketch, bool edit = false, bool inSessionPart = true) =>
        HoleSessionRules.CentresOfPickedSketch(
            edit, inSessionPart, () => sketch.Centres, p => p.Name.StartsWith("used"));

    [Fact]
    public void PickedSketch_GivesOnlyItsFreeCentres_AsForASelectedSketch()
    {
        var result = Clicked(WithCentres("other", "used a", "free b", "free c", "used d", "free e"));

        Assert.True(result.Replaces);
        Assert.Equal(PickedSketchOutcome.Replaced, result.Outcome);
        Assert.Equal(["free b", "free c", "free e"], result.Points.Select(p => p.Name));
        Assert.Equal(5, result.HoleCentresInSketch);
        Assert.False(result.AllUsed);
        Assert.Equal(
            "other was clicked in the browser: its 3 free hole centres of 5 are picked.",
            result.Describe("other"));
    }

    [Fact]
    public void PickedSketch_WithAllCentresUsed_PicksThemAll_AndSaysSo()
    {
        var result = Clicked(WithCentres("done", "used a", "used b", "used c", "used d", "used e"));

        Assert.True(result.Replaces);
        Assert.Equal(["used a", "used b", "used c", "used d", "used e"], result.Points.Select(p => p.Name));
        Assert.True(result.AllUsed);
        Assert.Equal(
            "done was clicked in the browser: all 5 of its hole centres are used, so all are picked.",
            result.Describe("done"));
    }

    [Fact]
    public void PickedSketch_WithEveryCentreFree_PicksThemAll()
    {
        var result = Clicked(WithCentres("new", "free a", "free b"));

        Assert.True(result.Replaces);
        Assert.Equal(["free a", "free b"], result.Points.Select(p => p.Name));
        Assert.False(result.AllUsed);
    }

    [Fact]
    public void PickedSketch_WithNoHoleCentres_DoesNotAskWhichPointsAreUsed()
    {
        var asked = false;

        HoleSessionRules.CentresOfPickedSketch<Pt>(false, true, () => [], _ => asked = true);

        Assert.False(asked);
    }

    [Fact]
    public void PickedSketch_WithNoHoleCentres_KeepsThePicks_AndSaysSo()
    {
        var result = Clicked(WithCentres("empty"));

        Assert.False(result.Replaces);
        Assert.Equal(PickedSketchOutcome.NoHoleCentres, result.Outcome);
        Assert.Empty(result.Points);
        Assert.Equal("empty was clicked in the browser but has no hole centres; the picks are kept.", result.Describe("empty"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PickedSketch_OfAnotherDocument_KeepsThePicks_WithoutReadingTheSketch_AndSaysSo(bool edit)
    {
        var read = false;
        var result = HoleSessionRules.CentresOfPickedSketch<Pt>(
            edit,
            inSessionPart: false,
            () =>
            {
                read = true;
                return [new Pt("free a")];
            },
            _ => false);

        Assert.False(result.Replaces);
        Assert.Equal(PickedSketchOutcome.OtherDocument, result.Outcome);
        Assert.Empty(result.Points);
        Assert.False(read);
        Assert.Equal(
            "Sketch1 was clicked in the browser but is not in the part the hole is made in; the picks are kept.",
            result.Describe("Sketch1"));
    }

    [Fact]
    public void PickedSketch_WhenEditing_KeepsThePicks_WithoutReadingTheSketch()
    {
        var read = false;
        var result = HoleSessionRules.CentresOfPickedSketch<Pt>(
            edit: true,
            inSessionPart: true,
            () =>
            {
                read = true;
                return [new Pt("free a")];
            },
            _ => false);

        Assert.False(result.Replaces);
        Assert.Equal(PickedSketchOutcome.Editing, result.Outcome);
        Assert.False(read);
        Assert.StartsWith("a sketch was clicked in the browser", result.Describe(null));
    }

    [Fact]
    public void Picks_Replace_WithAnotherSketch_TakesItsPointsAndSaysTheSketchChanged()
    {
        var first = new Sketch("first");
        var other = new Sketch("other");
        var c = new Pt("c");
        var d = new Pt("d");
        var picks = new HoleCentrePicks<Pt, Sketch>(edit: false);
        picks.Start(first, [new Pt("a"), new Pt("b")]);

        Assert.True(picks.Replace(other, [c, d]));
        Assert.Same(other, picks.Sketch);
        Assert.Equal([c, d], picks.Points);
    }

    [Fact]
    public void Picks_Replace_WithTheSameSketch_TakesItsPointsInTheSameSketch()
    {
        var sketch = new Sketch("s");
        var a = new Pt("a");
        var b = new Pt("b");
        var picks = new HoleCentrePicks<Pt, Sketch>(edit: false, sameSketch: (x, y) => x.Name == y.Name);
        picks.Start(sketch, [a]);

        Assert.False(picks.Replace(new Sketch("s"), [a, b]));
        Assert.Equal([a, b], picks.Points);
    }

    [Fact]
    public void Picks_Replace_WithNoSketchYet_SaysTheSketchChanged()
    {
        var picks = new HoleCentrePicks<Pt, Sketch>(edit: false);
        picks.Start(null, []);

        Assert.True(picks.Replace(new Sketch("s"), [new Pt("a")]));
        Assert.Equal(1, picks.Count);
    }

    [Fact]
    public void Picks_Replace_IsNotForAnEdit()
    {
        var picks = new HoleCentrePicks<Pt, Sketch>(edit: true);

        Assert.Throws<InvalidOperationException>(() => picks.Replace(new Sketch("s"), [new Pt("a")]));
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
