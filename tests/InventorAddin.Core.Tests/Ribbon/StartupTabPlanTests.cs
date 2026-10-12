using InventorAddin.Core.Ribbon;

namespace InventorAddin.Core.Tests.Ribbon;

public sealed class StartupTabPlanTests
{
    private static readonly TimeSpan OneSecond = TimeSpan.FromSeconds(1);

    private static StartupTabPlan Watching()
    {
        var plan = new StartupTabPlan();
        plan.Activated(inventorReady: false);
        plan.Ready(documentVisible: false);
        return plan;
    }

    [Fact]
    public void WatchWindow_IsFiveSeconds_AndAtMostTwoActivations()
    {
        Assert.Equal(TimeSpan.FromSeconds(5), StartupTabPlan.WatchWindow);
        Assert.Equal(2, StartupTabPlan.MaxActivations);
    }

    [Fact]
    public void Activated_WhenInventorAlreadyReady_DoesNothingAndIsDone()
    {
        var plan = new StartupTabPlan();

        var step = plan.Activated(inventorReady: true);

        Assert.False(step.MakeTabActive);
        Assert.True(step.Done);
        Assert.True(plan.IsDone);
        Assert.Equal(StartupTabStopReason.AlreadyReady, plan.StopReason);
    }

    [Fact]
    public void Activated_WhenInventorNotReady_WaitsForReady()
    {
        var plan = new StartupTabPlan();

        var step = plan.Activated(inventorReady: false);

        Assert.Equal(StartupTabStep.Nothing, step);
        Assert.False(plan.IsDone);
        Assert.False(plan.IsWatching);
        Assert.Null(plan.StopReason);
    }

    [Fact]
    public void AlreadyReady_ThenReady_DoesNothing()
    {
        var plan = new StartupTabPlan();
        plan.Activated(inventorReady: true);

        Assert.Equal(StartupTabStep.Nothing, plan.Ready(documentVisible: false));
        Assert.Equal(0, plan.Activations);
    }

    [Fact]
    public void Ready_WithDocumentVisible_DoesNothingAndIsDone()
    {
        var plan = new StartupTabPlan();
        plan.Activated(inventorReady: false);

        var step = plan.Ready(documentVisible: true);

        Assert.False(step.MakeTabActive);
        Assert.True(step.Done);
        Assert.Equal(StartupTabStopReason.DocumentOpen, plan.StopReason);
        Assert.Equal(0, plan.Activations);
    }

    [Fact]
    public void Ready_WithNoDocument_MakesTabActiveAndWatches()
    {
        var plan = new StartupTabPlan();
        plan.Activated(inventorReady: false);

        var step = plan.Ready(documentVisible: false);

        Assert.True(step.MakeTabActive);
        Assert.False(step.Done);
        Assert.True(plan.IsWatching);
        Assert.Equal(1, plan.Activations);
    }

    [Fact]
    public void Ready_BeforeActivated_DoesNothing()
    {
        var plan = new StartupTabPlan();

        Assert.Equal(StartupTabStep.Nothing, plan.Ready(documentVisible: false));
        Assert.False(plan.IsWatching);
    }

    [Fact]
    public void Ready_Twice_MakesTabActiveOnce()
    {
        var plan = Watching();

        Assert.Equal(StartupTabStep.Nothing, plan.Ready(documentVisible: false));
        Assert.Equal(1, plan.Activations);
    }

    [Fact]
    public void Check_BeforeReady_DoesNothing()
    {
        var plan = new StartupTabPlan();
        plan.Activated(inventorReady: false);

        Assert.Equal(StartupTabStep.Nothing, plan.Check(OneSecond, documentVisible: false, tabActive: false));
        Assert.False(plan.IsDone);
    }

    [Fact]
    public void Check_TabStillActive_KeepsWatching()
    {
        var plan = Watching();

        var step = plan.Check(OneSecond, documentVisible: false, tabActive: true);

        Assert.Equal(StartupTabStep.Nothing, step);
        Assert.True(plan.IsWatching);
    }

