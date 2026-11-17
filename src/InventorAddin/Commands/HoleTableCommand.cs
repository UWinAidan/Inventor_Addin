using System;
using Inventor;
using InventorAddin.Core.HoleTable;
using InventorAddin.Core.Models;
using InventorAddin.Core.Ribbon;
using InventorAddin.Core.ViewModels;
using InventorAddin.HoleTables;
using InventorAddin.UI;
using Forms = System.Windows.Forms;

namespace InventorAddin.Commands
{
    /// <summary>
    /// Hole Table (spec 04, "Target dialog"; task 167): opens the Hole Table Wizard window on the active drawing. The
    /// window's behaviour is <see cref="HoleTableWizardViewModel"/> (Core); the drawing is read and written through
    /// <see cref="HoleTableSource"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An <see cref="AddinCommand"/>, not a <see cref="DocumentEditCommand"/>: the window stays open across several
    /// table commands, so the source starts one transaction for each of them (task 166 and later) and this command
    /// starts none. It adds annotation to a drawing, not geometry, so its command type is <c>kNonShapeEditCmdType</c>.
    /// The window is modal, so the drawing cannot change under it.
    /// </para>
    /// <para>
    /// Until tasks 164 to 166 land, the table buttons report "Not yet available." through the source.
    /// </para>
    /// </remarks>
    public sealed class HoleTableCommand : AddinCommand
    {
        /// <summary>Shown when the active document is not a drawing.</summary>
        public const string WrongDocumentText = "Open a drawing first. The Hole Table works on a drawing.";

        public override string InternalName => CommandNames.HoleTable;
        public override string DisplayName => HoleTableWindow.TitleText;
        public override string Description =>
            "Tag the holes in a drawing view and make Precision and Clearance hole tables.";
        public override string Tooltip =>
            "Tag the holes in a drawing view and make a table of the precision holes and a table of the clearance holes.";

        protected override CommandTypesEnum CommandType => CommandTypesEnum.kNonShapeEditCmdType;

        protected override void Execute()
        {
            Document? doc = CommandTargetDocument();
            DocumentKind kind = doc == null ? DocumentKind.Unknown : DocumentKinds.Of(doc);
            if (doc == null || kind != DocumentKind.Drawing)
            {
                AddinServices.Log.Info($"Command {InternalName} not run: document kind {kind} is not accepted.");
                Forms.MessageBox.Show(WrongDocumentText, DisplayName,
                    Forms.MessageBoxButtons.OK, Forms.MessageBoxIcon.Information);
                return;
            }

            var source = new HoleTableSource((DrawingDocument)doc, AddinServices.Settings, AddinServices.Log);
            var viewModel = new HoleTableWizardViewModel(source);
            WindowHost.ShowDialog(new HoleTableWindow(viewModel), IconNames.HoleTable, stateKey: "HoleTable");
        }
    }
}
