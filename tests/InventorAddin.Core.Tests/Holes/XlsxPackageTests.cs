using System.IO.Compression;
using System.Text;
using InventorAddin.Core.Holes;

namespace InventorAddin.Core.Tests.Holes;

public sealed class XlsxPackageTests
{
    private const string MainNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    private static XlsxPackage Read(TestXlsx xlsx)
    {
        using var stream = xlsx.Build();
        return XlsxPackage.Read(stream);
    }

    [Fact]
    public void FindsSheetsByTabName_InWorkbookOrder_IgnoringCase()
    {
        var package = Read(new TestXlsx()
            .Sheet("First", [["one"]])
            .Sheet("Second Tab", [["two"]]));

        Assert.Equal(["First", "Second Tab"], package.SheetNames);
        Assert.Equal("two", package.Sheet("second tab")!.Text(1, 1));
        Assert.Equal("one", package.Sheet(" FIRST ")!.Text(1, 1));
        Assert.Null(package.Sheet("Third"));
    }

    [Fact]
    public void ReadsSharedAndInlineStrings_NumbersAndBooleans()
    {
        var package = Read(new TestXlsx().Sheet("S", [["shared", new Inline("inline"), 6.4m, true, false]]));
        var sheet = package.Sheet("S")!;

        Assert.Equal(new XlsxCell(XlsxCellKind.Text, "shared"), sheet.Cell("A1"));
        Assert.Equal(new XlsxCell(XlsxCellKind.Text, "inline"), sheet.Cell("B1"));
        Assert.Equal(new XlsxCell(XlsxCellKind.Number, "6.4"), sheet.Cell("C1"));
        Assert.Equal(new XlsxCell(XlsxCellKind.Boolean, "TRUE"), sheet.Cell("D1"));
        Assert.Equal(new XlsxCell(XlsxCellKind.Boolean, "FALSE"), sheet.Cell("E1"));
    }

    [Fact]
    public void FormulaCells_GiveTheCachedValue()
    {
        var package = Read(new TestXlsx().Sheet("S", [[new Formula("ROUND(A2*1.5,3)", 9), new Formula("A2&\"x\"", "six")]]));
        var sheet = package.Sheet("S")!;

        Assert.Equal("9", sheet.Text(1, 1));
        Assert.Equal(new XlsxCell(XlsxCellKind.Text, "six"), sheet.Cell("B1"));
    }

    [Fact]
    public void BlankCellsInBetween_AreFoundByReference()
    {
        var package = Read(new TestXlsx().Sheet("S", [["a", null, null, "d"], [], ["x"]]));
        var sheet = package.Sheet("S")!;

        Assert.Equal("d", sheet.Text(1, 4));
        Assert.Null(sheet.Cell("B1"));
        Assert.True(sheet.IsRowBlank(2));
        Assert.Equal("x", sheet.Text(3, 1));
        Assert.Equal(3, sheet.LastRow);
        Assert.Equal(4, sheet.LastColumn);
    }

    [Fact]
    public void CellsWithoutReferences_FollowThePreviousCell()
    {
        var xml = $"<worksheet xmlns=\"{MainNs}\"><sheetData>"
            + "<row r=\"2\"><c r=\"B2\"><v>1</v></c><c><v>2</v></c></row>"
            + "<row><c><v>3</v></c></row>"
            + "</sheetData></worksheet>";
        var sheet = Read(new TestXlsx().RawSheet("S", xml)).Sheet("S")!;

        Assert.Equal("1", sheet.Text(2, 2));
        Assert.Equal("2", sheet.Text(2, 3));
        Assert.Equal("3", sheet.Text(3, 1));
    }

    [Fact]
    public void RichTextSharedStrings_AreJoined_AndErrorCellsKept()
    {
        var xml = $"<worksheet xmlns=\"{MainNs}\"><sheetData><row r=\"1\">"
            + "<c r=\"A1\" t=\"inlineStr\"><is><r><t>Hole </t></r><r><t>fit</t></r></is></c>"
            + "<c r=\"B1\" t=\"e\"><v>#DIV/0!</v></c>"
            + "</row></sheetData></worksheet>";
        var sheet = Read(new TestXlsx().RawSheet("S", xml)).Sheet("S")!;

        Assert.Equal("Hole fit", sheet.Text(1, 1));
        Assert.Equal(new XlsxCell(XlsxCellKind.Error, "#DIV/0!"), sheet.Cell("B1"));
    }

    [Theory]
    [InlineData("6.5999999999999996", "6.6")]
    [InlineData("0.45000000000000001", "0.45")]
    [InlineData("0.28499999999999998", "0.285")]
    [InlineData("6", "6")]
    [InlineData("1.0000000000000001E-5", "1E-05")]
    public void NumberCells_AreRoundedTo15SignificantDigits_AsExcelShowsThem(string stored, string expected)
    {
        var sheet = Read(new TestXlsx().Sheet("S", [[new RawNumber(stored)]])).Sheet("S")!;

        Assert.Equal(new XlsxCell(XlsxCellKind.Number, expected), sheet.Cell("A1"));
    }

    [Fact]
    public void TextCells_AreNotRounded()
    {
        var sheet = Read(new TestXlsx().Sheet("S", [["0.45000000000000001"]])).Sheet("S")!;

        Assert.Equal("0.45000000000000001", sheet.Text(1, 1));
    }

    [Theory]
    [InlineData("A1", 1, 1)]
    [InlineData("B4", 4, 2)]
    [InlineData("Z10", 10, 26)]
    [InlineData("AA3", 3, 27)]
    [InlineData("$C$7", 7, 3)]
    public void TryParseReference_ReadsRowAndColumn(string reference, int row, int column)
    {
        Assert.True(XlsxPackage.TryParseReference(reference, out var r, out var c));
        Assert.Equal((row, column), (r, c));
    }

    [Theory]
    [InlineData("")]
    [InlineData("A")]
    [InlineData("12")]
    [InlineData("A0")]
    public void TryParseReference_RefusesBadReferences(string reference) =>
        Assert.False(XlsxPackage.TryParseReference(reference, out _, out _));

    [Fact]
    public void NotAZip_Throws_InvalidData()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("not a zip"));
        Assert.Throws<InvalidDataException>(() => XlsxPackage.Read(stream));
    }

    [Fact]
    public void ZipWithoutWorkbook_Throws_InvalidData()
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            zip.CreateEntry("readme.txt");
        }

        stream.Position = 0;
        Assert.Throws<InvalidDataException>(() => XlsxPackage.Read(stream));
    }
}
