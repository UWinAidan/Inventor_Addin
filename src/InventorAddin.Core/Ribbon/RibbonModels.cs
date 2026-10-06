namespace InventorAddin.Core.Ribbon;

/// <summary>
/// The Inventor ribbons the add-in's tab can appear on.
/// Each member is spelled exactly like the Inventor ribbon name; use <see cref="RibbonEnvironments.RibbonName"/>.
/// </summary>
public enum RibbonEnvironment
{
    /// <summary>Inventor with no document open.</summary>
    ZeroDoc,
    Part,
    Assembly,
    Drawing,
}

public static class RibbonEnvironments
{
    /// <summary>Every environment, in the order the add-in builds them.</summary>
    public static IReadOnlyList<RibbonEnvironment> All { get; } = new[]
    {
        RibbonEnvironment.ZeroDoc,
        RibbonEnvironment.Part,
        RibbonEnvironment.Assembly,
        RibbonEnvironment.Drawing,
    };

    /// <summary>The Inventor ribbon name for the environment (the key into <c>UserInterfaceManager.Ribbons</c>).</summary>
    public static string RibbonName(this RibbonEnvironment environment) => environment switch
    {
        RibbonEnvironment.ZeroDoc => "ZeroDoc",
        RibbonEnvironment.Part => "Part",
        RibbonEnvironment.Assembly => "Assembly",
        RibbonEnvironment.Drawing => "Drawing",
        _ => throw new ArgumentOutOfRangeException(nameof(environment), environment, "Unknown ribbon environment."),
    };
}

/// <summary>Size of a button on a ribbon panel.</summary>
public enum ButtonSize
{
    Small,
    Large,
}

/// <summary>A button on a panel: the internal name of the command it runs, and its size.</summary>
public sealed record ButtonLayout(string CommandInternalName, ButtonSize Size);

/// <summary>
/// A ribbon panel: internal id, display name and its buttons in display order.
/// Named <c>PanelLayout</c> rather than <c>RibbonPanel</c> to avoid a clash with <c>Inventor.RibbonPanel</c> in the add-in.
/// </summary>
public sealed record PanelLayout(string Id, string DisplayName, IReadOnlyList<ButtonLayout> Buttons);

/// <summary>Ids and display names for the add-in's tab and panels.</summary>
public static class RibbonIds
{
    public const string TabId = "id_Tab_WorkflowTools";
    public const string TabName = "Workflow Tools";

    public const string CadAutomationPanelId = "id_Panel_WorkflowTools_CadAutomation";
    public const string CadAutomationPanelName = "CAD Automation";

    public const string DrawingToolsPanelId = "id_Panel_WorkflowTools_DrawingTools";
    public const string DrawingToolsPanelName = "Drawing Tools";

    public const string DeveloperPanelId = "id_Panel_WorkflowTools_Dev";
    public const string DeveloperPanelName = "Developer";
}

/// <summary>
/// Internal names of the add-in's commands (the <c>ButtonDefinition</c> internal names).
/// The add-in commands use these same constants so the ribbon layout and the commands cannot drift apart.
/// </summary>
public static class CommandNames
{
    public const string ExportModelData = "WorkflowTools_ExportModelData";
    public const string ExportLibraries = "WorkflowTools_ExportLibraries";
}
