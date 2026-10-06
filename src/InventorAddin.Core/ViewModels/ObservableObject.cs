using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace InventorAddin.Core.ViewModels;

/// <summary>
/// Minimal <see cref="INotifyPropertyChanged"/> base for view-models. WPF windows in the add-in bind to
/// classes derived from this; Core itself has no WPF reference.
/// </summary>
public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Raises <see cref="PropertyChanged"/> for <paramref name="propertyName"/> (the caller's name by default).</summary>
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    /// <summary>
    /// Sets <paramref name="field"/> and raises <see cref="PropertyChanged"/> when the value changes.
    /// Returns whether it changed.
    /// </summary>
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
