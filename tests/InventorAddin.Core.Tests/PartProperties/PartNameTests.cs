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

    [Theory]
    [InlineData(@"C:\Work\AWB-10001,mounting_bracket.ipt", "mounting bracket")]
    [InlineData("/home/someone/work/AWB-10001,mounting_bracket.ipt", "mounting bracket")]
    [InlineData(@"C:\Work\AWB-10001,plate,_10mm.ipt", "plate, 10mm")]
    [InlineData(@"C:\Work\AWB-10001,.ipt", "")]
    [InlineData(@"C:\Work\AWB-10001,_.ipt", "")]
    [InlineData(@"C:\Work\notanumber,name.ipt", "name")]
    [InlineData(@"C:\Work\AWB-10001,Mounting_Bracket.iam", "Mounting Bracket")]
    [InlineData(@"C:\Work,old\AWB-10001,bracket.ipt", "bracket")]
    public void FromFileName_TakesTextAfterFirstCommaWithUnderscoresAsSpaces(string fullFileName, string expected)
    {
        Assert.Equal(expected, PartName.FromFileName(fullFileName));
    }

    [Theory]
    [InlineData(@"C:\Work\Old_bracket.v2.ipt", "Old bracket.v2")]
    [InlineData(@"C:\Work\_base_plate_.ipt", "base plate")]
    [InlineData("/home/someone/work/Old_bracket.ipt", "Old bracket")]
    public void FromFileName_OldFileWithoutComma_ShowsUnderscoresAsSpaces(string fullFileName, string expected)
    {
        Assert.Equal(expected, PartName.FromFileName(fullFileName));
    }
}
