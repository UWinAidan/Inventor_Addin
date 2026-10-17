using InventorAddin.Core.Ribbon;

namespace InventorAddin.Core.Tests.Ribbon;

public sealed class RibbonLayoutTests
{
    public static TheoryData<RibbonEnvironment, bool> EveryEnvironmentAndSetting()
    {
        var data = new TheoryData<RibbonEnvironment, bool>();
        foreach (var environment in Enum.GetValues<RibbonEnvironment>())
        {
            data.Add(environment, false);
            data.Add(environment, true);
        }
        return data;
    }

    [Theory]
    [InlineData(RibbonEnvironment.ZeroDoc, "ZeroDoc")]
    [InlineData(RibbonEnvironment.Part, "Part")]
    [InlineData(RibbonEnvironment.Assembly, "Assembly")]
    [InlineData(RibbonEnvironment.Drawing, "Drawing")]
    public void RibbonName_MatchesInventorRibbonName(RibbonEnvironment environment, string expected)
    {
        Assert.Equal(expected, environment.RibbonName());
    }

    [Fact]
    public void All_ListsEveryEnvironmentOnce()
    {
        Assert.Equal(Enum.GetValues<RibbonEnvironment>(), RibbonEnvironments.All);
    }

    [Fact]
    public void TabAndPanelConstants()
    {
        Assert.Equal($"id_Tab_{Branding.IdPrefix}", RibbonIds.TabId);
        Assert.Equal(Branding.TabName, RibbonIds.TabName);
        Assert.Equal($"id_Panel_{Branding.IdPrefix}_Create", RibbonIds.CreatePanelId);
        Assert.Equal("Create", RibbonIds.CreatePanelName);
        Assert.Equal($"id_Panel_{Branding.IdPrefix}_Properties", RibbonIds.PropertiesPanelId);
        Assert.Equal("Properties", RibbonIds.PropertiesPanelName);
        Assert.Equal($"id_Panel_{Branding.IdPrefix}_Drawing", RibbonIds.DrawingPanelId);
        Assert.Equal("Drawing", RibbonIds.DrawingPanelName);
        Assert.Equal($"id_Panel_{Branding.IdPrefix}_Dev", RibbonIds.DeveloperPanelId);
        Assert.Equal("Developer", RibbonIds.DeveloperPanelName);
        Assert.Equal($"id_Panel_{Branding.IdPrefix}_Awb", RibbonIds.AwbPanelId);
        Assert.Equal(Branding.ShortName, RibbonIds.AwbPanelName);
    }

    [Fact]
    public void CommandNames_MatchExistingCommands()
    {
        Assert.Equal($"{Branding.IdPrefix}_ExportModelData", CommandNames.ExportModelData);
        Assert.Equal($"{Branding.IdPrefix}_ExportLibraries", CommandNames.ExportLibraries);
        Assert.Equal($"{Branding.IdPrefix}_Settings", CommandNames.Settings);
        Assert.Equal($"{Branding.IdPrefix}_About", CommandNames.About);
        Assert.Equal($"{Branding.IdPrefix}_PartProperties", CommandNames.PartProperties);
        Assert.Equal($"{Branding.IdPrefix}_NewPart", CommandNames.NewPart);
        Assert.Equal($"{Branding.IdPrefix}_SaveAsCopy", CommandNames.SaveAsCopy);
        Assert.Equal($"{Branding.IdPrefix}_SaveAsReplace", CommandNames.SaveAsReplace);
        Assert.Equal($"{Branding.IdPrefix}_CreatePrint", CommandNames.CreatePrint);
        Assert.Equal($"{Branding.IdPrefix}_MassUpdate", CommandNames.MassUpdate);
        Assert.Equal($"{Branding.IdPrefix}_MaterialFinish", CommandNames.MaterialFinish);
    }

    public static TheoryData<RibbonEnvironment, bool> DocumentEnvironmentsAndSetting()
    {
        var data = new TheoryData<RibbonEnvironment, bool>();
        foreach (var environment in new[] { RibbonEnvironment.Part, RibbonEnvironment.Assembly, RibbonEnvironment.Drawing })
        {
            data.Add(environment, false);
            data.Add(environment, true);
        }
        return data;
    }

    // The whole layout, pinned per ribbon and developer setting (Aidan, 2026-10-12, spec 01 "Target"):
    // Create, Properties, Drawing, Developer (only when on), AWB far right.
    private static ButtonLayout Small(string iconName) =>
        new($"{Branding.IdPrefix}_{iconName}", ButtonSize.Small, iconName);

    private static ButtonLayout Large(string iconName) =>
        new($"{Branding.IdPrefix}_{iconName}", ButtonSize.Large, iconName);

    private static PanelLayout Create(params ButtonLayout[] buttons) =>
        new($"id_Panel_{Branding.IdPrefix}_Create", "Create", buttons);

