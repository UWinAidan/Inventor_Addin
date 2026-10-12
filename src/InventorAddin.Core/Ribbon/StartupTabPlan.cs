namespace InventorAddin.Core.Ribbon;

/// <summary>Why <see cref="StartupTabPlan"/> stopped.</summary>
public enum StartupTabStopReason
{
    /// <summary>Inventor was already ready when the add-in was activated (loaded from the Add-in Manager): not a startup.</summary>
    AlreadyReady,

    /// <summary>A document was visible, so the tab is left to Inventor.</summary>
    DocumentOpen,

    /// <summary>Inventor switched away from the tab once and the tab was made active again; the plan never retries more.</summary>
    MadeActiveAgain,

    /// <summary>The watch window passed with the tab still active.</summary>
    WatchEnded,

    /// <summary>The add-in stopped the plan itself, for example because the tab could not be found or set.</summary>
    Cancelled,
}

/// <summary>What the add-in should do after telling <see cref="StartupTabPlan"/> what it saw.</summary>
/// <param name="MakeTabActive">Make the add-in's tab the active tab on the ZeroDoc ribbon.</param>
/// <param name="Done">The plan has just stopped; unsubscribe and stop the timer. <see cref="StartupTabPlan.StopReason"/> says why.</param>
public readonly record struct StartupTabStep(bool MakeTabActive, bool Done)
{
    public static StartupTabStep Nothing => new(false, false);
}

/// <summary>
/// Decides, at Inventor startup, when to make the add-in's tab the active tab on the ZeroDoc ribbon (Default, spike 062).
/// </summary>
/// <remarks>
/// Inventor switches the ZeroDoc ribbon back to its own tab about a second after it reports ready, so the tab is made
/// active at <c>OnReady</c> and then watched for <see cref="WatchWindow"/>; the first time it is no longer active it is
/// made active once more and the plan stops. It never makes the tab active more than <see cref="MaxActivations"/> times,
/// and never after it is done, so a tab the user clicks later stays.
/// </remarks>
public sealed class StartupTabPlan
{
    /// <summary>How long after <c>OnReady</c> the tab is watched.</summary>
    public static readonly TimeSpan WatchWindow = TimeSpan.FromSeconds(5);

    /// <summary>The most times the plan makes the tab active.</summary>
    public const int MaxActivations = 2;

    private enum State
    {
        NotStarted,
        WaitingForReady,
        Watching,
        Done,
    }

    private State _state = State.NotStarted;

    /// <summary>How many times the plan has asked for the tab to be made active.</summary>
    public int Activations { get; private set; }

    public bool IsDone => _state == State.Done;

    /// <summary>True between <c>OnReady</c> making the tab active and the plan stopping.</summary>
    public bool IsWatching => _state == State.Watching;

    /// <summary>Why the plan stopped, or null while it is still running.</summary>
    public StartupTabStopReason? StopReason { get; private set; }

    /// <summary>The add-in was activated. <paramref name="inventorReady"/> is <c>Application.Ready</c> at that moment.</summary>
    public StartupTabStep Activated(bool inventorReady)
    {
        if (_state != State.NotStarted)
            return StartupTabStep.Nothing;

        if (inventorReady)
            return Finish(StartupTabStopReason.AlreadyReady);

        _state = State.WaitingForReady;
        return StartupTabStep.Nothing;
    }

    /// <summary>Inventor reported it is ready (<c>OnReady</c>, after timing).</summary>
    public StartupTabStep Ready(bool documentVisible)
    {
        if (_state != State.WaitingForReady)
            return StartupTabStep.Nothing;

        if (documentVisible)
            return Finish(StartupTabStopReason.DocumentOpen);

        _state = State.Watching;
        return Activate(done: false);
    }

    /// <summary>
    /// A periodic check while watching. <paramref name="elapsed"/> is the time since <c>OnReady</c>;
    /// <paramref name="tabActive"/> is whether the add-in's tab is the active ZeroDoc tab.
    /// </summary>
    public StartupTabStep Check(TimeSpan elapsed, bool documentVisible, bool tabActive)
    {
        if (_state != State.Watching)
            return StartupTabStep.Nothing;

        if (documentVisible)
            return Finish(StartupTabStopReason.DocumentOpen);

        if (elapsed >= WatchWindow)
            return Finish(StartupTabStopReason.WatchEnded);

        if (!tabActive)
        {
            _state = State.Done;
            StopReason = StartupTabStopReason.MadeActiveAgain;
            return Activate(done: true);
        }

        return StartupTabStep.Nothing;
    }

    /// <summary>Stops the plan from outside (the tab could not be found or set, or the add-in is unloading).</summary>
    /// <returns>True when this call stopped it; false when it was already done.</returns>
    public bool Cancel()
    {
        if (_state == State.Done)
            return false;
        Finish(StartupTabStopReason.Cancelled);
        return true;
    }

    private StartupTabStep Activate(bool done)
    {
        if (Activations >= MaxActivations)
        {
            if (_state != State.Done)
                Finish(StartupTabStopReason.MadeActiveAgain);
            return new StartupTabStep(false, true);
        }

        Activations++;
        return new StartupTabStep(true, done);
    }

    private StartupTabStep Finish(StartupTabStopReason reason)
    {
        _state = State.Done;
        StopReason = reason;
        return new StartupTabStep(false, true);
    }

    /// <summary>A sentence for the log saying why the plan stopped.</summary>
    public static string Describe(StartupTabStopReason reason) => reason switch
    {
        StartupTabStopReason.AlreadyReady => "Inventor was already running when the add-in loaded, so the tab is left alone.",
        StartupTabStopReason.DocumentOpen => "a document is open, so the tab is left alone.",
        StartupTabStopReason.MadeActiveAgain => "Inventor switched tabs once and the tab was made active again; no more changes.",
        StartupTabStopReason.WatchEnded => $"the tab stayed active for {WatchWindow.TotalSeconds:0} s.",
        StartupTabStopReason.Cancelled => "the add-in was unloaded or hit an error.",
        _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "Unknown stop reason."),
    };
}
