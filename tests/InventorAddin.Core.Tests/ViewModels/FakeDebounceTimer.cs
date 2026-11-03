using InventorAddin.Core.ViewModels;

namespace InventorAddin.Core.Tests.ViewModels;

/// <summary>
/// A debounce timer on a fake clock: <see cref="Advance"/> moves the clock and raises <see cref="Elapsed"/> when the
/// running timer's delay has passed, as the add-in's DispatcherTimer would on the UI thread.
/// </summary>
public sealed class FakeDebounceTimer : IDebounceTimer
{
    private TimeSpan _now;
    private TimeSpan? _due;

    public event EventHandler? Elapsed;

    public bool IsRunning => _due is not null;

    public int Starts { get; private set; }

    public void Start(TimeSpan delay)
    {
        Starts++;
        _due = _now + delay;
    }

    public void Stop() => _due = null;

    /// <summary>Moves the clock on by <paramref name="time"/>, raising <see cref="Elapsed"/> if the timer falls due.</summary>
    public void Advance(TimeSpan time)
    {
        _now += time;
        if (_due is { } due && _now >= due)
        {
            _due = null;
            Elapsed?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Advance(int milliseconds) => Advance(TimeSpan.FromMilliseconds(milliseconds));
}
