using InventorAddin.Core.Holes;
using InventorAddin.Core.Materials;

namespace InventorAddin.Core.Tests.Holes;

public sealed class HoleWorkbookReaderTests
{
    // ---- An invented workbook, laid out like the starter: title row 1, note row 2, headers row 4 ----

    private static object?[][] Tab(string title, object?[] headers, params object?[][] rows) =>
        [[title], ["A note."], [], headers, .. rows];

    private static object?[][] SettingsRows(params object?[][] rows) =>
        [["Settings"], [], ["Setting", "Value", "Meaning", "R", "G", "B", "Swatch"], .. rows];

    private static Dictionary<string, object?[][]> Tabs() => new()
    {
        ["How to edit"] = [["Text only."]],
        ["Settings"] = SettingsRows(
            ["Default units", "Imperial", "Units the window opens in."],
            ["Thread depth (x D)", 2, "Thread depth multiplier."],
            ["Drill extra depth (x D)", 0.25m, "Drill multiplier."],
            ["Press fit colour", "Red", "", 201, 11, 12],
            ["Normal fit colour", "Blue", "", 13, 14, 202],
            ["Slip fit colour", "Green", "", 15, 203, 16]),
        ["Clearance"] = Tab("Clearance holes",
            ["Units", "Size", "Nominal", "Close", "Normal", "Loose", "Show"],
            ["Metric", "T3", 3, 3.1m, 3.3m, 3.6m, "Y"],
            ["Metric", "T7", 7, 7.2m, 7.5m, 8, "N"],
            ["Imperial", "#3", 0.099m, 0.104m, 0.11m, 0.12m, "Y"]),
        ["Counterbore"] = Tab("Counterbores",
            ["Units", "Size", "Screw", "Cbore dia", "Cbore depth", "Hole fit", "Show"],
            ["Metric", "T3", "TSCREW", 6.1m, 3.2m, "Normal", "Y"],
            ["Imperial", "#3", "TSCREW", 0.21m, 0.11m, "Close", "Y"]),
        ["Countersink"] = Tab("Countersinks",
            ["Units", "Size", "Screw", "Csink dia", "Angle (deg)", "Hole fit", "Show"],
            ["Metric", "T3", "TFLAT", 6.3m, 90, "Loose", "Y"]),
        ["Tapped"] = Tab("Tapped holes",
            ["Units", "Thread", "Nominal", "Pitch / TPI", "Tap drill", "Class", "Default thread depth", "Default drill depth", "Show"],
            ["Metric", "M6", 6, "1.0", 5.1m, "6H", new Formula("ROUND(C5*Settings!$B$5,3)", 11), new Formula("ROUND(G5+C5*Settings!$B$6,3)", 13), "Y"],
            ["Metric", "M8", 8, "1.25", 6.9m, "6H", null, null, "Y"],
            ["Imperial", "#10-32 UNF", 0.19m, "32", 0.16m, "2B", null, 0.5m, "Y"]),
        ["Dowel"] = Tab("Dowel sizes",
            ["Units", "Size", "Nominal", "Show"],
            ["Metric", "7 mm", 7, "Y"],
            ["Imperial", "1/7\"", 0.143m, "N"]),
        ["Precision Tol"] = Tab("Precision tolerances",
            ["Units", "Fit", "Name / purpose", "Type", "ISO code", "Upper", "Lower", "Default", "Note"],
            ["Metric", "Normal", "Test location", "ISO", "H7", null, null, "Y", "A test note."],
            ["Imperial", "Press", "Test press", "Limits", null, 0, -0.0004m, "N", ""]),
        ["Drill Point"] = Tab("Blind hole bottoms",
            ["Name", "Angle (deg)", "Default", "Show", "Note"],
            ["120 deg", 120, "Y", "Y", "Test point."],
            ["Flat", null, "N", "N", "Flat bottom."]),
    };

    private static HoleWorkbookReadResult Read(Dictionary<string, object?[][]> tabs)
    {
        var xlsx = new TestXlsx();
        foreach (var (name, rows) in tabs)
        {
            xlsx.Sheet(name, rows);
        }

        using var stream = xlsx.Build();
        return HoleWorkbookReader.Read(stream);
    }

    private static HoleWorkbookReadResult ReadWith(string tab, object?[][] rows)
    {
        var tabs = Tabs();
        tabs[tab] = rows;
        return Read(tabs);
    }

    private static string Problems(HoleWorkbookReadResult result) =>
        string.Join(Environment.NewLine, result.Problems.Select(p => p.ToString()));

