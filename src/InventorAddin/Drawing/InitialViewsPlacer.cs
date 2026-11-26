using System;
using System.Collections.Generic;
using System.Linq;
using Inventor;
using InventorAddin.Core.Drawing;
using InventorAddin.Core.Models;
using InventorAddin.Extraction;

// The Drawing folder holds the drawing-side helpers; the namespace names the feature, not the folder (Inventor has types named Drawing*).
namespace InventorAddin.PrintViews
{
    /// <summary>What Place Initial Views needs to know about a model, read once: its kind, its range box as the front view sees it, and the flat pattern's size.</summary>
    internal sealed class InitialViewsModelInfo
    {
        public InitialViewsModelInfo(InitialViewModelKind kind, InitialViewsBoxCm box, FlatPatternSizeCm? flatPattern)
        {
            Kind = kind;
            Box = box;
            FlatPattern = flatPattern;
        }

        public InitialViewModelKind Kind { get; }
        public InitialViewsBoxCm Box { get; }

        /// <summary>The flat pattern's size, or null when the part is not sheet metal or has no flat pattern.</summary>
        public FlatPatternSizeCm? FlatPattern { get; }
    }

    /// <summary>
    /// Reads a model for Place Initial Views and puts the planned views on a sheet (task 192), by the calls spike 183 (round 3) confirmed.
    /// The plan (which views, where, at what scale) is <see cref="InitialViewsPlan"/>'s, in Core; this class only reads Inventor and places.
    /// <see cref="Place"/> changes the drawing, so the caller wraps it in one transaction. Centimetres throughout, as Inventor's own units.
    /// </summary>
    internal static class InitialViewsPlacer
    {
        /// <summary>
        /// The model's kind, range box (X across, Y up, Z deep, as Create Print's front view takes it: spike 032) and, for a sheet metal part
        /// with a flat pattern, the flat pattern's size. Null when the model is not a part or assembly or has no geometry to show.
        /// </summary>
        public static InitialViewsModelInfo? Read(Document model, DocumentKind kind)
        {
            InitialViewModelKind? modelKind = InitialViewsSetup.ModelKindFor(kind);
            if (modelKind == null)
                return null;

            Box? box = ComSafe.Get(() => kind.IsPart()
                ? ((PartDocument)model).ComponentDefinition.RangeBox
                : ((AssemblyDocument)model).ComponentDefinition.RangeBox);
            if (box == null)
                return null;

            double? width = ComSafe.Get(() => (double?)(box.MaxPoint.X - box.MinPoint.X));
            double? height = ComSafe.Get(() => (double?)(box.MaxPoint.Y - box.MinPoint.Y));
            double? depth = ComSafe.Get(() => (double?)(box.MaxPoint.Z - box.MinPoint.Z));
            // A lifted comparison is false for null and NaN.
            if (!IsSize(width) || !IsSize(height) || !IsSize(depth))
                return null;

            FlatPatternSizeCm? flat = null;
            if (modelKind == InitialViewModelKind.SheetMetalPart)
            {
                double? length = null;
                double? flatWidth = null;
                ComSafe.Run(() =>
                {
                    var sheetMetal = (SheetMetalComponentDefinition)((PartDocument)model).ComponentDefinition;
                    if (sheetMetal.HasFlatPattern)
                    {
                        length = sheetMetal.FlatPattern.Length;
                        flatWidth = sheetMetal.FlatPattern.Width;
                    }
                });
                if (IsSize(length) && IsSize(flatWidth))
                    flat = new FlatPatternSizeCm(length!.Value, flatWidth!.Value);
            }

            return new InitialViewsModelInfo(modelKind.Value, new InitialViewsBoxCm(width!.Value, height!.Value, depth!.Value), flat);
        }

        private static bool IsSize(double? value) => value > 0 && !double.IsInfinity(value.Value);

        /// <summary>Reads the model's mass so that the drawing's weight field can resolve (spike 183, round 3). The value is not used.</summary>
        public static void ReadMass(Document model, DocumentKind kind)
        {
            ComSafe.Get(() => (double?)(kind.IsPart()
                ? ((PartDocument)model).ComponentDefinition.MassProperties.Mass
                : ((AssemblyDocument)model).ComponentDefinition.MassProperties.Mass));
        }

