using System;
using Inventor;
using InventorAddin.Core.Holes;
using InventorAddin.Core.Logging;
using InventorAddin.Core.Ribbon;
using InventorAddin.Core.Settings;
using InventorAddin.Holes;
using InventorAddin.UI;
using Forms = System.Windows.Forms;

namespace InventorAddin.Commands
{
    /// <summary>
    /// "Inventor Hole" in AWB Hole's split button on the 3D Model and Sheet Metal tabs (spec 09, "The command"; task
    /// 129): runs Inventor's Hole once and leaves the setting as it is, so the next Hole or H opens AWB Hole again.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Inventor's Hole is run after this command's event has returned (<c>Dispatcher.BeginInvoke</c>), so an open AWB
    /// Hole session has ended (pressing another command cancels it) before Inventor's Hole starts, and Inventor's command
    /// is not started from inside ours. The mini toolbar backstop is told first
    /// (<see cref="HoleEditRedirect.InventorHoleAsked"/>; task 133), so it lets this start through. Nothing else stops
    /// <c>PartHoleCmd</c>: task 092 stops only Inventor's hole edit.
    /// </para>
    /// <para>
    /// The command itself changes nothing, so it is query-only; Inventor's Hole runs as Inventor's own command. Its icon
    /// is Inventor's Hole icon, copied by <see cref="HoleCommandTakeover"/>, else AWB Hole's.
    /// </para>
    /// </remarks>
    public sealed class InventorHoleCommand : AddinCommand
    {
        public override string InternalName => CommandNames.InventorHole;
        public override string DisplayName => HoleTakeoverRules.InventorHoleText;
        public override string Description => HoleTakeoverRules.InventorHoleDescription;

        protected override void Execute()
        {
            ILog log = AddinServices.Log;
            log.Info(HoleTakeoverRules.InventorHoleRunMessage);

            System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    HoleEditRedirect.Active?.InventorHoleAsked();
                    InventorHost.App.CommandManager.ControlDefinitions[HoleTakeoverRules.InventorHoleRunCommand].Execute();
                }
                catch (Exception ex)
                {
                    log.Error($"Command {InternalName}: running Inventor's Hole failed.", ex);
                    string text = ErrorMessages.CommandFailed(DisplayName, ex, AddinServices.LogFilePath);
                    Forms.MessageBox.Show(text, DisplayName, Forms.MessageBoxButtons.OK, Forms.MessageBoxIcon.Error);
                }
            }));
        }
    }

    /// <summary>
    /// "Use Inventor's Hole by default" in AWB Hole's split button (task 129): the same setting as "Use Inventor's Hole
    /// command" in Settings. Pressing it flips the setting, saves the settings file as Settings' Save does, and applies it
    /// at once (<see cref="HoleCommandTakeover.ApplyFromSettings"/>), which also sets the toggle's pressed state. The
    /// Settings window reads the session's settings, so it shows the same value next time it opens.
    /// </summary>
    /// <remarks>
    /// Turning it on hides the split button, as the setting does from Settings; it is then turned off in Settings. A save
    /// that fails changes nothing and says why. Query-only: it changes no document, only the settings file.
    /// </remarks>
    public sealed class UseInventorHoleByDefaultCommand : AddinCommand
    {
        public override string InternalName => CommandNames.UseInventorHoleByDefault;
        public override string DisplayName => HoleTakeoverRules.UseInventorHoleText;
        public override string Description => HoleTakeoverRules.UseInventorHoleDescription;

        protected override void Execute()
        {
            ILog log = AddinServices.Log;
            AddinSettings current = AddinServices.Settings;
            AddinSettings updated = HoleTakeoverRules.Toggled(current);

            string? folder = AddinServices.DataFolder;
            if (folder == null)
            {
                log.Warn($"Command {InternalName} not run: the add-in has no data folder, so settings cannot be saved.");
                Refuse(current, HoleTakeoverRules.ToggleNoDataFolderText);
                return;
            }

            var store = new SettingsStore(folder);
            try
            {
                store.Save(updated);
            }
            catch (Exception ex)
            {
                log.Error($"Could not save settings to '{store.FilePath}'.", ex);
                Refuse(current, HoleTakeoverRules.ToggleSaveFailedText(store.FilePath, ex.Message));
                return;
            }

            log.Info(HoleTakeoverRules.ToggledMessage(updated, store.FilePath));
            AddinServices.UpdateSettings(updated);
            HoleCommandTakeover.Active?.ApplyFromSettings(updated, HoleTakeoverRules.ToggledReason);
        }

        /// <summary>Nothing changed: the toggle shows the setting as it is, and the user is told why.</summary>
        private void Refuse(AddinSettings current, string text)
        {
            HoleCommandTakeover.Active?.ShowToggleState(current);
            Forms.MessageBox.Show(text, DisplayName, Forms.MessageBoxButtons.OK, Forms.MessageBoxIcon.Warning);
        }
    }
}
