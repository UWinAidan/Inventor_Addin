using System;
using System.Collections;
using System.Collections.Generic;
using Inventor;
using InventorAddin.Commands;
using InventorAddin.Core.Holes;
using InventorAddin.Core.Logging;
using InventorAddin.Core.Models;
using InventorAddin.Core.Ribbon;
using InventorAddin.Core.ViewModels;
using InventorAddin.UI;

namespace InventorAddin.Holes
{
    /// <summary>
    /// Every way of editing a hole opens the AWB Hole window on it (spec 09, "Editing opens AWB Hole"; spike 081 rounds
    /// 2 to 4; task 092): Inventor's Edit Feature on the browser's right-click menu and on the viewport's mini toolbar is
    /// swapped for ours (<see cref="HoleEditFeatureCommand"/>, with Inventor's icon), a double-click on a hole or a face
    /// it made opens our window, and Inventor's own hole edit, if it starts anyway, is stopped and our window opened.
    /// The mini toolbar's Hole entry, shown on a sketch point (<c>PartDMHoleCmd</c>, not the ribbon's Hole), is swapped
    /// for AWB Hole's button the same way, and Inventor's, if it starts anyway, is stopped and AWB Hole opened for a new
    /// hole (spike 103; task 133). The right-click marking menu's ring gets AWB Hole's button in the slot of Inventor's
    /// Hole (<c>PartHoleCmd</c>), and Inventor's ribbon Hole, if it starts anyway, is stopped the same way (task 142).
    /// <see cref="HoleEditRules"/> and <see cref="HoleTakeoverRules"/> make every decision; this reads Inventor, asks
    /// them, and applies the answer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Only while AWB Hole has taken over Inventor's Hole (the switch "Use Inventor's Hole command" off; asked of
    /// <see cref="HoleCommandTakeover"/> at each event, so a change in Settings takes effect at once), and only while a
    /// part is being edited: the part open in the window, or a part edited in place inside an assembly (task 105), as
    /// for the AWB Hole command.
    /// </para>
    /// <para>
    /// In place, Inventor gives a hole, or a face it made, as a proxy in the assembly (a <c>HoleFeatureProxy</c> or a
    /// <c>FaceProxy</c>); the part's own hole is used instead (spike 103). The selection is read from the part, then
    /// from the active document, where Inventor keeps it in place.
    /// </para>
    /// <para>
    /// The window is always opened after Inventor's event has returned (<see cref="AwbHoleWindowLauncher.ShowForEdit"/>
    /// defers it; spike 081 round 3), and a request while it is open brings it to the front. Nothing here throws into
    /// Inventor's events: a failure is logged and Inventor's own handling goes on.
    /// </para>
    /// </remarks>
    public sealed class HoleEditRedirect : IDisposable
    {
        private const string RouteMenu = "Edit Feature";
        private const string RouteDoubleClick = "double-click";
        private const string RouteBackstop = "Inventor's Edit Feature";

        private readonly Application _app;
        private readonly ButtonDefinition _editFeature;

        // AWB Hole's definition, kept alive by RibbonSetup; null when it failed to register (the Hole entry is then left).
        // Inventor finds the command by the entry's name, so only whether it is registered matters here.
        private readonly ButtonDefinition? _awbHole;

        // A start of Inventor's mini toolbar Hole the add-in asked for (task 129's "Inventor Hole"), let through once.
        private readonly InventorHoleRunRequest _holeRunRequest = new();
        private readonly Func<HoleCommandOwner?> _owner;
        private readonly ILog _log;

        // Kept referenced, or the events stop firing.
        private UserInputEvents? _input;

        // The hole the last menu or mini toolbar was built for, in case the selection is gone when our entry runs.
        private HoleFeature? _offeredHole;

        // The marking menu rings already logged this session (task 142), by HoleTakeoverRules.RingLogKey.
        private readonly HashSet<string> _loggedRings = new(StringComparer.Ordinal);

        /// <summary>The redirect running in this session, or null before the ribbon is built or after unloading.</summary>
        internal static HoleEditRedirect? Active { get; private set; }

