using System;
using System.Collections.Generic;
using Inventor;
using InventorAddin.Core.Drawing;

// The Drawing folder holds the drawing-side helpers; the namespace names the feature, not the folder (Inventor has types named Drawing*).
namespace InventorAddin.PrintViews
{
    /// <summary>
    /// Adds the overall REF dimensions to a drawing view (spec 05, "Added for M5", item 4; task 204). Which edges are the extremes, where the
    /// text goes and the text itself are Core's (<see cref="RefDimensionPlan"/>); this class reads the view's curves and applies the plan.
    /// Calls are those of spike 184, question 5, and task 204's own probe (<c>DrawingView.DrawingCurves</c>, <c>DrawingCurve.StartPoint</c>,
    /// <c>EndPoint</c>, <c>CurveType</c>, <c>Segments</c> with <c>Arc2d</c>, <c>Evaluator2D.RangeBox</c>; <c>Sheet.CreateGeometryIntent</c>;
    /// <c>GeneralDimensions.AddLinear</c>; <c>Text.FormattedText</c>; <c>Style.TextStyle.FontSize</c>; <c>Delete</c>).
    /// </summary>
    /// <remarks>
    /// Run it inside the caller's transaction, so one Undo removes both dimensions. Task 204's probe found that a linear dimension to an arc
    /// always measures to the arc's centre (any intent point, any dimension type), so an end that is an arc, circle or sloped line cannot be
    /// dimensioned to its extent: the dimension is checked after it is made and removed with a warning when it does not read the planned size.
    /// </remarks>
    internal static class RefDimensionWriter
    {
        /// <summary>The dimensions that were placed (0, 1 or 2).</summary>
        public static int Add(DrawingView view, List<string> warnings)
        {
            var sheet = ComSafe.Get(() => (Sheet)view.Parent, warnings, "REF: view's sheet");
            if (sheet == null) return 0;

            var curves = new List<DrawingCurve>();
            var refCurves = new List<RefCurve>();
            ComSafe.Run(() =>
            {
                foreach (DrawingCurve curve in view.DrawingCurves)
                {
                    RefCurve? read = ReadCurve(curve, warnings);
                    if (read == null) continue;
                    curves.Add(curve);
                    refCurves.Add(read.Value);
                }
            }, warnings, "REF: view curves");

            double centreX = view.Center.X, centreY = view.Center.Y, halfW = view.Width / 2, halfH = view.Height / 2;
            var rect = new ViewRectCm(centreX - halfW, centreY - halfH, centreX + halfW, centreY + halfH);
            RefDimensionResult? plan = RefDimensionPlan.Plan(refCurves, rect);
            if (plan == null)
            {
                warnings.Add("The view has no edges to dimension.");
                return 0;
            }

            double scale = ComSafe.Get(() => view.Scale, warnings, "REF: view scale");
            int placed = 0;
            foreach (RefDimension dim in new[] { plan.Width, plan.Height })
                if (AddOne(sheet, curves, dim, scale, warnings)) placed++;
            return placed;
        }

        private static bool AddOne(Sheet sheet, List<DrawingCurve> curves, RefDimension dim, double viewScale, List<string> warnings)
        {
            LinearGeneralDimension? dimension = null;
            try
            {
                TransientGeometry geometry = InventorHost.App.TransientGeometry;
                GeometryIntent first = sheet.CreateGeometryIntent(curves[dim.FirstCurveIndex], Type.Missing);
                GeometryIntent second = sheet.CreateGeometryIntent(curves[dim.SecondCurveIndex], Type.Missing);
                dimension = sheet.DrawingDimensions.GeneralDimensions.AddLinear(
                    geometry.CreatePoint2d(dim.TextPoint.XCm, dim.TextPoint.YCm), first, second,
                    dim.IsHorizontal ? DimensionTypeEnum.kHorizontalDimensionType : DimensionTypeEnum.kVerticalDimensionType);

                if (dim.NeedsExtentFallback && !RefDimensionPlan.ValueMatchesPlan(dimension.ModelValue, dim.SpanCm, viewScale))
                {
                    LinearGeneralDimension wrong = dimension;
                    var deleteFailures = new List<string>();
                    ComSafe.Run(() => wrong.Delete(), deleteFailures, "REF: remove wrong dimension");
                    warnings.Add(deleteFailures.Count == 0
                        ? RefDimensionPlan.NotPlacedWarning(dim.IsHorizontal)
                        : RefDimensionPlan.WrongDimensionMayRemainWarning(dim.IsHorizontal));
                    return false;
                }

                LinearGeneralDimension placedDimension = dimension;
                double normalHeightCm = ComSafe.Get(() => placedDimension.Style.TextStyle.FontSize, warnings, "REF: dimension text size");
                string text = normalHeightCm > 0 ? RefDimensionPlan.FormattedTextFor(normalHeightCm) : RefDimensionPlan.FormattedText;
                ComSafe.Run(() => placedDimension.Text.FormattedText = text, warnings, "REF: dimension text");
                return true;
            }
            catch (Exception ex)
            {
                warnings.Add((dim.IsHorizontal ? "The overall width" : "The overall height") + " could not be added: " + ex.Message);
                LinearGeneralDimension? failed = dimension;
                if (failed != null)
                    ComSafe.Run(() => failed.Delete(), warnings, "REF: remove failed dimension");
                return false;
            }
        }

        /// <summary>A curve as Core's <see cref="RefCurve"/>: a line by its end points, an arc by its centre, radius, start angle and signed sweep, anything else by its 2D range.</summary>
        private static RefCurve? ReadCurve(DrawingCurve curve, List<string> warnings)
        {
            CurveTypeEnum type = ComSafe.Get(() => curve.CurveType, warnings, "REF: curve type");
            if (type == CurveTypeEnum.kLineSegmentCurve)
            {
                Point2d? start = ComSafe.Get(() => curve.StartPoint, warnings, "REF: line start");
                Point2d? end = ComSafe.Get(() => curve.EndPoint, warnings, "REF: line end");
                if (start != null && end != null)
                    return RefCurve.Line(start.X, start.Y, end.X, end.Y);
            }
            else if (type == CurveTypeEnum.kCircularArcCurve)
            {
                Arc2d? arc = ComSafe.Get(() => curve.Segments[1].Geometry as Arc2d, warnings, "REF: arc geometry");
                if (arc != null)
                    return RefCurve.Arc(arc.Center.X, arc.Center.Y, arc.Radius, arc.StartAngle, arc.SweepAngle);
            }

            Box2d? box = ComSafe.Get(() => curve.Evaluator2D.RangeBox, warnings, "REF: curve range");
            if (box == null) return null;
            return new RefCurve(box.MinPoint.X, box.MinPoint.Y, box.MaxPoint.X, box.MaxPoint.Y, box.MinPoint.X, box.MinPoint.Y, box.MaxPoint.X, box.MaxPoint.Y);
        }
    }
}
