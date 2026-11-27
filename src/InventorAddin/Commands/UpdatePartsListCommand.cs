using System;
using System.Collections.Generic;
using Inventor;
using InventorAddin.Core.Drawing;
using InventorAddin.Core.Models;
using InventorAddin.PrintViews;
using Forms = System.Windows.Forms;
using SysEnvironment = System.Environment;

namespace InventorAddin.Commands
{
    /// <summary>
    /// Update Parts List (spec 05, "Added for M5: New tools", item 6; task 208): fills the Make/Buy cells of every parts list on the
    /// active sheet. AWB-numbered parts are Make; Content Center parts and files in the project's library folders are Buy; anything else
    /// is left blank. A parts list with no Make/Buy column gets one. The rule is Core's (<see cref="MakeBuy"/>); the reading and writing
    /// is <see cref="PartsListMakeBuyWriter"/>'s. The whole update is one transaction, so one Undo reverts it.
    /// </summary>
    /// <remarks>Writes table cells and may add a column, which are not geometry, so its command type is <c>kNonShapeEditCmdType</c>.</remarks>
    public sealed class UpdatePartsListCommand : DocumentEditCommand
    {
        public override string InternalName => Core.Ribbon.CommandNames.UpdatePartsList;
        public override string DisplayName => "Update Parts List";
        public override string Description =>
            "Fill the Make/Buy column of the parts lists on this sheet: AWB parts are Make, library and Content Center parts are Buy.";

        protected override CommandTypesEnum CommandType => CommandTypesEnum.kNonShapeEditCmdType;

        protected override bool CanRunOn(DocumentKind kind) => kind == DocumentKind.Drawing;

        protected override string WrongDocumentMessage(DocumentKind kind) => PartsListMakeBuy.WrongDocument;

        private readonly List<string> _warnings = new List<string>();
        private Sheet? _sheet;
        private IReadOnlyList<string> _libraryFolders = Array.Empty<string>();
        private PartsListMakeBuyResult? _result;

        protected override bool GatherInput(Document doc)
        {
            _warnings.Clear();
            _result = null;
            _sheet = ComSafe.Get(() => ((DrawingDocument)doc).ActiveSheet);
            int count = _sheet == null ? 0 : ComSafe.Get(() => (int?)_sheet.PartsLists.Count) ?? 0;
            if (_sheet == null || count == 0)
            {
                Forms.MessageBox.Show(PartsListMakeBuy.NoPartsList, DisplayName, Forms.MessageBoxButtons.OK, Forms.MessageBoxIcon.Information);
                return false;
            }

            // Read once per command.
            _libraryFolders = BatchPrintTreeReader.LibraryFolders(InventorHost.App);
            return true;
        }

        protected override void Execute(Document doc)
        {
            Sheet? sheet = _sheet;
            if (sheet == null)
                return;

            _result = PartsListMakeBuyWriter.Update(sheet, _libraryFolders, _warnings);
            ComSafe.Run(() => ((DrawingDocument)doc).Update(), _warnings, "drawing update");
        }

        protected override void Run(Document doc)
        {
            base.Run(doc);

            PartsListMakeBuyResult? r = _result;
            if (r == null)
                return;

            foreach (string warning in _warnings)
                AddinServices.Log.Warn($"Update Parts List: {warning}");
            string summary = PartsListMakeBuy.Summary(r.PartsLists, r.ColumnsAdded, r.Make, r.Buy, r.Blank, r.Cleared, r.FailedLists, r.FailedCells);
            AddinServices.Log.Info($"Update Parts List: {summary}");

            bool problems = r.FailedLists + r.FailedCells > 0 || _warnings.Count > 0;
            string text = summary;
            if (_warnings.Count > 0)
                text += SysEnvironment.NewLine + SysEnvironment.NewLine + string.Join(SysEnvironment.NewLine, _warnings);
            Forms.MessageBox.Show(
                text, DisplayName, Forms.MessageBoxButtons.OK, problems ? Forms.MessageBoxIcon.Warning : Forms.MessageBoxIcon.Information);
        }
    }
}
