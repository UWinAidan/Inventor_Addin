using InventorAddin.Core.Drawing;
using Xunit;

namespace InventorAddin.Core.Tests.Drawing;

public class PartsListMakeBuyTests
{
    [Theory]
    [InlineData("Make/Buy", "ANYTHING", true)]
    [InlineData(" make/buy ", "", true)]
    [InlineData("", "MAKE/BUY", true)]
    [InlineData(null, " make/buy ", true)]
    [InlineData("Other", "MAKE/BUY", false)]
    [InlineData("", "ITEM", false)]
    [InlineData(null, null, false)]
    public void IsMakeBuyColumn_MatchesPropertyThenHeading(string? property, string? title, bool expected) =>
        Assert.Equal(expected, PartsListMakeBuy.IsMakeBuyColumn(property, title));

    [Theory]
    [InlineData("MAKE", "", true)]
    [InlineData("MAKE", null, true)]
    [InlineData("BUY", "MAKE", true)]
    [InlineData("BUY", "BUY", false)]
    [InlineData("BUY", " BUY ", false)]
    [InlineData("", "typed by hand", false)]
    [InlineData("", "", false)]
    public void ShouldWrite_OnlyForNewNonBlankText(string text, string? current, bool expected) =>
        Assert.Equal(expected, PartsListMakeBuy.ShouldWrite(text, current));
    [Theory]
    [InlineData("", "MAKE", true)]
    [InlineData("", " buy ", true)]
    [InlineData("", "typed by hand", false)]
    [InlineData("", "", false)]
    [InlineData("", null, false)]
    [InlineData("BUY", "MAKE", false)]
    public void ShouldClear_OnlyStaleMakeBuyTextWhenBlank(string text, string? current, bool expected) =>
        Assert.Equal(expected, PartsListMakeBuy.ShouldClear(text, current));

    [Fact]
    public void Summary_SaysCountsAndAddedColumns() =>
        Assert.Equal(
            "Parts lists updated: 2. Make: 3, Buy: 4, left blank: 1. A Make/Buy column was added where it was missing (1).",
            PartsListMakeBuy.Summary(2, 1, 3, 4, 1, 0, 0, 0));

    [Fact]
    public void Summary_SaysClearedCells() =>
        Assert.Equal(
            "Parts lists updated: 1. Make: 0, Buy: 0, left blank: 2. Cleared an out-of-date Make/Buy text from 1 cell.",
            PartsListMakeBuy.Summary(1, 0, 0, 0, 2, 1, 0, 0));

    [Fact]
    public void Summary_SeparatesFailedListsFromFailedCells() =>
        Assert.Equal(
            "Parts lists updated: 1. Make: 0, Buy: 0, left blank: 0. 1 parts list could not get a Make/Buy column and was skipped. 2 cells could not be written; see the log.",
            PartsListMakeBuy.Summary(1, 0, 0, 0, 0, 0, 1, 2));
}
