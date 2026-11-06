using System.Globalization;

namespace InventorAddin.Core.Holes;

/// <summary>The window field a <see cref="HoleCheck"/> is about, so the window can show the message beside it.</summary>
public enum HoleField
{
    Centres,
    Size,
    Fastener,
    Seat,
    Tolerance,
    Depth,
    ThreadDepth,
    DrillDepth,
    DrillPoint,

    /// <summary>The counterbore diameter box (a dowel's since task 106, every kind's since task 126).</summary>
    CounterboreDiameter,

    /// <summary>The counterbore depth box (tasks 106 and 126).</summary>
    CounterboreDepth,

    /// <summary>The countersink diameter box (tasks 106 and 126).</summary>
    CountersinkDiameter,

    /// <summary>The countersink angle box (tasks 106 and 126).</summary>
    CountersinkAngle,

    /// <summary>A Simple hole's Diameter box (task 112).</summary>
    Diameter,

    /// <summary>The spotface diameter box (task 112).</summary>
    SpotfaceDiameter,

    /// <summary>The spotface depth box (task 112).</summary>
    SpotfaceDepth,

    /// <summary>A Custom tolerance's Upper box (task 113).</summary>
    ToleranceUpper,

    /// <summary>A Custom tolerance's Lower box (task 113).</summary>
    ToleranceLower,
}

/// <summary>One reason a hole cannot be built, with the message the window shows.</summary>
public sealed record HoleCheck(HoleField Field, string Message)
{
    public override string ToString() => Message;
}

/// <summary>
/// What <see cref="HoleRules.Build"/> found. <see cref="Geometry"/> is null when there are problems; nothing reaches
/// Inventor then. <see cref="Resolved"/> is the spec normalised, with every default the rules took filled in (the
/// fastener, tolerance, drill point and tapped depths), which is what the add-in stores on the hole.
/// </summary>
public sealed record HoleBuildResult(HoleGeometry? Geometry, HoleSpec Resolved, IReadOnlyList<HoleCheck> Problems)
{
    public bool Succeeded => Geometry is not null;
}

/// <summary>
/// Turns a <see cref="HoleSpec"/> and the hole workbook into the <see cref="HoleGeometry"/> Inventor needs, and checks
/// it first (spec 09, "Types and seats"; spike 081 round 6: Inventor refuses some values, such as a hole larger than
/// its counterbore, and the window must not send them).
/// Rows are found by units and size, ignoring case and surrounding spaces; rows hidden with <c>Show = N</c> are still
/// found, so a hole made before its size was hidden can be opened and kept.
/// </summary>
public static class HoleRules
{
    /// <summary>
    /// Nominal sizes are rounded to this many decimals of a millimetre before ISO 286 limits are worked out, so a
    /// size such as 18.000000000000004 stays in its step (task 083).
    /// </summary>
    public const int IsoNominalDecimalsMm = 6;

    /// <summary>The smallest countersink included angle a hole takes, in degrees (task 126).</summary>
    public const decimal MinCountersinkAngleDeg = 1m;

    /// <summary>The largest countersink included angle a hole takes, in degrees (task 126).</summary>
    public const decimal MaxCountersinkAngleDeg = 179m;

    /// <summary>
    /// The countersink angles offered as quick choices in the angle box, for a centre-drill spot on any hole (Aidan,
    /// 2026-10-25; task 127). Other angles can still be typed.
    /// </summary>
    public static IReadOnlyList<decimal> CountersinkQuickAnglesDeg { get; } = [60m, 90m];

    /// <summary>
    /// Works out the geometry of a hole with <paramref name="centreCount"/> picked hole centres, or the problems that
    /// stop it being built.
    /// </summary>
    public static HoleBuildResult Build(HoleSpec spec, HoleWorkbook workbook, int centreCount)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(workbook);

        var builder = new Builder(spec.Normalised(), workbook);
        if (centreCount < 1)
        {
            builder.Problem(HoleField.Centres, "Pick at least one hole centre.");
        }

