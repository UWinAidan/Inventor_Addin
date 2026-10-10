using InventorAddin.Core.Numbering;

namespace InventorAddin.Core.Tests.Numbering;

public sealed class PartNumberTests
{
    [Fact]
    public void Constants()
    {
        Assert.Equal("AWB", PartNumber.Prefix);
        Assert.Equal('-', PartNumber.Separator);
        Assert.Equal(5, PartNumber.DigitCount);
        Assert.Equal(10001, PartNumber.FirstValue);
        Assert.Equal(99999, PartNumber.LastValue);
    }

    [Theory]
    [InlineData(10001, "AWB-10001")]
    [InlineData(23456, "AWB-23456")]
    [InlineData(99999, "AWB-99999")]
    public void ToStringWritesPrefixDashAndFiveDigits(int value, string expected)
    {
        Assert.Equal(expected, new PartNumber(value).ToString());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10000)]
    [InlineData(100000)]
    [InlineData(-10001)]
    public void ConstructorRejectsValueOutsideRange(int value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PartNumber(value));
    }

    [Theory]
    [InlineData("AWB-10001", 10001)]
    [InlineData("AWB-99999", 99999)]
    [InlineData("AWB-31415", 31415)]
    [InlineData("awb-10001", 10001)]
    [InlineData("Awb-27182", 27182)]
    [InlineData("  AWB-10001  ", 10001)]
    [InlineData("\tAWB-10002\r\n", 10002)]
    public void TryParseAccepts(string text, int expected)
    {
        Assert.True(PartNumber.TryParse(text, out var number));
        Assert.Equal(expected, number.Value);
    }

    [Fact]
    public void LowerCasePrefixIsWrittenInCapitals()
    {
        Assert.Equal("AWB-10001", PartNumber.Parse("awb-10001").ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("AWB-1000")]
    [InlineData("AWB-100001")]
    [InlineData("AWB10001")]
    [InlineData("AWB-1000A")]
    [InlineData("AWB-09999")]
    [InlineData("AWB-10000")]
    [InlineData("AWB-00001")]
    [InlineData("AWB-00000")]
    [InlineData("AWB- 10001")]
    [InlineData("AWB -10001")]
    [InlineData("AWB-100 01")]
    [InlineData("A WB-10001")]
    [InlineData("XYZ-10001")]
    [InlineData("AWC-10001")]
    [InlineData("AB-10001")]
    [InlineData("AWBX-10001")]
    [InlineData("AWB_10001")]
    [InlineData("AWB--10001")]
    [InlineData("AWB-+1001")]
    [InlineData("AWB--1001")]
    [InlineData("AWB-١٠٠٠١")] // Arabic-Indic digits
    [InlineData("AWB-１０００１")] // full-width digits
    [InlineData("AWB-1²001")] // superscript two
    [InlineData("10001")]
    [InlineData("AWB-")]
    [InlineData("AWB-10001,mounting_bracket")]
    public void TryParseRejects(string? text)
    {
        Assert.False(PartNumber.TryParse(text, out var number));
        Assert.Equal(default, number);
    }

    [Fact]
    public void ParseReturnsTheNumber()
    {
        Assert.Equal(new PartNumber(12345), PartNumber.Parse("AWB-12345"));
    }

    [Theory]
    [InlineData("AWB-1000A")]
    [InlineData("")]
    [InlineData("XYZ-10001")]
    public void ParseThrowsFormatExceptionNamingTheText(string text)
    {
        var ex = Assert.Throws<FormatException>(() => PartNumber.Parse(text));
        Assert.Contains($"\"{text}\"", ex.Message);
    }

    [Theory]
    [InlineData("AWB-10001", true)]
    [InlineData(" awb-54321 ", true)]
    [InlineData("AWB-09999", false)]
    [InlineData(null, false)]
    [InlineData("AWB10001", false)]
    public void IsWellFormedMatchesTryParse(string? text, bool expected)
    {
        Assert.Equal(expected, PartNumber.IsWellFormed(text));
        Assert.Equal(PartNumber.TryParse(text, out _), PartNumber.IsWellFormed(text));
    }

    [Fact]
    public void NextIsFirstValueWhenNoneIssued()
    {
        Assert.Equal(new PartNumber(10001), PartNumber.Next(Array.Empty<PartNumber>()));
    }

    [Fact]
    public void NextIsOneMoreThanTheHighest()
    {
        var issued = new[] { new PartNumber(10005), new PartNumber(10003), new PartNumber(10001) };
        Assert.Equal(new PartNumber(10006), PartNumber.Next(issued));
    }

    [Fact]
    public void NextDoesNotFillGaps()
    {
        var issued = new[] { new PartNumber(10001), new PartNumber(10050) };
        Assert.Equal("AWB-10051", PartNumber.Next(issued).ToString());
    }

    [Fact]
    public void NextIgnoresDuplicates()
    {
        var issued = new[] { new PartNumber(10002), new PartNumber(10002) };
        Assert.Equal(new PartNumber(10003), PartNumber.Next(issued));
    }

    [Fact]
    public void NextReachesTheLastValue()
    {
        Assert.Equal(new PartNumber(99999), PartNumber.Next(new[] { new PartNumber(99998) }));
    }

    [Fact]
    public void NextThrowsWhenTheSequenceIsFull()
    {
        var issued = new[] { new PartNumber(10001), new PartNumber(99999) };
        var ex = Assert.Throws<InvalidOperationException>(() => PartNumber.Next(issued));
        Assert.Equal("The part number sequence is full: AWB-99999 has been used.", ex.Message);
    }

    [Fact]
    public void NextThrowsForNull()
    {
        Assert.Throws<ArgumentNullException>(() => PartNumber.Next(null!));
    }

    [Fact]
    public void EqualityIsByValue()
    {
        var parsed = PartNumber.Parse("awb-20202");
        var built = new PartNumber(20202);

        Assert.Equal(built, parsed);
        Assert.True(built == parsed);
        Assert.Equal(built.GetHashCode(), parsed.GetHashCode());
        Assert.NotEqual(new PartNumber(20203), built);
        Assert.True(new PartNumber(20203) != built);
    }

    [Fact]
    public void OrderingIsByValue()
    {
        var low = new PartNumber(10010);
        var high = new PartNumber(10100);

        Assert.True(low.CompareTo(high) < 0);
        Assert.True(high.CompareTo(low) > 0);
        Assert.Equal(0, low.CompareTo(new PartNumber(10010)));
        Assert.True(low < high);
        Assert.True(high > low);
        Assert.True(low <= new PartNumber(10010));
        Assert.True(high >= new PartNumber(10100));
    }

    [Fact]
    public void SortsByValue()
    {
        var numbers = new[] { new PartNumber(30000), new PartNumber(10001), new PartNumber(20000) };
        Assert.Equal(
            new[] { "AWB-10001", "AWB-20000", "AWB-30000" },
            numbers.Order().Select(n => n.ToString()));
    }
}
