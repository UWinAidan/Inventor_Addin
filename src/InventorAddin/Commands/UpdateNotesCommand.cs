using System;
using System.Collections.Generic;
using Inventor;
using InventorAddin.Core.Drawing;
using InventorAddin.Core.Models;
using InventorAddin.Extraction;
using InventorAddin.PrintViews;
using Forms = System.Windows.Forms;
using SysEnvironment = System.Environment;

namespace InventorAddin.Commands
{
    /// <summary>
    /// Update Notes (spec 05, "Added for M5: New tools", item 1; task 197): writes or refreshes AWB's notes on the drawing's first sheet from
    /// the model's material, finish and Finish Note. The model is the first part or assembly the drawing references. The rules come from the
    /// notes workbook (Core, task 196) and the writing is <see cref="NotesWriter"/>'s; Create Print runs the same writer once. The whole
    /// update is one transaction, so one Undo reverts it.
    /// </summary>
    /// <remarks>
    /// Adds and deletes drawing notes, which are not geometry, so its command type is <c>kNonShapeEditCmdType</c>.
    /// </remarks>
    public sealed class UpdateNotesCommand : DocumentEditCommand
    {
        public override string InternalName => Core.Ribbon.CommandNames.UpdateNotes;
        public override string DisplayName => "Update Notes";
        public override string Description =>
            "Write or refresh the notes on the first sheet from the model's material and finish. Notes you typed are left alone.";

        protected override CommandTypesEnum CommandType => CommandTypesEnum.kNonShapeEditCmdType;

        protected override bool CanRunOn(DocumentKind kind) => kind == DocumentKind.Drawing;

        protected override string WrongDocumentMessage(DocumentKind kind) => NotesMessages.WrongDocument;

        private Document? _model;
        private NotesWriteResult? _result;

        protected override bool GatherInput(Document doc)
        {
            _model = null;
            _result = null;

            _model = PrintUnitRefresher.FindModel((DrawingDocument)doc);
            if (_model == null)
            {
                Forms.MessageBox.Show(NotesMessages.NoModel, DisplayName, Forms.MessageBoxButtons.OK, Forms.MessageBoxIcon.Information);
                return false;
            }

            return true;
        }

        protected override void Run(Document doc)
        {
            base.Run(doc);

            NotesWriteResult? result = _result;
            if (result == null)
                return;

            // Done is only said when the notes were replaced; otherwise the message is the problems alone.
            string text = result.Replaced ? NotesMessages.Done(result.ListCount, result.BottomCount) : string.Empty;
            string problemsText = result.ProblemsText();
            if (text.Length > 0 && problemsText.Length > 0)
                text += SysEnvironment.NewLine + SysEnvironment.NewLine;
            text += problemsText;
            bool problems = result.HasProblems;
            Forms.MessageBox.Show(
                text, DisplayName, Forms.MessageBoxButtons.OK, problems ? Forms.MessageBoxIcon.Warning : Forms.MessageBoxIcon.Information);
        }

        protected override void Execute(Document doc)
        {
            if (_model == null)
                return;

            var drawing = (DrawingDocument)doc;
            _result = NotesWriter.Update(drawing, _model);
            ComSafe.Run(() => drawing.Update());
        }
    }
}
