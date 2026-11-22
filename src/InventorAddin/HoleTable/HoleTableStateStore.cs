using System;
using System.Collections.Generic;
using Inventor;
using InventorAddin.Core.HoleTable;
using InventorAddin.Core.Logging;

namespace InventorAddin.HoleTables
{
    /// <summary>What <see cref="HoleTableStateStore.Read"/> found: the state, or null with the reason a stored text could not be read (null when nothing is stored).</summary>
    internal sealed record HoleTableStoredState(HoleTableState? State, string? Reason);

    /// <summary>
    /// Keeps the wizard's state (<see cref="HoleTableState"/>, as JSON) in two places (task 166): an attribute set on each
    /// placed table symbol of the view, and a record on the sheet with one attribute per view. The symbols' copy goes
    /// with the tables; the sheet's copy survives them being deleted, so Edit Table still lists the view and keeps its
    /// notes and edits. A view's symbols are found by their definition name, which holds the sheet and view keys. The
    /// caller holds the transaction when writing.
    /// </summary>
    internal sealed class HoleTableStateStore
    {
        private const string LogPrefix = "Hole table: ";
        private static readonly HoleTableKind[] Kinds = { HoleTableKind.Precision, HoleTableKind.Clearance };

        private readonly ILog _log;

        public HoleTableStateStore(ILog log)
        {
            _log = log ?? NullLog.Instance;
        }

        /// <summary>
        /// The state stored for the view: the table symbols first, then the sheet's record. When a stored text exists but
        /// none can be read, the state is null and the reason is set (also logged).
        /// </summary>
        public HoleTableStoredState Read(Sheet sheet, string sheetKey, string viewKey)
        {
            string? reason = null;
            foreach (SketchedSymbol symbol in Symbols(sheet, sheetKey, viewKey))
            {
                string? text = ComSafe.Get(() => ReadText(symbol.AttributeSets, HoleTableNames.StateAttributeSetName, HoleTableNames.StateAttributeName));
                if (Parse(text, ref reason) is { } state)
                    return new HoleTableStoredState(state, null);
            }

            string? record = ComSafe.Get(() =>
                ReadText(sheet.AttributeSets, HoleTableNames.SheetRecordAttributeSetName, HoleTableNames.SheetRecordAttributeName(viewKey)));
            if (Parse(record, ref reason) is { } fromSheet)
                return new HoleTableStoredState(fromSheet, null);

            if (reason != null)
                _log.Warn(LogPrefix + reason);
            return new HoleTableStoredState(null, reason);
        }

        /// <summary>
        /// Writes the state to every placed table symbol of the view and to the sheet's record (also when there are no
        /// symbols); returns how many symbols it reached.
        /// </summary>
        public int Write(Sheet sheet, string sheetKey, string viewKey, HoleTableState state)
        {
            string text = HoleTableStateJson.Write(state);
            if (text.Length > HoleTableStateJson.WarnLength)
                _log.Warn(LogPrefix + $"the stored state is {text.Length} characters; Inventor keeps an attribute string in about 32 KB.");
            else
                _log.Info(LogPrefix + $"stored state is {text.Length} characters.");

            int written = 0;
            foreach (SketchedSymbol symbol in Symbols(sheet, sheetKey, viewKey))
            {
                AttributeSets sets = symbol.AttributeSets;
                if (sets.NameIsUsed[HoleTableNames.StateAttributeSetName])
                    sets[HoleTableNames.StateAttributeSetName].Delete();
                // Spike 160 C10: on a document object the parent-removed flag must be false.
                AttributeSet set = sets.Add(HoleTableNames.StateAttributeSetName, false);
                set.Add(HoleTableNames.StateAttributeName, ValueTypeEnum.kStringType, text);
                written++;
            }

            WriteSheetRecord(sheet, viewKey, text);
            return written;
        }

        private static void WriteSheetRecord(Sheet sheet, string viewKey, string text)
        {
            AttributeSets sets = sheet.AttributeSets;
            AttributeSet set = sets.NameIsUsed[HoleTableNames.SheetRecordAttributeSetName]
                ? sets[HoleTableNames.SheetRecordAttributeSetName]
                : sets.Add(HoleTableNames.SheetRecordAttributeSetName, false);
            string name = HoleTableNames.SheetRecordAttributeName(viewKey);
            if (set.NameIsUsed[name])
                set[name].Delete();
            set.Add(name, ValueTypeEnum.kStringType, text);
        }

        private static string? ReadText(AttributeSets sets, string setName, string attributeName)
        {
            if (!sets.NameIsUsed[setName])
                return null;
            AttributeSet set = sets[setName];
            return set.NameIsUsed[attributeName] ? set[attributeName].Value as string : null;
        }

        private static HoleTableState? Parse(string? text, ref string? reason)
        {
            if (string.IsNullOrEmpty(text))
                return null;
            HoleTableStateReadResult result = HoleTableStateJson.Read(text);
            if (result.State == null)
                reason ??= result.Reason;
            return result.State;
        }

        // The view's placed table symbols: those whose definition is one of its tables or a part of one (task 220).
        private static List<SketchedSymbol> Symbols(Sheet sheet, string sheetKey, string viewKey)
        {
            var found = new List<SketchedSymbol>();
            foreach (SketchedSymbol symbol in sheet.SketchedSymbols)
            {
                string? name = ComSafe.Get(() => symbol.Definition.Name);
                if (HoleTableNames.IsViewTable(name, sheetKey, viewKey))
                    found.Add(symbol);
            }

            return found;
        }
    }
}
