using System;
using Inventor;

namespace InventorAddin
{
    /// <summary>Process-wide access to the running Inventor Application.</summary>
    public static class InventorHost
    {
        private static Application? _app;

        public static Application App =>
            _app ?? throw new InvalidOperationException("Add-in has not been activated by Inventor.");

        public static bool IsAvailable => _app != null;

        /// <summary>Inventor major version, e.g. 30 for Inventor 2026.</summary>
        public static int MajorVersion => App.SoftwareVersion.Major;

        public static Document? ActiveDocument => App.ActiveDocument;

        /// <summary>The document being edited in place (e.g. a part edited inside an assembly), otherwise the active document.</summary>
        public static Document? ActiveEditDocument => App.ActiveEditDocument;

        internal static void Initialize(Application app) => _app = app;

        internal static void Shutdown() => _app = null;
    }
}
