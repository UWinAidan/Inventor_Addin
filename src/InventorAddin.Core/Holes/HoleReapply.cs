namespace InventorAddin.Core.Holes;

/// <summary>
/// The re-apply offer for an AWB hole opened in the window (spec 09, "The command"; task 092): what no longer matches
/// what AWB Hole made, one line each. Empty when the hole matches, and for a hole with no stored AWB data.
/// </summary>
public sealed record HoleReapplyOffer(IReadOnlyList<string> Differences)
{
    /// <summary>No offer: the hole matches its stored data, or it has none.</summary>
    public static HoleReapplyOffer None { get; } = new(Array.Empty<string>());

    /// <summary>True when something differs, so the window offers to re-apply.</summary>
    public bool IsOffered => Differences.Count > 0;
}

/// <summary>
/// Decides the re-apply offer (spec 09: "A hole changed while Inventor's Hole was restored"): when an AWB hole is opened,
/// its stored data is compared with the hole as Inventor reports it. The window then opens on the stored settings, says
/// what differs, and OK re-applies them, the fit, tolerance and colour with them; Cancel leaves the hole as it is.
/// </summary>
/// <remarks>
/// <para>
/// Compared, against what AWB Hole stored when it last applied the hole, never against the workbook as it is now, so a
/// workbook edit alone makes no offer (task 084):
/// </para>
/// <list type="bullet">
/// <item>the diameter, the tolerance and the fit colour (<see cref="HoleStateComparison"/>), except a tapped hole's
/// diameter, which its thread sets and the add-in does not write;</item>
/// <item>the seat (none, counterbore, countersink) and how the hole ends;</item>
/// <item>a blind hole's depths: the depth, or a tapped hole's drill and thread depths;</item>
/// <item>the seat's own sizes, on every kind (task 106 for a dowel, task 126 for clearance and tapped holes): the
/// counterbore diameter and depth, or the countersink diameter and angle.</item>
/// </list>
/// <para>
/// A depth stored as an Inventor expression (task 100) is compared by what the expression comes to today, read by the
/// part's own parameters, so a hole that followed its parameters is not offered; one whose depth was changed in
/// Inventor's Hole or Parameters dialog is. An expression that cannot be read now (no checker, or the text is no longer
/// a length) is not compared; the window shows it as not a length. A value the hole does not report, or one stored data
/// written before its field existed does not hold, is not compared either, so a gap never makes an offer.
/// </para>
/// <para>Lengths match within <see cref="HoleStateComparison.LengthToleranceCm"/>, angles within
/// <see cref="HoleReadBack.AngleToleranceDeg"/>, and are written in the stored spec's units.</para>
/// </remarks>
public static class HoleReapplyRules
{
    private const string CommandName = Branding.ShortName + " Hole";

    /// <summary>Above the differences in the window.</summary>
    public const string HeadingText = "This hole was changed outside " + CommandName + ":";

    /// <summary>Under the differences: what OK and Cancel do.</summary>
    public const string OfferText =
        "The " + CommandName + " settings it was made with are shown. OK re-applies them, with the fit, tolerance and " +
        "colour; Cancel leaves the hole as it is.";

