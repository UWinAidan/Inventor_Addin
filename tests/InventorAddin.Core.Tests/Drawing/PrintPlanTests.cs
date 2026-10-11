using InventorAddin.Core.Drawing;
using InventorAddin.Core.Settings;

namespace InventorAddin.Core.Tests.Drawing;

public sealed class PrintPlanTests
{
    // An ANSI D sheet, as spike 032 found in the default metric drawing template.
    private const double SheetWidthCm = 86.36;
    private const double SheetHeightCm = 55.88;

    // PrintsFolder

    [Fact]
    public void PrintsFolderName_IsPrints()
    {
        Assert.Equal("Prints", PrintPlan.PrintsFolderName);
    }

    [Theory]
    [InlineData(@"C:\CAD\BRACKET,AWB-10005.ipt", @"C:\CAD\Prints")]
    [InlineData(@"C:\Work.v2\bracket.v3.ipt", @"C:\Work.v2\Prints")]
    [InlineData(@"\\nas\share\plate, 10mm,AWB-10003.ipt", @"\\nas\share\Prints")]
    [InlineData("/home/test/shaft,AWB-10004.ipt", "/home/test/Prints")]
    [InlineData(@"C:\Mixed/sub\frame.iam", @"C:\Mixed/sub\Prints")]
    [InlineData(@"C:/Mixed\sub/frame.iam", @"C:/Mixed\sub/Prints")]
    [InlineData(@"  C:\Work\a.ipt  ", @"C:\Work\Prints")]
    [InlineData("a.ipt", "Prints")]
    public void PrintsFolder_IsThePrintsSubfolderOfTheModelsFolder(string model, string expected)
    {
        Assert.Equal(expected, PrintPlan.PrintsFolder(model));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(@"C:\Work\")]
    public void PrintsFolder_RejectsAModelWithNoFileName(string model)
    {
        Assert.Throws<ArgumentException>(() => PrintPlan.PrintsFolder(model));
    }

    // DrawingFileName

    [Theory]
    [InlineData(@"C:\CAD\BRACKET,AWB-10005.ipt", ".dwg", @"C:\CAD\Prints\BRACKET,AWB-10005.dwg")]
    [InlineData(@"C:\Work\test_plate,AWB-10001.ipt", ".idw", @"C:\Work\Prints\test_plate,AWB-10001.idw")]
    [InlineData(@"C:\Work\frame,AWB-10002.iam", ".dwg", @"C:\Work\Prints\frame,AWB-10002.dwg")]
    [InlineData(@"C:\Work\test_plate,AWB-10001.ipt", "dwg", @"C:\Work\Prints\test_plate,AWB-10001.dwg")]
    [InlineData(@"C:\Work.v2\bracket.v3.ipt", ".dwg", @"C:\Work.v2\Prints\bracket.v3.dwg")]
    [InlineData(@"C:\Work.v2\bracket", ".dwg", @"C:\Work.v2\Prints\bracket.dwg")]
    [InlineData(@"\\nas\share\plate, 10mm,AWB-10003.ipt", ".dwg", @"\\nas\share\Prints\plate, 10mm,AWB-10003.dwg")]
    [InlineData("/home/test/shaft,AWB-10004.ipt", ".dwg", "/home/test/Prints/shaft,AWB-10004.dwg")]
    [InlineData("a.ipt", ".dwg", @"Prints\a.dwg")]
    public void DrawingFileName_IsTheModelNameWithTheDrawingExtensionInPrints(string model, string extension, string expected)
    {
        Assert.Equal(expected, PrintPlan.DrawingFileName(model, extension));
    }

    [Fact]
    public void DrawingFileName_IsInPrintsFolder()
    {
        const string model = @"C:\CAD\BRACKET,AWB-10005.ipt";

        Assert.Equal(PrintPlan.PrintsFolder(model) + @"\BRACKET,AWB-10005.dwg", PrintPlan.DrawingFileName(model, ".dwg"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(@"C:\Work\")]
    [InlineData("/home/test/")]
    public void DrawingFileName_RejectsAModelWithNoFileName(string model)
    {
        Assert.Throws<ArgumentException>(() => PrintPlan.DrawingFileName(model, ".dwg"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void DrawingFileName_RejectsABlankExtension(string extension)
    {
        Assert.Throws<ArgumentException>(() => PrintPlan.DrawingFileName(@"C:\Work\a.ipt", extension));
    }

    // Existing prints

    [Fact]
    public void ExistingPrintCandidates_PrintsFolderFirstThenBesideTheModel_PreferredExtensionFirstInEach()
    {
        Assert.Equal(
            new[]
            {
                @"C:\Work\Prints\a,AWB-10001.dwg", @"C:\Work\Prints\a,AWB-10001.idw",
                @"C:\Work\a,AWB-10001.dwg", @"C:\Work\a,AWB-10001.idw",
            },
            PrintPlan.ExistingPrintCandidates(@"C:\Work\a,AWB-10001.ipt", ".dwg"));
        Assert.Equal(
            new[]
            {
                @"C:\Work\Prints\a,AWB-10001.idw", @"C:\Work\Prints\a,AWB-10001.dwg",
                @"C:\Work\a,AWB-10001.idw", @"C:\Work\a,AWB-10001.dwg",
            },
            PrintPlan.ExistingPrintCandidates(@"C:\Work\a,AWB-10001.ipt", ".idw"));

        // The preferred extension is kept as given, and the other one is still found ignoring case.
        Assert.Equal(
            new[]
            {
                @"C:\Work\Prints\a,AWB-10001.IDW", @"C:\Work\Prints\a,AWB-10001.dwg",
                @"C:\Work\a,AWB-10001.IDW", @"C:\Work\a,AWB-10001.dwg",
            },
            PrintPlan.ExistingPrintCandidates(@"C:\Work\a,AWB-10001.ipt", "IDW"));
    }

    [Fact]
    public void ExistingPrintCandidates_ForwardSlashes()
    {
        Assert.Equal(
            new[] { "/home/test/Prints/a.dwg", "/home/test/Prints/a.idw", "/home/test/a.dwg", "/home/test/a.idw" },
            PrintPlan.ExistingPrintCandidates("/home/test/a.ipt", ".dwg"));
    }

    [Fact]
    public void ExistingPrintCandidates_NetworkShare()
    {
        Assert.Equal(
            new[]
            {
                @"\\nas\share\Prints\plate, 10mm,AWB-10003.idw", @"\\nas\share\Prints\plate, 10mm,AWB-10003.dwg",
                @"\\nas\share\plate, 10mm,AWB-10003.idw", @"\\nas\share\plate, 10mm,AWB-10003.dwg",
            },
            PrintPlan.ExistingPrintCandidates(@"\\nas\share\plate, 10mm,AWB-10003.ipt", ".idw"));
    }

    [Fact]
    public void ExistingPrintCandidates_FirstIsTheFileCreatePrintSaves()
    {
        const string model = @"C:\Work\a,AWB-10001.ipt";

        Assert.Equal(PrintPlan.DrawingFileName(model, ".idw"), PrintPlan.ExistingPrintCandidates(model, ".idw")[0]);
    }

    [Theory]
    [InlineData(@"C:\Work\Prints\a,AWB-10001.dwg")]
    [InlineData(@"C:\Work\Prints\a,AWB-10001.idw")]
    [InlineData(@"c:\work\PRINTS\A,awb-10001.DWG")]
    [InlineData(@"C:\Work\a,AWB-10001.idw")]
    [InlineData(@"C:/Work/Prints/a,AWB-10001.dwg")]
    [InlineData(@" C:\Work\a,AWB-10001.dwg ")]
    public void IsPrintOf_ACandidateIgnoringCaseAndSlashes_IsAPrint(string drawing)
    {
        Assert.True(PrintPlan.IsPrintOf(@"C:\Work\a,AWB-10001.ipt", drawing));
    }

    [Theory]
    [InlineData(@"C:\Work\Other\a,AWB-10001.dwg")]
    [InlineData(@"C:\Work\Prints\Old\a,AWB-10001.dwg")]
    [InlineData(@"C:\Work\OldVersions\a,AWB-10001.dwg")]
    [InlineData(@"C:\Work\Prints\b,AWB-10001.dwg")]
    [InlineData(@"C:\Work\a,AWB-10001.pdf")]
    [InlineData(@"C:\Work\a,AWB-10001.iam")]
    [InlineData("")]
    [InlineData(null)]
    public void IsPrintOf_AnythingElse_IsNot(string? drawing)
    {
        Assert.False(PrintPlan.IsPrintOf(@"C:\Work\a,AWB-10001.ipt", drawing));
    }

    [Fact]
    public void IsPrintOf_EveryCandidateCreatePrintLooksFor_IsAPrint()
    {
        const string model = @"\\nas\share\plate, 10mm,AWB-10003.ipt";
        Assert.All(PrintPlan.ExistingPrintCandidates(model, ".idw"), c => Assert.True(PrintPlan.IsPrintOf(model, c)));
    }

    [Fact]
    public void FindExistingPrint_NoneExists_ReturnsNull()
    {
        Assert.Null(PrintPlan.FindExistingPrint(@"C:\Work\a.ipt", ".dwg", _ => false));
    }

    [Fact]
    public void FindExistingPrint_OnlyTheOtherExtensionInPrintsExists_ReturnsIt()
    {
        var onDisk = new HashSet<string> { @"C:\Work\Prints\a.idw" };

        Assert.Equal(@"C:\Work\Prints\a.idw", PrintPlan.FindExistingPrint(@"C:\Work\a.ipt", ".dwg", onDisk.Contains));
    }

    [Fact]
    public void FindExistingPrint_BothExistInPrints_ReturnsThePreferredOne()
    {
        var onDisk = new HashSet<string> { @"C:\Work\Prints\a.idw", @"C:\Work\Prints\a.dwg" };

        Assert.Equal(@"C:\Work\Prints\a.dwg", PrintPlan.FindExistingPrint(@"C:\Work\a.ipt", ".dwg", onDisk.Contains));
    }

    [Fact]
    public void FindExistingPrint_OnlyAnOldPrintBesideTheModel_ReturnsIt()
    {
        var onDisk = new HashSet<string> { @"C:\Work\a.idw" };

        Assert.Equal(@"C:\Work\a.idw", PrintPlan.FindExistingPrint(@"C:\Work\a.ipt", ".dwg", onDisk.Contains));
    }

    [Fact]
    public void FindExistingPrint_PrintsFolderWinsOverBesideTheModel()
    {
        // Even the other extension in Prints comes before the preferred one beside the model.
        var onDisk = new HashSet<string> { @"C:\Work\a.dwg", @"C:\Work\Prints\a.idw" };

        Assert.Equal(@"C:\Work\Prints\a.idw", PrintPlan.FindExistingPrint(@"C:\Work\a.ipt", ".dwg", onDisk.Contains));
    }

    [Fact]
    public void FindExistingPrint_BothExistBesideTheModel_ReturnsThePreferredOne()
    {
        var onDisk = new HashSet<string> { @"C:\Work\a.idw", @"C:\Work\a.dwg" };

        Assert.Equal(@"C:\Work\a.dwg", PrintPlan.FindExistingPrint(@"C:\Work\a.ipt", ".dwg", onDisk.Contains));
    }

    // StandardScales

    [Fact]
    public void StandardScales_TenToOneDownToOneToFifty_LargestFirst()
    {
        Assert.Equal(
            new[] { 10.0, 5.0, 4.0, 2.0, 1.0, 0.5, 0.25, 0.2, 0.1, 0.05, 0.02 },
            PrintPlan.StandardScales);
    }

    // ChooseScale

    [Fact]
    public void ChooseScale_TinyPart_LargestScale()
    {
        // 1 x 0.5 cm: at 10:1 it is 10 x 5 cm on the sheet.
        Assert.Equal(10.0, PrintPlan.ChooseScale(1.0, 0.5, SheetWidthCm, SheetHeightCm));
    }

    [Fact]
    public void ChooseScale_SmallPart_LargestScaleThatFits()
    {
        // 12 x 5 cm: 5:1 gives 60 cm wide, over 60% of 86.36 (51.8); 4:1 gives 48 cm.
        Assert.Equal(4.0, PrintPlan.ChooseScale(12.0, 5.0, SheetWidthCm, SheetHeightCm));
    }

    [Fact]
    public void ChooseScale_LargeAssembly_ReducingScale()
    {
        // 300 x 200 cm: 1:5 gives 60 x 40 cm, too big; 1:10 gives 30 x 20 cm.
        Assert.Equal(0.1, PrintPlan.ChooseScale(300.0, 200.0, SheetWidthCm, SheetHeightCm));
    }

    [Fact]
    public void ChooseScale_TooLargeForEveryScale_SmallestScale()
    {
        // 5000 cm long: even 1:50 gives 100 cm.
        Assert.Equal(0.02, PrintPlan.ChooseScale(5000.0, 100.0, SheetWidthCm, SheetHeightCm));
    }

    [Fact]
    public void ChooseScale_HeightDecides_WhenTheViewIsTall()
    {
        // Sheet 100 x 50: limits 60 x 30. A 10 x 30 view fits at 1:1 but not at 2:1 (60 cm tall).
        Assert.Equal(1.0, PrintPlan.ChooseScale(10.0, 30.0, 100.0, 50.0));
    }

    [Theory]
    [InlineData(60.0, 60.0, 1.0)]
    [InlineData(30.0, 30.0, 2.0)]
    [InlineData(12.0, 12.0, 5.0)]
    [InlineData(120.0, 120.0, 0.5)]
    public void ChooseScale_AViewThatFitsExactly_IsAccepted(double viewCm, double viewHeightCm, double expected)
    {
        // Sheet 100 x 100: the limit is 60 cm each way.
        Assert.Equal(expected, PrintPlan.ChooseScale(viewCm, viewHeightCm, 100.0, 100.0));
    }

    [Fact]
    public void ChooseScale_JustOverTheLimit_TakesTheNextScaleDown()
    {
        Assert.Equal(0.5, PrintPlan.ChooseScale(60.01, 10.0, 100.0, 100.0));
    }

    [Theory]
    [InlineData(0.0, 1.0, 10.0, 10.0)]
    [InlineData(1.0, 0.0, 10.0, 10.0)]
    [InlineData(1.0, 1.0, 0.0, 10.0)]
    [InlineData(1.0, 1.0, 10.0, 0.0)]
    [InlineData(-1.0, 1.0, 10.0, 10.0)]
    [InlineData(double.NaN, 1.0, 10.0, 10.0)]
    [InlineData(double.PositiveInfinity, 1.0, 10.0, 10.0)]
    public void ChooseScale_RejectsSizesThatAreNotPositive(double viewW, double viewH, double sheetW, double sheetH)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PrintPlan.ChooseScale(viewW, viewH, sheetW, sheetH));
    }

    // Messages

    [Fact]
    public void Messages()
    {
        Assert.Equal("Save the part before creating its print.", PrintPlan.UnsavedMessage);
        Assert.Equal("A print for this part already exists. Open it?", PrintPlan.AlreadyExistsQuestion);
        Assert.Equal(
            "Inventor has no default drawing template for metric units. No print was created.",
            PrintPlan.MissingTemplateMessage(UnitsSystem.Metric));
        Assert.Equal(
            "Inventor has no default drawing template for imperial units. No print was created.",
            PrintPlan.MissingTemplateMessage(UnitsSystem.Imperial));
    }

    [Theory]
    [InlineData(PrintCleanup.NoDrawing, "No drawing was made.")]
    [InlineData(PrintCleanup.Closed, "The drawing was closed without saving.")]
    [InlineData(PrintCleanup.CloseFailed, "The half-made drawing could not be closed. Close it without saving.")]
    public void FailedMessage_NamesTheStepTheReasonAndWhatBecameOfTheDrawing(PrintCleanup cleanup, string ending)
    {
        string text = PrintPlan.FailedMessage("adding the front view", "The model could not be found.", cleanup);

        Assert.Equal(
            $"Creating the print failed while adding the front view: The model could not be found. {ending}",
            text);
    }

    [Fact]
    public void FailedMessage_OnlyClosedSaysClosedWithoutSaving()
    {
        Assert.DoesNotContain("closed without saving", PrintPlan.FailedMessage("a", "b.", PrintCleanup.NoDrawing));
        Assert.DoesNotContain("closed without saving", PrintPlan.FailedMessage("a", "b.", PrintCleanup.CloseFailed));
    }

    [Fact]
    public void FailedMessage_RejectsAnUnknownCleanup()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PrintPlan.FailedMessage("a", "b.", (PrintCleanup)99));
    }
}
