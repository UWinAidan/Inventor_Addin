using InventorAddin.Core.ViewModels;

namespace InventorAddin.Core.HoleTable;

/// <summary>
/// What the add-in sees when it asks whether a due check may run (see <see cref="HoleTableAutoCheck.TakeDue"/>).
/// </summary>
/// <param name="ActiveDrawing">The drawing that is the active document, or null when the active document is not a drawing.</param>
/// <param name="EditingDrawingItself">True when the drawing itself is in edit, not a sketch or another object.</param>
/// <param name="CommandRunning">True while any Inventor command is running.</param>
/// <param name="AwbWindowOpen">True while any AWB window is open.</param>
public sealed record HoleTableAutoCheckContext(
    string? ActiveDrawing,
    bool EditingDrawingItself,
    bool CommandRunning,
    bool AwbWindowOpen);

/// <summary>
/// The policy of the automatic out-of-date check (task 175; spec 04, "Update"): when a check is due, whether it may run
/// now, whether its result is shown, and what remembering "Later" means. Time and the state of Inventor are passed in, so
/// it is testable. The add-in (task 176) feeds it events and a timer tick, runs <see cref="HoleTableStoredCheck.Run"/>
/// when told to, and shows <see cref="HoleTableOutOfDatePromptViewModel"/>.
/// </summary>
/// <remarks>
/// A drawing is identified by a string the add-in chooses (its full file name, or the document's internal name when it is
/// unsaved), compared ignoring case. Not thread safe: call it from the one thread that handles Inventor events.
/// </remarks>
public sealed class HoleTableAutoCheck
{
    /// <summary>How long after the last activation or update of a drawing its check waits (spec 04, open question default).</summary>
    public static readonly TimeSpan QuietTime = TimeSpan.FromSeconds(1.5);

    private readonly Dictionary<string, DateTime> _due = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, HashSet<string>> _later = new(StringComparer.OrdinalIgnoreCase);
    private bool _enabled;

    public HoleTableAutoCheck(bool enabled = true)
    {
        _enabled = enabled;
    }

    /// <summary>
    /// The Settings switch. Setting it off drops every pending check; events that arrive while it is off are ignored.
    /// </summary>
    public bool Enabled
    {
        get => _enabled;
        set
        {
            _enabled = value;
            if (!value)
            {
                _due.Clear();
            }
        }
    }

    /// <summary>A drawing became the active document. Makes its check due after the quiet time.</summary>
    public void DrawingActivated(string drawing, DateTime now) => MakeDue(drawing, now);

    /// <summary>
    /// A drawing updated. The add-in passes only updates of the drawing's references (task 176). Makes its check due after
    /// the quiet time; a second event restarts the wait.
    /// </summary>
    public void DrawingUpdated(string drawing, DateTime now) => MakeDue(drawing, now);

    /// <summary>A drawing closed: its pending check and its remembered Later are forgotten.</summary>
    public void DrawingClosed(string drawing)
    {
        _due.Remove(drawing);
        _later.Remove(drawing);
    }

    /// <summary>Whether a check is waiting for this drawing (due now or after the quiet time).</summary>
    public bool IsPending(string drawing) => _due.ContainsKey(drawing);

    /// <summary>
    /// Called on each tick. Returns the drawing whose check should run now, or null. A check runs only when the quiet time
    /// has passed since the drawing's last event, the drawing is the active document and is being edited itself, no
    /// command is running and no AWB window is open; until then it stays due. Returning a drawing takes its check off the
    /// due list.
    /// </summary>
    public string? TakeDue(HoleTableAutoCheckContext context, DateTime now)
    {
        if (!_enabled
            || context.ActiveDrawing is not { } active
            || !context.EditingDrawingItself
            || context.CommandRunning
            || context.AwbWindowOpen
            || !_due.TryGetValue(active, out var last)
            || now - last < QuietTime)
        {
            return null;
        }

        _due.Remove(active);
        return active;
    }

    /// <summary>
    /// After a check: whether to show the prompt. True only when at least one view is out of date and the out-of-date
    /// result differs from the one last answered with Later for this drawing. Views that could not be checked do not
    /// prompt (the add-in logs them). A check that finds nothing out of date forgets the remembered Later, so a view that
    /// goes out of date again asks again.
    /// </summary>
    public bool ShouldPrompt(string drawing, HoleTableStoredCheckResult result)
    {
        if (result.OutOfDate.Count == 0)
        {
            _later.Remove(drawing);
            return false;
        }

        return !(_later.TryGetValue(drawing, out var answered) && answered.SetEquals(Signature(result.OutOfDate)));
    }

    /// <summary>The user chose Later: this result is not asked about again for this drawing in this Inventor session.</summary>
    public void Later(string drawing, IEnumerable<HoleTableStoredViewCheck> outOfDate) =>
        _later[drawing] = Signature(outOfDate);

    /// <summary>The user chose Update now: forget any remembered Later, so the same result later asks again.</summary>
    public void UpdatedNow(string drawing) => _later.Remove(drawing);

    /// <summary>
    /// The Hole Table window closed: it has already asked about its stored tables, so the add-in runs the check once more
    /// without prompting and passes the result here, which counts as answered with Later.
    /// </summary>
    public void WizardClosed(string drawing, HoleTableStoredCheckResult result)
    {
        if (result.OutOfDate.Count == 0)
        {
            _later.Remove(drawing);
        }
        else
        {
            Later(drawing, result.OutOfDate);
        }
    }

    /// <summary>The result as a set of (sheet key, view key, reason text) so order does not matter.</summary>
    private static HashSet<string> Signature(IEnumerable<HoleTableStoredViewCheck> views) =>
        views.Select(v => string.Join("\u001f", v.SheetKey, v.ViewKey, v.Reason)).ToHashSet(StringComparer.Ordinal);

    private void MakeDue(string drawing, DateTime now)
    {
        if (_enabled)
        {
            _due[drawing] = now;
        }
    }
}
