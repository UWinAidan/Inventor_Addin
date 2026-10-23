using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using InventorAddin.Core.Holes;

namespace InventorAddin.UI.Controls
{
    /// <summary>
    /// Draws a <see cref="HoleDiagram"/> (task 104): the part's section hatched, the hole's outline, edges, thread and
    /// centre line, the dimension lines with their arrowheads, and the labels, with <c>fx</c> after a value typed as an
    /// expression. Every position and text comes from Core; this only draws them, scaled to fit when the control is not
    /// the diagram's size. The brushes, the font and the size are the implicit <c>HoleDiagramView</c> style in
    /// <c>UI/Theme/Styles.xaml</c>, from the theme's colours: the diagram follows the light or dark theme and is never
    /// drawn in a fit colour.
    /// </summary>
    public sealed class HoleDiagramView : Control
    {
        // Strokes and marks, as the mockup draws them.
        private const double OutlineThickness = 1.5;
        private const double EdgeThickness = 1.0;
        private const double ThreadThickness = 0.8;
        private const double CentreLineThickness = 0.8;
        private const double DimensionThickness = 0.8;
        private const double ExtensionThickness = 0.6;
        private const double HatchSpacing = 6.0;
        private const double HatchThickness = 1.0;
        private const double ArrowLength = 6.0;
        private const double ArrowHalfWidth = 2.4;
        private const double ExpressionGap = 3.0;
        private const string ExpressionText = "fx";

        public static readonly DependencyProperty DiagramProperty = Register<HoleDiagram>(nameof(Diagram));
        public static readonly DependencyProperty OutlineBrushProperty = Register<Brush>(nameof(OutlineBrush));
        public static readonly DependencyProperty EdgeBrushProperty = Register<Brush>(nameof(EdgeBrush));
        public static readonly DependencyProperty CentreLineBrushProperty = Register<Brush>(nameof(CentreLineBrush));
        public static readonly DependencyProperty DimensionBrushProperty = Register<Brush>(nameof(DimensionBrush));
        public static readonly DependencyProperty HatchBrushProperty = Register<Brush>(nameof(HatchBrush));
        public static readonly DependencyProperty HatchLineBrushProperty = Register<Brush>(nameof(HatchLineBrush));
        public static readonly DependencyProperty ExpressionBrushProperty = Register<Brush>(nameof(ExpressionBrush));

        static HoleDiagramView()
        {
            FocusableProperty.OverrideMetadata(typeof(HoleDiagramView), new FrameworkPropertyMetadata(false));
        }

        /// <summary>The diagram to draw; null draws nothing.</summary>
        public HoleDiagram? Diagram
        {
            get => (HoleDiagram?)GetValue(DiagramProperty);
            set => SetValue(DiagramProperty, value);
        }

        /// <summary>The hole's outline and the thread.</summary>
        public Brush? OutlineBrush
        {
            get => (Brush?)GetValue(OutlineBrushProperty);
            set => SetValue(OutlineBrushProperty, value);
        }

        /// <summary>The part's edges.</summary>
        public Brush? EdgeBrush
        {
            get => (Brush?)GetValue(EdgeBrushProperty);
            set => SetValue(EdgeBrushProperty, value);
        }

        public Brush? CentreLineBrush
        {
            get => (Brush?)GetValue(CentreLineBrushProperty);
            set => SetValue(CentreLineBrushProperty, value);
        }

        /// <summary>Dimension, extension and leader lines and the arrowheads.</summary>
        public Brush? DimensionBrush
        {
            get => (Brush?)GetValue(DimensionBrushProperty);
            set => SetValue(DimensionBrushProperty, value);
        }

        /// <summary>The section's fill under the hatch lines.</summary>
        public Brush? HatchBrush
        {
            get => (Brush?)GetValue(HatchBrushProperty);
            set => SetValue(HatchBrushProperty, value);
        }

        public Brush? HatchLineBrush
        {
            get => (Brush?)GetValue(HatchLineBrushProperty);
            set => SetValue(HatchLineBrushProperty, value);
        }

