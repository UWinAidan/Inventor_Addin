using System.Xml;
using InventorAddin.Core.Drawing;
using InventorAddin.Core.Holes;

namespace InventorAddin.Core.Materials;

/// <summary>
/// Reads the stock workbook, <c>libraries/AWB_Stock.xlsx</c> (spec 05, "Added for M5", item 5), without Excel, as
/// <see cref="NotesWorkbookReader"/> reads the notes workbook: tabs by name, columns by header text in any order, extra columns ignored,
/// and a bad row left out with a problem naming its tab and row while the rest load. The "How to edit" tab is for people and is not read.
/// Tabs: Plate (Thickness, Units), Round (Diameter, Units) and Settings (Units, Allowance, Step). Units read Metric (or mm) and
/// Imperial (or in). A units system with no usable Settings row gets the defaults on <see cref="StockWorkbook"/>.
/// </summary>
public static class StockWorkbookReader
{
    public const string PlateTab = "Plate";
    public const string RoundTab = "Round";
    public const string SettingsTab = "Settings";
    public const string ThicknessColumn = "Thickness";
    public const string DiameterColumn = "Diameter";
    public const string UnitsColumn = "Units";
    public const string AllowanceColumn = "Allowance";
    public const string StepColumn = "Step";

    /// <summary>The row that holds the headers on the Plate, Round and Settings tabs (title in row 1, note in row 2).</summary>
    public const int HeaderRow = 4;

    /// <summary>Reads the workbook at <paramref name="path"/>; a missing or unreadable file gives one problem and an empty workbook.</summary>
    public static StockWorkbookReadResult Read(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        return ReadFile(
            path,
            () => $"The stock workbook was not found at {path}.",
            reason => $"Could not read the stock workbook at {path}. Check that it is an .xlsx file. ({reason})");
    }

    /// <summary>Reads the workbook <see cref="LibraryPaths.StockWorkbook"/> resolved, with its own messages for a missing or unreadable file.</summary>
    public static StockWorkbookReadResult Read(LibraryPath workbook)
    {
        ArgumentNullException.ThrowIfNull(workbook);

        return ReadFile(
            workbook.Path,
            () => StockWorkbookMessages.Missing(workbook),
            reason => StockWorkbookMessages.Unreadable(workbook, reason));
    }

