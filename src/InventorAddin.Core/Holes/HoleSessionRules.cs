namespace InventorAddin.Core.Holes;

/// <summary>
/// The rules of the AWB Hole session (task 089; spec 09, "The window"; spike 081 rounds 2, 4 and 6): how the hole
/// centres are picked, how the preview follows the window, and what the session leaves behind. The add-in's
/// <c>HoleSession</c> reads Inventor, asks these, and applies the answer.
/// </summary>
public static class HoleSessionRules
{
    private const string CommandName = Branding.ShortName + " Hole";

    /// <summary>
    /// The colour of the picked hole centres: blue, the same in the light and dark themes, since Inventor draws the
    /// highlight over the model, not over the window (spike 081 round 2).
    /// </summary>
    public static HoleColour PickedColour { get; } = new(30, 120, 255);

    /// <summary>Inventor's status bar while the window is open.</summary>
    public const string StatusBarText = CommandName + ": click a point to add or remove a hole centre.";

    /// <summary>A preview change slower than this is logged at Info.</summary>
    public const int SlowChangeMs = 500;

    /// <summary>The name of each preview change's inner transaction; it never reaches the Undo list.</summary>
    public const string StepTransactionName = CommandName + " step";

    /// <summary>
    /// The window did not apply what it shows (the last change was refused or could not be checked), so OK keeps the
    /// window open.
    /// </summary>
    public const string NotAppliedText =
        "The hole in the model is not what the window shows: change the values Inventor refused, or Cancel.";

    /// <summary>OK could not apply the tolerance, colour or stored data; the window stays open.</summary>
    public static string FinishFailed(string? reason)
    {
        var why = (reason ?? "").Trim().TrimEnd('.');
        return why.Length == 0
            ? "The hole's tolerance, colour or data could not be applied."
            : $"The hole's tolerance, colour or data could not be applied ({why}).";
    }

    /// <summary>The Undo list entry for the whole session: <c>AWB Hole</c>, or <c>Edit AWB Hole</c> for an edit.</summary>
    public static string TransactionName(bool edit) => edit ? "Edit " + CommandName : CommandName;

    /// <summary>True when a preview change took long enough to log at Info.</summary>
    public static bool IsSlow(TimeSpan elapsed) => elapsed.TotalMilliseconds > SlowChangeMs;

    /// <summary>
    /// Whether a sketch is visible when the session ends. A new hole's sketch ends hidden when the hole is kept, as
    /// Inventor leaves a sketch its hole consumed; otherwise (Cancel, or an edit) it goes back as it was.
    /// </summary>
    /// <param name="newHole">The session made a new hole (not an edit).</param>
    /// <param name="kept">The session ended with OK.</param>
    /// <param name="wasVisible">Whether the sketch was visible when the window opened.</param>
    public static bool SketchVisibleAfter(bool newHole, bool kept, bool wasVisible) => !(newHole && kept) && wasVisible;

    /// <summary>
    /// The sketch whose hole centres a new hole starts with, as Inventor's Hole picks it: the sketch the user
    /// selected, else the sketch of the points the user selected, else the newest visible sketch with hole-centre
    /// points. Null when there is none.
    /// </summary>
    /// <param name="selectedSketch">The first selected sketch, or null.</param>
    /// <param name="sketchOfSelectedPoint">The sketch of the first selected sketch point, or null.</param>
    /// <param name="sketchesNewestFirst">The part's sketches, newest first, with whether each is visible and has a
    /// point marked as a hole centre. Read only as far as the first that qualifies, so the add-in can work the flags
    /// out lazily.</param>
    public static TSketch? DefaultSketch<TSketch>(
        TSketch? selectedSketch,
        TSketch? sketchOfSelectedPoint,
        IEnumerable<(TSketch Sketch, Func<bool> Visible, Func<bool> HasHoleCentres)> sketchesNewestFirst)
        where TSketch : class
    {
        ArgumentNullException.ThrowIfNull(sketchesNewestFirst);

        if (selectedSketch is not null)
        {
            return selectedSketch;
        }

        if (sketchOfSelectedPoint is not null)
        {
            return sketchOfSelectedPoint;
        }

        foreach (var (sketch, visible, hasHoleCentres) in sketchesNewestFirst)
        {
            if (visible() && hasHoleCentres())
            {
                return sketch;
            }
        }

        return null;
    }

