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
    /// placing is one transaction, so one Undo removes the marking. With the tick box on, the part number is also engraved on the drawing's part (task 200, <see cref="EngravedPartNumberWriter"/>), in its own transaction on the part, so one Undo in the part removes it.
    /// </summary>
    /// <remarks>
    /// Adds drawing notes and a sketch, which are not geometry, so its command type is <c>kNonShapeEditCmdType</c>. The engraving adds a cut feature to the part, but it is a separate transaction on the part; the command type is kept as kNonShapeEditCmdType because the command's own document is the drawing, whose edits are not geometry. Whether kShapeEditCmdType suits the part edit better is not known. The window and the pick
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

        private readonly List<string> _warnings = new List<string>();

        /// <summary>
        /// The window and pick, then the drawing marking in one transaction on the drawing, and only after that has ended the engraving in
        /// its own transaction on the part. Two transactions on two documents, never nested, so each document has one "Part Marking" Undo.
        /// </summary>
        protected override void Run(Document doc)
        {
            _warnings.Clear();
            if (!GatherInput(doc) || _options == null)
                return;

            RunInTransaction(doc, () => Execute(doc));

            if (PartMarking.WillEngraveFeature(_options))
                EngraveOnPart((DrawingDocument)doc, _options, _warnings);

            foreach (string warning in _warnings)
                AddinServices.Log.Warn($"Part Marking: {warning}");
            AddinServices.Log.Info(
                $"Part Marking: {_options.Target} placed on view '{ComSafe.Get(() => _view!.Name)}', method {_options.Method}, colour {_options.HighlightColour}.");

            if (_warnings.Count > 0)
            {
                Forms.MessageBox.Show(
                    "The marking was placed, but some of it could not be set:" + SysEnvironment.NewLine + SysEnvironment.NewLine +
                    string.Join(SysEnvironment.NewLine, _warnings),
                    DisplayName, Forms.MessageBoxButtons.OK, Forms.MessageBoxIcon.Warning);
            }
        }

        /// <summary>The drawing's marking only; the engraving is done after this transaction ends (see <see cref="Run"/>).</summary>
        protected override void Execute(Document doc)
        {
            if (_options == null || _curve == null || _view == null)
                return;

            if (_options.Target == PartMarkingTarget.Leader)
                PartMarkingWriter.AddLeader(_curve, _view, _options, _warnings);
            else
                PartMarkingWriter.AddZone(_view, _options, _warnings);

            ComSafe.Run(() => ((DrawingDocument)doc).Update(), _warnings, "drawing update");
        }

        /// <summary>
        /// Engraves the part number on the drawing's part, in its own transaction on the part so one Undo in the part removes it. A failure
        /// aborts that transaction (the part is left as it was) and becomes a warning: the drawing marking stays.
        /// </summary>
        private void EngraveOnPart(DrawingDocument drawing, PartMarkingOptions options, List<string> warnings)
        {
            Document? model = PrintUnitRefresher.FindModel(drawing);
            if (model is not PartDocument part)
            {
                warnings.Add("The drawing's model is not a part, so the part number was not engraved.");
                return;
            }

            try
            {
                RunInTransaction(model, () => EngravedPartNumberWriter.Add(part, options, warnings));
            }
            catch (Exception ex)
            {
                warnings.Add("The part number could not be engraved: " + ex.Message);
                return;
            }

            ComSafe.Run(() => part.Update(), warnings, "part update");
            AddinServices.Log.Info($"Part Marking: part number engraved on '{ComSafe.Get(() => part.DisplayName)}'.");
        }
    }
}