    private static PanelLayout Properties(params ButtonLayout[] buttons) =>
        new($"id_Panel_{Branding.IdPrefix}_Properties", "Properties", buttons);

    private static PanelLayout DrawingPanel(params ButtonLayout[] buttons) =>
        new($"id_Panel_{Branding.IdPrefix}_Drawing", "Drawing", buttons);

    private static PanelLayout Developer(params ButtonLayout[] buttons) =>
        new($"id_Panel_{Branding.IdPrefix}_Dev", "Developer", buttons);

    private static PanelLayout Awb() =>
        new($"id_Panel_{Branding.IdPrefix}_Awb", Branding.ShortName, new[] { Small("Settings"), Small("About") });

    private static PanelLayout[] ExpectedLayout(RibbonEnvironment environment, bool showDeveloperTools) =>
        (environment, showDeveloperTools) switch
        {
            // Settings and About on every ribbon (Aidan, 2026-10-17, spec 01 question 4), so ZeroDoc has the AWB panel.
            (RibbonEnvironment.ZeroDoc, false) => new[]
            {
                Create(Large("NewPart")),
                Properties(Small("MassUpdate")),
                Awb(),
            },
            (RibbonEnvironment.ZeroDoc, true) => new[]
            {
                Create(Large("NewPart")),
                Properties(Small("MassUpdate")),
                Developer(Small("ExportLibraries")),
                Awb(),
            },
            (RibbonEnvironment.Part, false) => new[]
            {
                Create(Large("NewPart"), Small("SaveAsCopy")),
                Properties(Large("PartProperties"), Small("MassUpdate"), Small("MaterialFinish")),
                DrawingPanel(Large("CreatePrint")),
                Awb(),
            },
            (RibbonEnvironment.Part, true) => new[]
            {
                Create(Large("NewPart"), Small("SaveAsCopy")),
                Properties(Large("PartProperties"), Small("MassUpdate"), Small("MaterialFinish")),
                DrawingPanel(Large("CreatePrint")),
                Developer(Small("ExportModelData"), Small("ExportLibraries")),
                Awb(),
            },
            (RibbonEnvironment.Assembly, false) => new[]
            {
                Create(Large("NewPart"), Small("SaveAsCopy"), Small("SaveAsReplace")),
                Properties(Large("PartProperties"), Small("MassUpdate"), Small("MaterialFinish")),
                DrawingPanel(Large("CreatePrint")),
                Awb(),
            },
            (RibbonEnvironment.Assembly, true) => new[]
            {
                Create(Large("NewPart"), Small("SaveAsCopy"), Small("SaveAsReplace")),
                Properties(Large("PartProperties"), Small("MassUpdate"), Small("MaterialFinish")),
                DrawingPanel(Large("CreatePrint")),
                Developer(Small("ExportModelData"), Small("ExportLibraries")),
                Awb(),
            },
            (RibbonEnvironment.Drawing, false) => new[]
            {
                Awb(),
            },
            (RibbonEnvironment.Drawing, true) => new[]
            {
                Developer(Small("ExportModelData"), Small("ExportLibraries")),
                Awb(),
            },
            _ => throw new ArgumentOutOfRangeException(nameof(environment)),
        };

