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
        private readonly Func<Document, IReadOnlyList<PropertyWrite>>? _extraWrites;

        /// <param name="doc">The new or copied document.</param>
        /// <param name="commandName">The command's display name: the transaction's name in Undo, and the log prefix.</param>
        /// <param name="extraWrites">
        /// Writes to add, in the same transaction, to whatever <see cref="WriteProperties"/> is given, decided from the
        /// open document (Mass Update's Finish Spec, Revision and Status backfill, tasks 217 and 219); null for none.
        /// </param>
        public InventorNewDocument(Document doc, string commandName, Func<Document, IReadOnlyList<PropertyWrite>>? extraWrites = null)
        {
            _doc = doc;
            _commandName = commandName;
            _extraWrites = extraWrites;
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
                IReadOnlyList<PropertyWrite>? extra = _extraWrites?.Invoke(_doc);
                if (extra != null && extra.Count > 0)
                {
                    // A write already in the list (Mass Update sets Part Number and so on) is never one the backfill adds.
                    var all = new List<PropertyWrite>(writes);
                    all.AddRange(extra);
                    writes = all;
                    AddinServices.Log.Info(
                        $"{_commandName}: filled in {string.Join(", ", System.Linq.Enumerable.Select(extra, w => w.Location.PropertyName))} on '{ComSafe.Get(() => _doc.DisplayName)}'.");
                }

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