        /// <summary>The <c>fx</c> after a value typed as an expression.</summary>
        public Brush? ExpressionBrush
        {
            get => (Brush?)GetValue(ExpressionBrushProperty);
            set => SetValue(ExpressionBrushProperty, value);
        }

        private static DependencyProperty Register<T>(string name) =>
            DependencyProperty.Register(
                name, typeof(T), typeof(HoleDiagramView),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        // The text's brush and font are Control's own properties, which do not redraw by themselves.
        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            if (e.Property == ForegroundProperty || e.Property == FontFamilyProperty || e.Property == FontSizeProperty)
                InvalidateVisual();
        }

        protected override Size MeasureOverride(Size constraint)
        {
            HoleDiagram? diagram = Diagram;
            double width = diagram?.Width ?? 0;
            double height = diagram?.Height ?? 0;
            return new Size(
                double.IsInfinity(constraint.Width) ? width : Math.Min(width, constraint.Width),
                double.IsInfinity(constraint.Height) ? height : Math.Min(height, constraint.Height));
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            HoleDiagram? diagram = Diagram;
            if (diagram == null || diagram.Width <= 0 || diagram.Height <= 0 || ActualWidth <= 0 || ActualHeight <= 0)
                return;

            // The diagram is laid out for its own box; centre it in the control at a scale that fits.
            double scale = Math.Min(ActualWidth / diagram.Width, ActualHeight / diagram.Height);
            double offsetX = (ActualWidth - diagram.Width * scale) / 2;
            double offsetY = (ActualHeight - diagram.Height * scale) / 2;
            Point ToPoint(DiagramPoint p) => new Point(offsetX + p.X * scale, offsetY + p.Y * scale);

            dc.PushClip(new RectangleGeometry(new Rect(0, 0, ActualWidth, ActualHeight)));

            DrawMaterial(dc, diagram, ToPoint);

            Pen? outline = MakePen(OutlineBrush, OutlineThickness);
            Pen? edge = MakePen(EdgeBrush, EdgeThickness);
            Pen? thread = MakePen(OutlineBrush, ThreadThickness);
            Pen? dimension = MakePen(DimensionBrush, DimensionThickness);
            Pen? extension = MakePen(DimensionBrush, ExtensionThickness);
            Pen? centre = MakePen(CentreLineBrush, CentreLineThickness);
            if (centre != null)
            {
                // A long dash, a gap, a short dash, a gap: 9, 3, 2, 3 at the mockup's size, in units of the thickness.
                centre.DashStyle = new DashStyle(new[] { 9 / CentreLineThickness, 3 / CentreLineThickness, 2 / CentreLineThickness, 3 / CentreLineThickness }, 0);
                centre.Freeze();
            }

            foreach (DiagramLine line in diagram.Lines)
            {
                Pen? pen = line.Ink switch
                {
                    DiagramInk.Outline => outline,
                    DiagramInk.Edge => edge,
                    DiagramInk.Thread => thread,
                    DiagramInk.CentreLine => centre,
                    DiagramInk.Extension => extension,
                    _ => dimension,
                };
                if (pen == null || line.Points.Count < 2)
                    continue;

                dc.DrawGeometry(null, pen, Polyline(line.Points, ToPoint, closed: false));
            }

            Brush? arrowBrush = DimensionBrush;
            if (arrowBrush != null)
            {
                foreach (DiagramArrow arrow in diagram.Arrows)
                    dc.DrawGeometry(arrowBrush, null, Arrowhead(arrow, ToPoint, scale));
            }

            DrawLabels(dc, diagram, ToPoint, scale);
            dc.Pop();
        }

