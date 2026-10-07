using System;
using Inventor;
using InventorAddin.Core.Logging;
using InventorAddin.UI;

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

        /// <summary>
        /// Defines the command's button in Inventor, with <paramref name="pictures"/> as its 16 px and 32 px icons,
        /// or text only when <paramref name="pictures"/> is null. If Inventor refuses the icons, the button is
        /// defined text only and a warning is logged.
        /// </summary>
        public void Register(Application app, string clientId, ButtonPictures? pictures)
        {
            ControlDefinitions defs = app.CommandManager.ControlDefinitions;

            // Re-use an existing definition if the add-in was reloaded in the same session
            ButtonDefinition? existing = ComSafe.Get(() => (ButtonDefinition)defs[InternalName]);
            if (existing != null)
            {
                Definition = existing;
                if (pictures != null)
                    SetIcons(existing, pictures);
            }
            else
            {
                Definition = AddDefinition(defs, clientId, pictures);
            }

            Definition.OnExecute += OnExecute;
        }

        private ButtonDefinition AddDefinition(ControlDefinitions defs, string clientId, ButtonPictures? pictures)
        {
            if (pictures != null)
            {
                try
                {
                    return defs.AddButtonDefinition(
                        DisplayName, InternalName, CommandType, clientId,
                        Description, Tooltip, pictures.Standard, pictures.Large, ButtonDisplayEnum.kAlwaysDisplayText);
                }
                catch (Exception ex)
                {
                    AddinServices.Log.Warn(
                        $"Icon {pictures.IconName} was refused for command {InternalName}; the button shows text only. {ex.Message}");
                }
            }

            return defs.AddButtonDefinition(
                DisplayName, InternalName, CommandType, clientId,
                Description, Tooltip, Type.Missing, Type.Missing, ButtonDisplayEnum.kAlwaysDisplayText);
        }

        /// <summary>
        /// Sets the icons on a definition Inventor already had (the add-in was reloaded), through the setters
        /// spike 020 confirmed. A failure leaves the definition's current icons and is logged.
        /// </summary>
        private void SetIcons(ButtonDefinition definition, ButtonPictures pictures)
        {
            try
            {
                definition.StandardIcon = (stdole.IPictureDisp)pictures.Standard;
                definition.LargeIcon = (stdole.IPictureDisp)pictures.Large;
            }
            catch (Exception ex)
            {
                AddinServices.Log.Warn(
                    $"Icon {pictures.IconName} could not be set on the existing definition of command {InternalName}; " +
                    $"the button keeps its current icons. {ex.Message}");
            }
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
