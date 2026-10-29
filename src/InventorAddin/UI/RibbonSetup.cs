using System;
using System.Collections.Generic;
using Inventor;
using InventorAddin.Commands;
using InventorAddin.Holes;
using InventorAddin.Core.Holes;
using InventorAddin.Core.Logging;
using InventorAddin.Core.Ribbon;
using InventorAddin.Core.Settings;
using InventorAddin.Core.Theming;

namespace InventorAddin.UI
{
    /// <summary>
    /// Draws the add-in's ribbon tab in each environment exactly as <see cref="RibbonLayout"/> describes it.
    /// To add a button: add a layout entry in Core and register the command in <see cref="CreateCommands"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Settings and Inventor's theme (for the icons) are read once, when the add-in starts. A changed setting or theme
    /// takes effect at the next start, except "Use Inventor's Hole command", which <see cref="HoleCommandTakeover"/>
    /// applies when Settings is saved.
    /// </para>
    /// <para>
    /// The add-in's controls (<see cref="AddinUi"/>: its tab on every ribbon and AWB Hole's split button on Inventor's
    /// Modify panel and Sheet Metal tab, task 129)
    /// are deleted when the add-in unloads, and any an earlier load left are deleted before the ribbon is built, so a
    /// reload from the Add-In Manager builds the ribbon as a start does (task 121). The command definitions are kept, so
    /// users' Quick Access Toolbar entries and shortcuts on our commands survive; a reload reuses them.
    /// </para>
    /// </remarks>
    public sealed class RibbonSetup : IDisposable
    {
        private readonly Application _app;
        private readonly string _clientId;
        private readonly AddinSettings _settings;
        private readonly ILog _log;

        // ButtonDefinitions must be kept alive or their events stop firing.
        private readonly List<AddinCommand> _commands = new();

        // Registered commands by internal name, for looking up layout entries.
        private readonly Dictionary<string, AddinCommand> _commandsByName = new(StringComparer.Ordinal);

        // Commands that failed to register, so their layout entries are skipped with a warning that says why.
        private readonly HashSet<string> _failedCommands = new(StringComparer.Ordinal);

        // AWB Hole in place of Inventor's Hole (task 091); null when AWB Hole failed to register.
        private HoleCommandTakeover? _holeTakeover;

        // Edit Feature and double-click on a hole open AWB Hole (task 092); null when our Edit Feature failed to register.
        private HoleEditRedirect? _holeEdit;

        public RibbonSetup(Application app, string clientId, AddinSettings settings, ILog log)
        {
            _app = app;
            _clientId = clientId;
            _settings = settings;
            _log = log;
        }

        /// <summary>Every command the add-in provides. Each is registered once, whether or not the layout shows it.</summary>
        private static IEnumerable<AddinCommand> CreateCommands()
        {
            yield return new NewPartCommand();
            yield return new SaveAsCopyCommand();
            yield return new SaveAsReplaceCommand();
            yield return new PartPropertiesCommand();
            yield return new CreatePrintCommand();
            yield return new MassUpdateCommand();
            yield return new MaterialFinishCommand();
            yield return new AwbHoleCommand();
            yield return new HoleEditFeatureCommand();
            yield return new InventorHoleCommand();
            yield return new UseInventorHoleByDefaultCommand();
            yield return new SettingsCommand();
            yield return new AboutCommand();
            yield return new ExportModelDataCommand();
            yield return new ExportLibrariesCommand();
        }

        /// <summary>
        /// Registers every command, with its icons, and builds each environment's ribbon. Inventor's theme is read once,
        /// here, for the icons; switching theme while Inventor runs takes effect at the next start. A command that fails
        /// to register is logged and left out (its buttons are skipped), and a ribbon that fails to build is logged; the
        /// other commands and ribbons are still built.
        /// </summary>
        public void Create()
        {
            // A reload starts clean: what an earlier load left would hide the new buttons (task 121).
            RemoveUi(AddinUiOccasion.Loading);

            var report = new RibbonBuildReport();

            UiTheme theme = ThemeResources.ReadInventorTheme();
            _log.Info(
                $"Ribbon icons use the {UiThemes.ResourceSuffix(theme)} set, from Inventor's theme at start. " +
                "After switching Inventor's theme, restart Inventor to update them.");

            RibbonIconChoice icons = RibbonIcons.ForLayout(_settings.ShowDeveloperTools);
            foreach (IconNameConflict conflict in icons.Conflicts)
                _log.Warn(
                    $"Ribbon layout gives command {conflict.CommandInternalName} two icons, {conflict.KeptIconName} and " +
                    $"{conflict.IgnoredIconName}. A command has one definition, so {conflict.KeptIconName} is used.");

            foreach (AddinCommand command in CreateCommands())
                TryRegister(command, icons, theme);

            foreach (RibbonEnvironment environment in RibbonEnvironments.All)
            {
                IReadOnlyList<PanelLayout> panels = RibbonLayout.For(environment, _settings.ShowDeveloperTools);
                if (panels.Count == 0)
                    continue;

                try
                {
                    BuildRibbon(environment, panels, report);
                }
                catch (Exception ex)
                {
                    _log.Error($"Could not build the {environment.RibbonName()} ribbon. The other ribbons are still built.", ex);
                }
            }

            report.CommandsRegistered = _commands.Count;
            report.CommandsFailed = _failedCommands.Count;
            _log.Info(report.Summary());

            StartHoleTakeover();
            StartHoleEditRedirect(theme);
        }

