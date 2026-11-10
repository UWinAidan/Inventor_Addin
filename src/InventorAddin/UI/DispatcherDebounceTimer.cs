using System;
using System.Windows.Threading;
using InventorAddin.Core.ViewModels;

namespace InventorAddin.UI
{
    /// <summary>
    /// The one-shot timer behind live typing (task 130): a <see cref="DispatcherTimer"/> on the window's dispatcher, so
    /// <see cref="Elapsed"/> is raised on the UI thread, where the view-model and Inventor's expression checks run. The
    /// debounce rule itself is <see cref="Debouncer"/> in Core.
    /// </summary>
    internal sealed class DispatcherDebounceTimer : IDebounceTimer
    {
        private readonly DispatcherTimer _timer;

        public DispatcherDebounceTimer(Dispatcher dispatcher)
        {
            if (dispatcher == null)
                throw new ArgumentNullException(nameof(dispatcher));

            _timer = new DispatcherTimer(DispatcherPriority.Normal, dispatcher);
            _timer.Tick += OnTick;
        }

        public event EventHandler? Elapsed;

        public void Start(TimeSpan delay)
        {
            // Stop first, so a running timer counts the delay again from now.
            _timer.Stop();
            _timer.Interval = delay;
            _timer.Start();
        }

        public void Stop() => _timer.Stop();

        // A DispatcherTimer repeats until stopped: this one raises once.
        private void OnTick(object? sender, EventArgs e)
        {
            _timer.Stop();
            Elapsed?.Invoke(this, EventArgs.Empty);
        }
    }
}