        /// <summary>
        /// Puts the views of <paramref name="plan"/> on <paramref name="sheet"/>, in the plan's order (front, top, right, iso; folded front,
        /// flat pattern, iso). The front view is the base view; top and right are projected from it, so in the template's third angle standard a
        /// view placed above it is the top view and one to its right is the right view. Every view, the iso included, is black and white
        /// (hidden line removed, no shading; task 228), and the scratch measuring places the same styles. The isometric view is its own base view (a projected
        /// view to the right of the front view would be the right view, which is where an assembly's iso is planned). The caller starts the
        /// transaction. Returns what was placed, for the log.
        /// </summary>
        private static List<(PlannedView Planned, DrawingView View)> PlaceViews(Sheet sheet, Document model, InitialViewsResult plan, bool scratch = false)
        {
            var placed = new List<(PlannedView Planned, DrawingView View)>();
            TransientGeometry geometry = InventorHost.App.TransientGeometry;
            DrawingViews views = sheet.DrawingViews;
            var modelDocument = (_Document)model;
            DrawingView? front = null;

            foreach (PlannedView planned in plan.Views)
            {
                Point2d centre = geometry.CreatePoint2d(planned.CentreXCm, planned.CentreYCm);
                DrawingView? view = null;

                switch (planned.Kind)
                {
                    case InitialViewKind.FoldedFront:
                        view = AddFoldedFront(views, modelDocument, model, centre, plan.Scale, plan.FrontOrientation);
                        front = view;
                        break;

                    case InitialViewKind.Front:
                        view = AddFront(views, modelDocument, model, centre, plan.Scale, plan.FrontOrientation);
                        front = view;
                        break;

                    case InitialViewKind.Top:
                    case InitialViewKind.Right:
                        if (front == null)
                            throw new InvalidOperationException($"The {planned.Kind} view is projected from the front view, which was not placed.");
                        view = views.AddProjectedView(front, centre, DrawingViewStyleEnum.kFromBaseDrawingViewStyle);
                        break;

                    case InitialViewKind.Iso:
                        view = AddIso(views, modelDocument, model, centre, plan.Scale, plan.FrontOrientation);
                        break;

                    case InitialViewKind.FlatPattern:
                        // The flat pattern is a base view with the folded-model option off (spike 183, round 3, C12).
                        NameValueMap options = InventorHost.App.TransientObjects.CreateNameValueMap();
                        options.Add("SheetMetalFoldedModel", false);
                        view = views.AddBaseView(modelDocument, centre, plan.Scale,
                            ViewOrientationTypeEnum.kFrontViewOrientation, DrawingViewStyleEnum.kHiddenLineRemovedDrawingViewStyle,
                            AdditionalOptions: options);
                        break;
                }

                if (view != null)
                {
                    // Every base view, the iso included, is at the sheet scale (task 227); a projected view follows its parent's.
                    if (!scratch && planned.Kind is not (InitialViewKind.Top or InitialViewKind.Right))
                        EnsureScale(view, plan.Scale, planned.Kind);
                    placed.Add((planned, view));
                }
            }

            return placed;
        }

        /// <summary>
        /// The sheet metal part's folded front view: looking along the bend lines with the base face's normal up the sheet (task 242, the
        /// rule is <see cref="FoldedViewRule"/>; a part with no usable bend falls back to <see cref="FaceViewRule"/>, task 229). Falls back
        /// to the standard front view, logged, when the face or its edges cannot be read.
        /// </summary>
        private static DrawingView AddFoldedFront(
            DrawingViews views, _Document modelDocument, Document model, Point2d centre, double scale, FaceViewOrientation? chosen)
        {
            // A direction chosen in the window (anything but Auto) wins over the base face (task 233).
            if (chosen != null)
                return AddFront(views, modelDocument, model, centre, scale, chosen);

            var warnings = new List<string>();
            Camera? camera = BuildBaseFaceCamera(model, warnings);
            if (camera == null)
            {
                AddinServices.Log.Warn("Place Initial Views: the base face could not be read, so the folded view uses the standard front. " +
                    (warnings.Count == 0 ? "Reason unknown." : string.Join(" ", warnings)));
                return views.AddBaseView(modelDocument, centre, scale,
                    ViewOrientationTypeEnum.kFrontViewOrientation, DrawingViewStyleEnum.kHiddenLineRemovedDrawingViewStyle);
            }

            return views.AddBaseView(modelDocument, centre, scale,
                ViewOrientationTypeEnum.kArbitraryViewOrientation, DrawingViewStyleEnum.kHiddenLineRemovedDrawingViewStyle,
                "", camera, Type.Missing);
        }

