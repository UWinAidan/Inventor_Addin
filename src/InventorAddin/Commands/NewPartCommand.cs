using System;
using Inventor;
using InventorAddin.Core.Numbering;
using InventorAddin.Core.Ribbon;
using InventorAddin.Core.Settings;
using InventorAddin.Core.ViewModels;
using InventorAddin.UI;
using Forms = System.Windows.Forms;

namespace InventorAddin.Commands
{
    /// <summary>
    /// New Part (spec 02, "Commands: New Part"): asks for the kind, part type, name, units and folder, then creates the
    /// file from Inventor's default template with the next part number, writes the creation properties and saves it
    /// under its name. The steps, their order and the messages are in <see cref="NewPartCreator"/> (Core); this class
    /// shows the dialog and supplies Inventor.
    /// </summary>
    /// <remarks>
    /// Not a <see cref="DocumentEditCommand"/>: it can run with no document open (it is on the ZeroDoc ribbon), and it
    /// changes no document that was open before it ran. What it does is create and save a new file, so its command type
    /// is <c>kFileOperationsCmdType</c> rather than one of the edit types, and it is not query-only either.
    /// </remarks>
    public sealed class NewPartCommand : AddinCommand
    {
        public override string InternalName => CommandNames.NewPart;
        public override string DisplayName => "New Part";
        public override string Description => "Create a part, sheet metal part or assembly with the next part number.";

        protected override CommandTypesEnum CommandType => CommandTypesEnum.kFileOperationsCmdType;

        protected override void Execute()
        {
            AddinSettings settings = AddinServices.Settings;

            // The active document's folder when it has been saved, else the Work folder; never the project workspace,
            // which spike 032 found blank.
            string? activeFile = ComSafe.Get(() => InventorHost.ActiveDocument?.FullFileName);
            string folder = NewPartCreator.DefaultFolder(activeFile, settings.WorkFolder);

            var viewModel = new NewPartViewModel(settings.DefaultDesigner, settings.DefaultUnits, folder);
            WindowHost.ShowDialog(new NewPartWindow(viewModel), IconNames.NewPart);

            NewPartRequest? request = viewModel.Request;
            if (request == null)
                return;

            var creator = new NewPartCreator(
                AddinServices.RegisterStore, new InventorNewPartDocuments(DisplayName), viewModel.Designer, AddinServices.Log);

            NewPartResult result;

            // Documents.Add can take several seconds the first time (spike 032), and the register may wait on the NAS.
            Forms.Cursor previousCursor = Forms.Cursor.Current;
            Forms.Cursor.Current = Forms.Cursors.WaitCursor;
            try
            {
                result = creator.Create(request);
            }
            catch (NewPartException ex)
            {
                // A failure after the number was assigned is already logged with its exception by NewPartCreator.
                if (ex.AssignedNumber == null)
                    AddinServices.Log.Warn($"New Part stopped: {ex.Message}");

                Forms.Cursor.Current = previousCursor;
                Forms.MessageBox.Show(ex.Message, DisplayName, Forms.MessageBoxButtons.OK,
                    ex.AssignedNumber == null ? Forms.MessageBoxIcon.Warning : Forms.MessageBoxIcon.Error);
                return;
            }
            finally
            {
                Forms.Cursor.Current = previousCursor;
            }

            AddinServices.Log.Info($"New Part: {result.PartNumber} created and saved as '{result.FullPath}'.");

            string? notice = NewPartCreator.Notice(result);
            if (notice != null)
            {
                Forms.MessageBox.Show(notice, DisplayName, Forms.MessageBoxButtons.OK,
                    result.DuplicateMessages.Count > 0 ? Forms.MessageBoxIcon.Warning : Forms.MessageBoxIcon.Information);
            }
        }

        /// <summary>Finds templates and creates documents in the running Inventor, by the calls spike 032 confirmed.</summary>
        private sealed class InventorNewPartDocuments : INewPartDocuments
        {
            private readonly string _transactionName;

            public InventorNewPartDocuments(string transactionName)
            {
                _transactionName = transactionName;
            }

            public string? FindTemplate(NewFileKind kind, UnitsSystem units) => Templates.DefaultFor(kind, units);

            public INewPartDocument Create(NewFileKind kind, string templatePath)
            {
                // Visible, so the new document opens and becomes the active one.
                Document doc = InventorHost.App.Documents.Add(Templates.DocumentTypeFor(kind), templatePath, true);
                return new InventorNewDocument(doc, _transactionName);
            }
        }
    }
}
