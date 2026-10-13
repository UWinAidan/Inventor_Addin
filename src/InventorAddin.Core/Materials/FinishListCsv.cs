using System.Globalization;

namespace InventorAddin.Core.Materials;

/// <summary>
/// One thing wrong with the finish list. <see cref="Line"/> is the 1-based line of the file, or null when the
/// problem is with the file as a whole (an empty file).
/// </summary>
public sealed record FinishListProblem(int? Line, string Message)
{
    public override string ToString() => Line is { } line ? $"Line {line}: {Message}" : Message;
}

/// <summary>What <see cref="FinishListCsv.Parse"/> found: the finishes that loaded, in file order, and the problems.</summary>
public sealed record FinishListReadResult(IReadOnlyList<Finish> Finishes, IReadOnlyList<FinishListProblem> Problems)
{
    public bool HasProblems => Problems.Count > 0;
}

/// <summary>
/// Reads the finish list, <c>libraries/finishes.csv</c> (spec 03, "The finish list"). The file is edited in Excel
/// and never written by the add-in, so it is read leniently: columns by header name in any order, extra columns
/// ignored, and a bad row left out with a problem while the rest load.
/// </summary>
public static class FinishListCsv
{
    public const string CodeColumn = "Code";
    public const string NameColumn = "Name";
    public const string KindColumn = "Kind";
    public const string AppliesToColumn = "AppliesTo";
    public const string AppearanceColumn = "Appearance";
    public const string ThicknessMinColumn = "ThicknessMin_mm";
    public const string ThicknessMaxColumn = "ThicknessMax_mm";
    public const string DimensionsAfterFinishColumn = "DimensionsAfterFinish";
    public const string PrintNoteColumn = "PrintNote";

    /// <summary>The Appearance cell that keeps the material's appearance (compared ignoring case).</summary>
    public const string NoChangeAppearance = "(no change)";

    /// <summary>The columns every finish list must have, in the order the starter file uses.</summary>
    public static IReadOnlyList<string> Columns { get; } =
    [
        CodeColumn, NameColumn, KindColumn, AppliesToColumn, AppearanceColumn,
        ThicknessMinColumn, ThicknessMaxColumn, DimensionsAfterFinishColumn, PrintNoteColumn,
    ];

    /// <summary>The header line of the starter file, for messages.</summary>
    public static string Header { get; } = string.Join(',', Columns);

    /// <summary>
    /// Reads finish list CSV text (see <see cref="CsvTable.Parse"/> for the CSV rules). Every column in
    /// <see cref="Columns"/> is required; a missing one makes the whole file invalid with one problem naming the
    /// missing columns and no finishes. Rows whose cells are all blank are skipped. A row is left out, with a problem
    /// naming its line for each thing wrong, when: Code or Name is blank; Code was used on an earlier row (ignoring
    /// case); Kind is not a <see cref="FinishKind"/> (ignoring case and spaces); a thickness is not a non-negative
    /// number in invariant culture, or the minimum is greater than the maximum; DimensionsAfterFinish is not Yes or No
    /// (ignoring case); or a quoted value has no closing quote. Missing trailing cells read as blank.
    /// </summary>
    public static FinishListReadResult Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var finishes = new List<Finish>();
        var problems = new List<FinishListProblem>();

        var table = CsvTable.Parse(text);
        if (table.Header is not { } header)
        {
            problems.Add(new FinishListProblem(null, $"The finish list is empty. The first line should be: {Header}"));
            return Result(finishes, problems);
        }

