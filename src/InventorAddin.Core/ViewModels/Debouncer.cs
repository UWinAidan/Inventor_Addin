namespace InventorAddin.Core.ViewModels;

/// <summary>
/// A one-shot timer that raises <see cref="Elapsed"/> on the UI thread. The add-in implements it with a WPF
/// <c>DispatcherTimer</c>; tests use a fake clock, so <see cref="Debouncer"/> is tested without waiting.
/// </summary>
public interface IDebounceTimer
{
    /// <summary>Raised once, <c>delay</c> after the last <see cref="Start"/>, unless stopped first.</summary>
    event EventHandler? Elapsed;

    /// <summary>Starts the timer, or starts it again from now when it is running.</summary>
    void Start(TimeSpan delay);

    /// <summary>Stops the timer; <see cref="Elapsed"/> is not raised.</summary>
    void Stop();
}

/// <summary>
/// Runs an action once a pause has passed since the last <see cref="Post"/> (task 130): text boxes update the preview
/// about <see cref="TypingPause"/> after the last keystroke. A new post replaces the waiting action and starts the
/// pause again; <see cref="Flush"/> runs it at once (Enter, leaving the box, OK) and <see cref="Cancel"/> drops it.
/// </summary>
public sealed class Debouncer
{
    /// <summary>How long after the last keystroke a typed box is applied (Aidan, 2026-10-25).</summary>
    public static readonly TimeSpan TypingPause = TimeSpan.FromMilliseconds(300);

    private readonly IDebounceTimer _timer;
    private Action? _pending;

    public Debouncer(IDebounceTimer timer, TimeSpan? pause = null)
    {
        _timer = timer ?? throw new ArgumentNullException(nameof(timer));
        Pause = pause ?? TypingPause;
        if (Pause < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(pause), "The pause cannot be negative.");
        }

        _timer.Elapsed += OnElapsed;
    }

    /// <summary>The pause after the last post.</summary>
    public TimeSpan Pause { get; }

    /// <summary>True while an action waits for the pause.</summary>
    public bool IsPending => _pending is not null;

    /// <summary>Waits for the pause, then runs <paramref name="action"/>, in place of any action already waiting.</summary>
    public void Post(Action action)
    {
        _pending = action ?? throw new ArgumentNullException(nameof(action));
        _timer.Start(Pause);
    }

    /// <summary>Runs the waiting action now, if there is one.</summary>
    public void Flush()
    {
        _timer.Stop();
        Run();
    }

    /// <summary>Drops the waiting action without running it.</summary>
    public void Cancel()
    {
        _timer.Stop();
        _pending = null;
    }

    private void OnElapsed(object? sender, EventArgs e)
    {
        _timer.Stop();
        Run();
    }

    // The action is taken off first, so one that posts again waits for a new pause and one that throws is not run twice.
    private void Run()
    {
        var action = _pending;
        _pending = null;
        action?.Invoke();
    }
}
