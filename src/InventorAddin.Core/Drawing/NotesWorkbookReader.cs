using System.Xml;
using InventorAddin.Core.Holes;
using InventorAddin.Core.Materials;

namespace InventorAddin.Core.Drawing;

/// <summary>
/// Reads the notes workbook, <c>libraries/AWB_Notes.xlsx</c> (spec 05, "Added for M5"), without Excel, as
/// <see cref="HoleWorkbookReader"/> reads the hole workbook: tabs by name, columns by header text in any order, extra
/// columns ignored, and a bad row left out with a problem naming its tab and row while the rest load. The "How to
/// edit" tab is for people and is not read. A blank Placement means <see cref="NotePlacement.List"/>.
/// </summary>
public static class NotesWorkbookReader
{
    public const string MaterialTab = "Material";
    public const string FinishTab = "Finish";
    public const string PatternColumn = "Pattern";
    public const string NoteColumn = "Note";
    public const string PlacementColumn = "Placement";

    /// <summary>The row that holds the headers on the Material and Finish tabs (title in row 1, note in row 2).</summary>
    public const int HeaderRow = 4;

    /// <summary>The headers each rules tab must have.</summary>
    public static IReadOnlyList<string> Headers { get; } = [PatternColumn, NoteColumn, PlacementColumn];

    /// <summary>Reads the workbook at <paramref name="path"/>; a missing or unreadable file gives one problem and an empty workbook.</summary>
    public static NotesWorkbookReadResult Read(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        return ReadFile(
            path,
            () => $"The notes workbook was not found at {path}.",
            reason => $"Could not read the notes workbook at {path}. Check that it is an .xlsx file. ({reason})");
    }

    /// <summary>Reads the workbook <see cref="LibraryPaths.NotesWorkbook"/> resolved, with its own messages for a missing or unreadable file.</summary>
    public static NotesWorkbookReadResult Read(LibraryPath workbook)
    {
        ArgumentNullException.ThrowIfNull(workbook);

        return ReadFile(
            workbook.Path,
            () => NotesWorkbookMessages.Missing(workbook),
            reason => NotesWorkbookMessages.Unreadable(workbook, reason));
    }

    /// <summary>Reads a workbook from a stream.</summary>
    public static NotesWorkbookReadResult Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        try
        {
            return Read(XlsxPackage.Read(stream));
        }
        catch (Exception ex) when (IsReadFailure(ex))
        {
            return Failed($"Could not read the notes workbook. Check that it is an .xlsx file. ({ex.Message})");
        }
    }

    /// <summary>Reads both rules tabs of a package that has already been opened.</summary>
    public static NotesWorkbookReadResult Read(XlsxPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);

        var problems = new List<NotesWorkbookProblem>();
        var workbook = new NotesWorkbook(
            ReadTab(package, MaterialTab, problems),
            ReadTab(package, FinishTab, problems));
        return new NotesWorkbookReadResult(workbook, problems.AsReadOnly());
    }

    private static NotesWorkbookReadResult ReadFile(string path, Func<string> missing, Func<string, string> unreadable)
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

    private static NotesWorkbookReadResult Failed(string message) =>
        new(NotesWorkbook.Empty, [new NotesWorkbookProblem(null, null, message)]);

    private static IReadOnlyList<NoteRule> ReadTab(XlsxPackage package, string tab, List<NotesWorkbookProblem> problems)
    {
        var sheet = package.Sheet(tab);
        if (sheet is null)
        {
            problems.Add(new NotesWorkbookProblem(null, null, $"The notes workbook has no \"{tab}\" tab; its rows are not available."));
            return [];
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

        var missing = Headers.Where(h => !columns.ContainsKey(h)).ToList();
        if (missing.Count > 0)
        {
            var names = string.Join(", ", missing.Select(h => $"\"{h}\""));
            var noun = missing.Count == 1 ? "column" : "columns";
            problems.Add(new NotesWorkbookProblem(
                tab,
                HeaderRow,
                $"The {tab} tab has no {names} {noun}. Row {HeaderRow} should read: {string.Join(", ", Headers)}; tab left out."));
            return [];
        }

        var rules = new List<NoteRule>();
        for (var row = HeaderRow + 1; row <= sheet.LastRow; row++)
        {
            if (sheet.IsRowBlank(row))
            {
                continue;
            }

            var pattern = sheet.Text(row, columns[PatternColumn]);
            var note = sheet.Text(row, columns[NoteColumn]);
            var placementText = sheet.Text(row, columns[PlacementColumn]);
            var rowProblems = new List<string>();

            if (pattern.Length == 0)
            {
                rowProblems.Add("Pattern is blank");
            }

            if (note.Length == 0)
            {
                rowProblems.Add("Note is blank");
            }

            var placement = NotePlacement.List;
            if (placementText.Length > 0 && !TryParsePlacement(placementText, out placement))
            {
                rowProblems.Add($"Placement \"{placementText}\" is not List or Bottom");
            }

            if (rowProblems.Count == 0)
            {
                rules.Add(new NoteRule(pattern, note, placement));
            }
            else
            {
                problems.AddRange(rowProblems.Select(p => new NotesWorkbookProblem(tab, row, $"{p}; row left out.")));
            }
        }

        return rules.AsReadOnly();
    }

    private static bool TryParsePlacement(string text, out NotePlacement placement)
    {
        // Enum.TryParse would also take numbers ("1"); only the two names count.
        if (string.Equals(text, nameof(NotePlacement.List), StringComparison.OrdinalIgnoreCase))
        {
            placement = NotePlacement.List;
            return true;
        }

        if (string.Equals(text, nameof(NotePlacement.Bottom), StringComparison.OrdinalIgnoreCase))
        {
            placement = NotePlacement.Bottom;
            return true;
        }

        placement = NotePlacement.List;
        return false;
    }
}
