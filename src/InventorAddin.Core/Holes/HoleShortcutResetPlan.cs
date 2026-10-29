namespace InventorAddin.Core.Holes;

/// <summary>What the add-in does after telling <see cref="HoleShortcutResetPlan"/> what it saw.</summary>
public enum HoleShortcutResetAction
{
    /// <summary>Nothing yet; check again at the next tick.</summary>
    Wait,

    /// <summary>Put the Hole commands right again (<see cref="HoleTakeoverRules.ResetReapplyOrder"/>), then keep checking.</summary>
    ReApply,

    /// <summary>The plan has stopped; stop the timer.</summary>
    Done,
}

/// <summary>A step of <see cref="HoleShortcutResetPlan"/>.</summary>
/// <param name="Action">What to do.</param>
/// <param name="LogMessage">The line to log for this step, or null for none.</param>
/// <param name="IsWarning">Log <paramref name="LogMessage"/> as a warning rather than as information.</param>
public readonly record struct HoleShortcutResetStep(HoleShortcutResetAction Action, string? LogMessage, bool IsWarning = false);

/// <summary>
/// After the user resets shortcuts (Tools > Customize > Keyboard > Reset All), decides when the Hole takeover is applied
/// again, so H opens the Hole command the setting names (spec 09, "The command"; task 120).
/// </summary>
/// <remarks>
/// <para>
/// Inventor fires <c>OnResetShortcuts</c> while the Customize dialog is still open, and the dialog writes its own
/// shortcut table when it closes. Applied at the event, the takeover was overwritten (check 091-3: the log shows it was
/// applied, yet H opened Inventor's Hole). So the plan waits until Inventor's main window is enabled again (a modal
/// dialog disables it), re-applies, and then watches the shortcuts for <see cref="SettleTime"/>, re-applying if they
/// change, at most <see cref="MaxReApplies"/> times in all.
/// </para>
/// <para>
/// An unreadable window state counts as enabled, and unreadable shortcuts as unchanged, so a read that fails never
/// keeps the plan waiting or re-applying.
/// </para>
/// </remarks>
public sealed class HoleShortcutResetPlan
{
    /// <summary>How often the add-in checks, while the plan is watching.</summary>
    public static readonly TimeSpan CheckInterval = TimeSpan.FromMilliseconds(500);

    /// <summary>How long after re-applying the shortcuts are watched before the plan stops.</summary>
    public static readonly TimeSpan SettleTime = TimeSpan.FromSeconds(5);

    /// <summary>How long the plan waits for the dialog to close before it gives up.</summary>
    public static readonly TimeSpan GiveUpAfter = TimeSpan.FromMinutes(30);

    /// <summary>The most times the plan re-applies after one reset.</summary>
    public const int MaxReApplies = 3;

    private const string Prefix = "Hole takeover (shortcuts reset):";

    private enum State
    {
        Idle,
        WaitingForDialog,
        Settling,
    }

    private State _state = State.Idle;
    private TimeSpan _reappliedAt;

    /// <summary>True from a reset until the plan stops.</summary>
    public bool IsWatching => _state != State.Idle;

    /// <summary>How many times the plan has asked to re-apply since the last reset.</summary>
    public int ReApplies { get; private set; }

    /// <summary>
    /// Inventor reported a shortcut reset. Starts the watch, or starts it over when one is running; the time passed to
    /// <see cref="Check"/> is from this reset.
    /// </summary>
    /// <param name="inventorWindowEnabled">Whether Inventor's main window is enabled, or null when unreadable.</param>
    /// <param name="shortcuts">The two Hole commands' shortcuts now (<see cref="HoleTakeoverRules.ShortcutsMessage"/>).</param>
    public HoleShortcutResetStep Reset(bool? inventorWindowEnabled, string shortcuts)
    {
        var again = IsWatching ? " again, before the last one was finished" : string.Empty;
        _state = State.WaitingForDialog;
        ReApplies = 0;
        _reappliedAt = TimeSpan.Zero;

        var window = inventorWindowEnabled switch
        {
            false => "Inventor's window is disabled, so the Customize dialog is open",
            true => "Inventor's window is enabled",
            null => "whether Inventor's window is enabled could not be read",
        };

        return new HoleShortcutResetStep(
            HoleShortcutResetAction.Wait,
            $"{Prefix} Inventor reported a shortcut reset{again}; {window}. Shortcuts now: {shortcuts}. " +
            "The Hole command is put back once the dialog has closed.");
    }

