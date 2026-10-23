using System;
using Inventor;
using InventorAddin.Core.Holes;
using InventorAddin.Core.Logging;
using InventorAddin.Core.Settings;

namespace InventorAddin.UI
{
    /// <summary>
    /// AWB Hole replaces Inventor's Hole on the 3D Model tab and takes its H shortcut, or gives both back, as the
    /// setting "Use Inventor's Hole command" says (spec 09, "The command"; spike 081 section 3; task 091).
    /// <see cref="HoleTakeoverRules"/> makes every decision; this class reads Inventor and applies the state.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Applied by <see cref="RibbonSetup"/> once the ribbon is built, again after Settings is saved
    /// (<see cref="ApplyFromSettings"/>, through <see cref="Active"/>), and after the user resets shortcuts
    /// (<c>UserInterfaceEvents.OnResetShortcuts</c>). <see cref="Dispose"/>, when the add-in unloads, gives Inventor's
    /// Hole and H back, so a removed add-in never leaves Inventor without its Hole.
    /// </para>
    /// <para>
    /// Every failure is logged and never thrown into Inventor. A take-over that fails at any step gives Inventor's
    /// Hole back, so Inventor's Hole keeps working.
    /// </para>
    /// </remarks>
    public sealed class HoleCommandTakeover : IDisposable
    {
        private readonly Application _app;
        private readonly ButtonDefinition _awbHole;
        private readonly ILog _log;

        // Kept referenced, or the event stops firing.
        private UserInterfaceEvents? _uiEvents;

        private HoleCommandOwner _owner = HoleCommandOwner.Inventor;

        /// <summary>
        /// Who has the Hole button and H now: AWB Hole only while it has taken over (a take-over that failed gives
        /// Inventor's Hole back). Editing a hole follows it (task 092).
        /// </summary>
        public HoleCommandOwner Owner => _owner;

        /// <summary>The takeover running in this session, or null before the ribbon is built or after unloading.</summary>
        internal static HoleCommandTakeover? Active { get; private set; }

