using InventorAddin.Core.Holes;

namespace InventorAddin.Core.Tests.Holes;

/// <summary>When the Hole takeover is applied again after a shortcut reset (task 120).</summary>
public sealed class HoleShortcutResetPlanTests
{
    private const string Shortcuts = "AWB Hole none, Inventor's Hole 'H'";

    private static TimeSpan S(double seconds) => TimeSpan.FromSeconds(seconds);

    [Fact]
    public void NewPlan_IsNotWatching_AndChecksAreDone()
    {
        var plan = new HoleShortcutResetPlan();

        Assert.False(plan.IsWatching);
        HoleShortcutResetStep step = plan.Check(S(1), true, false, Shortcuts);
        Assert.Equal(HoleShortcutResetAction.Done, step.Action);
        Assert.Null(step.LogMessage);
    }

    [Fact]
    public void Reset_WaitsAndLogsTheWindowAndShortcuts()
    {
        var plan = new HoleShortcutResetPlan();

        HoleShortcutResetStep step = plan.Reset(false, Shortcuts);

        Assert.Equal(HoleShortcutResetAction.Wait, step.Action);
        Assert.True(plan.IsWatching);
        Assert.Contains("Customize dialog is open", step.LogMessage);
        Assert.Contains(Shortcuts, step.LogMessage);
        Assert.StartsWith("Hole takeover (shortcuts reset):", step.LogMessage);
    }

    [Theory]
    [InlineData(true, "window is enabled")]
    [InlineData(null, "could not be read")]
    public void Reset_SaysWhatTheWindowWas(bool? enabled, string expected) =>
        Assert.Contains(expected, new HoleShortcutResetPlan().Reset(enabled, Shortcuts).LogMessage);

    [Fact]
    public void WhileTheDialogIsOpen_Waits_ThenReAppliesWhenItCloses()
    {
        var plan = new HoleShortcutResetPlan();
        plan.Reset(false, Shortcuts);

        HoleShortcutResetStep waiting = plan.Check(S(10), false, false, Shortcuts);
        Assert.Equal(HoleShortcutResetAction.Wait, waiting.Action);
        Assert.Null(waiting.LogMessage);

        HoleShortcutResetStep closed = plan.Check(S(12), true, false, Shortcuts);
        Assert.Equal(HoleShortcutResetAction.ReApply, closed.Action);
        Assert.Contains("12.0 s", closed.LogMessage);
        Assert.Contains(Shortcuts, closed.LogMessage);
        Assert.Equal(1, plan.ReApplies);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(null)]
    public void WindowEnabledOrUnreadable_ReAppliesAtTheFirstCheck(bool? enabled)
    {
        var plan = new HoleShortcutResetPlan();
        plan.Reset(enabled, Shortcuts);

        Assert.Equal(HoleShortcutResetAction.ReApply, plan.Check(S(0.5), enabled, null, Shortcuts).Action);
    }

    [Fact]
    public void AfterReApplying_ShortcutsRight_StopsAfterTheSettleTime()
    {
        var plan = new HoleShortcutResetPlan();
        plan.Reset(false, Shortcuts);
        plan.Check(S(2), true, false, Shortcuts);

        Assert.Equal(HoleShortcutResetAction.Wait, plan.Check(S(4), true, true, Shortcuts).Action);

        HoleShortcutResetStep done = plan.Check(S(2) + HoleShortcutResetPlan.SettleTime, true, true, Shortcuts);
        Assert.Equal(HoleShortcutResetAction.Done, done.Action);
        Assert.False(done.IsWarning);
        Assert.Contains("done", done.LogMessage);
        Assert.DoesNotContain("could not be checked", done.LogMessage);
        Assert.False(plan.IsWatching);
    }

    [Fact]
    public void AfterReApplying_ShortcutsUnreadable_StopsAndSaysSo()
    {
        var plan = new HoleShortcutResetPlan();
        plan.Reset(true, Shortcuts);
        plan.Check(S(0.5), true, null, Shortcuts);

        HoleShortcutResetStep done = plan.Check(S(0.5) + HoleShortcutResetPlan.SettleTime, true, null, Shortcuts);
        Assert.Equal(HoleShortcutResetAction.Done, done.Action);
        Assert.Contains("could not be checked", done.LogMessage);
    }