    /// <summary>What the preview does for a window change.</summary>
    /// <param name="hasHole">The session holds a hole (the edited hole, or a preview hole it made).</param>
    /// <param name="edit">The session edits an existing hole.</param>
    /// <param name="centres">The number of picked hole centres.</param>
    /// <param name="hasGeometry">The window's values build (<c>AwbHoleViewModel.Geometry</c> is set).</param>
    /// <param name="sketchChanged">The picks moved to another sketch since the held hole was made (a new hole only).
    /// Moving a hole's centres to another sketch has not been tried in Inventor, so the preview hole is removed and
    /// made again in the new sketch.</param>
    public static HolePreviewAction Plan(bool hasHole, bool edit, int centres, bool hasGeometry, bool sketchChanged = false)
    {
        if (sketchChanged && hasHole && !edit)
        {
            return centres > 0 && hasGeometry ? HolePreviewAction.RemoveThenCreate : HolePreviewAction.Remove;
        }

        if (centres <= 0)
        {
            // No centres, no preview hole. An edited hole cannot be removed and made again without losing its
            // place in the browser, so it stays at its last centres until one is picked (OK needs one anyway).
            return hasHole && !edit ? HolePreviewAction.Remove : HolePreviewAction.None;
        }

        if (!hasGeometry)
        {
            // The window shows the problem beside its field; the hole stays at its last good state.
            return HolePreviewAction.None;
        }

        return hasHole ? HolePreviewAction.Change : HolePreviewAction.Create;
    }

    /// <summary>
    /// What to do with a create or change once the writer has run, in its inner transaction (task 088 follow-ups;
    /// spike 081 round 6).
    /// </summary>
    /// <param name="action">The action that ran: <see cref="HolePreviewAction.Create"/> or
    /// <see cref="HolePreviewAction.Change"/>.</param>
    /// <param name="succeeded">The writer's <c>Succeeded</c>.</param>
    /// <param name="recreated">The writer's <c>Recreated</c>.</param>
    public static HolePreviewStep AfterWrite(HolePreviewAction action, bool succeeded, bool recreated)
    {
        if (action is not (HolePreviewAction.Create or HolePreviewAction.Change))
        {
            throw new ArgumentOutOfRangeException(nameof(action), action, "Only a create or a change writes a hole.");
        }

        if (succeeded)
        {
            // A new hole, or a hole made again, is a new object; a change in place keeps the one held.
            return new HolePreviewStep(
                EndStep: true,
                HoldNewHole: action == HolePreviewAction.Create || recreated,
                FindHoleAgain: false);
        }

        // Refused, or made but unhealthy: abort only this step, so the hole is back at its last good state. A
        // failed recreate deleted the held hole; after the abort it is found again (by name), not trusted.
        return new HolePreviewStep(
            EndStep: false,
            HoldNewHole: false,
            FindHoleAgain: action == HolePreviewAction.Change && recreated);
    }
}

/// <summary>What the preview does for one window change.</summary>
public enum HolePreviewAction
{
    /// <summary>Nothing: no centres for an edited hole, or values that do not build.</summary>
    None,

    /// <summary>Make the preview hole.</summary>
    Create,

    /// <summary>Change the held hole.</summary>
    Change,

    /// <summary>Delete the preview hole: a new hole with no centres left, or centres moved to another sketch.</summary>
    Remove,

    /// <summary>
    /// A new hole whose centres moved to another sketch: delete the preview hole, then make it again in the new
    /// sketch.
    /// </summary>
    RemoveThenCreate,
}

