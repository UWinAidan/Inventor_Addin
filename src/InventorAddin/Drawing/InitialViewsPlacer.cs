using System;
using System.Collections.Generic;
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
        /// flat pattern). The front view is the base view; top and right are projected from it, so in the template's third angle standard a
        /// view placed above it is the top view and one to its right is the right view. The isometric view is its own base view (a projected
        /// view to the right of the front view would be the right view, which is where an assembly's iso is planned). The caller starts the
        /// transaction. Returns what was placed, for the log.
        /// </summary>
        public static IReadOnlyList<string> Place(Sheet sheet, Document model, InitialViewsResult plan)
        {
            var placed = new List<string>();
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
                    case InitialViewKind.Front:
                    case InitialViewKind.FoldedFront:
                        view = views.AddBaseView(modelDocument, centre, plan.Scale,
                            ViewOrientationTypeEnum.kFrontViewOrientation, DrawingViewStyleEnum.kHiddenLineRemovedDrawingViewStyle);
                        front = view;
                        break;

                    case InitialViewKind.Top:
                    case InitialViewKind.Right:
                        if (front == null)
                            throw new InvalidOperationException($"The {planned.Kind} view is projected from the front view, which was not placed.");
                        view = views.AddProjectedView(front, centre, DrawingViewStyleEnum.kFromBaseDrawingViewStyle);
                        break;

                    case InitialViewKind.Iso:
                        view = views.AddBaseView(modelDocument, centre, plan.Scale,
                            ViewOrientationTypeEnum.kIsoTopRightViewOrientation, DrawingViewStyleEnum.kShadedDrawingViewStyle);
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
                    placed.Add($"{planned.Kind} at ({planned.CentreXCm:0.##}, {planned.CentreYCm:0.##}) cm");
            }

            return placed;
        }
    }
}
