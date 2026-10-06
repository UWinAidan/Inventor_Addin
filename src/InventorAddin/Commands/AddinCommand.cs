using System;
using Inventor;
using InventorAddin.Core.Logging;

namespace InventorAddin.Commands
{
    /// <summary>Base class for a ribbon button. Subclasses supply the metadata and <see cref="Execute"/>.</summary>
    public abstract class AddinCommand : IDisposable
    {
        public abstract string InternalName { get; }
        public abstract string DisplayName { get; }
        public virtual string Description => DisplayName;
        public virtual string Tooltip => Description;

        /// <summary>
        /// What the command changes, passed to Inventor when the button is defined.
        /// Query-only by default; commands that edit documents override this
        /// (see <see cref="DocumentEditCommand"/>).
        /// </summary>
        protected virtual CommandTypesEnum CommandType => CommandTypesEnum.kQueryOnlyCmdType;

        public ButtonDefinition? Definition { get; private set; }

        public void Register(Application app, string clientId)
        {
            ControlDefinitions defs = app.CommandManager.ControlDefinitions;

            // Re-use an existing definition if the add-in was reloaded in the same session
            Definition = ComSafe.Get(() => (ButtonDefinition)defs[InternalName])
                ?? defs.AddButtonDefinition(
                    DisplayName, InternalName, CommandType, clientId,
                    Description, Tooltip, Type.Missing, Type.Missing, ButtonDisplayEnum.kAlwaysDisplayText);

            Definition.OnExecute += OnExecute;
        }

        private void OnExecute(NameValueMap context)
        {
            var log = AddinServices.Log;
            log.Info($"Command {InternalName} ({DisplayName}) started.");

            try
            {
                Execute();
            }
            catch (Exception ex)
            {
                log.Error($"Command {InternalName} failed.", ex);
                string text = InventorAddin.Core.Logging.ErrorMessages.CommandFailed(DisplayName, ex, AddinServices.LogFilePath);
                System.Windows.Forms.MessageBox.Show(text, DisplayName,
                    System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
            }
        }

        protected abstract void Execute();

        public void Dispose()
        {
            if (Definition != null)
                Definition.OnExecute -= OnExecute;
            Definition = null;
        }
    }
}
