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

    /// <summary>A plain hole of a typed diameter, with no standard or size (task 112; spec 09, "Simple holes").</summary>
    Simple,

    /// <summary>A tapered pipe thread from the Taper Tapped tab (task 112; spec 09, "Taper tapped holes").</summary>
    TaperTapped,
}

/// <summary>The seat of a hole, as Inventor's: none, a counterbore, a countersink or a spotface (task 112).</summary>
public enum HoleSeat
{
    None,
    Counterbore,
    Countersink,
    Spotface,
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
/// Type Clearance lists AWB Clearance and AWB Dowel; Type Tapped lists AWB Tapped; Type Taper Tapped lists AWB Taper
/// Tapped; Type Simple has no standard (task 112).
/// </summary>
public static class HoleStandards
{
    public const string ClearanceType = "Clearance";
    public const string TappedType = "Tapped";
    public const string SimpleType = "Simple";
    public const string TaperTappedType = "Taper Tapped";

    public const string ClearanceStandard = Branding.ShortName + " Clearance";
    public const string TappedStandard = Branding.ShortName + " Tapped";
    public const string DowelStandard = Branding.ShortName + " Dowel";
    public const string TaperTappedStandard = Branding.ShortName + " Taper Tapped";

    /// <summary>The window's Types, in window order: Simple first (spec 09, "The window"; Aidan, 2026-11-09, task 144).</summary>
    public static IReadOnlyList<string> Types { get; } = [SimpleType, ClearanceType, TappedType, TaperTappedType];

    /// <summary>The window's Type for a kind: a dowel is the Clearance type (spec 09 question 3).</summary>
    public static string TypeName(HoleKind kind) => kind switch
    {
        HoleKind.Tapped => TappedType,
        HoleKind.Simple => SimpleType,
        HoleKind.TaperTapped => TaperTappedType,
        _ => ClearanceType,
    };

