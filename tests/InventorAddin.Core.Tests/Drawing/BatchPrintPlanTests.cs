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

    [Fact]
    public void TwoModelsWithTheSameDrawing_PrintItOnce()
    {
        var tree = C("top.iam", C("a.ipt"), C("b.ipt"));
        Func<string, string?> shared = m => m.EndsWith("top.iam") ? @"C:\W\Prints\top.idw" : @"C:\W\Prints\shared.idw";

        var plan = BatchPrintPlan.Create(tree, shared);

        Assert.Equal(new[] { "top.idw", "shared.idw" }, plan.Drawings.Select(d => LastPart(d.DrawingFullFileName)));
        Assert.Empty(plan.ModelsWithoutDrawing);
        Assert.Contains("Printed 2 drawings", plan.ReportText());
    }

    [Fact]
    public void ALibrarySubAssembly_IsSkippedWithItsChildren()
    {
        var lib = new BatchPrintComponent(@"C:\CC\kit.iam", true, new[] { C("inner.ipt") });
        var plan = BatchPrintPlan.Create(C("top.iam", lib, C("a.ipt")), Finder());

        Assert.Equal(new[] { "top.iam", "a.ipt" }, plan.Drawings.Select(d => LastPart(d.ModelFullFileName)));
        Assert.Equal(1, plan.SkippedLibraryCount);
        Assert.Empty(plan.ModelsWithoutDrawing);
    }

    [Fact]
    public void Report_SingularLibraryAndMissingWording()
    {
        var plan = BatchPrintPlan.Create(C("top.iam", Lib("bolt.ipt"), C("a.ipt")), Finder("a.ipt"));
        string report = plan.ReportText();

        Assert.Contains("Skipped 1 library part (", report);
        Assert.Contains("1 model has no drawing:", report);
        Assert.DoesNotContain("library parts", report);
    }

    private static BatchPrintPlan ThreeDrawings() =>
        BatchPrintPlan.Create(C("top.iam", C("a.ipt"), C("b.ipt")), Finder());

    [Fact]
    public void Report_SomeFailed_CountsWhatWasPrinted_AndNamesTheFailures()
    {
        var failures = new[] { new BatchPrintFailure(@"C:\W\Prints\a.idw", "could not be opened") };

        string report = ThreeDrawings().ReportText(failures);

        Assert.Contains(@"Printed 2 of 3 drawings to C:\W\top.pdf.", report);
        Assert.Contains("1 drawing could not be printed:", report);
        Assert.Contains("  a.idw: could not be opened", report);
    }

    [Fact]
    public void Report_FailuresAreMatchedLoosely_CountOnce_AndUnplannedOnesAreIgnored()
    {
        var failures = new[]
        {
            new BatchPrintFailure("c:/w/prints/A.idw", "x"),
            new BatchPrintFailure(@"C:\W\Prints\a.idw", "y"),
            new BatchPrintFailure(@"C:\W\Prints\other.idw", "z"),
        };

        string report = ThreeDrawings().ReportText(failures);

        Assert.Contains("Printed 2 of 3 drawings", report);
        Assert.DoesNotContain("other.idw", report);
    }

    [Fact]
    public void Report_AllFailed_SaysNoPdfWasMade()
    {
        var failures = new[]
        {
            new BatchPrintFailure(@"C:\W\Prints\top.idw", "x"), new BatchPrintFailure(@"C:\W\Prints\a.idw", "x"),
            new BatchPrintFailure(@"C:\W\Prints\b.idw", "x"),
        };

        string report = ThreeDrawings().ReportText(failures);

        Assert.Contains("No PDF was made: none of the 3 drawings could be printed.", report);
        Assert.DoesNotContain("Printed", report);
    }

    [Fact]
    public void Report_OnlyDrawingFailed_UsesSingularWording()
    {
        var plan = BatchPrintPlan.Create(C("top.iam"), Finder());

        string report = plan.ReportText(new[] { new BatchPrintFailure(@"C:\W\Prints\top.idw", "x") });

        Assert.Contains("No PDF was made: the drawing could not be printed.", report);
    }

    [Fact]
    public void ReplaceQuestion_NamesTheFile()
    {
        Assert.Equal("top.pdf already exists in this folder. Replace it?", BatchPrintPlan.ReplaceQuestion(@"C:\W\top.pdf"));
    }

    [Theory]
    [InlineData(@"C:\Lib\Std\bolt.ipt", @"C:\Lib", true)]
    [InlineData("c:/lib/std/bolt.ipt", @"C:\LIB\", true)]
    [InlineData(@"C:\Library2\bolt.ipt", @"C:\Lib", false)]
    [InlineData(@"C:\Lib", @"C:\Lib", false)]
    [InlineData(@"C:\W\top.iam", "", false)]
    public void IsUnderAnyFolder_MatchesWholeFolders(string path, string folder, bool expected)
    {
        Assert.Equal(expected, BatchPrintPlan.IsUnderAnyFolder(path, new[] { folder }));
    }

    [Theory]
    [InlineData(@"C:\Proj\a.ipj", @"Libs\Std", @"C:\Proj\Libs\Std")]
    [InlineData(@"C:\Proj\a.ipj", @"..\Shared\Lib", @"C:\Shared\Lib")]
    [InlineData(@"C:\Proj\a.ipj", @".\Lib", @"C:\Proj\Lib")]
    [InlineData(@"C:\Proj\a.ipj", @"D:\Lib", @"D:\Lib")]
    [InlineData(@"C:\Proj\a.ipj", @"\srv\share\Lib", @"\srv\share\Lib")]
    [InlineData(@"C:\Proj\a.ipj", "Libs/Std", @"C:\Proj\Libs\Std")]
    [InlineData(@"C:\Proj\a.ipj", "", "")]
    public void ResolveProjectFolder_TakesRelativePathsFromTheProjectFolder(string project, string folder, string expected)
    {
        Assert.Equal(expected, BatchPrintPlan.ResolveProjectFolder(project, folder));
    }
}
