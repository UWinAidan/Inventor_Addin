using System.Globalization;

namespace InventorAddin.Core.Holes;

/// <summary>
/// What the add-in reads from a hole feature in Inventor, for edit mode (task 088). Lengths are in centimetres and
/// angles in degrees; a value Inventor does not report for the hole is null.
/// </summary>
/// <param name="Seat">
/// None, Counterbore, Countersink or Spotface (task 112; the add-in reads a spotface from task 114); null for a seat AWB
/// Hole does not offer. Depths are as Inventor measures them: a spotface hole's from the spotface's bottom
/// (<see cref="HoleDepthReference"/>).
/// </param>
/// <param name="Tapped">The hole is threaded, with a straight or a tapered thread (<see cref="TaperedThread"/>).</param>
/// <param name="ThreadDesignation">Tapped holes: Inventor's designation (<c>M6x1</c>).</param>
/// <param name="ThreadType">Tapped holes: Inventor's thread type.</param>
/// <param name="ThreadDepthCm">Tapped holes: the thread depth, when the thread is not full depth.</param>
/// <param name="HoleDiameterCm">The hole diameter.</param>
/// <param name="Termination">Distance, Through All or To; null for an extent AWB Hole does not offer.</param>
/// <param name="DepthCm">Blind holes: the depth.</param>
/// <param name="CounterboreDiameterCm">Counterbore seats.</param>
/// <param name="CounterboreDepthCm">Counterbore seats.</param>
/// <param name="CountersinkDiameterCm">Countersink seats.</param>
/// <param name="CountersinkAngleDeg">Countersink seats: the included angle.</param>
/// <param name="FlatBottom">Blind holes: the bottom is flat.</param>
/// <param name="DrillPointAngleDeg">Blind holes with a drill point: its angle.</param>
/// <param name="ToleranceIsoCode">The ISO fit code on the diameter, when the tolerance is a fit.</param>
/// <param name="ToleranceUpperCm">The upper deviation, when the diameter has a tolerance.</param>
/// <param name="ToleranceLowerCm">The lower deviation, when the diameter has a tolerance.</param>
/// <param name="Flip">The hole runs in the negative direction.</param>
public sealed record InventorHoleFacts(
    HoleSeat? Seat,
    bool Tapped,
    string? ThreadDesignation,
    string? ThreadType,
    double? ThreadDepthCm,
    double? HoleDiameterCm,
    HoleTermination? Termination,
    double? DepthCm,
    double? CounterboreDiameterCm,
    double? CounterboreDepthCm,
    double? CountersinkDiameterCm,
    double? CountersinkAngleDeg,
    bool FlatBottom,
    double? DrillPointAngleDeg,
    string? ToleranceIsoCode,
    double? ToleranceUpperCm,
    double? ToleranceLowerCm,
    bool Flip)
{
    public bool HasTolerance => ToleranceIsoCode is not null || ToleranceUpperCm is not null || ToleranceLowerCm is not null;

    /// <summary>
    /// Tapped holes whose thread is not full depth: the thread depth parameter's expression when other parameters drive
    /// it (<c>d0</c>, <c>1.5 ul * d0 + 1 mm</c>; task 100); null when it is a plain length.
    /// </summary>
    public string? ThreadDepthExpression { get; init; }

    /// <summary>Blind holes: the depth parameter's expression when other parameters drive it; null when it is a plain length.</summary>
    public string? DepthExpression { get; init; }

    /// <summary>
    /// Counterbore seats: the counterbore depth parameter's expression when other parameters drive it (task 106); null
    /// when it is a plain length.
    /// </summary>
    public string? CounterboreDepthExpression { get; init; }

    /// <summary>
    /// The thread is a tapered pipe thread (task 112; the add-in reads Inventor's <c>TaperedThreadInfo</c> from task
    /// 114): <see cref="ThreadType"/> and <see cref="ThreadDesignation"/> are then the Taper Tapped tab's Thread type
    /// and Size (<c>NPT</c>, <c>1/4</c>).
    /// </summary>
    public bool TaperedThread { get; init; }

    /// <summary>Spotface seats (task 112): the spotface diameter.</summary>
    public double? SpotfaceDiameterCm { get; init; }

    /// <summary>Spotface seats (task 112): the spotface depth.</summary>
    public double? SpotfaceDepthCm { get; init; }

    /// <summary>Spotface seats: the spotface depth parameter's expression when other parameters drive it; else null.</summary>
    public string? SpotfaceDepthExpression { get; init; }

    /// <summary>
    /// The hole diameter parameter's expression when other parameters drive it (task 112: a Simple hole's diameter typed
    /// as an expression); null when it is a plain length.
    /// </summary>
    public string? DiameterExpression { get; init; }
}