        /// <summary>
        /// AWB Hole replaces Inventor's Hole on the 3D Model and Sheet Metal tabs, or not, as the settings say (tasks 091
        /// and 128), as a split button with Inventor Hole and "Use Inventor's Hole by default" (task 129; a plain button
        /// when either failed to register). Without AWB Hole's definition, Inventor's Hole is left as it is.
        /// </summary>
        private void StartHoleTakeover()
        {
            if (!_commandsByName.TryGetValue(CommandNames.AwbHole, out AddinCommand? awbHole) || awbHole.Definition == null)
            {
                _log.Warn($"Command {CommandNames.AwbHole} is not registered, so Inventor's Hole is not replaced on the 3D Model and Sheet Metal tabs.");
                return;
            }

            try
            {
                _holeTakeover = new HoleCommandTakeover(
                    _app, awbHole.Definition, DefinitionOf(CommandNames.InventorHole),
                    DefinitionOf(CommandNames.UseInventorHoleByDefault), _log);
                _holeTakeover.Start(_settings);
            }
            catch (Exception ex)
            {
                _log.Error("Hole takeover: starting failed; Inventor's Hole is left as it is.", ex);
            }
        }

        /// <summary>A registered command's definition, or null when it failed to register.</summary>
        private ButtonDefinition? DefinitionOf(string commandInternalName) =>
            _commandsByName.TryGetValue(commandInternalName, out AddinCommand? command) ? command.Definition : null;

        /// <summary>
        /// Edit Feature and double-click on a hole open AWB Hole while it has taken over Inventor's Hole (task 092), and so
        /// does the mini toolbar's Hole on a sketch point (task 133). Our Edit Feature has no ribbon button; it takes
        /// Inventor's Edit Feature icon, or AWB Hole's when that cannot be read.
        /// </summary>
        private void StartHoleEditRedirect(UiTheme theme)
        {
            if (!_commandsByName.TryGetValue(CommandNames.AwbHoleEdit, out AddinCommand? edit) || edit.Definition == null)
            {
                _log.Warn($"Command {CommandNames.AwbHoleEdit} is not registered, so editing a hole opens Inventor's Hole.");
                return;
            }

            // AWB Hole's own definition, with its icon, for the mini toolbar's Hole entry (task 133).
            ButtonDefinition? awbHole =
                _commandsByName.TryGetValue(CommandNames.AwbHole, out AddinCommand? hole) ? hole.Definition : null;

            ButtonPictures? fallback = LoadPictures(edit, IconNames.AwbHole, theme);
            try
            {
                _holeEdit = new HoleEditRedirect(_app, edit.Definition, awbHole, () => _holeTakeover?.Owner, _log);
                _holeEdit.Start(fallback);
            }
            catch (Exception ex)
            {
                _log.Error("Hole edit: starting failed; editing a hole opens Inventor's Hole.", ex);
                _holeEdit?.Dispose();
                _holeEdit = null;
            }
        }

