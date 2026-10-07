using InventorAddin.Core.Theming;

namespace InventorAddin.Core.Tests.Theming;

public sealed class UiThemeTests
{
    [Theory]
    [InlineData("DarkTheme")]
    [InlineData("dark")]
    [InlineData("  Dark Theme  ")]
    [InlineData("DARK")]
    public void FromInventorThemeName_NameContainingDark_IsDark(string name)
    {
        Assert.Equal(UiTheme.Dark, UiThemes.FromInventorThemeName(name));
    }

    [Theory]
    [InlineData("LightTheme")]
    [InlineData("Light Gray")]
    [InlineData("Classic")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void FromInventorThemeName_AnythingElse_IsLight(string? name)
    {
        Assert.Equal(UiTheme.Light, UiThemes.FromInventorThemeName(name));
    }

    [Theory]
    [InlineData(UiTheme.Light, "Light")]
    [InlineData(UiTheme.Dark, "Dark")]
    public void ResourceSuffix_IsThemeName(UiTheme theme, string expected)
    {
        Assert.Equal(expected, UiThemes.ResourceSuffix(theme));
    }

    [Fact]
    public void ResourceSuffix_RejectsUndefinedTheme()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => UiThemes.ResourceSuffix((UiTheme)99));
    }

    [Fact]
    public void ResourceSuffix_IsDistinctPerTheme()
    {
        var suffixes = Enum.GetValues<UiTheme>().Select(UiThemes.ResourceSuffix).ToList();

        Assert.Equal(suffixes.Count, suffixes.Distinct().Count());
    }
}