        /// <param name="editFeature">Our Edit Feature's definition, kept alive by <see cref="RibbonSetup"/>.</param>
        /// <param name="awbHole">
        /// AWB Hole's definition, kept alive by <see cref="RibbonSetup"/>, for the mini toolbar's Hole entry; null leaves
        /// Inventor's entry.
        /// </param>
        /// <param name="owner">Who has the Hole command now; null when the takeover is not running.</param>
        public HoleEditRedirect(
            Application app, ButtonDefinition editFeature, ButtonDefinition? awbHole, Func<HoleCommandOwner?> owner, ILog log)
        {
            _app = app ?? throw new ArgumentNullException(nameof(app));
            _editFeature = editFeature ?? throw new ArgumentNullException(nameof(editFeature));
            _awbHole = awbHole;
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        /// <summary>
        /// Gives our Edit Feature Inventor's icon (the mini toolbar shows icons only), else <paramref name="fallback"/>,
        /// and starts following the edit events. Call once, after the ribbon is built.
        /// </summary>
        public void Start(ButtonPictures? fallback)
        {
            CopyInventorsIcon(fallback);

            _input = _app.CommandManager.UserInputEvents;
            _input.OnLinearMarkingMenu += OnLinearMarkingMenu;
            _input.OnRadialMarkingMenu += OnRadialMarkingMenu;
            _input.OnContextualMiniToolbar += OnContextualMiniToolbar;
            _input.OnDoubleClick += OnDoubleClick;
            _input.OnActivateCommand += OnActivateCommand;
            Active = this;
            _log.Info($"Hole edit: Edit Feature and double-click on a hole open {AwbHoleViewModel.CommandName} while it has taken over Inventor's Hole.");
            if (_awbHole == null)
                _log.Warn($"Command {CommandNames.AwbHole} is not registered, so the mini toolbar's and the marking menu's Hole show Inventor's Hole.");
        }

        /// <summary>
        /// The add-in is about to run Inventor's Hole itself (task 129's "Inventor Hole"): the next start of Inventor's
        /// mini toolbar Hole is let through, not stopped by the backstop.
        /// </summary>
        internal void InventorHoleAsked() => _holeRunRequest.Ask();

        /// <summary>Our Edit Feature was pressed: the window opens on the selected hole.</summary>
        internal void EditFeaturePressed()
        {
            HoleFeature? offered = _offeredHole;
            _offeredHole = null;

            PartDocument? doc = CurrentPart();
            if (doc == null)
            {
                _log.Info($"Command {CommandNames.AwbHoleEdit} not run: no part is being edited.");
                return;
            }

            HoleFeature? hole = SelectedHoleNow(doc) ?? offered;
            if (hole == null)
            {
                _log.Warn($"Command {CommandNames.AwbHoleEdit} not run: no single hole is selected.");
                return;
            }

            Open(doc, hole, RouteMenu);
        }

        // ---- events ----

        /// <summary>The browser's right-click menu: ours put where Inventor's Edit Feature is, and Inventor's deleted.</summary>
        private void OnLinearMarkingMenu(ObjectsEnumerator selected, SelectionDeviceEnum device, CommandControls menu, NameValueMap info)
        {
            try
            {
                // A menu built for no hole (or another part) leaves no hole behind for our Edit Feature to fall back on.
                _offeredHole = null;
                if (!Redirects(out _))
                    return;

                HoleFeature? hole = SelectedHole(selected);
                if (hole == null)
                    return;

                var names = new List<string?>();
                foreach (CommandControl control in menu)
                    names.Add(ComSafe.Get(() => control.InternalName));
                if (!HoleEditRules.ReplacesMenuEntry(names, CommandNames.AwbHoleEdit))
                    return;

                menu.AddButton(_editFeature, false, true, HoleEditRules.InventorEditCommand, true);
                menu[HoleEditRules.InventorEditCommand].Delete();
                _offeredHole = hole;
            }
            catch (Exception ex)
            {
                _log.Error("Hole edit: replacing Edit Feature on the right-click menu failed; Inventor's is left.", ex);
            }
        }

        /// <summary>
        /// The viewport's mini toolbar: AWB Hole inserted before Inventor's Hole entry, whatever was clicked, and our Edit
        /// Feature before Inventor's on a hole; Inventor's removed in each case.
        /// </summary>
        private void OnContextualMiniToolbar(ObjectsEnumerator selected, NameValueMap displayed, NameValueMap info)
        {
            try
            {
                // A menu built for no hole (or another part) leaves no hole behind for our Edit Feature to fall back on.
                _offeredHole = null;
                if (!Redirects(out PartDocument? doc))
                    return;

                ReplaceMiniToolbarHole(selected, displayed, doc!);

                HoleFeature? hole = SelectedHole(selected);
                if (hole == null)
                    return;

                List<string?> names = ToolbarNames(displayed);
                int? target = HoleEditRules.MiniToolbarIndex(names, CommandNames.AwbHoleEdit);
                if (target == null)
                    return;

                // As spike 081: the entry's value goes with our name, or our definition when Inventor gives none.
                object? value = ComSafe.Get(() => displayed.Value[HoleEditRules.InventorEditCommand]);
                displayed.Insert(CommandNames.AwbHoleEdit, value ?? _editFeature, target.Value, true);
                displayed.Remove(HoleEditRules.InventorEditCommand);
                _offeredHole = hole;
            }
            catch (Exception ex)
            {
                _log.Error("Hole edit: replacing Edit Feature on the mini toolbar failed; Inventor's is left.", ex);
            }
        }

        /// <summary>
        /// The right-click marking menu's ring (task 142): AWB Hole's button put in the slot of Inventor's Hole, except in
        /// a flat pattern, which keeps Inventor's Hole (<see cref="HoleTakeoverRules.TakesOverHoleEntries"/>). The ring
        /// is a <c>RadialMarkingMenu</c> of eight slots, not a <c>NameValueMap</c>; the API reference says changes made
        /// to it here are transient, used for this display only.
        /// </summary>
        private void OnRadialMarkingMenu(ObjectsEnumerator selected, SelectionDeviceEnum device, RadialMarkingMenu ring, NameValueMap info)
        {
            try
            {
                PartDocument? doc = CurrentPart();
                if (_awbHole == null || !HoleTakeoverRules.TakesOverHoleEntries(_owner(), doc != null, InFlatPattern()))
                    return;

                ReplaceRingHole(ring, doc!);
            }
            catch (Exception ex)
            {
                _log.Error("Hole takeover: replacing Hole on the marking menu failed; Inventor's may be left.", ex);
            }
        }

        /// <summary>
        /// Puts AWB Hole's <c>ButtonDefinition</c> in each slot holding Inventor's Hole
        /// (<see cref="HoleTakeoverRules.RingHoleSlots"/>), then reads the ring back, as task 141 does for the mini
        /// toolbar. Each ring is logged at Info once per session, by its name and the part kind
        /// (<see cref="HoleTakeoverRules.RingLogKey"/>), before and after the swap.
        /// </summary>
        /// <remarks>
        /// If the ring after the swap is not Inventor's with only Hole's slot changed
        /// (<see cref="HoleTakeoverRules.RingSwapHeld"/>), a warning is logged and each slot that changed is set back to
        /// the object Inventor gave (<see cref="HoleTakeoverRules.RingSlotsToRestore"/>), each on its own so one refusal
        /// does not stop the rest; the ring is then logged again. Its Hole then runs <c>PartHoleCmd</c>, which the
        /// backstop (<see cref="StopInventorsHole"/>) turns into AWB Hole.
        /// </remarks>
        private void ReplaceRingHole(RadialMarkingMenu ring, PartDocument doc)
        {
            object?[] controls = RingControls(ring);
            List<string?> before = RingNames(controls);
            string? ringName = ComSafe.Get(() => ring.InternalName);
            bool sheetMetal = DocumentKinds.Of((Document)doc) == DocumentKind.SheetMetalPart;
            bool firstTime = _loggedRings.Add(HoleTakeoverRules.RingLogKey(ringName, sheetMetal));
            if (firstTime)
                _log.Info(HoleTakeoverRules.RingMessage(MiniToolbarStage.BeforeSwap, ringName, before, sheetMetal));

            IReadOnlyList<int> slots = HoleTakeoverRules.RingHoleSlots(before, CommandNames.AwbHole);
            if (slots.Count == 0)
                return;

            string? error = null;
            try
            {
                foreach (int slot in slots)
                    SetRingSlot(ring, slot, _awbHole);
            }
            catch (Exception ex)
            {
                error = ex.Message;
            }

            List<string?> after = RingNames(RingControls(ring));
            if (error == null && HoleTakeoverRules.RingSwapHeld(before, after, CommandNames.AwbHole))
            {
                if (firstTime)
                    _log.Info(HoleTakeoverRules.RingMessage(MiniToolbarStage.AfterSwap, ringName, after, sheetMetal));
                return;
            }

            _log.Warn(HoleTakeoverRules.RingSwapFailedMessage(
                HoleTakeoverRules.SwappedRing(before, CommandNames.AwbHole), after, error));
            var failed = new List<string>();
            foreach (int slot in HoleTakeoverRules.RingSlotsToRestore(before, after))
            {
                int index = slot;
                ComSafe.Run(() => SetRingSlot(ring, index, controls[index]), failed, HoleTakeoverRules.RingSlots[index]);
            }

            if (failed.Count > 0)
                _log.Error($"Hole takeover: putting the marking menu ring back failed for: {string.Join("; ", failed)}.");
            _log.Info(HoleTakeoverRules.RingMessage(
                MiniToolbarStage.PutBack, ringName, RingNames(RingControls(ring)), sheetMetal));
        }

        /// <summary>
        /// The ring's eight slots, in <see cref="HoleTakeoverRules.RingSlots"/> order; null for an empty slot, or one that
        /// could not be read.
        /// </summary>
        private static object?[] RingControls(RadialMarkingMenu ring)
        {
            var controls = new object?[HoleTakeoverRules.RingSlots.Count];
            for (int i = 0; i < controls.Length; i++)
            {
                int index = i;
                controls[i] = ComSafe.Get(() => GetRingSlot(ring, index));
            }

            return controls;
        }

        /// <summary>
        /// Each slot's command name: a command's <c>InternalName</c>, "submenu " and a sub-menu's name, null for an empty
        /// slot, and an empty string for anything whose name could not be read.
        /// </summary>
        private static List<string?> RingNames(object?[] controls)
        {
            var names = new List<string?>(controls.Length);
            foreach (object? control in controls)
            {
                names.Add(control switch
                {
                    null => null,
                    ButtonDefinition button => ComSafe.Get(() => button.InternalName) ?? string.Empty,
                    ControlDefinition definition => ComSafe.Get(() => definition.InternalName) ?? string.Empty,
                    RadialMarkingMenu submenu => "submenu " + (ComSafe.Get(() => submenu.InternalName) ?? "(unnamed)"),
                    _ => string.Empty,
                });
            }

            return names;
        }

        private static object? GetRingSlot(RadialMarkingMenu ring, int index) => index switch
        {
            0 => ring.NorthControl,
            1 => ring.NortheastControl,
            2 => ring.EastControl,
            3 => ring.SoutheastControl,
            4 => ring.SouthControl,
            5 => ring.SouthwestControl,
            6 => ring.WestControl,
            7 => ring.NorthwestControl,
            _ => throw new ArgumentOutOfRangeException(nameof(index)),
        };

        private static void SetRingSlot(RadialMarkingMenu ring, int index, object? control)
        {
            switch (index)
            {
                case 0: ring.NorthControl = control; break;
                case 1: ring.NortheastControl = control; break;
                case 2: ring.EastControl = control; break;
                case 3: ring.SoutheastControl = control; break;
                case 4: ring.SouthControl = control; break;
                case 5: ring.SouthwestControl = control; break;
                case 6: ring.WestControl = control; break;
                case 7: ring.NorthwestControl = control; break;
                default: throw new ArgumentOutOfRangeException(nameof(index));
            }
        }

        /// <summary>A double-click on a hole, in the browser or on a face it made: our window, not Inventor's.</summary>
        private void OnDoubleClick(
            ObjectsEnumerator selected, SelectionDeviceEnum device, MouseButtonEnum button, ShiftStateEnum shiftKeys,
            Point modelPosition, Point2d viewPosition, View view, NameValueMap additionalInfo,
            out HandlingCodeEnum handlingCode)
        {
            handlingCode = HandlingCodeEnum.kEventNotHandled;
            try
            {
                if (!Redirects(out PartDocument? doc))
                    return;

                HoleFeature? hole = SelectedHole(selected);
                if (hole == null)
                    return;

                handlingCode = HandlingCodeEnum.kEventHandled;
                Open(doc!, hole, RouteDoubleClick);
            }
            catch (Exception ex)
            {
                handlingCode = HandlingCodeEnum.kEventNotHandled;
                _log.Error("Hole edit: opening the window on a double-click failed; Inventor's edit is left.", ex);
            }
        }

        /// <summary>
        /// The mini toolbar's Hole entry (<c>PartDMHoleCmd</c>; task 133): AWB Hole's entry inserted where it is and
        /// Inventor's removed, so the list is Inventor's with that one change (task 141). The entry's value is Inventor's
        /// entry's value, as it is: the API reference says a mini toolbar entry's value is the entity Inventor highlights
        /// when the entry is hovered, and the command is found by the entry's name. Task 133 put AWB Hole's
        /// <c>ButtonDefinition</c> there when Inventor's value was empty, which is not an entity; the leading suspect for
        /// the entries from Hole onward being lost (Aidan's checks 133-1, 133-2, 133-5).
        /// </summary>
        /// <remarks>
        /// <para>
        /// The entries are logged at Info before and after the swap on each sketch point mini toolbar, and on any toolbar
        /// the swap runs on. A failure of <c>Insert</c> or <c>Remove</c> is caught and the list checked like any other
        /// result; nothing here throws into Inventor's event.
        /// </para>
        /// <para>
        /// If the list after the swap is not the expected one: when it is still Inventor's list (the swap changed nothing,
        /// for example <c>Insert</c> refused the value), the map is left alone and a warning logged. Otherwise a warning
        /// is logged and only what changed is undone (<see cref="UndoSwap"/>); only if that does not give Inventor's list
        /// back is the map cleared and each entry added again. A broken toolbar is worse than Inventor's Hole, whose start
        /// the backstop (<see cref="StopInventorsMiniToolbarHole"/>) still turns into AWB Hole. The Edit Feature swap runs
        /// either way.
        /// </para>
        /// </remarks>
        private void ReplaceMiniToolbarHole(ObjectsEnumerator selected, NameValueMap displayed, PartDocument doc)
        {
            // The Flat Pattern tab keeps Inventor's Hole (task 142).
            if (_awbHole == null || !HoleTakeoverRules.TakesOverHoleEntries(_owner(), true, InFlatPattern()))
                return;

            try
            {
                List<string?> before = ToolbarNames(displayed);
                var unreadableValues = new List<string?>();
                List<object?> values = ToolbarValues(displayed, before, unreadableValues);
                int? target = HoleTakeoverRules.MiniToolbarHoleIndex(before, CommandNames.AwbHole);
                bool onPoint = HasSketchPoint(selected);
                if (target == null && !onPoint)
                    return;

                bool sheetMetal = DocumentKinds.Of((Document)doc) == DocumentKind.SheetMetalPart;
                _log.Info(HoleTakeoverRules.MiniToolbarMessage(MiniToolbarStage.BeforeSwap, before, ValueKinds(values), sheetMetal));
                if (target == null)
                    return;

                object? holeValue = values[target.Value - 1];
                string? error = null;
                try
                {
                    displayed.Insert(CommandNames.AwbHole, holeValue, target.Value, true);
                    displayed.Remove(HoleTakeoverRules.InventorMiniToolbarHoleCommand);
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                }

                List<string?> after = ToolbarNames(displayed);
                _log.Info(HoleTakeoverRules.MiniToolbarMessage(
                    MiniToolbarStage.AfterSwap, after, ValueKinds(ToolbarValues(displayed, after)), sheetMetal));
                if (error == null && HoleTakeoverRules.MiniToolbarSwapHeld(before, after, CommandNames.AwbHole))
                    return;

                // Still Inventor's list: never touch an intact list.
                if (HoleTakeoverRules.SameMiniToolbar(before, after))
                {
                    _log.Warn(HoleTakeoverRules.MiniToolbarSwapLeftAloneMessage(before, error));
                    return;
                }

                _log.Warn(HoleTakeoverRules.MiniToolbarSwapFailedMessage(
                    HoleTakeoverRules.SwappedMiniToolbar(before, CommandNames.AwbHole), after, error));
                if (!UndoSwap(displayed, before, after, holeValue))
                    PutBack(displayed, before, values, unreadableValues);

                List<string?> putBack = ToolbarNames(displayed);
                _log.Info(HoleTakeoverRules.MiniToolbarMessage(
                    MiniToolbarStage.PutBack, putBack, ValueKinds(ToolbarValues(displayed, putBack)), sheetMetal));
            }
            catch (Exception ex)
            {
                _log.Error("Hole takeover: replacing Hole on the mini toolbar failed; Inventor's list may not have been put back.", ex);
            }
        }

        /// <summary>
        /// Undoes only what the swap changed (<see cref="HoleTakeoverRules.UndoMiniToolbarSwap"/>): AWB Hole's entry
        /// removed if it is there, and Inventor's Hole put back with its value at its old place if it is missing. True
        /// when the list is then Inventor's again.
        /// </summary>
        private bool UndoSwap(NameValueMap displayed, List<string?> before, List<string?> now, object? holeValue)
        {
            MiniToolbarUndo undo = HoleTakeoverRules.UndoMiniToolbarSwap(before, now, CommandNames.AwbHole);
            try
            {
                if (undo.RemoveOurs)
                    displayed.Remove(CommandNames.AwbHole);

                if (undo.ReinsertInventors)
                {
                    if (undo.InsertBeforeIndex is int index)
                        displayed.Insert(HoleTakeoverRules.InventorMiniToolbarHoleCommand, holeValue, index, true);
                    else
                        displayed.Add(HoleTakeoverRules.InventorMiniToolbarHoleCommand, holeValue);
                }
            }
            catch (Exception ex)
            {
                _log.Warn($"Hole takeover: undoing the mini toolbar swap failed ({ex.Message}); Inventor's entries are added again one by one.");
                return false;
            }

            if (HoleTakeoverRules.SameMiniToolbar(before, ToolbarNames(displayed)))
                return true;

            _log.Warn("Hole takeover: undoing the mini toolbar swap did not give Inventor's list back; its entries are added again one by one.");
            return false;
        }

        /// <summary>
        /// The last resort: the map cleared, then each of Inventor's entries added again in order with its value, each
        /// add guarded on its own so one refusal does not lose the rest. What could not be added, and what went back
        /// with no value because its value was unreadable before the swap, is logged.
        /// </summary>
        private void PutBack(NameValueMap displayed, List<string?> names, List<object?> values, List<string?> unreadableValues)
        {
            try
            {
                displayed.Clear();
            }
            catch (Exception ex)
            {
                _log.Error("Hole takeover: clearing the mini toolbar to put Inventor's list back failed; it is left as it is.", ex);
                return;
            }

            var failed = new List<string>();
            for (int i = 0; i < names.Count; i++)
            {
                string name = names[i]!;
                object? value = values[i];
                ComSafe.Run(() => displayed.Add(name, value), failed, name);
            }

            string message = HoleTakeoverRules.MiniToolbarPutBackMessage(failed, unreadableValues);
            if (failed.Count > 0)
                _log.Error(message);
            else
                _log.Warn(message);
        }

        /// <summary>True when a sketch point is selected (in place, a proxy of one).</summary>
        private static bool HasSketchPoint(ObjectsEnumerator selected)
        {
            foreach (object item in selected)
            {
                if (item is SketchPoint)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Each command Inventor starts; the run request (task 129) sees every one. Inventor's mini toolbar Hole, or its
        /// ribbon Hole (the marking menu's), started by some other route, is stopped and AWB Hole opened (tasks 133, 142). Inventor's hole edit, the backstop for Edit
        /// Feature, is stopped and our window opened on the selected hole (spike 081). Each is done once Inventor's event
        /// has returned.
        /// </summary>
        private void OnActivateCommand(string commandName, NameValueMap context)
        {
            try
            {
                bool asked = _holeRunRequest.StartIsAskedFor(commandName);
                if (HoleTakeoverRules.IsInventorHole(commandName))
                {
                    StopInventorsHole(commandName, asked);
                    return;
                }

                if (!HoleEditRules.IsInventorEdit(commandName))
                    return;

                bool redirects = Redirects(out PartDocument? doc);
                HoleFeature? hole = redirects ? SelectedHoleNow(doc!) : null;
                if (!HoleEditRules.StopsInventorEdit(commandName, redirects, hole != null))
                {
                    if (redirects)
                        _log.Info("Hole edit: Inventor's Edit Feature started with no single hole selected; it is left to run.");
                    return;
                }

                System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        _app.CommandManager.StopActiveCommand();
                    }
                    catch (Exception ex)
                    {
                        _log.Error("Hole edit: stopping Inventor's Edit Feature failed.", ex);
                    }

                    try
                    {
                        Open(doc!, hole!, RouteBackstop);
                    }
                    catch (Exception ex)
                    {
                        _log.Error("Hole edit: opening the window after Inventor's Edit Feature failed.", ex);
                    }
                }));
            }
            catch (Exception ex)
            {
                _log.Error("Hole edit: following Inventor's Edit Feature failed.", ex);
            }
        }

