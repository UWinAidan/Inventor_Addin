using System;
using Inventor;
using InventorAddin.Core.Models;

namespace InventorAddin.Commands
{
    /// <summary>
    /// Base class for a command that changes a document. The whole command runs inside one Inventor
    /// transaction, so a single undo reverts it, and a failure aborts the transaction so the document
    /// is left as it was.
    /// </summary>
    /// <remarks>
    /// The transaction is named after <see cref="AddinCommand.DisplayName"/>; that name is what appears
    /// in Inventor's Undo list.
    /// </remarks>
    public abstract class DocumentEditCommand : AddinCommand
    {
        /// <summary>
        /// What this command changes. Abstract here so each editing command has to choose
        /// (for example kShapeEditCmdType, kNonShapeEditCmdType or kFilePropertyEditCmdType).
        /// </summary>
        protected abstract override CommandTypesEnum CommandType { get; }

        /// <summary>
        /// Whether the command accepts a document of this kind. Accepts every kind by default;
        /// a subclass narrows this to the kinds its own rule allows.
        /// </summary>
        protected virtual bool CanRunOn(DocumentKind kind) => true;

        /// <summary>Short message shown when the active document is a kind <see cref="CanRunOn"/> rejects.</summary>
        protected virtual string WrongDocumentMessage(DocumentKind kind) =>
            $"{DisplayName} cannot run on this document ({kind}).";

        /// <summary>Does the work on <paramref name="doc"/>. Runs inside the command's transaction.</summary>
        protected abstract void Execute(Document doc);

        protected sealed override void Execute()
        {
            Document? doc = InventorHost.ActiveEditDocument;
            if (doc == null)
            {
                ShowInfo("Open a document first.");
                return;
            }

            DocumentKind kind = DocumentKinds.Of(doc);
            if (!CanRunOn(kind))
            {
                AddinServices.Log.Info($"Command {InternalName} not run: document kind {kind} is not accepted.");
                ShowInfo(WrongDocumentMessage(kind));
                return;
            }

            // The transaction's display name is what appears in Inventor's Undo list.
            Transaction transaction = InventorHost.App.TransactionManager.StartTransaction((_Document)doc, DisplayName);
            try
            {
                Execute(doc);
                transaction.End();
            }
            catch (Exception)
            {
                try
                {
                    transaction.Abort();
                }
                catch (Exception abortEx)
                {
                    // Log only: the original exception is the one the user needs to see.
                    AddinServices.Log.Error($"Command {InternalName}: aborting the transaction failed.", abortEx);
                }

                // AddinCommand logs and shows the original exception.
                throw;
            }
        }

        private void ShowInfo(string text) =>
            System.Windows.Forms.MessageBox.Show(text, DisplayName,
                System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Information);
    }
}