    /// <summary>The window's Standard for a kind; <c>""</c> for a Simple hole, which has none.</summary>
    public static string StandardName(HoleKind kind) => kind switch
    {
        HoleKind.Clearance => ClearanceStandard,
        HoleKind.Tapped => TappedStandard,
        HoleKind.Dowel => DowelStandard,
        HoleKind.TaperTapped => TaperTappedStandard,
        HoleKind.Simple => "",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    /// <summary>The Standards listed for a Type, in window order; none for Simple.</summary>
    public static IReadOnlyList<string> StandardsFor(string type) => (type ?? "").Trim() switch
    {
        var t when Same(t, TappedType) => [TappedStandard],
        var t when Same(t, TaperTappedType) => [TaperTappedStandard],
        var t when Same(t, SimpleType) => [],
        _ => [ClearanceStandard, DowelStandard],
    };

    /// <summary>The kind for a Type and Standard: Simple for the Simple type, else the Standard's kind.</summary>
    public static HoleKind? KindFor(string? type, string? standard) =>
        Same(type?.Trim(), SimpleType) ? HoleKind.Simple : KindFor(standard);

    /// <summary>The kind for a Standard name, or null when it is not one of ours.</summary>
    public static HoleKind? KindFor(string? standard)
    {
        if (string.IsNullOrWhiteSpace(standard))
        {
            return null;
        }

        foreach (var kind in Enum.GetValues<HoleKind>())
        {
            if (kind != HoleKind.Simple
                && string.Equals(standard.Trim(), StandardName(kind), StringComparison.OrdinalIgnoreCase))
            {
                return kind;
            }
        }

        return null;
    }

    /// <summary>
    /// True when a hole of <paramref name="kind"/> can take <paramref name="seat"/>: every seat on every kind, except a
    /// counterbore on a taper tapped hole, which Inventor does not allow (Aidan, 2026-10-28; spike 108).
    /// </summary>
    public static bool Takes(HoleKind kind, HoleSeat seat) =>
        !(kind == HoleKind.TaperTapped && seat == HoleSeat.Counterbore);

    /// <summary>True for the threaded kinds: Tapped and Taper Tapped.</summary>
    public static bool IsThreaded(HoleKind kind) => kind is HoleKind.Tapped or HoleKind.TaperTapped;

    private static bool Same(string? a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// The choices made for one AWB hole in the window (spec 09, "The window"). Lengths are in the units chosen at the
/// top: millimetres for <see cref="HoleUnits.Metric"/>, inches for <see cref="HoleUnits.Imperial"/>, as in the
/// workbook. <see cref="HoleRules"/> turns a spec and the workbook into <see cref="HoleGeometry"/>.
/// </summary>
/// <param name="Units">Metric or Imperial; picks the workbook rows.</param>
/// <param name="Kind">Clearance, Tapped, Dowel, Simple or Taper Tapped (task 112).</param>
/// <param name="Seat">
/// None, Counterbore or Countersink, for every kind (spec 09, "Types and seats"). The seat is described by its own
/// <see cref="CboreDiameter"/>, <see cref="CboreDepth"/>, <see cref="CsinkDiameter"/> and <see cref="CsinkAngleDeg"/>,
/// the window's seat boxes, on every kind (task 126; a dowel's since task 106). On a clearance or tapped hole a value
/// left null is taken from the Counterbore or Countersink row for the <see cref="Fastener"/> and size (a tapped hole's
/// of the same size, <see cref="HoleRules.SameSizeCounterbores"/>; task 099), so a spec stored before task 126, which
/// names only the row, resolves to the row's dimensions.
/// </param>
/// <param name="Size">
/// The size key: the Size of a Clearance, Counterbore, Countersink, Dowel or Taper Tapped row, or the Thread of a
/// Tapped row (as written in the workbook, <c>M6</c>, <c>#10-32 UNF</c>, <c>1/4</c>). <c>""</c> for a Simple hole,
/// whose size is its <see cref="Diameter"/>.
/// </param>
/// <param name="Fastener">
/// Seated clearance and tapped holes: the Screw of the Counterbore or Countersink row (<c>SHCS</c>), which gives the
/// seat's starting values (task 126) and, for a clearance hole, the through hole. Null takes the first row of the size
/// (for a tapped hole, the first row of the same size), in workbook order; a tapped hole with no row of its size has
/// none, and so has a clearance hole with no row of its size (task 137). Not used for a dowel.
/// </param>
/// <param name="ClearanceFit">
/// Clearance holes with no seat or a spotface, and seated clearance holes with no Fastener because their size has no
/// row of the seat (task 137): the Clearance column used. A seated hole with a row takes the row's Hole fit.
/// </param>
/// <param name="PrecisionFit">
/// Dowel holes: Slip, Normal or Press. Toleranced Clearance and Simple holes (task 113): the fit of the Precision Tol
/// row chosen, which picks its colour and, when two rows of the units share a name, the row; Normal for Custom.
/// </param>
/// <param name="Tolerance">
/// Dowel holes: the Name of the Precision Tol row, for the units and fit. Toleranced Clearance and Simple holes (task
/// 113): the Name of any Precision Tol row for the units. Null takes the row marked Default, else the first; null too
/// for a Custom tolerance (<see cref="CustomTolerance"/>).
/// </param>
/// <param name="ThreadDepth">Tapped holes: the thread depth; null takes the Tapped row's default. Not on a taper
/// tapped hole, whose thread Inventor sets (spec 09 question 22).</param>
/// <param name="DrillDepth">Tapped and taper tapped holes: the drill depth, the hole's depth when blind; null takes the
/// row's default.</param>
/// <param name="Termination">Distance, Through All or To a face.</param>
/// <param name="Depth">Clearance, dowel and Simple holes ending at a distance: the hole depth.</param>
/// <param name="DrillPoint">
/// Blind holes: the Name of the Drill Point row. Null takes the row marked Default, else the first.
/// </param>
/// <param name="Flip">The direction is flipped, as Inventor's Flip.</param>
/// <remarks>
/// A depth typed as an Inventor expression (<c>d0</c>, <c>1.5*d0+1</c>, <c>12 mm</c>; task 100) keeps its text in
/// <see cref="ThreadDepthExpression"/>, <see cref="DrillDepthExpression"/> or <see cref="DepthExpression"/>, beside
/// its value: the depth field holds what the expression came to when it was checked, in the spec's units, and every
/// check uses that value. The add-in writes the expression into the hole's parameter, so the hole stays parametric.
/// An expression is null for a depth typed as a plain number.
/// </remarks>
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

    /// <summary>Tapped holes: the expression <see cref="ThreadDepth"/> came from, or null for a plain number.</summary>
    public string? ThreadDepthExpression { get; init; }

    /// <summary>
    /// Through All tapped holes: the Full depth checkbox (Aidan, 2026-10-28; task 136; spec 09, "Tapped holes"), ticked
    /// by default. Ticked, the thread runs the whole hole; unticked, it stops at <see cref="ThreadDepth"/>, below the
    /// seat as a blind hole's. True on every other hole: a blind tapped hole always has its thread depth, a hole that
    /// ends at a face is threaded its full depth, and a taper tapped hole's thread Inventor sets (spec 09 question 22).
    /// </summary>
    public bool FullDepth { get; init; } = true;

    /// <summary>
    /// True for a tapped hole whose thread stops at <see cref="ThreadDepth"/>: a blind one, or a Through All one with
    /// <see cref="FullDepth"/> unticked (task 136).
    /// </summary>
    public bool HasThreadDepth =>
        Kind == HoleKind.Tapped && (IsBlind || (Termination == HoleTermination.ThroughAll && !FullDepth));

    /// <summary>True when the Full depth checkbox applies: a tapped hole that ends Through All (task 136).</summary>
    public bool TakesFullDepth => Kind == HoleKind.Tapped && Termination == HoleTermination.ThroughAll;

    /// <summary>Tapped holes: the expression <see cref="DrillDepth"/> came from, or null for a plain number.</summary>
    public string? DrillDepthExpression { get; init; }

    /// <summary>Clearance and dowel holes: the expression <see cref="Depth"/> came from, or null for a plain number.</summary>
    public string? DepthExpression { get; init; }

    /// <summary>
    /// Simple holes: the hole diameter typed in the Diameter box, in the spec's units (task 112; spec 09, "Simple
    /// holes"). Null for every other kind, and while the box is empty.
    /// </summary>
    public decimal? Diameter { get; init; }

    /// <summary>Simple holes: the expression <see cref="Diameter"/> came from, or null for a plain number.</summary>
    public string? DiameterExpression { get; init; }

    // The seat's dimensions, the window's seat boxes (spec 09, "The window": Seat sizes): a dowel's since task 106,
    // every kind's since task 126. In the spec's units; the angle in degrees. A dowel has no Fastener, so a null leaves
    // its box empty and HoleRules asks for a value; a clearance or tapped hole takes a null from its row, and asks only
    // when there is none.

    /// <summary>Holes with a counterbore: its diameter.</summary>
    public decimal? CboreDiameter { get; init; }

    /// <summary>Holes with a counterbore: its depth.</summary>
    public decimal? CboreDepth { get; init; }

    /// <summary>Holes with a counterbore: the expression <see cref="CboreDepth"/> came from, or null for a number.</summary>
    public string? CboreDepthExpression { get; init; }

    /// <summary>Holes with a countersink: its diameter.</summary>
    public decimal? CsinkDiameter { get; init; }

    /// <summary>Holes with a countersink: its included angle, in degrees.</summary>
    public decimal? CsinkAngleDeg { get; init; }

    /// <summary>Holes with a spotface (task 112): its diameter.</summary>
    public decimal? SpotfaceDiameter { get; init; }

    /// <summary>Holes with a spotface (task 112): its depth; 0 is allowed (spike 108).</summary>
    public decimal? SpotfaceDepth { get; init; }

    /// <summary>Holes with a spotface: the expression <see cref="SpotfaceDepth"/> came from, or null for a number.</summary>
    public string? SpotfaceDepthExpression { get; init; }

    /// <summary>
    /// Clearance (AWB Clearance) and Simple holes: the Toleranced checkbox (task 113; spec 09, "Toleranced clearance
    /// and Simple holes"). A toleranced hole takes a <see cref="Tolerance"/> row from any fit, or a Custom tolerance,
    /// on its modelled diameter, and is a precision hole (<see cref="HolePrecision"/>). Ignored on other kinds.
    /// </summary>
    public bool Toleranced { get; init; }

    /// <summary>
    /// Dowels and toleranced holes: the Custom tolerance is chosen (task 113; spec 09 questions 17 and 18, Default), with
    /// its limits in <see cref="CustomUpper"/> and <see cref="CustomLower"/>; <see cref="Tolerance"/> is then null.
    /// </summary>
    public bool CustomTolerance { get; init; }

    /// <summary>
    /// A Custom tolerance's upper limit, in the spec's units, relative to the modelled diameter. Null when the Custom
    /// tolerance is a <see cref="CustomCode"/>.
    /// </summary>
    public decimal? CustomUpper { get; init; }

    /// <summary>A Custom tolerance's lower limit, as <see cref="CustomUpper"/>.</summary>
    public decimal? CustomLower { get; init; }

    /// <summary>
    /// A Custom tolerance given as an ISO 286 code instead of limits (Aidan, 2026-11-09; task 150), for example <c>H8</c>
    /// or <c>k6</c>, kept in the case typed (H7 and h7 are different classes). The limits are worked out at the modelled
    /// diameter by <see cref="Iso286.Limits"/>, and Inventor applies the code as a fit, as for an ISO row. Null for limits.
    /// </summary>
    public string? CustomCode { get; init; }

    /// <summary>True for a Custom tolerance given as an ISO 286 code (<see cref="CustomCode"/>; task 150).</summary>
    public bool HasCustomCode => CustomTolerance && !string.IsNullOrWhiteSpace(CustomCode);

    /// <summary>True for a hole with a tolerance on its diameter: a dowel, or a toleranced Clearance or Simple hole.</summary>
    public bool IsPrecision => HolePrecision.IsPrecision(this);

    /// <summary>True when the hole ends at a distance and so has a bottom.</summary>
    public bool IsBlind => Termination == HoleTermination.Distance;

    /// <summary>
    /// True when <see cref="ClearanceFit"/> sizes the through hole: a clearance hole with no seat or a spotface (task
    /// 112), or with a counterbore or countersink and no Fastener, as a size with no row of the seat has (task 137). A
    /// seated clearance hole with a fastener takes its row's Hole fit.
    /// </summary>
    public bool UsesClearanceFit =>
        Kind == HoleKind.Clearance
        && (Seat is HoleSeat.None or HoleSeat.Spotface || string.IsNullOrWhiteSpace(Fastener));

    /// <summary>
    /// The same spec with every choice that does not apply to its kind, seat and termination put back to its default
    /// (null, or Normal for the fits), so two specs that build the same hole compare equal. A depth's expression is
    /// trimmed, and goes with its depth: null when the depth is. A fastener applies only to a clearance or tapped hole's
    /// counterbore or countersink; the seat's values apply only to its seat, on every kind (tasks 106, 112 and 126). A
    /// Simple hole has no size, only its diameter (task 112). This is what <see cref="HoleStoredData"/> stores.
    /// </summary>
    public HoleSpec Normalised()
    {
        var tapped = Kind == HoleKind.Tapped;
        var threaded = HoleStandards.IsThreaded(Kind);
        var simple = Kind == HoleKind.Simple;
        var cbore = Seat == HoleSeat.Counterbore;
        var csink = Seat == HoleSeat.Countersink;
        var spotface = Seat == HoleSeat.Spotface;
        var takesFastener = (cbore || csink) && Kind is HoleKind.Clearance or HoleKind.Tapped;
        var depthApplies = IsBlind && !threaded;
        var toleranced = Toleranced && HolePrecision.CanBeToleranced(Kind);
        var precise = Kind == HoleKind.Dowel || toleranced;
        var custom = precise && CustomTolerance;
        // A Custom code replaces the typed limits (task 150).
        var customCode = custom && !string.IsNullOrWhiteSpace(CustomCode) ? CustomCode.Trim() : null;
        return this with
        {
            Size = simple ? "" : Size.Trim(),
            Fastener = takesFastener ? Fastener?.Trim() : null,
            CboreDiameter = cbore ? CboreDiameter : null,
            CboreDepth = cbore ? CboreDepth : null,
            CboreDepthExpression = cbore && CboreDepth is not null ? HoleExpressions.Clean(CboreDepthExpression) : null,
            CsinkDiameter = csink ? CsinkDiameter : null,
            CsinkAngleDeg = csink ? CsinkAngleDeg : null,
            SpotfaceDiameter = spotface ? SpotfaceDiameter : null,
            SpotfaceDepth = spotface ? SpotfaceDepth : null,
            SpotfaceDepthExpression = spotface && SpotfaceDepth is not null
                ? HoleExpressions.Clean(SpotfaceDepthExpression)
                : null,
            ClearanceFit = UsesClearanceFit ? ClearanceFit : ClearanceFit.Normal,
            // A toleranced hole's PrecisionFit is its Precision Tol row's fit, which gives its colour (task 113); Normal
            // for Custom on a toleranced hole. A dowel keeps its own Fit.
            PrecisionFit = Kind == HoleKind.Dowel || (toleranced && !custom) ? PrecisionFit : PrecisionFit.Normal,
            Tolerance = precise && !custom ? Tolerance?.Trim() : null,
            Toleranced = toleranced,
            CustomTolerance = custom,
            CustomUpper = custom && customCode is null ? CustomUpper : null,
            CustomLower = custom && customCode is null ? CustomLower : null,
            CustomCode = customCode,
            ThreadDepth = tapped ? ThreadDepth : null,
            // Full depth applies only to a Through All tapped hole (task 136); every other hole is ticked.
            FullDepth = !TakesFullDepth || FullDepth,
            DrillDepth = threaded ? DrillDepth : null,
            Depth = depthApplies ? Depth : null,
            DrillPoint = IsBlind ? DrillPoint?.Trim() : null,
            ThreadDepthExpression = tapped && ThreadDepth is not null ? HoleExpressions.Clean(ThreadDepthExpression) : null,
            DrillDepthExpression = threaded && DrillDepth is not null ? HoleExpressions.Clean(DrillDepthExpression) : null,
            DepthExpression = depthApplies && Depth is not null ? HoleExpressions.Clean(DepthExpression) : null,
            Diameter = simple ? Diameter : null,
            DiameterExpression = simple && Diameter is not null ? HoleExpressions.Clean(DiameterExpression) : null,
        };
    }
}