    /// <summary>A tick of the add-in's timer.</summary>
    /// <param name="sinceReset">Time since the last <see cref="Reset"/>.</param>
    /// <param name="inventorWindowEnabled">Whether Inventor's main window is enabled, or null when unreadable.</param>
    /// <param name="shortcutsApplied">
    /// <see cref="HoleTakeoverRules.ShortcutsApplied"/> for the owner the setting names, or null when unreadable.
    /// </param>
    /// <param name="shortcuts">The two Hole commands' shortcuts now (<see cref="HoleTakeoverRules.ShortcutsMessage"/>).</param>
    public HoleShortcutResetStep Check(TimeSpan sinceReset, bool? inventorWindowEnabled, bool? shortcutsApplied, string shortcuts)
    {
        switch (_state)
        {
            case State.WaitingForDialog:
                if (inventorWindowEnabled == false)
                {
                    return sinceReset >= GiveUpAfter
                        ? Stop(
                            $"{Prefix} gave up after {GiveUpAfter.TotalMinutes:0} min waiting for the Customize dialog to close. " +
                            $"Shortcuts now: {shortcuts}. Save Settings, or restart Inventor, to give H to the right Hole.",
                            isWarning: true)
                        : Wait();
                }

                if (ReApplies >= MaxReApplies)
                {
                    return Stop(
                        $"{Prefix} a dialog closed after {ReApplies} re-applies; not re-applying again. " +
                        $"Shortcuts now: {shortcuts}. Save Settings, or restart Inventor, if H opens the wrong Hole.",
                        isWarning: true);
                }

                return ReApply(
                    sinceReset,
                    $"{Prefix} the dialog has closed ({sinceReset.TotalSeconds:0.0} s after the reset). " +
                    $"Shortcuts before re-applying: {shortcuts}. Re-applying.");

            case State.Settling:
                if (inventorWindowEnabled == false)
                {
                    _state = State.WaitingForDialog;
                    return new HoleShortcutResetStep(
                        HoleShortcutResetAction.Wait,
                        $"{Prefix} Inventor's window is disabled again; waiting for the dialog to close.");
                }

                if (shortcutsApplied == false)
                {
                    return ReApplies < MaxReApplies
                        ? ReApply(
                            sinceReset,
                            $"{Prefix} the shortcuts changed after re-applying ({shortcuts}). Re-applying again.")
                        : Stop(
                            $"{Prefix} the shortcuts are still not right after {ReApplies} re-applies ({shortcuts}). " +
                            "Save Settings, or restart Inventor, to give H to the right Hole.",
                            isWarning: true);
                }

                if (sinceReset - _reappliedAt >= SettleTime)
                {
                    var read = shortcutsApplied == null ? " (could not be checked)" : string.Empty;
                    return Stop(
                        $"{Prefix} done; shortcuts {SettleTime.TotalSeconds:0} s after re-applying: {shortcuts}{read}.",
                        isWarning: false);
                }

                return Wait();

            default:
                return new HoleShortcutResetStep(HoleShortcutResetAction.Done, null);
        }
    }

    /// <summary>The add-in is unloading or the watch failed: the plan stops. True when it was watching.</summary>
    public bool Cancel()
    {
        var was = IsWatching;
        _state = State.Idle;
        return was;
    }

    private HoleShortcutResetStep ReApply(TimeSpan sinceReset, string message)
    {
        _state = State.Settling;
        _reappliedAt = sinceReset;
        ReApplies++;
        return new HoleShortcutResetStep(HoleShortcutResetAction.ReApply, message);
    }

    private HoleShortcutResetStep Stop(string message, bool isWarning)
    {
        _state = State.Idle;
        return new HoleShortcutResetStep(HoleShortcutResetAction.Done, message, isWarning);
    }

    private static HoleShortcutResetStep Wait() => new(HoleShortcutResetAction.Wait, null);
}
