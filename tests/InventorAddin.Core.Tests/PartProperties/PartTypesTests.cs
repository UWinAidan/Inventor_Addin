using InventorAddin.Core.Models;
using InventorAddin.Core.PartProperties;

namespace InventorAddin.Core.Tests.PartProperties;

public sealed class PartTypesTests
{
    public static TheoryData<string, string> SpecTable() => new()
    {
        { "P", "Purchased" },
        { "M", "Manufactured" },
        { "PM", "Purchased, modified" },
        { "F", "Fastener" },
        { "A", "Assembly" },
        { "W", "Weldment" },
        { "R", "Reference: shown for context, not bought or made" },
        { "C", "Customer supplied" },
    };

    public static TheoryData<DocumentKind> EveryDocumentKind()
    {
        var data = new TheoryData<DocumentKind>();
        foreach (var kind in Enum.GetValues<DocumentKind>())
        {
            data.Add(kind);
        }
        return data;
    }

    [Fact]
    public void All_MatchesSpecTableInOrder()
    {
        var expected = SpecTable().Select(row => ((string)row[0], (string)row[1])).ToArray();
        var actual = PartTypes.All.Select(type => (type.Code, type.FullName)).ToArray();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void All_CodesAreUnique_IgnoringCase()
    {
        var codes = PartTypes.All.Select(type => type.Code).ToList();

        Assert.Equal(codes.Count, codes.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void All_CodesAreTrimmedAndNotBlank()
    {
        Assert.All(PartTypes.All, type =>
        {
            Assert.False(string.IsNullOrWhiteSpace(type.Code));
            Assert.Equal(type.Code.Trim(), type.Code);
        });
    }

    [Theory]
    [MemberData(nameof(SpecTable))]
    public void Find_ReturnsEntryForEveryCode(string code, string fullName)
    {
        var found = PartTypes.Find(code);

        Assert.NotNull(found);
        Assert.Equal(code, found.Code);
        Assert.Equal(fullName, found.FullName);
    }

    [Theory]
    [MemberData(nameof(SpecTable))]
    public void FullNameFor_ReturnsFullNameForEveryCode(string code, string fullName)
    {
        Assert.Equal(fullName, PartTypes.FullNameFor(code));
    }

    [Theory]
    [InlineData(" pm ", "PM")]
    [InlineData("pm", "PM")]
    [InlineData("Pm", "PM")]
    [InlineData("\tw\n", "W")]
    [InlineData("a", "A")]
    public void Find_IgnoresSurroundingWhitespaceAndCase(string input, string expectedCode)
    {
        Assert.Equal(expectedCode, PartTypes.Find(input)?.Code);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Find_ReturnsNullForBlank(string? code)
    {
        Assert.Null(PartTypes.Find(code));
    }

    [Theory]
    [InlineData("X")]
    [InlineData("P M")]
    [InlineData("Purchased")]
    [InlineData("PMX")]
    public void Find_ReturnsNullForUnknownCode(string code)
    {
        Assert.Null(PartTypes.Find(code));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FullNameFor_ReturnsEmptyForBlank(string? code)
    {
        Assert.Equal(string.Empty, PartTypes.FullNameFor(code));
    }

    [Theory]
    [InlineData("X")]
    [InlineData("Purchased")]
    public void FullNameFor_ReturnsUnknownCodeForCodeNotInList(string code)
    {
        Assert.Equal("Unknown code", PartTypes.FullNameFor(code));
    }

    [Fact]
    public void FullNameFor_NormalisesLikeFind()
    {
        Assert.Equal("Purchased, modified", PartTypes.FullNameFor(" pm "));
    }

    [Fact]
    public void DisplayText_IsCodeColonFullName()
    {
        Assert.Equal("PM: Purchased, modified", PartTypes.Find("PM")!.DisplayText);
    }

    [Theory]
    [MemberData(nameof(EveryDocumentKind))]
    public void DefaultFor_IsAForAssembly_WForWeldment_NullOtherwise(DocumentKind kind)
    {
        var expected = kind switch
        {
            DocumentKind.Assembly => "A",
            DocumentKind.WeldmentAssembly => "W",
            _ => null,
        };

        Assert.Equal(expected, PartTypes.DefaultFor(kind)?.Code);
    }

    [Theory]
    [InlineData(DocumentKind.Unknown)]
    [InlineData(DocumentKind.Part)]
    [InlineData(DocumentKind.SheetMetalPart)]
    [InlineData(DocumentKind.Drawing)]
    [InlineData(DocumentKind.Presentation)]
    public void DefaultFor_NonAssemblyKinds_IsNull(DocumentKind kind)
    {
        Assert.Null(PartTypes.DefaultFor(kind));
    }

    [Fact]
    public void DefaultFor_Assembly_IsA()
    {
        Assert.Same(PartTypes.Find("A"), PartTypes.DefaultFor(DocumentKind.Assembly));
    }

    [Fact]
    public void DefaultFor_WeldmentAssembly_IsW()
    {
        Assert.Same(PartTypes.Find("W"), PartTypes.DefaultFor(DocumentKind.WeldmentAssembly));
    }
}
