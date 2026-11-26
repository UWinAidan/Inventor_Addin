using InventorAddin.Core.Drawing;

namespace InventorAddin.Core.Tests.Drawing;

public sealed class BatchPrintPlanTests
{
    private static BatchPrintComponent C(string name, params BatchPrintComponent[] kids) =>
        new(@"C:\W\" + name, false, kids);

    private static BatchPrintComponent Lib(string name) => new(@"C:\CC\" + name, true);

    private static string LastPart(string path) => path.Split('\\', '/').Last();

    // Every model has a print in Prints\ except those listed.
    private static Func<string, string?> Finder(params string[] noPrint) =>
        m => noPrint.Any(n => m.EndsWith(n, StringComparison.OrdinalIgnoreCase))
            ? null
            : PrintPlan.FindExistingPrint(m, ".idw", _ => true);

    [Fact]
    public void AssemblyFirst_ThenDepthFirstInBrowserOrder()
    {
        var tree = C("top.iam",
            C("sub.iam", C("a.ipt"), C("b.ipt")),
            C("c.ipt"));

        var plan = BatchPrintPlan.Create(tree, Finder());

        Assert.Equal(
            new[] { "top.idw", "sub.idw", "a.idw", "b.idw", "c.idw" },
            plan.Drawings.Select(d => LastPart(d.DrawingFullFileName)));
        Assert.Equal(@"C:\W\Prints\top.idw", plan.Drawings[0].DrawingFullFileName);
    }

    [Fact]
    public void EachFileOnce_FirstOccurrenceWins_CaseAndSlashInsensitive()
    {
        var tree = C("top.iam",
            C("a.ipt"),
            C("sub.iam", new BatchPrintComponent("c:/w/A.ipt"), C("b.ipt")),
            C("sub.iam", C("never.ipt")));

        var plan = BatchPrintPlan.Create(tree, Finder());

        Assert.Equal(new[] { "top.iam", "a.ipt", "sub.iam", "b.ipt" },
            plan.Drawings.Select(d => LastPart(d.ModelFullFileName)));
    }

    [Fact]
    public void LibraryParts_AreSkippedAndCounted_EachOnce()
    {
        var tree = C("top.iam", Lib("bolt.ipt"), C("a.ipt"), Lib("bolt.ipt"), Lib("nut.ipt"));

        var plan = BatchPrintPlan.Create(tree, Finder());

        Assert.Equal(2, plan.SkippedLibraryCount);
        Assert.Equal(2, plan.Drawings.Count);
        Assert.Contains("Skipped 2 library parts", plan.ReportText());
    }

    [Fact]
    public void ModelsWithNoPrint_AreListed_AndTheirChildrenStillWalked()
    {
        var tree = C("top.iam", C("sub.iam", C("a.ipt")), C("b.ipt"));

        var plan = BatchPrintPlan.Create(tree, Finder("sub.iam", "b.ipt"));

        Assert.Equal(new[] { @"C:\W\sub.iam", @"C:\W\b.ipt" }, plan.ModelsWithoutDrawing);
        Assert.Equal(2, plan.Drawings.Count);
        string report = plan.ReportText();
        Assert.Contains("2 models have no drawing", report);
        Assert.Contains("  sub.iam", report);
        Assert.DoesNotContain(@"C:\W\sub.iam", report);
    }

    [Fact]
    public void NoDrawingAnywhere_IsEmpty()
    {
        var plan = BatchPrintPlan.Create(C("top.iam", C("a.ipt")), Finder("top.iam", "a.ipt"));

        Assert.True(plan.IsEmpty);
        Assert.Contains("No drawings were found", plan.ReportText());
    }

    [Theory]
    [InlineData(@"C:\W\Machine, 100.iam", @"C:\W\Machine, 100.pdf")]
    [InlineData(@"C:\Work.v2\top.v3.iam", @"C:\Work.v2\top.v3.pdf")]
    [InlineData("/home/t/top.iam", "/home/t/top.pdf")]
    public void PdfPath_IsAssemblyNameBesideTheAssembly(string assembly, string pdf)
    {
        Assert.Equal(pdf, BatchPrintPlan.PdfFileName(assembly));
        Assert.Equal(pdf, BatchPrintPlan.Create(new BatchPrintComponent(assembly), Finder()).PdfFullFileName);
    }

    [Fact]
    public void Report_SingularWording()
    {
        var plan = BatchPrintPlan.Create(C("top.iam"), Finder());
        Assert.Equal(@"Printed 1 drawing to C:\W\top.pdf.", plan.ReportText());
    }

    [Fact]
    public void NullArguments_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => BatchPrintPlan.Create(null!, Finder()));
        Assert.Throws<ArgumentNullException>(() => BatchPrintPlan.Create(C("t.iam"), null!));
    }
}
