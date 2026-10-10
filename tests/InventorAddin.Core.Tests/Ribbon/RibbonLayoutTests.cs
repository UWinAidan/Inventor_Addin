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
        Assert.Equal($"id_Panel_{Branding.IdPrefix}_Dev", RibbonIds.DeveloperPanelId);
        Assert.Equal("Developer", RibbonIds.DeveloperPanelName);
        Assert.Equal($"id_Panel_{Branding.IdPrefix}_CadAutomation", RibbonIds.CadAutomationPanelId);
        Assert.Equal("CAD Automation", RibbonIds.CadAutomationPanelName);
        Assert.Equal($"id_Panel_{Branding.IdPrefix}_DrawingTools", RibbonIds.DrawingToolsPanelId);
        Assert.Equal("Drawing Tools", RibbonIds.DrawingToolsPanelName);
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Part_FirstPanelIsCadAutomation_WithSmallNewPartThenSaveAsCopyThenPartPropertiesThenSettingsThenAbout(bool showDeveloperTools)
    {
        var panel = RibbonLayout.For(RibbonEnvironment.Part, showDeveloperTools)[0];

        Assert.Equal($"id_Panel_{Branding.IdPrefix}_CadAutomation", panel.Id);
        Assert.Equal("CAD Automation", panel.DisplayName);
        Assert.Equal(
            new[]
            {
                new ButtonLayout($"{Branding.IdPrefix}_NewPart", ButtonSize.Small, IconNames.NewPart),
                new ButtonLayout($"{Branding.IdPrefix}_SaveAsCopy", ButtonSize.Small, IconNames.SaveAsCopy),
                new ButtonLayout($"{Branding.IdPrefix}_PartProperties", ButtonSize.Small, IconNames.PartProperties),
                new ButtonLayout($"{Branding.IdPrefix}_Settings", ButtonSize.Small, IconNames.Settings),
                new ButtonLayout($"{Branding.IdPrefix}_About", ButtonSize.Small, IconNames.About),
            },
            panel.Buttons);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Assembly_FirstPanelIsCadAutomation_WithSaveAsReplaceAfterSaveAsCopy(bool showDeveloperTools)
    {
        var panel = RibbonLayout.For(RibbonEnvironment.Assembly, showDeveloperTools)[0];

        Assert.Equal($"id_Panel_{Branding.IdPrefix}_CadAutomation", panel.Id);
        Assert.Equal("CAD Automation", panel.DisplayName);
        Assert.Equal(
            new[]
            {
                new ButtonLayout($"{Branding.IdPrefix}_NewPart", ButtonSize.Small, IconNames.NewPart),
                new ButtonLayout($"{Branding.IdPrefix}_SaveAsCopy", ButtonSize.Small, IconNames.SaveAsCopy),
                new ButtonLayout($"{Branding.IdPrefix}_SaveAsReplace", ButtonSize.Small, IconNames.SaveAsReplace),
                new ButtonLayout($"{Branding.IdPrefix}_PartProperties", ButtonSize.Small, IconNames.PartProperties),
                new ButtonLayout($"{Branding.IdPrefix}_Settings", ButtonSize.Small, IconNames.Settings),
                new ButtonLayout($"{Branding.IdPrefix}_About", ButtonSize.Small, IconNames.About),
            },
            panel.Buttons);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Drawing_FirstPanelIsCadAutomation_WithSmallSettingsThenAboutButtons(bool showDeveloperTools)
    {
        var panel = RibbonLayout.For(RibbonEnvironment.Drawing, showDeveloperTools)[0];

        Assert.Equal($"id_Panel_{Branding.IdPrefix}_CadAutomation", panel.Id);
        Assert.Equal("CAD Automation", panel.DisplayName);
        Assert.Equal(
            new[]
            {
                new ButtonLayout($"{Branding.IdPrefix}_Settings", ButtonSize.Small, IconNames.Settings),
                new ButtonLayout($"{Branding.IdPrefix}_About", ButtonSize.Small, IconNames.About),
            },
            panel.Buttons);
    }

    [Theory]
    [MemberData(nameof(EveryEnvironmentAndSetting))]
    public void PartProperties_OnlyOnPartAndAssembly(RibbonEnvironment environment, bool showDeveloperTools)
    {
        var names = RibbonLayout.For(environment, showDeveloperTools)
            .SelectMany(p => p.Buttons)
            .Select(b => b.CommandInternalName)
            .ToList();

        bool expected = environment is RibbonEnvironment.Part or RibbonEnvironment.Assembly;
        Assert.Equal(expected, names.Contains(CommandNames.PartProperties));
    }

    [Theory]
    [MemberData(nameof(EveryEnvironmentAndSetting))]
    public void NewPart_OnlyOnZeroDocPartAndAssembly_FirstInCadAutomation(RibbonEnvironment environment, bool showDeveloperTools)
    {
        var panels = RibbonLayout.For(environment, showDeveloperTools);
        var names = panels.SelectMany(p => p.Buttons).Select(b => b.CommandInternalName).ToList();

        bool expected = environment is RibbonEnvironment.ZeroDoc or RibbonEnvironment.Part or RibbonEnvironment.Assembly;
        Assert.Equal(expected, names.Contains(CommandNames.NewPart));
        if (expected)
        {
            var panel = Assert.Single(panels, p => p.Id == RibbonIds.CadAutomationPanelId);
            Assert.Equal(
                new ButtonLayout(CommandNames.NewPart, ButtonSize.Small, IconNames.NewPart),
                panel.Buttons[0]);
        }
    }

    [Theory]
    [MemberData(nameof(EveryEnvironmentAndSetting))]
    public void SaveAsCopy_OnlyOnPartAndAssembly_RightAfterNewPart(RibbonEnvironment environment, bool showDeveloperTools)
    {
        var panels = RibbonLayout.For(environment, showDeveloperTools);
        var names = panels.SelectMany(p => p.Buttons).Select(b => b.CommandInternalName).ToList();

        bool expected = environment is RibbonEnvironment.Part or RibbonEnvironment.Assembly;
        Assert.Equal(expected, names.Contains(CommandNames.SaveAsCopy));
        if (expected)
        {
            var panel = Assert.Single(panels, p => p.Id == RibbonIds.CadAutomationPanelId);
            var newPart = panel.Buttons.ToList().FindIndex(b => b.CommandInternalName == CommandNames.NewPart);
            Assert.Equal(
                new ButtonLayout(CommandNames.SaveAsCopy, ButtonSize.Small, IconNames.SaveAsCopy),
                panel.Buttons[newPart + 1]);
        }
    }

    [Theory]
    [MemberData(nameof(EveryEnvironmentAndSetting))]
    public void SaveAsReplace_OnlyOnAssembly_RightAfterSaveAsCopy(RibbonEnvironment environment, bool showDeveloperTools)
    {
        var panels = RibbonLayout.For(environment, showDeveloperTools);
        var names = panels.SelectMany(p => p.Buttons).Select(b => b.CommandInternalName).ToList();

        bool expected = environment is RibbonEnvironment.Assembly;
        Assert.Equal(expected, names.Contains(CommandNames.SaveAsReplace));
        if (expected)
        {
            var panel = Assert.Single(panels, p => p.Id == RibbonIds.CadAutomationPanelId);
            var saveAsCopy = panel.Buttons.ToList().FindIndex(b => b.CommandInternalName == CommandNames.SaveAsCopy);
            Assert.Equal(
                new ButtonLayout(CommandNames.SaveAsReplace, ButtonSize.Small, IconNames.SaveAsReplace),
                panel.Buttons[saveAsCopy + 1]);
        }
    }

    [Theory]
    [MemberData(nameof(EveryEnvironmentAndSetting))]
    public void CreatePrint_OnlyOnPartAndAssembly_AsTheLargeButtonOnDrawingTools(RibbonEnvironment environment, bool showDeveloperTools)
    {
        var panels = RibbonLayout.For(environment, showDeveloperTools);
        var names = panels.SelectMany(p => p.Buttons).Select(b => b.CommandInternalName).ToList();

        bool expected = environment is RibbonEnvironment.Part or RibbonEnvironment.Assembly;
        Assert.Equal(expected, names.Contains(CommandNames.CreatePrint));
        if (expected)
        {
            var panel = Assert.Single(panels, p => p.Id == RibbonIds.DrawingToolsPanelId);
            Assert.Equal("Drawing Tools", panel.DisplayName);
            Assert.Equal(
                new[] { new ButtonLayout($"{Branding.IdPrefix}_CreatePrint", ButtonSize.Large, IconNames.CreatePrint) },
                panel.Buttons);
        }
    }

    [Theory]
    [MemberData(nameof(DocumentEnvironmentsAndSetting))]
    public void DocumentEnvironments_AlwaysHaveTheTab(RibbonEnvironment environment, bool showDeveloperTools)
    {
        Assert.True(RibbonLayout.HasPanels(environment, showDeveloperTools));
    }

    [Theory]
    [InlineData(RibbonEnvironment.Part)]
    [InlineData(RibbonEnvironment.Assembly)]
    public void ModelEnvironments_WithDeveloperTools_CadAutomationThenDrawingToolsThenDeveloper(RibbonEnvironment environment)
    {
        var ids = RibbonLayout.For(environment, showDeveloperTools: true).Select(p => p.Id);

        Assert.Equal(new[] { RibbonIds.CadAutomationPanelId, RibbonIds.DrawingToolsPanelId, RibbonIds.DeveloperPanelId }, ids);
    }

    [Theory]
    [InlineData(RibbonEnvironment.Part)]
    [InlineData(RibbonEnvironment.Assembly)]
    public void ModelEnvironments_WithoutDeveloperTools_CadAutomationThenDrawingTools(RibbonEnvironment environment)
    {
        var ids = RibbonLayout.For(environment, showDeveloperTools: false).Select(p => p.Id);

        Assert.Equal(new[] { RibbonIds.CadAutomationPanelId, RibbonIds.DrawingToolsPanelId }, ids);
    }

    [Fact]
    public void Drawing_WithDeveloperTools_CadAutomationThenDeveloper()
    {
        var ids = RibbonLayout.For(RibbonEnvironment.Drawing, showDeveloperTools: true).Select(p => p.Id);

        Assert.Equal(new[] { RibbonIds.CadAutomationPanelId, RibbonIds.DeveloperPanelId }, ids);
    }

    [Fact]
    public void Drawing_WithoutDeveloperTools_OnlyCadAutomation()
    {
        var panel = Assert.Single(RibbonLayout.For(RibbonEnvironment.Drawing, showDeveloperTools: false));

        Assert.Equal(RibbonIds.CadAutomationPanelId, panel.Id);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ZeroDoc_HasNoSettingsOrAboutButton(bool showDeveloperTools)
    {
        var names = RibbonLayout.For(RibbonEnvironment.ZeroDoc, showDeveloperTools)
            .SelectMany(p => p.Buttons)
            .Select(b => b.CommandInternalName)
            .ToList();

        Assert.DoesNotContain(CommandNames.Settings, names);
        Assert.DoesNotContain(CommandNames.About, names);
    }

    [Theory]
    [InlineData(RibbonEnvironment.Part)]
    [InlineData(RibbonEnvironment.Assembly)]
    [InlineData(RibbonEnvironment.Drawing)]
    public void DocumentEnvironments_WithDeveloperTools_ShowBothExportButtons(RibbonEnvironment environment)
    {
        var panel = Assert.Single(
            RibbonLayout.For(environment, showDeveloperTools: true),
            p => p.Id == $"id_Panel_{Branding.IdPrefix}_Dev");

        Assert.Equal("Developer", panel.DisplayName);
        Assert.Equal(
            new[]
            {
                new ButtonLayout($"{Branding.IdPrefix}_ExportModelData", ButtonSize.Small, IconNames.ExportModelData),
                new ButtonLayout($"{Branding.IdPrefix}_ExportLibraries", ButtonSize.Small, IconNames.ExportLibraries),
            },
            panel.Buttons);
    }

    [Fact]
    public void ZeroDoc_WithDeveloperTools_NewPartThenExportLibraries()
    {
        var panels = RibbonLayout.For(RibbonEnvironment.ZeroDoc, showDeveloperTools: true);

        Assert.Equal(new[] { RibbonIds.CadAutomationPanelId, RibbonIds.DeveloperPanelId }, panels.Select(p => p.Id));
        Assert.Equal(
            new[] { new ButtonLayout($"{Branding.IdPrefix}_NewPart", ButtonSize.Small, IconNames.NewPart) },
            panels[0].Buttons);
        Assert.Equal("Developer", panels[1].DisplayName);
        Assert.Equal(
            new[] { new ButtonLayout($"{Branding.IdPrefix}_ExportLibraries", ButtonSize.Small, IconNames.ExportLibraries) },
            panels[1].Buttons);
    }

    [Fact]
    public void ZeroDoc_WithoutDeveloperTools_OnlyNewPart()
    {
        // No Settings or About on ZeroDoc yet (spec 01 open question 4), so New Part is the only button.
        var panel = Assert.Single(RibbonLayout.For(RibbonEnvironment.ZeroDoc, showDeveloperTools: false));

        Assert.Equal(RibbonIds.CadAutomationPanelId, panel.Id);
        Assert.Equal(
            new[] { new ButtonLayout($"{Branding.IdPrefix}_NewPart", ButtonSize.Small, IconNames.NewPart) },
            panel.Buttons);
        Assert.True(RibbonLayout.HasPanels(RibbonEnvironment.ZeroDoc, showDeveloperTools: false));
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
    public void DeveloperPanelIsLast_AndPanelIdsAreUnique(RibbonEnvironment environment, bool showDeveloperTools)
    {
        var ids = RibbonLayout.For(environment, showDeveloperTools).Select(p => p.Id).ToList();

        Assert.Equal(ids.Count, ids.Distinct().Count());
        if (ids.Contains(RibbonIds.DeveloperPanelId))
            Assert.Equal(RibbonIds.DeveloperPanelId, ids[^1]);
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

        Assert.Equal(9, iconNames.Count);
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
