using InventorAddin.Core.Materials;

namespace InventorAddin.Core.Tests.Materials;

public sealed class CsvTableTests
{
    [Fact]
    public void EmptyText_HasNoHeaderAndNoRows()
    {
        var table = CsvTable.Parse("");

        Assert.Null(table.Header);
        Assert.Empty(table.Rows);
    }

    [Fact]
    public void BlankLinesOnly_HasNoHeader()
    {
        var table = CsvTable.Parse("\r\n\n\r\n");

        Assert.Null(table.Header);
        Assert.Empty(table.Rows);
    }

    [Fact]
    public void SimpleFile_SplitsHeaderAndRows_WithLineNumbers()
    {
        var table = CsvTable.Parse("A,B,C\r\n1,2,3\r\n4,5,6\r\n");

        Assert.Equal(["A", "B", "C"], table.Header!.Fields);
        Assert.Equal(1, table.Header.Line);
        Assert.Equal(2, table.Rows.Count);
        Assert.Equal(["1", "2", "3"], table.Rows[0].Fields);
        Assert.Equal(2, table.Rows[0].Line);
        Assert.Equal(["4", "5", "6"], table.Rows[1].Fields);
        Assert.Equal(3, table.Rows[1].Line);
    }

    [Theory]
    [InlineData("A,B\n1,2\n3,4")]
    [InlineData("A,B\r\n1,2\r\n3,4\r\n")]
    [InlineData("A,B\r1,2\r3,4\r")]
    public void LineEndings_CrLfLfAndCr_AllRead(string text)
    {
        var table = CsvTable.Parse(text);

        Assert.Equal(2, table.Rows.Count);
        Assert.Equal(["3", "4"], table.Rows[1].Fields);
        Assert.Equal(3, table.Rows[1].Line);
    }

    [Fact]
    public void ByteOrderMark_IsIgnored()
    {
        var table = CsvTable.Parse("﻿Code,Name\r\nX1,Thing\r\n");

        Assert.Equal("Code", table.Header!.Fields[0]);
        Assert.Single(table.Rows);
    }

    [Fact]
    public void BlankLines_AreSkipped_AndLineNumbersCountThem()
    {
        var table = CsvTable.Parse("A,B\r\n\r\n1,2\r\n\r\n\r\n3,4\r\n");

        Assert.Equal(2, table.Rows.Count);
        Assert.Equal(3, table.Rows[0].Line);
        Assert.Equal(6, table.Rows[1].Line);
    }

    [Fact]
    public void QuotedField_HoldsCommasAndDoubledQuotes()
    {
        var table = CsvTable.Parse("A,B\r\n\"one, two\",\"say \"\"hi\"\"\"\r\n");

        Assert.Equal(["one, two", "say \"hi\""], table.Rows[0].Fields);
    }

    [Fact]
    public void QuotedField_HoldsLineBreaks_AndLaterLinesCountThem()
    {
        var table = CsvTable.Parse("A,B\r\n\"first\r\nsecond\",x\r\n\"a\nb\",y\r\nlast,z\r\n");

        Assert.Equal(3, table.Rows.Count);
        Assert.Equal("first\r\nsecond", table.Rows[0][0]);
        Assert.Equal(2, table.Rows[0].Line);
        Assert.Equal("a\nb", table.Rows[1][0]);
        Assert.Equal(4, table.Rows[1].Line);
        Assert.Equal(6, table.Rows[2].Line);
    }

    [Fact]
    public void EmptyFields_AreKept()
    {
        var table = CsvTable.Parse("A,B,C\r\n,,\r\n\"\",x,\r\n");

        Assert.Equal(["", "", ""], table.Rows[0].Fields);
        Assert.True(table.Rows[0].IsBlank);
        Assert.Equal(["", "x", ""], table.Rows[1].Fields);
        Assert.False(table.Rows[1].IsBlank);
    }

    [Fact]
    public void Indexer_PastTheEnd_IsEmpty()
    {
        var table = CsvTable.Parse("A,B,C\r\n1\r\n");

        Assert.Equal("1", table.Rows[0][0]);
        Assert.Equal("", table.Rows[0][2]);
        Assert.Equal("", table.Rows[0][-1]);
    }

    [Fact]
    public void FieldsAreNotTrimmed()
    {
        var table = CsvTable.Parse("A,B\r\n  x , y\r\n");

        Assert.Equal(["  x ", " y"], table.Rows[0].Fields);
    }

    [Fact]
    public void QuoteInsideUnquotedValue_IsKept()
    {
        var table = CsvTable.Parse("A,B\r\n6\" bar,x\r\n");

        Assert.Equal("6\" bar", table.Rows[0][0]);
    }

    [Fact]
    public void UnclosedQuote_MarksTheRowUnterminated()
    {
        var table = CsvTable.Parse("A,B\r\n1,2\r\n\"open,3\r\n");

        Assert.False(table.Rows[0].Unterminated);
        Assert.True(table.Rows[1].Unterminated);
        Assert.False(table.Rows[1].IsBlank);
        Assert.Equal(3, table.Rows[1].Line);
    }

    [Fact]
    public void LastLineWithoutLineEnding_IsRead()
    {
        var table = CsvTable.Parse("A,B\r\n1,2");

        Assert.Equal(["1", "2"], table.Rows[0].Fields);
    }

    [Fact]
    public void RepoFinishList_QuotedNamesAndNotesRead()
    {
        var table = CsvTable.Parse(FinishListCsvTests.ReadRepoFinishList());

        Assert.Equal(9, table.Header!.Fields.Count);
        Assert.Equal(14, table.Rows.Count);
        Assert.All(table.Rows, r => Assert.Equal(9, r.Fields.Count));

        var zinc = table.Rows.Single(r => r[0] == "ZN");
        Assert.Equal("Zinc plate, clear trivalent", zinc[1]);
        Assert.StartsWith("ZINC PLATE PER ASTM B633, SC 2,", zinc[8]);
    }
}
