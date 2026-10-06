using System;
using Inventor;
using InventorAddin.Core.Models;

namespace InventorAddin.Commands
{
    /// <summary>
    /// Base class for a command that changes a document. By default the whole command runs inside one
    /// Inventor transaction, so a single undo reverts it, and a failure aborts the transaction so the
    /// document is left as it was.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every transaction is named after <see cref="AddinCommand.DisplayName"/>; that name is what appears
    /// in Inventor's Undo list.
    /// </para>
    /// <para>
    /// The command runs in this order: the null and document-kind checks, then <see cref="Run"/>. The
    /// default <see cref="Run"/> calls <see cref="GatherInput"/> (outside any transaction, so a dialog can
    /// open there) and then <see cref="Execute(Document)"/> inside one transaction. A command that only
    /// overrides <see cref="Execute(Document)"/> gets exactly that.
    /// </para>
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

        /// <summary>
        /// Collects what the command needs before anything is changed, for example by showing a dialog.
        /// Runs after the document-kind check and before any transaction starts. Returning <c>false</c>
        /// ends the command with no transaction and no message (the user cancelled). Returns <c>true</c>
        /// by default.
        /// </summary>
        protected virtual bool GatherInput(Document doc) => true;

        /// <summary>
        /// Does the work on <paramref name="doc"/>. With the default <see cref="Run"/> it runs inside the
        /// command's single transaction.
        /// </summary>
        protected abstract void Execute(Document doc);

        /// <summary>
        /// Runs the command on <paramref name="doc"/> once the null and document-kind checks have passed.
        /// The default calls <see cref="GatherInput"/> and, if it returns <c>true</c>, runs
        /// <see cref="Execute(Document)"/> inside one transaction through <see cref="RunInTransaction"/>.
        /// </summary>
        /// <remarks>
        /// An interactive command (a window with Apply and OK, say) overrides this, shows its window, and
        /// calls <see cref="RunInTransaction"/> once per apply. Each call is one Undo step, so the window
        /// itself stays outside any transaction. A command that overrides <see cref="Run"/> and never calls
        /// <see cref="Execute(Document)"/> still has to implement it, because it is abstract; give it a
        /// body that throws <see cref="NotSupportedException"/>.
        /// </remarks>
        protected virtual void Run(Document doc)
        {
            if (GatherInput(doc))
                RunInTransaction(doc, () => Execute(doc));
        }

        /// <summary>
        /// Runs <paramref name="edit"/> inside one transaction on <paramref name="doc"/>, named after
        /// <see cref="AddinCommand.DisplayName"/>. Each call is one Undo step. If <paramref name="edit"/>
        /// throws, the transaction is aborted, so the document is left as it was before this call, and the
        /// exception is rethrown.
        /// </summary>
        /// <remarks>This is the only place in the class that starts a transaction.</remarks>
        protected void RunInTransaction(Document doc, Action edit)
        {
            // The transaction's display name is what appears in Inventor's Undo list.
            Transaction transaction = InventorHost.App.TransactionManager.StartTransaction((_Document)doc, DisplayName);
            try
            {
                edit();
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

            Run(doc);
        }

        private void ShowInfo(string text) =>
            System.Windows.Forms.MessageBox.Show(text, DisplayName,
                System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Information);
    }
}