    /// <summary>
    /// The offer for a hole: <paramref name="stored"/> is its AWB data, <paramref name="facts"/> what Inventor reports
    /// for it now and <paramref name="coloured"/> whether any of its faces carries a fit colour.
    /// </summary>
    /// <param name="checker">Reads a stored depth expression against the part's parameters; null skips them.</param>
    public static HoleReapplyOffer Check(
        HoleStoredData stored, InventorHoleFacts facts, bool coloured, IHoleExpressionChecker? checker = null)
    {
        ArgumentNullException.ThrowIfNull(stored);
        ArgumentNullException.ThrowIfNull(facts);

        var spec = stored.Spec.Normalised();
        var units = spec.Units;
        var differences = new List<string>();

        // A tapped hole's diameter is its thread's (the writer never sets it), and a diameter the hole does not report
        // cannot be compared: both take the stored one.
        var diameter = spec.Kind == HoleKind.Tapped || facts.HoleDiameterCm is null
            ? stored.Applied.HoleDiameterCm
            : facts.HoleDiameterCm.Value;
        var current = new HoleAppliedState(
            diameter, facts.ToleranceIsoCode, facts.ToleranceUpperCm, facts.ToleranceLowerCm, coloured);
        differences.AddRange(HoleStateComparison.Compare(stored.Applied, current, units).Differences);

        var sameSeat = facts.Seat == spec.Seat;
        if (!sameSeat)
        {
            differences.Add($"The hole has {SeatText(facts.Seat)}; {CommandName} made it with {SeatText(spec.Seat)}.");
        }

        var sameEnd = facts.Termination == spec.Termination;
        if (!sameEnd)
        {
            differences.Add(
                $"The hole ends {EndText(facts.Termination)}; {CommandName} made it end {EndText(spec.Termination)}.");
        }

        var lengths = new LengthCompare(units, checker, differences);
        if (sameEnd && spec.IsBlind)
        {
            if (spec.Kind == HoleKind.Tapped)
            {
                lengths.Compare("drill depth", spec.DrillDepth, spec.DrillDepthExpression, facts.DepthCm);
                if (spec.ThreadDepth is not null && facts.ThreadDepthCm is null)
                {
                    differences.Add($"The thread runs the full depth; {CommandName} set a thread depth.");
                }
                else
                {
                    lengths.Compare("thread depth", spec.ThreadDepth, spec.ThreadDepthExpression, facts.ThreadDepthCm);
                }
            }
            else
            {
                lengths.Compare("depth", spec.Depth, spec.DepthExpression, facts.DepthCm);
            }
        }

        // The seat's own dimensions, on every kind (task 106 for a dowel, task 126 for the others). Data stored before a
        // kind's seat had them holds none, and is not compared.
        if (sameSeat)
        {
            if (spec.Seat == HoleSeat.Counterbore)
            {
                lengths.Compare("counterbore diameter", spec.CboreDiameter, null, facts.CounterboreDiameterCm);
                lengths.Compare("counterbore depth", spec.CboreDepth, spec.CboreDepthExpression, facts.CounterboreDepthCm);
            }
            else if (spec.Seat == HoleSeat.Countersink)
            {
                lengths.Compare("countersink diameter", spec.CsinkDiameter, null, facts.CountersinkDiameterCm);
                if (spec.CsinkAngleDeg is { } angle && facts.CountersinkAngleDeg is { } now
                    && Math.Abs(now - (double)angle) >= HoleReadBack.AngleToleranceDeg)
                {
                    differences.Add(
                        $"The countersink angle is {Degrees(now)}; {CommandName} set {Degrees((double)angle)}.");
                }
            }
        }

        return differences.Count == 0 ? HoleReapplyOffer.None : new HoleReapplyOffer(differences.AsReadOnly());
    }

    private static string SeatText(HoleSeat? seat) => seat switch
    {
        HoleSeat.None => "no seat",
        HoleSeat.Counterbore => "a counterbore",
        HoleSeat.Countersink => "a countersink",
        _ => "a seat " + CommandName + " does not offer",
    };

    private static string EndText(HoleTermination? termination) => termination switch
    {
        HoleTermination.Distance => "at a distance",
        HoleTermination.ThroughAll => "through all",
        HoleTermination.ToFace => "at a face",
        _ => "in a way " + CommandName + " does not offer",
    };

    private static string Degrees(double degrees) =>
        Math.Round(degrees, 4).ToString("0.####", System.Globalization.CultureInfo.InvariantCulture) + " deg";

    /// <summary>Compares one stored length (a value in the spec's units, maybe from an expression) with the hole's.</summary>
    private sealed class LengthCompare(HoleUnits units, IHoleExpressionChecker? checker, List<string> differences)
    {
        public void Compare(string what, decimal? stored, string? expression, double? currentCm)
        {
            if (stored is not { } value || currentCm is not { } now)
            {
                return;
            }

            double storedCm;
            string storedText;
            if (HoleExpressions.Clean(expression) is { } text)
            {
                if (checker is null)
                {
                    return;
                }

                var check = checker.Check(text, units);
                if (!check.IsValid || check.ValueCm is not { } cm)
                {
                    return;
                }

                storedCm = cm;
                storedText = $"{text} ({HoleStateComparison.LengthText(cm, units)})";
            }
            else
            {
                storedCm = HoleRules.ToCm(value, units);
                storedText = HoleStateComparison.LengthText(storedCm, units);
            }

            if (Math.Abs(now - storedCm) >= HoleStateComparison.LengthToleranceCm)
            {
                differences.Add(
                    $"The {what} is {HoleStateComparison.LengthText(now, units)}; {CommandName} set {storedText}.");
            }
        }
    }
}