        /// <param name="awbHole">AWB Hole's button definition, kept alive by <see cref="RibbonSetup"/>.</param>
        public HoleCommandTakeover(Application app, ButtonDefinition awbHole, ILog log)
        {
            _app = app ?? throw new ArgumentNullException(nameof(app));
            _awbHole = awbHole ?? throw new ArgumentNullException(nameof(awbHole));
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        /// <summary>Applies the settings and follows shortcut resets. Call once, after the ribbon is built.</summary>
        public void Start(AddinSettings settings)
        {
            Active = this;
            ApplyFromSettings(settings, "startup");

            try
            {
                _uiEvents = _app.UserInterfaceManager.UserInterfaceEvents;
                _uiEvents.OnResetShortcuts += OnResetShortcuts;
            }
            catch (Exception ex)
            {
                _uiEvents = null;
                _log.Error("Hole takeover: watching for a shortcut reset failed. After a reset, restart Inventor to give H to the right Hole.", ex);
            }
        }

        /// <summary>Gives the Hole button and H to the Hole command <paramref name="settings"/> names.</summary>
        /// <param name="reason">Why, for the log: startup, settings saved, shortcuts reset.</param>
        public void ApplyFromSettings(AddinSettings settings, string reason)
        {
            HoleCommandOwner owner;
            try
            {
                owner = HoleTakeoverRules.OwnerFor(settings);
            }
            catch (Exception ex)
            {
                _log.Error($"Hole takeover ({reason}): the setting could not be read; Inventor's Hole is left as it is.", ex);
                return;
            }

            Apply(owner, reason);
        }

        private void Apply(HoleCommandOwner owner, string reason)
        {
            _owner = owner;
            if (owner == HoleCommandOwner.Awb)
            {
                if (TryTakeOver(reason))
                    return;

                _log.Warn(HoleTakeoverRules.TakeOverFailedMessage(reason));
                _owner = HoleCommandOwner.Inventor;
            }

            GiveBack(reason);
        }

        /// <summary>
        /// Our large button before Inventor's Hole, Inventor's hidden, H moved to ours (spike 081: ours first, then
        /// Inventor's cleared). False after the first step that fails; the caller then gives Hole back.
        /// </summary>
        private bool TryTakeOver(string reason)
        {
            try
            {
                HoleTakeoverState state = HoleTakeoverRules.TakenOver;
                ControlDefinition inventorHole = InventorHole();
                RibbonPanel panel = ModifyPanel();

                CommandControl awbControl = FindControl(panel, _awbHole.InternalName)
                    ?? panel.CommandControls.AddButton(_awbHole, true, true, HoleTakeoverRules.InventorHoleCommand, true);
                awbControl.Visible = state.AwbButtonVisible;

                CommandControl? inventorControl = FindControl(panel, HoleTakeoverRules.InventorHoleCommand);
                if (inventorControl == null)
                    _log.Warn($"Hole takeover ({reason}): Inventor's Hole button is not on the 3D Model tab's Modify panel; only the shortcut is moved.");
                else
                    inventorControl.Visible = state.InventorButtonVisible;

                _awbHole.OverrideShortcut = state.AwbShortcut;
                inventorHole.OverrideShortcut = state.InventorShortcut ?? string.Empty;

                _log.Info(HoleTakeoverRules.AppliedMessage(state, reason));
                return true;
            }
            catch (Exception ex)
            {
                _log.Error($"Hole takeover ({reason}): replacing Inventor's Hole failed.", ex);
                return false;
            }
        }

        /// <summary>
        /// Inventor's Hole button shown, ours hidden, our shortcut cleared and Inventor's given its default back (spike
        /// 081 round 1's fix). Each step runs on its own, so one failure does not stop the others; failures are logged.
        /// </summary>
        private void GiveBack(string reason)
        {
            bool ok = true;

            ControlDefinition? inventorHole = Try(InventorHole, "finding Inventor's Hole command", reason, ref ok);
            RibbonPanel? panel = Try(ModifyPanel, "finding the 3D Model tab's Modify panel", reason, ref ok);

            string? currentOverride = inventorHole == null ? null : ComSafe.Get(() => inventorHole.OverrideShortcut);
            string? defaultShortcut = inventorHole == null ? null : ComSafe.Get(() => inventorHole.DefaultShortcut);
            HoleTakeoverState state = HoleTakeoverRules.GivenBack(currentOverride, defaultShortcut);

            if (panel != null)
            {
                CommandControl? inventorControl = Try(() => FindControl(panel, HoleTakeoverRules.InventorHoleCommand), "finding Inventor's Hole button", reason, ref ok);
                if (inventorControl != null)
                    Try(() => inventorControl.Visible = state.InventorButtonVisible, "showing Inventor's Hole button", reason, ref ok);

                CommandControl? awbControl = Try(() => FindControl(panel, _awbHole.InternalName), "finding our Hole button on the Modify panel", reason, ref ok);
                if (awbControl != null)
                    Try(() => awbControl.Visible = state.AwbButtonVisible, "hiding our Hole button on the Modify panel", reason, ref ok);
            }

            Try(() => _awbHole.OverrideShortcut = state.AwbShortcut, "clearing our Hole command's shortcut", reason, ref ok);
            if (inventorHole != null && state.InventorShortcut != null)
            {
                string shortcut = state.InventorShortcut;
                Try(() => inventorHole.OverrideShortcut = shortcut, "giving Inventor's Hole its shortcut back", reason, ref ok);
            }

            if (ok)
                _log.Info(HoleTakeoverRules.AppliedMessage(state, reason));
        }

        private void OnResetShortcuts(NameValueMap context)
        {
            // Re-applied after Inventor's event has returned, so the reset is done before the shortcuts are set again.
            try
            {
                System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        if (ReferenceEquals(Active, this))
                            Apply(_owner, "shortcuts reset");
                    }
                    catch (Exception ex)
                    {
                        _log.Error("Hole takeover (shortcuts reset): re-applying failed.", ex);
                    }
                }));
            }
            catch (Exception ex)
            {
                _log.Error("Hole takeover (shortcuts reset): could not queue the re-apply.", ex);
            }
        }

        private ControlDefinition InventorHole() =>
            _app.CommandManager.ControlDefinitions[HoleTakeoverRules.InventorHoleCommand];

        private RibbonPanel ModifyPanel() =>
            _app.UserInterfaceManager.Ribbons[HoleTakeoverRules.RibbonName]
                .RibbonTabs[HoleTakeoverRules.TabId]
                .RibbonPanels[HoleTakeoverRules.PanelId];

        private static CommandControl? FindControl(RibbonPanel panel, string internalName)
        {
            foreach (CommandControl c in panel.CommandControls)
                if (c.InternalName == internalName)
                    return c;
            return null;
        }

        private T? Try<T>(Func<T> step, string what, string reason, ref bool ok)
        {
            try
            {
                return step();
            }
            catch (Exception ex)
            {
                ok = false;
                _log.Error($"Hole takeover ({reason}): {what} failed.", ex);
                return default;
            }
        }

        private void Try(Action step, string what, string reason, ref bool ok) =>
            Try<object?>(() => { step(); return null; }, what, reason, ref ok);

        /// <summary>The add-in is unloading: Inventor's Hole and H are given back, and the reset event released.</summary>
        public void Dispose()
        {
            if (ReferenceEquals(Active, this))
                Active = null;

            try
            {
                if (_uiEvents != null)
                    _uiEvents.OnResetShortcuts -= OnResetShortcuts;
            }
            catch (Exception ex)
            {
                _log.Warn($"Hole takeover: releasing the shortcut reset event failed. {ex.Message}");
            }

            _uiEvents = null;
            GiveBack("add-in unloading");
        }
    }
}