        private void BuildRibbon(RibbonEnvironment environment, IReadOnlyList<PanelLayout> panels, RibbonBuildReport report)
        {
            Ribbon ribbon = _app.UserInterfaceManager.Ribbons[environment.RibbonName()];
            RibbonTab tab = GetOrAddTab(ribbon, out bool tabAdded);
            report.Tab(environment.RibbonName(), tabAdded);

            foreach (PanelLayout panelLayout in panels)
            {
                RibbonPanel panel = GetOrAddPanel(tab, panelLayout.Id, panelLayout.DisplayName, out bool panelAdded);
                report.Panel(panelAdded);

                foreach (ButtonLayout button in panelLayout.Buttons)
                {
                    if (!_commandsByName.TryGetValue(button.CommandInternalName, out AddinCommand? command))
                    {
                        if (_failedCommands.Contains(button.CommandInternalName))
                        {
                            _log.Warn(
                                $"Command {button.CommandInternalName} failed to register, so its button on panel " +
                                $"{panelLayout.Id} ({environment.RibbonName()}) is skipped.");
                            continue;
                        }

                        _log.Warn(
                            $"Ribbon layout lists command {button.CommandInternalName} on panel {panelLayout.Id} " +
                            $"({environment.RibbonName()}), but no such command is registered. The button is skipped.");
                        continue;
                    }

                    report.Button(AddButton(panel, command, button.Size == ButtonSize.Large));
                }
            }
        }

