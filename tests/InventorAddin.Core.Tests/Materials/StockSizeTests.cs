using InventorAddin.Core.Drawing;
using InventorAddin.Core.Materials;
using InventorAddin.Core.PartProperties;
using InventorAddin.Core.Settings;
using InventorAddin.Core.Tests.Holes;
using InventorAddin.Core.ViewModels;

namespace InventorAddin.Core.Tests.Materials;

public sealed class StockSizeTests
{
    // Invented lists: metric thickness 6, 8, 10, 12, 16; allowance 1 mm per side, step 5 mm.
    private static StockWorkbook Workbook() => new(
        new StockUnitData([6, 8, 10, 12, 16], [10, 20, 30, 40], 1m, 5m),
        new StockUnitData([0.25m, 0.5m, 0.75m], [0.5m, 1m], 0.04m, 0.25m));

    private static double[] Mm(double a, double b, double c) => [a / 10, b / 10, c / 10];

    private static double[] In(double a, double b, double c) => [a * 2.54, b * 2.54, c * 2.54];

    // ---- Plate ----

    [Fact]
    public void Plate_AcceptanceExample_10_2_x_61_x_123_Gives_PL_16_x_65_x_125()
    {
        var result = StockSize.Plate(Mm(10.2, 61, 123), PrintUnits.Metric, Workbook());

        Assert.True(result.Succeeded);
        Assert.Equal("PL 16 x 65 x 125", result.Text);
        Assert.Equal(16m, result.Primary);
        Assert.Equal(65m, result.Width);
        Assert.Equal(125m, result.Length);
    }

    [Fact]
    public void Plate_BoxInAnyOrder_GivesTheSameText()
    {
        var a = StockSize.Plate(Mm(123, 10.2, 61), PrintUnits.Metric, Workbook());
        var b = StockSize.Plate(Mm(61, 123, 10.2), PrintUnits.Metric, Workbook());

        Assert.Equal("PL 16 x 65 x 125", a.Text);
        Assert.Equal(a.Text, b.Text);
    }

    [Fact]
    public void Plate_ExactlyOnAListedSizeAndStep_IsNotRoundedUpAnother()
    {
        // 10 + 2 = 12 (listed); 63 + 2 = 65 (a step); 123 + 2 = 125 (a step).
        var result = StockSize.Plate(Mm(10, 63, 123), PrintUnits.Metric, Workbook());

        Assert.Equal("PL 12 x 65 x 125", result.Text);
    }

    [Fact]
    public void Plate_JustOverAListedSizeAndStep_GoesToTheNextOne()
    {
        // 10.01 + 2 = 12.01 -> 16 (not 12); 63.01 + 2 = 65.01 -> 70; 123.01 + 2 = 125.01 -> 130.
        var result = StockSize.Plate(Mm(10.01, 63.01, 123.01), PrintUnits.Metric, Workbook());

        Assert.Equal("PL 16 x 70 x 130", result.Text);
    }

    [Fact]
    public void Round_JustOverAListedDiameter_GoesToTheNextOne()
    {
        // 18.01 + 2 = 20.01 -> 30 (not 20).
        Assert.Equal("Ø30 x 85", StockSize.Round(Mm(18, 18.01, 82), PrintUnits.Metric, Workbook()).Text);
    }

    [Fact]
    public void Plate_TinyBox_NeverRoundsToZero()
    {
        var wb = new StockWorkbook(new StockUnitData([1], [], 0m, 5m), StockWorkbook.Empty.Imperial);

        Assert.Equal("PL 1 x 5 x 5", StockSize.Plate(Mm(0.00001, 0.00001, 0.00001), PrintUnits.Metric, wb).Text);
    }

    [Fact]
    public void Plate_FloatingPointNoise_DoesNotJumpAStep()
    {
        // 6.3 cm and 12.3 cm as centimetres in a double are not exactly 63 and 123 mm.
        var result = StockSize.Plate([1.0, 6.3, 12.3], PrintUnits.Metric, Workbook());

        Assert.Equal("PL 12 x 65 x 125", result.Text);
    }

    [Fact]
    public void Plate_ThickerThanTheBiggestListed_SaysSoAndGivesNoText()
    {
        var result = StockSize.Plate(Mm(15, 61, 123), PrintUnits.Metric, Workbook());

        Assert.False(result.Succeeded);
        Assert.Null(result.Text);
        Assert.Contains("larger than the biggest listed", result.Problem);
        Assert.Contains("17 mm", result.Problem);
        Assert.Contains("16 mm", result.Problem);
    }