/// <summary>Where the spec of a hole read back came from.</summary>
public enum HoleSource
{
    /// <summary>The hole's stored AWB data.</summary>
    Awb,

    /// <summary>
    /// The hole's geometry: a hole made by Inventor's command, or one whose AWB data cannot be read. It becomes an
    /// AWB hole on OK (spec 09 question 5).
    /// </summary>
    Inventor,
}

/// <summary>
/// A hole read back for edit mode: the spec to open the window with, where it came from, the stored data when there
/// was some, and a note for each choice that had to be guessed.
/// </summary>
public sealed record HoleReadBackResult(
    HoleSpec Spec,
    HoleSource Source,
    HoleStoredData? Stored,
    IReadOnlyList<string> Notes)
{
    public bool IsInventorHole => Source == HoleSource.Inventor;
}

/// <summary>
/// Reads a hole back into a <see cref="HoleSpec"/> for edit mode (spec 09, "Editing reads the hole first"): from its
/// stored AWB data when that can be read, else the closest spec the workbook can build from its geometry.
/// Lengths match when closer than <see cref="HoleStateComparison.LengthToleranceCm"/>; angles within
/// <see cref="AngleToleranceDeg"/>.
/// </summary>
public static class HoleReadBack
{
    /// <summary>Angles closer than this (degrees) are the same.</summary>
    public const double AngleToleranceDeg = 0.01;

    /// <summary>
    /// The spec for a hole. <paramref name="stored"/> is the result of reading its <c>AWB_Hole</c> attribute set, or
    /// null when the hole has none. <paramref name="defaultUnits"/> is tried first when the geometry fits both units.
    /// </summary>
    public static HoleReadBackResult Read(
        HoleStoredDataReadResult? stored, InventorHoleFacts facts, HoleWorkbook workbook, HoleUnits defaultUnits)
    {
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentNullException.ThrowIfNull(workbook);

        if (stored?.Data is { } storedData)
        {
            // Data stored before task 116 measured a threaded hole's depths from the top face: they are converted to
            // below the seat, so the hole opens as it is (HoleDepthReference.Upgrade). The re-apply check takes the
            // converted data too.
            var data = HoleDepthReference.Upgrade(storedData, facts);
            var windowFacts = HoleDepthReference.ToWindow(facts, data.Applied.HoleDiameterCm);
            return new HoleReadBackResult(WithDowelSeatFromHole(data.Spec, windowFacts), HoleSource.Awb, data, []);
        }

        // The depths as the window measures them (tasks 114 and 116): below the seat on a threaded hole, whose
        // countersink cone is worked out at the workbook's tap drill, as the window works it out.
        facts = HoleDepthReference.ToWindow(facts, TapDrillCm(facts, workbook));

        var notes = new List<string>();
        if (stored is not null)
        {
            notes.Add(stored.Message);
        }

        var spec = new Matcher(facts, workbook, defaultUnits, notes).Spec();
        return new HoleReadBackResult(spec, HoleSource.Inventor, null, notes.AsReadOnly());
    }

    /// <summary>
    /// A seated dowel's spec with every seat size it does not store taken from the hole (task 106): a dowel stored before
    /// its seat had its own sizes took them from a same-size row, and opens on the sizes the hole has. The counterbore
    /// depth's expression comes with its depth. Any other spec is returned as it is: a clearance or tapped hole stored
    /// before task 126 names its seat's row, and resolves to the row's dimensions (<see cref="HoleRules"/>).
    /// </summary>
    public static HoleSpec WithDowelSeatFromHole(HoleSpec spec, InventorHoleFacts facts)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(facts);