        return builder.Build();
    }

    /// <summary>
    /// The Precision Tol row for the units, fit and name; a null or blank name takes the row marked Default, else the
    /// first. Null when there is none.
    /// </summary>
    public static PrecisionToleranceRow? FindTolerance(
        HoleWorkbook workbook, HoleUnits units, PrecisionFit fit, string? name)
    {
        ArgumentNullException.ThrowIfNull(workbook);

        var rows = workbook.PrecisionTolerances.Where(r => r.Units == units && r.Fit == fit).ToList();
        if (!string.IsNullOrWhiteSpace(name))
        {
            return rows.FirstOrDefault(r => Same(r.Name, name));
        }

        return rows.FirstOrDefault(r => r.IsDefault) ?? rows.FirstOrDefault();
    }

    /// <summary>
    /// The Precision Tol row of a toleranced Clearance or Simple hole (task 113; spec 09 question 17, Default): any row
    /// for the units, whatever its fit. A name takes the row of that name, the one of <paramref name="fit"/> first when
    /// two rows share it; a null or blank name takes the first row marked Default, else the first. Null when there is
    /// none.
    /// </summary>
    public static PrecisionToleranceRow? FindAnyTolerance(
        HoleWorkbook workbook, HoleUnits units, string? name, PrecisionFit? fit = null)
    {
        ArgumentNullException.ThrowIfNull(workbook);

        var rows = workbook.PrecisionTolerances.Where(r => r.Units == units).ToList();
        if (!string.IsNullOrWhiteSpace(name))
        {
            var named = rows.Where(r => Same(r.Name, name)).ToList();
            return named.FirstOrDefault(r => r.Fit == fit) ?? named.FirstOrDefault();
        }

        return rows.FirstOrDefault(r => r.IsDefault) ?? rows.FirstOrDefault();
    }

    /// <summary>
    /// The Drill Point row by name; a null or blank name takes the shown row marked Default, else the first shown row.
    /// Null when there is none.
    /// </summary>
    public static DrillPointRow? FindDrillPoint(HoleWorkbook workbook, string? name)
    {
        ArgumentNullException.ThrowIfNull(workbook);

        if (!string.IsNullOrWhiteSpace(name))
        {
            return workbook.DrillPoints.FirstOrDefault(r => Same(r.Name, name));
        }

        var shown = workbook.DrillPoints.Where(r => !r.Hidden).ToList();
        return shown.FirstOrDefault(r => r.IsDefault) ?? shown.FirstOrDefault();
    }

    /// <summary>
    /// Two nominal diameters closer than this, in their rows' units, are the same size when a seat row is matched to a
    /// thread or dowel (task 099).
    /// </summary>
    public const decimal SameNominalTolerance = 0.0001m;

    /// <summary>
    /// The nominal diameter a tapped or dowel hole's seat is matched by (task 099): the Tapped row's Nominal for a
    /// thread, the Dowel row's for a dowel. Null for a clearance hole, or when the size is not in the tab. Hidden rows
    /// are found.
    /// </summary>
    public static decimal? SeatNominal(HoleWorkbook workbook, HoleUnits units, HoleKind kind, string? size)
    {
        ArgumentNullException.ThrowIfNull(workbook);

        if (string.IsNullOrWhiteSpace(size))
        {
            return null;
        }

        return kind switch
        {
            HoleKind.Tapped => workbook.Tapped.FirstOrDefault(r => r.Units == units && Same(r.Thread, size))?.Nominal,
            HoleKind.Dowel => workbook.Dowel.FirstOrDefault(r => r.Units == units && Same(r.Size, size))?.Nominal,
            _ => null,
        };
    }

    /// <summary>
    /// True when a Counterbore or Countersink row's Size is "the same size" as <paramref name="nominal"/> (spec 09,
    /// "Types and seats", Default): the Size has a Clearance row in the same units with the same nominal diameter.
    /// Hidden Clearance rows count.
    /// </summary>
    public static bool IsSameSize(HoleWorkbook workbook, HoleUnits units, string seatSize, decimal nominal)
    {
        ArgumentNullException.ThrowIfNull(workbook);

        return workbook.Clearance.Any(c => c.Units == units
            && Same(c.Size, seatSize)
            && Math.Abs(c.Nominal - nominal) < SameNominalTolerance);
    }

    /// <summary>
    /// The Counterbore rows of the same size as a thread or dowel of <paramref name="nominal"/> (see
    /// <see cref="IsSameSize"/>), in workbook order, hidden rows included. <c>M6</c> and a 6 mm dowel take the
    /// <c>M6</c> rows.
    /// </summary>
    public static IReadOnlyList<CounterboreRow> SameSizeCounterbores(HoleWorkbook workbook, HoleUnits units, decimal nominal)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        return workbook.Counterbore
            .Where(r => r.Units == units && IsSameSize(workbook, units, r.Size, nominal))
            .ToList()
            .AsReadOnly();
    }

    /// <summary>The Countersink rows of the same size, as <see cref="SameSizeCounterbores"/>.</summary>
    public static IReadOnlyList<CountersinkRow> SameSizeCountersinks(HoleWorkbook workbook, HoleUnits units, decimal nominal)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        return workbook.Countersink
            .Where(r => r.Units == units && IsSameSize(workbook, units, r.Size, nominal))
            .ToList()
            .AsReadOnly();
    }

    /// <summary>The Spotface rows of the same size, as <see cref="SameSizeCounterbores"/> (task 112).</summary>
    public static IReadOnlyList<SpotfaceRow> SameSizeSpotfaces(HoleWorkbook workbook, HoleUnits units, decimal nominal)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        return workbook.Spotface
            .Where(r => r.Units == units && IsSameSize(workbook, units, r.Size, nominal))
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// The Spotface row a hole's spotface starts from (task 112; spec 09, "Seat boxes"): for a clearance hole the row of
    /// its Size, for a tapped or dowel hole the row of the same size as its thread or dowel (<see cref="IsSameSize"/>),
    /// the first shown one, else the first. Null for a Simple or taper tapped hole, whose spotface follows no row, and
    /// when there is no such row (a workbook without the Spotface tab has none).
    /// </summary>
    public static SpotfaceRow? SpotfaceRowFor(HoleWorkbook workbook, HoleUnits units, HoleKind kind, string? size)
    {
        ArgumentNullException.ThrowIfNull(workbook);

        if (string.IsNullOrWhiteSpace(size))
        {
            return null;
        }

        IReadOnlyList<SpotfaceRow> rows = kind switch
        {
            HoleKind.Clearance => workbook.Spotface.Where(r => r.Units == units && Same(r.Size, size)).ToList(),
            HoleKind.Tapped or HoleKind.Dowel when SeatNominal(workbook, units, kind, size) is { } nominal =>
                SameSizeSpotfaces(workbook, units, nominal),
            _ => [],
        };
        return rows.FirstOrDefault(r => !r.Hidden) ?? rows.FirstOrDefault();
    }

    /// <summary>A seat with only the spotface values of <paramref name="row"/>; empty for none.</summary>
    public static HoleDowelSeat SpotfaceStart(SpotfaceRow? row) => row is null
        ? HoleDowelSeat.Empty
        : HoleDowelSeat.Empty with { SpotfaceDiameter = row.SpotfaceDiameter, SpotfaceDepth = row.SpotfaceDepth };

    /// <summary>
    /// Where a dowel's seat boxes start from the workbook (spec 09, "Dowel and precision holes"; task 106), value by
    /// value: the Dowel row's <c>Cbore dia</c>, <c>Cbore depth</c>, <c>Csink dia</c> and <c>Csink angle</c>, and for a
    /// blank cell or a workbook without the column, the Counterbore or Countersink row of the same size (the first shown
    /// one, else the first). A value neither gives is null. All null when the size is not in the Dowel tab. The last
    /// values used come before these; the view-model puts them first (<see cref="HoleLastDowelSeats"/>).
    /// </summary>
    public static HoleDowelSeat DowelSeatStart(HoleWorkbook workbook, HoleUnits units, string? size)
    {
        ArgumentNullException.ThrowIfNull(workbook);

        if (string.IsNullOrWhiteSpace(size)
            || workbook.Dowel.FirstOrDefault(r => r.Units == units && Same(r.Size, size)) is not { } row)
        {
            return HoleDowelSeat.Empty;
        }

        var cbores = SameSizeCounterbores(workbook, units, row.Nominal);
        var cbore = cbores.FirstOrDefault(r => !r.Hidden) ?? cbores.FirstOrDefault();
        var csinks = SameSizeCountersinks(workbook, units, row.Nominal);
        var csink = csinks.FirstOrDefault(r => !r.Hidden) ?? csinks.FirstOrDefault();
        return new HoleDowelSeat(
            row.CboreDiameter ?? cbore?.CboreDiameter,
            row.CboreDepth ?? cbore?.CboreDepth,
            row.CsinkDiameter ?? csink?.CsinkDiameter,
            row.CsinkAngleDeg ?? csink?.AngleDeg).Or(SpotfaceStart(SpotfaceRowFor(workbook, units, HoleKind.Dowel, size)));
    }

    /// <summary>
    /// Where a clearance or tapped hole's seat boxes start from the workbook (task 126): the Counterbore and Countersink
    /// rows of the size for <paramref name="fastener"/> (a tapped hole's rows of the same size, task 099), as
    /// <see cref="Build"/> picks them: a blank fastener takes the first row, hidden rows included. Each seat's values
    /// come from its own tab, so only the chosen seat's matter. <see cref="HoleDowelSeat.Empty"/> for a dowel, a blank
    /// size, or a size with no row; a value the row does not give is null. A tapped hole with no row starts from the last
    /// values used (<see cref="HoleLastDowelSeats.ForTapped"/>); the view-model puts those after these. A clearance hole
    /// with no row starts empty (task 137; spec 09 question 26, Aidan). The spotface
    /// values come from the Spotface row of the same size (<see cref="SpotfaceRowFor"/>; task 112), which follows no
    /// fastener.
    /// </summary>
    public static HoleDowelSeat SeatRowStart(
        HoleWorkbook workbook, HoleUnits units, HoleKind kind, string? size, string? fastener)
    {
        ArgumentNullException.ThrowIfNull(workbook);

        if (string.IsNullOrWhiteSpace(size))
        {
            return HoleDowelSeat.Empty;
        }

        IReadOnlyList<CounterboreRow> cbores;
        IReadOnlyList<CountersinkRow> csinks;
        switch (kind)
        {
            case HoleKind.Clearance:
                cbores = workbook.Counterbore.Where(r => r.Units == units && Same(r.Size, size)).ToList();
                csinks = workbook.Countersink.Where(r => r.Units == units && Same(r.Size, size)).ToList();
                break;
            case HoleKind.Tapped when SeatNominal(workbook, units, kind, size) is { } nominal:
                cbores = SameSizeCounterbores(workbook, units, nominal);
                csinks = SameSizeCountersinks(workbook, units, nominal);
                break;
            default:
                return HoleDowelSeat.Empty;
        }

        var cbore = PickByFastener(cbores, r => r.Screw, fastener);
        var csink = PickByFastener(csinks, r => r.Screw, fastener);
        return new HoleDowelSeat(cbore?.CboreDiameter, cbore?.CboreDepth, csink?.CsinkDiameter, csink?.AngleDeg)
            .Or(SpotfaceStart(SpotfaceRowFor(workbook, units, kind, size)));
    }

    /// <summary>The row whose Screw is <paramref name="fastener"/>; a blank fastener takes the first. Null for none.</summary>
    private static T? PickByFastener<T>(IReadOnlyList<T> rows, Func<T, string> screw, string? fastener)
        where T : class =>
        string.IsNullOrWhiteSpace(fastener) ? rows.FirstOrDefault() : rows.FirstOrDefault(r => Same(screw(r), fastener));

    /// <summary>A length in a row's units, in centimetres: millimetres divided by 10, inches times 2.54.</summary>
    public static double ToCm(decimal value, HoleUnits units) =>
        (double)(units == HoleUnits.Metric ? value / 10m : value * 2.54m);

    /// <summary>A nominal size in millimetres, rounded for <see cref="Iso286.HoleLimits"/>.</summary>
    public static double IsoNominalMm(decimal nominal, HoleUnits units) =>
        Math.Round((double)(units == HoleUnits.Metric ? nominal : nominal * 25.4m), IsoNominalDecimalsMm);

    private static bool Same(string a, string? b) =>
        string.Equals(a.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>Under Seat when a taper tapped hole has a counterbore, which Inventor does not allow (spike 108).</summary>
    public const string TaperCounterboreText =
        "A taper tapped hole cannot have a counterbore; Inventor does not allow one. Choose another seat.";

    /// <summary>Under the Upper box when a Custom tolerance has no upper limit (task 113).</summary>
    public const string CustomUpperMissingText = "Enter an upper limit.";

    /// <summary>Under the Lower box when a Custom tolerance has no lower limit (task 113).</summary>
    public const string CustomLowerMissingText = "Enter a lower limit.";

    /// <summary>A signed limit with its unit, for a message: <c>+0.02 mm</c>, <c>-0.0005 in</c>, <c>0 mm</c>.</summary>
    private static string Limit(decimal value, HoleUnits units) =>
        value.ToString("+0.######;-0.######;0", CultureInfo.InvariantCulture) + (units == HoleUnits.Metric ? " mm" : " in");

    private static string UnitsName(HoleUnits units) => units == HoleUnits.Metric ? "Metric" : "Imperial";

    private static string Length(decimal value, HoleUnits units) =>
        value.ToString("0.####", CultureInfo.InvariantCulture) + (units == HoleUnits.Metric ? " mm" : " in");

    private sealed class Builder
    {
        private readonly HoleSpec spec;
        private readonly HoleWorkbook workbook;
        private readonly List<HoleCheck> _problems = [];
        private HoleSpec _resolved;

        // Everything below is in the spec's units until the geometry is made.
        private decimal? _holeDiameter;

        // What a seat must be wider than: the thread's nominal for a tapped hole (task 099) and the dowel's for a dowel,
        // else the hole diameter.
        private decimal? _seatBound;
        private decimal? _depth;
        private decimal? _cboreDiameter;
        private decimal? _cboreDepth;
        private decimal? _csinkDiameter;
        private decimal? _csinkAngleDeg;
        private decimal? _spotfaceDiameter;
        private decimal? _spotfaceDepth;
        private HoleDrillPoint? _drillPoint;
        private HoleThread? _thread;
        private HoleTolerance? _tolerance;
        private HoleColour? _colour;

        public Builder(HoleSpec spec, HoleWorkbook workbook)
        {
            this.spec = spec;
            this.workbook = workbook;
            _resolved = spec;
        }

        private HoleUnits Units => spec.Units;

        private string UnitsText => UnitsName(Units);

        public void Problem(HoleField field, string message) => _problems.Add(new HoleCheck(field, message));

        public HoleBuildResult Build()
        {
            if (spec.Kind == HoleKind.Simple)
            {
                BuildSimple();
            }
            else if (string.IsNullOrWhiteSpace(spec.Size))
            {
                Problem(HoleField.Size, "Pick a size.");
            }
            else
            {
                switch (spec.Kind)
                {
                    case HoleKind.Clearance when spec.Seat is HoleSeat.None or HoleSeat.Spotface:
                        BuildClearance();
                        break;
                    case HoleKind.Clearance when spec.Seat == HoleSeat.Counterbore:
                        BuildCounterbore();
                        break;
                    case HoleKind.Clearance:
                        BuildCountersink();
                        break;
                    case HoleKind.Tapped:
                        BuildTapped();
                        break;
                    case HoleKind.Dowel:
                        BuildDowel();
                        break;
                    case HoleKind.TaperTapped:
                        BuildTaperTapped();
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(spec), spec.Kind, null);
                }
            }

            // A toleranced Clearance or Simple hole (task 113): the tolerance goes on the modelled diameter, the through
            // hole's, never the seat's (spec 09, Default). The spec is normalised, so only those kinds carry the flag.
            if (spec.Toleranced)
            {
                ResolveTolerance(_holeDiameter, dowel: false);
            }

            if (spec.IsBlind)
            {
                ResolveDepthAndDrillPoint();
            }

            CheckSizes();

            if (_problems.Count > 0 || _holeDiameter is not { } diameter)
            {
                return new HoleBuildResult(null, _resolved, _problems.AsReadOnly());
            }

            // Inventor measures a spotface hole's depths from the spotface's bottom; the window from the top face
            // (task 114, HoleDepthReference). The checks above use the window's depths.
            var spotfaceDepthCm = Cm(_spotfaceDepth);
            var spotfaceExpression = spec.Seat == HoleSeat.Spotface ? spec.SpotfaceDepthExpression : null;
            var thread = _thread is { FullDepth: false, IsTapered: false } t && HoleDepthReference.Shifts(spec.Seat)
                ? t with
                {
                    ThreadDepthCm = HoleDepthReference.ToInventorCm(spec.Seat, t.ThreadDepthCm, spotfaceDepthCm)
                        ?? t.ThreadDepthCm,
                    ThreadDepthExpression = HoleDepthReference.ToInventorExpression(
                        spec.Seat, t.ThreadDepthExpression, _resolved.ThreadDepth, spotfaceExpression, _spotfaceDepth,
                        Units),
                }
                : _thread;
            var windowDepthExpression = spec.IsBlind
                ? HoleStandards.IsThreaded(spec.Kind) ? spec.DrillDepthExpression : spec.DepthExpression
                : null;

            var geometry = new HoleGeometry(
                spec.Kind,
                spec.Seat,
                spec.Termination,
                spec.Flip,
                ToCm(diameter, Units),
                HoleDepthReference.ToInventorCm(spec.Seat, Cm(_depth), spotfaceDepthCm),
                Cm(_cboreDiameter),
                Cm(_cboreDepth),
                Cm(_csinkDiameter),
                (double?)_csinkAngleDeg,
                _drillPoint,
                thread,
                _tolerance,
                _colour)
            {
                // Checks above use the depths' values; the expressions go to Inventor as they were typed (task 100),
                // less the spotface depth on a spotface hole (task 114).
                DepthExpression = spec.IsBlind
                    ? HoleDepthReference.ToInventorExpression(
                        spec.Seat, windowDepthExpression, _depth, spotfaceExpression, _spotfaceDepth, Units)
                    : null,
                // A counterbore depth typed as an expression, on every kind (task 126).
                CounterboreDepthExpression = spec.Seat == HoleSeat.Counterbore ? spec.CboreDepthExpression : null,
                SpotfaceDiameterCm = Cm(_spotfaceDiameter),
                SpotfaceDepthCm = spotfaceDepthCm,
                SpotfaceDepthExpression = spotfaceExpression,
                DiameterExpression = spec.Kind == HoleKind.Simple ? spec.DiameterExpression : null,
                ExpressionUnits = Units,
            };

            return new HoleBuildResult(geometry, _resolved, _problems.AsReadOnly());
        }

        private double? Cm(decimal? value) => value is { } v ? ToCm(v, Units) : null;

        private T? FindRow<T>(IEnumerable<T> rows, Func<T, HoleUnits> units, Func<T, string> size, string key)
            where T : class =>
            rows.FirstOrDefault(r => units(r) == Units && Same(size(r), key));

        private string NotInTab(string tab) => $"{spec.Size.Trim()} is not in the {tab} tab for {UnitsText} holes.";

        private void BuildClearance()
        {
            var row = FindRow(workbook.Clearance, r => r.Units, r => r.Size, spec.Size);
            if (row is null)
            {
                Problem(HoleField.Size, NotInTab(HoleWorkbookReader.ClearanceTab));
                return;
            }

            _holeDiameter = row.Diameter(spec.ClearanceFit);

            // A spotface on a clearance hole (task 112): the through hole at the chosen fit, the spotface from its boxes,
            // and for a value they leave null the Spotface row of the size.
            if (spec.Seat == HoleSeat.Spotface)
            {
                TakeSeat(SpotfaceStart(SpotfaceRowFor(workbook, Units, HoleKind.Clearance, spec.Size)));
            }
        }

        /// <summary>
        /// A Simple hole (task 112; spec 09, "Simple holes"): the Diameter typed, no size, no row. Its seat follows no
        /// row: every value comes from the seat boxes, and one left empty is asked for under its box.
        /// </summary>
        private void BuildSimple()
        {
            if (spec.Diameter is not { } diameter)
            {
                Problem(HoleField.Diameter, "Enter a diameter.");
            }
            else if (diameter <= 0)
            {
                Problem(HoleField.Diameter, "Enter a diameter greater than zero.");
            }
            else
            {
                _holeDiameter = diameter;
            }

            if (spec.Seat != HoleSeat.None)
            {
                TakeSeat(HoleDowelSeat.Empty);
            }
        }

        /// <summary>
        /// A taper tapped hole (task 112; spec 09, "Taper tapped holes"): the Taper Tapped row of the size. The hole is
        /// the row's tap drill for the checks and the diagram; Inventor models its own thread-table tap drill (spec 09
        /// question 21) and threads the table's effective length, so there is no thread depth (question 22). The drill
        /// depth is the spec's, else the row's. No counterbore (Aidan, 2026-10-28); any other seat follows no row.
        /// </summary>
        private void BuildTaperTapped()
        {
            var row = FindRow(workbook.TaperTapped, r => r.Units, r => r.Size, spec.Size);
            if (row is null)
            {
                Problem(HoleField.Size, NotInTab(HoleWorkbookReader.TaperTappedTab));
                return;
            }

            var drillDepth = spec.DrillDepth ?? row.DrillDepth;
            _resolved = _resolved with { DrillDepth = drillDepth };
            _holeDiameter = row.TapDrill;
            _thread = new HoleThread(
                row.Size.Trim(), row.ThreadType.Trim(), "", (double)row.TapDrill, row.Units, FullDepth: false, 0)
            {
                IsTapered = true,
            };

            if (!HoleStandards.Takes(spec.Kind, spec.Seat))
            {
                Problem(HoleField.Seat, TaperCounterboreText);
                return;
            }

            if (spec.Seat != HoleSeat.None)
            {
                TakeSeat(HoleDowelSeat.Empty);
            }
        }

        /// <summary>
        /// A clearance counterbore or countersink on a size with no row of the seat (task 137; spec 09 question 26,
        /// Aidan): the through hole is the Clearance row of the size at the chosen fit, as a hole with no seat, and the
        /// seat comes only from its boxes; one left empty is asked for under its box. The hole has no fastener.
        /// </summary>
        private void BuildSeatWithoutRow()
        {
            _resolved = _resolved with { Fastener = null };
            var row = FindRow(workbook.Clearance, r => r.Units, r => r.Size, spec.Size);
            if (row is null)
            {
                Problem(HoleField.Size, NotInTab(HoleWorkbookReader.ClearanceTab));
                return;
            }

            _holeDiameter = row.Diameter(spec.ClearanceFit);
            TakeSeat(HoleDowelSeat.Empty);
        }

        /// <summary>The through hole of a seated hole: the Clearance row of the seat's size, at the seat's Hole fit.</summary>
        private void ThroughHole(string seatTab, string size, string screw, ClearanceFit fit)
        {
            var row = FindRow(workbook.Clearance, r => r.Units, r => r.Size, size);
            if (row is null)
            {
                Problem(
                    HoleField.Size,
                    $"The {seatTab} row for {size} {screw} takes its through hole from the Clearance tab, which has " +
                    $"no {size} row for {UnitsText} holes.");
                return;
            }

            _holeDiameter = row.Diameter(fit);
        }

        /// <summary>The seat row of the size and fastener; with no fastener, the first row of the size.</summary>
        private T? FindSeatRow<T>(IReadOnlyList<T> rows, Func<T, HoleUnits> units, Func<T, string> size,
            Func<T, string> screw, string tab)
            where T : class
        {
            var ofSize = rows.Where(r => units(r) == Units && Same(size(r), spec.Size)).ToList();
            if (ofSize.Count == 0)
            {
                Problem(HoleField.Size, NotInTab(tab));
                return null;
            }

            if (string.IsNullOrWhiteSpace(spec.Fastener))
            {
                return ofSize[0];
            }

            var row = ofSize.FirstOrDefault(r => Same(screw(r), spec.Fastener));
            if (row is null)
            {
                Problem(
                    HoleField.Fastener,
                    $"The {tab} tab has no {spec.Size.Trim()} row for {spec.Fastener.Trim()} for {UnitsText} holes.");
            }

            return row;
        }

        private void BuildCounterbore()
        {
            const string tab = HoleWorkbookReader.CounterboreTab;
            if (!workbook.Counterbore.Any(r => r.Units == Units && Same(r.Size, spec.Size)))
            {
                BuildSeatWithoutRow();
                return;
            }

            var row = FindSeatRow(workbook.Counterbore, r => r.Units, r => r.Size, r => r.Screw, tab);
            if (row is null)
            {
                return;
            }

            _resolved = _resolved with { Fastener = row.Screw };
            TakeSeat(new HoleDowelSeat(row.CboreDiameter, row.CboreDepth, null, null));
            ThroughHole(tab, row.Size, row.Screw, row.HoleFit);
        }

        private void BuildCountersink()
        {
            const string tab = HoleWorkbookReader.CountersinkTab;
            if (!workbook.Countersink.Any(r => r.Units == Units && Same(r.Size, spec.Size)))
            {
                BuildSeatWithoutRow();
                return;
            }

            var row = FindSeatRow(workbook.Countersink, r => r.Units, r => r.Size, r => r.Screw, tab);
            if (row is null)
            {
                return;
            }

            _resolved = _resolved with { Fastener = row.Screw };
            TakeSeat(new HoleDowelSeat(null, null, row.CsinkDiameter, row.AngleDeg));
            ThroughHole(tab, row.Size, row.Screw, row.HoleFit);
        }

        /// <summary>
        /// The seat of a tapped hole (task 126): its own values from the spec, the seat boxes, and for a value the spec
        /// leaves null the Counterbore or Countersink row of the same size as the thread's <paramref name="nominal"/>
        /// (spec 09 question 6; task 099), picked by fastener, else the first. A seat needs no row of its size: with
        /// none, a value the spec leaves null is asked for under its box, and the hole has no fastener.
        /// </summary>
        private void BuildSeatOfSameSize(decimal nominal)
        {
            if (spec.Seat == HoleSeat.None)
            {
                return;
            }

            _seatBound = nominal;
            if (spec.Seat == HoleSeat.Counterbore)
            {
                var rows = SameSizeCounterbores(workbook, Units, nominal);
                var needed = spec.CboreDiameter is null || spec.CboreDepth is null;
                var row = PickSameSize(rows, r => r.Screw, HoleWorkbookReader.CounterboreTab, needed, out var fastenerMissing);
                _resolved = _resolved with { Fastener = row?.Screw ?? (rows.Count > 0 ? spec.Fastener : null) };
                TakeSeat(new HoleDowelSeat(row?.CboreDiameter, row?.CboreDepth, null, null), ask: !fastenerMissing);
            }
            else if (spec.Seat == HoleSeat.Spotface)
            {
                // A spotface follows no fastener: the Spotface row of the same size (task 112).
                TakeSeat(SpotfaceStart(SpotfaceRowFor(workbook, Units, HoleKind.Tapped, spec.Size)));
            }
            else
            {
                var rows = SameSizeCountersinks(workbook, Units, nominal);
                var needed = spec.CsinkDiameter is null || spec.CsinkAngleDeg is null;
                var row = PickSameSize(rows, r => r.Screw, HoleWorkbookReader.CountersinkTab, needed, out var fastenerMissing);
                _resolved = _resolved with { Fastener = row?.Screw ?? (rows.Count > 0 ? spec.Fastener : null) };
                TakeSeat(new HoleDowelSeat(null, null, row?.CsinkDiameter, row?.AngleDeg), ask: !fastenerMissing);
            }
        }

        /// <summary>
        /// The row of the same size for the spec's fastener, else the first; null when there is none. A fastener with no
        /// row of the size is reported beside Fastener type only when <paramref name="needed"/>, that is when the seat
        /// boxes leave a value for the row to give; <paramref name="fastenerMissing"/> says it was.
        /// </summary>
        private T? PickSameSize<T>(
            IReadOnlyList<T> rows, Func<T, string> screw, string tab, bool needed, out bool fastenerMissing)
            where T : class
        {
            fastenerMissing = false;
            if (rows.Count == 0)
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(spec.Fastener))
            {
                return rows[0];
            }

            var row = rows.FirstOrDefault(r => Same(screw(r), spec.Fastener));
            if (row is null && needed)
            {
                fastenerMissing = true;
                Problem(
                    HoleField.Fastener,
                    $"The {tab} tab has no {spec.Fastener.Trim()} row the size of {spec.Size.Trim()} for {UnitsText} holes.");
            }

            return row;
        }

        /// <summary>
        /// The seat's values (tasks 106, 112 and 126): the spec's own, from the seat boxes, and for one the spec leaves
        /// null the row's value given in <paramref name="row"/>. Only the chosen seat's values are taken, and they go into
        /// the resolved spec, so the hole's stored data records the dimensions it was made with. A value neither gives is
        /// asked for under its box, unless <paramref name="ask"/> is false because another problem already says why.
        /// </summary>
        private void TakeSeat(HoleDowelSeat row, bool ask = true)
        {
            if (spec.Seat == HoleSeat.Counterbore)
            {
                _cboreDiameter = spec.CboreDiameter ?? row.CboreDiameter;
                _cboreDepth = spec.CboreDepth ?? row.CboreDepth;
                _resolved = _resolved with { CboreDiameter = _cboreDiameter, CboreDepth = _cboreDepth };
                if (_cboreDiameter is null && ask)
                {
                    Problem(HoleField.CounterboreDiameter, "Enter a counterbore diameter.");
                }

                if (_cboreDepth is null && ask)
                {
                    Problem(HoleField.CounterboreDepth, "Enter a counterbore depth.");
                }
            }
            else if (spec.Seat == HoleSeat.Countersink)
            {
                _csinkDiameter = spec.CsinkDiameter ?? row.CsinkDiameter;
                _csinkAngleDeg = spec.CsinkAngleDeg ?? row.CsinkAngleDeg;
                _resolved = _resolved with { CsinkDiameter = _csinkDiameter, CsinkAngleDeg = _csinkAngleDeg };
                if (_csinkDiameter is null && ask)
                {
                    Problem(HoleField.CountersinkDiameter, "Enter a countersink diameter.");
                }

                if (_csinkAngleDeg is null && ask)
                {
                    Problem(HoleField.CountersinkAngle, "Enter a countersink angle.");
                }
            }
            else if (spec.Seat == HoleSeat.Spotface)
            {
                _spotfaceDiameter = spec.SpotfaceDiameter ?? row.SpotfaceDiameter;
                _spotfaceDepth = spec.SpotfaceDepth ?? row.SpotfaceDepth;
                _resolved = _resolved with { SpotfaceDiameter = _spotfaceDiameter, SpotfaceDepth = _spotfaceDepth };
                if (_spotfaceDiameter is null && ask)
                {
                    Problem(HoleField.SpotfaceDiameter, "Enter a spotface diameter.");
                }

                if (_spotfaceDepth is null && ask)
                {
                    Problem(HoleField.SpotfaceDepth, "Enter a spotface depth.");
                }
            }
        }

        /// <summary>
        /// A dowel's seat (spec 09 question 9; task 106): its own values from the spec, which follow no fastener, so no
        /// Counterbore or Countersink row is used. A value left empty is asked for under its box.
        /// </summary>
        private void BuildDowelSeat(decimal nominal)
        {
            if (spec.Seat == HoleSeat.None)
            {
                return;
            }

            _seatBound = nominal;

            // A counterbore or countersink is the dowel's own; a spotface starts from the Spotface row of the same size
            // for a value left null (task 112).
            TakeSeat(spec.Seat == HoleSeat.Spotface
                ? SpotfaceStart(SpotfaceRowFor(workbook, Units, HoleKind.Dowel, spec.Size))
                : HoleDowelSeat.Empty);
        }

        private void BuildTapped()
        {
            var row = FindRow(workbook.Tapped, r => r.Units, r => r.Thread, spec.Size);
            if (row is null)
            {
                Problem(HoleField.Size, NotInTab(HoleWorkbookReader.TappedTab));
                return;
            }

            var threadDepth = spec.ThreadDepth ?? row.ThreadDepth;
            var drillDepth = spec.DrillDepth ?? row.DrillDepth;
            _resolved = _resolved with { ThreadDepth = threadDepth, DrillDepth = drillDepth };
            _holeDiameter = row.TapDrill;

            if (spec.IsBlind)
            {
                if (threadDepth <= 0)
                {
                    Problem(HoleField.ThreadDepth, "Enter a thread depth greater than zero.");
                }

                if (threadDepth > drillDepth)
                {
                    Problem(
                        HoleField.ThreadDepth,
                        $"The thread depth ({Length(threadDepth, Units)}) is more than the drill depth " +
                        $"({Length(drillDepth, Units)}).");
                }
            }

            _thread = new HoleThread(
                row.Designation,
                row.ThreadType,
                row.Class.Trim(),
                (double)row.TapDrill,
                row.Units,
                FullDepth: !spec.IsBlind,
                ToCm(threadDepth, Units))
            {
                ThreadDepthExpression = spec.IsBlind ? spec.ThreadDepthExpression : null,
            };

            BuildSeatOfSameSize(row.Nominal);
        }

        private void BuildDowel()
        {
            var row = FindRow(workbook.Dowel, r => r.Units, r => r.Size, spec.Size);
            if (row is null)
            {
                Problem(HoleField.Size, NotInTab(HoleWorkbookReader.DowelTab));
            }
            else
            {
                _holeDiameter = row.Nominal;
                BuildDowelSeat(row.Nominal);
            }

            ResolveTolerance(row?.Nominal, dowel: true);
        }

        /// <summary>
        /// The tolerance on a precision hole's diameter and its colour (<see cref="HolePrecision.Colour"/>). A dowel's
        /// row is the Precision Tol row of its fit (task 098); a toleranced Clearance or Simple hole's any row for the
        /// units (task 113; spec 09 question 17, Default), and its resolved PrecisionFit is the row's fit. A Custom
        /// tolerance (questions 17 and 18, Default) takes the typed Upper and Lower, Upper above Lower. ISO limits are
        /// worked out at <paramref name="nominal"/>, the modelled diameter; none when it is not known.
        /// </summary>
        private void ResolveTolerance(decimal? nominal, bool dowel)
        {
            if (spec.CustomTolerance)
            {
                _colour = HolePrecision.Colour(spec, null, workbook.Settings);
                ResolveCustomTolerance();
                return;
            }

            if (dowel)
            {
                // A dowel's colour is its fit's whatever its tolerance row (task 096).
                _colour = HolePrecision.Colour(spec, null, workbook.Settings);
            }

            var tolerance = dowel
                ? FindTolerance(workbook, Units, spec.PrecisionFit, spec.Tolerance)
                : FindAnyTolerance(workbook, Units, spec.Tolerance, spec.PrecisionFit);
            if (tolerance is null)
            {
                Problem(
                    HoleField.Tolerance,
                    (string.IsNullOrWhiteSpace(spec.Tolerance), dowel) switch
                    {
                        (true, true) => $"The Precision Tol tab has no {spec.PrecisionFit} rows for {UnitsText} holes.",
                        (false, true) => $"The Precision Tol tab has no \"{spec.Tolerance!.Trim()}\" row for " +
                                         $"{spec.PrecisionFit} fit and {UnitsText} holes.",
                        (true, false) => $"The Precision Tol tab has no rows for {UnitsText} holes.",
                        (false, false) => $"The Precision Tol tab has no \"{spec.Tolerance!.Trim()}\" row for " +
                                          $"{UnitsText} holes.",
                    });
                return;
            }

            _resolved = _resolved with { Tolerance = tolerance.Name };
            if (!dowel)
            {
                _resolved = _resolved with { PrecisionFit = tolerance.Fit };
                _colour = HolePrecision.Colour(_resolved, tolerance, workbook.Settings);
            }

            if (tolerance.Kind == ToleranceKind.Limits)
            {
                if (tolerance.Upper is { } upper && tolerance.Lower is { } lower)
                {
                    _tolerance = new HoleTolerance(tolerance.Name, null, ToCm(upper, Units), ToCm(lower, Units));
                }
                else
                {
                    Problem(HoleField.Tolerance, $"The tolerance \"{tolerance.Name}\" has no Upper and Lower values.");
                }

                return;
            }

            if (nominal is not { } size || size <= 0)
            {
                return;
            }

            var limits = Iso286.HoleLimits(IsoNominalMm(size, Units), tolerance.IsoCode);
            if (!limits.Succeeded)
            {
                Problem(HoleField.Tolerance, limits.Message);
                return;
            }

            _tolerance = new HoleTolerance(tolerance.Name, limits.Code, limits.UpperMm / 10, limits.LowerMm / 10);
        }

        /// <summary>
        /// A Custom tolerance (task 113): the Upper and Lower typed, in the spec's units, relative to the modelled
        /// diameter; both are needed and Upper must be above Lower. Inventor takes them as deviations.
        /// </summary>
        private void ResolveCustomTolerance()
        {
            if (spec.CustomUpper is null)
            {
                Problem(HoleField.ToleranceUpper, CustomUpperMissingText);
            }

            if (spec.CustomLower is null)
            {
                Problem(HoleField.ToleranceLower, CustomLowerMissingText);
            }

            if (spec.CustomUpper is not { } upper || spec.CustomLower is not { } lower)
            {
                return;
            }

            if (upper <= lower)
            {
                Problem(
                    HoleField.ToleranceUpper,
                    $"The upper limit ({Limit(upper, Units)}) is not above the lower limit ({Limit(lower, Units)}).");
                return;
            }

            _tolerance = new HoleTolerance(HolePrecision.CustomName, null, ToCm(upper, Units), ToCm(lower, Units));
        }

        private void ResolveDepthAndDrillPoint()
        {
            var threaded = HoleStandards.IsThreaded(spec.Kind);
            _depth = threaded ? _resolved.DrillDepth : spec.Depth;
            if (_depth is not > 0)
            {
                // A threaded hole whose thread was not found has no drill depth yet; its Size problem says why.
                if (!threaded || _thread is not null)
                {
                    Problem(
                        threaded ? HoleField.DrillDepth : HoleField.Depth,
                        threaded
                            ? "Enter a drill depth greater than zero."
                            : "Enter a depth greater than zero.");
                }
            }

            var row = FindDrillPoint(workbook, spec.DrillPoint);
            if (row is null)
            {
                Problem(
                    HoleField.DrillPoint,
                    string.IsNullOrWhiteSpace(spec.DrillPoint)
                        ? "The Drill Point tab has no rows."
                        : $"The Drill Point tab has no \"{spec.DrillPoint.Trim()}\" row.");
                return;
            }

            _resolved = _resolved with { DrillPoint = row.Name };
            _drillPoint = new HoleDrillPoint(row.Name, (double?)row.AngleDeg);
        }

        /// <summary>What the seat must be wider than, for a message: the thread and its nominal, or the hole.</summary>
        private string SeatBoundText(decimal value) => spec.Kind == HoleKind.Tapped && _seatBound is not null
            ? $"The thread {spec.Size.Trim()} ({Length(value, Units)})"
            : spec.Kind == HoleKind.TaperTapped
                ? $"The tap drill ({Length(value, Units)})"
                : $"The hole ({Length(value, Units)})";

        private void CheckSizes()
        {
            // The seat is in its own boxes on every kind (tasks 106 and 126), so its problems show beside them.
            if (_holeDiameter is { } diameter && diameter <= 0)
            {
                Problem(HoleField.Size, $"The hole diameter for {spec.Size.Trim()} is not greater than zero.");
                return;
            }

            if (_spotfaceDiameter is { } spotfaceDiameter && (_seatBound ?? _holeDiameter) is { } spotHole
                && spotHole >= spotfaceDiameter)
            {
                Problem(
                    HoleField.SpotfaceDiameter,
                    $"{SeatBoundText(spotHole)} is not smaller than its spotface ({Length(spotfaceDiameter, Units)}).");
            }

            // A spotface depth of 0 is allowed (spike 108; Aidan, 2026-10-28): Inventor models only the hole.
            if (_spotfaceDepth is { } spotfaceDepth)
            {
                if (spotfaceDepth < 0)
                {
                    Problem(HoleField.SpotfaceDepth, "The spotface depth is less than zero.");
                }
                else if (_depth is { } blind && blind > 0 && spotfaceDepth >= blind)
                {
                    Problem(
                        HoleField.Depth,
                        $"The spotface depth ({Length(spotfaceDepth, Units)}) is not less than the hole depth " +
                        $"({Length(blind, Units)}).");
                }
            }

            if (_cboreDiameter is { } cboreDiameter && (_seatBound ?? _holeDiameter) is { } hole)
            {
                if (hole >= cboreDiameter)
                {
                    Problem(
                        HoleField.CounterboreDiameter,
                        $"{SeatBoundText(hole)} is not smaller than its counterbore ({Length(cboreDiameter, Units)}).");
                }
            }

            // A counterbore depth of 0 is allowed too (spike 108 question 7; task 127): Inventor keeps the counterbore
            // hole type and models only the hole.
            if (_cboreDepth is { } cboreDepth)
            {
                if (cboreDepth < 0)
                {
                    Problem(HoleField.CounterboreDepth, "The counterbore depth is less than zero.");
                }
                else if (_depth is { } depth && depth > 0 && cboreDepth >= depth)
                {
                    Problem(
                        HoleField.Depth,
                        $"The counterbore depth ({Length(cboreDepth, Units)}) is not less than the hole depth " +
                        $"({Length(depth, Units)}).");
                }
            }

            if (_csinkDiameter is { } csinkDiameter && (_seatBound ?? _holeDiameter) is { } through
                && through >= csinkDiameter)
            {
                Problem(
                    HoleField.CountersinkDiameter,
                    $"{SeatBoundText(through)} is not smaller than its countersink ({Length(csinkDiameter, Units)}).");
            }

            if (_csinkAngleDeg is { } angle && angle is < MinCountersinkAngleDeg or > MaxCountersinkAngleDeg)
            {
                Problem(
                    HoleField.CountersinkAngle,
                    $"The countersink angle ({angle} deg) is not between {MinCountersinkAngleDeg} and " +
                    $"{MaxCountersinkAngleDeg} degrees.");
            }
            else if (_csinkAngleDeg is { } csinkAngle
                && _csinkDiameter is { } sinkDiameter
                && _holeDiameter is { } sinkHole && sinkHole > 0 && sinkHole < sinkDiameter
                && _depth is { } blindDepth && blindDepth > 0)
            {
                // The cone's depth from the countersink diameter down to the modelled hole diameter (the tap drill for a
                // tapped hole, as the task 099 spike measured it).
                var halfAngleRad = (double)csinkAngle / 2 * Math.PI / 180;
                var csinkDepth = (double)(sinkDiameter - sinkHole) / 2 / Math.Tan(halfAngleRad);
                if (csinkDepth >= (double)blindDepth)
                {
                    Problem(
                        HoleField.Depth,
                        $"The countersink depth ({Length(Math.Round((decimal)csinkDepth, 4), Units)}) is not less " +
                        $"than the hole depth ({Length(blindDepth, Units)}).");
                }
            }

            if (_drillPoint?.AngleDeg is { } pointAngle && pointAngle is <= 0 or >= 180)
            {
                Problem(
                    HoleField.DrillPoint,
                    $"The drill point angle ({pointAngle} deg) is not between 0 and 180 degrees.");
            }
        }
    }
}
