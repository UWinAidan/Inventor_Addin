using InventorAddin.Core.ViewModels;

namespace InventorAddin.Core.Tests.ViewModels;

/// <summary>The pause after the last keystroke before a typed box is applied (task 130), on a fake clock.</summary>
public sealed class DebouncerTests
{
    [Fact]
    public void ThePause_Is300Milliseconds()
    {
        var debouncer = new Debouncer(new FakeDebounceTimer());

        Assert.Equal(TimeSpan.FromMilliseconds(300), Debouncer.TypingPause);
        Assert.Equal(Debouncer.TypingPause, debouncer.Pause);
    }

    [Fact]
    public void APost_RunsOnceThePauseHasPassed_NotBefore()
    {
        var timer = new FakeDebounceTimer();
        var debouncer = new Debouncer(timer);
        var runs = 0;

        debouncer.Post(() => runs++);
        timer.Advance(299);

        Assert.Equal(0, runs);
        Assert.True(debouncer.IsPending);

        timer.Advance(1);

        Assert.Equal(1, runs);
        Assert.False(debouncer.IsPending);
        Assert.False(timer.IsRunning);

        timer.Advance(1000);
        Assert.Equal(1, runs);
    }

    [Fact]
    public void EachPost_StartsThePauseAgain_AndOnlyTheLastActionRuns()
    {
        var timer = new FakeDebounceTimer();
        var debouncer = new Debouncer(timer);
        var ran = new List<string>();

        debouncer.Post(() => ran.Add("1"));
        timer.Advance(200);
        debouncer.Post(() => ran.Add("12"));
        timer.Advance(200);

        Assert.Empty(ran);

        timer.Advance(100);

        Assert.Equal(["12"], ran);
    }

    [Fact]
    public void Flush_RunsTheWaitingActionAtOnce_AndStopsTheTimer()
    {
        var timer = new FakeDebounceTimer();
        var debouncer = new Debouncer(timer);
        var runs = 0;

        debouncer.Post(() => runs++);
        debouncer.Flush();

        Assert.Equal(1, runs);
        Assert.False(timer.IsRunning);

        timer.Advance(300);
        debouncer.Flush();
        Assert.Equal(1, runs);
    }

    [Fact]
    public void Cancel_DropsTheWaitingAction()
    {
        var timer = new FakeDebounceTimer();
        var debouncer = new Debouncer(timer);
        var runs = 0;

        debouncer.Post(() => runs++);
        debouncer.Cancel();
        timer.Advance(300);

        Assert.Equal(0, runs);
        Assert.False(debouncer.IsPending);
        Assert.False(timer.IsRunning);
    }

    [Fact]
    public void AnActionThatPostsAgain_WaitsForANewPause()
    {
        var timer = new FakeDebounceTimer();
        var debouncer = new Debouncer(timer);
        var runs = 0;

        void Action()
        {
            runs++;
            if (runs == 1)
            {
                debouncer.Post(Action);
            }
        }

        debouncer.Post(Action);
        timer.Advance(300);

        Assert.Equal(1, runs);
        Assert.True(debouncer.IsPending);

        timer.Advance(300);

        Assert.Equal(2, runs);
    }

    [Fact]
    public void APauseOfYourOwn_IsUsed()
    {
        var timer = new FakeDebounceTimer();
        var debouncer = new Debouncer(timer, TimeSpan.FromMilliseconds(50));
        var runs = 0;

        debouncer.Post(() => runs++);
        timer.Advance(50);

        Assert.Equal(1, runs);
    }

    [Fact]
    public void ANegativePause_OrNoTimer_IsRefused()
    {
        Assert.Throws<ArgumentNullException>(() => new Debouncer(null!));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new Debouncer(new FakeDebounceTimer(), TimeSpan.FromMilliseconds(-1)));
    }
}
