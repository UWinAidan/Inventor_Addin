using InventorAddin.Core.ViewModels;

namespace InventorAddin.Core.Tests.ViewModels;

public sealed class RelayCommandTests
{
    [Fact]
    public void Constructor_RejectsNullExecute()
    {
        Assert.Throws<ArgumentNullException>(() => new RelayCommand(null!));
    }

    [Fact]
    public void WithoutCanExecute_AlwaysEnabled_AndRuns()
    {
        int runs = 0;
        var command = new RelayCommand(() => runs++);

        Assert.True(command.CanExecute(null));
        command.Execute(null);

        Assert.Equal(1, runs);
    }

    [Fact]
    public void Execute_WhenCanExecuteIsFalse_DoesNothing()
    {
        int runs = 0;
        bool enabled = false;
        var command = new RelayCommand(() => runs++, () => enabled);

        Assert.False(command.CanExecute(null));
        command.Execute(null);
        Assert.Equal(0, runs);

        enabled = true;
        Assert.True(command.CanExecute(null));
        command.Execute(null);
        Assert.Equal(1, runs);
    }

    [Fact]
    public void RaiseCanExecuteChanged_RaisesEventWithCommandAsSender()
    {
        var command = new RelayCommand(() => { });
        object? sender = null;
        command.CanExecuteChanged += (s, _) => sender = s;

        command.RaiseCanExecuteChanged();

        Assert.Same(command, sender);
    }
}
