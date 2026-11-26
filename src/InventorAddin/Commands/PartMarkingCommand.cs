using System;
using System.Collections.Generic;
using Inventor;
using InventorAddin.Core.Drawing;
using InventorAddin.Core.Models;
using InventorAddin.Core.Settings;
using InventorAddin.Core.ViewModels;
using InventorAddin.Extraction;
using InventorAddin.PrintViews;
using InventorAddin.UI;
using Forms = System.Windows.Forms;
using SysEnvironment = System.Environment;

namespace InventorAddin.Commands
{
    /// <summary>
    /// Part Marking (spec 05, "Added for M5: New tools", item 2; task 199): the Part Marking window collects the method, the extra lines and
    /// the target; after OK the user picks an edge in a view, and the command places either a leader note to that edge or a dashed marking
    /// zone with its text on that edge's view, in the highlight colour from Settings. The words and sizes are Core's
    /// (<see cref="PartMarking"/>, <see cref="PartMarkingViewModel"/>); the placing is <see cref="PartMarkingWriter"/>'s. The whole
    /// placing is one transaction, so one Undo removes the marking. The engraved feature is task 200.
    /// </summary>
    /// <remarks>
    /// Adds drawing notes and a sketch, which are not geometry, so its command type is <c>kNonShapeEditCmdType</c>. The window and the pick
    /// run in <see cref="GatherInput"/>, outside the transaction.
    /// </remarks>
    public sealed class PartMarkingCommand : DocumentEditCommand
    {
        /// <summary>Shown when the active document is not a drawing.</summary>
        public const string WrongDocumentText = "Open a drawing first. Part Marking works on a drawing.";

        /// <summary>The prompt while the user picks the edge for the leader.</summary>
        public const string PickEdgePrompt = "Pick the edge the marking note should point to (Esc to cancel)";

        /// <summary>The prompt while the user picks the view for the zone.</summary>
        public const string PickZoneViewPrompt = "Pick an edge in the view to put the marking zone on (Esc to cancel)";

        public override string InternalName => Core.Ribbon.CommandNames.PartMarking;
        public override string DisplayName => "Part Marking";
        public override string Description =>
            "Mark the part number on the drawing: a note with a leader to an edge, or a dashed marking zone, in the highlight colour.";

        protected override CommandTypesEnum CommandType => CommandTypesEnum.kNonShapeEditCmdType;

        protected override bool CanRunOn(DocumentKind kind) => kind == DocumentKind.Drawing;

        protected override string WrongDocumentMessage(DocumentKind kind) => WrongDocumentText;

        private PartMarkingOptions? _options;
        private DrawingCurve? _curve;
        private DrawingView? _view;

        protected override bool GatherInput(Document doc)
        {
            _options = null;
            _curve = null;
            _view = null;

            var drawing = (DrawingDocument)doc;
            Document? model = PrintUnitRefresher.FindModel(drawing);
            bool isAssembly = model != null && ComSafe.Get(() => DocumentKinds.Of(model)).IsAssembly();
            PrintUnits printUnits = Commands.MaterialFinishTargets.UnitsOf(model ?? doc);
            UnitsSystem units = printUnits == PrintUnits.Imperial ? UnitsSystem.Imperial : UnitsSystem.Metric;

            var viewModel = new PartMarkingViewModel(units, isAssembly, AddinServices.Settings.PartMarkingColour);
            PartMarkingOptions? options = PartMarkingWindow.Ask(viewModel);
            if (options == null)
            {
                AddinServices.Log.Info("Part Marking: cancelled in the window; nothing was placed.");
                return false;
            }

            string prompt = options.Target == PartMarkingTarget.Leader ? PickEdgePrompt : PickZoneViewPrompt;
            object? picked = InventorHost.App.CommandManager.Pick(SelectionFilterEnum.kDrawingCurveSegmentFilter, prompt);
            if (picked is not DrawingCurveSegment segment)
            {
                AddinServices.Log.Info("Part Marking: no edge picked; nothing was placed.");
                return false;
            }

            DrawingCurve? curve = ComSafe.Get(() => segment.Parent);
            DrawingView? view = curve == null ? null : ComSafe.Get(() => curve.Parent);
            if (curve == null || view == null)
            {
                Forms.MessageBox.Show(
                    "The picked edge could not be read, so nothing was placed.", DisplayName, Forms.MessageBoxButtons.OK, Forms.MessageBoxIcon.Warning);
                return false;
            }

            _options = options;
            _curve = curve;
            _view = view;
            return true;
        }

        protected override void Execute(Document doc)
        {
            if (_options == null || _curve == null || _view == null)
                return;

            var warnings = new List<string>();
            if (_options.Target == PartMarkingTarget.Leader)
                PartMarkingWriter.AddLeader(_curve, _view, _options, warnings);
            else
                PartMarkingWriter.AddZone(_view, _options, warnings);

            ComSafe.Run(() => ((DrawingDocument)doc).Update(), warnings, "drawing update");

            foreach (string warning in warnings)
                AddinServices.Log.Warn($"Part Marking: {warning}");
            AddinServices.Log.Info(
                $"Part Marking: {_options.Target} placed on view '{ComSafe.Get(() => _view.Name)}', method {_options.Method}, colour {_options.HighlightColour}.");

            if (warnings.Count > 0)
            {
                Forms.MessageBox.Show(
                    "The marking was placed, but some of it could not be set:" + SysEnvironment.NewLine + SysEnvironment.NewLine +
                    string.Join(SysEnvironment.NewLine, warnings),
                    DisplayName, Forms.MessageBoxButtons.OK, Forms.MessageBoxIcon.Warning);
            }
        }
    }
}
