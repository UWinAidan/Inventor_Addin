using InventorAddin.Core.Materials;

namespace InventorAddin.Core.Tests.Materials;

public sealed class MaterialFilterTests
{
    private static readonly LibraryMaterial Alpha = new("Alphium, 10-A", "Metal", "Test Shine");
    private static readonly LibraryMaterial Beta = new("beta alloy, 20-B", "Metal", "Test Shine");
    private static readonly LibraryMaterial Gamma = new("Gammalon, 30", "Polymer", null);
    private static readonly LibraryMaterial Delta = new("Deltite, 40-C", "", "Test Matte");
    private static readonly LibraryMaterial Epsilon = new("Epsilonite", "ceramic", "Test Matte");

    private static readonly LibraryMaterial[] Library = [Gamma, Delta, Beta, Epsilon, Alpha];

    private static IReadOnlyList<string> Names(IEnumerable<LibraryMaterial> materials) =>
        materials.Select(m => m.DisplayName).ToList();

    [Fact]
    public void Categories_AllFirst_ThenDistinctSortedIgnoringCase_SkippingBlank()
    {
        Assert.Equal(["All", "ceramic", "Metal", "Polymer"], MaterialFilter.Categories(Library));
    }

    [Fact]
    public void Categories_DuplicatesDifferingInCaseOrSpaces_ListedOnce_FirstSpellingKept()
    {
        var materials = new[]
        {
            new LibraryMaterial("A", "Metal", null),
            new LibraryMaterial("B", " metal ", null),
            new LibraryMaterial("C", "METAL", null),
        };

        Assert.Equal(["All", "Metal"], MaterialFilter.Categories(materials));
    }

    [Fact]
    public void Categories_CategoryNamedAll_NotListedTwice()
    {
        var materials = new[] { new LibraryMaterial("A", "all", null), new LibraryMaterial("B", "Metal", null) };

        Assert.Equal(["All", "Metal"], MaterialFilter.Categories(materials));
    }

    [Fact]
    public void Categories_Empty_IsJustAll()
    {
        Assert.Equal(["All"], MaterialFilter.Categories([]));
    }

    [Fact]
    public void Apply_All_KeepsEverything_SortedByNameIgnoringCase()
    {
        var result = MaterialFilter.Apply(Library, "All", false, [], "");

        Assert.Equal(
            ["Alphium, 10-A", "beta alloy, 20-B", "Deltite, 40-C", "Epsilonite", "Gammalon, 30"],
            Names(result));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("all")]
    public void Apply_BlankOrAllInAnyCase_MeansEveryCategory(string? category)
    {
        Assert.Equal(5, MaterialFilter.Apply(Library, category, false, [], null).Count);
    }

    [Fact]
    public void Apply_Category_IgnoresCaseAndSpaces()
    {
        var result = MaterialFilter.Apply(Library, " metal ", false, [], null);

        Assert.Equal(["Alphium, 10-A", "beta alloy, 20-B"], Names(result));
    }

    [Fact]
    public void Apply_Category_MaterialWithNoCategory_OnlyUnderAll()
    {
        foreach (var category in MaterialFilter.Categories(Library).Skip(1))
        {
            Assert.DoesNotContain(Delta, MaterialFilter.Apply(Library, category, false, [], null));
        }

        Assert.Contains(Delta, MaterialFilter.Apply(Library, "All", false, [], null));
    }

    [Fact]
    public void Apply_UnknownCategory_IsEmpty()
    {
        Assert.Empty(MaterialFilter.Apply(Library, "Wood", false, [], null));
    }

    [Fact]
    public void Apply_PreferredOnly_KeepsPreferredIgnoringCaseAndSpaces()
    {
        var result = MaterialFilter.Apply(Library, "All", true, [" GAMMALON, 30 ", "alphium, 10-a", "Not here"], null);

        Assert.Equal(["Alphium, 10-A", "Gammalon, 30"], Names(result));
    }

    [Fact]
    public void Apply_PreferredOnlyOff_IgnoresPreferredList()
    {
        Assert.Equal(5, MaterialFilter.Apply(Library, "All", false, ["Gammalon, 30"], null).Count);
    }

    [Fact]
    public void Apply_PreferredOnly_EmptyPreferredList_IsEmpty()
    {
        Assert.Empty(MaterialFilter.Apply(Library, "All", true, [], null));
    }

    [Fact]
    public void Apply_Search_ContainsIgnoringCase_Trimmed()
    {
        var result = MaterialFilter.Apply(Library, "All", false, [], "  gAMMA ");

        Assert.Equal(["Gammalon, 30"], Names(result));
    }

    [Fact]
    public void Apply_Search_MatchesInsideName()
    {
        var result = MaterialFilter.Apply(Library, "All", false, [], "-a");

        Assert.Equal(["Alphium, 10-A"], Names(result));
    }

    [Fact]
    public void Apply_Search_NoMatch_IsEmpty()
    {
        Assert.Empty(MaterialFilter.Apply(Library, "All", false, [], "zzz"));
    }

    [Fact]
    public void Apply_AllFiltersCombined()
    {
        var materials = new[]
        {
            new LibraryMaterial("Metalone, Hard", "Metal", null),
            new LibraryMaterial("Metalone, Soft", "Metal", null),
            new LibraryMaterial("Metaltwo, Hard", "Metal", null),
            new LibraryMaterial("Plasticone, Hard", "Polymer", null),
        };

        var result = MaterialFilter.Apply(
            materials,
            "Metal",
            true,
            ["Metalone, Hard", "Metalone, Soft", "Plasticone, Hard"],
            "hard");

        Assert.Equal(["Metalone, Hard"], Names(result));
    }

    [Theory]
    [InlineData("Alphium, 10-A")]
    [InlineData("alphium, 10-a")]
    [InlineData("  Alphium, 10-A  ")]
    [InlineData("Deltite, 40-C")]
    public void IsInLibrary_DisplayNameIgnoringCaseAndSpaces(string name)
    {
        Assert.True(MaterialFilter.IsInLibrary(name, Library));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Alphium")]
    [InlineData("Alphium, 10-A extra")]
    [InlineData("Metal")]
    public void IsInLibrary_BlankPartialOrCategory_IsFalse(string? name)
    {
        Assert.False(MaterialFilter.IsInLibrary(name, Library));
    }

    [Fact]
    public void IsInLibrary_LibraryNameWithSpaces_StillMatches()
    {
        var materials = new[] { new LibraryMaterial(" Spaced, 1 ", "Metal", null) };

        Assert.True(MaterialFilter.IsInLibrary("Spaced, 1", materials));
    }
}
