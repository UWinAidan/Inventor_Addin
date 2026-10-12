using InventorAddin.Core.Models;
using InventorAddin.Core.Numbering;
using InventorAddin.Core.PartProperties;

namespace InventorAddin.Core.Tests.Numbering;

public sealed class PartFileNameTests
{
    private static readonly PartNumber Number = PartNumber.Parse("AWB-10001");

    // NormaliseName

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData("___", "")]
    [InlineData("mounting bracket", "MOUNTING BRACKET")]
    [InlineData("mounting_bracket", "MOUNTING BRACKET")]
    [InlineData("  mounting   bracket  ", "MOUNTING BRACKET")]
    [InlineData("mounting \t_ bracket", "MOUNTING BRACKET")]
    [InlineData("_mounting_bracket_", "MOUNTING BRACKET")]
    [InlineData("Mounting Bracket", "MOUNTING BRACKET")]
    [InlineData("plate, 10mm", "PLATE, 10MM")]
    [InlineData("pièce, été", "PIÈCE, ÉTÉ")]
    public void NormaliseName_TrimsTurnsUnderscoresToSpacesCollapsesWhiteSpaceAndUpperCases(string? typed, string expected)
    {
        Assert.Equal(expected, PartFileName.NormaliseName(typed));
    }

    [Fact]
    public void NormaliseName_TypedWithUnderscoresOrSpaces_GivesTheSameName()
    {
        Assert.Equal(PartFileName.NormaliseName("mounting bracket"), PartFileName.NormaliseName("mounting_bracket"));
    }

    // ValidateName

    [Theory]
    [InlineData("mounting bracket")]
    [InlineData("plate, 10mm")]
    [InlineData("bracket.v2")]
    [InlineData("CONsole")]
    [InlineData("COM10")]
    [InlineData("LPT0")]
    [InlineData("my con")]
    [InlineData("a.con")]
    public void ValidateName_UsableName_GivesNull(string typed)
    {
        Assert.Null(PartFileName.ValidateName(typed));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("_ _")]
    public void ValidateName_Blank(string? typed)
    {
        Assert.Equal("Enter a part name.", PartFileName.ValidateName(typed));
    }

    [Theory]
    [InlineData("a\\b", '\\')]
    [InlineData("a/b", '/')]
    [InlineData("a:b", ':')]
    [InlineData("a*b", '*')]
    [InlineData("a?b", '?')]
    [InlineData("a\"b", '"')]
    [InlineData("a<b", '<')]
    [InlineData("a>b", '>')]
    [InlineData("a|b", '|')]
    public void ValidateName_InvalidCharacter_NamesIt(string typed, char character)
    {
        Assert.Equal($"A part name cannot contain the character '{character}'.", PartFileName.ValidateName(typed));
    }

    [Theory]
    [InlineData("a\nb", "U+000A")]
    [InlineData("a\tb", "U+0009")]
    [InlineData("a\0b", "U+0000")]
    [InlineData("a\u001Fb", "U+001F")]
    public void ValidateName_ControlCharacter_NamesItsCodePoint(string typed, string codePoint)
    {
        Assert.Equal($"A part name cannot contain the character '{codePoint}'.", PartFileName.ValidateName(typed));
    }

    [Fact]
    public void ValidateName_ReportsFirstInvalidCharacter()
    {
        Assert.Equal("A part name cannot contain the character '?'.", PartFileName.ValidateName("a?b*c."));
    }

    [Theory]
    [InlineData("bracket.")]
    [InlineData("bracket. ")]
    [InlineData("bracket._")]
    [InlineData(".")]
    public void ValidateName_EndingInDot(string typed)
    {
        Assert.Equal("A part name cannot end with a dot.", PartFileName.ValidateName(typed));
    }

    [Theory]
    [InlineData("CON", "CON")]
    [InlineData("con", "CON")]
    [InlineData(" Prn ", "PRN")]
    [InlineData("AUX", "AUX")]
    [InlineData("nul.txt", "NUL")]
    [InlineData("COM1", "COM1")]
    [InlineData("com9", "COM9")]
    [InlineData("LPT1", "LPT1")]
    [InlineData("lpt9.part", "LPT9")]
    [InlineData("_CON_", "CON")]
    public void ValidateName_ReservedName(string typed, string reserved)
    {
        Assert.Equal($"'{reserved}' is a name Windows keeps for itself.", PartFileName.ValidateName(typed));
    }

    // Build

    [Theory]
    [InlineData("mounting bracket", ".ipt", "MOUNTING_BRACKET,AWB-10001.ipt")]
    [InlineData("mounting_bracket", ".ipt", "MOUNTING_BRACKET,AWB-10001.ipt")]
    [InlineData("  mounting   bracket ", ".iam", "MOUNTING_BRACKET,AWB-10001.iam")]
    [InlineData("Mounting Bracket", ".ipt", "MOUNTING_BRACKET,AWB-10001.ipt")]
    [InlineData("MOUNTING BRACKET", ".ipt", "MOUNTING_BRACKET,AWB-10001.ipt")]
    [InlineData("plate, 10mm", ".ipt", "PLATE,_10MM,AWB-10001.ipt")]
    [InlineData("bracket.v2", ".idw", "BRACKET.V2,AWB-10001.idw")]
    [InlineData("pièce", ".ipt", "PIÈCE,AWB-10001.ipt")]
    public void Build_WithNumber_IsNameCommaNumber(string name, string extension, string expected)
    {
        Assert.Equal(expected, PartFileName.Build(Number, name, extension));
    }

    [Fact]
    public void Build_SpecExample()
    {
        Assert.Equal(
            "MOUNTING_BRACKET,AWB-10005.ipt",
            PartFileName.Build(PartNumber.Parse("AWB-10005"), "MOUNTING BRACKET", ".ipt"));
    }

    [Fact]
    public void Build_WithoutNumber_IsNameAndExtension()
    {
        Assert.Equal("OLD_BRACKET.ipt", PartFileName.Build(null, "old bracket", ".ipt"));
    }

    // ForKind and ValidateNameForKind

    [Theory]
    [InlineData("GEARBOX")]
    [InlineData("gearbox")]
    [InlineData("ASSY_GEARBOX")]
    [InlineData("assy gearbox")]
    [InlineData("ASSY  GEARBOX")]
    [InlineData("  assy__gearbox ")]
    public void ForKind_Assembly_AddsThePrefixOnce(string typed)
    {
        Assert.Equal("ASSY GEARBOX", PartFileName.ForKind(typed, DocumentKind.Assembly));
    }

    [Fact]
    public void ForKind_Weldment_AddsThePrefix()
    {
        Assert.Equal("ASSY FRAME", PartFileName.ForKind("frame", DocumentKind.WeldmentAssembly));
    }

    [Fact]
    public void ForKind_PrefixNotFollowedByASpace_IsPartOfTheName()
    {
        Assert.Equal("ASSY ASSYGEARBOX", PartFileName.ForKind("ASSYGEARBOX", DocumentKind.Assembly));
    }

    [Theory]
    [InlineData(DocumentKind.Part)]
    [InlineData(DocumentKind.SheetMetalPart)]
    public void ForKind_Parts_AreNeverPrefixed(DocumentKind kind)
    {
        Assert.Equal("GEARBOX", PartFileName.ForKind("gearbox", kind));
        Assert.Equal("ASSY GEARBOX", PartFileName.ForKind("assy_gearbox", kind));
    }

    [Fact]
    public void ForKind_UpperCasesACommaAndANonAsciiLetter()
    {
        Assert.Equal("ASSY PIÈCE, ÉTÉ", PartFileName.ForKind("pièce, été", DocumentKind.Assembly));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public void ForKind_BlankName_StaysBlank(string? typed)
    {
        Assert.Equal(string.Empty, PartFileName.ForKind(typed, DocumentKind.Assembly));
    }

    [Fact]
    public void ForKind_Assembly_BuildsTheFileNameWithTheUnderscorePrefix()
    {
        Assert.Equal(
            "ASSY_GEARBOX,AWB-10006.iam",
            PartFileName.Build(PartNumber.Parse("AWB-10006"), PartFileName.ForKind("GEARBOX", DocumentKind.Assembly), ".iam"));
    }

    [Theory]
    [InlineData("ASSY")]
    [InlineData("ASSY_")]
    [InlineData(" assy ")]
    public void ValidateNameForKind_OnlyThePrefix_IsRefusedForAnAssembly(string typed)
    {
        Assert.Equal("Enter a name after ASSY_.", PartFileName.ValidateNameForKind(typed, DocumentKind.Assembly));
        Assert.Equal("Enter a name after ASSY_.", PartFileName.ValidateNameForKind(typed, DocumentKind.WeldmentAssembly));
    }

    [Fact]
    public void ValidateNameForKind_OnlyThePrefix_IsANameForAPart()
    {
        Assert.Null(PartFileName.ValidateNameForKind("ASSY", DocumentKind.Part));
    }

    [Fact]
    public void ValidateNameForKind_OtherProblems_ComeFromValidateName()
    {
        Assert.Equal(PartFileName.ValidateName(" "), PartFileName.ValidateNameForKind(" ", DocumentKind.Assembly));
        Assert.Equal(PartFileName.ValidateName("a*b"), PartFileName.ValidateNameForKind("a*b", DocumentKind.Assembly));
        Assert.Null(PartFileName.ValidateNameForKind("gearbox", DocumentKind.Assembly));
    }

    [Theory]
    [InlineData("")]
    [InlineData("ipt")]
    [InlineData(".")]
    public void Build_ExtensionWithoutLeadingDot_Throws(string extension)
    {
        var ex = Assert.Throws<ArgumentException>(() => PartFileName.Build(Number, "bracket", extension));
        Assert.Equal("extension", ex.ParamName);
    }

    [Theory]
    [InlineData("", "Enter a part name.")]
    [InlineData("a/b", "A part name cannot contain the character '/'.")]
    [InlineData("bracket.", "A part name cannot end with a dot.")]
    [InlineData("CON", "'CON' is a name Windows keeps for itself.")]
    public void Build_InvalidName_ThrowsWithValidationMessage(string name, string message)
    {
        var ex = Assert.Throws<ArgumentException>(() => PartFileName.Build(Number, name, ".ipt"));
        Assert.Equal("name", ex.ParamName);
        Assert.StartsWith(message, ex.Message, StringComparison.Ordinal);
    }

    // ValidatePath

    [Fact]
    public void MaxPathLength_Is259()
    {
        Assert.Equal(259, PartFileName.MaxPathLength);
    }

    [Theory]
    [InlineData(@"C:\Work", 0)]
    [InlineData(@"C:\Work\", 1)]
    [InlineData("/home/someone/work", 0)]
    [InlineData("/home/someone/work/", 1)]
    public void ValidatePath_AtTheLimit_GivesNull(string folder, int extraForTrailingSeparator)
    {
        var withSeparator = folder.Length + 1 - extraForTrailingSeparator;
        var fileName = new string('a', PartFileName.MaxPathLength - withSeparator);
        Assert.Null(PartFileName.ValidatePath(folder, fileName));
    }

    [Theory]
    [InlineData(@"C:\Work", 0)]
    [InlineData(@"C:\Work\", 1)]
    public void ValidatePath_OneOverTheLimit_GivesMessage(string folder, int extraForTrailingSeparator)
    {
        var withSeparator = folder.Length + 1 - extraForTrailingSeparator;
        var fileName = new string('a', PartFileName.MaxPathLength - withSeparator + 1);
        Assert.Equal(
            "The full path is too long for Windows. Choose a shorter name or folder.",
            PartFileName.ValidatePath(folder, fileName));
    }

    [Fact]
    public void ValidatePath_ShortPath_GivesNull()
    {
        Assert.Null(PartFileName.ValidatePath(@"C:\Work", "MOUNTING_BRACKET,AWB-10001.ipt"));
    }

    // TryReadNumber

    [Theory]
    [InlineData("A,AWB-10001.ipt", "AWB-10001")]
    [InlineData("A,B,AWB-10001.ipt", "AWB-10001")]
    [InlineData(",AWB-10001.ipt", "AWB-10001")]
    [InlineData(@"C:\Work\MOUNTING_BRACKET,AWB-10001.ipt", "AWB-10001")]
    [InlineData("/home/someone/work/mounting_bracket,AWB-10001.ipt", "AWB-10001")]
    [InlineData(@"C:\Work\plate,_10mm,AWB-10001.ipt", "AWB-10001")]
    [InlineData(@"C:\Work\bracket,awb-23456.ipt", "AWB-23456")]
    [InlineData(@"C:\Work\bracket, AWB-23456 .ipt", "AWB-23456")]
    [InlineData(@"C:\Work,A\Sub,AWB-10002\BRACKET,AWB-10001.ipt", "AWB-10001")]
    [InlineData("FRAME,AWB-99999.iam", "AWB-99999")]
    public void TryReadNumber_NumberAfterLastComma(string fullFileName, string expected)
    {
        Assert.True(PartFileName.TryReadNumber(fullFileName, out var number));
        Assert.Equal(expected, number.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("A.ipt")]
    [InlineData("A,B.ipt")]
    [InlineData(@"C:\Work\Old_bracket.v2.ipt")]
    [InlineData(@"C:\Work\AWB-10001.ipt")]
    [InlineData(@"C:\Work\name,notanumber.ipt")]
    [InlineData(@"C:\Work\name,AWB-1000.ipt")]
    [InlineData(@"C:\Work\name,AWB-10001,.ipt")]
    [InlineData(@"C:\folder,AWB-10001\bracket.ipt")]
    [InlineData(@"C:\Work\AWB-10001,A.ipt")]
    [InlineData(@"C:\Work\AWB-10005,BRACKET.ipt")]
    public void TryReadNumber_NoNumber(string? fullFileName)
    {
        Assert.False(PartFileName.TryReadNumber(fullFileName, out var number));
        Assert.Equal(default, number);
    }

    // Build then read

    [Theory]
    [InlineData("mounting bracket")]
    [InlineData("mounting_bracket")]
    [InlineData("  Mounting   Bracket  ")]
    [InlineData("plate, 10mm")]
    [InlineData("a,b,c")]
    [InlineData("bracket.v2")]
    [InlineData("x")]
    [InlineData("M8 bolt (long)")]
    public void BuildThenRead_GivesNormalisedNameAndNumber(string typed)
    {
        var fileName = PartFileName.Build(Number, typed, ".ipt");

        Assert.Equal(PartFileName.NormaliseName(typed), PartName.FromFileName(@"C:\Work\" + fileName));
        Assert.True(PartFileName.TryReadNumber(@"C:\Work\" + fileName, out var number));
        Assert.Equal(Number, number);
    }

    [Theory]
    [InlineData("old bracket")]
    [InlineData("bracket.v2")]
    [InlineData("A,B")]
    [InlineData("plate, 10mm")]
    public void BuildThenRead_WithoutNumber_GivesNormalisedNameAndNoNumber(string typed)
    {
        var fileName = PartFileName.Build(null, typed, ".ipt");

        Assert.Equal(PartFileName.NormaliseName(typed), PartName.FromFileName("/home/someone/work/" + fileName));
        Assert.False(PartFileName.TryReadNumber("/home/someone/work/" + fileName, out _));
    }
}