        private void DrawMaterial(DrawingContext dc, HoleDiagram diagram, Func<DiagramPoint, Point> toPoint)
        {
            Pen? hatchPen = MakePen(HatchLineBrush, HatchThickness);
            foreach (var polygon in diagram.Material)
            {
                if (polygon.Count < 3)
                    continue;

                StreamGeometry area = Polyline(polygon, toPoint, closed: true);
                dc.DrawGeometry(HatchBrush, null, area);
                if (hatchPen == null)
                    continue;

                // Lines at 45 degrees, every HatchSpacing, across the polygon's bounds and clipped to it.
                Rect bounds = area.Bounds;
                dc.PushClip(area);
                for (double start = bounds.Left - bounds.Height; start < bounds.Right; start += HatchSpacing)
                    dc.DrawLine(hatchPen, new Point(start, bounds.Bottom), new Point(start + bounds.Height, bounds.Top));
                dc.Pop();
            }
        }

        private void DrawLabels(DrawingContext dc, HoleDiagram diagram, Func<DiagramPoint, Point> toPoint, double scale)
        {
            Brush? textBrush = Foreground;
            if (textBrush == null)
                return;

            double pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            var typeface = new Typeface(FontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
            var italic = new Typeface(FontFamily, FontStyles.Italic, FontWeights.Normal, FontStretches.Normal);
            double size = FontSize * scale;

            foreach (DiagramLabel label in diagram.Labels)
            {
                var text = new FormattedText(
                    label.Text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, typeface, size, textBrush, pixelsPerDip);
                FormattedText? fx = null;
                double width = text.WidthIncludingTrailingWhitespace;
                if (label.IsExpression)
                {
                    fx = new FormattedText(
                        ExpressionText, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, italic, size - scale,
                        ExpressionBrush ?? textBrush, pixelsPerDip);
                    width += ExpressionGap * scale + fx.WidthIncludingTrailingWhitespace;
                }

                Point at = toPoint(label.At);
                double left = label.Anchor switch
                {
                    DiagramAnchor.Start => at.X,
                    DiagramAnchor.Middle => at.X - width / 2,
                    _ => at.X - width,
                };
                double top = at.Y - text.Height / 2;
                dc.DrawText(text, new Point(left, top));
                if (fx != null)
                {
                    // On the same baseline as the value.
                    double fxTop = top + text.Baseline - fx.Baseline;
                    dc.DrawText(fx, new Point(left + text.WidthIncludingTrailingWhitespace + ExpressionGap * scale, fxTop));
                }
            }
        }

        private static Pen? MakePen(Brush? brush, double thickness)
        {
            if (brush == null)
                return null;

            var pen = new Pen(brush, thickness)
            {
                LineJoin = PenLineJoin.Round,
                StartLineCap = PenLineCap.Flat,
                EndLineCap = PenLineCap.Flat,
            };
            return pen;
        }

        private static StreamGeometry Polyline(System.Collections.Generic.IReadOnlyList<DiagramPoint> points, Func<DiagramPoint, Point> toPoint, bool closed)
        {
            var geometry = new StreamGeometry();
            using (StreamGeometryContext context = geometry.Open())
            {
                context.BeginFigure(toPoint(points[0]), isFilled: closed, isClosed: closed);
                for (int i = 1; i < points.Count; i++)
                    context.LineTo(toPoint(points[i]), isStroked: true, isSmoothJoin: true);
            }

            geometry.Freeze();
            return geometry;
        }

        private static StreamGeometry Arrowhead(DiagramArrow arrow, Func<DiagramPoint, Point> toPoint, double scale)
        {
            Point tip = toPoint(arrow.Tip);
            double dx = arrow.DirectionX;
            double dy = arrow.DirectionY;
            var back = new Point(tip.X - dx * ArrowLength * scale, tip.Y - dy * ArrowLength * scale);
            var side = new Vector(-dy * ArrowHalfWidth * scale, dx * ArrowHalfWidth * scale);

            var geometry = new StreamGeometry();
            using (StreamGeometryContext context = geometry.Open())
            {
                context.BeginFigure(tip, isFilled: true, isClosed: true);
                context.LineTo(back + side, isStroked: false, isSmoothJoin: false);
                context.LineTo(back - side, isStroked: false, isSmoothJoin: false);
            }

            geometry.Freeze();
            return geometry;
        }
    }
}
