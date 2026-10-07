using System.Reflection;
using InventorAddin.Core.Ribbon;
using InventorAddin.Core.Theming;

namespace InventorAddin.Core.Tests.Ribbon;

public sealed class RibbonIconsTests
{
    [Theory]
    [InlineData("About", UiTheme.Light, 16, "About.Light.16.png")]
    [InlineData("About", UiTheme.Dark, 16, "About.Dark.16.png")]
    [InlineData("PartProperties", UiTheme.Light, 32, "PartProperties.Light.32.png")]
    [InlineData("ExportLibraries", UiTheme.Dark, 32, "ExportLibraries.Dark.32.png")]
    public void FileName_IsNameThemeSizePng(string iconName, UiTheme theme, int size, string expected)
    {
        Assert.Equal(expected, RibbonIcons.FileName(iconName, theme, size));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(24)]
    [InlineData(48)]
    public void FileName_OtherSize_Throws(int size)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RibbonIcons.FileName("About", UiTheme.Light, size));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void FileName_BlankName_Throws(string iconName)
    {
        Assert.Throws<ArgumentException>(() => RibbonIcons.FileName(iconName, UiTheme.Light, RibbonIcons.SmallSizePx));
    }

    [Fact]
    public void Sizes_Are16And32()
    {
        Assert.Equal(16, RibbonIcons.SmallSizePx);
        Assert.Equal(32, RibbonIcons.LargeSizePx);
    }

    [Fact]
    public void Choose_OneIconPerCommand_FromItsButtons()
    {
        var choice = RibbonIcons.Choose(new[]
        {
            new ButtonLayout("CmdA", ButtonSize.Small, "IconA"),
            new ButtonLayout("CmdB", ButtonSize.Small, "IconB"),
            new ButtonLayout("CmdA", ButtonSize.Large, "IconA"),
        });

        Assert.Equal(2, choice.IconNameByCommand.Count);
        Assert.Equal("IconA", choice.IconNameFor("CmdA"));
        Assert.Equal("IconB", choice.IconNameFor("CmdB"));
        Assert.Empty(choice.Conflicts);
    }

    [Fact]
    public void Choose_TwoDifferentNamesForOneCommand_KeepsFirstAndReportsConflict()
    {
        var choice = RibbonIcons.Choose(new[]
        {
            new ButtonLayout("CmdA", ButtonSize.Small, "First"),
            new ButtonLayout("CmdA", ButtonSize.Small, "Second"),
            new ButtonLayout("CmdA", ButtonSize.Small, "Second"),
            new ButtonLayout("CmdA", ButtonSize.Small, "First"),
            new ButtonLayout("CmdA", ButtonSize.Small, "Third"),
        });

        Assert.Equal("First", choice.IconNameFor("CmdA"));
        Assert.Equal(
            new[]
            {
                new IconNameConflict("CmdA", "First", "Second"),
                new IconNameConflict("CmdA", "First", "Third"),
            },
            choice.Conflicts);
    }

    [Fact]
    public void Choose_NamesCompareExactly()
    {
        var choice = RibbonIcons.Choose(new[]
        {
            new ButtonLayout("CmdA", ButtonSize.Small, "About"),
            new ButtonLayout("CmdA", ButtonSize.Small, "about"),
        });

        Assert.Equal("About", choice.IconNameFor("CmdA"));
        Assert.Single(choice.Conflicts);
    }

    [Fact]
    public void Choose_NoButtons_IsEmpty()
    {
        var choice = RibbonIcons.Choose(Array.Empty<ButtonLayout>());

        Assert.Empty(choice.IconNameByCommand);
        Assert.Empty(choice.Conflicts);
        Assert.Null(choice.IconNameFor("CmdA"));
    }

    [Fact]
    public void Choose_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => RibbonIcons.Choose(null!));
    }

    [Fact]
    public void ForLayout_WithDeveloperTools_GivesEveryCommandItsIconName_WithoutConflicts()
    {
        var choice = RibbonIcons.ForLayout(showDeveloperTools: true);

        Assert.Empty(choice.Conflicts);
        Assert.Equal(IconNames.PartProperties, choice.IconNameFor(CommandNames.PartProperties));
        Assert.Equal(IconNames.Settings, choice.IconNameFor(CommandNames.Settings));
        Assert.Equal(IconNames.About, choice.IconNameFor(CommandNames.About));
        Assert.Equal(IconNames.ExportModelData, choice.IconNameFor(CommandNames.ExportModelData));
        Assert.Equal(IconNames.ExportLibraries, choice.IconNameFor(CommandNames.ExportLibraries));
    }

    [Fact]
    public void ForLayout_WithoutDeveloperTools_DeveloperCommandsHaveNoIcon()
    {
        var choice = RibbonIcons.ForLayout(showDeveloperTools: false);

        Assert.Empty(choice.Conflicts);
        Assert.Equal(IconNames.About, choice.IconNameFor(CommandNames.About));
        Assert.Null(choice.IconNameFor(CommandNames.ExportModelData));
        Assert.Null(choice.IconNameFor(CommandNames.ExportLibraries));
    }

    public static TheoryData<string, UiTheme, int> EveryIconThemeAndSize()
    {
        var data = new TheoryData<string, UiTheme, int>();
        foreach (string iconName in AllIconNames())
            foreach (UiTheme theme in Enum.GetValues<UiTheme>())
                foreach (int size in new[] { RibbonIcons.SmallSizePx, RibbonIcons.LargeSizePx })
                    data.Add(iconName, theme, size);
        return data;
    }

    /// <summary>The add-in embeds the PNGs in <c>src/InventorAddin/UI/Icons</c> and finds them by this file name.</summary>
    [Theory]
    [MemberData(nameof(EveryIconThemeAndSize))]
    public void EveryIcon_HasItsPngInTheAddinIconsFolder(string iconName, UiTheme theme, int size)
    {
        string path = Path.Combine(RepoRoot(), "src", "InventorAddin", "UI", "Icons", RibbonIcons.FileName(iconName, theme, size));
        Assert.True(File.Exists(path), $"Missing icon image {path}.");
    }

    private static IEnumerable<string> AllIconNames() =>
        typeof(IconNames).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!);

    private static string RepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "InventorAddin.slnx")))
                return dir.FullName;
        }
        throw new InvalidOperationException("Could not find the repository root (InventorAddin.slnx) above the test output folder.");
    }
}
