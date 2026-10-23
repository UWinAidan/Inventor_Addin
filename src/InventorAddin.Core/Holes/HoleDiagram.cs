using System.Globalization;

namespace InventorAddin.Core.Holes;

/// <summary>
/// The values the AWB Hole window's diagram is drawn from (task 104; mockup choices 3 and 12), in the window's units:
/// lengths in mm or inches, angles in degrees. A value that is not known (an empty box, a size not in the workbook) is
/// null: the diagram still draws the hole in proportion and leaves that dimension out.
/// </summary>
public sealed record HoleDiagramInput
{
    public HoleUnits Units { get; init; }

    public HoleKind Kind { get; init; }

    public HoleSeat Seat { get; init; }

    public HoleTermination Termination { get; init; } = HoleTermination.ThroughAll;

    /// <summary>The hole diameter as modelled: the clearance diameter, the tap drill, or the dowel's nominal.</summary>
    public decimal? HoleDiameter { get; init; }

    /// <summary>A dowel's tolerance after the diameter, for example <c>H7</c>; null for none.</summary>
    public string? Tolerance { get; init; }

    /// <summary>A blind hole's depth (the drill depth when tapped); ignored when the hole is not blind.</summary>
    public decimal? Depth { get; init; }

    /// <summary>The depth was typed as an Inventor expression: its value is marked <c>fx</c>.</summary>
    public bool DepthIsExpression { get; init; }

    /// <summary>A blind hole's drill point angle; null for a flat bottom.</summary>
    public decimal? PointAngleDeg { get; init; }

    public decimal? CounterboreDiameter { get; init; }

    public decimal? CounterboreDepth { get; init; }

    public bool CounterboreDepthIsExpression { get; init; }

    public decimal? CountersinkDiameter { get; init; }

    public decimal? CountersinkAngleDeg { get; init; }

    /// <summary>A tapped hole's thread as Inventor names it, for example <c>M6x1</c>.</summary>
    public string? ThreadName { get; init; }

    /// <summary>A tapped hole's thread (major) diameter.</summary>
    public decimal? ThreadDiameter { get; init; }

    /// <summary>A blind tapped hole's thread depth; a hole that is not blind is threaded its full depth.</summary>
    public decimal? ThreadDepth { get; init; }

    public bool ThreadDepthIsExpression { get; init; }

    public bool IsBlind => Termination == HoleTermination.Distance;
}

/// <summary>What a diagram line is: the control picks its brush and stroke from this.</summary>
public enum DiagramInk
{
    /// <summary>The hole's outline: the thickest line.</summary>
    Outline,

    /// <summary>The part's own edges around the hole.</summary>
    Edge,

    /// <summary>A tapped hole's thread, at the thread's diameter.</summary>
    Thread,

    /// <summary>The hole's centre line, dashed.</summary>
    CentreLine,

    /// <summary>Dimension and leader lines.</summary>
    Dimension,

    /// <summary>Extension lines from the outline to a dimension line.</summary>
    Extension,
}

/// <summary>Where a label's <see cref="DiagramLabel.At"/> point is on its text.</summary>
public enum DiagramAnchor
{
    Start,
    Middle,
    End,
}

/// <summary>A point in the diagram, in device-independent units from its top left corner.</summary>
public readonly record struct DiagramPoint(double X, double Y);

/// <summary>An open polyline.</summary>
public sealed record DiagramLine(DiagramInk Ink, IReadOnlyList<DiagramPoint> Points);

/// <summary>An arrowhead with its tip at <see cref="Tip"/>, pointing along the unit direction.</summary>
public sealed record DiagramArrow(DiagramPoint Tip, double DirectionX, double DirectionY);

/// <summary>
/// A dimension's text. <see cref="At"/> is on the text's vertical centre, at its start, middle or end. A value typed
/// as an expression has <see cref="IsExpression"/>: the control draws a small italic <c>fx</c> after the text.
/// </summary>
public sealed record DiagramLabel(string Text, DiagramPoint At, DiagramAnchor Anchor, bool IsExpression);