        /// <summary>
        /// The front view of a part or assembly (task 233): the standard front view when the direction is the standard front or none was
        /// chosen, otherwise an arbitrary orientation from a camera looking along the chosen direction (spike 225, question 1). Falls back
        /// to the standard front, logged, when the camera cannot be built.
        /// </summary>
        private static DrawingView AddFront(
            DrawingViews views, _Document modelDocument, Document model, Point2d centre, double scale, FaceViewOrientation? orientation)
        {
            if (orientation != null && !FrontViewRule.IsStandardFront(orientation))
            {
                var warnings = new List<string>();
                Camera? camera = null;
                ComSafe.Run(() => camera = BuildCamera(model, orientation), warnings, "front view camera");
                if (camera != null)
                {
                    return views.AddBaseView(modelDocument, centre, scale,
                        ViewOrientationTypeEnum.kArbitraryViewOrientation, DrawingViewStyleEnum.kHiddenLineRemovedDrawingViewStyle,
                        "", camera, Type.Missing);
                }

                AddinServices.Log.Warn("Place Initial Views: the front view's camera could not be built, so the standard front is used. " +
                    (warnings.Count == 0 ? "Reason unknown." : string.Join(" ", warnings)));
            }

            return views.AddBaseView(modelDocument, centre, scale,
                ViewOrientationTypeEnum.kFrontViewOrientation, DrawingViewStyleEnum.kHiddenLineRemovedDrawingViewStyle);
        }

        /// <summary>
        /// The iso view (task 243): Inventor's top-right iso when the front is the standard front or none was chosen (a sheet metal part on
        /// Auto), otherwise an arbitrary orientation looking at the front-top-right corner of the chosen front (<see cref="IsoViewRule"/>).
        /// Falls back to the standard iso, logged, when the camera cannot be built.
        /// </summary>
        private static DrawingView AddIso(
            DrawingViews views, _Document modelDocument, Document model, Point2d centre, double scale, FaceViewOrientation? front)
        {
            if (!IsoViewRule.IsStandardIso(front))
            {
                var warnings = new List<string>();
                Camera? camera = null;
                ComSafe.Run(() => camera = BuildCamera(model, IsoViewRule.Choose(front!)), warnings, "iso view camera");
                if (camera != null)
                {
                    return views.AddBaseView(modelDocument, centre, scale,
                        ViewOrientationTypeEnum.kArbitraryViewOrientation, DrawingViewStyleEnum.kHiddenLineRemovedDrawingViewStyle,
                        "", camera, Type.Missing);
                }

                AddinServices.Log.Warn("Place Initial Views: the iso view's camera could not be built, so the standard iso is used. " +
                    (warnings.Count == 0 ? "Reason unknown." : string.Join(" ", warnings)));
            }

            return views.AddBaseView(modelDocument, centre, scale,
                ViewOrientationTypeEnum.kIsoTopRightViewOrientation, DrawingViewStyleEnum.kHiddenLineRemovedDrawingViewStyle);
        }

        /// <summary>
        /// A camera looking at the middle of the model's range box from <paramref name="orientation"/>'s direction with its up
        /// direction up the sheet: the eye is 100 cm from the target along the direction, as spike 225 made it.
        /// </summary>
        private static Camera BuildCamera(Document model, FaceViewOrientation orientation)
        {
            Box box = model is PartDocument part
                ? part.ComponentDefinition.RangeBox
                : ((AssemblyDocument)model).ComponentDefinition.RangeBox;
            TransientGeometry geometry = InventorHost.App.TransientGeometry;
            Point target = geometry.CreatePoint(
                (box.MinPoint.X + box.MaxPoint.X) / 2, (box.MinPoint.Y + box.MaxPoint.Y) / 2, (box.MinPoint.Z + box.MaxPoint.Z) / 2);

            Camera camera = InventorHost.App.TransientObjects.CreateCamera();
            camera.Target = target;
            camera.Eye = geometry.CreatePoint(
                target.X + orientation.NormalX * 100, target.Y + orientation.NormalY * 100, target.Z + orientation.NormalZ * 100);
            camera.UpVector = geometry.CreateUnitVector(orientation.UpX, orientation.UpY, orientation.UpZ);
            return camera;
        }

