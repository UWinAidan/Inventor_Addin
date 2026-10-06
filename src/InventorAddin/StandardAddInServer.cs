using System;
using System.Runtime.InteropServices;
using Inventor;
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

        public void Activate(ApplicationAddInSite addInSiteObject, bool firstTime)
        {
            InventorHost.Initialize(addInSiteObject.Application);

            // Never throws; falls back to NullLog and default settings.
            AddinServices.Initialize();

            var version = typeof(StandardAddInServer).Assembly.GetName().Version;
            int? inventorMajor = ComSafe.Get(() => (int?)InventorHost.MajorVersion);
            AddinServices.Log.Info(
                $"Workflow Tools {version?.ToString() ?? "unknown"} started in Inventor major version {inventorMajor?.ToString() ?? "unknown"}.");

            _ribbon = new RibbonSetup(InventorHost.App, ClientId);
            _ribbon.Create();
        }

        public void Deactivate()
        {
            _ribbon?.Dispose();
            _ribbon = null;

            AddinServices.Log.Info("Workflow Tools stopped.");
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
