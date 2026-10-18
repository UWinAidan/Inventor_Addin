using System.Globalization;

namespace InventorAddin.Core.Holes;

/// <summary>
/// What the add-in reads from a hole feature in Inventor, for edit mode (task 088). Lengths are in centimetres and
/// angles in degrees; a value Inventor does not report for the hole is null.
/// </summary>
/// <param name="Seat">None, Counterbore or Countersink; null for a seat AWB Hole does not offer (a spot face).</param>
/// <param name="Tapped">The hole is threaded.</param>
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

        if (stored?.Data is { } data)
        {
            return new HoleReadBackResult(data.Spec, HoleSource.Awb, data, []);
        }

        var notes = new List<string>();
        if (stored is not null)
        {
            notes.Add(stored.Message);
        }

        var spec = new Matcher(facts, workbook, defaultUnits, notes).Spec();
        return new HoleReadBackResult(spec, HoleSource.Inventor, null, notes.AsReadOnly());
    }

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
                notes.Add(HoleFeatureMessages.SpotFaceNotOffered);
            }

            // A seated hole with a tolerance on its diameter is read as a seated dowel first (task 099).
            var spec = facts.Tapped
                ? Tapped(seat, termination)
                : seat switch
                {
                    HoleSeat.None => DowelOrClearance(termination),
                    _ when facts.HasTolerance && Dowel(termination, seat) is { } dowel => dowel,
                    HoleSeat.Counterbore => Counterbore(termination),
                    _ => Countersink(termination),
                };

            spec = spec with { Flip = facts.Flip };
            if (spec.IsBlind)
            {
                spec = spec with { DrillPoint = DrillPoint() };
                if (spec.Kind != HoleKind.Tapped && facts.DepthCm is { } depth)
                {
                    spec = spec with { Depth = FromCm(depth, spec.Units) };
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
            };
        }

        private HoleSpec DowelOrClearance(HoleTermination termination)
        {
            if (facts.HasTolerance && Dowel(termination, HoleSeat.None) is { } dowel)
            {
                return dowel;
            }

            foreach (var units in UnitsOrder)
            {
                foreach (var row in workbook.Clearance.Where(r => r.Units == units))
                {
                    foreach (var fit in new[] { ClearanceFit.Normal, ClearanceFit.Close, ClearanceFit.Loose })
                    {
                        if (Same(facts.HoleDiameterCm, row.Diameter(fit), units))
                        {
                            return Blank(units, HoleKind.Clearance, HoleSeat.None, termination) with
                            {
                                Size = row.Size,
                                ClearanceFit = fit,
                            };
                        }
                    }
                }
            }

            var closest = workbook.Clearance
                .Where(r => r.Units == defaultUnits && !r.Hidden)
                .SelectMany(r => Enum.GetValues<ClearanceFit>().Select(f => (Row: r, Fit: f)))
                .OrderBy(c => Distance(facts.HoleDiameterCm, c.Row.Diameter(c.Fit), defaultUnits))
                .FirstOrDefault();

            if (closest.Row is null)
            {
                notes.Add($"No Clearance row has a {Mm(facts.HoleDiameterCm)} hole; pick a size.");
                return Blank(defaultUnits, HoleKind.Clearance, HoleSeat.None, termination);
            }

            notes.Add(
                $"No Clearance row has a {Mm(facts.HoleDiameterCm)} hole; the closest, {closest.Row.Size} " +
                $"{closest.Fit}, is picked.");
            return Blank(defaultUnits, HoleKind.Clearance, HoleSeat.None, termination) with
            {
                Size = closest.Row.Size,
                ClearanceFit = closest.Fit,
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
                if (tolerance is null)
                {
                    notes.Add("The hole's tolerance is not in the Precision Tol tab; the default tolerance is picked.");
                }

                return Blank(units, HoleKind.Dowel, seat, termination) with
                {
                    Size = row.Size,
                    Fastener = SameSizeFastener(seat, units, row.Size, row.Nominal),
                    PrecisionFit = tolerance?.Fit ?? PrecisionFit.Normal,
                    Tolerance = tolerance?.Name,
                };
            }

            return null;
        }

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

            if (row is null)
            {
                row = workbook.Counterbore
                    .Where(r => r.Units == defaultUnits && !r.Hidden)
                    .OrderBy(r => Distance(facts.CounterboreDiameterCm, r.CboreDiameter, defaultUnits))
                    .FirstOrDefault();
                notes.Add(row is null
                    ? $"No Counterbore row has a {Mm(facts.CounterboreDiameterCm)} counterbore; pick a size."
                    : $"No Counterbore row has a {Mm(facts.CounterboreDiameterCm)} counterbore; the closest, " +
                      $"{row.Size} {row.Screw}, is picked.");
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

            if (row is null)
            {
                row = workbook.Countersink
                    .Where(r => r.Units == defaultUnits && !r.Hidden)
                    .OrderBy(r => Distance(facts.CountersinkDiameterCm, r.CsinkDiameter, defaultUnits))
                    .FirstOrDefault();
                notes.Add(row is null
                    ? $"No Countersink row has a {Mm(facts.CountersinkDiameterCm)} countersink; pick a size."
                    : $"No Countersink row has a {Mm(facts.CountersinkDiameterCm)} countersink; the closest, " +
                      $"{row.Size} {row.Screw}, is picked.");
            }

            return Blank(row?.Units ?? defaultUnits, HoleKind.Clearance, HoleSeat.Countersink, termination) with
            {
                Size = row?.Size ?? "",
                Fastener = row?.Screw,
            };
        }

        /// <summary>
        /// The fastener of a tapped or dowel hole's seat (task 099): the Screw of the Counterbore or Countersink row of
        /// the same size whose seat matches the hole's. When none matches, a note says so and the first row of the same
        /// size is picked; with no row of the same size, null (the window says why the seat cannot be kept).
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
                notes.Add($"No {tab} row the size of {size} matches the hole's {tab.ToLowerInvariant()}; the first, {first}, is picked.");
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
