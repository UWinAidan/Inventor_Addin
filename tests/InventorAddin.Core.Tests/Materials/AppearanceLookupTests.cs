using InventorAddin.Core.Materials;

namespace InventorAddin.Core.Tests.Materials;

public sealed class AppearanceLookupTests
{
    private static AppearanceCandidate C(int rank, string display, string asset) => new(rank, display, asset);

    [Fact]
    public void Find_SingleMatch_Found()
    {
        var result = AppearanceLookup.Find("Test Shine", [C(0, "Test Shine", "Asset-1"), C(0, "Other", "Asset-2")]);

        Assert.Equal(new AppearanceLookupResult(AppearanceLookupStatus.Found, "Asset-1", 0, 1), result);
    }

    [Fact]
    public void Find_IgnoresCaseAndSpaces()
    {
        var result = AppearanceLookup.Find("  test SHINE ", [C(0, "Test Shine ", "Asset-1")]);

        Assert.Equal(AppearanceLookupStatus.Found, result.Status);
        Assert.Equal("Asset-1", result.AssetName);
    }

    [Fact]
    public void Find_NoMatch_Missing()
    {
        var result = AppearanceLookup.Find("Nowhere", [C(0, "Test Shine", "Asset-1")]);

        Assert.Equal(AppearanceLookupResult.Missing, result);
        Assert.Null(result.AssetName);
        Assert.Null(result.LibraryRank);
        Assert.Equal(0, result.Count);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Find_BlankName_Missing(string? name)
    {
        Assert.Equal(AppearanceLookupStatus.Missing, AppearanceLookup.Find(name, [C(0, "", "Asset-1")]).Status);
    }

    [Fact]
    public void Find_NoCandidates_Missing()
    {
        Assert.Equal(AppearanceLookupStatus.Missing, AppearanceLookup.Find("Test Shine", []).Status);
    }

    [Fact]
    public void Find_InRankZeroAndLaterAsDifferentAsset_RankZeroWins()
    {
        // As spike 067's Zinc: the material library has its own appearance of the same name.
        var candidates = new[] { C(2, "Test Metal", "Lib-Metal"), C(0, "Test Metal", "Gen-Metal"), C(1, "Test Metal", "Other-Metal") };

        var result = AppearanceLookup.Find("Test Metal", candidates);

        Assert.Equal(new AppearanceLookupResult(AppearanceLookupStatus.Found, "Gen-Metal", 0, 1), result);
    }

    [Fact]
    public void Find_OnlyInLaterRank_FoundThere()
    {
        var candidates = new[] { C(0, "Something else", "Gen-1"), C(2, "Test Glow", "Lib-Glow") };

        var result = AppearanceLookup.Find("Test Glow", candidates);

        Assert.Equal(new AppearanceLookupResult(AppearanceLookupStatus.Found, "Lib-Glow", 2, 1), result);
    }

    [Fact]
    public void Find_SameAssetTwiceInDecidingRank_CountsOnce()
    {
        // As spike 067: the same asset listed in two libraries of the same rank, with one asset name.
        var candidates = new[] { C(1, "Test Blue", "Gen-060"), C(1, "Test Blue", "gen-060") };

        var result = AppearanceLookup.Find("Test Blue", candidates);

        Assert.Equal(new AppearanceLookupResult(AppearanceLookupStatus.Found, "Gen-060", 1, 1), result);
    }

    [Fact]
    public void Find_TwoDistinctAssetsInDecidingRank_Ambiguous()
    {
        var candidates = new[]
        {
            C(0, "Test Yellow", "Wall-Matte"),
            C(0, "Test Yellow", "Wall-Glossy"),
            C(0, "Test Yellow", "Wall-Matte"),
            C(1, "Test Yellow", "Lib-Yellow"),
        };

        var result = AppearanceLookup.Find("test yellow", candidates);

        Assert.Equal(new AppearanceLookupResult(AppearanceLookupStatus.Ambiguous, null, 0, 2), result);
    }

    [Fact]
    public void Find_AmbiguousOnlyInLaterRank_EarlierUniqueMatchWins()
    {
        var candidates = new[] { C(1, "Test Gray", "A"), C(1, "Test Gray", "B"), C(0, "Test Gray", "Gen-Gray") };

        var result = AppearanceLookup.Find("Test Gray", candidates);

        Assert.Equal(new AppearanceLookupResult(AppearanceLookupStatus.Found, "Gen-Gray", 0, 1), result);
    }

    [Fact]
    public void Find_SameAssetInDifferentRanks_FoundInLowestRank()
    {
        var candidates = new[] { C(1, "Test Red", "Gen-Red"), C(0, "Test Red", "Gen-Red") };

        var result = AppearanceLookup.Find("Test Red", candidates);

        Assert.Equal(0, result.LibraryRank);
        Assert.Equal(AppearanceLookupStatus.Found, result.Status);
    }
}
