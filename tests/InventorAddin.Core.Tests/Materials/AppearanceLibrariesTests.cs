using InventorAddin.Core.Materials;

namespace InventorAddin.Core.Tests.Materials;

public sealed class AppearanceLibrariesTests
{
    private const string AppearanceGuid = AppearanceLibraries.AppearanceLibraryInternalName;

    private static LoadedLibrary L(string internalName, string displayName) => new(internalName, displayName);

    [Fact]
    public void Rank_AppearanceLibraryByInternalName_Zero_OthersOne_OpenedTwo()
    {
        var ranks = AppearanceLibraries.Rank(
            new[]
            {
                L("AAAA-1", "Test Materials"),
                L(AppearanceGuid, "Autodesk Appearance Library"),
                L("BBBB-2", "Opened Library"),
            },
            "BBBB-2");

        Assert.Equal(new[] { 1, 0, 2 }, ranks);
    }

    [Fact]
    public void Rank_InternalNameWins_OverDisplayName()
    {
        var ranks = AppearanceLibraries.Rank(
            new[]
            {
                L("CCCC-3", "Autodesk Appearance Library"),
                L(AppearanceGuid, "Renamed"),
            },
            null);

        Assert.Equal(new[] { 1, 0 }, ranks);
    }

    [Fact]
    public void Rank_NoInternalName_FallsBackToDisplayName_IgnoringCase()
    {
        var ranks = AppearanceLibraries.Rank(
            new[] { L("CCCC-3", " autodesk appearance library "), L("DDDD-4", "Other") },
            null);

        Assert.Equal(new[] { 0, 1 }, ranks);
    }

    [Fact]
    public void Rank_InternalName_IgnoresCaseAndBraces()
    {
        var ranks = AppearanceLibraries.Rank(
            new[] { L("{" + AppearanceGuid.ToLowerInvariant() + "}", "X"), L("{bbbb-2}", "Y") },
            "BBBB-2");

        Assert.Equal(new[] { 0, 2 }, ranks);
    }

    [Fact]
    public void Rank_OpenedLibrary_IsTwo_EvenWhenNamedLikeTheAppearanceLibrary()
    {
        var ranks = AppearanceLibraries.Rank(new[] { L("EEEE-5", "Autodesk Appearance Library") }, "EEEE-5");

        Assert.Equal(new[] { 2 }, ranks);
    }

    [Fact]
    public void Rank_NoOpenedLibrary_BlankInternalNamesAreNotOpened()
    {
        var ranks = AppearanceLibraries.Rank(new[] { L("", "Favorites") }, "");

        Assert.Equal(new[] { 1 }, ranks);
    }

    [Fact]
    public void Rank_Empty_Empty()
    {
        Assert.Empty(AppearanceLibraries.Rank(Array.Empty<LoadedLibrary>(), null));
    }
}
