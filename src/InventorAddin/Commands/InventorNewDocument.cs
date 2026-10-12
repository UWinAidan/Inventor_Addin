using System;
using System.Collections.Generic;
using Inventor;
using InventorAddin.Core.Numbering;
using InventorAddin.Core.Numbering.MassUpdate;
using InventorAddin.Core.PartProperties;
using InventorAddin.Extraction;

namespace InventorAddin.Commands
{
    /// <summary>
    /// A document a command has just made (New Part), opened as a copy (Save As Copy) or opened invisibly to change it
    /// (Mass Update), by the calls spike 032 confirmed: properties written with <see cref="PropertyWriter"/> inside one
    /// transaction, saved, or closed without saving.
    /// </summary>
    internal sealed class InventorNewDocument : INewPartDocument, ICopiedDocument, IMassUpdateDocument
    {
        private readonly Document _doc;
        private readonly string _commandName;

        /// <param name="doc">The new or copied document.</param>
        /// <param name="commandName">The command's display name: the transaction's name in Undo, and the log prefix.</param>
        public InventorNewDocument(Document doc, string commandName)
        {
            _doc = doc;
            _commandName = commandName;
        }

        /// <summary>
        /// Writes inside one transaction, so one Undo reverts them. Spike 032: a new document needs none, and one
        /// does no harm.
        /// </summary>
        public void WriteProperties(IReadOnlyList<PropertyWrite> writes)
        {
            Transaction transaction = InventorHost.App.TransactionManager.StartTransaction((_Document)_doc, _commandName);
            try
            {
                PropertyWriter.Apply(_doc, writes);
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
                    AddinServices.Log.Error($"{_commandName}: aborting the property transaction failed.", abortEx);
                }

                throw;
            }
        }

        public void SaveAs(string fullPath) => _doc.SaveAs(fullPath, false);

        public void Save() => _doc.Save();

        // The same read as Part Properties (PropertyReader.ReadCommon): Design Tracking's "Part Number".
        public string? ReadPartNumber() =>
            PropertyReader.GetString(
                _doc, PartPropertyStorage.PartNumber.SetName, PartPropertyStorage.PartNumber.PropertyName);

        // Close(true) skips saving (spike 032).
        public void CloseWithoutSaving() => _doc.Close(true);
    }
}
