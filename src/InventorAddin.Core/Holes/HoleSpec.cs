namespace InventorAddin.Core.Holes;

/// <summary>
/// What kind of AWB hole it is (spec 09, "Types and seats"). In the window a dowel or precision hole is Type
/// Clearance with Standard AWB Dowel (spec 09 question 3); Inventor models it as a plain drilled hole.
/// </summary>
public enum HoleKind
{
    Clearance,
    Tapped,
    Dowel,
}

/// <summary>The seat of a hole, as Inventor's: none, a counterbore or a countersink.</summary>
public enum HoleSeat
{
    None,
    Counterbore,
    Countersink,
}

/// <summary>How the hole ends, as Inventor's: at a depth, through everything, or at a face picked in Inventor.</summary>
public enum HoleTermination
{
    Distance,
    ThroughAll,
    ToFace,
}

/// <summary>
/// The Type and Standard names the window shows (spec 09, "The window"), and how they map onto <see cref="HoleKind"/>.
/// Type Clearance lists AWB Clearance and AWB Dowel; Type Tapped lists AWB Tapped.
/// </summary>
public static class HoleStandards
{
    public const string ClearanceType = "Clearance";
    public const string TappedType = "Tapped";

    public const string ClearanceStandard = Branding.ShortName + " Clearance";
    public const string TappedStandard = Branding.ShortName + " Tapped";
    public const string DowelStandard = Branding.ShortName + " Dowel";

    /// <summary>The window's Type for a kind: Tapped for tapped holes, Clearance for clearance and dowel holes.</summary>
    public static string TypeName(HoleKind kind) => kind == HoleKind.Tapped ? TappedType : ClearanceType;

    /// <summary>The window's Standard for a kind.</summary>
    public static string StandardName(HoleKind kind) => kind switch
    {
        HoleKind.Clearance => ClearanceStandard,
        HoleKind.Tapped => TappedStandard,
        HoleKind.Dowel => DowelStandard,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    /// <summary>The Standards listed for a Type, in window order.</summary>
    public static IReadOnlyList<string> StandardsFor(string type) =>
        string.Equals(type, TappedType, StringComparison.OrdinalIgnoreCase)
            ? [TappedStandard]
            : [ClearanceStandard, DowelStandard];

    /// <summary>The kind for a Standard name, or null when it is not one of ours.</summary>
    public static HoleKind? KindFor(string? standard)
    {
        foreach (var kind in Enum.GetValues<HoleKind>())
        {
            if (string.Equals(standard?.Trim(), StandardName(kind), StringComparison.OrdinalIgnoreCase))
            {
                return kind;
            }
        }

        return null;
    }
}

/// <summary>
/// The choices made for one AWB hole in the window (spec 09, "The window"). Lengths are in the units chosen at the
/// top: millimetres for <see cref="HoleUnits.Metric"/>, inches for <see cref="HoleUnits.Imperial"/>, as in the
/// workbook. <see cref="HoleRules"/> turns a spec and the workbook into <see cref="HoleGeometry"/>.
/// </summary>
/// <param name="Units">Metric or Imperial; picks the workbook rows.</param>
/// <param name="Kind">Clearance, Tapped or Dowel.</param>
/// <param name="Seat">None, Counterbore or Countersink. Dowel holes have no seat.</param>
/// <param name="Size">
/// The size key: the Size of a Clearance, Counterbore, Countersink or Dowel row, or the Thread of a Tapped row
/// (as written in the workbook, <c>M6</c>, <c>#10-32 UNF</c>).
/// </param>
/// <param name="Fastener">
/// Seated clearance holes: the Screw of the Counterbore or Countersink row (<c>SHCS</c>). Null takes the first row
/// of the size, in workbook order.
/// </param>
/// <param name="ClearanceFit">Clearance holes with no seat: the Clearance column used. Seated holes take the row's Hole fit.</param>
/// <param name="PrecisionFit">Dowel holes: Slip, Normal or Press.</param>
/// <param name="Tolerance">
/// Dowel holes: the Name of the Precision Tol row, for the units and fit. Null takes the row marked Default, else
/// the first.
/// </param>
/// <param name="ThreadDepth">Tapped holes: the thread depth; null takes the Tapped row's default.</param>
/// <param name="DrillDepth">Tapped holes: the drill depth, the hole's depth when blind; null takes the row's default.</param>
/// <param name="Termination">Distance, Through All or To a face.</param>
/// <param name="Depth">Clearance and dowel holes ending at a distance: the hole depth.</param>
/// <param name="DrillPoint">
/// Blind holes: the Name of the Drill Point row. Null takes the row marked Default, else the first.
/// </param>
/// <param name="Flip">The direction is flipped, as Inventor's Flip.</param>
public sealed record HoleSpec(
    HoleUnits Units,
    HoleKind Kind,
    HoleSeat Seat,
    string Size,
    string? Fastener,
    ClearanceFit ClearanceFit,
    PrecisionFit PrecisionFit,
    string? Tolerance,
    decimal? ThreadDepth,
    decimal? DrillDepth,
    HoleTermination Termination,
    decimal? Depth,
    string? DrillPoint,
    bool Flip)
{
    /// <summary>A clearance hole with no seat, Normal fit, through all; the other choices at their defaults.</summary>
    public static HoleSpec ClearanceHole(HoleUnits units, string size) => new(
        units, HoleKind.Clearance, HoleSeat.None, size, null, ClearanceFit.Normal, PrecisionFit.Normal, null,
        null, null, HoleTermination.ThroughAll, null, null, false);

    /// <summary>True when the hole ends at a distance and so has a bottom.</summary>
    public bool IsBlind => Termination == HoleTermination.Distance;

    /// <summary>
    /// The same spec with every choice that does not apply to its kind, seat and termination put back to its default
    /// (null, or Normal for the fits), so two specs that build the same hole compare equal. This is what
    /// <see cref="HoleStoredData"/> stores.
    /// </summary>
    public HoleSpec Normalised()
    {
        var seated = Kind == HoleKind.Clearance && Seat != HoleSeat.None;
        return this with
        {
            Seat = Kind == HoleKind.Dowel ? HoleSeat.None : Seat,
            Size = Size.Trim(),
            Fastener = seated ? Fastener?.Trim() : null,
            ClearanceFit = Kind == HoleKind.Clearance && Seat == HoleSeat.None ? ClearanceFit : ClearanceFit.Normal,
            PrecisionFit = Kind == HoleKind.Dowel ? PrecisionFit : PrecisionFit.Normal,
            Tolerance = Kind == HoleKind.Dowel ? Tolerance?.Trim() : null,
            ThreadDepth = Kind == HoleKind.Tapped ? ThreadDepth : null,
            DrillDepth = Kind == HoleKind.Tapped ? DrillDepth : null,
            Depth = IsBlind && Kind != HoleKind.Tapped ? Depth : null,
            DrillPoint = IsBlind ? DrillPoint?.Trim() : null,
        };
    }
}
