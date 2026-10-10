using InventorAddin.Core.Drawing;
using InventorAddin.Core.Settings;

namespace InventorAddin.Core.Tests.Drawing;

public sealed class PrintPlanTests
{
    // An ANSI D sheet, as spike 032 found in the default metric drawing template.
    private const double SheetWidthCm = 86.36;
    private const double SheetHeightCm = 55.88;

    // DrawingFileName

    [Theory]
    [InlineData(@"C:\Work\AWB-10001,test_plate.ipt", ".dwg", @"C:\Work\AWB-10001,test_plate.dwg")]
    [InlineData(@"C:\Work\AWB-10001,test_plate.ipt", ".idw", @"C:\Work\AWB-10001,test_plate.idw")]
    [InlineData(@"C:\Work\AWB-10002,frame.iam", ".dwg", @"C:\Work\AWB-10002,frame.dwg")]
    [InlineData(@"C:\Work\AWB-10001,test_plate.ipt", "dwg", @"C:\Work\AWB-10001,test_plate.dwg")]
    [InlineData(@"C:\Work.v2\bracket.v3.ipt", ".dwg", @"C:\Work.v2\bracket.v3.dwg")]
    [InlineData(@"C:\Work.v2\bracket", ".dwg", @"C:\Work.v2\bracket.dwg")]
    [InlineData(@"\\nas\share\AWB-10003,plate, 10mm.ipt", ".dwg", @"\\nas\share\AWB-10003,plate, 10mm.dwg")]
    [InlineData("/home/test/AWB-10004,shaft.ipt", ".dwg", "/home/test/AWB-10004,shaft.dwg")]
    public void DrawingFileName_ReplacesTheModelExtensionWithTheDrawingExtension(string model, string extension, string expected)
    {
        Assert.Equal(expected, PrintPlan.DrawingFileName(model, extension));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(@"C:\Work\")]
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
    public void ExistingPrintCandidates_PreferredExtensionFirstThenTheOther()
    {
        Assert.Equal(
            new[] { @"C:\Work\AWB-10001,a.dwg", @"C:\Work\AWB-10001,a.idw" },
            PrintPlan.ExistingPrintCandidates(@"C:\Work\AWB-10001,a.ipt", ".dwg"));
        Assert.Equal(
            new[] { @"C:\Work\AWB-10001,a.idw", @"C:\Work\AWB-10001,a.dwg" },
            PrintPlan.ExistingPrintCandidates(@"C:\Work\AWB-10001,a.ipt", ".idw"));

        // The preferred extension is kept as given, and the other one is still found ignoring case.
        Assert.Equal(
            new[] { @"C:\Work\AWB-10001,a.IDW", @"C:\Work\AWB-10001,a.dwg" },
            PrintPlan.ExistingPrintCandidates(@"C:\Work\AWB-10001,a.ipt", "IDW"));
    }

    [Fact]
    public void FindExistingPrint_NoneExists_ReturnsNull()
    {
        Assert.Null(PrintPlan.FindExistingPrint(@"C:\Work\a.ipt", ".dwg", _ => false));
    }

    [Fact]
    public void FindExistingPrint_OnlyTheOtherExtensionExists_ReturnsIt()
    {
        var onDisk = new HashSet<string> { @"C:\Work\a.idw" };

        Assert.Equal(@"C:\Work\a.idw", PrintPlan.FindExistingPrint(@"C:\Work\a.ipt", ".dwg", onDisk.Contains));
    }

    [Fact]
    public void FindExistingPrint_BothExist_ReturnsThePreferredOne()
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