/// <summary>What the session does after the writer ran in an inner transaction.</summary>
/// <param name="EndStep">End the inner transaction (true) or abort it (false).</param>
/// <param name="HoldNewHole">Hold the result's hole in place of the one held.</param>
/// <param name="FindHoleAgain">After the abort, find the held hole again by its name.</param>
public sealed record HolePreviewStep(bool EndStep, bool HoldNewHole, bool FindHoleAgain);

/// <summary>What a click on a sketch point did to the picked centres.</summary>
public enum HoleCentrePick
{
    /// <summary>The point was not picked; now it is.</summary>
    Added,

    /// <summary>The point was picked; now it is not.</summary>
    Removed,

    /// <summary>
    /// A new hole: the point is in another sketch than the picked centres, so the picks start again in that sketch
    /// with only this point (Default, task 089: one feature holds centres of one sketch).
    /// </summary>
    SketchChanged,

    /// <summary>
    /// An edited hole: the point is not in the hole's sketch, or the hole has no sketch (its centres are not sketch
    /// points), so the click is ignored (spec 09).
    /// </summary>
    Ignored,
}

/// <summary>
/// The picked hole centres of one AWB Hole session: the sketch they are in and the points, in pick order. A click
/// toggles a point, without Ctrl (spec 09, "Hole centres"). Generic so the add-in can hold Inventor's sketch points
/// and the tests plain values.
/// </summary>
public sealed class HoleCentrePicks<TPoint, TSketch>
    where TPoint : class
    where TSketch : class
{
    private readonly List<TPoint> _points = [];
    private readonly Func<TPoint, TPoint, bool> _samePoint;
    private readonly Func<TSketch, TSketch, bool> _sameSketch;

    /// <param name="edit">An edit: only points of the hole's sketch can be added or removed.</param>
    /// <param name="samePoint">Whether two points are the same point; reference equality when null.</param>
    /// <param name="sameSketch">Whether two sketches are the same sketch; reference equality when null.</param>
    public HoleCentrePicks(
        bool edit,
        Func<TPoint, TPoint, bool>? samePoint = null,
        Func<TSketch, TSketch, bool>? sameSketch = null)
    {
        IsEdit = edit;
        _samePoint = samePoint ?? ReferenceEquals;
        _sameSketch = sameSketch ?? ReferenceEquals;
    }

    public bool IsEdit { get; }

    /// <summary>The sketch the centres are in; null until one is known.</summary>
    public TSketch? Sketch { get; private set; }

    public IReadOnlyList<TPoint> Points => _points;

    public int Count => _points.Count;

    /// <summary>Starts the picks: the default sketch's hole centres, or the edited hole's own centres.</summary>
    public void Start(TSketch? sketch, IEnumerable<TPoint> points)
    {
        ArgumentNullException.ThrowIfNull(points);

        Sketch = sketch;
        _points.Clear();
        foreach (var point in points)
        {
            if (point is not null && IndexOf(point) < 0)
            {
                _points.Add(point);
            }
        }
    }

    /// <summary>A click on <paramref name="point"/>, a point of <paramref name="sketch"/>.</summary>
    public HoleCentrePick Toggle(TPoint point, TSketch? sketch)
    {
        ArgumentNullException.ThrowIfNull(point);

        var index = IndexOf(point);
        if (index >= 0)
        {
            _points.RemoveAt(index);
            return HoleCentrePick.Removed;
        }

        if (IsEdit && Sketch is null)
        {
            // An edited hole whose centres are not sketch points: there is no sketch to pick in.
            return HoleCentrePick.Ignored;
        }

        var inSketch = Sketch is null || (sketch is not null && _sameSketch(Sketch, sketch));
        if (!inSketch)
        {
            if (IsEdit)
            {
                return HoleCentrePick.Ignored;
            }

            Sketch = sketch;
            _points.Clear();
            _points.Add(point);
            return HoleCentrePick.SketchChanged;
        }

        Sketch ??= sketch;
        _points.Add(point);
        return HoleCentrePick.Added;
    }

    private int IndexOf(TPoint point) => _points.FindIndex(p => _samePoint(p, point));
}