        /// <summary>
        /// A camera for the sheet metal part's folded view (spike 225, question 1; task 242): the base face's outward normal is its plane
        /// normal, negated when <c>IsParamReversed</c>; the bends' axes are their front faces' cylinder axes. Null when any step fails.
        /// </summary>
        private static Camera? BuildBaseFaceCamera(Document model, List<string> warnings)
        {
            Camera? camera = null;
            ComSafe.Run(() =>
            {
                var definition = (SheetMetalComponentDefinition)((PartDocument)model).ComponentDefinition;
                if (!definition.HasFlatPattern)
                {
                    warnings.Add("The part has no flat pattern.");
                    return;
                }

                Face face = definition.FlatPattern.BaseFace;
                if (face.Geometry is not Plane plane)
                {
                    warnings.Add("The base face is not planar.");
                    return;
                }

                UnitVector normal = plane.Normal;
                double sign = face.IsParamReversed ? -1 : 1;

                var edges = new List<FaceEdgeCm>();
                foreach (Edge edge in face.Edges)
                {
                    if (edge.GeometryType != CurveTypeEnum.kLineSegmentCurve)
                        continue;
                    Point start = edge.StartVertex.Point;
                    Point stop = edge.StopVertex.Point;
                    edges.Add(new FaceEdgeCm(stop.X - start.X, stop.Y - start.Y, stop.Z - start.Z, start.DistanceTo(stop)));
                }

                var bends = ReadBendAxes(definition, warnings);
                FaceViewOrientation? orientation = FoldedViewRule.Choose(sign * normal.X, sign * normal.Y, sign * normal.Z, bends, edges);
                if (orientation == null)
                {
                    warnings.Add($"No usable straight edge on the base face ({edges.Count} found).");
                    return;
                }

                camera = BuildCamera(model, orientation);
            }, warnings, "base face camera");
            return camera;
        }

        /// <summary>
        /// Each bend's axis (its front face's cylinder <c>AxisVector</c>) and length (the longest straight edge of that face, else its
        /// radius). A bend that cannot be read is skipped with a warning.
        /// </summary>
        private static List<BendAxisCm> ReadBendAxes(SheetMetalComponentDefinition definition, List<string> warnings)
        {
            var axes = new List<BendAxisCm>();
            ComSafe.Run(() =>
            {
                foreach (Bend bend in definition.Bends)
                {
                    Bend current = bend;
                    ComSafe.Run(() =>
                    {
                        foreach (Face face in current.FrontFaces)
                        {
                            if (face.Geometry is not Cylinder cylinder)
                                continue;

                            UnitVector axis = cylinder.AxisVector;
                            double length = 0;
                            foreach (Edge edge in face.Edges)
                            {
                                if (edge.GeometryType != CurveTypeEnum.kLineSegmentCurve)
                                    continue;
                                length = Math.Max(length, edge.StartVertex.Point.DistanceTo(edge.StopVertex.Point));
                            }

                            axes.Add(new BendAxisCm(axis.X, axis.Y, axis.Z, length > 0 ? length : cylinder.Radius));
                            break;
                        }
                    }, warnings, "bend");
                }
            }, warnings, "bends");
            return axes;
        }

