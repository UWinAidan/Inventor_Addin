using InventorAddin.Core.Drawing;
using Xunit;

namespace InventorAddin.Core.Tests.Drawing;

public class OutOfBoundsReportTests
{
    private static SheetFinding Finding(string sheet, string message) =>
        new(sheet, new BoundsFinding("x", BoundsProblem.Outside, BorderEdges.None, false, message));

    [Fact]
    public void Line_NamesTheSheetThenTheMessage() =>
        Assert.Equal("Sheet:1: Note is outside the border.", Finding("Sheet:1", "Note is outside the border.").Line);

    [Fact]
    public void Lines_KeepTheOrderGiven()
    {
        var lines = OutOfBoundsReport.Lines(new[] { Finding("A", "one."), Finding("B", "two.") });
        Assert.Equal(new[] { "A: one.", "B: two." }, lines);
    }

    [Theory]
    [InlineData(1, "1 item is outside the border or over the title block. Pick one to select it.")]
    [InlineData(3, "3 items are outside the border or over the title block. Pick one to select it.")]
    public void Heading_AgreesWithTheCount(int count, string expected) =>
        Assert.Equal(expected, OutOfBoundsReport.Heading(count));

    [Fact]
    public void NoneMessage_IsTheSpecifiedText() =>
        Assert.Equal("Everything is inside the border.", OutOfBoundsReport.NoneMessage);

    [Theory]
    [InlineData(1, "1 item could not be read and was not checked. See the log.")]
    [InlineData(4, "4 items could not be read and were not checked. See the log.")]
    public void NotCheckedLine_AgreesWithTheCount(int count, string expected) =>
        Assert.Equal(expected, OutOfBoundsReport.NotCheckedLine(count));

    [Fact]
    public void NotCheckedWarning_NamesTheItem() =>
        Assert.Equal("Note \"x\" was not checked: its extent could not be read.", OutOfBoundsReport.NotCheckedWarning("Note \"x\""));
}
