namespace InventorAddin.Core.Holes;

/// <summary>
/// The icon each AWB Hole choice shows on its icon button (task 104; mockup choice 2): Type, Seat, Termination and
/// Drill point. The add-in draws the icon named here (its geometry is in <c>UI/Theme/Styles.xaml</c>, keyed
/// <c>HoleIcon.{name}.{part}</c>); which icon a choice gets is decided here.
/// </summary>
public static class HoleIcons
{
    public const string Clearance = "Clearance";
    public const string Tapped = "Tapped";

    /// <summary>Type Simple (task 112; drawn by task 119).</summary>
    public const string Simple = "Simple";

    /// <summary>Type Taper Tapped (task 112; drawn by task 119).</summary>
    public const string TaperTapped = "TaperTapped";

    public const string SeatNone = "SeatNone";
    public const string Counterbore = "Counterbore";
    public const string Countersink = "Countersink";

    /// <summary>Seat Spotface (task 112; drawn by task 119).</summary>
    public const string Spotface = "Spotface";
    public const string Distance = "Distance";
    public const string ThroughAll = "ThroughAll";
    public const string ToFace = "ToFace";

    /// <summary>A drill point with a shallow cone, drawn from the 118 deg point.</summary>
    public const string PointShallow = "PointShallow";

    /// <summary>A drill point with a steep cone, drawn from the 90 deg point.</summary>
    public const string PointSteep = "PointSteep";

    /// <summary>A drill point with a sharp cone, drawn from the 60 deg point (task 131).</summary>
    public const string PointSharp = "PointSharp";

    public const string FlatBottom = "FlatBottom";

    /// <summary>
    /// Angles at or above this many degrees show the shallow cone: half way between the 118 deg and 90 deg the icons
    /// are drawn from.
    /// </summary>
    public const decimal ShallowPointFromDeg = 104m;

    /// <summary>
    /// Angles at or above this many degrees, and under <see cref="ShallowPointFromDeg"/>, show the steep cone, below it
    /// the sharp one: half way between the 90 deg and 60 deg the icons are drawn from. An angle exactly half way goes
    /// to the wider cone, as at <see cref="ShallowPointFromDeg"/> (task 131; Default).
    /// </summary>
    public const decimal SteepPointFromDeg = 75m;

    /// <summary>Every icon name, for checks that each one is drawn.</summary>
    public static IReadOnlyList<string> All { get; } =
    [
        Clearance, Tapped, SeatNone, Counterbore, Countersink, Distance, ThroughAll, ToFace, PointShallow, PointSteep,
        FlatBottom, Simple, TaperTapped, Spotface, PointSharp,
    ];

    /// <summary>
    /// The icon for a Type: Tapped, Simple and Taper Tapped their own, anything else Clearance (a dowel is the
    /// Clearance type).
    /// </summary>
    public static string ForType(string? type) => (type ?? "").Trim() switch
    {
        var t when Same(t, HoleStandards.TappedType) => Tapped,
        var t when Same(t, HoleStandards.SimpleType) => Simple,
        var t when Same(t, HoleStandards.TaperTappedType) => TaperTapped,
        _ => Clearance,
    };

    public static string ForSeat(HoleSeat seat) => seat switch
    {
        HoleSeat.Counterbore => Counterbore,
        HoleSeat.Countersink => Countersink,
        HoleSeat.Spotface => Spotface,
        _ => SeatNone,
    };

    private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    public static string ForTermination(HoleTermination termination) => termination switch
    {
        HoleTermination.Distance => Distance,
        HoleTermination.ToFace => ToFace,
        _ => ThroughAll,
    };

    /// <summary>
    /// The icon for a Drill Point row's angle: flat for none, else the cone nearest of 118 deg (shallow), 90 deg
    /// (steep) and 60 deg (sharp); see <see cref="ShallowPointFromDeg"/> and <see cref="SteepPointFromDeg"/>.
    /// </summary>
    public static string ForDrillPoint(decimal? angleDeg) => angleDeg switch
    {
        null => FlatBottom,
        >= ShallowPointFromDeg => PointShallow,
        >= SteepPointFromDeg => PointSteep,
        _ => PointSharp,
    };
}
