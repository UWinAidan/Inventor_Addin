using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace InventorAddin.Core.Holes;

/// <summary>What kind of value a cell holds, from its <c>t</c> attribute.</summary>
public enum XlsxCellKind
{
    /// <summary>A number (no <c>t</c>, or <c>t="n"</c>). Dates are numbers too.</summary>
    Number,

    /// <summary>A shared string, an inline string, or the text result of a formula.</summary>
    Text,

    /// <summary>A boolean; <see cref="XlsxCell.Text"/> is <c>TRUE</c> or <c>FALSE</c>.</summary>
    Boolean,

    /// <summary>An error value such as <c>#DIV/0!</c>.</summary>
    Error,
}

/// <summary>
/// One cell's cached value: the value Excel (or LibreOffice) last showed, so a formula cell gives its result.
/// <see cref="Text"/> is the value as written in the file (a number in invariant culture, for example <c>6.4</c>).
/// </summary>
public sealed record XlsxCell(XlsxCellKind Kind, string Text)
{
    public bool IsBlank => string.IsNullOrWhiteSpace(Text);
}

/// <summary>One worksheet: its tab name and its cells, found by 1-based row and column.</summary>
public sealed class XlsxSheet
{
    private readonly Dictionary<(int Row, int Column), XlsxCell> _cells;

    internal XlsxSheet(string name, Dictionary<(int Row, int Column), XlsxCell> cells)
    {
        Name = name;
        _cells = cells;
        LastRow = cells.Count == 0 ? 0 : cells.Keys.Max(k => k.Row);
        LastColumn = cells.Count == 0 ? 0 : cells.Keys.Max(k => k.Column);
    }

    /// <summary>The tab name as the workbook writes it.</summary>
    public string Name { get; }

    /// <summary>The last row that holds a cell with a value, or 0 for an empty sheet.</summary>
    public int LastRow { get; }

    /// <summary>The last column that holds a cell with a value, or 0 for an empty sheet.</summary>
    public int LastColumn { get; }

    /// <summary>The cell at a 1-based row and column, or null when it is missing or empty.</summary>
    public XlsxCell? Cell(int row, int column) => _cells.GetValueOrDefault((row, column));

    /// <summary>The cell at an A1 reference such as <c>B4</c>, or null when it is missing or empty.</summary>
    public XlsxCell? Cell(string reference) =>
        XlsxPackage.TryParseReference(reference, out var row, out var column) ? Cell(row, column) : null;

    /// <summary>The cell's text, trimmed, or <c>""</c> for a missing cell.</summary>
    public string Text(int row, int column) => Cell(row, column)?.Text.Trim() ?? "";

    /// <summary>True when every cell of the row is missing or blank.</summary>
    public bool IsRowBlank(int row) =>
        !_cells.Any(kv => kv.Key.Row == row && !kv.Value.IsBlank);
}

/// <summary>
/// Reads an <c>.xlsx</c> package (Office Open XML spreadsheet) with only <see cref="System.IO.Compression"/> and
/// <see cref="System.Xml"/>, no Excel. Sheets are found by tab name through <c>xl/workbook.xml</c> and its
/// relationships. Cells are read from their cached values; formulas are never recalculated. Reading only.
/// </summary>
public sealed class XlsxPackage
{
    private const string MainNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private const string RelNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private const string PackageRelNs = "http://schemas.openxmlformats.org/package/2006/relationships";

    private static readonly XNamespace M = MainNs;
    private static readonly XNamespace R = RelNs;
    private static readonly XNamespace P = PackageRelNs;

    private readonly Dictionary<string, XlsxSheet> _sheets;

    private XlsxPackage(IReadOnlyList<string> sheetNames, Dictionary<string, XlsxSheet> sheets)
    {
        SheetNames = sheetNames;
        _sheets = sheets;
    }

    /// <summary>The tab names in workbook order.</summary>
    public IReadOnlyList<string> SheetNames { get; }

    /// <summary>The sheet with this tab name, matched ignoring case and surrounding spaces, or null.</summary>
    public XlsxSheet? Sheet(string name) => _sheets.GetValueOrDefault(name.Trim());

