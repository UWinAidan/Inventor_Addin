using System;
using System.Diagnostics;
using Inventor;
using InventorAddin.Core.Logging;
using InventorAddin.Core.Ribbon;

namespace InventorAddin.UI
{
    /// <summary>
    /// When Inventor starts with no document open, makes the add-in's tab the active tab on the ZeroDoc ribbon.
    /// <see cref="StartupTabPlan"/> makes every decision; this class reads Inventor, tells the plan, and sets the tab.
    /// </summary>
    /// <remarks>
    /// Inventor switches back to its own tab about a second after <c>OnReady</c> (spike 062), so after making the tab
    /// active this watches the ribbon with a timer until the plan says it is done. Nothing here ever throws into Inventor.
    /// </remarks>
    public sealed class StartupTab : IDisposable
    {
        private const int CheckIntervalMs = 250;

        private readonly Application _app;
        private readonly ILog _log;
        private readonly StartupTabPlan _plan = new();
        private readonly Stopwatch _sinceReady = new();

        // Kept referenced, or the event stops firing.
        private ApplicationEvents? _events;
        private System.Windows.Forms.Timer? _timer;

        public StartupTab(Application app, ILog log)
        {
            _app = app;
            _log = log;
        }

        /// <summary>Call once, from <c>Activate</c>, after the ribbon is built.</summary>
        public void Start()
        {
            try
            {
                // Unreadable is treated as ready, so the tab is left alone.
                bool ready = ComSafe.Get(() => (bool?)_app.Ready) ?? true;
                Apply(_plan.Activated(ready));
                if (_plan.IsDone)
                    return;

                _events = _app.ApplicationEvents;
                _events.OnReady += OnReady;
            }
            catch (Exception ex)
            {
                Fail("Startup tab: watching for Inventor to be ready failed.", ex);
            }
        }

        private void OnReady(EventTimingEnum beforeOrAfter, NameValueMap context, out HandlingCodeEnum handlingCode)
        {
            handlingCode = HandlingCodeEnum.kEventNotHandled;
            if (beforeOrAfter != EventTimingEnum.kAfter)
                return;

            try
            {
                Unsubscribe();
                _sinceReady.Start();
                Apply(_plan.Ready(DocumentVisible()));
                if (_plan.IsWatching)
                    StartTimer();
            }
            catch (Exception ex)
            {
                Fail("Startup tab: making the tab active failed.", ex);
            }
        }

        private void StartTimer()
        {
            _timer = new System.Windows.Forms.Timer { Interval = CheckIntervalMs };
            _timer.Tick += OnTick;
            _timer.Start();
        }

        private void OnTick(object? sender, EventArgs e)
        {
            try
            {
                Apply(_plan.Check(_sinceReady.Elapsed, DocumentVisible(), TabActive()));
            }
            catch (Exception ex)
            {
                Fail("Startup tab: checking the tab failed.", ex);
            }
        }

        /// <summary>Carries out a step of the plan: sets the tab, and stops and logs when the plan is done.</summary>
        private void Apply(StartupTabStep step)
        {
            if (step.MakeTabActive)
            {
                RibbonTab? tab = FindTab();
                if (tab == null)
                {
                    _log.Warn($"Startup tab: the {RibbonIds.TabName} tab is not on the ZeroDoc ribbon.");
                    _plan.Cancel();
                    Stop();
                    return;
                }

                tab.Active = true;
                _log.Info(_plan.Activations == 1
                    ? $"Startup tab: made {RibbonIds.TabName} the active tab."
                    : $"Startup tab: Inventor switched tabs; made {RibbonIds.TabName} the active tab again " +
                      $"after {_sinceReady.Elapsed.TotalSeconds:0.0} s.");
            }

            if (step.Done)
            {
                Stop();
                if (_plan.StopReason is { } reason)
                    _log.Info($"Startup tab: stopped; {StartupTabPlan.Describe(reason)}");
            }
        }

        // Unreadable is treated as visible, so the tab is left alone.
        private bool DocumentVisible() => (ComSafe.Get(() => (int?)_app.Documents.VisibleDocuments.Count) ?? 1) > 0;

        // Unreadable is treated as active, so the tab is not set again.
        private bool TabActive()
        {
            RibbonTab? tab = ComSafe.Get(FindTab);
            return tab == null || (ComSafe.Get(() => (bool?)tab.Active) ?? true);
        }

        private RibbonTab? FindTab()
        {
            Ribbon ribbon = _app.UserInterfaceManager.Ribbons[RibbonEnvironment.ZeroDoc.RibbonName()];
            foreach (RibbonTab t in ribbon.RibbonTabs)
                if (t.InternalName == RibbonIds.TabId)
                    return t;
            return null;
        }

        private void Fail(string message, Exception ex)
        {
            _log.Error(message, ex);
            _plan.Cancel();
            try
            {
                Stop();
            }
            catch (Exception stopEx)
            {
                _log.Error("Startup tab: stopping failed.", stopEx);
            }
        }

        private void Stop()
        {
            if (_timer != null)
            {
                _timer.Stop();
                _timer.Tick -= OnTick;
                _timer.Dispose();
                _timer = null;
            }
            _sinceReady.Stop();
            Unsubscribe();
        }

        private void Unsubscribe()
        {
            if (_events == null)
                return;
            _events.OnReady -= OnReady;
            _events = null;
        }

        public void Dispose()
        {
            try
            {
                if (_plan.Cancel())
                    _log.Info($"Startup tab: stopped; {StartupTabPlan.Describe(StartupTabStopReason.Cancelled)}");
                Stop();
            }
            catch (Exception ex)
            {
                _log.Error("Startup tab: stopping failed.", ex);
            }
        }
    }
}
