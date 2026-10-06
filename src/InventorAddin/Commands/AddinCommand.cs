using System;
using Inventor;

namespace InventorAddin.Commands
{
    /// <summary>Base class for a ribbon button. Subclasses supply the metadata and <see cref="Execute"/>.</summary>
    public abstract class AddinCommand : IDisposable
    {
        public abstract string InternalName { get; }
        public abstract string DisplayName { get; }
        public virtual string Description => DisplayName;
        public virtual string Tooltip => Description;

        public ButtonDefinition? Definition { get; private set; }

        public void Register(Application app, string clientId)
        {
            ControlDefinitions defs = app.CommandManager.ControlDefinitions;

            // Re-use an existing definition if the add-in was reloaded in the same session
            Definition = ComSafe.Get(() => (ButtonDefinition)defs[InternalName])
                ?? defs.AddButtonDefinition(
                    DisplayName, InternalName, CommandTypesEnum.kQueryOnlyCmdType, clientId,
                    Description, Tooltip, Type.Missing, Type.Missing, ButtonDisplayEnum.kAlwaysDisplayText);

            Definition.OnExecute += OnExecute;
        }

        private void OnExecute(NameValueMap context)
        {
            try
            {
                Execute();
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show(ex.ToString(), DisplayName,
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
