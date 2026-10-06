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
        Assert.Equal("id_Tab_WorkflowTools", RibbonIds.TabId);
        Assert.Equal("Workflow Tools", RibbonIds.TabName);
        Assert.Equal("id_Panel_WorkflowTools_Dev", RibbonIds.DeveloperPanelId);
        Assert.Equal("Developer", RibbonIds.DeveloperPanelName);
        Assert.Equal("id_Panel_WorkflowTools_CadAutomation", RibbonIds.CadAutomationPanelId);
        Assert.Equal("CAD Automation", RibbonIds.CadAutomationPanelName);
        Assert.Equal("id_Panel_WorkflowTools_DrawingTools", RibbonIds.DrawingToolsPanelId);
        Assert.Equal("Drawing Tools", RibbonIds.DrawingToolsPanelName);
    }

    [Fact]
    public void CommandNames_MatchExistingCommands()
    {
        Assert.Equal("WorkflowTools_ExportModelData", CommandNames.ExportModelData);
        Assert.Equal("WorkflowTools_ExportLibraries", CommandNames.ExportLibraries);
        Assert.Equal("WorkflowTools_Settings", CommandNames.Settings);
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
    [MemberData(nameof(DocumentEnvironmentsAndSetting))]
    public void DocumentEnvironments_FirstPanelIsCadAutomation_WithSmallSettingsButton(RibbonEnvironment environment, bool showDeveloperTools)
    {
        var panel = RibbonLayout.For(environment, showDeveloperTools)[0];

        Assert.Equal("id_Panel_WorkflowTools_CadAutomation", panel.Id);
        Assert.Equal("CAD Automation", panel.DisplayName);
        Assert.Equal(new[] { new ButtonLayout("WorkflowTools_Settings", ButtonSize.Small) }, panel.Buttons);
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
    [InlineData(RibbonEnvironment.Drawing)]
    public void DocumentEnvironments_WithDeveloperTools_CadAutomationThenDeveloper(RibbonEnvironment environment)
    {
        var ids = RibbonLayout.For(environment, showDeveloperTools: true).Select(p => p.Id);

        Assert.Equal(new[] { RibbonIds.CadAutomationPanelId, RibbonIds.DeveloperPanelId }, ids);
    }

    [Theory]
    [InlineData(RibbonEnvironment.Part)]
    [InlineData(RibbonEnvironment.Assembly)]
    [InlineData(RibbonEnvironment.Drawing)]
    public void DocumentEnvironments_WithoutDeveloperTools_OnlyCadAutomation(RibbonEnvironment environment)
    {
        var panel = Assert.Single(RibbonLayout.For(environment, showDeveloperTools: false));

        Assert.Equal(RibbonIds.CadAutomationPanelId, panel.Id);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ZeroDoc_HasNoSettingsButton(bool showDeveloperTools)
    {
        var names = RibbonLayout.For(RibbonEnvironment.ZeroDoc, showDeveloperTools)
            .SelectMany(p => p.Buttons)
            .Select(b => b.CommandInternalName);

        Assert.DoesNotContain(CommandNames.Settings, names);
    }

    [Theory]
    [InlineData(RibbonEnvironment.Part)]
    [InlineData(RibbonEnvironment.Assembly)]
    [InlineData(RibbonEnvironment.Drawing)]
    public void DocumentEnvironments_WithDeveloperTools_ShowBothExportButtons(RibbonEnvironment environment)
    {
        var panel = Assert.Single(
            RibbonLayout.For(environment, showDeveloperTools: true),
            p => p.Id == "id_Panel_WorkflowTools_Dev");

        Assert.Equal("Developer", panel.DisplayName);
        Assert.Equal(
            new[]
            {
                new ButtonLayout("WorkflowTools_ExportModelData", ButtonSize.Small),
                new ButtonLayout("WorkflowTools_ExportLibraries", ButtonSize.Small),
            },
            panel.Buttons);
    }

    [Fact]
    public void ZeroDoc_WithDeveloperTools_ShowsOnlyExportLibraries()
    {
        var panel = Assert.Single(RibbonLayout.For(RibbonEnvironment.ZeroDoc, showDeveloperTools: true));

        Assert.Equal("id_Panel_WorkflowTools_Dev", panel.Id);
        Assert.Equal("Developer", panel.DisplayName);
        Assert.Equal(
            new[] { new ButtonLayout("WorkflowTools_ExportLibraries", ButtonSize.Small) },
            panel.Buttons);
    }

    [Fact]
    public void ZeroDoc_WithoutDeveloperTools_NoPanels()
    {
        // ZeroDoc has only the Developer panel (spec 01 open question 4), so turning it off leaves nothing.
        Assert.Empty(RibbonLayout.For(RibbonEnvironment.ZeroDoc, showDeveloperTools: false));
        Assert.False(RibbonLayout.HasPanels(RibbonEnvironment.ZeroDoc, showDeveloperTools: false));
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
