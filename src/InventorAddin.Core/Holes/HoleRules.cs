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

    /// <summary>A dowel's counterbore diameter box (task 106).</summary>
    CounterboreDiameter,

    /// <summary>A dowel's counterbore depth box (task 106).</summary>
    CounterboreDepth,

    /// <summary>A dowel's countersink diameter box (task 106).</summary>
    CountersinkDiameter,

    /// <summary>A dowel's countersink angle box (task 106).</summary>
    CountersinkAngle,
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
            row.CsinkAngleDeg ?? csink?.AngleDeg);
    }

    /// <summary>A length in a row's units, in centimetres: millimetres divided by 10, inches times 2.54.</summary>
    public static double ToCm(decimal value, HoleUnits units) =>
        (double)(units == HoleUnits.Metric ? value / 10m : value * 2.54m);

    /// <summary>A nominal size in millimetres, rounded for <see cref="Iso286.HoleLimits"/>.</summary>
    public static double IsoNominalMm(decimal nominal, HoleUnits units) =>
        Math.Round((double)(units == HoleUnits.Metric ? nominal : nominal * 25.4m), IsoNominalDecimalsMm);

    private static bool Same(string a, string? b) =>
        string.Equals(a.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase);

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

        // What a seat must be wider than: the thread's nominal for a tapped hole (task 099), else the hole diameter.
        private decimal? _seatBound;
        private decimal? _depth;
        private decimal? _cboreDiameter;
        private decimal? _cboreDepth;
        private decimal? _csinkDiameter;
        private decimal? _csinkAngleDeg;
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
            if (string.IsNullOrWhiteSpace(spec.Size))
            {
                Problem(HoleField.Size, "Pick a size.");
            }
            else
            {
                switch (spec.Kind)
                {
                    case HoleKind.Clearance when spec.Seat == HoleSeat.None:
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
                    default:
                        throw new ArgumentOutOfRangeException(nameof(spec), spec.Kind, null);
                }
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

            var geometry = new HoleGeometry(
                spec.Kind,
                spec.Seat,
                spec.Termination,
                spec.Flip,
                ToCm(diameter, Units),
                Cm(_depth),
                Cm(_cboreDiameter),
                Cm(_cboreDepth),
                Cm(_csinkDiameter),
                (double?)_csinkAngleDeg,
                _drillPoint,
                _thread,
                _tolerance,
                _colour)
            {
                // Checks above use the depths' values; the expressions go to Inventor as they were typed (task 100).
                DepthExpression = spec.IsBlind
                    ? spec.Kind == HoleKind.Tapped ? spec.DrillDepthExpression : spec.DepthExpression
                    : null,
                CounterboreDepthExpression = spec.Kind == HoleKind.Dowel && spec.Seat == HoleSeat.Counterbore
                    ? spec.CboreDepthExpression
                    : null,
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
            var row = FindSeatRow(workbook.Counterbore, r => r.Units, r => r.Size, r => r.Screw, tab);
            if (row is null)
            {
                return;
            }

            _resolved = _resolved with { Fastener = row.Screw };
            _cboreDiameter = row.CboreDiameter;
            _cboreDepth = row.CboreDepth;
            ThroughHole(tab, row.Size, row.Screw, row.HoleFit);
        }

        private void BuildCountersink()
        {
            const string tab = HoleWorkbookReader.CountersinkTab;
            var row = FindSeatRow(workbook.Countersink, r => r.Units, r => r.Size, r => r.Screw, tab);
            if (row is null)
            {
                return;
            }

            _resolved = _resolved with { Fastener = row.Screw };
            _csinkDiameter = row.CsinkDiameter;
            _csinkAngleDeg = row.AngleDeg;
            ThroughHole(tab, row.Size, row.Screw, row.HoleFit);
        }

        /// <summary>
        /// The seat of a tapped or dowel hole: the Counterbore or Countersink row of the same size as the thread's or
        /// dowel's <paramref name="nominal"/> (spec 09 questions 6 and 9; task 099), picked by fastener, else the first.
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
                var row = PickSameSize(
                    SameSizeCounterbores(workbook, Units, nominal), r => r.Screw, HoleWorkbookReader.CounterboreTab, nominal);
                if (row is not null)
                {
                    _resolved = _resolved with { Fastener = row.Screw };
                    _cboreDiameter = row.CboreDiameter;
                    _cboreDepth = row.CboreDepth;
                }
            }
            else
            {
                var row = PickSameSize(
                    SameSizeCountersinks(workbook, Units, nominal), r => r.Screw, HoleWorkbookReader.CountersinkTab, nominal);
                if (row is not null)
                {
                    _resolved = _resolved with { Fastener = row.Screw };
                    _csinkDiameter = row.CsinkDiameter;
                    _csinkAngleDeg = row.AngleDeg;
                }
            }
        }

        private T? PickSameSize<T>(IReadOnlyList<T> rows, Func<T, string> screw, string tab, decimal nominal)
            where T : class
        {
            var size = spec.Size.Trim();
            if (rows.Count == 0)
            {
                Problem(
                    HoleField.Seat,
                    $"No {tab} row is the size of {size} (a Clearance size with nominal {Length(nominal, Units)}) " +
                    $"for {UnitsText} holes, so it cannot have a {tab.ToLowerInvariant()}. Set Seat to None.");
                return null;
            }

            if (string.IsNullOrWhiteSpace(spec.Fastener))
            {
                return rows[0];
            }

            var row = rows.FirstOrDefault(r => Same(screw(r), spec.Fastener));
            if (row is null)
            {
                Problem(
                    HoleField.Fastener,
                    $"The {tab} tab has no {spec.Fastener.Trim()} row the size of {size} for {UnitsText} holes.");
            }

            return row;
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
            if (spec.Seat == HoleSeat.Counterbore)
            {
                _cboreDiameter = spec.CboreDiameter;
                _cboreDepth = spec.CboreDepth;
                if (_cboreDiameter is null)
                {
                    Problem(HoleField.CounterboreDiameter, "Enter a counterbore diameter.");
                }

                if (_cboreDepth is null)
                {
                    Problem(HoleField.CounterboreDepth, "Enter a counterbore depth.");
                }
            }
            else
            {
                _csinkDiameter = spec.CsinkDiameter;
                _csinkAngleDeg = spec.CsinkAngleDeg;
                if (_csinkDiameter is null)
                {
                    Problem(HoleField.CountersinkDiameter, "Enter a countersink diameter.");
                }

                if (_csinkAngleDeg is null)
                {
                    Problem(HoleField.CountersinkAngle, "Enter a countersink angle.");
                }
            }
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

            _colour = workbook.Settings.FitColour(spec.PrecisionFit);

            var tolerance = FindTolerance(workbook, Units, spec.PrecisionFit, spec.Tolerance);
            if (tolerance is null)
            {
                Problem(
                    HoleField.Tolerance,
                    string.IsNullOrWhiteSpace(spec.Tolerance)
                        ? $"The Precision Tol tab has no {spec.PrecisionFit} rows for {UnitsText} holes."
                        : $"The Precision Tol tab has no \"{spec.Tolerance.Trim()}\" row for {spec.PrecisionFit} fit " +
                          $"and {UnitsText} holes.");
                return;
            }

            _resolved = _resolved with { Tolerance = tolerance.Name };

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

            if (row is null)
            {
                return;
            }

            var limits = Iso286.HoleLimits(IsoNominalMm(row.Nominal, Units), tolerance.IsoCode);
            if (!limits.Succeeded)
            {
                Problem(HoleField.Tolerance, limits.Message);
                return;
            }

            _tolerance = new HoleTolerance(tolerance.Name, limits.Code, limits.UpperMm / 10, limits.LowerMm / 10);
        }

        private void ResolveDepthAndDrillPoint()
        {
            _depth = spec.Kind == HoleKind.Tapped ? _resolved.DrillDepth : spec.Depth;
            if (_depth is not > 0)
            {
                // A tapped hole whose thread was not found has no drill depth yet; its Size problem says why.
                if (spec.Kind != HoleKind.Tapped || _thread is not null)
                {
                    Problem(
                        spec.Kind == HoleKind.Tapped ? HoleField.DrillDepth : HoleField.Depth,
                        spec.Kind == HoleKind.Tapped
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
            : $"The hole ({Length(value, Units)})";

        private void CheckSizes()
        {
            // A dowel's seat is typed in its own boxes (task 106); other seats come from the Size's rows.
            var dowel = spec.Kind == HoleKind.Dowel;
            if (_holeDiameter is { } diameter && diameter <= 0)
            {
                Problem(HoleField.Size, $"The hole diameter for {spec.Size.Trim()} is not greater than zero.");
                return;
            }

            if (_cboreDiameter is { } cboreDiameter && (_seatBound ?? _holeDiameter) is { } hole)
            {
                if (hole >= cboreDiameter)
                {
                    Problem(
                        dowel ? HoleField.CounterboreDiameter : HoleField.Size,
                        $"{SeatBoundText(hole)} is not smaller than its counterbore ({Length(cboreDiameter, Units)}).");
                }
            }

            if (_cboreDepth is { } cboreDepth)
            {
                if (cboreDepth <= 0)
                {
                    Problem(
                        dowel ? HoleField.CounterboreDepth : HoleField.Size,
                        "The counterbore depth is not greater than zero.");
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
                    dowel ? HoleField.CountersinkDiameter : HoleField.Size,
                    $"{SeatBoundText(through)} is not smaller than its countersink ({Length(csinkDiameter, Units)}).");
            }

            if (_csinkAngleDeg is { } angle && angle is <= 0 or >= 180)
            {
                Problem(
                    dowel ? HoleField.CountersinkAngle : HoleField.Size,
                    $"The countersink angle ({angle} deg) is not between 0 and 180 degrees.");
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
