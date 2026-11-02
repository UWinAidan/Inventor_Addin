using System.Globalization;
using System.IO.Compression;
using System.Security;
using System.Text;

namespace InventorAddin.Core.Tests.Holes;

/// <summary>A formula cell: the formula text and the value Excel last showed.</summary>
internal sealed record Formula(string Text, object Cached);

/// <summary>A number cell whose <c>v</c> is written exactly as given, as Excel writes 17 significant digits.</summary>
internal sealed record RawNumber(string Text);

/// <summary>A formula cell that shows an Excel error such as <c>#DIV/0!</c> (<c>t="e"</c>).</summary>
internal sealed record ErrorValue(string Text);

/// <summary>An inline string cell (<c>t="inlineStr"</c>), as openpyxl writes them.</summary>
internal sealed record Inline(string Text);

/// <summary>
/// Builds a small <c>.xlsx</c> package in memory for tests: a workbook, its relationships, shared strings and one
/// worksheet per tab. Rows are lists of cell values from column A; null leaves the cell out. Strings go to the
/// shared strings part, numbers are written in invariant culture, and booleans as <c>t="b"</c>.
/// </summary>
internal sealed class TestXlsx
{
    private const string MainNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private const string RelNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private const string PackageRelNs = "http://schemas.openxmlformats.org/package/2006/relationships";

    private readonly List<(string Name, string Xml)> _sheets = [];
    private readonly List<string> _shared = [];

    /// <summary>Adds a tab from rows starting at row 1.</summary>
    public TestXlsx Sheet(string name, IEnumerable<object?[]> rows)
    {
        var xml = new StringBuilder();
        xml.Append($"<worksheet xmlns=\"{MainNs}\"><sheetData>");
        var rowNumber = 0;
        foreach (var row in rows)
        {
            rowNumber++;
            if (row.Length == 0)
            {
                continue;
            }

            xml.Append($"<row r=\"{rowNumber}\">");
            for (var i = 0; i < row.Length; i++)
            {
                if (row[i] is { } value)
                {
                    xml.Append(CellXml($"{ColumnName(i + 1)}{rowNumber}", value));
                }
            }

            xml.Append("</row>");
        }

        xml.Append("</sheetData></worksheet>");
        _sheets.Add((name, xml.ToString()));
        return this;
    }

    /// <summary>Adds a tab whose worksheet XML is given as written.</summary>
    public TestXlsx RawSheet(string name, string worksheetXml)
    {
        _sheets.Add((name, worksheetXml));
        return this;
    }

    public MemoryStream Build()
    {
        var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            Write(zip, "_rels/.rels",
                $"<Relationships xmlns=\"{PackageRelNs}\"><Relationship Id=\"rId1\" Type=\"{RelNs}/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");

            var sheets = new StringBuilder();
            var rels = new StringBuilder();
            for (var i = 0; i < _sheets.Count; i++)
            {
                var id = $"rId{i + 10}";
                sheets.Append($"<sheet name=\"{SecurityElement.Escape(_sheets[i].Name)}\" sheetId=\"{i + 1}\" r:id=\"{id}\"/>");
                rels.Append($"<Relationship Id=\"{id}\" Type=\"{RelNs}/worksheet\" Target=\"worksheets/sheet{i + 1}.xml\"/>");
                Write(zip, $"xl/worksheets/sheet{i + 1}.xml", _sheets[i].Xml);
            }

            rels.Append($"<Relationship Id=\"rId2\" Type=\"{RelNs}/sharedStrings\" Target=\"sharedStrings.xml\"/>");
            Write(zip, "xl/workbook.xml",
                $"<workbook xmlns=\"{MainNs}\" xmlns:r=\"{RelNs}\"><sheets>{sheets}</sheets></workbook>");
            Write(zip, "xl/_rels/workbook.xml.rels", $"<Relationships xmlns=\"{PackageRelNs}\">{rels}</Relationships>");

            var shared = new StringBuilder();
            foreach (var s in _shared)
            {
                shared.Append($"<si><t xml:space=\"preserve\">{SecurityElement.Escape(s)}</t></si>");
            }

            Write(zip, "xl/sharedStrings.xml",
                $"<sst xmlns=\"{MainNs}\" count=\"{_shared.Count}\" uniqueCount=\"{_shared.Count}\">{shared}</sst>");
        }

        stream.Position = 0;
        return stream;
    }

    public static string ColumnName(int column)
    {
        var name = "";
        while (column > 0)
        {
            column--;
            name = (char)('A' + (column % 26)) + name;
            column /= 26;
        }

        return name;
    }

    private string CellXml(string reference, object value) => value switch
    {
        string s => $"<c r=\"{reference}\" t=\"s\"><v>{SharedIndex(s)}</v></c>",
        Inline inline => $"<c r=\"{reference}\" t=\"inlineStr\"><is><t>{SecurityElement.Escape(inline.Text)}</t></is></c>",
        RawNumber raw => $"<c r=\"{reference}\"><v>{raw.Text}</v></c>",
        ErrorValue error => $"<c r=\"{reference}\" t=\"e\"><f>1/0</f><v>{SecurityElement.Escape(error.Text)}</v></c>",
        bool b => $"<c r=\"{reference}\" t=\"b\"><v>{(b ? 1 : 0)}</v></c>",
        Formula f => f.Cached is string text
            ? $"<c r=\"{reference}\" t=\"str\"><f>{SecurityElement.Escape(f.Text)}</f><v>{SecurityElement.Escape(text)}</v></c>"
            : $"<c r=\"{reference}\"><f>{SecurityElement.Escape(f.Text)}</f><v>{Number(f.Cached)}</v></c>",
        _ => $"<c r=\"{reference}\"><v>{Number(value)}</v></c>",
    };

    private static string Number(object value) => Convert.ToString(value, CultureInfo.InvariantCulture)!;

    private int SharedIndex(string s)
    {
        var index = _shared.IndexOf(s);
        if (index < 0)
        {
            _shared.Add(s);
            index = _shared.Count - 1;
        }

        return index;
    }

    private static void Write(ZipArchive zip, string path, string content)
    {
        var entry = zip.CreateEntry(path);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }
}
