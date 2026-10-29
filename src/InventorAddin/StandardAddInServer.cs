using System;
using System.Runtime.InteropServices;
using Inventor;
using InventorAddin.Commands;
using InventorAddin.Core;
using InventorAddin.UI;

namespace InventorAddin
{
    /// <summary>Entry point Inventor calls when loading/unloading the add-in.</summary>
    [Guid(AddInGuid)]
    [ComVisible(true)]
    public class StandardAddInServer : ApplicationAddInServer
    {
        // Must match ClassId/ClientId in InventorAddin.addin
        public const string AddInGuid = "09DB1FA1-9732-4FA2-9975-44316FEEA4CE";
        public const string ClientId = "{" + AddInGuid + "}";

        private RibbonSetup? _ribbon;
        private StartupTab? _startupTab;

        public void Activate(ApplicationAddInSite addInSiteObject, bool firstTime)
        {
            InventorHost.Initialize(addInSiteObject.Application);

            // Never throws; falls back to NullLog and default settings.
            AddinServices.Initialize();

            var version = typeof(StandardAddInServer).Assembly.GetName().Version;
            int? inventorMajor = ComSafe.Get(() => (int?)InventorHost.MajorVersion);
            AddinServices.Log.Info(
                $"{Branding.ProductName} {version?.ToString() ?? "unknown"} started in Inventor major version {inventorMajor?.ToString() ?? "unknown"}.");

            // A reload from the Add-In Manager passes false; the ribbon is built the same either way (task 121).
            AddinServices.Log.Info($"Inventor activated the add-in with firstTime = {firstTime}.");

            _ribbon = new RibbonSetup(InventorHost.App, ClientId, AddinServices.Settings, AddinServices.Log);
            _ribbon.Create();

            _startupTab = new StartupTab(InventorHost.App, AddinServices.Log);
            _startupTab.Start();
        }

        public void Deactivate()
        {
            // An open AWB Hole session is cancelled, so the part is left as it was (task 089).
            AwbHoleWindowLauncher.Shutdown();

            _startupTab?.Dispose();
            _startupTab = null;

            _ribbon?.Dispose();
            _ribbon = null;

            AddinServices.Log.Info($"{Branding.ProductName} stopped.");
            AddinServices.Shutdown();
            InventorHost.Shutdown();

            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        public void ExecuteCommand(int commandID)
        {
            // Obsolete in the Inventor API; commands go through ButtonDefinition.OnExecute.
        }

        public object? Automation => null;
    }
}
