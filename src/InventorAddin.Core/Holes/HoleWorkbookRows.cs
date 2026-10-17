using System.Globalization;

namespace InventorAddin.Core.Holes;

// The rows of the hole workbook, libraries/AWB_Holes.xlsx (spec 09, "The workbook").
//
// Units: every length keeps the units of its row, given by the row's Units value: millimetres for Metric rows and
// inches for Imperial rows, exactly as typed in the workbook. Fields are named without a unit suffix (Nominal, not
// NominalMm) because one field holds either unit; convert with the row's Units at the geometry boundary (task 084).
// Angles are in degrees and say so in the name.
//
// Row is the 1-based worksheet row, for messages. Hidden is true for Show = N; the window leaves those rows out.

/// <summary>The units of a workbook row: Metric rows are in millimetres, Imperial rows in inches.</summary>
public enum HoleUnits
{
    Metric,
    Imperial,
}

/// <summary>The three clearance fits: the Close, Normal and Loose columns of the Clearance tab.</summary>
public enum ClearanceFit
{
    Close,
    Normal,
    Loose,
}

/// <summary>The fits of dowel and precision holes, from the Precision Tol tab's Fit column.</summary>
public enum PrecisionFit
{
    Slip,
    Normal,
    Press,
}

/// <summary>How a Precision Tol row gives its tolerance: an ISO 286 code, or upper and lower limits.</summary>
public enum ToleranceKind
{
    Iso,
    Limits,
}

/// <summary>A colour from the Settings tab, as red, green and blue from 0 to 255.</summary>
public sealed record HoleColour(byte R, byte G, byte B);

/// <summary>A Clearance tab row: the hole diameter for each fit.</summary>
public sealed record ClearanceRow(
    int Row,
    HoleUnits Units,
    string Size,
    decimal Nominal,
    decimal Close,
    decimal Normal,
    decimal Loose,
    bool Hidden)
{
    /// <summary>The hole diameter for a fit, in the row's units.</summary>
    public decimal Diameter(ClearanceFit fit) => fit switch
    {
        ClearanceFit.Close => Close,
        ClearanceFit.Normal => Normal,
        ClearanceFit.Loose => Loose,
        _ => throw new ArgumentOutOfRangeException(nameof(fit), fit, null),
    };
}

/// <summary>A Counterbore tab row. The through hole comes from the Clearance tab at <see cref="HoleFit"/>.</summary>
public sealed record CounterboreRow(
    int Row,
    HoleUnits Units,
    string Size,
    string Screw,
    decimal CboreDiameter,
    decimal CboreDepth,
    ClearanceFit HoleFit,
    bool Hidden);

/// <summary>A Countersink tab row. The through hole comes from the Clearance tab at <see cref="HoleFit"/>.</summary>
public sealed record CountersinkRow(
    int Row,
    HoleUnits Units,
    string Size,
    string Screw,
    decimal CsinkDiameter,
    decimal AngleDeg,
    ClearanceFit HoleFit,
    bool Hidden);

/// <summary>
/// A Tapped tab row. <see cref="Thread"/> is as written in the workbook (<c>M6</c>, <c>#10-32 UNF</c>);
/// <see cref="Designation"/> and <see cref="ThreadType"/> are as Inventor's thread table names them.
/// <see cref="PitchOrTpi"/> is the pitch in mm for Metric rows and threads per inch for Imperial rows.
/// <see cref="ThreadDepth"/> and <see cref="DrillDepth"/> are the cell values, or worked out from Settings when the
/// cell is blank.
/// </summary>
public sealed record TappedRow(
    int Row,
    HoleUnits Units,
    string Thread,
    decimal Nominal,
    decimal PitchOrTpi,
    decimal TapDrill,
    string Class,
    decimal ThreadDepth,
    decimal DrillDepth,
    bool Hidden)
{
    /// <summary>Inventor's thread type for metric threads (spike 081).</summary>
    public const string MetricThreadType = "ISO Metric profile";

    /// <summary>Inventor's thread type for imperial threads (spike 081).</summary>
    public const string ImperialThreadType = "ANSI Unified Screw Threads";

    /// <summary>The thread designation as Inventor's thread table names it (<c>M6x1</c>, <c>10-32 UNF</c>).</summary>
    public string Designation => ThreadDesignation(Units, Thread, PitchOrTpi);

    /// <summary>Inventor's thread type for the row's units.</summary>
    public string ThreadType => Units == HoleUnits.Metric ? MetricThreadType : ImperialThreadType;

    /// <summary>
    /// The designation Inventor's thread table uses (spike 081). Metric: the pitch is added when the thread has
    /// none, written without trailing zeros (<c>M6</c> with 1.0 is <c>M6x1</c>, <c>M8</c> with 1.25 is
    /// <c>M8x1.25</c>); a thread already written with a pitch (<c>M8x1</c>) is kept. Imperial: a leading <c>#</c>
    /// is dropped (<c>#10-32 UNF</c> is <c>10-32 UNF</c>).
    /// </summary>
    public static string ThreadDesignation(HoleUnits units, string thread, decimal pitchOrTpi)
    {
        var text = thread.Trim();
        if (units == HoleUnits.Imperial)
        {
            return text.TrimStart('#').TrimStart();
        }

        if (text.Contains('x', StringComparison.OrdinalIgnoreCase))
        {
            return text;
        }

        return text + "x" + pitchOrTpi.ToString("0.############################", CultureInfo.InvariantCulture);
    }
}

