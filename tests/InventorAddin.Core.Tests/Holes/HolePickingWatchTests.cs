using InventorAddin.Core.Holes;
using InventorAddin.Core.Ribbon;

namespace InventorAddin.Core.Tests.Holes;

/// <summary>
/// Task 090: pressing AWB Hole while its window is open restarts the picking instead of cancelling the session;
/// anything else that stops the picking still cancels it (task 089's default).
/// </summary>
public sealed class HolePickingWatchTests
{
    private static HolePickingWatch Watch() => new(CommandNames.AwbHole);

    [Fact]
    public void Stopped_WithNoCommandStarted_Cancels()
    {
        // Esc in the model.
        Assert.Equal(HolePickingStop.Cancel, Watch().PickingStopped());
    }

    [Fact]
    public void Stopped_AfterAnotherCommand_Cancels()
    {
        var watch = Watch();
        watch.CommandStarted("PartExtrudeCmd");

        Assert.Equal(HolePickingStop.Cancel, watch.PickingStopped());
    }

    [Fact]
    public void Stopped_AfterAwbHoleStarted_Restarts()
    {
        var watch = Watch();
        watch.CommandStarted(CommandNames.AwbHole);

        Assert.Equal(HolePickingStop.Restart, watch.PickingStopped());
    }

    [Fact]
    public void Stopped_AfterAwbHoleThenAnotherCommand_Cancels()
    {
        var watch = Watch();
        watch.CommandStarted(CommandNames.AwbHole);
        watch.CommandStarted("PartFilletCmd");

        Assert.Equal(HolePickingStop.Cancel, watch.PickingStopped());
    }

    [Fact]
    public void TheCommandIsForgotten_SoALaterEscCancels()
    {
        var watch = Watch();
        watch.CommandStarted(CommandNames.AwbHole);
        Assert.Equal(HolePickingStop.Restart, watch.PickingStopped());

        Assert.Null(watch.LastStarted);
        Assert.Equal(HolePickingStop.Cancel, watch.PickingStopped());
    }

    [Fact]
    public void Resumed_ForgetsTheCommand()
    {
        // AWB Hole pressed without stopping the picking: the command resumes, so a later Esc must still cancel.
        var watch = Watch();
        watch.CommandStarted(CommandNames.AwbHole);

        watch.Resumed();

        Assert.Equal(HolePickingStop.Cancel, watch.PickingStopped());
    }

    [Theory]
    [InlineData("awb_awbhole")]
    [InlineData("Awb_AwbHoleEdit")]
    [InlineData("")]
    [InlineData(null)]
    public void OnlyTheExactCommandName_Restarts(string? name)
    {
        var watch = Watch();
        watch.CommandStarted(name);

        Assert.Equal(HolePickingStop.Cancel, watch.PickingStopped());
    }

    [Fact]
    public void SurroundingSpaces_AreIgnored()
    {
        var watch = Watch();
        watch.CommandStarted($" {CommandNames.AwbHole} ");

        Assert.Equal(HolePickingStop.Restart, watch.PickingStopped());
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void RefusesABlankOwnCommand(string own)
    {
        Assert.Throws<ArgumentException>(() => new HolePickingWatch(own));
    }

    [Fact]
    public void RefusesANullOwnCommand()
    {
        Assert.Throws<ArgumentNullException>(() => new HolePickingWatch(null!));
    }
}