    /// <summary>
    /// Reads a whole package from a stream. Throws <see cref="InvalidDataException"/> when the stream is not a zip
    /// package or a required part is missing, and <see cref="XmlException"/> when a part is not valid XML.
    /// </summary>
    public static XlsxPackage Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);

        var workbookPath = FindWorkbookPath(zip);
        var workbook = LoadXml(zip, workbookPath)
            ?? throw new InvalidDataException($"The package has no workbook part ({workbookPath}).");
        var workbookRels = LoadRelationships(zip, workbookPath);

        var sharedStrings = new List<string>();
        var sharedPath = workbookRels.Values
            .Where(r => r.Type.EndsWith("/sharedStrings", StringComparison.Ordinal))
            .Select(r => r.Target)
            .FirstOrDefault() ?? ResolveTarget(workbookPath, "sharedStrings.xml");
        if (LoadXml(zip, sharedPath) is { } shared)
        {
            foreach (var si in shared.Root!.Elements(M + "si"))
            {
                sharedStrings.Add(StringItemText(si));
            }
        }

        var names = new List<string>();
        var sheets = new Dictionary<string, XlsxSheet>(StringComparer.OrdinalIgnoreCase);
        var sheetsElement = workbook.Root!.Element(M + "sheets");
        foreach (var sheet in sheetsElement?.Elements(M + "sheet") ?? [])
        {
            var name = (string?)sheet.Attribute("name") ?? "";
            var id = (string?)sheet.Attribute(R + "id") ?? "";
            names.Add(name);
            if (!workbookRels.TryGetValue(id, out var rel))
            {
                throw new InvalidDataException($"The workbook's sheet \"{name}\" has no relationship \"{id}\".");
            }

            var part = LoadXml(zip, rel.Target)
                ?? throw new InvalidDataException($"The workbook's sheet \"{name}\" has no part ({rel.Target}).");
            sheets.TryAdd(name.Trim(), new XlsxSheet(name, ReadCells(part, sharedStrings)));
        }

        return new XlsxPackage(names.AsReadOnly(), sheets);
    }

    /// <summary>
    /// Parses an A1 reference (<c>B4</c>, <c>AA10</c>; <c>$</c> signs ignored) into a 1-based row and column.
    /// </summary>
    public static bool TryParseReference(string reference, out int row, out int column)
    {
        row = 0;
        column = 0;
        var i = 0;
        var text = reference.Replace("$", "", StringComparison.Ordinal).Trim();
        while (i < text.Length && char.IsAsciiLetter(text[i]))
        {
            column = checked((column * 26) + (char.ToUpperInvariant(text[i]) - 'A' + 1));
            i++;
        }

        if (i == 0 || i == text.Length)
        {
            return false;
        }

        return int.TryParse(text.AsSpan(i), NumberStyles.None, CultureInfo.InvariantCulture, out row) && row > 0;
    }

    private static string FindWorkbookPath(ZipArchive zip)
    {
        var rels = LoadXml(zip, "_rels/.rels");
        var target = rels?.Root!
            .Elements(P + "Relationship")
            .Where(r => ((string?)r.Attribute("Type") ?? "").EndsWith("/officeDocument", StringComparison.Ordinal))
            .Select(r => (string?)r.Attribute("Target"))
            .FirstOrDefault();
        return target is null ? "xl/workbook.xml" : ResolveTarget("", target);
    }

    private sealed record Relationship(string Type, string Target);

    /// <summary>The relationships of a part, keyed by id, with each target resolved to a package path.</summary>
    private static Dictionary<string, Relationship> LoadRelationships(ZipArchive zip, string partPath)
    {
        var slash = partPath.LastIndexOf('/');
        var folder = slash < 0 ? "" : partPath[..(slash + 1)];
        var file = partPath[(slash + 1)..];
        var result = new Dictionary<string, Relationship>(StringComparer.Ordinal);
        if (LoadXml(zip, $"{folder}_rels/{file}.rels") is not { } rels)
        {
            return result;
        }

        foreach (var rel in rels.Root!.Elements(P + "Relationship"))
        {
            var id = (string?)rel.Attribute("Id");
            var target = (string?)rel.Attribute("Target");
            if (id is null || target is null || (string?)rel.Attribute("TargetMode") == "External")
            {
                continue;
            }

            result.TryAdd(id, new Relationship((string?)rel.Attribute("Type") ?? "", ResolveTarget(partPath, target)));
        }

        return result;
    }

    /// <summary>
    /// Resolves a relationship target against the part that owns it: <c>/xl/x.xml</c> is from the package root,
    /// <c>worksheets/sheet1.xml</c> is beside the part, and <c>..</c> steps up.
    /// </summary>
    private static string ResolveTarget(string sourcePart, string target)
    {
        var parts = new List<string>();
        if (!target.StartsWith('/'))
        {
            var slash = sourcePart.LastIndexOf('/');
            if (slash > 0)
            {
                parts.AddRange(sourcePart[..slash].Split('/', StringSplitOptions.RemoveEmptyEntries));
            }
        }

        foreach (var segment in target.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == "..")
            {
                if (parts.Count > 0)
                {
                    parts.RemoveAt(parts.Count - 1);
                }
            }
            else if (segment != ".")
            {
                parts.Add(segment);
            }
        }

        return string.Join('/', parts);
    }

    private static XDocument? LoadXml(ZipArchive zip, string path)
    {
        var entry = zip.GetEntry(path)
            ?? zip.Entries.FirstOrDefault(e => string.Equals(e.FullName, path, StringComparison.OrdinalIgnoreCase));
        if (entry is null)
        {
            return null;
        }

        using var stream = entry.Open();
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
        using var reader = XmlReader.Create(stream, settings);
        return XDocument.Load(reader);
    }

    /// <summary>The text of a shared or inline string item: its <c>t</c>, or its rich text runs joined. Phonetic runs are left out.</summary>
    private static string StringItemText(XElement item)
    {
        if (item.Element(M + "t") is { } plain)
        {
            return plain.Value;
        }

        var text = new StringBuilder();
        foreach (var run in item.Elements(M + "r"))
        {
            text.Append(run.Element(M + "t")?.Value);
        }

        return text.ToString();
    }

    private static Dictionary<(int Row, int Column), XlsxCell> ReadCells(XDocument sheet, List<string> sharedStrings)
    {
        var cells = new Dictionary<(int Row, int Column), XlsxCell>();
        var sheetData = sheet.Root!.Element(M + "sheetData");
        if (sheetData is null)
        {
            return cells;
        }

        var rowNumber = 0;
        foreach (var row in sheetData.Elements(M + "row"))
        {
            rowNumber = int.TryParse((string?)row.Attribute("r"), NumberStyles.None, CultureInfo.InvariantCulture, out var r)
                ? r
                : rowNumber + 1;

            var columnNumber = 0;
            foreach (var c in row.Elements(M + "c"))
            {
                var reference = (string?)c.Attribute("r");
                if (reference is not null && TryParseReference(reference, out var refRow, out var refColumn))
                {
                    rowNumber = refRow;
                    columnNumber = refColumn;
                }
                else
                {
                    columnNumber++;
                }

                if (ReadCell(c, sharedStrings) is { } cell)
                {
                    cells[(rowNumber, columnNumber)] = cell;
                }
            }
        }

        return cells;
    }

    private static XlsxCell? ReadCell(XElement c, List<string> sharedStrings)
    {
        var type = (string?)c.Attribute("t") ?? "n";
        var value = c.Element(M + "v")?.Value;

        switch (type)
        {
            case "s":
                if (value is null
                    || !int.TryParse(value.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var index)
                    || index >= sharedStrings.Count)
                {
                    throw new InvalidDataException($"A cell refers to shared string \"{value}\", which does not exist.");
                }

                return new XlsxCell(XlsxCellKind.Text, sharedStrings[index]);

            case "inlineStr":
                return c.Element(M + "is") is { } inline ? new XlsxCell(XlsxCellKind.Text, StringItemText(inline)) : null;

            case "b":
                return value is null ? null : new XlsxCell(XlsxCellKind.Boolean, value.Trim() == "1" ? "TRUE" : "FALSE");

            case "e":
                return value is null ? null : new XlsxCell(XlsxCellKind.Error, value);

            case "str":
            case "d":
                return value is null ? null : new XlsxCell(XlsxCellKind.Text, value);

            default:
                return value is null ? null : new XlsxCell(XlsxCellKind.Number, NormaliseNumber(value.Trim()));
        }
    }

    /// <summary>
    /// Excel stores a number cell with up to 17 significant digits, not always the shortest form, so
    /// a typed 6.6 is written <c>6.5999999999999996</c>. Rounding through a double to 15 significant digits (what
    /// Excel itself shows) gives back <c>6.6</c>. Text that is not a number is kept as written.
    /// </summary>
    internal static string NormaliseNumber(string text) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && double.IsFinite(value)
            ? value.ToString("G15", CultureInfo.InvariantCulture)
            : text;
}
