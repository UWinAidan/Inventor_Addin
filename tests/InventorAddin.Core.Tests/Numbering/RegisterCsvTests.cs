using InventorAddin.Core.Models;
using InventorAddin.Core.Numbering;

namespace InventorAddin.Core.Tests.Numbering;

public sealed class RegisterCsvTests
{
    private const string Header = "Number,Name,PartType,Kind,FilePath,AssignedAt,AssignedBy,Computer";

    private static readonly DateTimeOffset At = new(2026, 10, 10, 14, 3, 0, TimeSpan.Zero);

    private static RegisterEntry Entry(
        int value = 10001,
        string name = "test plate",
        string partType = "P",
        DocumentKind kind = DocumentKind.Part,
        string filePath = @"C:\Work\AWB-10001,test_plate.ipt",
        DateTimeOffset? at = null) =>
        new(new PartNumber(value), name, partType, kind, filePath, at ?? At, "testuser", "TESTPC");

    // Entry

    [Fact]
    public void EntryTurnsNullTextIntoEmpty()
    {
        var entry = new RegisterEntry(new PartNumber(10001), null!, null!, DocumentKind.Part, null!, At, null!, null!);

        Assert.Equal("", entry.Name);
        Assert.Equal("", entry.PartType);
        Assert.Equal("", entry.FilePath);
        Assert.Equal("", entry.AssignedBy);
        Assert.Equal("", entry.Computer);
    }

    [Fact]
    public void EntryTurnsNullTextIntoEmptyThroughWith()
    {
        var entry = Entry() with { Name = null!, Computer = null! };

        Assert.Equal("", entry.Name);
        Assert.Equal("", entry.Computer);
    }

    [Fact]
    public void EntriesWithTheSameValuesAreEqual()
    {
        Assert.Equal(Entry(), Entry());
        Assert.NotEqual(Entry(), Entry(name: "other"));
    }

    // Write

    [Fact]
    public void HeaderIsFixed()
    {
        Assert.Equal(Header, RegisterCsv.Header);
    }

    [Fact]
    public void WriteEmptyGivesTheHeaderLineOnly()
    {
        Assert.Equal(Header + "\r\n", RegisterCsv.Write([]));
    }

    [Fact]
    public void WriteGivesOneLinePerEntryInOrder()
    {
        var text = RegisterCsv.Write(
        [
            Entry(10002, "second", "", DocumentKind.Assembly, @"C:\Work\second.iam"),
            Entry(10001, filePath: @"C:\Work\first.ipt"),
        ]);

        Assert.Equal(
            Header + "\r\n"
            + @"AWB-10002,second,,Assembly,C:\Work\second.iam,2026-10-10T14:03:00Z,testuser,TESTPC" + "\r\n"
            + @"AWB-10001,test plate,P,Part,C:\Work\first.ipt,2026-10-10T14:03:00Z,testuser,TESTPC" + "\r\n",
            text);
    }

    [Fact]
    public void WriteQuotesAPathWithAComma()
    {
        var text = RegisterCsv.Write([Entry()]);

        Assert.Equal(
            Header + "\r\n"
            + "AWB-10001,test plate,P,Part,\"C:\\Work\\AWB-10001,test_plate.ipt\",2026-10-10T14:03:00Z,testuser,TESTPC\r\n",
            text);
    }

    [Theory]
    [InlineData("a,b", "\"a,b\"")]
    [InlineData("say \"hi\"", "\"say \"\"hi\"\"\"")]
    [InlineData("two\nlines", "\"two\nlines\"")]
    [InlineData("two\r\nlines", "\"two\r\nlines\"")]
    [InlineData("cr\ronly", "\"cr\ronly\"")]
    [InlineData("plain name", "plain name")]
    public void WriteQuotesOnlyWhenNeeded(string name, string expected)
    {
        var text = RegisterCsv.Write([Entry(name: name, filePath: @"C:\Work\x.ipt")]);

        Assert.Equal(
            Header + "\r\n" + $"AWB-10001,{expected},P,Part,C:\\Work\\x.ipt,2026-10-10T14:03:00Z,testuser,TESTPC\r\n",
            text);
    }

    [Fact]
    public void WriteGivesTheTimeInUtcToTheSecond()
    {
        var local = new DateTimeOffset(2026, 10, 10, 16, 3, 7, TimeSpan.FromHours(2)).AddMilliseconds(450);

        var text = RegisterCsv.Write([Entry(at: local)]);

        Assert.Contains(",2026-10-10T14:03:07Z,", text);
    }

    [Theory]
    [InlineData(DocumentKind.Part, "Part")]
    [InlineData(DocumentKind.SheetMetalPart, "SheetMetalPart")]
    [InlineData(DocumentKind.Assembly, "Assembly")]
    public void WriteGivesTheKindMemberName(DocumentKind kind, string expected)
    {
        var text = RegisterCsv.Write([Entry(kind: kind)]);

        Assert.Contains($",P,{expected},", text);
    }

    // Read

