using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Inventor;
using InventorAddin.Core.Holes;
using InventorAddin.Core.Logging;
using InventorAddin.Core.Ribbon;
using InventorAddin.Core.Settings;

namespace InventorAddin.UI
{
    /// <summary>
    /// AWB Hole replaces Inventor's Hole on the 3D Model tab and the Sheet Metal tab and takes its H shortcut, or gives
    /// them back, as the setting "Use Inventor's Hole command" says (spec 09, "The command"; spike 081 section 3; tasks
    /// 091 and 128). The Flat Pattern tab keeps Inventor's Hole. <see cref="HoleTakeoverRules"/> and
    /// <see cref="SheetMetalHolePanel"/> make every decision; this class reads Inventor and applies the state.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Applied by <see cref="RibbonSetup"/> once the ribbon is built, again after Settings is saved
    /// (<see cref="ApplyFromSettings"/>, through <see cref="Active"/>), and after the user resets shortcuts
    /// (<c>UserInterfaceEvents.OnResetShortcuts</c>; task 120). Inventor fires that event while the Customize dialog is
    /// still open, and the dialog writes its own shortcuts when it closes, so <see cref="HoleShortcutResetPlan"/> waits
    /// for Inventor's window to be enabled again, re-applies as toggling the setting does, and checks the shortcuts stay.
    /// Every step is logged. <see cref="Dispose"/>, when the add-in unloads, gives Inventor's
    /// Hole and H back, so a removed add-in never leaves Inventor without its Hole.
    /// </para>
    /// <para>
    /// The Sheet Metal tab's panel is found at every apply by the control's command (<c>PartHoleCmd</c>), not by a fixed
    /// panel name, and what was found is logged when it changes (task 128). A tab or panel that cannot be read is
    /// skipped and named in that log line. No such panel, or a ribbon that cannot be read at all, is a warning, not a
    /// failure: the 3D Model tab is still taken over. Only a failure while changing a panel that was found fails the
    /// take-over.
    /// </para>
    /// <para>
    /// On both panels AWB Hole is a split button (task 129): AWB Hole on top, and a dropdown with AWB Hole, Inventor Hole
    /// and "Use Inventor's Hole by default", whose pressed state this sets at every apply from the settings. Without
    /// either dropdown command, or when Inventor refuses the split button, AWB Hole's plain button is used as before.
    /// Our control is found by its name, its top command or a dropdown entry (<see cref="AddinUi.IsControlOf"/>), since
    /// which of those Inventor reports for a split button is not confirmed.
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

        // The split button's dropdown commands (task 129), kept alive by RibbonSetup; null when one failed to register.
        private readonly ButtonDefinition? _inventorHoleItem;
        private readonly ButtonDefinition? _toggleItem;
        private bool _plainButtonLogged;

        private readonly ILog _log;

        // Kept referenced, or the event stops firing.
        private UserInterfaceEvents? _uiEvents;

        private const string ResetReason = "shortcuts reset";
        private readonly HoleShortcutResetPlan _resetPlan = new();
        private readonly Stopwatch _sinceReset = new();
        private System.Windows.Forms.Timer? _resetTimer;

        private HoleCommandOwner _owner = HoleCommandOwner.Inventor;

        // The last Sheet Metal panel lookup logged, so an unchanged ribbon is not logged at every apply.
        private string? _sheetMetalLookupLogged;

        /// <summary>
        /// Who has the Hole button and H now: AWB Hole only while it has taken over (a take-over that failed gives
        /// Inventor's Hole back). Editing a hole follows it (task 092).
        /// </summary>
        public HoleCommandOwner Owner => _owner;

        /// <summary>The takeover running in this session, or null before the ribbon is built or after unloading.</summary>
        internal static HoleCommandTakeover? Active { get; private set; }

