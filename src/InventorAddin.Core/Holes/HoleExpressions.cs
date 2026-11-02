namespace InventorAddin.Core.Holes;

/// <summary>
/// What Inventor makes of the text in a depth box (task 100): a length, in centimetres, or not one. Inventor gives no
/// reason of its own for an expression it cannot read (spike 100: <c>IsExpressionValid</c> answers false and
/// <c>GetValueFromExpression</c> fails with "Unspecified error"), so <see cref="Reason"/> is usually null and the
/// window shows <see cref="HoleExpressions.NotALengthText"/>.
/// </summary>
/// <param name="IsValid">Inventor reads the text as a length.</param>
/// <param name="ValueCm">The length when valid, in centimetres.</param>
/// <param name="Reason">Why the text is not a length, when the checker has a reason; else null.</param>
public sealed record HoleExpressionCheck(bool IsValid, double? ValueCm, string? Reason)
{
    /// <summary>A length of <paramref name="valueCm"/> centimetres.</summary>
    public static HoleExpressionCheck Valid(double valueCm) => new(true, valueCm, null);

    /// <summary>Not a length; <paramref name="reason"/> is shown under the box when given.</summary>
    public static HoleExpressionCheck Invalid(string? reason = null) =>
        new(false, null, string.IsNullOrWhiteSpace(reason) ? null : reason.Trim());
}

/// <summary>
/// Reads the text of a depth box the way Inventor's own boxes do (spec 09, "The window"; task 100): a length with a
/// unit (<c>12 mm</c>, <c>0.5 in</c>), the part's parameters (<c>d0</c>) and expressions of them
/// (<c>1.5*d0+1</c>). A number with no unit is in <paramref name="units"/>' length, millimetres or inches, as Inventor
/// reads it with that unit (spike 100: <c>1.5*d0+1</c> with d0 = 50 mm is 76 mm in millimetres and 100.4 mm in
/// inches). The add-in checks against the part's own parameters, so names resolve; names are case sensitive.
/// </summary>
public interface IHoleExpressionChecker
{
    /// <summary>What <paramref name="expression"/> is as a length, a number with no unit read in <paramref name="units"/>.</summary>
    HoleExpressionCheck Check(string expression, HoleUnits units);
}

/// <summary>Texts and units shared by the depth boxes and the add-in's expression writes (task 100).</summary>
public static class HoleExpressions
{
    /// <summary>Under a depth box whose text Inventor cannot read as a length, when it gives no reason.</summary>
    public const string NotALengthText =
        "Inventor cannot read this as a length. Enter a number, a length such as 12 mm, or the part's parameters.";

    /// <summary>
    /// The unit name Inventor takes for <paramref name="units"/>' length, <c>mm</c> or <c>in</c>: the units a number
    /// with no unit in an expression is read in, and the units the add-in gives a depth parameter that holds an
    /// expression, so Inventor reads the expression as the window did.
    /// </summary>
    public static string UnitName(HoleUnits units) => units == HoleUnits.Imperial ? "in" : "mm";

    /// <summary>The text as an expression: trimmed, or null when blank.</summary>
    public static string? Clean(string? expression)
    {
        var trimmed = (expression ?? "").Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }
}
