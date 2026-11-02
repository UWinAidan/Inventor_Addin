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
    /// hole (spike 103; task 133). <see cref="HoleEditRules"/> and <see cref="HoleTakeoverRules"/> make every decision;
    /// this reads Inventor, asks them, and applies the answer.
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
        private readonly ButtonDefinition? _awbHole;

        // A start of Inventor's mini toolbar Hole the add-in asked for (task 129's "Inventor Hole"), let through once.
        private readonly InventorHoleRunRequest _holeRunRequest = new();

        // The names on a sheet metal part's mini toolbar on a sketch point are logged once per session (task 133).
        private bool _sheetMetalToolbarLogged;
        private readonly Func<HoleCommandOwner?> _owner;
        private readonly ILog _log;

        // Kept referenced, or the events stop firing.
        private UserInputEvents? _input;

        // The hole the last menu or mini toolbar was built for, in case the selection is gone when our entry runs.
        private HoleFeature? _offeredHole;

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
            _input.OnContextualMiniToolbar += OnContextualMiniToolbar;
            _input.OnDoubleClick += OnDoubleClick;
            _input.OnActivateCommand += OnActivateCommand;
            Active = this;
            _log.Info($"Hole edit: Edit Feature and double-click on a hole open {AwbHoleViewModel.CommandName} while it has taken over Inventor's Hole.");
            if (_awbHole == null)
                _log.Warn($"Command {CommandNames.AwbHole} is not registered, so the mini toolbar's Hole opens Inventor's Hole.");
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
        /// The mini toolbar's Hole entry (<c>PartDMHoleCmd</c>; task 133): AWB Hole's button inserted where it is and
        /// Inventor's removed, as for Edit Feature. Its value is Inventor's entry's, or AWB Hole's definition when
        /// Inventor gives none. A failure is logged and leaves Inventor's entry; the Edit Feature swap still runs.
        /// </summary>
        private void ReplaceMiniToolbarHole(ObjectsEnumerator selected, NameValueMap displayed, PartDocument doc)
        {
            if (_awbHole == null)
                return;

            try
            {
                List<string?> names = ToolbarNames(displayed);
                LogSheetMetalToolbar(selected, names, doc);

                int? target = HoleTakeoverRules.MiniToolbarHoleIndex(names, CommandNames.AwbHole);
                if (target == null)
                    return;

                object? value = ComSafe.Get(() => displayed.Value[HoleTakeoverRules.InventorMiniToolbarHoleCommand]);
                displayed.Insert(CommandNames.AwbHole, value ?? _awbHole, target.Value, true);
                displayed.Remove(HoleTakeoverRules.InventorMiniToolbarHoleCommand);
            }
            catch (Exception ex)
            {
                _log.Error("Hole takeover: replacing Hole on the mini toolbar failed; Inventor's is left.", ex);
            }
        }

        /// <summary>
        /// The first mini toolbar on a sketch point in a sheet metal part this session: its entries logged at Info, since
        /// sheet metal may show another Hole command (task 133). Only <c>PartDMHoleCmd</c> is replaced either way.
        /// </summary>
        private void LogSheetMetalToolbar(ObjectsEnumerator selected, List<string?> names, PartDocument doc)
        {
            if (_sheetMetalToolbarLogged || DocumentKinds.Of((Document)doc) != DocumentKind.SheetMetalPart)
                return;

            bool onPoint = false;
            foreach (object item in selected)
            {
                if (item is SketchPoint)
                {
                    onPoint = true;
                    break;
                }
            }

            if (!onPoint)
                return;

            _sheetMetalToolbarLogged = true;
            _log.Info(HoleTakeoverRules.SheetMetalMiniToolbarMessage(names));
        }

        /// <summary>
        /// Each command Inventor starts; the run request (task 129) sees every one. Inventor's mini toolbar Hole, started
        /// by some other route, is stopped and AWB Hole opened (task 133). Inventor's hole edit, the backstop for Edit
        /// Feature, is stopped and our window opened on the selected hole (spike 081). Each is done once Inventor's event
        /// has returned.
        /// </summary>
        private void OnActivateCommand(string commandName, NameValueMap context)
        {
            try
            {
                bool asked = _holeRunRequest.StartIsAskedFor(commandName);
                if (HoleTakeoverRules.IsInventorMiniToolbarHole(commandName))
                {
                    StopInventorsMiniToolbarHole(commandName, asked);
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
        /// The mini toolbar backstop (task 133): Inventor's <c>PartDMHoleCmd</c> started while AWB Hole has Hole, and the
        /// add-in did not ask for it. Once its event has returned it is stopped and AWB Hole opened on the part, as its
        /// button does.
        /// </summary>
        private void StopInventorsMiniToolbarHole(string commandName, bool asked)
        {
            PartDocument? doc = CurrentPart();
            if (!HoleTakeoverRules.StopsInventorMiniToolbarHole(commandName, _owner(), doc != null, asked))
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
                    _log.Error("Hole takeover: stopping Inventor's Hole from the mini toolbar failed.", ex);
                }

                try
                {
                    _log.Info($"Hole takeover: Inventor's Hole from the mini toolbar started; {AwbHoleViewModel.CommandName} opens instead.");
                    AwbHoleWindowLauncher.ShowNew(doc!);
                }
                catch (Exception ex)
                {
                    _log.Error($"Hole takeover: opening {AwbHoleViewModel.CommandName} after Inventor's Hole failed.", ex);
                }
            }));
        }

        // ---- helpers ----

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

        /// <summary>The add-in is unloading: the events are released, and Inventor's Edit Feature is its own again.</summary>
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
