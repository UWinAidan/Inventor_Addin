using System;
using System.Collections.Generic;
using Inventor;
using InventorAddin.Core.Models;
using InventorAddin.PrintViews;
using Forms = System.Windows.Forms;
using SysEnvironment = System.Environment;

namespace InventorAddin.Commands
{
    /// <summary>
    /// Overall REF Dimensions (spec 05, "Added for M5: New tools", item 4; task 204): adds a bold "value REF" width and height to the
    /// selected view, or to the sheet's first base view when no view is selected. The whole command is one transaction, so one Undo removes
    /// both dimensions. The plan and the text are Core's (<c>RefDimensionPlan</c>); the placing is <see cref="RefDimensionWriter"/>'s.
    /// </summary>
    /// <remarks>Adds drawing dimensions, which are not geometry, so its command type is <c>kNonShapeEditCmdType</c>.</remarks>
    public sealed class RefDimensionsCommand : DocumentEditCommand
    {
        /// <summary>Shown when the active document is not a drawing.</summary>
        public const string WrongDocumentText = "Open a drawing first. Overall REF Dimensions works on a drawing.";

        /// <summary>Shown when the active sheet has no view to dimension.</summary>
        public const string NoViewText = "The sheet has no views. Place a view first.";

        public override string InternalName => Core.Ribbon.CommandNames.OverallRefDimensions;
        public override string DisplayName => "Overall REF Dimensions";
        public override string Description =>
            "Add bold overall width and height REF dimensions to the selected view, or the sheet's first base view.";

        protected override CommandTypesEnum CommandType => CommandTypesEnum.kNonShapeEditCmdType;

        protected override bool CanRunOn(DocumentKind kind) => kind == DocumentKind.Drawing;

        protected override string WrongDocumentMessage(DocumentKind kind) => WrongDocumentText;

        private DrawingView? _view;
        private readonly List<string> _warnings = new List<string>();

        protected override bool GatherInput(Document doc)
        {
            _view = null;
            _warnings.Clear();
            var drawing = (DrawingDocument)doc;

            // A selected view wins; otherwise the sheet's first base view (no parent view), then its first view.
            foreach (object item in drawing.SelectSet)
            {
                if (item is DrawingView selected)
                {
                    _view = selected;
                    break;
                }
            }

            if (_view == null)
            {
                Sheet? sheet = ComSafe.Get(() => drawing.ActiveSheet);
                int count = sheet == null ? 0 : ComSafe.Get(() => (int?)sheet.DrawingViews.Count) ?? 0;
                for (int i = 1; sheet != null && i <= count; i++)
                {
                    int index = i;
                    DrawingView? candidate = ComSafe.Get(() => sheet.DrawingViews[index]);
                    if (candidate == null) continue;
                    _view ??= candidate;
                    if (ComSafe.Get(() => candidate.ParentView) == null)
                    {
                        _view = candidate;
                        break;
                    }
                }
            }

            if (_view == null)
            {
                Forms.MessageBox.Show(NoViewText, DisplayName, Forms.MessageBoxButtons.OK, Forms.MessageBoxIcon.Information);
                return false;
            }

            return true;
        }

        protected override void Execute(Document doc)
        {
            DrawingView? view = _view;
            if (view == null)
                return;

            int placed = RefDimensionWriter.Add(view, _warnings);
            ComSafe.Run(() => ((DrawingDocument)doc).Update(), _warnings, "drawing update");

            string viewName = ComSafe.Get(() => view.Name) ?? "?";
            foreach (string warning in _warnings)
                AddinServices.Log.Warn($"Overall REF Dimensions: {warning}");
            AddinServices.Log.Info($"Overall REF Dimensions: {placed} dimension(s) placed on view '{viewName}'.");

            if (_warnings.Count > 0)
            {
                Forms.MessageBox.Show(
                    (placed > 0 ? "Some REF dimensions were placed, but not all:" : "No REF dimension was placed:") +
                    SysEnvironment.NewLine + SysEnvironment.NewLine + string.Join(SysEnvironment.NewLine, _warnings),
                    DisplayName, Forms.MessageBoxButtons.OK, Forms.MessageBoxIcon.Warning);
            }
        }
    }
}