        var columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < header.Fields.Count; i++)
        {
            columns.TryAdd(header.Fields[i].Trim(), i);
        }

        var missing = Columns.Where(c => !columns.ContainsKey(c)).ToList();
        if (missing.Count > 0)
        {
            var names = string.Join(", ", missing.Select(c => $"\"{c}\""));
            var noun = missing.Count == 1 ? "column" : "columns";
            problems.Add(new FinishListProblem(
                header.Line,
                $"The finish list has no {names} {noun}. The first line should be: {Header}"));
            return Result(finishes, problems);
        }

        var codesSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in table.Rows)
        {
            if (row.IsBlank)
            {
                continue;
            }

            var rowProblems = new List<string>();
            var finish = ReadRow(row, columns, codesSeen, rowProblems);
            if (finish is not null)
            {
                finishes.Add(finish);
            }
            else
            {
                problems.AddRange(rowProblems.Select(p => new FinishListProblem(row.Line, $"{p}; row left out.")));
            }
        }

        return Result(finishes, problems);
    }

    private static FinishListReadResult Result(List<Finish> finishes, List<FinishListProblem> problems) =>
        new(finishes.AsReadOnly(), problems.AsReadOnly());

    private static Finish? ReadRow(
        CsvRow row,
        Dictionary<string, int> columns,
        HashSet<string> codesSeen,
        List<string> problems)
    {
        if (row.Unterminated)
        {
            problems.Add("a quoted value has no closing quote");
            return null;
        }

        string Cell(string column) => row[columns[column]].Trim();

        var code = Cell(CodeColumn);
        if (code.Length == 0)
        {
            problems.Add("Code is blank");
        }
        else if (!codesSeen.Add(code))
        {
            problems.Add($"Code \"{code}\" is already used on an earlier row");
        }

        var name = Cell(NameColumn);
        if (name.Length == 0)
        {
            problems.Add("Name is blank");
        }

        var kindText = Cell(KindColumn);
        if (!FinishKinds.TryParse(kindText, out var kind))
        {
            problems.Add(
                $"Kind \"{kindText}\" is not one of Plating, Conversion, Anodize, Heat treat or Paint");
        }

        var minText = Cell(ThicknessMinColumn);
        var maxText = Cell(ThicknessMaxColumn);
        var minOk = TryParseThickness(minText, out var min);
        var maxOk = TryParseThickness(maxText, out var max);
        if (!minOk)
        {
            problems.Add($"{ThicknessMinColumn} \"{minText}\" is not a number of 0 or more, for example 0.013");
        }

        if (!maxOk)
        {
            problems.Add($"{ThicknessMaxColumn} \"{maxText}\" is not a number of 0 or more, for example 0.025");
        }

        if (minOk && maxOk && min > max)
        {
            problems.Add($"{ThicknessMinColumn} {minText} is greater than {ThicknessMaxColumn} {maxText}");
        }

        var dimensionsText = Cell(DimensionsAfterFinishColumn);
        var dimensionsAfterFinish = false;
        if (string.Equals(dimensionsText, "Yes", StringComparison.OrdinalIgnoreCase))
        {
            dimensionsAfterFinish = true;
        }
        else if (!string.Equals(dimensionsText, "No", StringComparison.OrdinalIgnoreCase))
        {
            problems.Add($"{DimensionsAfterFinishColumn} \"{dimensionsText}\" is not Yes or No");
        }

        if (problems.Count > 0)
        {
            return null;
        }

        var appliesTo = Cell(AppliesToColumn)
            .Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        var appearance = Cell(AppearanceColumn);
        string? appearanceName =
            appearance.Length == 0 || string.Equals(appearance, NoChangeAppearance, StringComparison.OrdinalIgnoreCase)
                ? null
                : appearance;

        return new Finish(
            code,
            name,
            kind,
            Array.AsReadOnly(appliesTo),
            appearanceName,
            minText,
            maxText,
            dimensionsAfterFinish,
            Cell(PrintNoteColumn));
    }

    /// <summary>
    /// Reads a thickness in mm: digits with an optional <c>.</c> decimal point, invariant culture. No sign, group
    /// separator or exponent, so a negative value fails.
    /// </summary>
    internal static bool TryParseThickness(string text, out decimal value) =>
        decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value);
}