    [Fact]
    public void AfterReApplying_ShortcutsChanged_ReAppliesAgain_UpToTheLimit()
    {
        var plan = new HoleShortcutResetPlan();
        plan.Reset(true, Shortcuts);
        Assert.Equal(HoleShortcutResetAction.ReApply, plan.Check(S(1), true, null, Shortcuts).Action);

        for (var i = 2; i <= HoleShortcutResetPlan.MaxReApplies; i++)
        {
            HoleShortcutResetStep again = plan.Check(S(i), true, false, Shortcuts);
            Assert.Equal(HoleShortcutResetAction.ReApply, again.Action);
            Assert.Contains("changed after re-applying", again.LogMessage);
        }

        HoleShortcutResetStep giveUp = plan.Check(S(10), true, false, Shortcuts);
        Assert.Equal(HoleShortcutResetAction.Done, giveUp.Action);
        Assert.True(giveUp.IsWarning);
        Assert.Contains("Save Settings", giveUp.LogMessage);
        Assert.Equal(HoleShortcutResetPlan.MaxReApplies, plan.ReApplies);
    }

    [Fact]
    public void ADialogClosingAfterTheLastReApply_DoesNotReApplyAgain()
    {
        var plan = new HoleShortcutResetPlan();
        plan.Reset(true, Shortcuts);
        plan.Check(S(1), true, null, Shortcuts);
        for (var i = 2; i <= HoleShortcutResetPlan.MaxReApplies; i++)
            plan.Check(S(i), true, false, Shortcuts);

        Assert.Equal(HoleShortcutResetAction.Wait, plan.Check(S(5), false, null, Shortcuts).Action);

        HoleShortcutResetStep closed = plan.Check(S(6), true, null, Shortcuts);
        Assert.Equal(HoleShortcutResetAction.Done, closed.Action);
        Assert.True(closed.IsWarning);
        Assert.Equal(HoleShortcutResetPlan.MaxReApplies, plan.ReApplies);
    }

    [Fact]
    public void ReApplyingAgain_RestartsTheSettleTime()
    {
        var plan = new HoleShortcutResetPlan();
        plan.Reset(true, Shortcuts);
        plan.Check(S(1), true, null, Shortcuts);
        plan.Check(S(3), true, false, Shortcuts);

        Assert.Equal(HoleShortcutResetAction.Wait, plan.Check(S(1) + HoleShortcutResetPlan.SettleTime, true, true, Shortcuts).Action);
        Assert.Equal(HoleShortcutResetAction.Done, plan.Check(S(3) + HoleShortcutResetPlan.SettleTime, true, true, Shortcuts).Action);
    }

    [Fact]
    public void WindowDisabledAgainWhileSettling_WaitsForTheDialogAgain()
    {
        var plan = new HoleShortcutResetPlan();
        plan.Reset(true, Shortcuts);
        plan.Check(S(1), true, null, Shortcuts);

        HoleShortcutResetStep disabled = plan.Check(S(2), false, false, Shortcuts);
        Assert.Equal(HoleShortcutResetAction.Wait, disabled.Action);
        Assert.Contains("disabled again", disabled.LogMessage);

        Assert.Equal(HoleShortcutResetAction.Wait, plan.Check(S(20), false, false, Shortcuts).Action);
        Assert.Equal(HoleShortcutResetAction.ReApply, plan.Check(S(21), true, false, Shortcuts).Action);
    }

    [Fact]
    public void DialogNeverCloses_GivesUpWithAWarning()
    {
        var plan = new HoleShortcutResetPlan();
        plan.Reset(false, Shortcuts);

        HoleShortcutResetStep step = plan.Check(HoleShortcutResetPlan.GiveUpAfter, false, false, Shortcuts);

        Assert.Equal(HoleShortcutResetAction.Done, step.Action);
        Assert.True(step.IsWarning);
        Assert.Contains("gave up", step.LogMessage);
        Assert.False(plan.IsWatching);
    }

    [Fact]
    public void ResetWhileWatching_StartsOverAndSaysSo()
    {
        var plan = new HoleShortcutResetPlan();
        plan.Reset(true, Shortcuts);
        plan.Check(S(1), true, false, Shortcuts);
        plan.Check(S(2), true, false, Shortcuts);

        HoleShortcutResetStep step = plan.Reset(false, Shortcuts);

        Assert.Contains("again", step.LogMessage);
        Assert.Equal(0, plan.ReApplies);
        Assert.Equal(HoleShortcutResetAction.Wait, plan.Check(S(1), false, false, Shortcuts).Action);
    }

    [Fact]
    public void Cancel_StopsAndSaysWhetherItWasWatching()
    {
        var plan = new HoleShortcutResetPlan();
        Assert.False(plan.Cancel());

        plan.Reset(false, Shortcuts);
        Assert.True(plan.Cancel());
        Assert.False(plan.IsWatching);
        Assert.Equal(HoleShortcutResetAction.Done, plan.Check(S(1), true, false, Shortcuts).Action);
    }
}
