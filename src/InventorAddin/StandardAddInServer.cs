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

            _ribbon = new RibbonSetup(InventorHost.App, ClientId);
            _ribbon.Create();
        }

        public void Deactivate()
        {
            _ribbon?.Dispose();
            _ribbon = null;
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
