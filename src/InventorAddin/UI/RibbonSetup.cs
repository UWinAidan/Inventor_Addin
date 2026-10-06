using System;
using System.Collections.Generic;
using Inventor;
using InventorAddin.Commands;

namespace InventorAddin.UI
{
    /// <summary>
    /// Builds the add-in's ribbon tab in each environment.
    /// Placeholder layout until the UI mockups in docs/ui-mockups are implemented.
    /// </summary>
    public sealed class RibbonSetup : IDisposable
    {
        private const string TabId = "id_Tab_WorkflowTools";
        private const string TabName = "Workflow Tools";
        private const string DevPanelId = "id_Panel_WorkflowTools_Dev";

        // Ribbons the tab appears on
        private static readonly string[] Ribbons = { "ZeroDoc", "Part", "Assembly", "Drawing" };

        private readonly Application _app;
        private readonly string _clientId;

        // ButtonDefinitions must be kept alive or their events stop firing.
        private readonly List<AddinCommand> _commands = new();

        public RibbonSetup(Application app, string clientId)
        {
            _app = app;
            _clientId = clientId;
        }

        public void Create()
        {
            var exportModel = Register(new ExportModelDataCommand());
            var exportLibraries = Register(new ExportLibrariesCommand());

            foreach (string ribbonName in Ribbons)
            {
                Ribbon ribbon = _app.UserInterfaceManager.Ribbons[ribbonName];
                RibbonTab tab = GetOrAddTab(ribbon);
                RibbonPanel dev = GetOrAddPanel(tab, DevPanelId, "Developer");

                if (ribbonName != "ZeroDoc")
                    AddButton(dev, exportModel);
                AddButton(dev, exportLibraries);
            }
        }

        private AddinCommand Register(AddinCommand command)
        {
            command.Register(_app, _clientId);
            _commands.Add(command);
            return command;
        }

        private RibbonTab GetOrAddTab(Ribbon ribbon)
        {
            foreach (RibbonTab t in ribbon.RibbonTabs)
                if (t.InternalName == TabId)
                    return t;
            return ribbon.RibbonTabs.Add(TabName, TabId, _clientId);
        }

        private RibbonPanel GetOrAddPanel(RibbonTab tab, string id, string name)
        {
            foreach (RibbonPanel p in tab.RibbonPanels)
                if (p.InternalName == id)
                    return p;
            return tab.RibbonPanels.Add(name, id, _clientId);
        }

        private static void AddButton(RibbonPanel panel, AddinCommand command)
        {
            foreach (CommandControl c in panel.CommandControls)
                if (c.InternalName == command.Definition!.InternalName)
                    return;
            panel.CommandControls.AddButton(command.Definition, true);
        }

        public void Dispose()
        {
            foreach (var c in _commands)
                c.Dispose();
            _commands.Clear();
        }
    }
}
