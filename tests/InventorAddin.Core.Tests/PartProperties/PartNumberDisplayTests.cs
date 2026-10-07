using InventorAddin.Core.PartProperties;

namespace InventorAddin.Core.Tests.PartProperties;

public sealed class PartNumberDisplayTests
{
    private const string SavedFile = @"C:\Work\Bracket.ipt";

    [Fact]
    public void NotAssignedText()
    {
        Assert.Equal("Not assigned", PartNumberDisplay.NotAssignedText);
    }

    [Theory]
    [InlineData("Bracket", SavedFile, false)]
    [InlineData(" bracket ", SavedFile, false)]
    [InlineData("BRACKET", SavedFile, false)]
    [InlineData("PN-1001", SavedFile, true)]
    [InlineData(" PN-1001 ", SavedFile, true)]
    [InlineData("Bracket.ipt", SavedFile, true)]
    [InlineData("", SavedFile, false)]
    [InlineData("   ", SavedFile, false)]
    [InlineData(null, SavedFile, false)]
    [InlineData("Bracket.v2", @"C:\Work\Bracket.v2.ipt", false)]
    [InlineData("Bracket", @"C:\Work\Bracket.v2.ipt", true)]
    [InlineData("Bracket", "/home/someone/work/Bracket.ipt", false)]
    public void IsAssigned_SavedFile(string? partNumber, string fullFileName, bool expected)
    {
        Assert.Equal(expected, PartNumberDisplay.IsAssigned(partNumber, fullFileName));
    }

    [Theory]
    [InlineData("Part1", "", true)]
    [InlineData("Part1", null, true)]
    [InlineData("Part1", "   ", true)]
    [InlineData("", "", false)]
    [InlineData("  ", "", false)]
    [InlineData(null, null, false)]
    public void IsAssigned_NeverSavedFile_OnlyBlankIsNotAssigned(string? partNumber, string? fullFileName, bool expected)
    {
        Assert.Equal(expected, PartNumberDisplay.IsAssigned(partNumber, fullFileName));
    }
}