    /// <summary>Reads a workbook from a stream.</summary>
    public static StockWorkbookReadResult Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        try
        {
            return Read(XlsxPackage.Read(stream));
        }
        catch (Exception ex) when (IsReadFailure(ex))
        {
            return Failed($"Could not read the stock workbook. Check that it is an .xlsx file. ({ex.Message})");
        }
    }

    /// <summary>Reads the three tabs of a package that has already been opened.</summary>
    public static StockWorkbookReadResult Read(XlsxPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);

        var problems = new List<StockWorkbookProblem>();
        var plate = ReadList(package, PlateTab, ThicknessColumn, problems);
        var round = ReadList(package, RoundTab, DiameterColumn, problems);
        var settings = ReadSettings(package, problems);

        StockUnitData Build(PrintUnits units, decimal defaultAllowance, decimal defaultStep)
        {
            var (allowance, step) = settings.GetValueOrDefault(units, (defaultAllowance, defaultStep));
            return new StockUnitData(Sorted(plate[units]), Sorted(round[units]), allowance, step);
        }

        var workbook = new StockWorkbook(
            Build(PrintUnits.Metric, StockWorkbook.DefaultMetricAllowanceMm, StockWorkbook.DefaultMetricStepMm),
            Build(PrintUnits.Imperial, StockWorkbook.DefaultImperialAllowanceIn, StockWorkbook.DefaultImperialStepIn));
        return new StockWorkbookReadResult(workbook, problems.AsReadOnly());
    }

    private static StockWorkbookReadResult ReadFile(string path, Func<string> missing, Func<string, string> unreadable)
    {
        if (!File.Exists(path))
        {
            return Failed(missing());
        }

        try
        {
            using var stream = File.OpenRead(path);
            return Read(XlsxPackage.Read(stream));
        }
        catch (Exception ex) when (IsReadFailure(ex))
        {
            return Failed(unreadable(ex.Message));
        }
    }

    private static bool IsReadFailure(Exception ex) =>
        ex is InvalidDataException or XmlException or IOException or UnauthorizedAccessException;

    private static StockWorkbookReadResult Failed(string message) =>
        new(StockWorkbook.Empty, [new StockWorkbookProblem(null, null, message)]);

    private static List<decimal> Sorted(IEnumerable<decimal> values) => values.Distinct().Order().ToList();

    private static Dictionary<PrintUnits, List<decimal>> EmptyLists() =>
        new() { [PrintUnits.Metric] = [], [PrintUnits.Imperial] = [] };

    /// <summary>The columns of a tab by header text, or null (with a problem) when the tab or a header is missing.</summary>
    private static (XlsxSheet Sheet, Dictionary<string, int> Columns)? OpenTab(
        XlsxPackage package, string tab, IReadOnlyList<string> headers, List<StockWorkbookProblem> problems)
    {
        var sheet = package.Sheet(tab);
        if (sheet is null)
        {
            problems.Add(new StockWorkbookProblem(null, null, $"The stock workbook has no \"{tab}\" tab; its rows are not available."));
            return null;
        }

        var columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var column = 1; column <= sheet.LastColumn; column++)
        {
            var text = sheet.Text(HeaderRow, column);
            if (text.Length > 0)
            {
                columns.TryAdd(text, column);
            }
        }

        var missing = headers.Where(h => !columns.ContainsKey(h)).ToList();
        if (missing.Count > 0)
        {
            var names = string.Join(", ", missing.Select(h => $"\"{h}\""));
            var noun = missing.Count == 1 ? "column" : "columns";
            problems.Add(new StockWorkbookProblem(
                tab,
                HeaderRow,
                $"The {tab} tab has no {names} {noun}. Row {HeaderRow} should read: {string.Join(", ", headers)}; tab left out."));
            return null;
        }

        return (sheet, columns);
    }

    private static Dictionary<PrintUnits, List<decimal>> ReadList(
        XlsxPackage package, string tab, string sizeHeader, List<StockWorkbookProblem> problems)
    {
        var result = EmptyLists();
        if (OpenTab(package, tab, [sizeHeader, UnitsColumn], problems) is not { } open)
        {
            return result;
        }

        var (sheet, columns) = open;

        for (var row = HeaderRow + 1; row <= sheet.LastRow; row++)
        {
            if (sheet.IsRowBlank(row))
            {
                continue;
            }

            var rowProblems = new List<string>();
            var size = ReadPositive(sheet.Text(row, columns[sizeHeader]), sizeHeader, rowProblems);
            var units = ReadUnits(sheet.Text(row, columns[UnitsColumn]), rowProblems);
            if (rowProblems.Count == 0)
            {
                result[units!.Value].Add(size!.Value);
            }
            else
            {
                problems.AddRange(rowProblems.Select(p => new StockWorkbookProblem(tab, row, $"{p}; row left out.")));
            }
        }

        return result;
    }

    private static Dictionary<PrintUnits, (decimal Allowance, decimal Step)> ReadSettings(
        XlsxPackage package, List<StockWorkbookProblem> problems)
    {
        var result = new Dictionary<PrintUnits, (decimal, decimal)>();
        if (OpenTab(package, SettingsTab, [UnitsColumn, AllowanceColumn, StepColumn], problems) is not { } open)
        {
            return result;
        }

        var (sheet, columns) = open;

        for (var row = HeaderRow + 1; row <= sheet.LastRow; row++)
        {
            if (sheet.IsRowBlank(row))
            {
                continue;
            }

            var rowProblems = new List<string>();
            var units = ReadUnits(sheet.Text(row, columns[UnitsColumn]), rowProblems);
            var allowanceText = sheet.Text(row, columns[AllowanceColumn]);
            decimal? allowance = null;
            if (HoleWorkbookReader.TryParseNumber(allowanceText, out var a) && a >= 0)
            {
                allowance = a;
            }
            else
            {
                rowProblems.Add($"Allowance \"{allowanceText}\" is not a number of zero or more");
            }

            var step = ReadPositive(sheet.Text(row, columns[StepColumn]), StepColumn, rowProblems);
            if (rowProblems.Count > 0)
            {
                problems.AddRange(rowProblems.Select(p => new StockWorkbookProblem(SettingsTab, row, $"{p}; row left out, the default is used.")));
            }
            else if (!result.TryAdd(units!.Value, (allowance!.Value, step!.Value)))
            {
                problems.Add(new StockWorkbookProblem(SettingsTab, row, $"{units} is already set on an earlier row; row left out."));
            }
        }

        return result;
    }

    private static decimal? ReadPositive(string text, string header, List<string> rowProblems)
    {
        if (HoleWorkbookReader.TryParseNumber(text, out var value) && value > 0)
        {
            return value;
        }

        rowProblems.Add(text.Length == 0 ? $"{header} is blank" : $"{header} \"{text}\" is not a number above zero");
        return null;
    }

    private static PrintUnits? ReadUnits(string text, List<string> rowProblems)
    {
        if (text.Equals("Metric", StringComparison.OrdinalIgnoreCase) || text.Equals("mm", StringComparison.OrdinalIgnoreCase))
        {
            return PrintUnits.Metric;
        }

        if (text.Equals("Imperial", StringComparison.OrdinalIgnoreCase) || text.Equals("in", StringComparison.OrdinalIgnoreCase))
        {
            return PrintUnits.Imperial;
        }

        rowProblems.Add(text.Length == 0 ? "Units is blank" : $"Units \"{text}\" is not Metric or Imperial");
        return null;
    }
}
