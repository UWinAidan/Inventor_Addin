using System;
using System.Collections.Generic;
using Inventor;
using InventorAddin.Commands;
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
    /// Settings and Inventor's theme (for the icons) are read once, when the add-in starts. A changed setting or theme
    /// takes effect at the next start.
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
            yield return new PartPropertiesCommand();
            yield return new SettingsCommand();
            yield return new AboutCommand();
            yield return new ExportModelDataCommand();
            yield return new ExportLibrariesCommand();
        }

        /// <summary>
        /// Registers every command, with its icons, and builds each environment's ribbon. Inventor's theme is read once,
        /// here, for the icons; switching theme while Inventor runs takes effect at the next start. A ribbon that fails
        /// to build is logged and the others are still built.
        /// </summary>
        public void Create()
        {
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
                Register(command, LoadPictures(command, icons.IconNameFor(command.InternalName), theme));

            foreach (RibbonEnvironment environment in RibbonEnvironments.All)
            {
                IReadOnlyList<PanelLayout> panels = RibbonLayout.For(environment, _settings.ShowDeveloperTools);
                if (panels.Count == 0)
                    continue;

                try
                {
                    BuildRibbon(environment, panels);
                }
                catch (Exception ex)
                {
                    _log.Error($"Could not build the {environment.RibbonName()} ribbon. The other ribbons are still built.", ex);
                }
            }
        }

        private void BuildRibbon(RibbonEnvironment environment, IReadOnlyList<PanelLayout> panels)
        {
            Ribbon ribbon = _app.UserInterfaceManager.Ribbons[environment.RibbonName()];
            RibbonTab tab = GetOrAddTab(ribbon);

            foreach (PanelLayout panelLayout in panels)
            {
                RibbonPanel panel = GetOrAddPanel(tab, panelLayout.Id, panelLayout.DisplayName);

                foreach (ButtonLayout button in panelLayout.Buttons)
                {
                    if (!_commandsByName.TryGetValue(button.CommandInternalName, out AddinCommand? command))
                    {
                        _log.Warn(
                            $"Ribbon layout lists command {button.CommandInternalName} on panel {panelLayout.Id} " +
                            $"({environment.RibbonName()}), but no such command is registered. The button is skipped.");
                        continue;
                    }

                    AddButton(panel, command, button.Size == ButtonSize.Large);
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

        private RibbonTab GetOrAddTab(Ribbon ribbon)
        {
            foreach (RibbonTab t in ribbon.RibbonTabs)
                if (t.InternalName == RibbonIds.TabId)
                    return t;
            return ribbon.RibbonTabs.Add(RibbonIds.TabName, RibbonIds.TabId, _clientId);
        }

        private RibbonPanel GetOrAddPanel(RibbonTab tab, string id, string name)
        {
            foreach (RibbonPanel p in tab.RibbonPanels)
                if (p.InternalName == id)
                    return p;
            return tab.RibbonPanels.Add(name, id, _clientId);
        }

        private static void AddButton(RibbonPanel panel, AddinCommand command, bool useLargeIcon)
        {
            foreach (CommandControl c in panel.CommandControls)
                if (c.InternalName == command.Definition!.InternalName)
                    return;
            panel.CommandControls.AddButton(command.Definition, useLargeIcon);
        }

        public void Dispose()
        {
            foreach (var c in _commands)
                c.Dispose();
            _commands.Clear();
            _commandsByName.Clear();
        }
    }
}