    internal static string RepoWorkbookPath()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "InventorAddin.slnx")))
            {
                return Path.Combine(dir.FullName, "libraries", "AWB_Holes.xlsx");
            }
        }

        throw new InvalidOperationException(
            $"Could not find the repository root (the folder holding InventorAddin.slnx) above {AppContext.BaseDirectory}.");
    }

    // ---- The whole invented workbook ----

    [Fact]
    public void InventedWorkbook_ReadsEveryTab_WithNoProblems()
    {
        var result = Read(Tabs());

        Assert.False(result.HasProblems, Problems(result));
        var wb = result.Workbook;
        Assert.Equal(3, wb.Clearance.Count);
        Assert.Equal(2, wb.Counterbore.Count);
        Assert.Single(wb.Countersink);
        Assert.Equal(3, wb.Tapped.Count);
        Assert.Equal(2, wb.Dowel.Count);
        Assert.Equal(2, wb.PrecisionTolerances.Count);
        Assert.Equal(2, wb.DrillPoints.Count);
    }

    [Fact]
    public void Settings_ReadByName()
    {
        var settings = Read(Tabs()).Workbook.Settings;

        Assert.Equal(
            new HoleSettings(
                HoleUnits.Imperial,
                2m,
                0.25m,
                new HoleColour(201, 11, 12),
                new HoleColour(13, 14, 202),
                new HoleColour(15, 203, 16)),
            settings);
        Assert.Equal(new HoleColour(201, 11, 12), settings.FitColour(PrecisionFit.Press));
        Assert.Equal(new HoleColour(15, 203, 16), settings.FitColour(PrecisionFit.Slip));
    }

    [Fact]
    public void Settings_HeaderRowIsFoundWhereverItIs()
    {
        var result = ReadWith("Settings",
        [
            ["Setting", "Value", "R", "G", "B"],
            ["thread depth (x d)", 3],
            ["Default units", "Metric"],
            ["Drill extra depth (x D)", 1],
            ["Press fit colour", "", 1, 2, 3],
            ["Normal fit colour", "", 4, 5, 6],
            ["Slip fit colour", "", 7, 8, 9],
        ]);

        Assert.False(result.HasProblems, Problems(result));
        Assert.Equal(3m, result.Workbook.Settings.ThreadDepthMultiplier);
        Assert.Equal(HoleUnits.Metric, result.Workbook.Settings.DefaultUnits);
    }

    [Fact]
    public void Settings_BadOrMissingValues_AreReported_AndTakeTheStarterValue()
    {
        var result = ReadWith("Settings", SettingsRows(
            ["Default units", "Furlongs"],
            ["Thread depth (x D)", "deep"],
            ["Press fit colour", "", "", 300, 0, 0],
            ["Normal fit colour", "", "", 1, 2, 3],
            ["Slip fit colour", "", "", 4, 5, 6]));

        var settings = result.Workbook.Settings;
        Assert.Equal(HoleSettings.Starter.DefaultUnits, settings.DefaultUnits);
        Assert.Equal(HoleSettings.Starter.ThreadDepthMultiplier, settings.ThreadDepthMultiplier);
        Assert.Equal(HoleSettings.Starter.DrillExtraDepthMultiplier, settings.DrillExtraDepthMultiplier);
        Assert.Equal(HoleSettings.Starter.PressFitColour, settings.PressFitColour);
        Assert.Equal(new HoleColour(1, 2, 3), settings.NormalFitColour);
        Assert.Equal(
            [
                "Settings tab, row 4: Default units: Value \"Furlongs\" is not Metric or Imperial; Metric is used.",
                "Settings tab, row 5: Thread depth (x D): Value \"deep\" is not a number greater than 0, for example 6.4; 1.5 is used.",
                "Settings tab: There is no \"Drill extra depth (x D)\" setting; 0.5 is used.",
                "Settings tab, row 6: Press fit colour: R \"300\" is not a whole number from 0 to 255; 220, 40, 40 is used.",
            ],
            result.Problems.Select(p => p.ToString()));
    }

    // ---- Blind depth multiplier (task 097) ----

    private static object?[][] SettingsWithBlind(object? value) => SettingsRows(
        ["Default units", "Imperial"],
        ["Thread depth (x D)", 2],
        ["Drill extra depth (x D)", 0.25m],
        ["Blind depth multiplier", value, "Blind holes start at this x D."],
        ["Press fit colour", "Red", "", 201, 11, 12],
        ["Normal fit colour", "Blue", "", 13, 14, 202],
        ["Slip fit colour", "Green", "", 15, 203, 16]);

    [Fact]
    public void Settings_BlindDepthMultiplier_IsRead()
    {
        var result = ReadWith("Settings", SettingsWithBlind(3.5m));

        Assert.False(result.HasProblems, Problems(result));
        Assert.Equal(3.5m, result.Workbook.Settings.BlindDepthMultiplier);
    }

    [Fact]
    public void Settings_WithoutTheBlindDepthRow_ReadWithNoProblem_AndTheStarterValue()
    {
        // The invented workbook, like a copy made before task 097, has no Blind depth multiplier row.
        var result = Read(Tabs());

        Assert.False(result.HasProblems, Problems(result));
        Assert.Equal(2m, result.Workbook.Settings.BlindDepthMultiplier);
        Assert.Equal(HoleSettings.StarterBlindDepthMultiplier, HoleSettings.Starter.BlindDepthMultiplier);
    }

    [Theory]
    [InlineData("deep", "Value \"deep\" is not a number greater than 0, for example 6.4")]
    [InlineData(0, "Value \"0\" is not a number greater than 0, for example 6.4")]
    [InlineData(null, "Value is blank")]
    public void Settings_BadBlindDepthMultiplier_IsReported_AndTakesTheStarterValue(object? value, string why)
    {
        var result = ReadWith("Settings", SettingsWithBlind(value));

        Assert.Equal(2m, result.Workbook.Settings.BlindDepthMultiplier);
        Assert.Equal(
            [$"Settings tab, row 7: Blind depth multiplier: {why}; 2 is used."],
            result.Problems.Select(p => p.ToString()));
    }

    [Fact]
    public void DefaultBlindDepth_IsMultiplierTimesNominal_RoundedAsTheThreadDepthIs()
    {
        Assert.Equal(12m, HoleSettings.Starter.DefaultBlindDepth(6m));
        Assert.Equal(0.38m, HoleSettings.Starter.DefaultBlindDepth(0.19m));
        Assert.Equal(3.704m, (HoleSettings.Starter with { BlindDepthMultiplier = 1.2345m }).DefaultBlindDepth(3m));
    }

    [Fact]
    public void Settings_MissingTab_UsesTheStarterSettings()
    {
        var tabs = Tabs();
        tabs.Remove("Settings");
        var result = Read(tabs);

        Assert.Equal(HoleSettings.Starter, result.Workbook.Settings);
        Assert.Equal(["The hole workbook has no \"Settings\" tab; the starter settings are used."], result.Problems.Select(p => p.ToString()));
    }

    // ---- Data tabs ----

    [Fact]
    public void Clearance_RowsKeepTheirUnits_AndShowNIsHidden()
    {
        var rows = Read(Tabs()).Workbook.Clearance;

        Assert.Equal(new ClearanceRow(5, HoleUnits.Metric, "T3", 3m, 3.1m, 3.3m, 3.6m, Hidden: false), rows[0]);
        Assert.True(rows[1].Hidden);
        Assert.Equal(HoleUnits.Imperial, rows[2].Units);
        Assert.Equal(0.104m, rows[2].Diameter(ClearanceFit.Close));
        Assert.Equal(3.6m, rows[0].Diameter(ClearanceFit.Loose));
    }

    [Fact]
    public void CounterboreAndCountersink_Read()
    {
        var wb = Read(Tabs()).Workbook;

        Assert.Equal(
            new CounterboreRow(6, HoleUnits.Imperial, "#3", "TSCREW", 0.21m, 0.11m, ClearanceFit.Close, Hidden: false),
            wb.Counterbore[1]);
        Assert.Equal(
            new CountersinkRow(5, HoleUnits.Metric, "T3", "TFLAT", 6.3m, 90m, ClearanceFit.Loose, Hidden: false),
            wb.Countersink[0]);
    }

    [Fact]
    public void Tapped_UsesTheCellDepths_OrWorksThemOutFromSettings()
    {
        var tapped = Read(Tabs()).Workbook.Tapped;

        // Cached formula values are used as they are (11 and 13, not 6 x 2 and 12 + 6 x 0.25).
        Assert.Equal((11m, 13m), (tapped[0].ThreadDepth, tapped[0].DrillDepth));

        // Blank: thread round(8 x 2, 3) = 16; drill round(16 + 8 x 0.25, 3) = 18.
        Assert.Equal((16m, 18m), (tapped[1].ThreadDepth, tapped[1].DrillDepth));

        // Thread blank, drill given: round(0.19 x 2, 3) = 0.38, drill 0.5 as typed.
        Assert.Equal((0.38m, 0.5m), (tapped[2].ThreadDepth, tapped[2].DrillDepth));
    }

    [Fact]
    public void Tapped_DesignationAndThreadType_AreInventors()
    {
        var tapped = Read(Tabs()).Workbook.Tapped;

        Assert.Equal("M6x1", tapped[0].Designation);
        Assert.Equal("M8x1.25", tapped[1].Designation);
        Assert.Equal("10-32 UNF", tapped[2].Designation);
        Assert.Equal("ISO Metric profile", tapped[0].ThreadType);
        Assert.Equal("ANSI Unified Screw Threads", tapped[2].ThreadType);
        Assert.Equal("#10-32 UNF", tapped[2].Thread);
        Assert.Equal(32m, tapped[2].PitchOrTpi);
        Assert.Equal("2B", tapped[2].Class);
    }

    [Theory]
    [InlineData(HoleUnits.Metric, "M6", "1.0", "M6x1")]
    [InlineData(HoleUnits.Metric, "M8", "1.25", "M8x1.25")]
    [InlineData(HoleUnits.Metric, "M10", "1.50", "M10x1.5")]
    [InlineData(HoleUnits.Metric, "M2.5", "0.45", "M2.5x0.45")]
    [InlineData(HoleUnits.Metric, "M8x1", "1", "M8x1")]
    [InlineData(HoleUnits.Imperial, "#10-32 UNF", "32", "10-32 UNF")]
    [InlineData(HoleUnits.Imperial, "#4-40 UNC", "40", "4-40 UNC")]
    [InlineData(HoleUnits.Imperial, "1/4-20 UNC", "20", "1/4-20 UNC")]
    public void ThreadDesignation(HoleUnits units, string thread, string pitch, string expected) =>
        Assert.Equal(expected, TappedRow.ThreadDesignation(units, thread, decimal.Parse(pitch, System.Globalization.CultureInfo.InvariantCulture)));

    [Fact]
    public void Dowel_Reads()
    {
        var dowel = Read(Tabs()).Workbook.Dowel;

        Assert.Equal(new DowelRow(5, HoleUnits.Metric, "7 mm", 7m, Hidden: false), dowel[0]);
        Assert.True(dowel[1].Hidden);
    }

    [Fact]
    public void PrecisionTol_IsoAndLimitsRows()
    {
        var rows = Read(Tabs()).Workbook.PrecisionTolerances;

        Assert.Equal(
            new PrecisionToleranceRow(5, HoleUnits.Metric, PrecisionFit.Normal, "Test location", ToleranceKind.Iso, "H7", null, null, IsDefault: true, "A test note."),
            rows[0]);
        Assert.Equal(
            new PrecisionToleranceRow(6, HoleUnits.Imperial, PrecisionFit.Press, "Test press", ToleranceKind.Limits, null, 0m, -0.0004m, IsDefault: false, ""),
            rows[1]);
    }

    [Fact]
    public void DrillPoint_FlatHasNoAngle()
    {
        var points = Read(Tabs()).Workbook.DrillPoints;

        Assert.Equal(new DrillPointRow(5, "120 deg", 120m, IsDefault: true, Hidden: false, "Test point."), points[0]);
        Assert.True(points[1].IsFlat);
        Assert.True(points[1].Hidden);
        Assert.False(points[0].IsFlat);
    }

    [Fact]
    public void HowToEdit_IsNotRequired()
    {
        var tabs = Tabs();
        tabs.Remove("How to edit");
        Assert.False(Read(tabs).HasProblems);
    }

    [Fact]
    public void Headers_MatchIgnoringCaseAndSpaces_InAnyOrder_ExtraColumnsIgnored()
    {
        var result = ReadWith("Dowel", Tab("Dowel",
            ["  show ", "Extra", "NOMINAL", "size", "units"],
            ["Y", "ignored", 9, "9 mm", " metric "]));

        Assert.False(result.HasProblems, Problems(result));
        Assert.Equal(new DowelRow(5, HoleUnits.Metric, "9 mm", 9m, Hidden: false), result.Workbook.Dowel[0]);
    }

    [Fact]
    public void Rows_StopAtTheFirstFullyBlankRow()
    {
        var result = ReadWith("Dowel", Tab("Dowel",
            ["Units", "Size", "Nominal", "Show"],
            ["Metric", "7 mm", 7, "Y"],
            [],
            ["Metric", "after the gap", 9, "Y"]));

        Assert.Single(result.Workbook.Dowel);
        Assert.False(result.HasProblems);
    }

    [Fact]
    public void BlankShow_ShowsTheRow_AndBooleanCellsWork()
    {
        var result = ReadWith("Dowel", Tab("Dowel",
            ["Units", "Size", "Nominal", "Show"],
            ["Metric", "7 mm", 7, null],
            ["Metric", "8 mm", 8, false],
            ["Metric", "9 mm", 9, new Inline("n")]));

        Assert.False(result.HasProblems, Problems(result));
        Assert.Equal([false, true, true], result.Workbook.Dowel.Select(d => d.Hidden));
    }

    [Fact]
    public void NumbersWrittenAsText_AreRead()
    {
        var result = ReadWith("Dowel", Tab("Dowel",
            ["Units", "Size", "Nominal", "Show"],
            ["Metric", "7 mm", "7.0", "Y"]));

        Assert.Equal(7m, result.Workbook.Dowel[0].Nominal);
    }

    [Fact]
    public void ExcelSavedNumbers_ReadAsTyped()
    {
        var tabs = Tabs();
        tabs["Dowel"] = Tab("Dowel",
            ["Units", "Size", "Nominal", "Show"],
            ["Metric", "6.6 mm", new RawNumber("6.5999999999999996"), "Y"],
            ["Imperial", "test", new RawNumber("0.28499999999999998"), "Y"]);
        tabs["Tapped"] = Tab("Tapped holes",
            ["Units", "Thread", "Nominal", "Pitch / TPI", "Tap drill", "Class", "Default thread depth", "Default drill depth", "Show"],
            ["Metric", "M2.5", 2.5m, new RawNumber("0.45000000000000001"), new RawNumber("2.0499999999999998"), "6H", null, null, "Y"]);
        var result = Read(tabs);

        Assert.False(result.HasProblems, Problems(result));
        Assert.Equal([6.6m, 0.285m], result.Workbook.Dowel.Select(d => d.Nominal));
        var tap = Assert.Single(result.Workbook.Tapped);
        Assert.Equal("M2.5x0.45", tap.Designation);
        Assert.Equal(2.05m, tap.TapDrill);
    }

    // ---- Problems ----

    [Fact]
    public void ErrorCells_AreReportedAsErrors()
    {
        var result = ReadWith("Tapped", Tab("Tapped holes",
            ["Units", "Thread", "Nominal", "Pitch / TPI", "Tap drill", "Class", "Default thread depth", "Default drill depth", "Show"],
            ["Metric", "M6", 6, "1.0", 5.1m, "6H", new ErrorValue("#DIV/0!"), null, "Y"]));

        Assert.Empty(result.Workbook.Tapped);
        Assert.Equal(
            ["Tapped tab, row 5: Default thread depth shows the error #DIV/0!; check its formula; row left out."],
            result.Problems.Select(p => p.ToString()));
    }

    [Fact]
    public void MissingTab_IsReported_AndTheRestLoad()
    {
        var tabs = Tabs();
        tabs.Remove("Countersink");
        var result = Read(tabs);

        Assert.Equal(["The hole workbook has no \"Countersink\" tab; its rows are not available."], result.Problems.Select(p => p.ToString()));
        Assert.Empty(result.Workbook.Countersink);
        Assert.Equal(3, result.Workbook.Clearance.Count);
    }

    [Fact]
    public void MissingHeaders_LeaveTheTabOut()
    {
        var result = ReadWith("Clearance", Tab("Clearance",
            ["Units", "Size", "Nominal", "Close", "Normal", "Show"],
            ["Metric", "T3", 3, 3.1m, 3.3m, "Y"]));

        Assert.Empty(result.Workbook.Clearance);
        Assert.Equal(
            ["Clearance tab, row 4: The Clearance tab has no \"Loose\" column. Row 4 should read: Units, Size, Nominal, Close, Normal, Loose, Show; tab left out."],
            result.Problems.Select(p => p.ToString()));
    }

    [Fact]
    public void BadRows_AreLeftOut_WithOneProblemPerThingWrong()
    {
        var result = ReadWith("Clearance", Tab("Clearance",
            ["Units", "Size", "Nominal", "Close", "Normal", "Loose", "Show"],
            ["Metric", "T3", 3, 3.1m, 3.3m, 3.6m, "Y"],
            ["Cubits", "T4", "four", 4.1m, 4.3m, 4.6m, "Maybe"],
            ["Metric", "", 5, 5.1m, -1, 5.6m, "Y"],
            ["Imperial", "#3", 0.099m, 0.104m, 0.11m, 0.12m, "Y"]));

        Assert.Equal(["T3", "#3"], result.Workbook.Clearance.Select(c => c.Size));
        Assert.Equal(
            [
                "Clearance tab, row 6: Units \"Cubits\" is not Metric or Imperial; row left out.",
                "Clearance tab, row 6: Nominal \"four\" is not a number greater than 0, for example 6.4; row left out.",
                "Clearance tab, row 6: Show \"Maybe\" is not Y or N; row left out.",
                "Clearance tab, row 7: Size is blank; row left out.",
                "Clearance tab, row 7: Normal \"-1\" is not a number greater than 0, for example 6.4; row left out.",
            ],
            result.Problems.Select(p => p.ToString()));
    }

    [Fact]
    public void UnknownFitsAndTypes_AreReported()
    {
        var tabs = Tabs();
        tabs["Counterbore"] = Tab("Counterbores",
            ["Units", "Size", "Screw", "Cbore dia", "Cbore depth", "Hole fit", "Show"],
            ["Metric", "T3", "TSCREW", 6.1m, 3.2m, "Snug", "Y"]);
        tabs["Precision Tol"] = Tab("Precision tolerances",
            ["Units", "Fit", "Name / purpose", "Type", "ISO code", "Upper", "Lower", "Default", "Note"],
            ["Metric", "Tight", "A", "ISO", "H7", null, null, "Y", ""],
            ["Metric", "Slip", "B", "Guess", "", null, null, "N", ""],
            ["Metric", "Slip", "C", "ISO", "", null, null, "N", ""],
            ["Imperial", "Normal", "D", "Limits", null, -0.001m, 0.001m, "N", ""],
            ["Imperial", "Normal", "E", "limits", null, "x", null, "N", ""]);
        var result = Read(tabs);

        Assert.Empty(result.Workbook.Counterbore);
        Assert.Empty(result.Workbook.PrecisionTolerances);
        Assert.Equal(
            [
                "Counterbore tab, row 5: Hole fit \"Snug\" is not one of Close, Normal or Loose; row left out.",
                "Precision Tol tab, row 5: Fit \"Tight\" is not one of Slip, Normal or Press; row left out.",
                "Precision Tol tab, row 6: Type \"Guess\" is not ISO or Limits; row left out.",
                "Precision Tol tab, row 7: ISO code is blank on a Type ISO row; row left out.",
                "Precision Tol tab, row 8: Upper -0.001 is less than Lower 0.001; row left out.",
                "Precision Tol tab, row 9: Upper \"x\" is not a number, for example 0.0005; row left out.",
                "Precision Tol tab, row 9: Lower is blank; row left out.",
            ],
            result.Problems.Select(p => p.ToString()));
    }

    [Fact]
    public void DrillPoint_BadAngle_IsReported()
    {
        var result = ReadWith("Drill Point", Tab("Drill",
            ["Name", "Angle (deg)", "Default", "Show", "Note"],
            ["Odd", 200, "N", "Y", ""]));

        Assert.Equal(
            ["Drill Point tab, row 5: Angle (deg) \"200\" is not an angle between 0 and 180 degrees, for example 90; row left out."],
            result.Problems.Select(p => p.ToString()));
    }

    [Fact]
    public void MissingFile_GivesOneProblem_AndAnEmptyWorkbook()
    {
        var path = Path.Combine(Path.GetTempPath(), $"awb-missing-{Guid.NewGuid():N}.xlsx");
        var result = HoleWorkbookReader.Read(path);

        Assert.Same(HoleWorkbook.Empty, result.Workbook);
        Assert.Equal([$"The hole workbook was not found at {path}."], result.Problems.Select(p => p.ToString()));
    }

    [Fact]
    public void UnreadableFile_GivesOneProblem_AndAnEmptyWorkbook()
    {
        var path = Path.Combine(Path.GetTempPath(), $"awb-bad-{Guid.NewGuid():N}.xlsx");
        File.WriteAllText(path, "this is not a zip");
        try
        {
            var result = HoleWorkbookReader.Read(path);

            Assert.Same(HoleWorkbook.Empty, result.Workbook);
            var problem = Assert.Single(result.Problems);
            Assert.StartsWith($"Could not read the hole workbook at {path}. Check that it is an .xlsx file.", problem.ToString());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void MissingFile_ByLibraryPath_SaysWhichWorkbook(bool isStarter)
    {
        var workbook = new LibraryPath(Path.Combine(Path.GetTempPath(), $"awb-missing-{Guid.NewGuid():N}.xlsx"), isStarter);
        var result = HoleWorkbookReader.Read(workbook);

        Assert.Same(HoleWorkbook.Empty, result.Workbook);
        Assert.Equal([HoleWorkbookMessages.Missing(workbook)], result.Problems.Select(p => p.ToString()));
    }

    [Fact]
    public void UnreadableFile_ByLibraryPath_SaysWhichWorkbook()
    {
        var workbook = new LibraryPath(Path.Combine(Path.GetTempPath(), $"awb-bad-{Guid.NewGuid():N}.xlsx"), IsStarter: false);
        File.WriteAllText(workbook.Path, "this is not a zip");
        try
        {
            var result = HoleWorkbookReader.Read(workbook);

            Assert.Same(HoleWorkbook.Empty, result.Workbook);
            var problem = Assert.Single(result.Problems).ToString();
            Assert.StartsWith($"Could not read the hole workbook set in Settings at {workbook.Path} (", problem);
            Assert.EndsWith("Close it in Excel, check that it is an .xlsx file, or check the hole data path in Settings.", problem);
        }
        finally
        {
            File.Delete(workbook.Path);
        }
    }

    [Fact]
    public void StarterWorkbook_ByLibraryPath_Reads()
    {
        var result = HoleWorkbookReader.Read(new LibraryPath(RepoWorkbookPath(), IsStarter: true));

        Assert.False(result.HasProblems, Problems(result));
        Assert.NotEmpty(result.Workbook.Clearance);
    }

    // ---- The committed starter workbook ----

    [Fact]
    public void StarterWorkbook_ReadsWithNoProblems()
    {
        var result = HoleWorkbookReader.Read(RepoWorkbookPath());

        Assert.False(result.HasProblems, Problems(result));
        var wb = result.Workbook;
        Assert.NotEmpty(wb.Clearance);
        Assert.NotEmpty(wb.Counterbore);
        Assert.NotEmpty(wb.Countersink);
        Assert.NotEmpty(wb.Tapped);
        Assert.NotEmpty(wb.Dowel);
        Assert.NotEmpty(wb.PrecisionTolerances);
        Assert.NotEmpty(wb.DrillPoints);
    }

    [Fact]
    public void StarterWorkbook_KnownRows()
    {
        var wb = HoleWorkbookReader.Read(RepoWorkbookPath()).Workbook;

        var m6 = wb.Clearance.Single(c => c.Units == HoleUnits.Metric && c.Size == "M6");
        Assert.Equal((6.4m, 6.6m, 7m), (m6.Close, m6.Normal, m6.Loose));

        var tap = wb.Tapped.Single(t => t.Designation == "M6x1");
        Assert.Equal(5m, tap.TapDrill);
        Assert.Equal((9m, 12m), (tap.ThreadDepth, tap.DrillDepth));
        Assert.Contains(wb.Tapped, t => t.Designation == "10-32 UNF" && t.Units == HoleUnits.Imperial);

        var h7 = wb.PrecisionTolerances.Single(p => p.Units == HoleUnits.Metric && p.IsoCode == "H7");
        Assert.Equal(PrecisionFit.Normal, h7.Fit);
        Assert.Equal(ToleranceKind.Iso, h7.Kind);
        Assert.True(h7.IsDefault);

        Assert.Equal(HoleSettings.Starter, wb.Settings);
        Assert.Equal(["118 deg", "90 deg", "Flat"], wb.DrillPoints.Select(d => d.Name));
        Assert.True(wb.DrillPoints[2].IsFlat);
    }

    [Fact]
    public void StarterWorkbook_HasTheBlindDepthMultiplierRow_AfterTheDrillExtraDepth()
    {
        using var stream = File.OpenRead(RepoWorkbookPath());
        var sheet = XlsxPackage.Read(stream).Sheet(HoleWorkbookReader.SettingsTab)!;

        Assert.Equal(HoleWorkbookReader.DrillExtraDepthSetting, sheet.Text(6, 1));
        Assert.Equal(HoleWorkbookReader.BlindDepthSetting, sheet.Text(7, 1));
        Assert.Equal("2", sheet.Text(7, 2));
        Assert.NotEqual("", sheet.Text(7, 3));
        Assert.Equal(HoleWorkbookReader.PressFitColourSetting, sheet.Text(8, 1));
        Assert.Equal(HoleWorkbookReader.SlipFitColourSetting, sheet.Text(10, 1));
        Assert.Equal(2m, HoleWorkbookReader.Read(RepoWorkbookPath()).Workbook.Settings.BlindDepthMultiplier);
    }

    // ---- The Dowel tab's seat columns (task 106) ----

    [Fact]
    public void Dowel_WithoutTheSeatColumns_ReadsWithNoProblem_AndNoSeatValues()
    {
        var result = Read(Tabs());

        Assert.False(result.HasProblems, Problems(result));
        var row = result.Workbook.Dowel[0];
        Assert.Null(row.CboreDiameter);
        Assert.Null(row.CboreDepth);
        Assert.Null(row.CsinkDiameter);
        Assert.Null(row.CsinkAngleDeg);
    }

    [Fact]
    public void Dowel_SeatColumns_AreRead_InAnyOrder_ABlankCellIsNull()
    {
        var result = ReadWith("Dowel", Tab("Dowel sizes",
            ["Units", "Size", "Nominal", "Show", "Csink angle", "Cbore dia", "Cbore depth", "Csink dia"],
            ["Metric", "7 mm", 7, "Y", 100, 8.5m, "3", 8.2m],
            ["Metric", "9 mm", 9, "Y", null, 10.5m, null, null]));

        Assert.False(result.HasProblems, Problems(result));
        Assert.Equal(
            new DowelRow(5, HoleUnits.Metric, "7 mm", 7m, false)
            {
                CboreDiameter = 8.5m, CboreDepth = 3m, CsinkDiameter = 8.2m, CsinkAngleDeg = 100m,
            },
            result.Workbook.Dowel[0]);
        Assert.Equal(
            new DowelRow(6, HoleUnits.Metric, "9 mm", 9m, false) { CboreDiameter = 10.5m },
            result.Workbook.Dowel[1]);
    }

    [Fact]
    public void Dowel_SomeSeatColumnsOnly_ReadWithNoProblem()
    {
        var result = ReadWith("Dowel", Tab("Dowel sizes",
            ["Units", "Size", "Nominal", "Show", "Cbore dia"],
            ["Metric", "7 mm", 7, "Y", 8.5m]));

        Assert.False(result.HasProblems, Problems(result));
        Assert.Equal(8.5m, result.Workbook.Dowel[0].CboreDiameter);
        Assert.Null(result.Workbook.Dowel[0].CsinkAngleDeg);
    }

    [Theory]
    [InlineData("Cbore dia", "wide")]
    [InlineData("Cbore depth", "0")]
    [InlineData("Csink dia", "-2")]
    [InlineData("Csink angle", "180")]
    [InlineData("Csink angle", "steep")]
    public void Dowel_BadSeatValue_IsAProblemNamingTabRowAndHeader_AndTheRowIsLeftOut(string header, string value)
    {
        string?[] headers = ["Units", "Size", "Nominal", "Show", "Cbore dia", "Cbore depth", "Csink dia", "Csink angle"];
        object?[] good = ["Metric", "8 mm", 8, "Y", 9m, 2m, 9m, 90];
        object?[] bad = ["Metric", "7 mm", 7, "Y", 8m, 2m, 8m, 90];
        bad[Array.IndexOf(headers, header)] = value;

        var result = ReadWith("Dowel", Tab("Dowel sizes", headers, bad, good));

        var problem = Assert.Single(result.Problems);
        Assert.Equal("Dowel", problem.Tab);
        Assert.Equal(5, problem.Row);
        Assert.Contains(header, problem.Message);
        Assert.Contains(value, problem.Message);
        Assert.Contains("row left out", problem.Message);
        Assert.Equal(["8 mm"], result.Workbook.Dowel.Select(d => d.Size));
    }

    [Fact]
    public void StarterWorkbook_DowelTab_HasTheSeatColumns_WithAValueOnEveryRow()
    {
        using var stream = File.OpenRead(RepoWorkbookPath());
        var sheet = XlsxPackage.Read(stream).Sheet(HoleWorkbookReader.DowelTab)!;
        Assert.Equal(
            ["Units", "Size", "Nominal", "Show", .. HoleWorkbookReader.DowelSeatHeaders],
            Enumerable.Range(1, 8).Select(c => sheet.Text(HoleWorkbookReader.HeaderRow, c)));

        var wb = HoleWorkbookReader.Read(RepoWorkbookPath()).Workbook;
        Assert.NotEmpty(wb.Dowel);
        foreach (var row in wb.Dowel)
        {
            Assert.True(row.CboreDiameter > row.Nominal, $"{row.Size}: Cbore dia");
            Assert.True(row.CboreDepth > 0, $"{row.Size}: Cbore depth");
            Assert.True(row.CsinkDiameter > row.Nominal, $"{row.Size}: Csink dia");
            Assert.Equal(90m, row.CsinkAngleDeg);
        }

        // Every starter dowel seat builds, blind at the starter depth too.
        foreach (var row in wb.Dowel)
        {
            var seat = HoleRules.DowelSeatStart(wb, row.Units, row.Size);
            var dowel = HoleSpec.ClearanceHole(row.Units, row.Size) with
            {
                Kind = HoleKind.Dowel,
                Termination = HoleTermination.Distance,
                Depth = wb.Settings.DefaultBlindDepth(row.Nominal),
            };
            foreach (var spec in new[]
                     {
                         dowel with { Seat = HoleSeat.Counterbore, CboreDiameter = seat.CboreDiameter, CboreDepth = seat.CboreDepth },
                         dowel with { Seat = HoleSeat.Countersink, CsinkDiameter = seat.CsinkDiameter, CsinkAngleDeg = seat.CsinkAngleDeg },
                     })
            {
                var built = HoleRules.Build(spec, wb, 1);
                Assert.True(built.Succeeded, $"{row.Size} {spec.Seat}: {string.Join(" | ", built.Problems)}");
            }
        }
    }

    [Fact]
    public void StarterWorkbook_HowToEdit_MentionsTheDowelSeatColumns()
    {
        using var stream = File.OpenRead(RepoWorkbookPath());
        var sheet = XlsxPackage.Read(stream).Sheet("How to edit")!;

        var line = Enumerable.Range(1, sheet.LastRow).Select(r => sheet.Text(r, 1)).Single(t => t.StartsWith("Dowel:"));
        foreach (var header in HoleWorkbookReader.DowelSeatHeaders)
        {
            Assert.Contains(header, line);
        }
    }
}
