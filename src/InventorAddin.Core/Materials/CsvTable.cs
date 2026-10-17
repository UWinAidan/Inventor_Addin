using System.Text;

namespace InventorAddin.Core.Materials;

/// <summary>
/// One CSV record: its fields as written (not trimmed) and the 1-based line of the file it starts on.
/// <see cref="Unterminated"/> is true when the record ends inside a quoted value with no closing quote.
/// </summary>
public sealed record CsvRow(int Line, IReadOnlyList<string> Fields, bool Unterminated = false)
{
    /// <summary>The field at <paramref name="index"/>, or <c>""</c> when the row is shorter than that.</summary>
    public string this[int index] => index >= 0 && index < Fields.Count ? Fields[index] : "";

    /// <summary>True when every field is blank or whitespace, as Excel writes an empty row (<c>,,,,</c>).</summary>
    public bool IsBlank => !Unterminated && Fields.All(string.IsNullOrWhiteSpace);
}

/// <summary>
/// A CSV file read into a header row and data rows. Used for files written by Excel and read leniently by the
/// add-in, such as the finish list (spec 03). Reading only.
/// </summary>
public sealed class CsvTable
{
    private CsvTable(CsvRow? header, IReadOnlyList<CsvRow> rows)
    {
        Header = header;
        Rows = rows;
    }

    /// <summary>The first non-blank record, or null when the text has none.</summary>
    public CsvRow? Header { get; }

    /// <summary>Every record after the header, in file order, blank lines left out.</summary>
    public IReadOnlyList<CsvRow> Rows { get; }

    /// <summary>
    /// Reads RFC 4180 CSV text: fields separated by commas; a field that starts with a double quote is quoted and
    /// may hold commas, line breaks and doubled quotes (<c>""</c> for one quote). Lines end in CRLF, LF or CR.
    /// A UTF-8 byte order mark at the start is ignored. Empty lines are skipped. A quote inside an unquoted value,
    /// or text after a closing quote, is kept as written.
    /// </summary>
    public static CsvTable Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var records = Split(text)
            .Where(r => !(r.Fields.Count == 1 && r.Fields[0].Length == 0 && !r.Unterminated))
            .ToList();

        return records.Count == 0
            ? new CsvTable(null, [])
            : new CsvTable(records[0], records.Skip(1).ToList().AsReadOnly());
    }

    /// <summary>Splits CSV text into records with a small state machine over the characters.</summary>
    private static List<CsvRow> Split(string text)
    {
        var records = new List<CsvRow>();
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
                    records.Add(new CsvRow(recordLine, fields.AsReadOnly()));
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
                    field.Append(c);
                    fieldStarted = true;
                    recordHasData = true;
                    break;
            }
        }

        if (inQuotes || recordHasData)
        {
            fields.Add(field.ToString());
            records.Add(new CsvRow(recordLine, fields.AsReadOnly(), Unterminated: inQuotes));
        }

        return records;
    }
}
