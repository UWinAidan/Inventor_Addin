using System;
using InventorAddin.Core;
using InventorAddin.Core.Logging;
using InventorAddin.Core.Numbering;
using InventorAddin.Core.Settings;
using IOPath = System.IO.Path;
using SysEnvironment = System.Environment;

namespace InventorAddin
{
    /// <summary>
    /// Session-wide services: the data folder, the log, the settings loaded at startup and the part number register.
    /// Initialised in <see cref="StandardAddInServer.Activate"/> and reset in <see cref="StandardAddInServer.Deactivate"/>.
    /// Before initialisation (or if it failed) <see cref="Log"/> is <see cref="NullLog.Instance"/> and
    /// <see cref="Settings"/> holds defaults, so callers never need a null check.
    /// </summary>
    public static class AddinServices
    {
        /// <summary>The data folder, or null before initialisation or when it could not be determined.</summary>
        public static string? DataFolder { get; private set; }

        public static ILog Log { get; private set; } = NullLog.Instance;

        /// <summary>Full path of the current log file, or null when no log file is being written.</summary>
        public static string? LogFilePath { get; private set; }

        public static AddinSettings Settings { get; private set; } = new AddinSettings();

        /// <summary>This PC's register files go in this folder under the data folder (spec 02, "The register").</summary>
        private const string LocalRegisterFolderName = "register";

        private static RegisterStore? _registerStore;

        /// <summary>
        /// The part number register (spec 02), built on first use from <see cref="AddinSettings.RegisterFolder"/>
        /// (blank: no shared folder, always offline), the <c>register</c> folder under the data folder, and
        /// <c>AssignedBy</c> from <see cref="AddinSettings.DefaultDesigner"/> or the Windows user name. Rebuilt
        /// after the settings change. Building it touches no file.
        /// </summary>
        /// <exception cref="InvalidOperationException">The user's application data folder could not be found.</exception>
        public static RegisterStore RegisterStore
        {
            get
            {
                if (_registerStore == null)
                {
                    string localFolder = IOPath.Combine(DataFolder ?? GetDataFolder(), LocalRegisterFolderName);
                    var environment = new RegisterEnvironment(
                        RegisterEnvironment.AssignedByFor(Settings.DefaultDesigner, SysEnvironment.UserName));
                    _registerStore = new RegisterStore(Settings.RegisterFolder, localFolder, environment, Log);
                }

                return _registerStore;
            }
        }

        /// <summary>
        /// Creates the log, then loads settings and logs each settings warning.
        /// Never throws: on failure it keeps whatever log was created (or <see cref="NullLog"/>) and default settings.
        /// </summary>
        internal static void Initialize()
        {
            Reset();

            try
            {
                string folder = GetDataFolder();

                var log = new FileLog(folder);
                DataFolder = folder;
                Log = log;
                LogFilePath = log.FilePath;

                SettingsLoadResult result = new SettingsStore(folder).Load();
                Settings = result.Settings;
                foreach (string warning in result.Warnings)
                    Log.Warn(warning);
            }
            catch (Exception ex)
            {
                // Log is still NullLog if the failure came before the FileLog existed.
                Log.Error("Could not start add-in services. Default settings are used.", ex);
                Settings = new AddinSettings();
            }
        }

        /// <summary>
        /// Replaces the session's settings after the user saved new ones. The ribbon is not rebuilt;
        /// a changed ribbon setting takes effect at the next start. The register is rebuilt on next use, so a changed
        /// register folder or Default designer applies straight away.
        /// </summary>
        internal static void UpdateSettings(AddinSettings settings)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _registerStore = null;
        }

        internal static void Shutdown() => Reset();

        /// <summary>%APPDATA%\<see cref="Branding.DataFolderName"/>.</summary>
        private static string GetDataFolder()
        {
            string appData = SysEnvironment.GetFolderPath(SysEnvironment.SpecialFolder.ApplicationData);
            if (string.IsNullOrEmpty(appData))
                throw new InvalidOperationException("The user's application data folder could not be found.");
            return IOPath.Combine(appData, Branding.DataFolderName);
        }

        private static void Reset()
        {
            DataFolder = null;
            Log = NullLog.Instance;
            LogFilePath = null;
            Settings = new AddinSettings();
            _registerStore = null;
        }
    }
}
