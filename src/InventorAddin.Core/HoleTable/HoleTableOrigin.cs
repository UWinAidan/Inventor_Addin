namespace InventorAddin.Core.HoleTable;

/// <summary>A point on the drawing sheet, in centimetres.</summary>
public readonly record struct SheetPoint(double XCm, double YCm);

/// <summary>
/// The ordinate origin the hole table measures from, or the reason the wizard must stop (spec 04 questions 7 and 17).
/// </summary>
public sealed record HoleTableOriginResult
{
    /// <summary>Origins closer than this on both axes count as one point.</summary>
    public const double SamePointToleranceCm = 0.001;

    private HoleTableOriginResult(SheetPoint? origin, string? stopMessage)
    {
        Origin = origin;
        StopMessage = stopMessage;
    }

    public SheetPoint? Origin { get; }

    /// <summary>The plain-words reason to stop; null when there is an origin.</summary>
    public string? StopMessage { get; }

    public bool IsStop => StopMessage is not null;

    /// <summary>Every ordinate origin the add-in found on the view, in sheet centimetres.</summary>
    /// <param name="origins">Every origin point read.</param>
    /// <param name="originUnreadable">An origin exists on the view but its point could not be read; only matters when <paramref name="origins"/> is empty.</param>
    public static HoleTableOriginResult From(IReadOnlyList<SheetPoint> origins, bool originUnreadable = false)
    {
        ArgumentNullException.ThrowIfNull(origins);

        if (origins.Count == 0)
        {
            return new HoleTableOriginResult(
                null, originUnreadable ? HoleTableMessages.OrdinateOriginUnreadable : HoleTableMessages.NoOrdinateOrigin);
        }

        var first = origins[0];
        if (origins.Max(o => o.XCm) - origins.Min(o => o.XCm) > SamePointToleranceCm
            || origins.Max(o => o.YCm) - origins.Min(o => o.YCm) > SamePointToleranceCm)
        {
            return new HoleTableOriginResult(null, HoleTableMessages.MoreThanOneOrdinateOrigin);
        }

        return new HoleTableOriginResult(first, null);
    }
}
