using InventorAddin.Core.Holes;

namespace InventorAddin.Core.HoleTable;

/// <summary>
/// The type of a hole as the hole table sees it, in the order the clearance table sorts them (Aidan, 2026-11-12, spec 04
/// questions 6 and 9). A thread decides the type before a seat does (question 10, Default).
/// </summary>
public enum HoleTableType
{
    /// <summary>Simple, Clearance and dowel holes with no seat.</summary>
    Plain,
    Counterbore,
    Countersink,
    Spotface,
    Tapped,
    TaperTapped,
}

/// <summary>
/// One hole centre of a hole feature, as the add-in reads it: the centre sketch point's reference key (stable across
/// model changes, spike 160 C11), its 1-based index in the feature's centre list (display and logs only), and the
/// centre on the sheet in centimetres.
/// </summary>
public sealed record HoleTableCentre(string ReferenceKey, int Index, double SheetXCm, double SheetYCm);

/// <summary>
/// One hole instance as the hole table sees it (spec 04, "Decided behaviour"). Lengths are in centimetres and angles in
/// degrees; a value the hole does not have is null.
/// </summary>
/// <param name="Id">
/// Stable across model changes: the feature name and the centre's reference key (<c>Plate Hole 3#key</c>). The centre
/// index shifts when a centre is removed, the key does not (Default, DECISIONS.md 2026-11-17).
/// </param>
/// <param name="CentreIndex">The 1-based index in the feature's centre list, for display and logs only.</param>
/// <param name="Units">The hole's own units: the AWB spec's, else the drawing's.</param>
/// <param name="DrillDiameterCm">
/// The diameter the hole sorts and is described by (spec 04 question 11): the through hole, or the tap drill. A taper
/// tapped hole has none in Inventor, so it takes the AWB Taper Tapped row's tap drill, else null. A straight tapped
/// hole takes, in order: Inventor's hole diameter (the stored one when they agree), the AWB stored data's applied hole
/// diameter while it fits the actual thread, the AWB Tapped row's tap drill (task 182); the thread's own diameter
/// parameter may be unreadable.
/// </param>
/// <param name="SeatDiameterCm">The counterbore, countersink or spotface diameter.</param>
/// <param name="SeatDepthCm">The counterbore or spotface depth; null for a countersink.</param>
/// <param name="SeatAngleDeg">The countersink included angle.</param>
/// <param name="ThreadDepthCm">Null for a full-depth thread.</param>
/// <param name="SheetXCm">The hole centre on the sheet; task 157 turns it into table coordinates.</param>
public sealed record HoleTableHole(
    string Id,
    int CentreIndex,
    string FeatureName,
    HoleUnits Units,
    HoleTableType Type,
    bool IsPrecision,
    double? DrillDiameterCm,
    HoleTermination? Termination,
    double? DepthCm,
    bool FlatBottom,
    double? DrillPointAngleDeg,
    double? SeatDiameterCm,
    double? SeatDepthCm,
    double? SeatAngleDeg,
    string? ThreadDesignation,
    string? ThreadType,
    double? ThreadDepthCm,
    bool TaperedThread,
    HoleTolerance? Tolerance,
    double SheetXCm,
    double SheetYCm)
{
    /// <summary>The seat kind as Inventor reports it, so a tapped hole's seat is described by its own kind (task 156); null falls back to the table type.</summary>
    public HoleSeat? Seat { get; init; }

    /// <summary>The thread class (<c>6H</c>) for the description's thread line (task 156); null when not read. Set by the add-in side (a later task).</summary>
    public string? ThreadClass { get; init; }

    /// <summary>Taper tapped holes: the engagement length when known (task 156), in centimetres; null when not read.</summary>
    public double? TaperEngagementCm { get; init; }
}

