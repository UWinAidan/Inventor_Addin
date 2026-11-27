using System;
using System.Collections.Generic;
using Inventor;
using InventorAddin.Core.Drawing;
using InventorAddin.Core.Logging;
using InventorAddin.Core.Materials;
using InventorAddin.Core.Models;
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
    /// Stock Size (spec 05, "Added for M5: New tools", item 5; task 206): reads the part's oriented minimum range box, works out the
    /// raw stock size from the stock workbook (Core, task 205), shows it in the Stock Size window where it can be edited, and writes it to
    /// the custom "Material Size" property that the title block shows. The window stays outside the transaction; the one property write
    /// is one transaction, so one Undo removes it. A sheet metal part is declined (<see cref="StockSizeEligibility"/>): its Material Size is
    /// the flat pattern's (task 230).
    /// </summary>
    /// <remarks>Writes a document property, not geometry, so its command type is <c>kFilePropertyEditCmdType</c>, as Part Properties.</remarks>
    public sealed class StockSizeCommand : DocumentEditCommand
    {
        private const string LogPrefix = "Stock Size: ";

        public override string InternalName => CommandNames.StockSize;
        public override string DisplayName => "Stock Size";
        public override string Description =>
            "Work out the raw stock size of this part from its box and the stock workbook, and write it to Material Size.";

        protected override CommandTypesEnum CommandType => CommandTypesEnum.kFilePropertyEditCmdType;

        protected override bool CanRunOn(DocumentKind kind) => kind.IsPart();

        protected override string WrongDocumentMessage(DocumentKind kind) => StockSizeEligibility.WrongDocument;

        private PropertyWrite? _write;

        protected override bool GatherInput(Document doc)
        {
            _write = null;

            string? decline = SheetMetalDecline(doc);
            if (decline != null)
            {
                AddinServices.Log.Info(LogPrefix + "sheet metal part; nothing written.");
                Forms.MessageBox.Show(decline, DisplayName, Forms.MessageBoxButtons.OK, Forms.MessageBoxIcon.Information);
                return false;
            }

            IReadOnlyList<double> box = ReadBoxCm(doc);
            StockWorkbookReadResult workbook = LoadWorkbook();
            string? stored = PropertyReader.GetString(
                doc, PartPropertyStorage.MaterialSize.SetName, PartPropertyStorage.MaterialSize.PropertyName);

            var viewModel = new StockSizeViewModel(
                box, MaterialFinishTargets.UnitsOf(doc), workbook.Workbook, workbook.Problems, stored);
            if (StockSizeWindow.Ask(viewModel) != true)
            {
                AddinServices.Log.Info(LogPrefix + "cancelled in the window; nothing written.");
                return false;
            }

            _write = viewModel.WriteFor();
            if (_write == null)
            {
                AddinServices.Log.Info(LogPrefix + "no change to write.");
                return false;
            }

            return true;
        }

        protected override void Execute(Document doc)
        {
            PropertyWrite? write = _write;
            if (write == null)
                return;

            PropertyWriter.Apply(doc, new[] { write });
            AddinServices.Log.Info(LogPrefix + $"Material Size set to '{write.TextValue}'.");
        }

        /// <summary>The Core message for a sheet metal part, or null for any other part. A part that cannot be read is treated as not sheet metal.</summary>
        private static string? SheetMetalDecline(Document doc)
        {
            bool isSheetMetal = false;
            bool hasFlatPattern = false;
            ComSafe.Run(() =>
            {
                if (((PartDocument)doc).ComponentDefinition is SheetMetalComponentDefinition sheetMetal)
                {
                    isSheetMetal = true;
                    hasFlatPattern = sheetMetal.HasFlatPattern;
                }
            });
            return StockSizeEligibility.DeclineReason(isSheetMetal, hasFlatPattern);
        }

        /// <summary>
        /// The lengths of the part's oriented minimum range box in cm (spike 184 question 6: the true size of a part modelled off-axis, where
        /// <c>RangeBox</c> is the axis-aligned one). Empty when it cannot be read; Core then says the size could not be read and the user types it.
        /// </summary>
        private static IReadOnlyList<double> ReadBoxCm(Document doc)
        {
            var warnings = new List<string>();
            OrientedBox? box = ComSafe.Get(() => ((PartDocument)doc).ComponentDefinition.OrientedMinimumRangeBox, warnings, "oriented range box");
            double? one = box == null ? null : ComSafe.Get(() => (double?)box.DirectionOne.Length, warnings, "box direction one");
            double? two = box == null ? null : ComSafe.Get(() => (double?)box.DirectionTwo.Length, warnings, "box direction two");
            double? three = box == null ? null : ComSafe.Get(() => (double?)box.DirectionThree.Length, warnings, "box direction three");
            foreach (string warning in warnings)
                AddinServices.Log.Warn(LogPrefix + warning);
            return one.HasValue && two.HasValue && three.HasValue ? new[] { one.Value, two.Value, three.Value } : Array.Empty<double>();
        }

        private static StockWorkbookReadResult LoadWorkbook()
        {
            AddinSettings settings = AddinServices.Settings;
            string addinFolder;
            try
            {
                addinFolder = AddinServices.AddinFolder;
            }
            catch (InvalidOperationException ex)
            {
                // A workbook set in Settings does not need the add-in's folder.
                if (string.IsNullOrWhiteSpace(settings.StockWorkbookPath))
                {
                    AddinServices.Log.Error(LogPrefix + "the add-in's folder could not be found.", ex);
                    return new StockWorkbookReadResult(
                        StockWorkbook.Empty,
                        new[] { new StockWorkbookProblem(null, null, StockWorkbookMessages.AddinFolderUnknown(ex.Message)) });
                }

                addinFolder = string.Empty;
            }

            LibraryPath path = LibraryPaths.StockWorkbook(settings, addinFolder);
            StockWorkbookReadResult result = StockWorkbookReader.Read(path);
            foreach (StockWorkbookProblem problem in result.Problems)
                AddinServices.Log.Warn(LogPrefix + $"stock workbook {path.Path}: {problem}");
            return result;
        }
    }
}