        /// <summary>
        /// The backstop (tasks 133 and 142): Inventor's <c>PartDMHoleCmd</c> (the mini toolbar's Hole) or
        /// <c>PartHoleCmd</c> (the ribbon's, which the marking menu's ring shows) started while AWB Hole has Hole, and
        /// the add-in did not ask for it, outside a flat pattern (the Flat Pattern tab keeps Inventor's Hole). Once its
        /// event has returned it is stopped and AWB Hole opened on the part, as its button does.
        /// </summary>
        private void StopInventorsHole(string commandName, bool asked)
        {
            PartDocument? doc = CurrentPart();
            if (!HoleTakeoverRules.StopsInventorHole(commandName, _owner(), doc != null, InFlatPattern(), asked))
            {
                if (asked)
                    _log.Info("Hole takeover: Inventor's Hole was asked for; it is left to run.");
                return;
            }

            System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    _app.CommandManager.StopActiveCommand();
                }
                catch (Exception ex)
                {
                    _log.Error($"Hole takeover: stopping Inventor's Hole ({commandName}) failed.", ex);
                }

                try
                {
                    _log.Info(HoleTakeoverRules.InventorHoleStoppedMessage(commandName));
                    AwbHoleWindowLauncher.ShowNew(doc!);
                }
                catch (Exception ex)
                {
                    _log.Error($"Hole takeover: opening {AwbHoleViewModel.CommandName} after Inventor's Hole failed.", ex);
                }
            }));
        }

        // ---- helpers ----

        /// <summary>
        /// Each entry's value, by name, in the order of <paramref name="names"/>; null for no value, an unreadable one,
        /// or an unreadable name (reading a missing name would add it to the map). The names whose value could not be
        /// read are added to <paramref name="unreadable"/> when it is given.
        /// </summary>
        private static List<object?> ToolbarValues(NameValueMap displayed, List<string?> names, List<string?>? unreadable = null)
        {
            var values = new List<object?>(names.Count);
            foreach (string? name in names)
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    values.Add(null);
                    continue;
                }

                try
                {
                    values.Add(displayed.Value[name]);
                }
                catch (Exception)
                {
                    values.Add(null);
                    unreadable?.Add(name);
                }
            }

            return values;
        }

        /// <summary>Each value's COM type name, for the log; null for no value.</summary>
        private static List<string?> ValueKinds(List<object?> values)
        {
            var kinds = new List<string?>(values.Count);
            foreach (object? value in values)
                kinds.Add(value == null ? null : ComSafe.Get(() => Microsoft.VisualBasic.Information.TypeName(value)));

            return kinds;
        }

        /// <summary>The mini toolbar's entry names, in order (the map is 1-based); an unreadable name is null.</summary>
        private static List<string?> ToolbarNames(NameValueMap displayed)
        {
            int count = ComSafe.Get(() => displayed.Count);
            var names = new List<string?>();
            for (int i = 1; i <= count; i++)
            {
                int index = i;
                names.Add(ComSafe.Get(() => displayed.Name[index]));
            }

            return names;
        }

        /// <summary>
        /// True while a flat pattern is being edited (task 142): <c>ActiveEditObject</c> is the <c>FlatPattern</c>, or the
        /// active environment's name says so (<see cref="HoleTakeoverRules.InFlatPattern"/>).
        /// </summary>
        private bool InFlatPattern()
        {
            object? editing = ComSafe.Get(() => _app.ActiveEditObject);
            string? environment = ComSafe.Get(() => _app.UserInterfaceManager.ActiveEnvironment.InternalName);
            return HoleTakeoverRules.InFlatPattern(editing is FlatPattern, environment);
        }

        /// <summary>True when editing opens AWB Hole now, with a part being edited.</summary>
        private bool Redirects(out PartDocument? doc)
        {
            doc = CurrentPart();
            return HoleEditRules.Redirects(_owner(), doc != null);
        }

        /// <summary>
        /// The part being edited (<c>ActiveEditDocument</c>): the part open in the window, or a part edited in place
        /// inside an assembly. Null for any other document.
        /// </summary>
        private static PartDocument? CurrentPart()
        {
            Document? doc = ComSafe.Get(() => InventorHost.ActiveEditDocument);
            if (doc == null || !DocumentKinds.Of(doc).IsPart())
                return null;
            return doc as PartDocument;
        }

        private void Open(PartDocument doc, HoleFeature hole, string route)
        {
            string? name = ComSafe.Get(() => hole.Name);
            bool opening = AwbHoleWindowLauncher.ShowForEdit(doc, hole);
            _log.Info(opening ? HoleEditRules.OpeningMessage(route, name) : HoleEditRules.AlreadyOpenMessage(route, name));
        }

        /// <summary>
        /// The one hole selected now: in the part's selection, else, when the part is edited in place, in the active
        /// document's (spike 103).
        /// </summary>
        private HoleFeature? SelectedHoleNow(PartDocument doc)
        {
            HoleFeature? hole = SelectedHole(ComSafe.Get(() => doc.SelectSet));
            if (hole != null)
                return hole;

            Document? active = ComSafe.Get(() => _app.ActiveDocument);
            if (active == null || ReferenceEquals(active, doc))
                return null;
            return SelectedHole(ComSafe.Get(() => active.SelectSet));
        }

        /// <summary>The one hole a selection is about: holes, and faces holes made (<c>Face.CreatedByFeature</c>).</summary>
        private HoleFeature? SelectedHole(IEnumerable? selected)
        {
            if (selected == null)
                return null;

            var holes = new List<HoleFeature?>();
            foreach (object item in selected)
                holes.Add(HoleOf(item));
            return HoleEditRules.SingleHole(holes, SameHole);
        }

        /// <summary>
        /// The hole an item is, or made it: a hole, or a face (a <c>FaceProxy</c> in place) through
        /// <c>CreatedByFeature</c>. A <c>HoleFeatureProxy</c>, given or found, is taken as the part's own hole; one that
        /// gives no native object is logged and skipped. Null for anything else, a face of another feature included.
        /// </summary>
        private HoleFeature? HoleOf(object? item)
        {
            object? found = item switch
            {
                HoleFeature hole => hole,
                Face face => ComSafe.Get(() => face.CreatedByFeature),
                _ => null,
            };
            if (found is not HoleFeatureProxy proxy)
                return found as HoleFeature;

            HoleFeature? native = ComSafe.Get(() => proxy.NativeObject);
            if (native == null)
                _log.Warn("Hole edit: a hole selected in the assembly gave no hole in the part; it is skipped.");
            return native;
        }

        private static bool SameHole(HoleFeature a, HoleFeature b)
        {
            string? name = ComSafe.Get(() => a.Name);
            return name != null && string.Equals(name, ComSafe.Get(() => b.Name), StringComparison.Ordinal);
        }

        /// <summary>
        /// Inventor's Edit Feature icon on ours (spike 081: <c>StandardIcon</c> and <c>LargeIcon</c> copied), each
        /// falling back to <paramref name="fallback"/>'s. A failure is logged; the entry then shows text only, and the
        /// mini toolbar an empty button.
        /// </summary>
        private void CopyInventorsIcon(ButtonPictures? fallback)
        {
            try
            {
                ControlDefinition inventors = _app.CommandManager.ControlDefinitions[HoleEditRules.InventorEditCommand];
                stdole.IPictureDisp? standard = ComSafe.Get(() => inventors.StandardIcon);
                stdole.IPictureDisp? large = ComSafe.Get(() => inventors.LargeIcon);
                if (standard == null || large == null)
                    _log.Warn("Hole edit: Inventor's Edit Feature icon could not be read; ours uses the " +
                              $"{AwbHoleViewModel.CommandName} icon where it is missing.");

                standard ??= fallback?.Standard as stdole.IPictureDisp;
                large ??= fallback?.Large as stdole.IPictureDisp;
                if (standard != null)
                    _editFeature.StandardIcon = standard;
                if (large != null)
                    _editFeature.LargeIcon = large;
            }
            catch (Exception ex)
            {
                _log.Error("Hole edit: giving our Edit Feature an icon failed; the mini toolbar may show it without one.", ex);
            }
        }

        /// <summary>
        /// The add-in is unloading: the events are released, and Inventor's Edit Feature, its mini toolbar Hole and its
        /// marking menu Hole are its own again.
        /// </summary>
        public void Dispose()
        {
            if (ReferenceEquals(Active, this))
                Active = null;
            _offeredHole = null;

            UserInputEvents? input = _input;
            _input = null;
            if (input == null)
                return;

            try
            {
                input.OnLinearMarkingMenu -= OnLinearMarkingMenu;
                input.OnRadialMarkingMenu -= OnRadialMarkingMenu;
                input.OnContextualMiniToolbar -= OnContextualMiniToolbar;
                input.OnDoubleClick -= OnDoubleClick;
                input.OnActivateCommand -= OnActivateCommand;
            }
            catch (Exception ex)
            {
                _log.Warn($"Hole edit: releasing the edit events failed. {ex.Message}");
            }
        }
    }
}