        /// <param name="awbHole">AWB Hole's button definition, kept alive by <see cref="RibbonSetup"/>.</param>
        /// <param name="inventorHoleItem">Inventor Hole's definition for the split button, or null (task 129).</param>
        /// <param name="toggleItem">"Use Inventor's Hole by default"'s definition for the split button, or null.</param>
        public HoleCommandTakeover(
            Application app, ButtonDefinition awbHole, ButtonDefinition? inventorHoleItem, ButtonDefinition? toggleItem, ILog log)
        {
            _app = app ?? throw new ArgumentNullException(nameof(app));
            _awbHole = awbHole ?? throw new ArgumentNullException(nameof(awbHole));
            _inventorHoleItem = inventorHoleItem;
            _toggleItem = toggleItem;
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        /// <summary>Applies the settings and follows shortcut resets. Call once, after the ribbon is built.</summary>
        public void Start(AddinSettings settings)
        {
            Active = this;
            CopyInventorHoleIcon();
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

            ShowToggleState(settings);
            Apply(owner, reason);
        }

        /// <summary>
        /// "Use Inventor's Hole by default" shows pressed while the setting is on (task 129). A failure is logged; the
        /// setting itself is applied either way.
        /// </summary>
        internal void ShowToggleState(AddinSettings settings)
        {
            if (_toggleItem == null)
                return;

            try
            {
                _toggleItem.Pressed = HoleTakeoverRules.TogglePressed(settings);
            }
            catch (Exception ex)
            {
                _log.Warn($"Hole takeover: showing whether \"{HoleTakeoverRules.UseInventorHoleText}\" is on failed. {ex.Message}");
            }
        }

        private void Apply(HoleCommandOwner owner, string reason)
        {
            bool tookOver = owner == HoleCommandOwner.Awb && TryTakeOver(reason);
            _owner = HoleTakeoverRules.OwnerAfterApply(owner, tookOver);
            if (_owner == HoleCommandOwner.Awb)
                return;

            if (owner == HoleCommandOwner.Awb)
                _log.Warn(HoleTakeoverRules.TakeOverFailedMessage(reason));
            GiveBack(reason);
        }

        /// <summary>
        /// Our large button before Inventor's Hole, Inventor's hidden, H moved to ours (spike 081: ours first, then
        /// Inventor's cleared), on the 3D Model tab's Modify panel and on the Sheet Metal tab's Hole panel (task 128).
        /// False after the first step that fails; the caller then gives Hole back on both.
        /// </summary>
        private bool TryTakeOver(string reason)
        {
            try
            {
                HoleTakeoverState state = HoleTakeoverRules.TakenOver;
                ControlDefinition inventorHole = InventorHole();

                if (!ShowOnPanel(ModifyPanel(), state, reason))
                    _log.Warn($"Hole takeover ({reason}): Inventor's Hole button is not on the 3D Model tab's Modify panel; only the shortcut is moved.");

                // Reading the Sheet Metal tab must not cost the 3D Model tab its takeover; only a failure on a panel
                // that was found (ShowOnPanel) fails the take-over.
                RibbonPanel? sheetMetal;
                try
                {
                    sheetMetal = SheetMetalPanel(reason);
                }
                catch (Exception ex)
                {
                    sheetMetal = null;
                    _log.Warn($"Hole takeover ({reason}): the Sheet Metal tab could not be read; only the 3D Model tab is taken over. {ex.Message}");
                }

                if (sheetMetal != null)
                    ShowOnPanel(sheetMetal, state, reason);

                _awbHole.OverrideShortcut = state.AwbShortcut;
                inventorHole.OverrideShortcut = state.InventorShortcut ?? string.Empty;

                _log.Info(HoleTakeoverRules.AppliedMessage(state, reason, sheetMetal != null));
                return true;
            }
            catch (Exception ex)
            {
                _log.Error($"Hole takeover ({reason}): replacing Inventor's Hole failed.", ex);
                return false;
            }
        }

        /// <summary>
        /// Takes over one panel: our large split button (or plain button) added before Inventor's Hole when it is not
        /// there yet, then both shown or hidden as <paramref name="state"/> says. False when Inventor's Hole is not on
        /// the panel (our control is then added as task 091 adds it on the Modify panel).
        /// </summary>
        private bool ShowOnPanel(RibbonPanel panel, HoleTakeoverState state, string reason)
        {
            CommandControl awbControl = FindAwbControl(panel) ?? AddAwbControl(panel, reason);
            awbControl.Visible = state.AwbButtonVisible;

            CommandControl? inventorControl = FindControl(panel, HoleTakeoverRules.InventorHoleCommand);
            if (inventorControl == null)
                return false;

            inventorControl.Visible = state.InventorButtonVisible;
            return true;
        }

        /// <summary>
        /// Inventor's Hole button shown, ours hidden, our shortcut cleared and Inventor's given its default back (spike
        /// 081 round 1's fix), on the 3D Model tab and the Sheet Metal tab. Each step runs on its own, so one failure does
        /// not stop the others; failures are logged.
        /// </summary>
        private void GiveBack(string reason)
        {
            bool ok = true;

            ControlDefinition? inventorHole = Try(InventorHole, "finding Inventor's Hole command", reason, ref ok);
            RibbonPanel? panel = Try(ModifyPanel, "finding the 3D Model tab's Modify panel", reason, ref ok);
            RibbonPanel? sheetMetal = null;
            try
            {
                sheetMetal = SheetMetalPanel(reason);
            }
            catch (Exception ex)
            {
                // As when taking over: an unreadable Sheet Metal tab only means there is nothing there to give back.
                _log.Warn($"Hole takeover ({reason}): the Sheet Metal tab could not be read; Hole is given back on the 3D Model tab only. {ex.Message}");
            }

            string? currentOverride = inventorHole == null ? null : ComSafe.Get(() => inventorHole.OverrideShortcut);
            string? defaultShortcut = inventorHole == null ? null : ComSafe.Get(() => inventorHole.DefaultShortcut);
            HoleTakeoverState state = HoleTakeoverRules.GivenBack(currentOverride, defaultShortcut);

            if (panel != null)
                GiveBackOnPanel(panel, state, "the Modify panel", reason, ref ok);
            if (sheetMetal != null)
                GiveBackOnPanel(sheetMetal, state, "the Sheet Metal tab", reason, ref ok);

            Try(() => _awbHole.OverrideShortcut = state.AwbShortcut, "clearing our Hole command's shortcut", reason, ref ok);
            if (inventorHole != null && state.InventorShortcut != null)
            {
                string shortcut = state.InventorShortcut;
                Try(() => inventorHole.OverrideShortcut = shortcut, "giving Inventor's Hole its shortcut back", reason, ref ok);
            }

            if (ok)
                _log.Info(HoleTakeoverRules.AppliedMessage(state, reason, sheetMetal != null));
        }

        /// <summary>Inventor's Hole button shown and ours hidden on one panel; each step on its own.</summary>
        private void GiveBackOnPanel(RibbonPanel panel, HoleTakeoverState state, string where, string reason, ref bool ok)
        {
            CommandControl? inventorControl = Try(() => FindControl(panel, HoleTakeoverRules.InventorHoleCommand), $"finding Inventor's Hole button on {where}", reason, ref ok);
            if (inventorControl != null)
                Try(() => inventorControl.Visible = state.InventorButtonVisible, $"showing Inventor's Hole button on {where}", reason, ref ok);

            CommandControl? awbControl = Try(() => FindAwbControl(panel), $"finding our Hole button on {where}", reason, ref ok);
            if (awbControl != null)
                Try(() => awbControl.Visible = state.AwbButtonVisible, $"hiding our Hole button on {where}", reason, ref ok);
        }

        /// <summary>
        /// Inventor reported a shortcut reset, with the Customize dialog still open: starts the plan and its timer. Nothing
        /// is applied here; the dialog would overwrite it when it closes (check 091-3).
        /// </summary>
        private void OnResetShortcuts(NameValueMap context)
        {
            try
            {
                if (!ReferenceEquals(Active, this))
                {
                    _log.Info($"Hole takeover ({ResetReason}): Inventor reported a shortcut reset, but the takeover is not running; nothing is changed.");
                    return;
                }

                _sinceReset.Restart();
                LogStep(_resetPlan.Reset(MainWindowEnabled(), ShortcutsNow(out _, out _)));

                if (_resetTimer == null)
                {
                    _resetTimer = new System.Windows.Forms.Timer { Interval = (int)HoleShortcutResetPlan.CheckInterval.TotalMilliseconds };
                    _resetTimer.Tick += OnResetTick;
                    _resetTimer.Start();
                }
            }
            catch (Exception ex)
            {
                FailReset("watching for the Customize dialog to close failed", ex);
            }
        }

        /// <summary>A tick while <see cref="HoleShortcutResetPlan"/> watches: tells it what Inventor shows, then does its step.</summary>
        private void OnResetTick(object? sender, EventArgs e)
        {
            try
            {
                string shortcuts = ShortcutsNow(out string? awbShortcut, out string? inventorShortcut);
                HoleCommandOwner? owner = SettingsOwner();
                bool? applied = owner == null ? null : HoleTakeoverRules.ShortcutsApplied(owner.Value, awbShortcut, inventorShortcut);

                HoleShortcutResetStep step = _resetPlan.Check(_sinceReset.Elapsed, MainWindowEnabled(), applied, shortcuts);
                LogStep(step);

                if (step.Action == HoleShortcutResetAction.ReApply)
                    ReapplyAfterReset(owner);
                else if (step.Action == HoleShortcutResetAction.Done)
                    StopResetTimer();
            }
            catch (Exception ex)
            {
                FailReset("checking the shortcuts failed", ex);
            }
        }

        /// <summary>What toggling the setting off and on does (<see cref="HoleTakeoverRules.ResetReapplyOrder"/>).</summary>
        private void ReapplyAfterReset(HoleCommandOwner? owner)
        {
            if (owner == null)
            {
                _log.Warn($"Hole takeover ({ResetReason}): the setting could not be read, so the Hole command is not re-applied.");
                return;
            }

            foreach (HoleCommandOwner step in HoleTakeoverRules.ResetReapplyOrder(owner.Value))
                Apply(step, ResetReason);
        }

        /// <summary>The owner the current settings name (as saved last), or null when they cannot be read.</summary>
        private HoleCommandOwner? SettingsOwner()
        {
            try
            {
                return HoleTakeoverRules.OwnerFor(AddinServices.Settings);
            }
            catch (Exception ex)
            {
                _log.Error($"Hole takeover ({ResetReason}): the setting could not be read.", ex);
                return null;
            }
        }

        /// <summary>The two Hole commands' override shortcuts as read now (null when unreadable), and the log text for them.</summary>
        private string ShortcutsNow(out string? awbShortcut, out string? inventorShortcut)
        {
            awbShortcut = ComSafe.Get(() => _awbHole.OverrideShortcut);
            ControlDefinition? inventorHole = ComSafe.Get(InventorHole);
            inventorShortcut = inventorHole == null ? null : ComSafe.Get(() => inventorHole.OverrideShortcut);
            return HoleTakeoverRules.ShortcutsMessage(awbShortcut, inventorShortcut);
        }

        /// <summary>
        /// Whether Inventor's main window is enabled; a modal dialog such as Customize disables it. Null when unreadable.
        /// </summary>
        private bool? MainWindowEnabled()
        {
            // Read as long so the conversion works whether the interop declares the handle as int or long (as WindowHost).
            long? hwnd = ComSafe.Get(() => (long?)_app.MainFrameHWND);
            if (hwnd == null || hwnd.Value == 0)
                return null;

            try
            {
                return IsWindowEnabled(new IntPtr(hwnd.Value));
            }
            catch (Exception)
            {
                return null;
            }
        }

        private void LogStep(HoleShortcutResetStep step)
        {
            if (step.LogMessage == null)
                return;

            if (step.IsWarning)
                _log.Warn(step.LogMessage);
            else
                _log.Info(step.LogMessage);
        }

        private void FailReset(string what, Exception ex)
        {
            _log.Error($"Hole takeover ({ResetReason}): {what}. Save Settings, or restart Inventor, to give H to the right Hole.", ex);
            _resetPlan.Cancel();
            try
            {
                StopResetTimer();
            }
            catch (Exception stopEx)
            {
                _log.Error($"Hole takeover ({ResetReason}): stopping the check failed.", stopEx);
            }
        }

        private void StopResetTimer()
        {
            _sinceReset.Reset();
            if (_resetTimer == null)
                return;

            _resetTimer.Stop();
            _resetTimer.Tick -= OnResetTick;
            _resetTimer.Dispose();
            _resetTimer = null;
        }

        private ControlDefinition InventorHole() =>
            _app.CommandManager.ControlDefinitions[HoleTakeoverRules.InventorHoleCommand];

        private RibbonPanel ModifyPanel() =>
            _app.UserInterfaceManager.Ribbons[HoleTakeoverRules.RibbonName]
                .RibbonTabs[HoleTakeoverRules.TabId]
                .RibbonPanels[HoleTakeoverRules.PanelId];

        /// <summary>
        /// The Sheet Metal tab's panel that holds Inventor's Hole, found by the control's command over the whole Part
        /// ribbon (<see cref="SheetMetalHolePanel"/>; task 128), or null when there is none. What was found is logged when
        /// it differs from the last lookup. A tab or panel that throws while being read (a contextual tab, another
        /// add-in's tab, a panel not filled yet) is skipped and named in that line, so one bad tab cannot hide the Sheet
        /// Metal one. Throws only when the Part ribbon or its tab list cannot be read; the caller treats that as no panel.
        /// </summary>
        private RibbonPanel? SheetMetalPanel(string reason)
        {
            var read = new List<RibbonPanelControls>();
            var sheetMetalPanels = new Dictionary<string, RibbonPanel>(StringComparer.Ordinal);
            var skipped = new List<string>();

            Ribbon ribbon = _app.UserInterfaceManager.Ribbons[HoleTakeoverRules.RibbonName];
            int tabIndex = 0;
            foreach (RibbonTab tab in ribbon.RibbonTabs)
            {
                tabIndex++;
                string? tabId = null;
                try
                {
                    tabId = ComSafe.Get(() => tab.InternalName);
                    RibbonPanels? panels = ComSafe.Get(() => tab.RibbonPanels);
                    if (tabId == null || panels == null)
                        continue;

                    string? tabName = ComSafe.Get(() => tab.DisplayName);
                    int panelIndex = 0;
                    foreach (RibbonPanel panel in panels)
                    {
                        panelIndex++;
                        string? panelId = null;
                        try
                        {
                            panelId = ComSafe.Get(() => panel.InternalName);
                            if (panelId == null)
                                continue;

                            read.Add(new RibbonPanelControls(tabId, tabName, panelId, ControlNames(panel)));
                            if (SheetMetalHolePanel.IsSheetMetalTab(tabId))
                                sheetMetalPanels[panelId] = panel;
                        }
                        catch (Exception ex)
                        {
                            skipped.Add($"panel {panelId ?? "#" + panelIndex} on tab {tabId} ({ex.Message})");
                        }
                    }
                }
                catch (Exception ex)
                {
                    skipped.Add($"tab {tabId ?? "#" + tabIndex} ({ex.Message})");
                }
            }

            SheetMetalHolePanelChoice choice = SheetMetalHolePanel.Find(read);
            string message = skipped.Count == 0
                ? choice.Message
                : $"{choice.Message} Could not be read, so skipped: {string.Join("; ", skipped)}.";
            if (message != _sheetMetalLookupLogged)
            {
                _sheetMetalLookupLogged = message;
                string line = $"{message} ({reason})";
                if (choice.IsWarning || skipped.Count > 0)
                    _log.Warn(line);
                else
                    _log.Info(line);
            }

            return choice.PanelId != null && sheetMetalPanels.TryGetValue(choice.PanelId, out RibbonPanel? found) ? found : null;
        }

        /// <summary>The internal names of the panel's top-level controls, null where unreadable.</summary>
        private static List<string?> ControlNames(RibbonPanel panel)
        {
            var names = new List<string?>();
            CommandControls? controls = ComSafe.Get(() => panel.CommandControls);
            if (controls == null)
                return names;

            foreach (CommandControl c in controls)
                names.Add(ComSafe.Get(() => c.InternalName));
            return names;
        }

        /// <summary>
        /// Adds AWB Hole's large control before Inventor's Hole: the split button (task 129) when both dropdown commands
        /// are registered and Inventor takes it, else the plain button of task 091. What Inventor reports for a new split
        /// button is logged, so how it is found again can be confirmed.
        /// </summary>
        private CommandControl AddAwbControl(RibbonPanel panel, string reason)
        {
            string panelId = ComSafe.Get(() => panel.InternalName) ?? "(unnamed panel)";
            if (HoleTakeoverRules.ButtonKind(_inventorHoleItem != null, _toggleItem != null) == HoleButtonKind.SplitButton)
            {
                try
                {
                    ObjectCollection items = _app.TransientObjects.CreateObjectCollection();
                    foreach (string command in HoleTakeoverRules.SplitButtonCommands)
                        items.Add(SplitDefinition(command));

                    CommandControl split = panel.CommandControls.AddSplitButton(
                        _awbHole, items, true, true, HoleTakeoverRules.InventorHoleCommand, true);

                    string? name = ComSafe.Get(() => split.InternalName);
                    string? top = ComSafe.Get(() => split.ControlDefinition.InternalName);
                    bool foundAgain = FindAwbControl(panel) != null;
                    string line =
                        $"Hole takeover ({reason}): split button added on panel {panelId}; Inventor names it " +
                        $"'{name ?? "unreadable"}' with top command '{top ?? "unreadable"}'" +
                        (foundAgain ? "." : ", and it cannot be found again, so it may be added twice.");
                    if (foundAgain)
                        _log.Info(line);
                    else
                        _log.Warn(line);
                    return split;
                }
                catch (Exception ex)
                {
                    _log.Warn($"Hole takeover ({reason}): Inventor refused the split button on panel {panelId}, so " +
                              $"{_awbHole.DisplayName} is a plain button there. {ex.Message}");
                }
            }
            else if (!_plainButtonLogged)
            {
                _plainButtonLogged = true;
                _log.Warn(HoleTakeoverRules.PlainButtonMessage(reason));
            }

            return panel.CommandControls.AddButton(_awbHole, true, true, HoleTakeoverRules.InventorHoleCommand, true);
        }

        /// <summary>The definition for one of <see cref="HoleTakeoverRules.SplitButtonCommands"/>.</summary>
        private ButtonDefinition SplitDefinition(string command) => command switch
        {
            CommandNames.AwbHole => _awbHole,
            CommandNames.InventorHole => _inventorHoleItem!,
            CommandNames.UseInventorHoleByDefault => _toggleItem!,
            _ => throw new ArgumentOutOfRangeException(nameof(command), command, "Not a split button command."),
        };

        /// <summary>AWB Hole's control on the panel, plain or split (<see cref="AddinUi.IsControlOf"/>), or null.</summary>
        private CommandControl? FindAwbControl(RibbonPanel panel)
        {
            foreach (CommandControl c in panel.CommandControls)
            {
                if (AddinUi.IsControlOf(_awbHole.InternalName, ComSafe.Get(() => c.InternalName),
                        ComSafe.Get(() => c.ControlDefinition.InternalName), ChildCommands(c)))
                    return c;
            }

            return null;
        }

        /// <summary>
        /// The names and commands of a control's dropdown entries (<c>ChildControls</c>); empty for a plain button or
        /// when unreadable. Used to find our split button again (task 129), here and when the add-in's controls are
        /// removed.
        /// </summary>
        internal static List<string?> ChildCommands(CommandControl control)
        {
            var names = new List<string?>();
            CommandControls? children = ComSafe.Get(() => control.ChildControls);
            if (children == null)
                return names;

            try
            {
                foreach (CommandControl child in children)
                {
                    names.Add(ComSafe.Get(() => child.InternalName));
                    names.Add(ComSafe.Get(() => child.ControlDefinition.InternalName));
                }
            }
            catch (Exception)
            {
                // A dropdown that cannot be listed adds no names; the control's own names are still compared.
            }

            return names;
        }

        /// <summary>
        /// Inventor Hole takes Inventor's Hole icon (task 129, as task 092 does for Edit Feature); where it cannot be
        /// read, the AWB Hole icon it was registered with stays. A failure is logged.
        /// </summary>
        private void CopyInventorHoleIcon()
        {
            if (_inventorHoleItem == null)
                return;

            try
            {
                ControlDefinition inventors = InventorHole();
                stdole.IPictureDisp? standard = ComSafe.Get(() => inventors.StandardIcon);
                stdole.IPictureDisp? large = ComSafe.Get(() => inventors.LargeIcon);
                if (standard == null || large == null)
                    _log.Warn($"Hole takeover: Inventor's Hole icon could not be read; {HoleTakeoverRules.InventorHoleText} " +
                              $"keeps the {_awbHole.DisplayName} icon where it is missing.");

                if (standard != null)
                    _inventorHoleItem.StandardIcon = standard;
                if (large != null)
                    _inventorHoleItem.LargeIcon = large;
            }
            catch (Exception ex)
            {
                _log.Warn($"Hole takeover: giving {HoleTakeoverRules.InventorHoleText} Inventor's Hole icon failed. {ex.Message}");
            }
        }

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

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowEnabled(IntPtr hwnd);

        /// <summary>
        /// The add-in is unloading: Inventor's Hole and H are given back, and the reset event released. <see cref="RibbonSetup"/>
        /// then deletes our buttons from the Modify panel and the Sheet Metal tab, so a reload adds them again (tasks 121
        /// and 128).
        /// </summary>
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

            try
            {
                if (_resetPlan.Cancel())
                    _log.Info($"Hole takeover ({ResetReason}): stopped watching; the add-in is unloading.");
                StopResetTimer();
            }
            catch (Exception ex)
            {
                _log.Warn($"Hole takeover: stopping the shortcut reset check failed. {ex.Message}");
            }

            GiveBack("add-in unloading");
        }
    }
}
