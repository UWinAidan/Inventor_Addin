using System;
using System.Linq;
using System.Reflection;
using InventorAddin.Core;
using InventorAddin.Core.Ribbon;
using InventorAddin.Core.Settings;
using InventorAddin.Core.ViewModels;
using InventorAddin.UI;
using IOPath = System.IO.Path;

namespace InventorAddin.Commands
{
    /// <summary>Opens the Settings window. Query-only: it changes no document, only the settings file.</summary>
    public sealed class SettingsCommand : AddinCommand
    {
        public override string InternalName => CommandNames.Settings;
        public override string DisplayName => "Settings";
        public override string Description => $"Edit {Branding.ProductName} settings.";

        protected override void Execute()
        {
            string? folder = AddinServices.DataFolder;
            if (folder == null)
            {
                System.Windows.Forms.MessageBox.Show(
                    "Settings cannot be edited because the add-in could not set up its data folder when Inventor started.",
                    DisplayName,
                    System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Information);
                return;
            }

            var viewModel = new SettingsViewModel(AddinServices.Settings, new SettingsStore(folder), AddinServices.Log);
            WindowHost.ShowDialog(new SettingsWindow(viewModel));

            // Set only by a successful save; null after Cancel or closing the window.
            AddinSettings? saved = viewModel.SavedSettings;
            if (saved != null)
                AddinServices.UpdateSettings(saved);
        }
    }

    /// <summary>Shows the add-in version and build date, so it is clear which build Inventor loaded. Query-only.</summary>
    public sealed class AboutCommand : AddinCommand
    {
        public override string InternalName => CommandNames.About;
        public override string DisplayName => "About";
        public override string Description => $"Show the {Branding.ProductName} version and build date.";

        protected override void Execute()
        {
            Assembly assembly = typeof(AboutCommand).Assembly;

            string? version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            string? buildDate = assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
                .FirstOrDefault(a => string.Equals(a.Key, "BuildDate", StringComparison.Ordinal))
                ?.Value;
            int? inventorMajor = ComSafe.Get(() => (int?)InventorHost.MajorVersion);

            string? logFilePath = AddinServices.LogFilePath;
            string? logFolder = logFilePath == null ? null : IOPath.GetDirectoryName(logFilePath);

            var viewModel = new AboutViewModel(version, buildDate, inventorMajor, logFolder);
            WindowHost.ShowDialog(new AboutWindow(viewModel));
        }
    }
}
