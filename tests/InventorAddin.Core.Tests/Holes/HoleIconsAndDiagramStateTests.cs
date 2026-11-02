using System.Xml.Linq;
using InventorAddin.Core.Holes;
using InventorAddin.Core.Settings;

namespace InventorAddin.Core.Tests.Holes;

/// <summary>Which icon each AWB Hole choice shows, and the diagram's remembered state (task 104).</summary>
public sealed class HoleIconsAndDiagramStateTests
{
    [Fact]
    public void Icons_AreAllDifferent()
    {
        Assert.Equal(HoleIcons.All.Count, HoleIcons.All.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(14, HoleIcons.All.Count);
    }

    [Theory]
    [InlineData("Clearance", HoleIcons.Clearance)]
    [InlineData("tapped", HoleIcons.Tapped)]
    [InlineData(null, HoleIcons.Clearance)]
    public void ForType(string? type, string expected) => Assert.Equal(expected, HoleIcons.ForType(type));

    [Fact]
    public void ForSeatAndTermination_EachValueHasItsOwnIcon()
    {
        Assert.Equal(HoleIcons.SeatNone, HoleIcons.ForSeat(HoleSeat.None));
        Assert.Equal(HoleIcons.Counterbore, HoleIcons.ForSeat(HoleSeat.Counterbore));
        Assert.Equal(HoleIcons.Countersink, HoleIcons.ForSeat(HoleSeat.Countersink));
        Assert.Equal(HoleIcons.Distance, HoleIcons.ForTermination(HoleTermination.Distance));
        Assert.Equal(HoleIcons.ThroughAll, HoleIcons.ForTermination(HoleTermination.ThroughAll));
        Assert.Equal(HoleIcons.ToFace, HoleIcons.ForTermination(HoleTermination.ToFace));
    }

    [Theory]
    [InlineData(null, HoleIcons.FlatBottom)]
    [InlineData(118.0, HoleIcons.PointShallow)]
    [InlineData(140.0, HoleIcons.PointShallow)]
    [InlineData(104.0, HoleIcons.PointShallow)]
    [InlineData(103.9, HoleIcons.PointSteep)]
    [InlineData(90.0, HoleIcons.PointSteep)]
    [InlineData(60.0, HoleIcons.PointSteep)]
    public void ForDrillPoint_ShallowFrom104Degrees(double? angle, string expected) =>
        Assert.Equal(expected, HoleIcons.ForDrillPoint(angle is { } a ? (decimal)a : null));

    [Fact]
    public void DiagramState_StartsExpanded()
    {
        Assert.True(new AddinSettings().HoleDiagramExpanded);
        Assert.True(HoleDiagramState.IsExpanded(new AddinSettings()));
    }

    [Fact]
    public void DiagramState_Remember_ReturnsTheChangedSettings_OrNullWhenUnchanged()
    {
        var settings = new AddinSettings { DefaultDesigner = "A. Person" };

        var collapsed = HoleDiagramState.Remember(settings, expanded: false);

        Assert.Equal(settings with { HoleDiagramExpanded = false }, collapsed);
        Assert.Null(HoleDiagramState.Remember(settings, expanded: true));
        Assert.Null(HoleDiagramState.Remember(collapsed!, expanded: false));
        Assert.Equal(settings, HoleDiagramState.Remember(collapsed!, expanded: true));
    }

    // ===== Every icon is drawn (task 119): the drawings are Geometry resources in the add-in's Styles.xaml =====

    private const string KeyPrefix = "HoleIcon.";
    private static readonly string[] Parts = ["Material", "Line", "Thick"];

    [Fact]
    public void EveryIcon_HasItsMaterialDrawingInStylesXaml()
    {
        var keys = StylesHoleIconGeometry();

        var missing = HoleIcons.All.Where(name => !keys.ContainsKey(KeyPrefix + name + ".Material")).ToList();

        Assert.True(missing.Count == 0, "No HoleIcon.{name}.Material key in UI/Theme/Styles.xaml for: " + string.Join(", ", missing));
    }

    [Fact]
    public void EveryHoleIconKeyInStylesXaml_IsAKnownIconAndPart()
    {
        var unknown = StylesHoleIconGeometry().Keys
            .Where(key => !HoleIcons.All.Any(name => Parts.Any(part => key == KeyPrefix + name + "." + part)))
            .ToList();

        Assert.True(unknown.Count == 0, "Styles.xaml has HoleIcon keys for no HoleIcons name or part: " + string.Join(", ", unknown));
    }

    [Fact]
    public void NoTwoIcons_HaveTheSameDrawing()
    {
        var keys = StylesHoleIconGeometry();
        string Drawing(string name) =>
            string.Join("|", Parts.Select(part => keys.GetValueOrDefault(KeyPrefix + name + "." + part, "").Trim()));

        var alike = HoleIcons.All.GroupBy(Drawing).Where(g => g.Count() > 1).Select(g => string.Join(" = ", g)).ToList();

        Assert.True(alike.Count == 0, "Icons drawn the same: " + string.Join("; ", alike));
    }

    /// <summary>The <c>HoleIcon.*</c> Geometry resources in the add-in's Styles.xaml, read as plain XML, by key.</summary>
    private static Dictionary<string, string> StylesHoleIconGeometry()
    {
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
        string path = Path.Combine(RepoRoot(), "src", "InventorAddin", "UI", "Theme", "Styles.xaml");
        return XDocument.Load(path).Root!.Elements()
            .Where(e => e.Name.LocalName == "Geometry")
            .Select(e => (Key: (string?)e.Attribute(xaml + "Key"), Data: e.Value))
            .Where(e => e.Key != null && e.Key.StartsWith(KeyPrefix, StringComparison.Ordinal))
            .ToDictionary(e => e.Key!, e => e.Data, StringComparer.Ordinal);
    }

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
