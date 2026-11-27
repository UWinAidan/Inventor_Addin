using InventorAddin.Core.Drawing;
using Xunit;

namespace InventorAddin.Core.Tests.Drawing;

public class MakeBuyTests
{
    [Fact]
    public void AwbNumberedPart_IsMake() =>
        Assert.Equal(MakeBuyKind.Make, MakeBuy.Decide("AWB-10001", false, false));

    [Fact]
    public void ContentCenterBolt_IsBuy() =>
        Assert.Equal(MakeBuyKind.Buy, MakeBuy.Decide("BOLT-M6x20", true, false));

    [Fact]
    public void FileInLibraryFolder_IsBuy() =>
        Assert.Equal(MakeBuyKind.Buy, MakeBuy.Decide("Widget-7", false, true));

    [Theory]
    [InlineData("Widget-7")]
    [InlineData("AWB-1000")]
    [InlineData("AWB-100010")]
    [InlineData("AWB-00001")]
    [InlineData("")]
    [InlineData(null)]
    public void Anything_Else_IsBlank(string? partNumber) =>
        Assert.Equal(MakeBuyKind.Blank, MakeBuy.Decide(partNumber, false, false));

    [Fact]
    public void AwbNumberedFileInLibraryFolder_IsBuy_LibraryWins() =>
        Assert.Equal(MakeBuyKind.Buy, MakeBuy.Decide("AWB-10001", false, true));

    [Fact]
    public void AwbNumberedContentCenterPart_IsBuy_LibraryWins() =>
        Assert.Equal(MakeBuyKind.Buy, MakeBuy.Decide("AWB-10001", true, false));

    [Fact]
    public void PartNumberIsMatchedByTheNumberingParser_IgnoringCaseAndSpaces() =>
        Assert.Equal(MakeBuyKind.Make, MakeBuy.Decide(" awb-10001 ", false, false));

    [Theory]
    [InlineData(MakeBuyKind.Make, "MAKE")]
    [InlineData(MakeBuyKind.Buy, "BUY")]
    [InlineData(MakeBuyKind.Blank, "")]
    public void CellText_ForKind(MakeBuyKind kind, string expected) =>
        Assert.Equal(expected, MakeBuy.CellText(kind));

    [Fact]
    public void CellText_DecidesInOneCall() =>
        Assert.Equal("BUY", MakeBuy.CellText("AWB-10001", false, true));
}
