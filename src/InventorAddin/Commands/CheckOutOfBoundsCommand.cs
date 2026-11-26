using System;
using System.Collections.Generic;
using System.Linq;
using Inventor;
using InventorAddin.Core.Drawing;
using InventorAddin.Core.Models;
using InventorAddin.Core.Ribbon;
using InventorAddin.PrintViews;
using InventorAddin.UI;
using Forms = System.Windows.Forms;

namespace InventorAddin.Commands
{
    /// <summary>
    /// Check Out of Bounds (spec 05, "Commands: Check Out of Bounds"; task 195): reads every sheet of the drawing
    /// (<see cref="SheetExtentsReader"/>), asks <see cref="OutOfBoundsCheck"/> (Core) which views and annotations fall outside the border
    /// or over the title block, and shows either "Everything is inside the border." or a list. Picking a line in the list makes its
    /// sheet active and selects the item.
    /// </summary>
    /// <remarks>
    /// Changes nothing in the document, so, like the export commands, its command type is the default <c>kQueryOnlyCmdType</c> and it
    /// is not a <see cref="DocumentEditCommand"/>: no transaction. Selecting and activating a sheet are not edits.
    /// </remarks>
    public sealed class CheckOutOfBoundsCommand : AddinCommand
    {
        public override string InternalName => CommandNames.CheckOutOfBounds;
        public override string DisplayName => "Check Out of Bounds";
        public override string Description =>
            "List the views and annotations that fall outside the border or over the title block, on every sheet.";

        protected override void Execute()
        {
            Document? doc = CommandTargetDocument();
            if (doc == null || DocumentKinds.Of(doc) != DocumentKind.Drawing)
            {
                Forms.MessageBox.Show(OutOfBoundsReport.WrongDocumentMessage, DisplayName, Forms.MessageBoxButtons.OK, Forms.MessageBoxIcon.Information);
                return;
            }

            var drawing = (DrawingDocument)doc;
            var warnings = new List<string>();
            var findings = new List<SheetFinding>();
            var extentsList = new List<SheetExtents>();
            var targets = new List<(SheetExtents Sheet, object Target)>();

            Sheets? sheets = ComSafe.Get(() => drawing.Sheets, warnings, "sheets");
            int count = sheets == null ? 0 : ComSafe.Get(() => (int?)sheets.Count) ?? 0;
            for (int i = 1; i <= count; i++)
            {
                Sheet? sheet = ComSafe.Get(() => sheets![i], warnings, $"sheet {i}");
                if (sheet == null)
                    continue;

                SheetExtents extents = SheetExtentsReader.Read(sheet, warnings);
                extentsList.Add(extents);
                foreach (SheetExtentItem item in extents.Items)
                {
                    // One item at a time, so each finding stays tied to the object it came from.
                    foreach (BoundsFinding finding in OutOfBoundsCheck.Check(extents.Border, extents.TitleBlock, new[] { item.Item }))
                    {
                        findings.Add(new SheetFinding(extents.Name, finding));
                        targets.Add((extents, item.Target));
                    }
                }
            }

            foreach (string warning in warnings)
                AddinServices.Log.Warn($"Check Out of Bounds: {warning}");
            AddinServices.Log.Info($"Check Out of Bounds: {findings.Count} finding(s) on {count} sheet(s).");

            if (findings.Count == 0)
            {
                string text = OutOfBoundsReport.NoneMessage;
                int skipped = extentsList.Sum(e => e.Skipped);
                if (skipped > 0)
                    text += $"{System.Environment.NewLine}{System.Environment.NewLine}{OutOfBoundsReport.NotCheckedLine(skipped)}";
                Forms.MessageBox.Show(text, DisplayName, Forms.MessageBoxButtons.OK, Forms.MessageBoxIcon.Information);
                return;
            }

            var window = new OutOfBoundsWindow(
                OutOfBoundsReport.Heading(findings.Count),
                OutOfBoundsReport.Lines(findings),
                index => Select(drawing, targets[index].Sheet.Sheet, targets[index].Target));
            WindowHost.ShowDialog(window, IconNames.CheckOutOfBounds);
        }

        /// <summary>Makes the sheet active and selects the item. A failure is logged; the list stays open.</summary>
        private static void Select(DrawingDocument drawing, Sheet sheet, object target)
        {
            try
            {
                sheet.Activate();
                drawing.SelectSet.Clear();
                drawing.SelectSet.Select(target);
            }
            catch (Exception ex)
            {
                AddinServices.Log.Warn($"Check Out of Bounds: could not select the item. {ex.Message}");
            }
        }
    }
}