    [Theory]
    [MemberData(nameof(EveryEnvironmentAndSetting))]
    public void For_ReturnsThePanelsInOrder_WithTheirButtonsInOrder(RibbonEnvironment environment, bool showDeveloperTools)
    {
        var expected = ExpectedLayout(environment, showDeveloperTools);
        var actual = RibbonLayout.For(environment, showDeveloperTools);

        Assert.Equal(expected.Select(p => p.Id), actual.Select(p => p.Id));
        for (int i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i].DisplayName, actual[i].DisplayName);
            Assert.Equal(expected[i].Buttons, actual[i].Buttons);
        }
    }

    [Theory]
    [MemberData(nameof(EveryEnvironmentAndSetting))]
    public void SmallButtons_SitTogetherInEachPanel_SoInventorStacksThem(RibbonEnvironment environment, bool showDeveloperTools)
    {
        foreach (var panel in RibbonLayout.For(environment, showDeveloperTools))
        {
            var sizes = panel.Buttons.Select(b => b.Size).ToList();
            int first = sizes.IndexOf(ButtonSize.Small);
            int last = sizes.LastIndexOf(ButtonSize.Small);
            if (first < 0)
                continue;
            Assert.All(sizes.Skip(first).Take(last - first + 1), s => Assert.Equal(ButtonSize.Small, s));
        }
    }

    [Theory]
    [MemberData(nameof(DocumentEnvironmentsAndSetting))]
    public void DocumentEnvironments_AlwaysHaveTheTab(RibbonEnvironment environment, bool showDeveloperTools)
    {
        Assert.True(RibbonLayout.HasPanels(environment, showDeveloperTools));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EveryEnvironment_HasSettingsAndAbout_InTheAwbPanelFarRight(bool showDeveloperTools)
    {
        // Aidan, 2026-10-17 (spec 01 question 4): with no document open too.
        foreach (var environment in RibbonEnvironments.All)
        {
            var panels = RibbonLayout.For(environment, showDeveloperTools);
            var last = panels[^1];

            Assert.Equal(RibbonIds.AwbPanelId, last.Id);
            Assert.Equal(new[] { CommandNames.Settings, CommandNames.About }, last.Buttons.Select(b => b.CommandInternalName));
        }
    }

    [Theory]
    [MemberData(nameof(EveryEnvironmentAndSetting))]
    public void DeveloperPanel_PresentOnlyWhenEnabled(RibbonEnvironment environment, bool showDeveloperTools)
    {
        var ids = RibbonLayout.For(environment, showDeveloperTools).Select(p => p.Id);

        Assert.Equal(showDeveloperTools, ids.Contains(RibbonIds.DeveloperPanelId));
    }

    [Theory]
    [MemberData(nameof(EveryEnvironmentAndSetting))]
    public void NoPanelIsEverEmpty(RibbonEnvironment environment, bool showDeveloperTools)
    {
        Assert.All(RibbonLayout.For(environment, showDeveloperTools), p => Assert.NotEmpty(p.Buttons));
    }

    [Theory]
    [MemberData(nameof(EveryEnvironmentAndSetting))]
    public void HasPanels_AgreesWithFor(RibbonEnvironment environment, bool showDeveloperTools)
    {
        Assert.Equal(
            RibbonLayout.For(environment, showDeveloperTools).Count > 0,
            RibbonLayout.HasPanels(environment, showDeveloperTools));
    }

    [Theory]
    [MemberData(nameof(EveryEnvironmentAndSetting))]
    public void AwbPanelIsLast_DeveloperRightBeforeIt_AndPanelIdsAreUnique(RibbonEnvironment environment, bool showDeveloperTools)
    {
        var ids = RibbonLayout.For(environment, showDeveloperTools).Select(p => p.Id).ToList();

        Assert.Equal(ids.Count, ids.Distinct().Count());
        if (ids.Contains(RibbonIds.AwbPanelId))
            Assert.Equal(RibbonIds.AwbPanelId, ids[^1]);
        if (ids.Contains(RibbonIds.DeveloperPanelId))
        {
            int expected = ids.Contains(RibbonIds.AwbPanelId) ? ids.Count - 2 : ids.Count - 1;
            Assert.Equal(expected, ids.IndexOf(RibbonIds.DeveloperPanelId));
        }
    }

    [Theory]
    [MemberData(nameof(EveryEnvironmentAndSetting))]
    public void CommandAppearsAtMostOncePerEnvironment(RibbonEnvironment environment, bool showDeveloperTools)
    {
        var names = RibbonLayout.For(environment, showDeveloperTools)
            .SelectMany(p => p.Buttons)
            .Select(b => b.CommandInternalName)
            .ToList();

        Assert.Equal(names.Count, names.Distinct().Count());
    }

    [Theory]
    [MemberData(nameof(EveryEnvironmentAndSetting))]
    public void EveryButton_HasAnIconName(RibbonEnvironment environment, bool showDeveloperTools)
    {
        Assert.All(
            RibbonLayout.For(environment, showDeveloperTools).SelectMany(p => p.Buttons),
            b => Assert.False(string.IsNullOrWhiteSpace(b.IconName), $"{b.CommandInternalName} has no icon name."));
    }

    [Theory]
    [InlineData(CommandNames.PartProperties, IconNames.PartProperties)]
    [InlineData(CommandNames.Settings, IconNames.Settings)]
    [InlineData(CommandNames.About, IconNames.About)]
    [InlineData(CommandNames.ExportModelData, IconNames.ExportModelData)]
    [InlineData(CommandNames.ExportLibraries, IconNames.ExportLibraries)]
    [InlineData(CommandNames.NewPart, IconNames.NewPart)]
    [InlineData(CommandNames.CreatePrint, IconNames.CreatePrint)]
    [InlineData(CommandNames.SaveAsCopy, IconNames.SaveAsCopy)]
    [InlineData(CommandNames.SaveAsReplace, IconNames.SaveAsReplace)]
    [InlineData(CommandNames.MassUpdate, IconNames.MassUpdate)]
    [InlineData(CommandNames.MaterialFinish, IconNames.MaterialFinish)]
    public void EachCommand_UsesItsIconName_InEveryEnvironment(string command, string iconName)
    {
        var buttons = RibbonEnvironments.All
            .SelectMany(e => RibbonLayout.For(e, showDeveloperTools: true))
            .SelectMany(p => p.Buttons)
            .Where(b => b.CommandInternalName == command)
            .ToList();

        Assert.NotEmpty(buttons);
        Assert.All(buttons, b => Assert.Equal(iconName, b.IconName));
    }

    // Aidan, 2026-10-11 (spec 02): New Part, Part Properties and Create Print are large; every other button is small.
    // Changing a size here should be a deliberate decision, not a side effect.
    [Theory]
    [InlineData(CommandNames.NewPart, ButtonSize.Large)]
    [InlineData(CommandNames.PartProperties, ButtonSize.Large)]
    [InlineData(CommandNames.CreatePrint, ButtonSize.Large)]
    [InlineData(CommandNames.SaveAsCopy, ButtonSize.Small)]
    [InlineData(CommandNames.SaveAsReplace, ButtonSize.Small)]
    [InlineData(CommandNames.Settings, ButtonSize.Small)]
    [InlineData(CommandNames.About, ButtonSize.Small)]
    [InlineData(CommandNames.MassUpdate, ButtonSize.Small)]
    [InlineData(CommandNames.MaterialFinish, ButtonSize.Small)]
    [InlineData(CommandNames.ExportModelData, ButtonSize.Small)]
    [InlineData(CommandNames.ExportLibraries, ButtonSize.Small)]
    public void EachCommand_HasItsSize_InEveryEnvironment(string command, ButtonSize size)
    {
        var buttons = RibbonEnvironments.All
            .SelectMany(e => RibbonLayout.For(e, showDeveloperTools: true))
            .SelectMany(p => p.Buttons)
            .Where(b => b.CommandInternalName == command)
            .ToList();

        Assert.NotEmpty(buttons);
        Assert.All(buttons, b => Assert.Equal(size, b.Size));
    }

    [Fact]
    public void ButtonSizes_CoverEveryButtonOnTheRibbon()
    {
        // When a button is added, add it to EachCommand_HasItsSize_InEveryEnvironment as well.
        var commands = RibbonEnvironments.All
            .SelectMany(e => RibbonLayout.For(e, showDeveloperTools: true))
            .SelectMany(p => p.Buttons)
            .Select(b => b.CommandInternalName)
            .Distinct()
            .OrderBy(n => n, StringComparer.Ordinal);

        Assert.Equal(
            new[]
            {
                CommandNames.About, CommandNames.CreatePrint, CommandNames.ExportLibraries, CommandNames.ExportModelData,
                CommandNames.MassUpdate, CommandNames.MaterialFinish, CommandNames.NewPart, CommandNames.PartProperties, CommandNames.SaveAsCopy,
                CommandNames.SaveAsReplace, CommandNames.Settings,
            }.OrderBy(n => n, StringComparer.Ordinal),
            commands);
    }

    [Fact]
    public void IconNames_AreCommandNamesWithoutPrefix()
    {
        string prefix = Branding.IdPrefix + "_";
        Assert.Equal(CommandNames.PartProperties, prefix + IconNames.PartProperties);
        Assert.Equal(CommandNames.Settings, prefix + IconNames.Settings);
        Assert.Equal(CommandNames.About, prefix + IconNames.About);
        Assert.Equal(CommandNames.ExportModelData, prefix + IconNames.ExportModelData);
        Assert.Equal(CommandNames.ExportLibraries, prefix + IconNames.ExportLibraries);
        Assert.Equal(CommandNames.NewPart, prefix + IconNames.NewPart);
        Assert.Equal(CommandNames.CreatePrint, prefix + IconNames.CreatePrint);
        Assert.Equal(CommandNames.SaveAsCopy, prefix + IconNames.SaveAsCopy);
        Assert.Equal(CommandNames.SaveAsReplace, prefix + IconNames.SaveAsReplace);
        Assert.Equal(CommandNames.MassUpdate, prefix + IconNames.MassUpdate);
        Assert.Equal(CommandNames.MaterialFinish, prefix + IconNames.MaterialFinish);
    }

    [Fact]
    public void IconNames_AreDistinct()
    {
        var iconNames = RibbonEnvironments.All
            .SelectMany(e => RibbonLayout.For(e, showDeveloperTools: true))
            .SelectMany(p => p.Buttons)
            .GroupBy(b => b.CommandInternalName)
            .Select(g => g.First().IconName)
            .ToList();

        Assert.Equal(11, iconNames.Count);
        Assert.Equal(iconNames.Count, iconNames.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void For_RejectsUndefinedEnvironment()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RibbonLayout.For((RibbonEnvironment)99, showDeveloperTools: true));
    }

    [Fact]
    public void RibbonName_RejectsUndefinedEnvironment()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ((RibbonEnvironment)99).RibbonName());
    }
}
