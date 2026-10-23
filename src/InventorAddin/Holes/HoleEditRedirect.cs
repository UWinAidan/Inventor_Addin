using System;
using System.Collections;
using System.Collections.Generic;
using Inventor;
using InventorAddin.Commands;
using InventorAddin.Core.Holes;
using InventorAddin.Core.Logging;
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
    /// <see cref="HoleEditRules"/> makes every decision; this reads Inventor, asks it, and applies the answer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Only while AWB Hole has taken over Inventor's Hole (the switch "Use Inventor's Hole command" off; asked of
    /// <see cref="HoleCommandTakeover"/> at each event, so a change in Settings takes effect at once), and only in a part
    /// open in the window, as for the AWB Hole command.
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
        private readonly Func<HoleCommandOwner?> _owner;
        private readonly ILog _log;

        // Kept referenced, or the events stop firing.
        private UserInputEvents? _input;

        // The hole the last menu or mini toolbar was built for, in case the selection is gone when our entry runs.
        private HoleFeature? _offeredHole;

        /// <summary>The redirect running in this session, or null before the ribbon is built or after unloading.</summary>
        internal static HoleEditRedirect? Active { get; private set; }

        /// <param name="editFeature">Our Edit Feature's definition, kept alive by <see cref="RibbonSetup"/>.</param>
        /// <param name="owner">Who has the Hole command now; null when the takeover is not running.</param>
        public HoleEditRedirect(Application app, ButtonDefinition editFeature, Func<HoleCommandOwner?> owner, ILog log)
        {
            _app = app ?? throw new ArgumentNullException(nameof(app));
            _editFeature = editFeature ?? throw new ArgumentNullException(nameof(editFeature));
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
        }

        /// <summary>Our Edit Feature was pressed: the window opens on the selected hole.</summary>
        internal void EditFeaturePressed()
        {
            HoleFeature? offered = _offeredHole;
            _offeredHole = null;

            PartDocument? doc = CurrentPart();
            if (doc == null)
            {
                _log.Info($"Command {CommandNames.AwbHoleEdit} not run: no part is open in the window.");
                return;
            }

            HoleFeature? hole = SelectedHole(ComSafe.Get(() => doc.SelectSet)) ?? offered;
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

        /// <summary>The viewport's mini toolbar: ours inserted before Inventor's Edit Feature, and Inventor's removed.</summary>
        private void OnContextualMiniToolbar(ObjectsEnumerator selected, NameValueMap displayed, NameValueMap info)
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

                int count = ComSafe.Get(() => displayed.Count);
                var names = new List<string?>();
                for (int i = 1; i <= count; i++)
                {
                    int index = i;
                    names.Add(ComSafe.Get(() => displayed.Name[index]));
                }

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
        /// The backstop: Inventor's hole edit started by some other route. Once its event has returned it is stopped and
        /// our window opened on the selected hole (spike 081).
        /// </summary>
        private void OnActivateCommand(string commandName, NameValueMap context)
        {
            try
            {
                if (!HoleEditRules.IsInventorEdit(commandName))
                    return;

                bool redirects = Redirects(out PartDocument? doc);
                HoleFeature? hole = redirects ? SelectedHole(ComSafe.Get(() => doc!.SelectSet)) : null;
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

        // ---- helpers ----

        /// <summary>True when editing opens AWB Hole now, with the part open in the window.</summary>
        private bool Redirects(out PartDocument? doc)
        {
            doc = CurrentPart();
            return HoleEditRules.Redirects(_owner(), doc != null);
        }

        /// <summary>The part open in the window, or null for any other document (an assembly with a part edited in place).</summary>
        private static PartDocument? CurrentPart()
        {
            Document? doc = ComSafe.Get(() => InventorHost.ActiveDocument);
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

        /// <summary>The one hole a selection is about: holes, and faces holes made (<c>Face.CreatedByFeature</c>).</summary>
        private static HoleFeature? SelectedHole(IEnumerable? selected)
        {
            if (selected == null)
                return null;

            var holes = new List<HoleFeature?>();
            foreach (object item in selected)
                holes.Add(HoleOf(item));
            return HoleEditRules.SingleHole(holes, SameHole);
        }

        private static HoleFeature? HoleOf(object? item) => item switch
        {
            HoleFeature hole => hole,
            Face face => ComSafe.Get(() => face.CreatedByFeature) as HoleFeature,
            _ => null,
        };

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
