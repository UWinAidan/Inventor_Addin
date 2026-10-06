using System;
using System.Collections.Generic;
using Inventor;
using InventorAddin.Commands;
using InventorAddin.Core.Logging;
using InventorAddin.Core.Ribbon;
using InventorAddin.Core.Settings;

namespace InventorAddin.UI
{
    /// <summary>
    /// Draws the add-in's ribbon tab in each environment exactly as <see cref="RibbonLayout"/> describes it.
    /// To add a button: add a layout entry in Core and register the command in <see cref="CreateCommands"/>.
    /// </summary>
    /// <remarks>
    /// Settings are read once, when the add-in starts. A changed setting takes effect at the next start.
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
            yield return new SettingsCommand();
            yield return new ExportModelDataCommand();
            yield return new ExportLibrariesCommand();
        }

        public void Create()
        {
            foreach (AddinCommand command in CreateCommands())
                Register(command);

            foreach (RibbonEnvironment environment in RibbonEnvironments.All)
            {
                IReadOnlyList<PanelLayout> panels = RibbonLayout.For(environment, _settings.ShowDeveloperTools);
                if (panels.Count == 0)
                    continue;

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
        }

        private void Register(AddinCommand command)
        {
            if (_commandsByName.ContainsKey(command.InternalName))
            {
                _log.Warn($"Command {command.InternalName} is registered more than once. The duplicate is ignored.");
                return;
            }

            command.Register(_app, _clientId);
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
