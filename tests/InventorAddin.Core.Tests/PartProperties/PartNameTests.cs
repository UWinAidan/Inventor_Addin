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
    [InlineData("A,AWB-10001.ipt", "A")]
    [InlineData("A,B,AWB-10001.ipt", "A,B")]
    [InlineData(",AWB-10001.ipt", "")]
    [InlineData("_,AWB-10001.ipt", "")]
    [InlineData(@"C:\Work\MOUNTING_BRACKET,AWB-10005.ipt", "MOUNTING BRACKET")]
    [InlineData("/home/someone/work/mounting_bracket,AWB-10001.ipt", "mounting bracket")]
    [InlineData(@"C:\Work\plate,_10mm,AWB-10001.ipt", "plate, 10mm")]
    [InlineData(@"C:\Work\Mounting_Bracket,awb-10001.iam", "Mounting Bracket")]
    [InlineData(@"C:\Work,old\Sub,AWB-10002\bracket,AWB-10001.ipt", "bracket")]
    public void FromFileName_WithNumber_TakesTextBeforeLastCommaWithUnderscoresAsSpaces(string fullFileName, string expected)
    {
        Assert.Equal(expected, PartName.FromFileName(fullFileName));
    }

    [Theory]
    [InlineData("A.ipt", "A")]
    [InlineData("A,B.ipt", "A,B")]
    [InlineData(@"C:\Work\name,notanumber.ipt", "name,notanumber")]
    [InlineData(@"C:\Work\plate,_10mm.ipt", "plate, 10mm")]
    [InlineData(@"C:\Work\AWB-10001,A.ipt", "AWB-10001,A")]
    [InlineData(@"C:\Work\AWB-10005,BRACKET.ipt", "AWB-10005,BRACKET")]
    [InlineData(@"C:\Work\AWB-10001,mounting_bracket.ipt", "AWB-10001,mounting bracket")]
    public void FromFileName_WithoutNumber_IsTheWholeStem(string fullFileName, string expected)
    {
        Assert.Equal(expected, PartName.FromFileName(fullFileName));
    }

    [Theory]
    [InlineData("A,AWB-10001.ipt")]
    [InlineData("A,B,AWB-10001.ipt")]
    [InlineData(",AWB-10001.ipt")]
    [InlineData("AWB-10001,A.ipt")]
    [InlineData("A.ipt")]
    [InlineData("A,B.ipt")]
    [InlineData(@"C:\A,AWB-10003\B,AWB-10004.ipt")]
    public void FromFileName_AndTryReadNumber_AgreeOnTheSplit(string fullFileName)
    {
        var name = PartName.FromFileName(fullFileName);
        var stem = System.IO.Path.GetFileNameWithoutExtension(fullFileName.Replace('\\', '/')).Replace('_', ' ').Trim();

        if (InventorAddin.Core.Numbering.PartFileName.TryReadNumber(fullFileName, out var number))
        {
            Assert.Equal(stem, (name + "," + number).Trim(), ignoreCase: true);
        }
        else
        {
            Assert.Equal(stem, name);
        }
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
