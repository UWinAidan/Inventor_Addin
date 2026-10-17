namespace InventorAddin.Core.Materials;

/// <summary>One appearance Inventor has loaded, as a candidate for a finish's appearance name.</summary>
/// <param name="LibraryRank">
/// The search order of the library holding it; lower ranks are searched first. The add-in (task 074) ranks Inventor's
/// Autodesk Appearance Library 0, the other libraries Inventor loads 1, and the material library it opened last.
/// </param>
/// <param name="DisplayName">The appearance's display name, which the finish list uses.</param>
/// <param name="AssetName">
/// The appearance's internal name. The same appearance listed in two libraries has the same asset name (spike 067).
/// </param>
public sealed record AppearanceCandidate(int LibraryRank, string DisplayName, string AssetName);

/// <summary>The outcome of looking up an appearance by display name.</summary>
public enum AppearanceLookupStatus
{
    /// <summary>One appearance has the name; <see cref="AppearanceLookupResult.AssetName"/> says which.</summary>
    Found,

    /// <summary>No loaded appearance has the name, or the name is blank.</summary>
    Missing,

    /// <summary>The deciding library holds more than one different appearance with the name.</summary>
    Ambiguous,
}

/// <summary>The result of <see cref="AppearanceLookup.Find"/>.</summary>
/// <param name="Status">Found, Missing or Ambiguous.</param>
/// <param name="AssetName">The found appearance's asset name; null unless Found.</param>
/// <param name="LibraryRank">The rank of the deciding library; null when Missing.</param>
/// <param name="Count">
/// The number of distinct appearances with the name in the deciding library: 1 when Found, 2 or more when Ambiguous,
/// 0 when Missing.
/// </param>
public sealed record AppearanceLookupResult(
    AppearanceLookupStatus Status,
    string? AssetName,
    int? LibraryRank,
    int Count)
{
    /// <summary>The result for a name no candidate has.</summary>
    public static AppearanceLookupResult Missing { get; } = new(AppearanceLookupStatus.Missing, null, null, 0);
}

/// <summary>
/// Finding the Inventor appearance a finish's appearance name means (spec 03, "Finding an appearance by name";
/// spike 067 section 5).
/// </summary>
public static class AppearanceLookup
{
    /// <summary>
    /// Looks up <paramref name="name"/> by display name, ignoring case and surrounding spaces. The lowest library rank
    /// holding any match decides and later ranks are not looked at, so a name in rank 0 wins over a different
    /// appearance of the same name further on. In the deciding rank, matches with the same asset name (ignoring case)
    /// are one appearance and the result is Found; more than one distinct asset name is Ambiguous. A blank name is
    /// Missing.
    /// </summary>
    public static AppearanceLookupResult Find(string? name, IEnumerable<AppearanceCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        var wanted = (name ?? "").Trim();
        if (wanted.Length == 0)
        {
            return AppearanceLookupResult.Missing;
        }

        var deciding = candidates
            .Where(c => string.Equals((c.DisplayName ?? "").Trim(), wanted, StringComparison.OrdinalIgnoreCase))
            .GroupBy(c => c.LibraryRank)
            .OrderBy(g => g.Key)
            .FirstOrDefault();

        if (deciding is null)
        {
            return AppearanceLookupResult.Missing;
        }

        var assets = deciding
            .Select(c => c.AssetName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return assets.Count == 1
            ? new AppearanceLookupResult(AppearanceLookupStatus.Found, assets[0], deciding.Key, 1)
            : new AppearanceLookupResult(AppearanceLookupStatus.Ambiguous, null, deciding.Key, assets.Count);
    }
}
