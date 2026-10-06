using InventorAddin.Core.Ribbon;
using InventorAddin.Core.Settings;
using InventorAddin.Core.ViewModels;
using InventorAddin.UI;

namespace InventorAddin.Commands
{
    /// <summary>Opens the Settings window. Query-only: it changes no document, only the settings file.</summary>
    public sealed class SettingsCommand : AddinCommand
    {
        public override string InternalName => CommandNames.Settings;
        public override string DisplayName => "Settings";
        public override string Description => "Edit Workflow Tools settings.";

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
}
