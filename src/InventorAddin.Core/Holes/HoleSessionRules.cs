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
    public const string StatusBarText =
        CommandName + ": click a point to add or remove a hole centre, or a sketch in the browser to pick its centres.";

    /// <summary>A preview change slower than this is logged at Info.</summary>
    public const int SlowChangeMs = 500;

    /// <summary>The name of each preview change's inner transaction; it never reaches the Undo list.</summary>
    public const string StepTransactionName = CommandName + " step";

    /// <summary>Drawing the preview graphics slower than this is logged at Info (tasks 101 and 110).</summary>
    public const int SlowDrawMs = 30;

    /// <summary>True when drawing the preview graphics took long enough to log at Info.</summary>
    public static bool IsSlowDraw(TimeSpan elapsed) => elapsed.TotalMilliseconds > SlowDrawMs;

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
    /// Inventor leaves a sketch its hole consumed; otherwise (Cancel, an edit, or a sketch that is shared at the end)
    /// it goes back as it was. Inventor does not hide a shared sketch when a feature consumes it (task 094).
    /// </summary>
    /// <param name="newHole">The session made a new hole (not an edit).</param>
    /// <param name="kept">The session ended with OK.</param>
    /// <param name="wasVisible">Whether the sketch was visible when the window opened.</param>
    /// <param name="sharedAfter">Whether the sketch is shared once the session has put it back (see
    /// <see cref="UnshareAfter"/>): it was shared before, or another feature uses it too.</param>
    public static bool SketchVisibleAfter(bool newHole, bool kept, bool wasVisible, bool sharedAfter = false) =>
        !(newHole && kept && !sharedAfter) && wasVisible;

    /// <summary>
    /// Whether the session sets a sketch back to not shared when it ends (task 094): the sketch is shared now but was
    /// not when the window opened. Inventor shares a sketch once a second feature consumes it (an AWB hole on a sketch
    /// another hole already uses), and leaves it shared when that second hole is removed again; with one feature left
    /// it can be unshared, with two Inventor refuses and the sketch stays shared, as with Inventor's Hole. A sketch
    /// shared before the window opened stays shared. After Cancel the session's transaction has already put it back.
    /// </summary>
    /// <param name="wasShared">Whether the sketch was shared when the window opened.</param>
    /// <param name="isShared">Whether it is shared now.</param>
    public static bool UnshareAfter(bool wasShared, bool isShared) => isShared && !wasShared;

    /// <summary>
    /// Whether OK puts every face of the hole back to the part's appearance (task 096; spec 09 question 8, Aidan): only
    /// when an edited hole was a dowel or precision hole when the window opened and is kept as another kind. The
    /// reset covers every face, whatever appearance it had. A hole that never was a dowel keeps the colours a user put
    /// on its faces, and a new hole has nothing to reset.
    /// </summary>
    /// <param name="edit">The session edits an existing hole.</param>
    /// <param name="openedKind">The kind the window opened with: from the hole's stored data, or read back from its
    /// geometry for a hole made by Inventor's Hole.</param>
    /// <param name="openedFitColoured">A face of the hole carried a fit appearance when the window opened.</param>
    /// <param name="keptKind">The kind the hole is kept as.</param>
    public static bool ResetsFaceAppearances(bool edit, HoleKind openedKind, bool openedFitColoured, HoleKind keptKind) =>
        ResetsFaceAppearances(edit, openedKind == HoleKind.Dowel, openedFitColoured, keptKind == HoleKind.Dowel);

    /// <summary>
    /// As <see cref="ResetsFaceAppearances(bool, HoleKind, bool, HoleKind)"/>, by the precision rule (task 113;
    /// <see cref="HolePrecision.IsPrecision(HoleSpec)"/>): an edited hole that was a precision hole (a dowel, or a
    /// toleranced Clearance or Simple hole), or carried a fit colour, when the window opened, and is kept as a hole that
    /// is not one, has every face put back to the part's appearance. Unticking Toleranced is such a change, as is moving
    /// a toleranced hole to a type that cannot be toleranced.
    /// </summary>
    /// <param name="edit">The session edits an existing hole.</param>
    /// <param name="openedPrecision">The hole the window opened on was a precision hole.</param>
    /// <param name="openedFitColoured">A face of the hole carried a fit appearance when the window opened.</param>
    /// <param name="keptPrecision">The hole is kept as a precision hole.</param>
    public static bool ResetsFaceAppearances(bool edit, bool openedPrecision, bool openedFitColoured, bool keptPrecision) =>
        edit && (openedPrecision || openedFitColoured) && !keptPrecision;

    /// <summary>
    /// Whether a document being deactivated ends the session as Cancel does (spec 09, "The command"; spike 103 round
    /// 2; task 105): the user switched away from the document that was active when the session began, which is the
    /// assembly when the part is edited in place and the part in its own window. Only the event before the switch
    /// counts. The full document names are compared ignoring case; a name that is empty matches nothing.
    /// </summary>
    /// <param name="sessionDocument">The full document name of the document active when the session began.</param>
    /// <param name="deactivating">The full document name of the document being deactivated.</param>
    /// <param name="before">The event is the one raised before the switch.</param>
    public static bool EndsOnDeactivate(string? sessionDocument, string? deactivating, bool before) =>
        before
        && !string.IsNullOrEmpty(sessionDocument)
        && string.Equals(sessionDocument, deactivating, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The hole centres a new hole starts with, as Inventor's Hole picks them (task 117; spec 09, "Hole centres";
    /// Aidan, 2026-10-24, question 11): the sketch the user selected, with its free hole centres (those no feature uses
    /// yet), or all of them when every one is used (task 154; Aidan, 2026-11-10, checklist 117-4; a selection wins,
    /// task 094); else the sketch of the points the user selected, with all its hole centres, used or not; else, per
    /// point, the newest visible sketch that has hole-centre points no feature uses yet, with only those points. There
    /// a sketch whose centres are all used is skipped for the next newest. <see cref="DefaultCentresFrom.None"/> when
    /// there is none.
    /// </summary>
    /// <param name="selectedSketch">The first selected sketch, or null.</param>
    /// <param name="sketchOfSelectedPoint">The sketch of the first selected sketch point, or null.</param>
    /// <param name="sketchesNewestFirst">The part's sketches, newest first, with whether each is visible. Read only
    /// as far as the first that qualifies, and visibility before anything else, so the add-in can work it out
    /// lazily.</param>
    /// <param name="holeCentres">A sketch's points marked as hole centres, in sketch order. Asked only for the
    /// chosen selection or for a visible sketch.</param>
    /// <param name="usedByFeature">Whether a feature already uses a hole-centre point. Asked for a selected sketch's
    /// centres and when nothing is selected, never for the sketch of a selected point.</param>
    public static DefaultCentres<TSketch, TPoint> DefaultCentres<TSketch, TPoint>(
        TSketch? selectedSketch,
        TSketch? sketchOfSelectedPoint,
        IEnumerable<(TSketch Sketch, Func<bool> Visible)> sketchesNewestFirst,
        Func<TSketch, IEnumerable<TPoint>> holeCentres,
        Func<TPoint, bool> usedByFeature)
        where TSketch : class
    {
        ArgumentNullException.ThrowIfNull(sketchesNewestFirst);
        ArgumentNullException.ThrowIfNull(holeCentres);
        ArgumentNullException.ThrowIfNull(usedByFeature);

        if (selectedSketch is not null)
        {
            var all = holeCentres(selectedSketch).ToList();
            var (points, allUsed) = FreeOrAll(all, usedByFeature);
            return new(selectedSketch, points, DefaultCentresFrom.SelectedSketch, all.Count, allUsed);
        }

        if (sketchOfSelectedPoint is not null)
        {
            var all = holeCentres(sketchOfSelectedPoint).ToList();
            return new(sketchOfSelectedPoint, all, DefaultCentresFrom.SketchOfSelectedPoints, all.Count);
        }

        foreach (var (sketch, visible) in sketchesNewestFirst)
        {
            if (!visible())
            {
                continue;
            }

            var all = holeCentres(sketch).ToList();
            var free = all.Where(point => !usedByFeature(point)).ToList();
            if (free.Count > 0)
            {
                return new(sketch, free, DefaultCentresFrom.NewestWithFreeCentres, all.Count);
            }
        }

        return new(null, [], DefaultCentresFrom.None, 0);
    }

    /// <summary>
    /// The hole centres a sketch clicked in the browser while the window is open gives (task 145, changed by task 154;
    /// spec 09, "Hole centres"; Aidan, 2026-11-10, checklist 145-3): its free hole-centre points, those no feature
    /// uses yet, or all of them when every one is used, as for a sketch selected before the window opens. A used point
    /// can still be picked by clicking it. They replace the picks. A sketch with none leaves the picks as they were,
    /// and so do a sketch of another document (another part, or the assembly itself, when the part is edited in place)
    /// and an edit, whose centres stay in the hole's own sketch.
    /// </summary>
    /// <param name="edit">The session edits an existing hole.</param>
    /// <param name="inSessionPart">The sketch belongs to the part the session works on.</param>
    /// <param name="holeCentres">The sketch's points marked as hole centres, in sketch order. Asked only for a sketch
    /// of the session's part, and not for an edit.</param>
    /// <param name="usedByFeature">Whether a feature already uses a hole-centre point. Asked only for the sketch's
    /// hole centres.</param>
    public static PickedSketchCentres<TPoint> CentresOfPickedSketch<TPoint>(
        bool edit,
        bool inSessionPart,
        Func<IEnumerable<TPoint>> holeCentres,
        Func<TPoint, bool> usedByFeature)
    {
        ArgumentNullException.ThrowIfNull(holeCentres);
        ArgumentNullException.ThrowIfNull(usedByFeature);

        if (!inSessionPart)
        {
            return new([], PickedSketchOutcome.OtherDocument, 0);
        }

        if (edit)
        {
            return new([], PickedSketchOutcome.Editing, 0);
        }

        var all = holeCentres().ToList();
        if (all.Count == 0)
        {
            return new([], PickedSketchOutcome.NoHoleCentres, 0);
        }

        var (points, allUsed) = FreeOrAll(all, usedByFeature);
        return new(points, PickedSketchOutcome.Replaced, all.Count, allUsed);
    }

    /// <summary>
    /// A chosen sketch's hole centres (task 154): those no feature uses yet, or all of them when every one is used.
    /// <c>AllUsed</c> is true only for a sketch that has hole centres and none of them free.
    /// </summary>
    private static (List<TPoint> Points, bool AllUsed) FreeOrAll<TPoint>(
        List<TPoint> all,
        Func<TPoint, bool> usedByFeature)
    {
        var free = all.Where(point => !usedByFeature(point)).ToList();
        return free.Count > 0 || all.Count == 0 ? (free, false) : (all, true);
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

/// <summary>How the hole centres a new hole starts with were chosen (task 117).</summary>
public enum DefaultCentresFrom
{
    /// <summary>No selection, and no visible sketch has a hole centre no feature uses.</summary>
    None,

    /// <summary>
    /// The sketch the user selected: its hole centres no feature uses, or all of them when every one is used (task 154).
    /// </summary>
    SelectedSketch,

    /// <summary>The sketch of the points the user selected: all its hole centres.</summary>
    SketchOfSelectedPoints,

    /// <summary>Nothing selected: the newest visible sketch with hole centres no feature uses, only those.</summary>
    NewestWithFreeCentres,
}

/// <summary>
/// The hole centres a new hole starts with (task 117; see <see cref="HoleSessionRules.DefaultCentres"/>).
/// </summary>
/// <param name="Sketch">The sketch the centres are in; null when there is none.</param>
/// <param name="Points">The picked centres, in sketch order.</param>
/// <param name="From">How the sketch and points were chosen.</param>
/// <param name="HoleCentresInSketch">How many hole-centre points the sketch has, used or not.</param>
/// <param name="AllUsed">A selected sketch whose hole centres are all used by a feature, so all are picked
/// (task 154).</param>
public sealed record DefaultCentres<TSketch, TPoint>(
    TSketch? Sketch,
    IReadOnlyList<TPoint> Points,
    DefaultCentresFrom From,
    int HoleCentresInSketch,
    bool AllUsed = false)
    where TSketch : class
{
    /// <summary>
    /// For the session's start log line: how the sketch and points were chosen, for example
    /// "newest visible with free centres, 2 of 4 hole centres". Null when there is no sketch.
    /// </summary>
    public string? Describe() => From switch
    {
        DefaultCentresFrom.SelectedSketch when HoleCentresInSketch == 0 => "selected, no hole centres",
        DefaultCentresFrom.SelectedSketch when AllUsed =>
            $"selected, all {HoleCentresInSketch} hole centres are used, so all are picked",
        DefaultCentresFrom.SelectedSketch => $"selected, {Points.Count} free of {HoleCentresInSketch} hole centres",
        DefaultCentresFrom.SketchOfSelectedPoints => $"of the selected points, all {HoleCentresInSketch} hole centres",
        DefaultCentresFrom.NewestWithFreeCentres =>
            $"newest visible with free centres, {Points.Count} of {HoleCentresInSketch} hole centres",
        _ => null,
    };
}

/// <summary>What a sketch clicked in the browser did to the picked centres (task 145).</summary>
public enum PickedSketchOutcome
{
    /// <summary>
    /// The picks are now the sketch's free hole centres, or all of them when every one is used (task 154).
    /// </summary>
    Replaced,

    /// <summary>The sketch has no hole-centre points: the picks are kept.</summary>
    NoHoleCentres,

    /// <summary>
    /// The sketch is in another document than the session's part (another part, or the assembly, in place): the picks
    /// are kept and nothing of that document is changed.
    /// </summary>
    OtherDocument,

    /// <summary>An edited hole keeps its centres in its own sketch: the picks are kept.</summary>
    Editing,
}

/// <summary>
/// The hole centres a sketch clicked in the browser gives (task 145; see
/// <see cref="HoleSessionRules.CentresOfPickedSketch"/>).
/// </summary>
/// <param name="Points">The centres that replace the picks, in sketch order; empty when the picks are kept.</param>
/// <param name="Outcome">Whether the picks are replaced, and why not.</param>
/// <param name="HoleCentresInSketch">How many hole-centre points the sketch has, used or not.</param>
/// <param name="AllUsed">Every hole centre of the sketch is used by a feature, so all are picked (task 154).</param>
public sealed record PickedSketchCentres<TPoint>(
    IReadOnlyList<TPoint> Points,
    PickedSketchOutcome Outcome,
    int HoleCentresInSketch,
    bool AllUsed = false)
{
    /// <summary>True when the picks are replaced by <see cref="Points"/>.</summary>
    public bool Replaces => Outcome == PickedSketchOutcome.Replaced;

    /// <summary>The log line for a click on <paramref name="sketchName"/> in the browser.</summary>
    public string Describe(string? sketchName)
    {
        var name = string.IsNullOrWhiteSpace(sketchName) ? "a sketch" : sketchName;
        return Outcome switch
        {
            PickedSketchOutcome.Replaced when AllUsed =>
                $"{name} was clicked in the browser: all {HoleCentresInSketch} of its hole centres are used, so all are picked.",
            PickedSketchOutcome.Replaced =>
                $"{name} was clicked in the browser: its {Points.Count} free hole centres of {HoleCentresInSketch} are picked.",
            PickedSketchOutcome.NoHoleCentres =>
                $"{name} was clicked in the browser but has no hole centres; the picks are kept.",
            PickedSketchOutcome.OtherDocument =>
                $"{name} was clicked in the browser but is not in the part the hole is made in; the picks are kept.",
            _ => $"{name} was clicked in the browser; an edited hole keeps its centres in its own sketch.",
        };
    }
}

/// <summary>What the session does when Inventor stops the hole centre picking.</summary>
public enum HolePickingStop
{
    /// <summary>Another command started, or Esc in the model: the session is cancelled (Default, task 089).</summary>
    Cancel,

    /// <summary>
    /// The AWB Hole command itself started (its button or shortcut pressed while the window is open): the picking is
    /// started again and the session goes on; the command brings the window to the front (task 090).
    /// </summary>
    Restart,
}

/// <summary>
/// Tells, when Inventor stops the hole centre picking, whether it was stopped by the AWB Hole command itself
/// (task 090). Pressing AWB Hole while its window is open must bring the window to the front, not cancel the
/// session, but starting any command, ours included, may stop the picking first. The add-in reports each command
/// Inventor starts (<c>UserInputEvents.OnActivateCommand</c>); when the picking stops right after AWB Hole started,
/// the picking is restarted.
/// </summary>
/// <remarks>
/// This relies on Inventor reporting the new command before it stops the picking, which is not confirmed. If it is the
/// other way round, the session is cancelled and the command opens a new window: nothing is lost from the part, only
/// the window's settings. <c>CommandManager.ActiveCommand</c> is not used as a second signal: while the picking runs it
/// may itself name AWB Hole, which would turn Esc and every other command into a restart, letting another command run
/// inside the session's transaction.
/// </remarks>
public sealed class HolePickingWatch
{
    private readonly string[] _ownCommands;
    private string? _lastStarted;

    /// <param name="ownCommand">The AWB Hole command's internal name (<c>CommandNames.AwbHole</c>).</param>
    /// <param name="moreOwnCommands">
    /// Other commands that open the window and so restart the picking too: our Edit Feature (task 092), which brings
    /// the open window to the front.
    /// </param>
    public HolePickingWatch(string ownCommand, params string[] moreOwnCommands)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownCommand);
        ArgumentNullException.ThrowIfNull(moreOwnCommands);
        foreach (var command in moreOwnCommands)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(command, nameof(moreOwnCommands));
        }

        _ownCommands = [ownCommand.Trim(), .. moreOwnCommands.Select(c => c.Trim())];
    }

    /// <summary>The last command Inventor reported starting since the picking last stopped or was resumed.</summary>
    public string? LastStarted => _lastStarted;

    /// <summary>Inventor started a command.</summary>
    public void CommandStarted(string? commandName) => _lastStarted = commandName;

    /// <summary>
    /// Inventor stopped the picking: restart when the last command it started is AWB Hole (or another of its own
    /// commands), else cancel. Forgets the last command started, so a later Esc cancels.
    /// </summary>
    public HolePickingStop PickingStopped()
    {
        var own = IsOwn(_lastStarted);
        _lastStarted = null;
        return own ? HolePickingStop.Restart : HolePickingStop.Cancel;
    }

    /// <summary>The picking was started again: forget the last command started.</summary>
    public void Resumed() => _lastStarted = null;

    // Inventor's internal command names are matched exactly, as the ribbon defines them.
    private bool IsOwn(string? name) =>
        _ownCommands.Any(own => string.Equals(name?.Trim(), own, StringComparison.Ordinal));
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

    /// <summary>
    /// A sketch clicked in the browser (task 145): the picks become <paramref name="points"/>, in
    /// <paramref name="sketch"/>. Returns true when the sketch differs from the one the picks were in. A new hole only.
    /// </summary>
    public bool Replace(TSketch sketch, IEnumerable<TPoint> points)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        ArgumentNullException.ThrowIfNull(points);
        if (IsEdit)
        {
            throw new InvalidOperationException("An edited hole keeps its centres in its own sketch.");
        }

        var otherSketch = Sketch is null || !_sameSketch(Sketch, sketch);
        Start(sketch, points);
        return otherSketch;
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
