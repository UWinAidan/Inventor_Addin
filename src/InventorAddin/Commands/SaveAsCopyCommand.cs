using Inventor;
using InventorAddin.Core.Models;
using InventorAddin.Core.Numbering;
using InventorAddin.Core.PartProperties;
using InventorAddin.Core.Ribbon;
using InventorAddin.Core.Settings;
using InventorAddin.Core.ViewModels;
using InventorAddin.Extraction;
using InventorAddin.UI;
using Forms = System.Windows.Forms;

namespace InventorAddin.Commands
{
    /// <summary>
    /// Save As Copy (spec 02, "Commands: Save As Copy"): copies the active part or assembly, with its unsaved edits, into
    /// its own folder under the next part number and a new name, writes the creation properties on the copy, saves it
    /// and opens it. The original is not changed. The steps, their order and the messages are in
    /// <see cref="SaveAsCopyCreator"/> (Core); this class reads the original, shows the dialog and supplies Inventor.
    /// </summary>
    /// <remarks>
    /// Not a <see cref="DocumentEditCommand"/>: the original is never edited, so there is nothing to wrap in a
    /// transaction on it. Like New Part, the command creates and saves a new file, so its command type is
    /// <c>kFileOperationsCmdType</c>. The copy's property writes run in a transaction of their own on the copy.
    /// </remarks>
    public sealed class SaveAsCopyCommand : AddinCommand
    {
        public override string InternalName => CommandNames.SaveAsCopy;
        public override string DisplayName => "Save As Copy";
        public override string Description => "Copy this part or assembly under the next part number and a new name, and open the copy.";

        protected override CommandTypesEnum CommandType => CommandTypesEnum.kFileOperationsCmdType;

        protected override void Execute()
        {
            // The document open in the window, as Create Print uses: the part or assembly being copied.
            Document? original = InventorHost.ActiveDocument;
            if (original == null)
            {
                Show("Open a part or assembly first.", Forms.MessageBoxIcon.Information);
                return;
            }

            DocumentKind kind = DocumentKinds.Of(original);
            if (!SaveAsCopyCreator.CanCopy(kind))
            {
                Show(SaveAsCopyCreator.WrongDocumentMessage, Forms.MessageBoxIcon.Information);
                return;
            }

            string originalPath = ComSafe.Get(() => original.FullFileName) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(originalPath))
            {
                Show(SaveAsCopyCreator.UnsavedMessage, Forms.MessageBoxIcon.Information);
                return;
            }

            string? partType = PropertyReader.GetString(
                original, PartPropertyStorage.PartType.SetName, PartPropertyStorage.PartType.PropertyName);

            AddinSettings settings = AddinServices.Settings;
            var viewModel = new SaveAsCopyViewModel(originalPath, kind, partType, settings.DefaultDesigner);
            WindowHost.ShowDialog(new SaveAsCopyWindow(viewModel), IconNames.SaveAsCopy);

            SaveAsCopyRequest? request = viewModel.Request;
            if (request == null)
                return;

            var creator = new SaveAsCopyCreator(
                AddinServices.RegisterStore, new InventorCopySource(original, DisplayName), viewModel.Designer, AddinServices.Log);

            NewPartResult result;

            // Saving and opening a large assembly can take a while, and the register may wait on the NAS.
            Forms.Cursor previousCursor = Forms.Cursor.Current;
            Forms.Cursor.Current = Forms.Cursors.WaitCursor;
            try
            {
                result = creator.Copy(request);
            }
            catch (NewPartException ex)
            {
                // A failure after the number was assigned is already logged with its exception by SaveAsCopyCreator.
                if (ex.AssignedNumber == null)
                    AddinServices.Log.Warn($"Save As Copy stopped: {ex.Message}");

                Forms.Cursor.Current = previousCursor;
                Show(ex.Message, ex.AssignedNumber == null ? Forms.MessageBoxIcon.Warning : Forms.MessageBoxIcon.Error);
                return;
            }
            finally
            {
                Forms.Cursor.Current = previousCursor;
            }

            string? notice = NewPartCreator.Notice(result);
            if (notice != null)
                Show(notice, result.DuplicateMessages.Count > 0 ? Forms.MessageBoxIcon.Warning : Forms.MessageBoxIcon.Information);
        }

        private void Show(string text, Forms.MessageBoxIcon icon) =>
            Forms.MessageBox.Show(text, DisplayName, Forms.MessageBoxButtons.OK, icon);

        /// <summary>
        /// The open original. Task 045, from spike 032: <c>SaveAs(path, false)</c> renames the open document, so the copy
        /// is saved with <c>SaveAs(path, true)</c>, which keeps the original's name, and then opened.
        /// </summary>
        private sealed class InventorCopySource : ISaveAsCopySource
        {
            private readonly Document _original;
            private readonly string _commandName;

            public InventorCopySource(Document original, string commandName)
            {
                _original = original;
                _commandName = commandName;
            }

            // SaveCopyAs = true: writes the file, with the original's unsaved edits; the open document keeps its name.
            public void SaveCopyAs(string fullPath) => _original.SaveAs(fullPath, true);

            public ICopiedDocument OpenCopy(string fullPath)
            {
                // Visible, so the copy opens in its own window; Activate makes it the active document.
                Document copy = InventorHost.App.Documents.Open(fullPath, true);
                ComSafe.Run(() => copy.Activate());
                return new InventorNewDocument(copy, _commandName);
            }
        }
    }
}
