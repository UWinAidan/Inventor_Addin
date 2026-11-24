using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using InventorAddin.Core.Drawing;

namespace InventorAddin.UI.Controls
{
    /// <summary>
    /// Draws an <see cref="InitialViewsSheetPreview"/> (task 234): the sheet, its border, the title block band and the planned view boxes,
    /// scaled to fit the control. Every position and text comes from Core; this only draws them. The brushes are the implicit
    /// <c>SheetPreviewView</c> style in <c>UI/Theme/Styles.xaml</c>, from the theme's colours.
    /// </summary>
    public sealed class SheetPreviewView : Control
    {
        private const double Inset = 6;

        public static readonly DependencyProperty PreviewProperty = Register<InitialViewsSheetPreview>(nameof(Preview));
        public static readonly DependencyProperty SheetBrushProperty = Register<Brush>(nameof(SheetBrush));
        public static readonly DependencyProperty LineBrushProperty = Register<Brush>(nameof(LineBrush));
        public static readonly DependencyProperty TitleBlockBrushProperty = Register<Brush>(nameof(TitleBlockBrush));
        public static readonly DependencyProperty ViewBrushProperty = Register<Brush>(nameof(ViewBrush));
        public static readonly DependencyProperty ViewLineBrushProperty = Register<Brush>(nameof(ViewLineBrush));
        public static readonly DependencyProperty ErrorBrushProperty = Register<Brush>(nameof(ErrorBrush));

        static SheetPreviewView()
        {
            FocusableProperty.OverrideMetadata(typeof(SheetPreviewView), new FrameworkPropertyMetadata(false));
        }

        /// <summary>What to draw; null draws nothing.</summary>
        public InitialViewsSheetPreview? Preview
        {
            get => (InitialViewsSheetPreview?)GetValue(PreviewProperty);
            set => SetValue(PreviewProperty, value);
        }

        /// <summary>The sheet's fill.</summary>
        public Brush? SheetBrush
        {
            get => (Brush?)GetValue(SheetBrushProperty);
            set => SetValue(SheetBrushProperty, value);
        }

        /// <summary>The sheet's edge, the border and the title block's outline.</summary>
        public Brush? LineBrush
        {
            get => (Brush?)GetValue(LineBrushProperty);
            set => SetValue(LineBrushProperty, value);
        }

        /// <summary>The title block's fill.</summary>
        public Brush? TitleBlockBrush
        {
            get => (Brush?)GetValue(TitleBlockBrushProperty);
            set => SetValue(TitleBlockBrushProperty, value);
        }

        /// <summary>The view boxes' fill.</summary>
        public Brush? ViewBrush
        {
            get => (Brush?)GetValue(ViewBrushProperty);
            set => SetValue(ViewBrushProperty, value);
        }

        /// <summary>The view boxes' outline.</summary>
        public Brush? ViewLineBrush
        {
            get => (Brush?)GetValue(ViewLineBrushProperty);
            set => SetValue(ViewLineBrushProperty, value);
        }

        /// <summary>The view boxes' outline when the views do not fit.</summary>
        public Brush? ErrorBrush
        {
            get => (Brush?)GetValue(ErrorBrushProperty);
            set => SetValue(ErrorBrushProperty, value);
        }

        private static DependencyProperty Register<T>(string name) =>
            DependencyProperty.Register(
                name, typeof(T), typeof(SheetPreviewView),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            if (e.Property == ForegroundProperty || e.Property == FontFamilyProperty || e.Property == FontSizeProperty)
                InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            InitialViewsSheetPreview? preview = Preview;
            if (preview == null || preview.SheetWidthMm <= 0 || preview.SheetHeightMm <= 0 || ActualWidth <= 2 * Inset || ActualHeight <= 2 * Inset)
                return;

            double scale = Math.Min((ActualWidth - 2 * Inset) / preview.SheetWidthMm, (ActualHeight - 2 * Inset) / preview.SheetHeightMm);
            double offsetX = (ActualWidth - preview.SheetWidthMm * scale) / 2;
            double offsetY = (ActualHeight - preview.SheetHeightMm * scale) / 2;

            // Sheet millimetres, y up, to control pixels, y down.
            Rect ToRect(PreviewRectMm r) => new Rect(
                offsetX + r.LeftMm * scale,
                offsetY + (preview.SheetHeightMm - r.TopMm) * scale,
                Math.Max(0, r.WidthMm * scale),
                Math.Max(0, r.HeightMm * scale));

            Pen? line = LineBrush == null ? null : new Pen(LineBrush, 1);
            Pen? thin = LineBrush == null ? null : new Pen(LineBrush, 0.75);
            Brush? viewOutline = preview.Fits ? ViewLineBrush : (ErrorBrush ?? ViewLineBrush);
            Pen? viewPen = viewOutline == null ? null : new Pen(viewOutline, 1);

            dc.DrawRectangle(SheetBrush, line, new Rect(offsetX, offsetY, preview.SheetWidthMm * scale, preview.SheetHeightMm * scale));
            dc.DrawRectangle(null, thin, ToRect(preview.Border));
            dc.DrawRectangle(TitleBlockBrush, thin, ToRect(preview.TitleBlock));
            DrawCentred(dc, "Title block", ToRect(preview.TitleBlock), FontSize - 1);

            foreach (PreviewView view in preview.Views)
            {
                Rect box = ToRect(view.Rect);
                dc.DrawRectangle(ViewBrush, viewPen, box);
                DrawCentred(dc, view.Label, box, FontSize);
            }

            // The scale sits in the strip left of the title block, inside the border, where no view is drawn.
            FormattedText? scaleText = Format(preview.ScaleText, FontSize);
            if (scaleText != null)
            {
                Rect band = ToRect(preview.TitleBlock);
                Rect border = ToRect(preview.Border);
                if (band.Left - border.Left >= scaleText.Width + 6)
                    dc.DrawText(scaleText, new Point(border.Left + 3, band.Bottom - scaleText.Height - 2));
            }
        }

        private void DrawCentred(DrawingContext dc, string text, Rect box, double size)
        {
            FormattedText? t = Format(text, size);
            if (t == null || t.Width > box.Width || t.Height > box.Height)
                return;
            dc.DrawText(t, new Point(box.Left + (box.Width - t.Width) / 2, box.Top + (box.Height - t.Height) / 2));
        }

        private FormattedText? Format(string text, double size)
        {
            if (Foreground == null || string.IsNullOrEmpty(text))
                return null;
            var typeface = new Typeface(FontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
            return new FormattedText(
                text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, typeface, Math.Max(6, size), Foreground,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);
        }
    }
}
