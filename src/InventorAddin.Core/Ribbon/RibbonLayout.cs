namespace InventorAddin.Core.Ribbon;

/// <summary>
/// Decides which panels and buttons the add-in's tab (<see cref="RibbonIds.TabName"/>) shows in each environment.
/// The add-in draws exactly what this returns.
/// </summary>
/// <remarks>
/// A button is listed only once its command exists (spec 01). Feature tasks add their own entries to
/// <see cref="Definitions"/>. Panel order follows the spec: CAD Automation, Drawing Tools, Modelling, Developer last.
/// </remarks>
public static class RibbonLayout
{
    private sealed record ButtonDefinition(string CommandInternalName, ButtonSize Size, string IconName, RibbonEnvironment[] Environments);

    private sealed record PanelDefinition(string Id, string DisplayName, bool DeveloperOnly, ButtonDefinition[] Buttons);

    private static readonly RibbonEnvironment[] AllEnvironments = RibbonEnvironments.All.ToArray();

    private static readonly RibbonEnvironment[] DocumentEnvironments =
    {
        RibbonEnvironment.Part,
        RibbonEnvironment.Assembly,
        RibbonEnvironment.Drawing,
    };

    private static readonly RibbonEnvironment[] ModelEnvironments =
    {
        RibbonEnvironment.Part,
        RibbonEnvironment.Assembly,
    };

    // Spec 02, "Commands: Save As and Replace": a component selected in an assembly.
    private static readonly RibbonEnvironment[] AssemblyEnvironments =
    {
        RibbonEnvironment.Assembly,
    };

    // Spec 02, "Commands: New Part": also with no document open.
    private static readonly RibbonEnvironment[] NewFileEnvironments =
    {
        RibbonEnvironment.ZeroDoc,
        RibbonEnvironment.Part,
        RibbonEnvironment.Assembly,
    };

    private static readonly PanelDefinition[] Definitions =
    {
        new(RibbonIds.CadAutomationPanelId, RibbonIds.CadAutomationPanelName, DeveloperOnly: false, new ButtonDefinition[]
        {
            new(CommandNames.NewPart, ButtonSize.Small, IconNames.NewPart, NewFileEnvironments),
            // Spec 02, "Commands: Save As Copy": a copy of the active part or assembly.
            new(CommandNames.SaveAsCopy, ButtonSize.Small, IconNames.SaveAsCopy, ModelEnvironments),
            // Spec 02, "Commands: Save As and Replace": the selected component in the active assembly.
            new(CommandNames.SaveAsReplace, ButtonSize.Small, IconNames.SaveAsReplace, AssemblyEnvironments),
            // Spec 07: parts and assemblies only, not drawings and not with no document open.
            new(CommandNames.PartProperties, ButtonSize.Small, IconNames.PartProperties, ModelEnvironments),
            // ZeroDoc gets no Settings or About button until spec 01 open question 4 is answered.
            new(CommandNames.Settings, ButtonSize.Small, IconNames.Settings, DocumentEnvironments),
            new(CommandNames.About, ButtonSize.Small, IconNames.About, DocumentEnvironments),
        }),
        new(RibbonIds.DrawingToolsPanelId, RibbonIds.DrawingToolsPanelName, DeveloperOnly: false, new ButtonDefinition[]
        {
            // Spec 02, "Commands: Create Print": a drawing of the active part or assembly.
            new(CommandNames.CreatePrint, ButtonSize.Large, IconNames.CreatePrint, ModelEnvironments),
        }),
        new(RibbonIds.DeveloperPanelId, RibbonIds.DeveloperPanelName, DeveloperOnly: true, new ButtonDefinition[]
        {
            new(CommandNames.ExportModelData, ButtonSize.Small, IconNames.ExportModelData, DocumentEnvironments),
            new(CommandNames.ExportLibraries, ButtonSize.Small, IconNames.ExportLibraries, AllEnvironments),
        }),
    };

    /// <summary>
    /// The panels to show on <paramref name="environment"/>'s tab, in display order.
    /// Never contains a panel without buttons.
    /// </summary>
    public static IReadOnlyList<PanelLayout> For(RibbonEnvironment environment, bool showDeveloperTools)
    {
        if (!Enum.IsDefined(environment))
            throw new ArgumentOutOfRangeException(nameof(environment), environment, "Unknown ribbon environment.");

        var panels = new List<PanelLayout>();
        foreach (var panel in Definitions)
        {
            if (panel.DeveloperOnly && !showDeveloperTools)
                continue;

            var buttons = panel.Buttons
                .Where(b => b.Environments.Contains(environment))
                .Select(b => new ButtonLayout(b.CommandInternalName, b.Size, b.IconName))
                .ToArray();

            if (buttons.Length > 0)
                panels.Add(new PanelLayout(panel.Id, panel.DisplayName, buttons));
        }
        return panels;
    }

    /// <summary>
    /// Whether <paramref name="environment"/> has any panels. When false the add-in should not create the tab there.
    /// </summary>
    public static bool HasPanels(RibbonEnvironment environment, bool showDeveloperTools) =>
        For(environment, showDeveloperTools).Count > 0;
}
