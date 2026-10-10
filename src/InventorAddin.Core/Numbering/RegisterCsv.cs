using System.Globalization;
using System.Text;
using InventorAddin.Core.Models;

namespace InventorAddin.Core.Numbering;

/// <summary>What <see cref="RegisterCsv.Read"/> found: the rows it could read, in file order, and why it skipped others.</summary>
public sealed record RegisterReadResult(IReadOnlyList<RegisterEntry> Entries, IReadOnlyList<string> Warnings);

/// <summary>
/// The register file format (spec 02, "The register: Format"): CSV with a header row so Aidan can open it in Excel.
/// Text only; reading and writing the file is done elsewhere.
/// </summary>
public static class RegisterCsv
{
    public const string NumberColumn = "Number";
    public const string NameColumn = "Name";
    public const string PartTypeColumn = "PartType";
    public const string KindColumn = "Kind";
    public const string FilePathColumn = "FilePath";
    public const string AssignedAtColumn = "AssignedAt";
    public const string AssignedByColumn = "AssignedBy";
    public const string ComputerColumn = "Computer";

    /// <summary>The columns in the order <see cref="Write"/> puts them.</summary>
    public static IReadOnlyList<string> Columns { get; } =
    [
        NumberColumn, NameColumn, PartTypeColumn, KindColumn, FilePathColumn, AssignedAtColumn, AssignedByColumn, ComputerColumn,
    ];

    /// <summary>The header row <see cref="Write"/> writes.</summary>
    public static string Header { get; } = string.Join(',', Columns);

    /// <summary>The line ending <see cref="Write"/> uses, as Excel on Windows does.</summary>
    public const string NewLine = "\r\n";

    /// <summary>How <c>AssignedAt</c> is written: UTC, ISO 8601, to the second, with <c>Z</c>.</summary>
    public const string DateFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";

    /// <summary>
    /// The CSV text for <paramref name="entries"/>: the header, then one line per entry in the order given, every line
    /// ending <see cref="NewLine"/>. A value holding a comma, a double quote or a line break is quoted, quotes doubled.
    /// </summary>
    public static string Write(IEnumerable<RegisterEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var text = new StringBuilder();
        text.Append(Header).Append(NewLine);
        foreach (var entry in entries)
        {
            text.Append(Escape(entry.Number.ToString())).Append(',')
                .Append(Escape(entry.Name)).Append(',')
                .Append(Escape(entry.PartType)).Append(',')
                .Append(Escape(entry.Kind.ToString())).Append(',')
                .Append(Escape(entry.FilePath)).Append(',')
                .Append(Escape(FormatDate(entry.AssignedAt))).Append(',')
                .Append(Escape(entry.AssignedBy)).Append(',')
                .Append(Escape(entry.Computer))
                .Append(NewLine);
        }

        return text.ToString();
    }

    /// <summary>
    /// Reads register CSV text. Accepts <c>\n</c> or <c>\r\n</c> line endings, a UTF-8 byte order mark, quoted fields
    /// (with doubled quotes and line breaks inside), and blank lines, which are skipped. Columns are found by header
    /// name, ignoring case and order; unknown columns are ignored. A row that cannot be read is skipped with a warning
    /// naming the line it starts on. Text with no rows at all reads as an empty register. Throws
    /// <see cref="FormatException"/> when the header has no <c>Number</c> column.
    /// </summary>
    /// <remarks>
    /// Only <c>Number</c> is required. A missing optional column, or a blank cell in one, reads as <c>""</c>,
    /// <see cref="DocumentKind.Unknown"/> or <see cref="DateTimeOffset.MinValue"/>; a missing column gives one warning.
    /// </remarks>
    public static RegisterReadResult Read(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var entries = new List<RegisterEntry>();
        var warnings = new List<string>();

        var records = Parse(text)
            .Where(r => !(r.Fields.Count == 1 && r.Fields[0].Length == 0 && !r.Unterminated))
            .ToList();
        if (records.Count == 0)
        {
            return new RegisterReadResult(entries, warnings);
        }

        var header = records[0];
        var columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < header.Fields.Count; i++)
        {
            columns.TryAdd(header.Fields[i].Trim(), i);
        }

        if (!columns.ContainsKey(NumberColumn))
        {
            throw new FormatException(
                $"The register has no \"{NumberColumn}\" column. The first line should be: {Header}");
        }

        foreach (var column in Columns)
        {
            if (!columns.ContainsKey(column))
            {
                warnings.Add($"The register has no \"{column}\" column; it reads as blank.");
            }
        }

        foreach (var record in records.Skip(1))
        {
            if (TryReadEntry(record, header.Fields.Count, columns, out var entry, out var problem))
            {
                entries.Add(entry);
            }
            else
            {
                warnings.Add($"Line {record.Line}: {problem}; row skipped.");
            }
        }