        return spec.Kind == HoleKind.Dowel ? WithSeatFromHole(spec, facts) : spec;
    }

    /// <summary>
    /// A seated spec with every seat size it does not hold taken from the hole, whatever its kind (task 126): a hole
    /// made by Inventor's command opens on the seat it has, not on a row's. The counterbore depth's expression comes with
    /// its depth. A spec with no seat is returned as it is.
    /// </summary>
    public static HoleSpec WithSeatFromHole(HoleSpec spec, InventorHoleFacts facts)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(facts);

        if (spec.Seat == HoleSeat.None)
        {
            return spec;
        }

        var units = spec.Units;
        var filled = spec.Seat switch
        {
            HoleSeat.Counterbore => spec with
            {
                CboreDiameter = spec.CboreDiameter ?? Length(facts.CounterboreDiameterCm, units),
                CboreDepth = spec.CboreDepth ?? Length(facts.CounterboreDepthCm, units),
                CboreDepthExpression = spec.CboreDepth is null && facts.CounterboreDepthCm is not null
                    ? facts.CounterboreDepthExpression
                    : spec.CboreDepthExpression,
            },
            HoleSeat.Spotface => spec with
            {
                SpotfaceDiameter = spec.SpotfaceDiameter ?? Length(facts.SpotfaceDiameterCm, units),
                SpotfaceDepth = spec.SpotfaceDepth ?? Length(facts.SpotfaceDepthCm, units),
                SpotfaceDepthExpression = spec.SpotfaceDepth is null && facts.SpotfaceDepthCm is not null
                    ? facts.SpotfaceDepthExpression
                    : spec.SpotfaceDepthExpression,
            },
            _ => spec with
            {
                CsinkDiameter = spec.CsinkDiameter ?? Length(facts.CountersinkDiameterCm, units),
                CsinkAngleDeg = spec.CsinkAngleDeg ?? Angle(facts.CountersinkAngleDeg),
            },
        };
        return filled.Normalised();
    }

    /// <summary>
    /// The tap drill a threaded hole of <paramref name="facts"/> is modelled at, in centimetres: the Tapped row of its
    /// designation and thread type, or the Taper Tapped row of its size and thread type (the row's tap drill, as the
    /// window takes it; spec 09 question 21). Null for a hole that is not threaded or a thread not in the workbook.
    /// </summary>
    public static double? TapDrillCm(InventorHoleFacts facts, HoleWorkbook workbook)
    {
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentNullException.ThrowIfNull(workbook);
        if (!facts.Tapped)
        {
            return null;
        }

        var designation = facts.ThreadDesignation?.Trim() ?? "";
        var threadType = facts.ThreadType?.Trim();
        bool Same(string a, string? b) => string.Equals(a.Trim(), b, StringComparison.OrdinalIgnoreCase);

        if (facts.TaperedThread)
        {
            var taper = workbook.TaperTapped.FirstOrDefault(r =>
                Same(r.Size, designation) && (threadType is null || Same(r.ThreadType, threadType)));
            return taper is null ? null : HoleRules.ToCm(taper.TapDrill, taper.Units);
        }

        var row = workbook.Tapped.FirstOrDefault(r =>
            Same(r.Designation, designation) && (threadType is null || Same(r.ThreadType, threadType)));
        return row is null ? null : HoleRules.ToCm(row.TapDrill, row.Units);
    }

    private static decimal? Length(double? cm, HoleUnits units) => cm is { } c ? FromCm(c, units) : null;

    /// <summary>An angle in degrees as the window shows it, to 4 decimals.</summary>
    private static decimal? Angle(double? degrees) =>
        degrees is { } d && double.IsFinite(d) ? Math.Round((decimal)d, 4) : null;

    /// <summary>A length in centimetres in a row's units: millimetres to 4 decimals, inches to 5.</summary>
    public static decimal FromCm(double cm, HoleUnits units) => units == HoleUnits.Metric
        ? Math.Round((decimal)(cm * 10), 4)
        : Math.Round((decimal)(cm / 2.54), 5);

    private sealed class Matcher(InventorHoleFacts facts, HoleWorkbook workbook, HoleUnits defaultUnits, List<string> notes)
    {
        private HoleUnits[] UnitsOrder => defaultUnits == HoleUnits.Metric
            ? [HoleUnits.Metric, HoleUnits.Imperial]
            : [HoleUnits.Imperial, HoleUnits.Metric];

        private static bool Same(double? cm, decimal value, HoleUnits units) =>
            cm is { } c && Math.Abs(c - HoleRules.ToCm(value, units)) < HoleStateComparison.LengthToleranceCm;

        private static bool SameAngle(double? a, decimal b) => a is { } x && Math.Abs(x - (double)b) < AngleToleranceDeg;

        private static double Distance(double? cm, decimal value, HoleUnits units) =>
            cm is { } c ? Math.Abs(c - HoleRules.ToCm(value, units)) : double.MaxValue;

        private string Mm(double? cm) =>
            cm is { } c ? (c * 10).ToString("0.####", CultureInfo.InvariantCulture) + " mm" : "an unknown size";

        public HoleSpec Spec()
        {
            var termination = facts.Termination ?? HoleTermination.ThroughAll;
            if (facts.Termination is null)
            {
                notes.Add($"The hole ends in a way {Branding.ShortName} Hole does not offer; Termination is Through All.");
            }

            var seat = facts.Seat ?? HoleSeat.None;
            if (facts.Seat is null)
            {
                notes.Add(HoleFeatureMessages.SeatNotOffered);
            }

            // A seated hole with a tolerance on its diameter is read as a seated dowel first (task 099). A spotface
            // follows no fastener, so its hole is read as a hole with no seat is (task 112).
            var spec = facts.Tapped && facts.TaperedThread
                ? TaperTapped(seat, termination)
                : facts.Tapped
                ? Tapped(seat, termination)
                : seat switch
                {
                    HoleSeat.None or HoleSeat.Spotface => DowelOrClearance(termination, seat),
                    _ when facts.HasTolerance && Dowel(termination, seat) is { } dowel => dowel,
                    HoleSeat.Counterbore => Counterbore(termination),
                    _ => Countersink(termination),
                };

            // The seat's own dimensions, on every kind (task 126): the window opens on the seat the hole has.
            spec = WithSeatFromHole(spec, facts) with { Flip = facts.Flip };

            // A Clearance or Simple hole with a tolerance on its diameter opens toleranced (task 113).
            if (facts.HasTolerance && HolePrecision.CanBeToleranced(spec.Kind))
            {
                spec = Toleranced(spec);
            }

            if (spec.IsBlind)
            {
                spec = spec with { DrillPoint = DrillPoint() };
                if (!HoleStandards.IsThreaded(spec.Kind) && facts.DepthCm is { } depth)
                {
                    spec = spec with { Depth = FromCm(depth, spec.Units), DepthExpression = facts.DepthExpression };
                }
            }

            return spec.Normalised();
        }

        private HoleSpec Blank(HoleUnits units, HoleKind kind, HoleSeat seat, HoleTermination termination) =>
            HoleSpec.ClearanceHole(units, "") with { Kind = kind, Seat = seat, Termination = termination };

        private HoleSpec Tapped(HoleSeat seat, HoleTermination termination)
        {
            var designation = facts.ThreadDesignation?.Trim() ?? "";
            var row = workbook.Tapped.FirstOrDefault(r =>
                string.Equals(r.Designation, designation, StringComparison.OrdinalIgnoreCase)
                && (facts.ThreadType is null
                    || string.Equals(r.ThreadType, facts.ThreadType.Trim(), StringComparison.OrdinalIgnoreCase)));

            var units = row?.Units
                ?? (string.Equals(facts.ThreadType?.Trim(), TappedRow.ImperialThreadType, StringComparison.OrdinalIgnoreCase)
                    ? HoleUnits.Imperial
                    : string.Equals(facts.ThreadType?.Trim(), TappedRow.MetricThreadType, StringComparison.OrdinalIgnoreCase)
                        ? HoleUnits.Metric
                        : defaultUnits);

            if (row is null)
            {
                notes.Add(designation.Length == 0
                    ? "The hole's thread could not be read; pick a size."
                    : $"The thread {designation} is not in the Tapped tab; pick a size.");
            }

            var blind = termination == HoleTermination.Distance;
            return Blank(units, HoleKind.Tapped, seat, termination) with
            {
                Size = row?.Thread ?? "",
                Fastener = SameSizeFastener(seat, units, row?.Thread, row?.Nominal),
                // A hole that is not blind has no depths in Inventor; take the row's defaults, as HoleRules does.
                ThreadDepth = blind && facts.ThreadDepthCm is { } thread ? FromCm(thread, units) : row?.ThreadDepth,
                DrillDepth = blind && facts.DepthCm is { } drill ? FromCm(drill, units) : row?.DrillDepth,
                // A depth driven by parameters shows its expression in the window (task 100).
                ThreadDepthExpression = blind && facts.ThreadDepthCm is not null ? facts.ThreadDepthExpression : null,
                DrillDepthExpression = blind && facts.DepthCm is not null ? facts.DepthExpression : null,
            };
        }

        /// <summary>
        /// A plain hole, with no seat or a spotface: a dowel when it has a tolerance and a Dowel row of its diameter, a
        /// clearance hole when a Clearance row at some fit has its diameter, else a Simple hole of its own diameter (task
        /// 112, Default), with a note. A hole whose diameter cannot be read is a Simple hole with an empty Diameter.
        /// </summary>
        private HoleSpec DowelOrClearance(HoleTermination termination, HoleSeat seat)
        {
            if (facts.HasTolerance && Dowel(termination, seat) is { } dowel)
            {
                return dowel;
            }

            if (ClearanceMatch() is { } match)
            {
                return Blank(match.Row.Units, HoleKind.Clearance, seat, termination) with
                {
                    Size = match.Row.Size,
                    ClearanceFit = match.Fit,
                };
            }

            notes.Add(facts.HoleDiameterCm is null
                ? "The hole's diameter could not be read; it is shown as a Simple hole. Enter a diameter."
                : $"No Clearance row has a {Mm(facts.HoleDiameterCm)} hole; it is shown as a Simple hole of its own " +
                  "diameter.");
            return Blank(defaultUnits, HoleKind.Simple, seat, termination) with
            {
                Diameter = Length(facts.HoleDiameterCm, defaultUnits),
                DiameterExpression = facts.DiameterExpression,
            };
        }

        /// <summary>
        /// The Clearance row and fit whose diameter is the hole's, in either units, the default units first, trying
        /// Normal, Close and Loose for each row in workbook order; hidden rows count. Null when there is none.
        /// </summary>
        private (ClearanceRow Row, ClearanceFit Fit)? ClearanceMatch()
        {
            foreach (var units in UnitsOrder)
            {
                foreach (var row in workbook.Clearance.Where(r => r.Units == units))
                {
                    foreach (var fit in new[] { ClearanceFit.Normal, ClearanceFit.Close, ClearanceFit.Loose })
                    {
                        if (Same(facts.HoleDiameterCm, row.Diameter(fit), units))
                        {
                            return (row, fit);
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// A seated clearance hole whose seat matches no row of its tab (task 137; spec 09 question 26, Aidan: a seat
        /// needs no row of its size): read at the size of the Clearance row its through hole matches, with its own seat
        /// (<see cref="WithSeatFromHole"/>). When that size has rows of the seat, the one whose Hole fit gives the hole's
        /// diameter is picked, else the first; when it has none, the hole has no fastener and keeps the matched fit. Null
        /// when no Clearance row has the hole's diameter.
        /// </summary>
        private HoleSpec? SeatOnClearanceSize<T>(
            HoleSeat seat, HoleTermination termination, IReadOnlyList<T> seatRows, Func<T, HoleUnits> units,
            Func<T, string> size, Func<T, string> screw, Func<T, ClearanceFit> holeFit, string tab, double? seatDiameterCm)
        {
            if (ClearanceMatch() is not { } match)
            {
                return null;
            }

            var (clearance, fit) = match;

            var ofSize = seatRows
                .Where(r => units(r) == clearance.Units
                    && string.Equals(size(r).Trim(), clearance.Size.Trim(), StringComparison.OrdinalIgnoreCase))
                .ToList();
            var row = ofSize.FirstOrDefault(r => Same(facts.HoleDiameterCm, clearance.Diameter(holeFit(r)), clearance.Units))
                ?? ofSize.FirstOrDefault();
            var name = tab.ToLowerInvariant();
            notes.Add(row is null
                ? $"No {tab} row has a {Mm(seatDiameterCm)} {name}; the hole is read as {clearance.Size.Trim()}, which " +
                  $"has no {tab} row, with its own {name}."
                : $"No {tab} row has a {Mm(seatDiameterCm)} {name}; the hole's size, {clearance.Size.Trim()} " +
                  $"{screw(row).Trim()}, is picked, and the hole's own {name} is kept.");
            return Blank(clearance.Units, HoleKind.Clearance, seat, termination) with
            {
                Size = clearance.Size,
                Fastener = row is null ? null : screw(row),
                ClearanceFit = fit,
            };
        }

        /// <summary>
        /// A taper tapped hole (task 112): the Taper Tapped row whose Thread type and Size are the hole's thread type and
        /// designation (as Inventor's thread table names them), in either units, the default units first. With no such
        /// row, a note and an empty size. The drill depth is the hole's when blind, else the row's.
        /// </summary>
        private HoleSpec TaperTapped(HoleSeat seat, HoleTermination termination)
        {
            var designation = facts.ThreadDesignation?.Trim() ?? "";
            var threadType = facts.ThreadType?.Trim();
            var row = UnitsOrder
                .SelectMany(u => workbook.TaperTapped.Where(r => r.Units == u))
                .FirstOrDefault(r =>
                    string.Equals(r.Size.Trim(), designation, StringComparison.OrdinalIgnoreCase)
                    && (threadType is null
                        || string.Equals(r.ThreadType.Trim(), threadType, StringComparison.OrdinalIgnoreCase)));
            if (row is null)
            {
                notes.Add(designation.Length == 0
                    ? "The hole's taper thread could not be read; pick a size."
                    : $"The taper thread {designation} is not in the Taper Tapped tab; pick a size.");
            }

            var units = row?.Units ?? defaultUnits;
            var blind = termination == HoleTermination.Distance;
            return Blank(units, HoleKind.TaperTapped, seat, termination) with
            {
                Size = row?.Size ?? "",
                DrillDepth = blind && facts.DepthCm is { } drill ? FromCm(drill, units) : row?.DrillDepth,
                DrillDepthExpression = blind && facts.DepthCm is not null ? facts.DepthExpression : null,
            };
        }

        private HoleSpec? Dowel(HoleTermination termination, HoleSeat seat)
        {
            foreach (var units in UnitsOrder)
            {
                var row = workbook.Dowel.FirstOrDefault(r => r.Units == units && Same(facts.HoleDiameterCm, r.Nominal, units));
                if (row is null)
                {
                    continue;
                }

                var tolerance = workbook.PrecisionTolerances.FirstOrDefault(t => t.Units == units && MatchesTolerance(t, units));
                var custom = tolerance is null ? CustomLimits(units) : null;
                if (tolerance is null)
                {
                    notes.Add(custom is null ? NotInTabDefaultText : NotInTabCustomText);
                }

                // A dowel's seat is its own (task 106): the hole's sizes, not a same-size row's. Limits no row has open as
                // a Custom tolerance (task 113).
                return WithDowelSeatFromHole(
                    Blank(units, HoleKind.Dowel, seat, termination) with
                    {
                        Size = row.Size,
                        PrecisionFit = tolerance?.Fit ?? PrecisionFit.Normal,
                        Tolerance = tolerance?.Name,
                        CustomTolerance = custom is not null,
                        CustomUpper = custom?.Upper,
                        CustomLower = custom?.Lower,
                    },
                    facts);
            }

            return null;
        }

        private const string NotInTabDefaultText =
            "The hole's tolerance is not in the Precision Tol tab; the default tolerance is picked.";

        private const string NotInTabCustomText =
            "The hole's tolerance is not in the Precision Tol tab; it is shown as a Custom tolerance.";

        /// <summary>
        /// A Clearance or Simple hole with a tolerance on its diameter (task 113): toleranced, with the Precision Tol row
        /// of its units, any fit, whose code or limits it has; else, when Inventor gives both deviations and no ISO code
        /// no row has, a Custom tolerance of them; else the default row, with a note.
        /// </summary>
        private HoleSpec Toleranced(HoleSpec spec)
        {
            var units = spec.Units;
            var row = workbook.PrecisionTolerances.FirstOrDefault(t => t.Units == units && MatchesTolerance(t, units));
            if (row is not null)
            {
                return spec with { Toleranced = true, Tolerance = row.Name, PrecisionFit = row.Fit };
            }

            if (CustomLimits(units) is { } custom)
            {
                notes.Add(NotInTabCustomText);
                return spec with
                {
                    Toleranced = true,
                    CustomTolerance = true,
                    CustomUpper = custom.Upper,
                    CustomLower = custom.Lower,
                };
            }

            notes.Add(NotInTabDefaultText);
            var fallback = HoleRules.FindAnyTolerance(workbook, units, null);
            return spec with { Toleranced = true, Tolerance = fallback?.Name, PrecisionFit = fallback?.Fit ?? PrecisionFit.Normal };
        }

        /// <summary>
        /// The hole's deviations in <paramref name="units"/>, for a Custom tolerance: only when Inventor reports both
        /// and no ISO code, and the upper is above the lower; else null.
        /// </summary>
        private (decimal Upper, decimal Lower)? CustomLimits(HoleUnits units)
        {
            if (facts.ToleranceIsoCode is not null
                || facts.ToleranceUpperCm is not { } upperCm || facts.ToleranceLowerCm is not { } lowerCm)
            {
                return null;
            }

            var upper = LimitFromCm(upperCm, units);
            var lower = LimitFromCm(lowerCm, units);
            return upper > lower ? (upper, lower) : null;
        }

        /// <summary>A deviation in centimetres in a row's units: millimetres to 6 decimals, inches to 7.</summary>
        private static decimal LimitFromCm(double cm, HoleUnits units) => units == HoleUnits.Metric
            ? Math.Round((decimal)(cm * 10), 6)
            : Math.Round((decimal)(cm / 2.54), 7);

        private bool MatchesTolerance(PrecisionToleranceRow row, HoleUnits units)
        {
            if (row.Kind == ToleranceKind.Iso)
            {
                return facts.ToleranceIsoCode is { } code && row.IsoCode is { } rowCode
                    && string.Equals(code.Trim(), rowCode.Trim(), StringComparison.OrdinalIgnoreCase);
            }

            return facts.ToleranceIsoCode is null
                && row.Upper is { } upper && row.Lower is { } lower
                && Same(facts.ToleranceUpperCm, upper, units)
                && Same(facts.ToleranceLowerCm, lower, units);
        }

        private HoleSpec Counterbore(HoleTermination termination)
        {
            var candidates = UnitsOrder
                .SelectMany(u => workbook.Counterbore.Where(r => r.Units == u))
                .Where(r => Same(facts.CounterboreDiameterCm, r.CboreDiameter, r.Units)
                    && Same(facts.CounterboreDepthCm, r.CboreDepth, r.Units))
                .ToList();
            var row = candidates.FirstOrDefault(r => ThroughHoleMatches(r.Units, r.Size, r.HoleFit)) ?? candidates.FirstOrDefault();

            if (row is null
                && SeatOnClearanceSize(
                    HoleSeat.Counterbore, termination, workbook.Counterbore, r => r.Units, r => r.Size, r => r.Screw,
                    r => r.HoleFit, HoleWorkbookReader.CounterboreTab, facts.CounterboreDiameterCm) is { } onSize)
            {
                return onSize;
            }

            if (row is null)
            {
                row = workbook.Counterbore
                    .Where(r => r.Units == defaultUnits && !r.Hidden)
                    .OrderBy(r => Distance(facts.CounterboreDiameterCm, r.CboreDiameter, defaultUnits))
                    .FirstOrDefault();
                notes.Add(row is null
                    ? $"No Counterbore row has a {Mm(facts.CounterboreDiameterCm)} counterbore; pick a size."
                    : $"No Counterbore row has a {Mm(facts.CounterboreDiameterCm)} counterbore; the closest, " +
                      $"{row.Size} {row.Screw}, is picked, and the hole's own counterbore is kept.");
            }

            return Blank(row?.Units ?? defaultUnits, HoleKind.Clearance, HoleSeat.Counterbore, termination) with
            {
                Size = row?.Size ?? "",
                Fastener = row?.Screw,
            };
        }

        private HoleSpec Countersink(HoleTermination termination)
        {
            var candidates = UnitsOrder
                .SelectMany(u => workbook.Countersink.Where(r => r.Units == u))
                .Where(r => Same(facts.CountersinkDiameterCm, r.CsinkDiameter, r.Units)
                    && SameAngle(facts.CountersinkAngleDeg, r.AngleDeg))
                .ToList();
            var row = candidates.FirstOrDefault(r => ThroughHoleMatches(r.Units, r.Size, r.HoleFit)) ?? candidates.FirstOrDefault();

            if (row is null
                && SeatOnClearanceSize(
                    HoleSeat.Countersink, termination, workbook.Countersink, r => r.Units, r => r.Size, r => r.Screw,
                    r => r.HoleFit, HoleWorkbookReader.CountersinkTab, facts.CountersinkDiameterCm) is { } onSize)
            {
                return onSize;
            }

            if (row is null)
            {
                row = workbook.Countersink
                    .Where(r => r.Units == defaultUnits && !r.Hidden)
                    .OrderBy(r => Distance(facts.CountersinkDiameterCm, r.CsinkDiameter, defaultUnits))
                    .FirstOrDefault();
                notes.Add(row is null
                    ? $"No Countersink row has a {Mm(facts.CountersinkDiameterCm)} countersink; pick a size."
                    : $"No Countersink row has a {Mm(facts.CountersinkDiameterCm)} countersink; the closest, " +
                      $"{row.Size} {row.Screw}, is picked, and the hole's own countersink is kept.");
            }

            return Blank(row?.Units ?? defaultUnits, HoleKind.Clearance, HoleSeat.Countersink, termination) with
            {
                Size = row?.Size ?? "",
                Fastener = row?.Screw,
            };
        }

        /// <summary>
        /// The fastener of a tapped hole's seat (task 099): the Screw of the Counterbore or Countersink row of
        /// the same size whose seat matches the hole's. When none matches, a note says so and the first row of the same
        /// size is picked as Fastener type; the hole keeps its own seat either way (task 126). With no row of the same
        /// size, null: the seat needs none.
        /// </summary>
        private string? SameSizeFastener(HoleSeat seat, HoleUnits units, string? size, decimal? nominal)
        {
            if (seat == HoleSeat.None || nominal is not { } n)
            {
                return null;
            }

            string? match;
            string? first;
            if (seat == HoleSeat.Counterbore)
            {
                var rows = HoleRules.SameSizeCounterbores(workbook, units, n);
                match = rows.FirstOrDefault(r => Same(facts.CounterboreDiameterCm, r.CboreDiameter, units)
                    && Same(facts.CounterboreDepthCm, r.CboreDepth, units))?.Screw;
                first = rows.FirstOrDefault()?.Screw;
            }
            else
            {
                var rows = HoleRules.SameSizeCountersinks(workbook, units, n);
                match = rows.FirstOrDefault(r => Same(facts.CountersinkDiameterCm, r.CsinkDiameter, units)
                    && SameAngle(facts.CountersinkAngleDeg, r.AngleDeg))?.Screw;
                first = rows.FirstOrDefault()?.Screw;
            }

            if (match is null && first is not null)
            {
                var tab = seat == HoleSeat.Counterbore ? HoleWorkbookReader.CounterboreTab : HoleWorkbookReader.CountersinkTab;
                notes.Add(
                    $"No {tab} row the size of {size} matches the hole's {tab.ToLowerInvariant()}; the first, {first}, " +
                    $"is picked as Fastener type, and the hole's own {tab.ToLowerInvariant()} is kept.");
            }

            return match ?? first;
        }

        private bool ThroughHoleMatches(HoleUnits units, string size, ClearanceFit fit) =>
            workbook.Clearance.Any(c => c.Units == units
                && string.Equals(c.Size.Trim(), size.Trim(), StringComparison.OrdinalIgnoreCase)
                && Same(facts.HoleDiameterCm, c.Diameter(fit), units));

        private string? DrillPoint()
        {
            var row = facts.FlatBottom
                ? workbook.DrillPoints.FirstOrDefault(r => r.IsFlat)
                : workbook.DrillPoints.FirstOrDefault(r => r.AngleDeg is { } a && SameAngle(facts.DrillPointAngleDeg, a));
            if (row is null)
            {
                notes.Add(facts.FlatBottom
                    ? "The Drill Point tab has no flat bottom; the default drill point is picked."
                    : "The Drill Point tab has no row for the hole's drill point angle; the default is picked.");
            }

            return row?.Name;
        }
    }
}