        /// <summary>
        /// The command's ribbon pictures, or null (text only) when the layout shows no button for it or the icon cannot
        /// be loaded. A load failure is logged as one warning naming the icon.
        /// </summary>
        private ButtonPictures? LoadPictures(AddinCommand command, string? iconName, UiTheme theme)
        {
            if (iconName == null)
                return null;

            try
            {
                return IconLoader.LoadButtonPictures(iconName, theme);
            }
            catch (Exception ex)
            {
                _log.Warn(
                    $"Icon {iconName} ({UiThemes.ResourceSuffix(theme)}) could not be loaded for command {command.InternalName}; " +
                    $"the button shows text only. {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Loads the command's icons and registers it. A failure is logged as an error naming the command, and the command
        /// is left out so that the other commands and the ribbons are still built.
        /// </summary>
        private void TryRegister(AddinCommand command, RibbonIconChoice icons, UiTheme theme)
        {
            try
            {
                // The split button's dropdown commands have no layout entry, so their icons are named in Core (task 129).
                string? iconName = icons.IconNameFor(command.InternalName)
                    ?? HoleTakeoverRules.DropdownIconName(command.InternalName);
                Register(command, LoadPictures(command, iconName, theme));
            }
            catch (Exception ex)
            {
                _log.Error($"Could not register command {command.InternalName}. It is left out of the ribbon.", ex);
                _failedCommands.Add(command.InternalName);

                // Register may have attached the event before failing; detach it so the command holds nothing.
                try
                {
                    command.Dispose();
                }
                catch (Exception disposeEx)
                {
                    _log.Warn($"Could not release command {command.InternalName} after it failed to register. {disposeEx.Message}");
                }
            }
        }

        private void Register(AddinCommand command, ButtonPictures? pictures)
        {
            if (_commandsByName.ContainsKey(command.InternalName))
            {
                _log.Warn($"Command {command.InternalName} is registered more than once. The duplicate is ignored.");
                return;
            }

            command.Register(_app, _clientId, pictures);
            _commands.Add(command);
            _commandsByName.Add(command.InternalName, command);
        }

        private RibbonTab GetOrAddTab(Ribbon ribbon, out bool added)
        {
            added = false;
            foreach (RibbonTab t in ribbon.RibbonTabs)
                if (t.InternalName == RibbonIds.TabId)
                    return t;
            added = true;
            return ribbon.RibbonTabs.Add(RibbonIds.TabName, RibbonIds.TabId, _clientId);
        }

        private RibbonPanel GetOrAddPanel(RibbonTab tab, string id, string name, out bool added)
        {
            added = false;
            foreach (RibbonPanel p in tab.RibbonPanels)
                if (p.InternalName == id)
                    return p;
            added = true;
            return tab.RibbonPanels.Add(name, id, _clientId);
        }

        /// <summary>Adds the command's button to the panel; false when a button for it was already there.</summary>
        private static bool AddButton(RibbonPanel panel, AddinCommand command, bool useLargeIcon)
        {
            foreach (CommandControl c in panel.CommandControls)
                if (c.InternalName == command.Definition!.InternalName)
                    return false;
            panel.CommandControls.AddButton(command.Definition, useLargeIcon);
            return true;
        }

        /// <summary>
        /// Deletes everything <see cref="AddinUi"/> lists: our buttons on Inventor's panels, then our tab on every ribbon
        /// (with its panels and buttons). The command definitions are kept. Each item is deleted on its own, so one
        /// failure does not stop the others. What was removed is logged as one line, and each failure as a warning.
        /// Never throws.
        /// </summary>
        private void RemoveUi(AddinUiOccasion occasion)
        {
            var report = new AddinUiRemovalReport(occasion);
            try
            {
                RemoveForeignButtons(report);
                RemoveTabs(report);

                _log.Info(report.Summary());
                foreach (string message in report.NotFoundMessages())
                    _log.Info(message);
                foreach (string message in report.FailureMessages())
                    _log.Warn(message);
            }
            catch (Exception ex)
            {
                _log.Error($"Ribbon: removing the add-in's tabs and buttons failed ({occasion}).", ex);
            }
        }

        private void RemoveForeignButtons(AddinUiRemovalReport report)
        {
            foreach (ForeignButton button in AddinUi.ForeignButtons)
            {
                string where = button.PanelId == null
                    ? $"{button.CommandInternalName} on tab {button.TabId} of the {button.RibbonName} ribbon"
                    : $"{button.CommandInternalName} on panel {button.PanelId} of the {button.RibbonName} ribbon";

                var found = new List<CommandControl>();
                try
                {
                    // Found by enumerating, so a tab id that is wrong (the Sheet Metal tab's is not confirmed, task 128)
                    // is reported as not found rather than as a failure.
                    RibbonTab? tab = null;
                    foreach (RibbonTab t in _app.UserInterfaceManager.Ribbons[button.RibbonName].RibbonTabs)
                        if (ComSafe.Get(() => t.InternalName) == button.TabId)
                        {
                            tab = t;
                            break;
                        }

                    if (tab == null)
                    {
                        report.TabNotFound(button.RibbonName, button.TabId);
                        continue;
                    }

                    // A null panel id means every panel on the tab: that panel is looked up at run time (task 128).
                    var panels = new List<RibbonPanel>();
                    if (button.PanelId == null)
                    {
                        foreach (RibbonPanel p in tab.RibbonPanels)
                            panels.Add(p);
                    }
                    else
                    {
                        panels.Add(tab.RibbonPanels[button.PanelId]);
                    }

                    // Collected first: deleting while enumerating a COM collection can skip items. A split button (task
                    // 129) is found by its name, its top command or a dropdown entry.
                    foreach (RibbonPanel panel in panels)
                        foreach (CommandControl c in panel.CommandControls)
                            if (button.Matches(ComSafe.Get(() => c.InternalName),
                                    ComSafe.Get(() => c.ControlDefinition.InternalName),
                                    HoleCommandTakeover.ChildCommands(c)))
                                found.Add(c);
                }
                catch (Exception ex)
                {
                    report.Failed($"{where} (the panel could not be read)", ex.Message);
                }

                foreach (CommandControl control in found)
                {
                    try
                    {
                        control.Delete();
                        report.ButtonRemoved();
                    }
                    catch (Exception ex)
                    {
                        report.Failed(where, ex.Message);
                    }
                }
            }
        }

        private void RemoveTabs(AddinUiRemovalReport report)
        {
            foreach (string ribbonName in AddinUi.TabRibbonNames)
            {
                string where = $"the {RibbonIds.TabName} tab on the {ribbonName} ribbon";

                var found = new List<RibbonTab>();
                try
                {
                    foreach (RibbonTab t in _app.UserInterfaceManager.Ribbons[ribbonName].RibbonTabs)
                        if (ComSafe.Get(() => t.InternalName) == RibbonIds.TabId)
                            found.Add(t);
                }
                catch (Exception ex)
                {
                    report.Failed($"{where} (the ribbon's tabs could not be read)", ex.Message);
                }

                foreach (RibbonTab tab in found)
                {
                    try
                    {
                        tab.Delete();
                        report.TabRemoved();
                    }
                    catch (Exception ex)
                    {
                        report.Failed(where, ex.Message);
                    }
                }
            }
        }

        public void Dispose()
        {
            // Editing goes back to Inventor, and Inventor's Hole and H are given back, before the commands are released.
            _holeEdit?.Dispose();
            _holeEdit = null;
            _holeTakeover?.Dispose();
            _holeTakeover = null;

            // Each command on its own, so one failing release cannot skip the others or the removal below.
            foreach (var c in _commands)
            {
                try
                {
                    c.Dispose();
                }
                catch (Exception ex)
                {
                    _log.Warn($"Could not release command {c.InternalName} while unloading. {ex.Message}");
                }
            }
            _commands.Clear();
            _commandsByName.Clear();
            _failedCommands.Clear();

            // Then our tabs and our buttons on Inventor's panels are deleted, so a reload starts clean (task 121). The
            // command definitions are kept (see AddinUi).
            RemoveUi(AddinUiOccasion.Unloading);
        }
    }
}