        private static void EnsureScale(DrawingView view, double scale, InitialViewKind kind)
        {
            double? actual = ComSafe.Get(() => (double?)view.Scale);
            if (actual is double a && Math.Abs(a - scale) <= scale * 1e-6)
                return;

            AddinServices.Log.Warn($"Place Initial Views: the {kind} view was placed at scale {actual?.ToString("0.####") ?? "unknown"}, not {scale:0.####}; setting it.");
            ComSafe.Run(() => view.Scale = scale);
        }

        /// <summary>
        /// Puts the views of <paramref name="plan"/> on <paramref name="sheet"/> (see <see cref="PlaceViews"/>) and reads each placed view's
        /// size back. Returns one line per view for the log: its centre, its placed size and the size the plan estimated (task 227).
        /// </summary>
        public static IReadOnlyList<string> Place(Sheet sheet, Document model, InitialViewsResult plan)
        {
            var lines = new List<string>();
            foreach ((PlannedView planned, DrawingView view) in PlaceViews(sheet, model, plan))
            {
                double? width = ComSafe.Get(() => (double?)view.Width);
                double? height = ComSafe.Get(() => (double?)view.Height);
                string actual = width is double w && height is double h ? $"placed {w:0.##} x {h:0.##}" : "placed size unknown";
                lines.Add($"{planned.Kind} at ({planned.CentreXCm:0.##}, {planned.CentreYCm:0.##}) cm, {actual}, planned {planned.WidthCm:0.##} x {planned.HeightCm:0.##} cm");
            }

            return lines;
        }

        /// <summary><see cref="Measure"/> with the wait cursor showing, as the measuring takes a moment on a large model.</summary>
        public static IReadOnlyDictionary<InitialViewKind, ViewSizeCm>? MeasureWithWaitCursor(
            Document model, InitialViewsModelInfo info, string template, PrintUnits units, FrontViewChoice front = FrontViewChoice.Auto)
        {
            System.Windows.Forms.Cursor previous = System.Windows.Forms.Cursor.Current;
            System.Windows.Forms.Cursor.Current = System.Windows.Forms.Cursors.WaitCursor;
            try
            {
                return Measure(model, info, template, units, front);
            }
            finally
            {
                System.Windows.Forms.Cursor.Current = previous;
            }
        }

        /// <summary>
        /// The real size at scale 1 of each view the model's kind gets, read from views placed in a scratch drawing made from
        /// <paramref name="template"/> and closed without saving (task 227). The range box gives only an upper bound for a view: the
        /// model's outline is inside it, and an assembly's box can be far larger than its geometry. Null when the measuring fails for any
        /// reason (logged), so the caller keeps the estimate.
        /// </summary>
        public static IReadOnlyDictionary<InitialViewKind, ViewSizeCm>? Measure(
            Document model, InitialViewsModelInfo info, string template, PrintUnits units, FrontViewChoice front = FrontViewChoice.Auto)
        {
            Document? scratch = null;
            bool previousSilent = InventorHost.App.SilentOperation;
            try
            {
                InventorHost.App.SilentOperation = true;
                try
                {
                    scratch = InventorHost.App.Documents.Add(DocumentTypeEnum.kDrawingDocumentObject, template, false);
                }
                finally
                {
                    InventorHost.App.SilentOperation = previousSilent;
                }

                Sheet sheet = ((DrawingDocument)scratch).ActiveSheet;

                // At the estimate's own scale on the usable area of the units' default sheet (A3 or ANSI B), so a big assembly stays near the
                // sheet. The template's own first sheet is the one measured on. Only each view's own size is read (placed size divided by its
                // scale), so overlap and the sheet size do not matter (task 232).
                // The views follow the chosen Front view, so the measured sizes are those of the print (task 233).
                FrontViewSelection selection = FrontViewRule.Select(front, info.Kind, info.Box);
                InitialViewsResult plan = InitialViewsPlan.Plan(
                    info.Kind, selection.Box, info.FlatPattern, InitialViewsPlan.UsableArea(InitialViewsSetup.DefaultSheet(units)))
                    with { FrontOrientation = selection.Orientation };

                var sizes = new Dictionary<InitialViewKind, ViewSizeCm>();
                foreach ((PlannedView planned, DrawingView view) in PlaceViews(sheet, model, plan, scratch: true))
                {
                    double viewScale = ComSafe.Get(() => (double?)view.Scale) ?? plan.Scale;
                    double? width = ComSafe.Get(() => (double?)view.Width) / viewScale;
                    double? height = ComSafe.Get(() => (double?)view.Height) / viewScale;
                    if (IsSize(width) && IsSize(height))
                        sizes[planned.Kind] = new ViewSizeCm(width!.Value, height!.Value);
                }

                AddinServices.Log.Info($"Initial Views: front view {front}; measured (per view, at scale 1): " + string.Join("; ",
                    sizes.Select(s => $"{s.Key} {s.Value.WidthCm:0.##} x {s.Value.HeightCm:0.##} cm")) +
                    $" (estimated from the range box, at scale 1: {string.Join("; ", plan.Views.Select(v => $"{v.Kind} {v.WidthCm / plan.Scale:0.##} x {v.HeightCm / plan.Scale:0.##}"))}).");
                return sizes.Count == 0 ? null : sizes;
            }
            catch (Exception ex)
            {
                AddinServices.Log.Warn($"Initial Views: measuring the views failed ({ex.Message}); the range box estimate is used.");
                return null;
            }
            finally
            {
                if (scratch != null)
                {
                    Document toClose = scratch;
                    ComSafe.Run(() => toClose.Close(true));
                }
            }
        }
    }
}