/// <summary>Builds <see cref="HoleTableHole"/> instances from what the add-in reads for a hole feature.</summary>
public static class HoleTableHoles
{
    /// <summary>
    /// The instances of one hole feature, one per centre, in the order given.
    /// </summary>
    /// <param name="featureName">The hole feature's name.</param>
    /// <param name="facts">What was read from the feature.</param>
    /// <param name="awbSpec">The stored AWB spec, or null when the hole has none that can be read.</param>
    /// <param name="units">The hole's own units: the AWB spec's, else the drawing's.</param>
    /// <param name="centres">The centres, each with its reference key, index and sheet position.</param>
    /// <param name="taperTappedRow">
    /// The AWB Taper Tapped row of the hole's thread, when the spec and workbook give one; its tap drill (converted to
    /// centimetres here) is the drill diameter of a taper tapped hole, which has none in Inventor (spike 108).
    /// </param>
    /// <param name="tolerance">The diameter tolerance, when the hole has one.</param>
    /// <param name="storedHoleDiameterCm">
    /// The applied hole diameter in the AWB stored data (the tap drill AWB Hole modelled), when the hole has it. For a
    /// straight tapped hole it is used when Inventor's diameter did not read, and only while it still describes the
    /// actual thread: it is stale when <paramref name="tappedRow"/> (the actual thread's row) is given and its tap drill
    /// differs from it beyond <see cref="HoleStateComparison.LengthToleranceCm"/> (task 182). When Inventor's diameter
    /// read and agrees with it within that tolerance, the stored value is kept as is.
    /// </param>
    /// <param name="tappedRow">
    /// The AWB Tapped row of the hole's actual thread; its tap drill (converted to centimetres here) is the last choice
    /// for a straight tapped hole, after Inventor's diameter and a non-stale stored one (task 182, order of task 179
    /// reversed), and is what makes a stored diameter stale.
    /// </param>
    public static IReadOnlyList<HoleTableHole> From(
        string featureName,
        InventorHoleFacts facts,
        HoleSpec? awbSpec,
        HoleUnits units,
        IReadOnlyList<HoleTableCentre> centres,
        TaperTappedRow? taperTappedRow = null,
        HoleTolerance? tolerance = null,
        double? storedHoleDiameterCm = null,
        TappedRow? tappedRow = null)
    {
        ArgumentNullException.ThrowIfNull(featureName);
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentNullException.ThrowIfNull(centres);

        var precision = HolePrecision.IsPrecision(awbSpec, facts.HasTolerance);
        var type = TypeOf(facts);

        var drill = facts.HoleDiameterCm;
        if (type == HoleTableType.TaperTapped && taperTappedRow is not null)
        {
            // Inventor gives a taper tapped hole no drill diameter, and its figure differs from AWB's for some sizes
            // (spike 160); AWB's row wins when given (spec 04 question 11).
            drill = HoleRules.ToCm(taperTappedRow.TapDrill, taperTappedRow.Units);
        }
        else if (type == HoleTableType.Tapped)
        {
            // The drill the hole actually has (task 182): Inventor's diameter when it reads (the stored one when they agree,
            // so an unchanged AWB hole prints as before); else the stored diameter while it still fits the actual thread;
            // else the row's tap drill. The thread's diameter parameter may not read (task 170).
            double? rowDrill = tappedRow is null ? null : HoleRules.ToCm(tappedRow.TapDrill, tappedRow.Units);
            if (facts.HoleDiameterCm is { } inventorCm)
            {
                drill = storedHoleDiameterCm is { } s && Math.Abs(s - inventorCm) < HoleStateComparison.LengthToleranceCm
                    ? s
                    : inventorCm;
            }
            else
            {
                // Stale when the actual thread's row has a tap drill the stored diameter does not match.
                bool stale = storedHoleDiameterCm is { } sd && rowDrill is { } rd
                    && Math.Abs(sd - rd) >= HoleStateComparison.LengthToleranceCm;
                drill = storedHoleDiameterCm is not null && !stale ? storedHoleDiameterCm : rowDrill;
            }
        }

        double? seatDiameter = null, seatDepth = null, seatAngle = null;
        switch (facts.Seat)
        {
            case HoleSeat.Counterbore:
                seatDiameter = facts.CounterboreDiameterCm;
                seatDepth = facts.CounterboreDepthCm;
                break;
            case HoleSeat.Countersink:
                seatDiameter = facts.CountersinkDiameterCm;
                seatAngle = facts.CountersinkAngleDeg;
                break;
            case HoleSeat.Spotface:
                seatDiameter = facts.SpotfaceDiameterCm;
                seatDepth = facts.SpotfaceDepthCm;
                break;
        }

        return centres.Select(c => new HoleTableHole(
            $"{featureName}#{c.ReferenceKey}",
            c.Index,
            featureName,
            units,
            type,
            precision,
            drill,
            facts.Termination,
            facts.DepthCm,
            facts.FlatBottom,
            facts.DrillPointAngleDeg,
            seatDiameter,
            seatDepth,
            seatAngle,
            facts.ThreadDesignation,
            facts.ThreadType,
            facts.Tapped ? facts.ThreadDepthCm : null,
            facts.Tapped && facts.TaperedThread,
            tolerance,
            c.SheetXCm,
            c.SheetYCm) { Seat = facts.Seat }).ToList().AsReadOnly();
    }

    /// <summary>
    /// The table type: a thread decides first (spec 04 question 10, Default): tapered is <see cref="HoleTableType.TaperTapped"/>,
    /// else <see cref="HoleTableType.Tapped"/>. Otherwise the seat; no seat is <see cref="HoleTableType.Plain"/>. A
    /// dowel is Plain, or its seat's type.
    /// </summary>
    public static HoleTableType TypeOf(InventorHoleFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);

        if (facts.Tapped)
        {
            return facts.TaperedThread ? HoleTableType.TaperTapped : HoleTableType.Tapped;
        }

        return facts.Seat switch
        {
            HoleSeat.Counterbore => HoleTableType.Counterbore,
            HoleSeat.Countersink => HoleTableType.Countersink,
            HoleSeat.Spotface => HoleTableType.Spotface,
            _ => HoleTableType.Plain,
        };
    }
}
