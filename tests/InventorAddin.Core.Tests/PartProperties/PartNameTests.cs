using InventorAddin.Core.PartProperties;

namespace InventorAddin.Core.Tests.PartProperties;

public sealed class PartNameTests
{
    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData(@"C:\Work\Bracket.v2.ipt", "Bracket.v2")]
    [InlineData(@"C:\Work\Base plate.iam", "Base plate")]
    [InlineData("/home/someone/work/Bracket.ipt", "Bracket")]
    [InlineData(@"C:\Work/mixed\Plate.ipt", "Plate")]
    [InlineData(@"\\server\share\Shaft.IPT", "Shaft")]
    [InlineData("Bracket.ipt", "Bracket")]
    [InlineData("Bracket", "Bracket")]
    [InlineData(@"C:\Work.v1\Bracket", "Bracket")]
    public void FromFileName_DropsFolderAndExtension(string? fullFileName, string expected)
    {
        Assert.Equal(expected, PartName.FromFileName(fullFileName));
    }
}