/// <summary>A Dowel tab row: a nominal size offered for dowel and precision holes.</summary>
public sealed record DowelRow(int Row, HoleUnits Units, string Size, decimal Nominal, bool Hidden);

/// <summary>
/// A Precision Tol row. For <see cref="ToleranceKind.Iso"/> rows <see cref="IsoCode"/> is set and the limits are
/// null; for <see cref="ToleranceKind.Limits"/> rows <see cref="Upper"/> and <see cref="Lower"/> are set, relative to
/// the nominal size in the row's units, and the code is null.
/// </summary>
public sealed record PrecisionToleranceRow(
    int Row,
    HoleUnits Units,
    PrecisionFit Fit,
    string Name,
    ToleranceKind Kind,
    string? IsoCode,
    decimal? Upper,
    decimal? Lower,
    bool IsDefault,
    string Note);

/// <summary>A Drill Point tab row. <see cref="AngleDeg"/> is null for a flat bottom.</summary>
public sealed record DrillPointRow(int Row, string Name, decimal? AngleDeg, bool IsDefault, bool Hidden, string Note)
{
    public bool IsFlat => AngleDeg is null;
}

/// <summary>
/// The Settings tab. A setting that is missing or cannot be read is reported and takes the starter value
/// (<see cref="Starter"/>), so the rest of the workbook still works.
/// </summary>
public sealed record HoleSettings(
    HoleUnits DefaultUnits,
    decimal ThreadDepthMultiplier,
    decimal DrillExtraDepthMultiplier,
    HoleColour PressFitColour,
    HoleColour NormalFitColour,
    HoleColour SlipFitColour)
{
    /// <summary>The starter workbook's values (spec 09): Metric, 1.5 x D, 0.5 x D, press red, normal blue, slip green.</summary>
    public static HoleSettings Starter { get; } = new(
        HoleUnits.Metric,
        1.5m,
        0.5m,
        new HoleColour(220, 40, 40),
        new HoleColour(40, 100, 220),
        new HoleColour(40, 170, 70));

    /// <summary>The fit colour for a precision fit.</summary>
    public HoleColour FitColour(PrecisionFit fit) => fit switch
    {
        PrecisionFit.Press => PressFitColour,
        PrecisionFit.Normal => NormalFitColour,
        PrecisionFit.Slip => SlipFitColour,
        _ => throw new ArgumentOutOfRangeException(nameof(fit), fit, null),
    };

    /// <summary>The default thread depth for a nominal diameter: <c>round(nominal x thread multiplier, 3)</c>.</summary>
    public decimal DefaultThreadDepth(decimal nominal) =>
        Math.Round(nominal * ThreadDepthMultiplier, 3, MidpointRounding.AwayFromZero);

    /// <summary>The default drill depth: <c>round(thread depth + nominal x drill multiplier, 3)</c>.</summary>
    public decimal DefaultDrillDepth(decimal nominal, decimal threadDepth) =>
        Math.Round(threadDepth + (nominal * DrillExtraDepthMultiplier), 3, MidpointRounding.AwayFromZero);
}
