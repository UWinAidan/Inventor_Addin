using System;
using System.Collections.Generic;
using System.Globalization;
using Inventor;
using InventorAddin.Core.Drawing;

// The Drawing folder holds the drawing-side helpers; the namespace names the feature, not the folder (Inventor has types named Drawing*).
namespace InventorAddin.PrintViews
{
    /// <summary>
    /// Places the part marking on a drawing (spec 05, "Added for M5", item 2; task 199). The words, sizes and colour come from Core
    /// (<see cref="PartMarking"/>); this class reads Inventor and applies the result. Calls are those of spike 184, question 3 (round 2):
    /// <c>LeaderNotes.Add</c> with a text point and <c>Sheet.CreateGeometryIntent</c>; <c>DrawingView.Sketches.Add</c>, <c>Edit</c>,
    /// <c>SheetToSketchSpace</c>, <c>SketchLines.AddAsTwoPointRectangle</c>, <c>LineType</c> and <c>OverrideColor</c> on each line,
    /// <c>TextBoxes.AddFitted</c>, <c>ExitEdit</c>. Task 199's own probe confirmed <c>LeaderNote.Color</c> can be set.
    /// </summary>
    /// <remarks>Run it inside the caller's transaction, so one Undo removes the marking.</remarks>
    internal static class PartMarkingWriter
    {
        // The zone text is small so the 40 by 15 mm zone holds it on two lines: the sketch is in model units, so these are centimetres on the part.
        private const double ZoneTextHeightCm = 0.25;
        private const double ZoneTextMarginCm = 0.1;

        // The leader note's characters on the sheet, in centimetres (3 mm).
        private const double LeaderTextHeightCm = 0.3;

        // How far above the view's top-right corner the leader note sits, in sheet centimetres.
        private const double LeaderLiftCm = 1.5;

        /// <summary>The part number field's formatted text: Design Tracking Part Number of the drawing's model (spike 184, round 2).</summary>
        private const string PartNumberField =
            "<Property Document='model' PropertySet='Design Tracking Properties' Property='Part Number' " +
            "FormatID='{32853F0F-3444-11D1-9E93-0060B03C1CA6}' PropertyID='5'>PART NUMBER</Property>";

        /// <summary>
        /// Adds the note with a leader to the picked edge, on the sheet the edge's view is on. The note sits above the view's top-right corner,
        /// in the highlight colour.
        /// </summary>
        public static void AddLeader(DrawingCurve curve, DrawingView view, PartMarkingOptions options, List<string> warnings)
        {
            var sheet = (Sheet)view.Parent;
            TransientGeometry geometry = InventorHost.App.TransientGeometry;
            TransientObjects objects = InventorHost.App.TransientObjects;

            Point2d centre = view.Center;
            Point2d textPoint = geometry.CreatePoint2d(centre.X + view.Width / 2, centre.Y + view.Height / 2 + LeaderLiftCm);

            ObjectCollection points = objects.CreateObjectCollection();
            points.Add(textPoint);
            points.Add(sheet.CreateGeometryIntent(curve, Type.Missing));

            LeaderNote note = sheet.DrawingNotes.LeaderNotes.Add(points, LeaderFormattedText(options), Type.Missing);
            Color colour = CreateColour(options.HighlightColour);
            ComSafe.Run(() => note.Color = colour, warnings, "leader note colour");
        }

        /// <summary>
        /// Adds a dashed 40 by 15 mm rectangle (4 by 1.5 cm in the sketch's model units, spike 184) in a new sketch on <paramref name="view"/>,
        /// centred on it, with the zone text inside and the marking note under it. Lines and text are in the highlight colour. The sketch is
        /// left for the user to drag.
        /// </summary>
        public static void AddZone(DrawingView view, PartMarkingOptions options, List<string> warnings)
        {
            TransientGeometry geometry = InventorHost.App.TransientGeometry;
            Color colour = CreateColour(options.HighlightColour);
            double width = PartMarking.ZoneWidthCm(options.Units);
            double height = PartMarking.ZoneHeightCm(options.Units);

            DrawingSketch sketch = view.Sketches.Add();
            sketch.Edit();
            try
            {
                Point2d centre = sketch.SheetToSketchSpace(view.Center);
                double left = centre.X - width / 2;
                double top = centre.Y + height / 2;

                SketchEntitiesEnumerator lines = sketch.SketchLines.AddAsTwoPointRectangle(
                    geometry.CreatePoint2d(left, centre.Y - height / 2), geometry.CreatePoint2d(left + width, top));
                foreach (object item in lines)
                {
                    var line = (SketchLine)item;
                    ComSafe.Run(() =>
                    {
                        line.LineType = LineTypeEnum.kDashedLineType;
                        line.OverrideColor = colour;
                    }, warnings, "zone line style");
                }

                AddText(
                    sketch, geometry, colour, warnings, NotesText.Escape(PartMarking.ZoneText),
                    left + ZoneTextMarginCm, top - ZoneTextMarginCm, width - 2 * ZoneTextMarginCm, "zone text");

                // The marking itself (method and extra lines) goes under the zone, in the same sketch, so it moves with it.
                string note = NoteFormattedText(options);
                if (note.Length > 0)
                {
                    AddText(
                        sketch, geometry, colour, warnings, note,
                        left, centre.Y - height / 2 - ZoneTextMarginCm, width * 1.5, "marking note");
                }
            }
            finally
            {
                ComSafe.Run(() => sketch.ExitEdit(), warnings, "leaving the zone sketch");
            }
        }

        private static void AddText(
            DrawingSketch sketch, TransientGeometry geometry, Color colour, List<string> warnings, string escapedContent,
            double x, double y, double widthCm, string what)
        {
            string formatted = $"<StyleOverride FontSize='{ZoneTextHeightCm.ToString("0.####", CultureInfo.InvariantCulture)}'>{escapedContent}</StyleOverride>";

            Inventor.TextBox box = sketch.TextBoxes.AddFitted(geometry.CreatePoint2d(x, y), formatted, Type.Missing);
            ComSafe.Run(() =>
            {
                // Upper left, so the text starts at the point and hangs down from it (as the title block texts do).
                box.HorizontalJustification = HorizontalTextAlignmentEnum.kAlignTextLeft;
                box.VerticalJustification = VerticalTextAlignmentEnum.kAlignTextUpper;
                box.Width = widthCm;
                box.Color = colour;
            }, warnings, what);
        }

        /// <summary>The leader note's text: the note lines, with the model's part number on its own line after the method line.</summary>
        private static string LeaderFormattedText(PartMarkingOptions options)
        {
            var parts = new List<string>();
            foreach (LeaderNoteLine line in PartMarking.LeaderNoteLines(options))
                parts.Add(line.IsPartNumber ? PartNumberField : NotesText.Escape(line.Text));

            string joined = string.Join("<Br/>", parts);
            return $"<StyleOverride FontSize='{LeaderTextHeightCm.ToString("0.####", CultureInfo.InvariantCulture)}'>{joined}</StyleOverride>";
        }

        private static string NoteFormattedText(PartMarkingOptions options)
        {
            var parts = new List<string>();
            foreach (string line in PartMarking.NoteLines(options))
                parts.Add(NotesText.Escape(line));
            return string.Join("<Br/>", parts);
        }

        private static Color CreateColour(string hex)
        {
            (byte r, byte g, byte b) = PartMarking.ColourRgb(hex);
            return InventorHost.App.TransientObjects.CreateColor(r, g, b, 1.0);
        }
    }
}
