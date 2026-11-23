using Inventor;
using InventorAddin.Core.Drawing;
using InventorAddin.Core.Models;
using InventorAddin.Extraction;
using Forms = System.Windows.Forms;
using IOFile = System.IO.File;

namespace InventorAddin.Commands
{
    /// <summary>
    /// Open Drawing (spec 05, "Decided for M5: Open Drawing"; task 193): opens the print of the part or assembly being edited, found by
    /// <see cref="PrintPlan.FindExistingPrint"/> (<c>Prints\</c> first, then beside the model), or makes it active when it is already open.
    /// When the model has no print it asks <see cref="PrintPlan.NoPrintQuestion"/>; Yes runs Create Print (the same code as its button),
    /// No does nothing.
    /// </summary>
    /// <remarks>
    /// Not a <see cref="DocumentEditCommand"/>: opening a drawing changes no document, so, like Create Print, its command type is
    /// <c>kFileOperationsCmdType</c>.
    /// </remarks>
    public sealed class OpenDrawingCommand : AddinCommand
    {
        public override string InternalName => Core.Ribbon.CommandNames.OpenDrawing;
        public override string DisplayName => "Open Drawing";
        public override string Description => "Open the drawing of this part or assembly from its folder's Prints folder.";

        protected override CommandTypesEnum CommandType => CommandTypesEnum.kFileOperationsCmdType;

        protected override void Execute()
        {
            Document? model = CommandTargetDocument();
            if (model == null)
            {
                Show("Open a part or assembly first.", Forms.MessageBoxIcon.Information);
                return;
            }

            string? problem = CreatePrintCommand.ModelProblem(
                model, PrintPlan.OpenWrongDocumentMessage, PrintPlan.OpenUnsavedMessage, out _, out string modelPath);
            if (problem != null)
            {
                Show(problem, Forms.MessageBoxIcon.Information);
                return;
            }

            string? existing = PrintPlan.FindExistingPrint(modelPath, PrintPlan.NewPrintExtension, IOFile.Exists);
            if (existing != null)
            {
                AddinServices.Log.Info($"Open Drawing: opening '{existing}'.");
                CreatePrintCommand.OpenPrint(existing);
                return;
            }

            AddinServices.Log.Info($"Open Drawing: no print for '{modelPath}'; offering Create Print.");
            Forms.DialogResult answer = Forms.MessageBox.Show(
                PrintPlan.NoPrintQuestion, DisplayName, Forms.MessageBoxButtons.YesNo, Forms.MessageBoxIcon.Question);
            if (answer != Forms.DialogResult.Yes)
                return;

            // Create Print re-checks the model and the template and shows its own messages.
            new CreatePrintCommand().RunFromOpenDrawing();
        }

        private void Show(string text, Forms.MessageBoxIcon icon) =>
            Forms.MessageBox.Show(text, DisplayName, Forms.MessageBoxButtons.OK, icon);
    }
}
