using InventorAddin.Core.Drawing;
using InventorAddin.Core.Settings;

namespace InventorAddin.Core.Tests.Drawing;

public sealed class PrintTemplatePublishTests
{
    private static readonly string AddinDir = Path.Combine(Path.GetTempPath(), "awbtest", "addin");
    private static readonly string Starter = Path.Combine(AddinDir, "libraries", "Templates");

    [Fact]
    public void CopyTarget_DifferentFolder_IsTheStarterTemplatesFolder()
    {
        var chosen = Path.Combine(Path.GetTempPath(), "awbtest", "repo", "libraries", "Templates");
        Assert.Equal(Starter, PrintTemplatePublish.CopyTarget(chosen, AddinDir));
    }

    [Fact]
    public void CopyTarget_ChosenIsStarterFolder_IsNull()
    {
        Assert.Null(PrintTemplatePublish.CopyTarget(Starter, AddinDir));
    }

    [Fact]
    public void CopyTarget_SameFolderWithTrailingSeparatorAndOtherCase_IsNull()
    {
        var chosen = Starter.ToUpperInvariant() + Path.DirectorySeparatorChar;
        if (OperatingSystem.IsWindows())
            Assert.Null(PrintTemplatePublish.CopyTarget(chosen, AddinDir));
        else
            Assert.Null(PrintTemplatePublish.CopyTarget(Starter + Path.DirectorySeparatorChar, AddinDir));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void CopyTarget_BlankFolders_Throw(string blank)
    {
        Assert.Throws<ArgumentException>(() => PrintTemplatePublish.CopyTarget(blank, AddinDir));
        Assert.Throws<ArgumentException>(() => PrintTemplatePublish.CopyTarget(Starter, blank));
    }

    [Fact]
    public void SettingsOverrides_BlankPaths_AreNone()
    {
        Assert.Empty(PrintTemplatePublish.SettingsOverrides(new AddinSettings()));
    }

    [Fact]
    public void SettingsOverrides_ListsTrimmedNonBlankPaths()
    {
        var settings = new AddinSettings { PrintTemplateMetricPath = "  C:\\x\\m.idw ", PrintTemplateImperialPath = " " };
        Assert.Equal(new[] { "C:\\x\\m.idw" }, PrintTemplatePublish.SettingsOverrides(settings));
    }

    [Fact]
    public void Report_Copied_NamesBothFolders()
    {
        var lines = PrintTemplatePublish.Report("C:\\repo\\T", Starter, Array.Empty<string>(), new AddinSettings());
        var line = Assert.Single(lines);
        Assert.Contains("C:\\repo\\T", line);
        Assert.Contains(Starter, line);
        Assert.Contains("at once", line);
    }

    [Fact]
    public void Report_NoCopyNeeded_SaysSo()
    {
        var lines = PrintTemplatePublish.Report(Starter, null, Array.Empty<string>(), new AddinSettings());
        Assert.Contains("no copy was needed", Assert.Single(lines));
    }

    [Fact]
    public void Report_CopyFailed_IsAWarningNamingTheFile()
    {
        var lines = PrintTemplatePublish.Report("C:\\repo\\T", Starter, new[] { "AWB Metric.idw: access denied" }, new AddinSettings());
        Assert.Equal(2, lines.Count);
        Assert.Contains("did not finish", lines[0]);
        Assert.Contains("AWB Metric.idw: access denied", lines[1]);
    }

    [Fact]
    public void Report_SettingsPathSet_SaysCreatePrintUsesIt()
    {
        var settings = new AddinSettings { PrintTemplateImperialPath = "D:\\mine\\imp.idw" };
        var lines = PrintTemplatePublish.Report("C:\\repo\\T", Starter, Array.Empty<string>(), settings);
        Assert.Equal(2, lines.Count);
        Assert.Contains("D:\\mine\\imp.idw", lines[1]);
        Assert.Contains("not the copy beside the add-in", lines[1]);
    }
}