/// <summary>
/// The AWB Hole window's diagram of the hole (task 104): the part in section, the hole outline with its seat, thread
/// and bottom, the centre line, and dimension lines with their values, laid out in a box of <see cref="Width"/> by
/// <see cref="Height"/>. Everything is decided here, which dimensions a hole gets and where their text goes; the
/// add-in only draws the polygons, lines, arrowheads and labels, in the theme's colours.
/// </summary>
/// <param name="Material">The part's section, hatched: one polygon for a blind hole, two for a hole through it.</param>
public sealed record HoleDiagram(
    double Width,
    double Height,
    IReadOnlyList<IReadOnlyList<DiagramPoint>> Material,
    IReadOnlyList<DiagramLine> Lines,
    IReadOnlyList<DiagramArrow> Arrows,
    IReadOnlyList<DiagramLabel> Labels)
{
    /// <summary>The drawing's size in the window: the full width of the content inside its border, and 134 tall.</summary>
    public const double DefaultWidth = 314;

    public const double DefaultHeight = 134;

    public const string DiameterSign = "⌀";
    public const string DegreeSign = "°";

    /// <summary>What the collapsed summary shows for a value that is not known.</summary>
    public const string UnknownText = "?";

    /// <summary>
    /// The width a label is laid out for: Segoe UI 11 averages about 6.4 a character for these texts, and the
    /// <c>fx</c> after an expression's value takes about 14.
    /// </summary>
    public static double EstimateTextWidth(string text, bool isExpression) =>
        (text ?? "").Length * 6.4 + (isExpression ? 14 : 0);

    /// <summary>The diagram of <paramref name="input"/> in a box of the given size.</summary>
    public static HoleDiagram Draw(HoleDiagramInput input, double width = DefaultWidth, double height = DefaultHeight)
    {
        ArgumentNullException.ThrowIfNull(input);
        return new Layout(input, width, height).Build();
    }

    /// <summary>
    /// The one-line summary the collapsed diagram shows (mockup choice 12), for example
    /// <c>⌀6 H7, 31 deep, counterbore ⌀11 by 6.5</c>. A value not known shows as <see cref="UnknownText"/>.
    /// </summary>
    public static string Summary(HoleDiagramInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var units = input.Units;
        string Len(decimal? value) => value is { } v ? FormatLength(v, units) : UnknownText;

        var parts = new List<string>();
        if (input.Kind == HoleKind.Tapped)
        {
            parts.Add(Clean(input.ThreadName) ?? UnknownText);
        }
        else
        {
            var tolerance = Clean(input.Tolerance);
            parts.Add(DiameterSign + Len(input.HoleDiameter) + (tolerance is null ? "" : " " + tolerance));
        }

        parts.Add(input.Termination switch
        {
            HoleTermination.Distance when input.Kind == HoleKind.Tapped =>
                $"thread {Len(input.ThreadDepth)}, drill {Len(input.Depth)}",
            HoleTermination.Distance => $"{Len(input.Depth)} deep",
            HoleTermination.ToFace => "to a face",
            _ => "through all",
        });

        if (input.Seat == HoleSeat.Counterbore)
        {
            parts.Add($"counterbore {DiameterSign}{Len(input.CounterboreDiameter)} by {Len(input.CounterboreDepth)}");
        }
        else if (input.Seat == HoleSeat.Countersink)
        {
            var angle = input.CountersinkAngleDeg is { } a ? FormatAngle(a) : UnknownText;
            parts.Add($"countersink {DiameterSign}{Len(input.CountersinkDiameter)} at {angle}");
        }

        return string.Join(", ", parts);
    }

    /// <summary>A length as the diagram shows it: mm to 3 decimals, inches to 4, no trailing zeros, in the user's format.</summary>
    public static string FormatLength(decimal value, HoleUnits units) =>
        Math.Round(value, units == HoleUnits.Imperial ? 4 : 3, MidpointRounding.AwayFromZero)
            .ToString(units == HoleUnits.Imperial ? "0.####" : "0.###", CultureInfo.CurrentCulture);

    /// <summary>An angle as the diagram shows it: up to 2 decimals and the degree sign, <c>118°</c>.</summary>
    public static string FormatAngle(decimal degrees) =>
        Math.Round(degrees, 2, MidpointRounding.AwayFromZero).ToString("0.##", CultureInfo.CurrentCulture) + DegreeSign;

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private static double? Positive(decimal? value) => value is { } v && v > 0 ? (double)v : null;

    /// <summary>
    /// Works the hole out in its own units first (the outline from the top face down, the part around it), then fits
    /// it with its dimensions into the box. Dimensions that share a side are stacked outwards, each line clear of the
    /// text of the one inside it.
    /// </summary>
    private sealed class Layout(HoleDiagramInput input, double width, double height)
    {
        // Gaps in the box, as the mockup draws them.
        private const double FirstDimensionGap = 12;
        private const double TextGap = 5;
        private const double StackGap = 10;
        private const double SeatDimensionRise = 13;
        private const double TopWithSeatDimension = 28;
        private const double TopWithThreadName = 20;
        private const double TopPlain = 14;
        private const double Bottom = 6;
        private const double Side = 6;
        private const double PointArcRadius = 10;
        private const double PointLabelOffset = 28;

        private readonly List<IReadOnlyList<DiagramPoint>> _material = [];
        private readonly List<DiagramLine> _lines = [];
        private readonly List<DiagramArrow> _arrows = [];
        private readonly List<DiagramLabel> _labels = [];

        // The hole in its own units (mm or inches).
        private double _r;
        private double _seatR;
        private double _seatBottom;
        private double _cboreDepth;
        private double _csinkHeight;
        private double _threadR;
        private double _threadEnd;
        private double _depth;
        private double _tip;
        private double _block;
        private double _blockDepth;
        private double? _pointHalfAngleRad;

        // The fit into the box.
        private double _scale;
        private double _cx;
        private double _top;

        private bool Blind => input.IsBlind;

        private bool Tapped => input.Kind == HoleKind.Tapped;

        private double X(double value) => _cx + value * _scale;

        private double Y(double value) => _top + value * _scale;

        private DiagramPoint P(double x, double y) => new(X(x), Y(y));

        public HoleDiagram Build()
        {
            Resolve();

            // The dimensions on each side, inner first.
            var left = new List<StackItem>();
            var right = new List<StackItem>();
            if (Blind && Positive(input.Depth) is not null)
            {
                left.Add(StackItem.Dimension(
                    0, _depth, -_seatR, -_r, Len(input.Depth!.Value), input.DepthIsExpression));
            }

            if (input.Seat == HoleSeat.Countersink && input.CountersinkAngleDeg is { } csinkAngle && csinkAngle > 0)
            {
                right.Add(StackItem.Leader((_seatR + _r) / 2, _csinkHeight / 2, FormatAngle(csinkAngle)));
            }

            var rightDimensions = new List<StackItem>();
            if (input.Seat == HoleSeat.Counterbore && Positive(input.CounterboreDepth) is not null)
            {
                rightDimensions.Add(StackItem.Dimension(
                    0, _cboreDepth, _seatR, _seatR, Len(input.CounterboreDepth!.Value), input.CounterboreDepthIsExpression));
            }

            if (Tapped && Blind && Positive(input.ThreadDepth) is not null)
            {
                rightDimensions.Add(StackItem.Dimension(
                    0, _threadEnd, Math.Max(_seatR, _threadR), _threadR, Len(input.ThreadDepth!.Value),
                    input.ThreadDepthIsExpression));
            }

            right.AddRange(rightDimensions.OrderBy(d => d.Y2 - d.Y1));
            Stack(left);
            Stack(right);

            // The hole diameter: across the hole below the right-hand dimensions when there is room, its text out to
            // the right of the part.
            var wallEnd = Blind ? _depth : _blockDepth;
            var lowStart = right.Select(i => i.Y2).Append(_seatBottom).Max();
            double diaY;
            double diaOffset;
            if (wallEnd - lowStart >= 0.2 * (wallEnd - _seatBottom))
            {
                diaY = (lowStart + wallEnd) / 2;
                diaOffset = FirstDimensionGap;
            }
            else
            {
                diaY = (_seatBottom + wallEnd) / 2;
                diaOffset = Extent(right) + 4;
            }

            string? diaText = null;
            if (Positive(input.HoleDiameter) is not null)
            {
                var tolerance = Clean(input.Tolerance);
                diaText = DiameterSign + Len(input.HoleDiameter!.Value) + (tolerance is null ? "" : " " + tolerance);
            }

            var threadName = Tapped ? Clean(input.ThreadName) : null;
            var seatDiameter = input.Seat switch
            {
                HoleSeat.Counterbore => Positive(input.CounterboreDiameter) is not null ? input.CounterboreDiameter : null,
                HoleSeat.Countersink => Positive(input.CountersinkDiameter) is not null ? input.CountersinkDiameter : null,
                _ => null,
            };
            var pointText = Blind && input.PointAngleDeg is { } point && point > 0 ? FormatAngle(point) : null;

            // The room each side needs past the part, and above it.
            var leftNeed = Math.Max(Extent(left), Side);
            if (threadName is not null)
            {
                leftNeed = Math.Max(leftNeed, 14 + EstimateTextWidth(threadName, false) + 2);
            }

            var rightNeed = Math.Max(Extent(right), Side);
            if (diaText is not null)
            {
                rightNeed = Math.Max(rightNeed, diaOffset + 4 + EstimateTextWidth(diaText, false) + 2);
            }

            var top = seatDiameter is not null ? TopWithSeatDimension
                : threadName is not null ? TopWithThreadName
                : TopPlain;
            Fit(leftNeed, rightNeed, top, pointText);

            DrawPart();
            DrawThread();
            DrawCentreLine(top);
            DrawStack(left, toLeft: true);
            DrawStack(right, toLeft: false);
            if (seatDiameter is { } seatDia)
            {
                DrawSeatDiameter(DiameterSign + Len(seatDia));
            }

            if (diaText is not null)
            {
                DrawHoleDiameter(diaY, diaOffset, diaText);
            }

            if (threadName is not null)
            {
                DrawThreadName(threadName);
            }

            if (pointText is not null)
            {
                DrawPointAngle(pointText);
            }

            return new HoleDiagram(width, height, _material, _lines, _arrows, _labels);
        }

        private string Len(decimal value) => FormatLength(value, input.Units);

        /// <summary>
        /// The hole's sizes in its own units. A value that is not known, or that Inventor would refuse (a seat narrower
        /// than the hole), is drawn in proportion instead, so the picture always reads.
        /// </summary>
        private void Resolve()
        {
            var diameter = Positive(input.HoleDiameter)
                ?? (Positive(input.ThreadDiameter) * 0.85)
                ?? (Positive(input.CounterboreDiameter) * 0.55)
                ?? (Positive(input.CountersinkDiameter) * 0.55)
                ?? 1.0;
            _r = diameter / 2;

            _seatR = _r;
            _seatBottom = 0;
            if (input.Seat == HoleSeat.Counterbore)
            {
                _seatR = Math.Max((Positive(input.CounterboreDiameter) ?? diameter * 1.8) / 2, _r * 1.15);
                _cboreDepth = Positive(input.CounterboreDepth) ?? diameter * 0.6;
                _seatBottom = _cboreDepth;
            }
            else if (input.Seat == HoleSeat.Countersink)
            {
                _seatR = Math.Max((Positive(input.CountersinkDiameter) ?? diameter * 1.8) / 2, _r * 1.15);
                var angle = Positive(input.CountersinkAngleDeg) is { } a && a < 180 ? a : 90;
                _csinkHeight = (_seatR - _r) / Math.Tan(angle * Math.PI / 360);
                _seatBottom = _csinkHeight;
            }

            _threadR = Tapped ? Math.Max((Positive(input.ThreadDiameter) ?? diameter * 1.18) / 2, _r * 1.05) : _r;

            if (Blind)
            {
                var threadDepth = Positive(input.ThreadDepth) ?? diameter * 1.8;
                _depth = Positive(input.Depth) ?? (Tapped ? threadDepth + diameter * 0.6 : diameter * 2);

                // A seat as deep as the hole is drawn a little shallower, so the hole still shows below it.
                if (_seatBottom >= _depth * 0.9)
                {
                    var scaleDown = _depth * 0.6 / _seatBottom;
                    _cboreDepth *= scaleDown;
                    _csinkHeight *= scaleDown;
                    _seatBottom *= scaleDown;
                }

                _threadEnd = Math.Clamp(threadDepth, _seatBottom, _depth);
                if (Positive(input.PointAngleDeg) is { } pointAngle && pointAngle < 180)
                {
                    _pointHalfAngleRad = pointAngle * Math.PI / 360;
                    _tip = _depth + _r / Math.Tan(_pointHalfAngleRad.Value);
                }
                else
                {
                    _tip = _depth;
                }

                _blockDepth = _tip + Math.Max(0.15 * _tip, 0.5 * diameter);
            }
            else
            {
                _blockDepth = Math.Max(_seatBottom + 2.2 * diameter, 3 * diameter);
                _threadEnd = _blockDepth;
                _tip = _blockDepth;
            }

            var widest = Math.Max(_seatR, _threadR);
            _block = widest + Math.Max(0.9 * widest, 0.9 * diameter);
        }

        /// <summary>The scale and the place of the hole's centre and top face, to fit the box with its dimensions.</summary>
        private void Fit(double leftNeed, double rightNeed, double top, string? pointText)
        {
            var rightRoom = rightNeed;
            for (var pass = 0; pass < 3; pass++)
            {
                var byWidth = (width - leftNeed - rightRoom) / (2 * _block);
                var byHeight = (height - top - Bottom) / _blockDepth;
                _scale = Math.Max(0.01, Math.Min(byWidth, byHeight));

                // The drill point's angle is written to the right of the tip: keep it inside the box.
                if (pointText is null)
                {
                    break;
                }

                var pointNeed = PointLabelOffset + EstimateTextWidth(pointText, false) + 2 - _block * _scale;
                if (pointNeed <= rightRoom + 0.5)
                {
                    break;
                }

                rightRoom = pointNeed;
            }

            var contentWidth = leftNeed + 2 * _block * _scale + rightRoom;
            _cx = Math.Max(0, (width - contentWidth) / 2) + leftNeed + _block * _scale;
            var spare = Math.Max(0, height - top - Bottom - _blockDepth * _scale);
            _top = top + spare / 2;
        }

        /// <summary>The right half of the hole's outline, from the top face down; the left half is its mirror.</summary>
        private List<(double X, double Y)> Profile()
        {
            var points = new List<(double X, double Y)>();
            if (input.Seat == HoleSeat.Counterbore)
            {
                points.Add((_seatR, 0));
                points.Add((_seatR, _cboreDepth));
                points.Add((_r, _cboreDepth));
            }
            else if (input.Seat == HoleSeat.Countersink)
            {
                points.Add((_seatR, 0));
                points.Add((_r, _csinkHeight));
            }
            else
            {
                points.Add((_r, 0));
            }

            if (!Blind)
            {
                points.Add((_r, _blockDepth));
            }
            else
            {
                points.Add((_r, _depth));
                points.Add((0, _tip));
            }

            return points;
        }

        private void DrawPart()
        {
            var right = Profile();
            var left = right.Select(p => (X: -p.X, p.Y)).ToList();
            var b = _block;
            var t = _blockDepth;

            if (!Blind)
            {
                _material.Add(Points([(-b, 0), .. left, (-b, t)]));
                _material.Add(Points([(b, 0), .. right, (b, t)]));
                _lines.Add(new DiagramLine(DiagramInk.Outline, Points(left)));
                _lines.Add(new DiagramLine(DiagramInk.Outline, Points(right)));
                Line(DiagramInk.Edge, (-b, 0), left[0]);
                Line(DiagramInk.Edge, right[0], (b, 0));
                Line(DiagramInk.Edge, (-b, t), (-_r, t));
                Line(DiagramInk.Edge, (_r, t), (b, t));
                Line(DiagramInk.Edge, (-b, 0), (-b, t));
                Line(DiagramInk.Edge, (b, 0), (b, t));
                return;
            }

            // Blind: one section, the outline down the left wall, across the bottom and up the right wall.
            var outline = left.Concat(Enumerable.Reverse(right.Take(right.Count - 1))).ToList();
            _material.Add(Points([(-b, 0), .. outline, (b, 0), (b, t), (-b, t)]));
            _lines.Add(new DiagramLine(DiagramInk.Outline, Points(outline)));
            Line(DiagramInk.Edge, (-b, 0), outline[0]);
            Line(DiagramInk.Edge, outline[^1], (b, 0), (b, t), (-b, t), (-b, 0));
        }

        private void DrawThread()
        {
            if (!Tapped)
            {
                return;
            }

            var from = _seatBottom;
            var to = _threadEnd;
            Line(DiagramInk.Thread, (-_threadR, from), (-_threadR, to));
            Line(DiagramInk.Thread, (_threadR, from), (_threadR, to));
            if (Blind)
            {
                Line(DiagramInk.Thread, (-_threadR, to), (-_r, to));
                Line(DiagramInk.Thread, (_threadR, to), (_r, to));
            }
        }

        private void DrawCentreLine(double top)
        {
            var above = Math.Min(16, Math.Max(0, Y(0) - 2));
            var bottom = Y(Blind ? _tip : _blockDepth);
            var below = Math.Min(8, Math.Max(0, height - bottom - 1));
            _lines.Add(new DiagramLine(
                DiagramInk.CentreLine, [new DiagramPoint(_cx, Y(0) - Math.Min(above, top - 2)), new DiagramPoint(_cx, bottom + below)]));
        }

        /// <summary>Gives each item on a side its line's distance from the part: the first 12 out, each next one past the text before it.</summary>
        private static void Stack(List<StackItem> items)
        {
            var offset = FirstDimensionGap;
            foreach (var item in items)
            {
                item.Offset = offset;
                offset += TextGap + item.TextWidth + StackGap;
            }
        }

        /// <summary>How far past the part a side's dimensions reach, their text included.</summary>
        private static double Extent(List<StackItem> items) =>
            items.Count == 0 ? 0 : items[^1].Offset + TextGap + items[^1].TextWidth + 2;

        private void DrawStack(List<StackItem> items, bool toLeft)
        {
            var sign = toLeft ? -1 : 1;
            var edge = X(sign * _block);
            foreach (var item in items)
            {
                var lineX = edge + sign * item.Offset;
                if (item.IsLeader)
                {
                    var y = Y(item.Y1);
                    _lines.Add(new DiagramLine(DiagramInk.Dimension, [P(item.FromX1, item.Y1), new DiagramPoint(lineX, y)]));
                    _labels.Add(new DiagramLabel(
                        item.Text, new DiagramPoint(lineX + sign * 4, y), toLeft ? DiagramAnchor.End : DiagramAnchor.Start, false));
                    continue;
                }

                var y1 = Y(item.Y1);
                var y2 = Y(item.Y2);
                Extension(item.FromX1, y1, lineX, sign);
                Extension(item.FromX2, y2, lineX, sign);
                _lines.Add(new DiagramLine(DiagramInk.Dimension, [new DiagramPoint(lineX, y1), new DiagramPoint(lineX, y2)]));
                _arrows.Add(new DiagramArrow(new DiagramPoint(lineX, y1), 0, -1));
                _arrows.Add(new DiagramArrow(new DiagramPoint(lineX, y2), 0, 1));
                _labels.Add(new DiagramLabel(
                    item.Text,
                    new DiagramPoint(lineX + sign * TextGap, (y1 + y2) / 2),
                    toLeft ? DiagramAnchor.End : DiagramAnchor.Start,
                    item.IsExpression));
            }
        }

        private void Extension(double fromX, double y, double lineX, int sign) =>
            _lines.Add(new DiagramLine(
                DiagramInk.Extension, [new DiagramPoint(X(fromX) + sign * 2, y), new DiagramPoint(lineX + sign * 4, y)]));

        /// <summary>The seat's diameter above the top face, its text over the middle.</summary>
        private void DrawSeatDiameter(string text)
        {
            var y = Y(0) - SeatDimensionRise;
            var x1 = X(-_seatR);
            var x2 = X(_seatR);
            _lines.Add(new DiagramLine(DiagramInk.Extension, [new DiagramPoint(x1, Y(0) - 3), new DiagramPoint(x1, y - 4)]));
            _lines.Add(new DiagramLine(DiagramInk.Extension, [new DiagramPoint(x2, Y(0) - 3), new DiagramPoint(x2, y - 4)]));
            _lines.Add(new DiagramLine(DiagramInk.Dimension, [new DiagramPoint(x1, y), new DiagramPoint(x2, y)]));
            _arrows.Add(new DiagramArrow(new DiagramPoint(x1, y), -1, 0));
            _arrows.Add(new DiagramArrow(new DiagramPoint(x2, y), 1, 0));
            _labels.Add(new DiagramLabel(text, new DiagramPoint(_cx, y - 8), DiagramAnchor.Middle, false));
        }

        /// <summary>The hole's diameter across the hole, with a leader out past the part to its text.</summary>
        private void DrawHoleDiameter(double depth, double offset, string text)
        {
            var y = Y(depth);
            var outX = X(_block) + offset;
            _lines.Add(new DiagramLine(DiagramInk.Dimension, [P(-_r, depth), P(_r, depth)]));
            _arrows.Add(new DiagramArrow(P(-_r, depth), -1, 0));
            _arrows.Add(new DiagramArrow(P(_r, depth), 1, 0));
            _lines.Add(new DiagramLine(DiagramInk.Dimension, [P(_r, depth), new DiagramPoint(outX, y)]));
            _labels.Add(new DiagramLabel(text, new DiagramPoint(outX + 4, y), DiagramAnchor.Start, false));
        }

        /// <summary>The thread's name, up and out to the left from the thread line, above the top face.</summary>
        private void DrawThreadName(string text)
        {
            var from = P(-_threadR, _seatBottom + 0.3 * (_threadEnd - _seatBottom));
            var y = Y(0) - 10;
            var bend = X(-_block) - 6;
            _lines.Add(new DiagramLine(
                DiagramInk.Dimension, [from, new DiagramPoint(bend, y), new DiagramPoint(bend - 6, y)]));
            _labels.Add(new DiagramLabel(text, new DiagramPoint(bend - 8, y), DiagramAnchor.End, false));
        }

        /// <summary>An arc across the drill point's cone at the tip, and its angle to the right.</summary>
        private void DrawPointAngle(string text)
        {
            var x0 = _cx;
            var y0 = Y(_tip);
            if (_pointHalfAngleRad is { } half)
            {
                var arc = new List<DiagramPoint>();
                const int Steps = 12;
                for (var i = 0; i <= Steps; i++)
                {
                    var angle = -half + 2 * half * i / Steps;
                    arc.Add(new DiagramPoint(x0 + PointArcRadius * Math.Sin(angle), y0 - PointArcRadius * Math.Cos(angle)));
                }

                _lines.Add(new DiagramLine(DiagramInk.Dimension, arc));
            }

            _lines.Add(new DiagramLine(
                DiagramInk.Dimension,
                [new DiagramPoint(x0 + 3, y0 - 5), new DiagramPoint(x0 + 22, y0 - 1), new DiagramPoint(x0 + 26, y0 - 1)]));
            _labels.Add(new DiagramLabel(text, new DiagramPoint(x0 + PointLabelOffset, y0 - 1), DiagramAnchor.Start, false));
        }

        private void Line(DiagramInk ink, params (double X, double Y)[] points) =>
            _lines.Add(new DiagramLine(ink, Points(points)));

        private List<DiagramPoint> Points(IEnumerable<(double X, double Y)> points) =>
            points.Select(p => P(p.X, p.Y)).ToList();

        /// <summary>
        /// A dimension or leader on one side of the part. A dimension runs from <see cref="Y1"/> to <see cref="Y2"/>
        /// with extension lines from the outline at <see cref="FromX1"/> and <see cref="FromX2"/>; a leader runs from
        /// the point (<see cref="FromX1"/>, <see cref="Y1"/>) out to its line. All in the hole's units.
        /// </summary>
        private sealed class StackItem
        {
            private StackItem(bool isLeader, double y1, double y2, double fromX1, double fromX2, string text, bool isExpression)
            {
                IsLeader = isLeader;
                Y1 = y1;
                Y2 = y2;
                FromX1 = fromX1;
                FromX2 = fromX2;
                Text = text;
                IsExpression = isExpression;
                TextWidth = EstimateTextWidth(text, isExpression);
            }

            public bool IsLeader { get; }

            public double Y1 { get; }

            public double Y2 { get; }

            public double FromX1 { get; }

            public double FromX2 { get; }

            public string Text { get; }

            public bool IsExpression { get; }

            public double TextWidth { get; }

            public double Offset { get; set; }

            public static StackItem Dimension(double y1, double y2, double fromX1, double fromX2, string text, bool isExpression) =>
                new(false, y1, y2, fromX1, fromX2, text, isExpression);

            public static StackItem Leader(double fromX, double y, string text) =>
                new(true, y, y, fromX, fromX, text, false);
        }
    }
}
