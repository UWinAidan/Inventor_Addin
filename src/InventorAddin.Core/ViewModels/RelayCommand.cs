using System.Windows.Input;

namespace InventorAddin.Core.ViewModels;

/// <summary>
/// Minimal <see cref="ICommand"/> that runs a delegate. <see cref="ICommand"/> comes from the base
/// <c>net8.0</c> library, so Core needs no WPF reference.
/// </summary>
/// <remarks>
/// WPF only re-queries <see cref="CanExecute"/> when <see cref="CanExecuteChanged"/> fires, so the owning
/// view-model calls <see cref="RaiseCanExecuteChanged"/> whenever the answer may have changed.
/// The command parameter is ignored.
/// </remarks>
public sealed class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

    /// <summary>Runs the delegate if <see cref="CanExecute"/> allows it; otherwise does nothing.</summary>
    public void Execute(object? parameter)
    {
        if (CanExecute(parameter))
            _execute();
    }

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