    [Fact]
    public void ReadGivesEntriesInFileOrder()
    {
        var text = Header + "\r\n"
            + "AWB-10002,second,,Assembly,C:\\Work\\second.iam,2026-10-10T14:03:00Z,testuser,TESTPC\r\n"
            + "AWB-10001,test plate,P,Part,\"C:\\Work\\AWB-10001,test_plate.ipt\",2026-10-10T14:03:00Z,testuser,TESTPC\r\n";

        var result = RegisterCsv.Read(text);

        Assert.Empty(result.Warnings);
        Assert.Equal(2, result.Entries.Count);
        Assert.Equal(Entry(10002, "second", "", DocumentKind.Assembly, @"C:\Work\second.iam"), result.Entries[0]);
        Assert.Equal(Entry(), result.Entries[1]);
    }

    [Fact]
    public void ReadAcceptsLfLineEndings()
    {
        var text = Header + "\nAWB-10001,test plate,P,Part,\"C:\\Work\\AWB-10001,test_plate.ipt\",2026-10-10T14:03:00Z,testuser,TESTPC\n";

        var result = RegisterCsv.Read(text);

        Assert.Equal([Entry()], result.Entries);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void ReadAcceptsAByteOrderMark()
    {
        var text = "\uFEFF" + Header + "\r\nAWB-10001,test plate,P,Part,\"C:\\Work\\AWB-10001,test_plate.ipt\",2026-10-10T14:03:00Z,testuser,TESTPC";

        var result = RegisterCsv.Read(text);

        Assert.Equal([Entry()], result.Entries);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void ReadAcceptsTheLastLineWithoutALineEnding()
    {
        var text = Header + "\r\nAWB-10001,,,Part,,2026-10-10T14:03:00Z,,";

        var result = RegisterCsv.Read(text);

        Assert.Single(result.Entries);
        Assert.Equal(new PartNumber(10001), result.Entries[0].Number);
    }

    [Fact]
    public void ReadSkipsBlankLines()
    {
        var text = "\r\n" + Header + "\r\n\r\nAWB-10001,,,Part,,2026-10-10T14:03:00Z,,\r\n\n\r\nAWB-10002,,,Part,,2026-10-10T14:03:00Z,,\r\n\r\n";

        var result = RegisterCsv.Read(text);

        Assert.Equal(2, result.Entries.Count);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void ReadUnquotesFieldsWithDoubledQuotesAndLineBreaks()
    {
        var text = Header + "\r\n"
            + "AWB-10001,\"say \"\"hi\"\", then\r\nnext line\",P,Part,x,2026-10-10T14:03:00Z,testuser,TESTPC\r\n";

        var result = RegisterCsv.Read(text);

        Assert.Empty(result.Warnings);
        Assert.Equal("say \"hi\", then\r\nnext line", Assert.Single(result.Entries).Name);
    }

    [Fact]
    public void ReadFindsColumnsByNameIgnoringCaseAndOrderAndIgnoresExtraColumns()
    {
        var text = "computer,Notes,ASSIGNEDBY,assignedat,filepath,kind,parttype,name,number\r\n"
            + "TESTPC,checked by hand,testuser,2026-10-10T14:03:00Z,\"C:\\Work\\AWB-10001,test_plate.ipt\",part,P,test plate,AWB-10001\r\n";

        var result = RegisterCsv.Read(text);

        Assert.Empty(result.Warnings);
        Assert.Equal([Entry()], result.Entries);
    }

    [Fact]
    public void ReadWithoutANumberColumnThrows()
    {
        var text = "Name,PartType,Kind\r\ntest plate,P,Part\r\n";

        Assert.Throws<FormatException>(() => RegisterCsv.Read(text));
    }

    [Theory]
    [InlineData("")]
    [InlineData("\uFEFF")]
    [InlineData("\r\n\r\n")]
    public void ReadOfTextWithNoRowsIsEmpty(string text)
    {
        var result = RegisterCsv.Read(text);

        Assert.Empty(result.Entries);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void ReadOfAHeaderOnlyIsEmpty()
    {
        var result = RegisterCsv.Read(Header + "\r\n");

        Assert.Empty(result.Entries);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void ReadOfMissingOptionalColumnsGivesBlanksAndOneWarningEach()
    {
        var result = RegisterCsv.Read("Number,Name\r\nAWB-10001,test plate\r\n");

        var entry = Assert.Single(result.Entries);
        Assert.Equal(new PartNumber(10001), entry.Number);
        Assert.Equal("test plate", entry.Name);
        Assert.Equal("", entry.FilePath);
        Assert.Equal(DocumentKind.Unknown, entry.Kind);
        Assert.Equal(DateTimeOffset.MinValue, entry.AssignedAt);
        Assert.Equal(6, result.Warnings.Count);
        Assert.Contains(result.Warnings, w => w.Contains("\"Kind\"", StringComparison.Ordinal));
    }

    [Fact]
    public void ReadOfBlankKindAndDateGivesUnknownAndMinValue()
    {
        var result = RegisterCsv.Read(Header + "\r\nAWB-10001,test plate,,,,,,\r\n");

        var entry = Assert.Single(result.Entries);
        Assert.Equal(DocumentKind.Unknown, entry.Kind);
        Assert.Equal(DateTimeOffset.MinValue, entry.AssignedAt);
        Assert.Empty(result.Warnings);
    }

    [Theory]
    [InlineData("AWB-1,a,,Part,,2026-10-10T14:03:00Z,,", "\"AWB-1\" is not a part number")]
    [InlineData(",a,,Part,,2026-10-10T14:03:00Z,,", "\"\" is not a part number")]
    [InlineData("AWB-10001,a,,Part,,yesterday,,", "\"yesterday\" is not a date")]
    [InlineData("AWB-10001,a,,Widget,,2026-10-10T14:03:00Z,,", "\"Widget\" is not a kind")]
    [InlineData("AWB-10001,a,,3,,2026-10-10T14:03:00Z,,", "\"3\" is not a kind")]
    [InlineData("AWB-10001,a,,Part,,2026-10-10T14:03:00Z,,,extra", "9 values, but the header has 8 columns")]
    [InlineData("AWB-10001,a,,Part", "4 values, but the header has 8 columns")]
    public void ReadSkipsABadRowWithAWarningNamingItsLine(string badRow, string expectedProblem)
    {
        var text = Header + "\r\n"
            + "AWB-10001,good,,Part,,2026-10-10T14:03:00Z,,\r\n"
            + badRow + "\r\n"
            + "AWB-10003,also good,,Part,,2026-10-10T14:03:00Z,,\r\n";

        var result = RegisterCsv.Read(text);

        Assert.Equal(["good", "also good"], result.Entries.Select(e => e.Name));
        var warning = Assert.Single(result.Warnings);
        Assert.StartsWith("Line 3: ", warning, StringComparison.Ordinal);
        Assert.Contains(expectedProblem, warning, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadCountsLinesInsideQuotedValuesForLaterWarnings()
    {
        var text = Header + "\r\n"
            + "AWB-10001,\"one\r\ntwo\nthree\",,Part,,2026-10-10T14:03:00Z,,\r\n"
            + "bad,,,Part,,2026-10-10T14:03:00Z,,\r\n";

        var result = RegisterCsv.Read(text);

        Assert.Single(result.Entries);
        Assert.StartsWith("Line 5: ", Assert.Single(result.Warnings), StringComparison.Ordinal);
    }

    [Fact]
    public void ReadSkipsARowWhoseQuoteIsNeverClosed()
    {
        var text = Header + "\r\n"
            + "AWB-10001,good,,Part,,2026-10-10T14:03:00Z,,\r\n"
            + "AWB-10002,\"never closed,,Part,,2026-10-10T14:03:00Z,,\r\n";

        var result = RegisterCsv.Read(text);

        Assert.Single(result.Entries);
        var warning = Assert.Single(result.Warnings);
        Assert.StartsWith("Line 3: ", warning, StringComparison.Ordinal);
        Assert.Contains("closing quote", warning, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadAcceptsANumberWithSpacesOrLowerCasePrefix()
    {
        var result = RegisterCsv.Read(Header + "\r\n awb-10001 ,,,Part,,2026-10-10T14:03:00Z,,\r\n");

        Assert.Equal(new PartNumber(10001), Assert.Single(result.Entries).Number);
    }

    [Fact]
    public void ReadGivesTheTimeInUtc()
    {
        var result = RegisterCsv.Read(Header + "\r\nAWB-10001,,,Part,,2026-10-10T16:03:00+02:00,,\r\n");

        var at = Assert.Single(result.Entries).AssignedAt;
        Assert.Equal(At, at);
        Assert.Equal(TimeSpan.Zero, at.Offset);
    }

    [Fact]
    public void ReadOfNullThrows()
    {
        Assert.Throws<ArgumentNullException>(() => RegisterCsv.Read(null!));
    }

    // Round trip

    [Fact]
    public void RoundTripGivesBackEqualEntries()
    {
        RegisterEntry[] entries =
        [
            Entry(),
            Entry(10002, "bracket, left", "", DocumentKind.SheetMetalPart, @"C:\Work\AWB-10002,bracket,_left.ipt"),
            Entry(10003, "the \"big\" frame", "A", DocumentKind.Assembly, @"C:\Work\AWB-10003,frame.iam"),
            Entry(10004, "two\r\nlines\nand \"quotes\", commas", "P", DocumentKind.Part, @"C:\Work\odd.ipt"),
            Entry(10005, "", "", DocumentKind.Part, "", new DateTimeOffset(2027, 1, 2, 3, 4, 5, TimeSpan.Zero)),
            Entry(10006, " leading and trailing spaces ", " P ", DocumentKind.Part, @"C:\Work\x.ipt"),
        ];

        var result = RegisterCsv.Read(RegisterCsv.Write(entries));

        Assert.Empty(result.Warnings);
        Assert.Equal(entries, result.Entries);
    }
}