        return new RegisterReadResult(entries, warnings);
    }

    private static bool TryReadEntry(
        CsvRecord record,
        int headerCount,
        Dictionary<string, int> columns,
        out RegisterEntry entry,
        out string problem)
    {
        entry = null!;

        if (record.Unterminated)
        {
            problem = "a quoted value has no closing quote";
            return false;
        }

        if (record.Fields.Count != headerCount)
        {
            problem = $"{record.Fields.Count} values, but the header has {headerCount} columns";
            return false;
        }

        string Cell(string column) => columns.TryGetValue(column, out var index) ? record.Fields[index] : "";

        var numberText = Cell(NumberColumn);
        if (!PartNumber.TryParse(numberText, out var number))
        {
            problem = $"\"{numberText}\" is not a part number";
            return false;
        }

        var kindText = Cell(KindColumn);
        if (!TryParseKind(kindText, out var kind))
        {
            problem = $"\"{kindText}\" is not a kind of file";
            return false;
        }

        var dateText = Cell(AssignedAtColumn);
        if (!TryParseDate(dateText, out var assignedAt))
        {
            problem = $"\"{dateText}\" is not a date and time";
            return false;
        }

        entry = new RegisterEntry(
            number,
            Cell(NameColumn),
            Cell(PartTypeColumn),
            kind,
            Cell(FilePathColumn),
            assignedAt,
            Cell(AssignedByColumn),
            Cell(ComputerColumn));
        problem = "";
        return true;
    }

    private static bool TryParseKind(string text, out DocumentKind kind)
    {
        kind = DocumentKind.Unknown;
        var trimmed = text.Trim();
        if (trimmed.Length == 0)
        {
            return true;
        }

        // Match member names only: Enum.TryParse would also accept numbers such as "3".
        foreach (var value in Enum.GetValues<DocumentKind>())
        {
            if (string.Equals(value.ToString(), trimmed, StringComparison.OrdinalIgnoreCase))
            {
                kind = value;
                return true;
            }
        }

        return false;
    }

    private static bool TryParseDate(string text, out DateTimeOffset value)
    {
        value = DateTimeOffset.MinValue;
        var trimmed = text.Trim();
        if (trimmed.Length == 0)
        {
            return true;
        }

        if (DateTimeOffset.TryParse(
                trimmed,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed))
        {
            value = parsed.ToUniversalTime();
            return true;
        }

        return false;
    }

    private static string FormatDate(DateTimeOffset value) =>
        value.UtcDateTime.ToString(DateFormat, CultureInfo.InvariantCulture);

    private static string Escape(string value)
    {
        if (value.AsSpan().IndexOfAny(",\"\r\n") < 0)
        {
            return value;
        }

        return "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }

    /// <summary>One CSV record: its fields and the 1-based line it starts on.</summary>
    private sealed record CsvRecord(int Line, IReadOnlyList<string> Fields, bool Unterminated);

    /// <summary>Splits CSV text into records with a small state machine over the characters.</summary>
    private static List<CsvRecord> Parse(string text)
    {
        var records = new List<CsvRecord>();
        var fields = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        var recordHasData = false;
        var fieldStarted = false;
        var line = 1;
        var recordLine = 1;

        var start = text.Length > 0 && text[0] == '﻿' ? 1 : 0;
        for (var i = start; i < text.Length; i++)
        {
            var c = text[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    if (c == '\n' || (c == '\r' && !(i + 1 < text.Length && text[i + 1] == '\n')))
                    {
                        line++;
                    }

                    field.Append(c);
                }

                continue;
            }

            switch (c)
            {
                case ',':
                    fields.Add(field.ToString());
                    field.Clear();
                    fieldStarted = false;
                    recordHasData = true;
                    break;

                case '\r':
                case '\n':
                    if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                    {
                        i++;
                    }

                    fields.Add(field.ToString());
                    field.Clear();
                    fieldStarted = false;
                    records.Add(new CsvRecord(recordLine, fields, Unterminated: false));
                    fields = [];
                    recordHasData = false;
                    line++;
                    recordLine = line;
                    break;

                case '"' when !fieldStarted:
                    // A quote opens a quoted value only at the start of a field.
                    inQuotes = true;
                    fieldStarted = true;
                    recordHasData = true;
                    break;

                default:
                    // A quote inside an unquoted value, or text after a closing quote, is kept as it is.
                    field.Append(c);
                    fieldStarted = true;
                    recordHasData = true;
                    break;
            }
        }

        if (inQuotes || recordHasData)
        {
            fields.Add(field.ToString());
            records.Add(new CsvRecord(recordLine, fields, Unterminated: inQuotes));
        }

        return records;
    }
}