    [Fact]
    public void Plate_EmptyThicknessList_SaysTheListIsEmpty()
    {
        var result = StockSize.Plate(Mm(5, 61, 123), PrintUnits.Metric, StockWorkbook.Empty);

        Assert.Null(result.Text);
        Assert.Contains("lists no plate thickness sizes", result.Problem);
    }

    [Fact]
    public void Plate_Imperial_UsesInchesAndTheSharedNumberFormat()
    {
        // 0.40 + 0.08 = 0.48 -> 0.5; 2.30 + 0.08 = 2.38 -> 2.5; 4.80 + 0.08 = 4.88 -> 5.
        var result = StockSize.Plate(In(0.40, 2.30, 4.80), PrintUnits.Imperial, Workbook());

        Assert.Equal("PL 0.5 x 2.5 x 5", result.Text);
        Assert.Equal(PrintUnits.Imperial, result.Units);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void BadBox_GivesAProblemNotAGuess(double bad)
    {
        var plate = StockSize.Plate([1, 2, bad], PrintUnits.Metric, Workbook());
        var round = StockSize.Round([1, 2, bad], PrintUnits.Metric, Workbook());

        Assert.Null(plate.Text);
        Assert.Contains("could not be read", plate.Problem);
        Assert.Null(round.Text);
    }

    [Fact]
    public void WrongNumberOfLengths_GivesAProblem()
    {
        Assert.Null(StockSize.Plate([1, 2], PrintUnits.Metric, Workbook()).Text);
    }

    // ---- Round ----

    [Fact]
    public void Round_DiameterFromTheTwoSmaller_LengthFromTheLongest()
    {
        // The larger of the two smaller is 27.5; + 2 = 29.5 -> 30. Length 82 + 2 = 84 -> 85.
        var result = StockSize.Round(Mm(27, 27.5, 82), PrintUnits.Metric, Workbook());

        Assert.Equal("Ø30 x 85", result.Text);
        Assert.Equal(30m, result.Primary);
        Assert.Null(result.Width);
        Assert.Equal(85m, result.Length);
    }

    [Fact]
    public void Round_BiggerThanTheBiggestDiameter_SaysSo()
    {
        var result = StockSize.Round(Mm(40, 41, 90), PrintUnits.Metric, Workbook());

        Assert.Null(result.Text);
        Assert.Contains("round bar diameter", result.Problem);
        Assert.Contains("Round tab", result.Problem);
    }

    [Fact]
    public void Round_Imperial_UsesTheSharedNumberFormat()
    {
        var result = StockSize.Round(In(0.8, 0.9, 3.0), PrintUnits.Imperial, Workbook());

        Assert.Equal("Ø1 x 3.25", result.Text);
    }

    // ---- The number format is task 230's ----

    [Fact]
    public void NumberFormat_IsTheOneTheSheetMetalTextUses()
    {
        // 6.4 mm -> the same digits a sheet metal text prints for a 6.4 mm thickness.
        var sheetMetal = SheetMetalSizeText.For(0.64, 6, 12, PrintUnits.Metric);
        var imperial = SheetMetalSizeText.For(2.54 * 0.125, 2.54, 5.08, PrintUnits.Imperial);

        Assert.Equal("6.4 x 60 x 120 mm", sheetMetal);
        Assert.Equal("0.125 x 1 x 2 in", imperial);
        Assert.Equal("6.4", MaterialSizeNumber.FromCm(0.64, imperial: false));
        Assert.Equal("0.125", MaterialSizeNumber.FromUnits(0.125m, imperial: true));
        Assert.Equal("65", MaterialSizeNumber.FromUnits(65m, imperial: false));
    }

    // ---- Workbook reading ----

    private static Stream Xlsx(
        object?[][]? plate = null, object?[][]? round = null, object?[][]? settings = null, bool withSettings = true)
    {
        object?[][] Tab(string title, string[] headers, object?[][] rows) => [[title], ["A note."], [], headers, .. rows];

        var xlsx = new TestXlsx()
            .Sheet("How to edit", [["Text only."]])
            .Sheet("Plate", Tab("p", ["Thickness", "Units"], plate ?? [[8, "Metric"]]))
            .Sheet("Round", Tab("r", ["Diameter", "Units"], round ?? [[20, "Metric"]]));
        if (withSettings)
        {
            xlsx.Sheet("Settings", Tab("s", ["Units", "Allowance", "Step"], settings ?? [["Metric", 1, 5]]));
        }

        return xlsx.Build();
    }

    [Fact]
    public void Read_InventedWorkbook_GivesSortedListsAndSettings_WithNoProblems()
    {
        using var stream = Xlsx(
            plate: [[12, "Metric"], [6, "metric"], [0.5, "Imperial"], [8, "mm"], [6, "Metric"]],
            round: [[30, "Metric"], [1, "in"]],
            settings: [["Metric", 1.5, 10], ["Imperial", 0.05, 0.5]]);

        var result = StockWorkbookReader.Read(stream);

        Assert.False(result.HasProblems, string.Join("; ", result.Problems));
        Assert.Equal([6m, 8m, 12m], result.Workbook.Metric.PlateThicknesses);
        Assert.Equal([0.5m], result.Workbook.Imperial.PlateThicknesses);
        Assert.Equal([30m], result.Workbook.Metric.RoundDiameters);
        Assert.Equal([1m], result.Workbook.Imperial.RoundDiameters);
        Assert.Equal(1.5m, result.Workbook.Metric.AllowancePerSide);
        Assert.Equal(10m, result.Workbook.Metric.Step);
        Assert.Equal(0.05m, result.Workbook.Imperial.AllowancePerSide);
        Assert.Equal(0.5m, result.Workbook.Imperial.Step);
    }

    [Fact]
    public void Read_BadRows_NameTabAndRow_AndTheRestLoad()
    {
        using var stream = Xlsx(
            plate: [[8, "Metric"], [null, "Metric"], [10, "Furlongs"], [-2, "Metric"], [12, null], [16, "Metric"]]);

        var result = StockWorkbookReader.Read(stream);

        Assert.Equal([8m, 16m], result.Workbook.Metric.PlateThicknesses);
        Assert.Equal(
            [
                "Plate tab, row 6: Thickness is blank; row left out.",
                "Plate tab, row 7: Units \"Furlongs\" is not Metric or Imperial; row left out.",
                "Plate tab, row 8: Thickness \"-2\" is not a number above zero; row left out.",
                "Plate tab, row 9: Units is blank; row left out.",
            ],
            result.Problems.Select(p => p.ToString()));
        Assert.False(result.CouldNotRead);
    }

    [Fact]
    public void Read_MissingSettingsTab_UsesDefaults_AndReportsIt()
    {
        using var stream = Xlsx(withSettings: false);

        var result = StockWorkbookReader.Read(stream);

        Assert.True(result.CouldNotRead);
        Assert.Contains(result.Problems, p => p.Message.Contains("no \"Settings\" tab"));
        Assert.Equal(StockWorkbook.DefaultMetricAllowanceMm, result.Workbook.Metric.AllowancePerSide);
        Assert.Equal(StockWorkbook.DefaultImperialStepIn, result.Workbook.Imperial.Step);
        Assert.Equal([8m], result.Workbook.Metric.PlateThicknesses);
    }

    [Fact]
    public void Read_DuplicateSettingsUnits_KeepsTheFirst_AndReportsTheSecond()
    {
        using var stream = Xlsx(settings: [["Metric", 2, 10], ["mm", 3, 20]]);

        var result = StockWorkbookReader.Read(stream);

        Assert.Equal(2m, result.Workbook.Metric.AllowancePerSide);
        Assert.Equal(10m, result.Workbook.Metric.Step);
        Assert.Equal("Settings tab, row 6: Metric is already set on an earlier row; row left out.", result.Problems.Single().ToString());
    }

    [Fact]
    public void Read_BadSettingsRow_UsesTheDefaultForThatUnits()
    {
        using var stream = Xlsx(settings: [["Metric", "lots", 5], ["Imperial", 0.1, 0.5]]);

        var result = StockWorkbookReader.Read(stream);

        Assert.Equal(StockWorkbook.DefaultMetricAllowanceMm, result.Workbook.Metric.AllowancePerSide);
        Assert.Equal(0.1m, result.Workbook.Imperial.AllowancePerSide);
        Assert.Single(result.Problems);
        Assert.Contains("Settings tab, row 5", result.Problems[0].ToString());
    }

    [Fact]
    public void Read_MissingHeader_LeavesTheTabOut()
    {
        var xlsx = new TestXlsx()
            .Sheet("Plate", [["t"], ["n"], [], ["Thick", "Units"], [8, "Metric"]])
            .Sheet("Round", [["t"], ["n"], [], ["Diameter", "Units"], [20, "Metric"]])
            .Sheet("Settings", [["t"], ["n"], [], ["Units", "Allowance", "Step"], ["Metric", 1, 5]]);
        using var stream = xlsx.Build();

        var result = StockWorkbookReader.Read(stream);

        Assert.Empty(result.Workbook.Metric.PlateThicknesses);
        Assert.Equal([20m], result.Workbook.Metric.RoundDiameters);
        Assert.Contains("Plate tab has no \"Thickness\" column", result.Problems.Single().Message);
    }

    [Fact]
    public void Read_NotAnXlsx_GivesOneProblemAndAnEmptyWorkbook()
    {
        using var stream = new MemoryStream("not a zip"u8.ToArray());

        var result = StockWorkbookReader.Read(stream);

        Assert.True(result.CouldNotRead);
        Assert.Equal(StockWorkbook.Empty, result.Workbook);
    }

    [Fact]
    public void Read_MissingFile_NamesWhichWorkbook()
    {
        var starter = new LibraryPath(@"C:\nowhere\libraries\AWB_Stock.xlsx", IsStarter: true);
        var mine = new LibraryPath(@"D:\nowhere\AWB_Stock.xlsx", IsStarter: false);

        var s = StockWorkbookReader.Read(starter);
        var m = StockWorkbookReader.Read(mine);

        Assert.StartsWith("The starter stock workbook was not found at C:\\nowhere", s.Problems.Single().Message);
        Assert.StartsWith("The stock workbook set in Settings was not found at D:\\nowhere", m.Problems.Single().Message);
    }

    [Fact]
    public void Read_StarterWorkbook_LoadsCleanly_AndGivesTheAcceptanceExampleShape()
    {
        var result = StockWorkbookReader.Read(StarterPath());

        Assert.False(result.HasProblems, string.Join("; ", result.Problems));
        Assert.NotEmpty(result.Workbook.Metric.PlateThicknesses);
        Assert.NotEmpty(result.Workbook.Metric.RoundDiameters);
        Assert.NotEmpty(result.Workbook.Imperial.PlateThicknesses);
        Assert.NotEmpty(result.Workbook.Imperial.RoundDiameters);
        Assert.Equal("PL 16 x 65 x 125", StockSize.Plate(Mm(10.2, 61, 123), PrintUnits.Metric, result.Workbook).Text);
        Assert.True(StockSize.Round(Mm(27, 27.5, 82), PrintUnits.Metric, result.Workbook).Succeeded);
        Assert.True(StockSize.Plate(In(0.4, 2.3, 4.8), PrintUnits.Imperial, result.Workbook).Succeeded);
    }

    private static string StarterPath()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "InventorAddin.slnx")))
            {
                return Path.Combine(dir.FullName, "libraries", LibraryPaths.StarterStockWorkbookFileName);
            }
        }

        throw new InvalidOperationException("Could not find the repository root.");
    }

    // ---- Paths and settings ----

    [Fact]
    public void LibraryPaths_StockWorkbook_BlankIsTheStarter_AndASettingIsTrimmed()
    {
        var blank = LibraryPaths.StockWorkbook(new AddinSettings(), @"C:\addin");
        var set = LibraryPaths.StockWorkbook(new AddinSettings { StockWorkbookPath = @"  D:\mine\AWB_Stock.xlsx " }, @"C:\addin");

        Assert.True(blank.IsStarter);
        Assert.Equal(Path.Combine(@"C:\addin", "libraries", "AWB_Stock.xlsx"), blank.Path);
        Assert.False(set.IsStarter);
        Assert.Equal(@"D:\mine\AWB_Stock.xlsx", set.Path);
    }

    [Fact]
    public void Settings_StockWorkbookPath_IsPartOfEquality_AndNullIsBlank()
    {
        var a = new AddinSettings { StockWorkbookPath = @"D:\a.xlsx" };

        Assert.NotEqual(a, new AddinSettings());
        Assert.Equal(a, new AddinSettings { StockWorkbookPath = @"D:\a.xlsx" });
        Assert.Equal("", new AddinSettings { StockWorkbookPath = null! }.StockWorkbookPath);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(@"D:\Libraries\AWB_Stock.xlsx")]
    public void ValidateStockWorkbookPath_BlankOrFullXlsxPath_IsValid(string? path) =>
        Assert.Null(SettingsViewModel.ValidateStockWorkbookPath(path));

    [Theory]
    [InlineData(@"D:\Libraries\AWB_Stock.csv")]
    [InlineData("AWB_Stock.xlsx")]
    public void ValidateStockWorkbookPath_Bad_NamesTheSetting(string path) =>
        Assert.StartsWith("Stock data", SettingsViewModel.ValidateStockWorkbookPath(path));

    // ---- The window's model ----

    private static StockSizeViewModel Vm(
        double[]? box = null, PrintUnits units = PrintUnits.Metric, string? stored = null, StockShape shape = StockShape.Plate) =>
        new(box ?? Mm(10.2, 61, 123), units, Workbook(), storedMaterialSize: stored, shape: shape);

    [Fact]
    public void ViewModel_StartsWithThePlateResult()
    {
        var vm = Vm();

        Assert.True(vm.IsPlate);
        Assert.False(vm.IsRound);
        Assert.Equal("PL 16 x 65 x 125", vm.Text);
        Assert.False(vm.IsEdited);
        Assert.True(vm.CanWrite);
        Assert.Null(vm.Message);
    }

    [Fact]
    public void ViewModel_ChoosingRound_WorksOutTheRoundText_AndRaisesChanges()
    {
        var vm = Vm(Mm(27, 27.5, 82));
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.IsRound = true;

        Assert.Equal(StockShape.Round, vm.Shape);
        Assert.Equal("Ø30 x 85", vm.Text);
        Assert.Contains(nameof(StockSizeViewModel.Text), changed);
        Assert.Contains(nameof(StockSizeViewModel.Result), changed);
    }

    [Fact]
    public void ViewModel_EditedText_IsWrittenAsEdited()
    {
        var vm = Vm();

        vm.Text = "  PL 16 x 70 x 130  ";

        Assert.True(vm.IsEdited);
        var write = vm.WriteFor();
        Assert.NotNull(write);
        Assert.Equal(PartPropertyStorage.MaterialSize, write!.Location);
        Assert.Equal("PL 16 x 70 x 130", write.TextValue);
        Assert.False(write.IsRefresh);
    }

    [Fact]
    public void ViewModel_ChangingShape_DropsAnEdit()
    {
        var vm = Vm(Mm(27, 27.5, 82));
        vm.Text = "mine";

        vm.Shape = StockShape.Round;

        Assert.Equal("Ø30 x 85", vm.Text);
        Assert.False(vm.IsEdited);
    }

    [Fact]
    public void ViewModel_StoredSameText_WritesNothing_AndADifferentOneIsARefresh()
    {
        Assert.Null(Vm(stored: " PL 16 x 65 x 125 ").WriteFor());

        var write = Vm(stored: "old").WriteFor();
        Assert.NotNull(write);
        Assert.True(write!.IsRefresh);
    }

    [Fact]
    public void ViewModel_NothingFits_ShowsTheProblem_BlankText_AndCannotWriteUntilTyped()
    {
        var vm = Vm(Mm(30, 61, 123));

        Assert.Equal("", vm.Text);
        Assert.False(vm.CanWrite);
        Assert.Contains("larger than the biggest listed", vm.Message);
        Assert.Null(vm.WriteFor());

        vm.Text = "PL 40 x 65 x 125";

        Assert.True(vm.CanWrite);
        Assert.Null(vm.Message);
        Assert.Contains("larger than the biggest listed", vm.ResultProblem);
        Assert.Equal("PL 40 x 65 x 125", vm.WriteFor()!.TextValue);
    }

    [Fact]
    public void ViewModel_BlankedText_WritesNothing()
    {
        var vm = Vm();

        vm.Text = "   ";

        Assert.False(vm.CanWrite);
        Assert.Null(vm.WriteFor());
    }

    [Fact]
    public void ViewModel_WorkbookProblems_AreShownOnePerLine()
    {
        var vm = new StockSizeViewModel(
            Mm(10, 61, 123),
            PrintUnits.Metric,
            Workbook(),
            [new StockWorkbookProblem("Plate", 6, "Thickness is blank; row left out."), new StockWorkbookProblem(null, null, "Another.")]);

        Assert.Equal(
            $"Plate tab, row 6: Thickness is blank; row left out.{Environment.NewLine}Another.",
            vm.WorkbookProblemsText);
        Assert.Null(Vm().WorkbookProblemsText);
    }
}
