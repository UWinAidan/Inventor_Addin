namespace InventorAddin.Core.Holes;

/// <summary>
/// Exactly what the add-in builds in Inventor for one AWB hole, worked out by <see cref="HoleRules"/>. Lengths are in
/// centimetres (Inventor's internal units) and angles in degrees, except the tap drill, which is in the thread table's
/// units (<see cref="HoleThread.TapDrillInTableUnits"/>). The add-in copies these values into Inventor and decides
/// nothing itself.
/// </summary>
/// <param name="Kind">Clearance, Tapped or Dowel. Inventor models a dowel as a plain drilled hole.</param>
/// <param name="Seat">None, Counterbore or Countersink.</param>
/// <param name="Termination">Distance, Through All or To a face.</param>
/// <param name="Flip">The direction is flipped.</param>
/// <param name="HoleDiameterCm">
/// The modelled hole diameter: the clearance diameter, the tap drill for a tapped hole, the nominal for a dowel.
/// </param>
/// <param name="DepthCm">The hole depth when <see cref="Termination"/> is Distance (the drill depth when tapped); else null.</param>
/// <param name="CounterboreDiameterCm">Counterbore seats only.</param>
/// <param name="CounterboreDepthCm">Counterbore seats only.</param>
/// <param name="CountersinkDiameterCm">Countersink seats only.</param>
/// <param name="CountersinkAngleDeg">Countersink seats only: the included angle.</param>
/// <param name="DrillPoint">Blind holes only: the bottom.</param>
/// <param name="Thread">Tapped holes only.</param>
/// <param name="Tolerance">Dowel holes only: the tolerance on the hole diameter.</param>
/// <param name="FitColour">Dowel holes only: the colour every face of the hole takes.</param>
public sealed record HoleGeometry(
    HoleKind Kind,
    HoleSeat Seat,
    HoleTermination Termination,
    bool Flip,
    double HoleDiameterCm,
    double? DepthCm,
    double? CounterboreDiameterCm,
    double? CounterboreDepthCm,
    double? CountersinkDiameterCm,
    double? CountersinkAngleDeg,
    HoleDrillPoint? DrillPoint,
    HoleThread? Thread,
    HoleTolerance? Tolerance,
    HoleColour? FitColour)
{
    /// <summary>True when the hole's faces are coloured by fit (dowel and precision holes only).</summary>
    public bool IsColoured => FitColour is not null;

    /// <summary>
    /// The Inventor expression the hole depth comes from (the drill depth when tapped), when it was typed as one
    /// (task 100): the add-in writes it into the hole's depth parameter after <see cref="DepthCm"/>, so the hole stays
    /// parametric. Null for a plain number and when the hole is not blind.
    /// </summary>
    public string? DepthExpression { get; init; }

    /// <summary>
    /// The Inventor expression a dowel's counterbore depth comes from, when it was typed as one (task 106): written into
    /// the hole's counterbore depth parameter after <see cref="CounterboreDepthCm"/>, so the seat stays parametric.
    /// Null for a plain number and for any other seat.
    /// </summary>
    public string? CounterboreDepthExpression { get; init; }

    /// <summary>
    /// The units a number with no unit in <see cref="DepthExpression"/>, <see cref="CounterboreDepthExpression"/> or
    /// <see cref="HoleThread.ThreadDepthExpression"/>
    /// is in: the window's. The add-in gives a parameter that takes an expression this length unit
    /// (<see cref="HoleExpressions.UnitName"/>), so Inventor reads the expression as the window did.
    /// </summary>
    public HoleUnits ExpressionUnits { get; init; }
}

/// <summary>
/// The bottom of a blind hole, from a Drill Point row: angled with <see cref="AngleDeg"/> (<c>118</c>, <c>90</c>),
/// or flat when the angle is null.
/// </summary>
public sealed record HoleDrillPoint(string Name, double? AngleDeg)
{
    public bool IsFlat => AngleDeg is null;
}

/// <summary>
/// The thread of a tapped hole, named as Inventor's thread table names it (spike 081).
/// </summary>
/// <param name="Designation">Inventor's designation: <c>M6x1</c>, <c>10-32 UNF</c>.</param>
/// <param name="ThreadType">Inventor's thread type: <c>ISO Metric profile</c> or <c>ANSI Unified Screw Threads</c>.</param>
/// <param name="Class">The thread class: <c>6H</c>, <c>2B</c>.</param>
/// <param name="TapDrillInTableUnits">
/// The tap drill in the thread table's units, millimetres for metric and inches for imperial, as a number: the way
/// Inventor's <c>HoleTapInfo.TapDrillDiameter</c> and <c>MinorDiameterMin</c> take it (spike 081, task 095). Not
/// centimetres.
/// </param>
/// <param name="TableUnits">The thread table's units: Metric (mm) or Imperial (inches).</param>
/// <param name="FullDepth">True when the thread runs the full depth of the hole (Through All and To a face).</param>
/// <param name="ThreadDepthCm">The thread depth, used when <see cref="FullDepth"/> is false.</param>
public sealed record HoleThread(
    string Designation,
    string ThreadType,
    string Class,
    double TapDrillInTableUnits,
    HoleUnits TableUnits,
    bool FullDepth,
    double ThreadDepthCm)
{
    /// <summary>
    /// The Inventor expression the thread depth comes from, when it was typed as one and the thread is not full depth
    /// (task 100); written into the thread depth parameter after <see cref="ThreadDepthCm"/>. Else null.
    /// </summary>
    public string? ThreadDepthExpression { get; init; }
}

/// <summary>
/// The tolerance on a dowel hole's diameter, from a Precision Tol row. ISO rows carry the code, which Inventor
/// applies as a fit and works out itself; the limits are worked out by <see cref="Iso286"/> for showing and checking.
/// Limits rows carry no code; Inventor takes the limits as upper and lower deviations. Deviations are relative to the
/// nominal size, in centimetres.
/// </summary>
public sealed record HoleTolerance(string Name, string? IsoCode, double UpperCm, double LowerCm)
{
    public bool IsIso => IsoCode is not null;
}