    [Fact]
    public void Check_DocumentVisible_IsDoneWithoutMakingActive()
    {
        var plan = Watching();

        var step = plan.Check(OneSecond, documentVisible: true, tabActive: false);

        Assert.False(step.MakeTabActive);
        Assert.True(step.Done);
        Assert.Equal(StartupTabStopReason.DocumentOpen, plan.StopReason);
        Assert.Equal(1, plan.Activations);
    }

    [Fact]
    public void Check_TabNoLongerActive_MakesItActiveAgainAndIsDone()
    {
        var plan = Watching();

        var step = plan.Check(OneSecond, documentVisible: false, tabActive: false);

        Assert.True(step.MakeTabActive);
        Assert.True(step.Done);
        Assert.True(plan.IsDone);
        Assert.Equal(StartupTabStopReason.MadeActiveAgain, plan.StopReason);
        Assert.Equal(2, plan.Activations);
    }

    [Fact]
    public void Check_AfterWatchWindow_IsDoneWithoutMakingActive()
    {
        var plan = Watching();

        var step = plan.Check(StartupTabPlan.WatchWindow, documentVisible: false, tabActive: false);

        Assert.False(step.MakeTabActive);
        Assert.True(step.Done);
        Assert.Equal(StartupTabStopReason.WatchEnded, plan.StopReason);
        Assert.Equal(1, plan.Activations);
    }

    [Fact]
    public void Check_JustInsideWatchWindow_StillReactivates()
    {
        var plan = Watching();

        var step = plan.Check(StartupTabPlan.WatchWindow - TimeSpan.FromMilliseconds(1), documentVisible: false, tabActive: false);

        Assert.True(step.MakeTabActive);
    }

    [Fact]
    public void NeverMakesActiveMoreThanTwice_NorAfterDone()
    {
        var plan = Watching();
        plan.Check(OneSecond, documentVisible: false, tabActive: false);

        for (int i = 0; i < 10; i++)
        {
            Assert.Equal(StartupTabStep.Nothing, plan.Check(TimeSpan.FromSeconds(2 + i * 0.25), documentVisible: false, tabActive: false));
            Assert.Equal(StartupTabStep.Nothing, plan.Ready(documentVisible: false));
            Assert.Equal(StartupTabStep.Nothing, plan.Activated(inventorReady: false));
        }

        Assert.Equal(2, plan.Activations);
        Assert.Equal(StartupTabStopReason.MadeActiveAgain, plan.StopReason);
    }

    [Fact]
    public void Cancel_WhileWatching_StopsPlan()
    {
        var plan = Watching();

        Assert.True(plan.Cancel());

        Assert.True(plan.IsDone);
        Assert.Equal(StartupTabStopReason.Cancelled, plan.StopReason);
        Assert.Equal(StartupTabStep.Nothing, plan.Check(OneSecond, documentVisible: false, tabActive: false));
    }

    [Fact]
    public void Cancel_WhenDone_KeepsReasonAndReturnsFalse()
    {
        var plan = new StartupTabPlan();
        plan.Activated(inventorReady: true);

        Assert.False(plan.Cancel());
        Assert.Equal(StartupTabStopReason.AlreadyReady, plan.StopReason);
    }

    [Theory]
    [InlineData(StartupTabStopReason.AlreadyReady)]
    [InlineData(StartupTabStopReason.DocumentOpen)]
    [InlineData(StartupTabStopReason.MadeActiveAgain)]
    [InlineData(StartupTabStopReason.WatchEnded)]
    [InlineData(StartupTabStopReason.Cancelled)]
    public void Describe_EveryReason_IsASentence(StartupTabStopReason reason)
    {
        Assert.False(string.IsNullOrWhiteSpace(StartupTabPlan.Describe(reason)));
    }
}
