using System.Globalization;
using InventorAddin.Core.Theming;

namespace InventorAddin.Core.Tests.Theming;

public sealed class SectionHeadingsTests
{
    private const char S = SectionHeadings.LetterSpace;

    [Fact]
    public void LetterSpace_IsHairSpace()
    {
        Assert.Equal('\u200A', SectionHeadings.LetterSpace);
    }

    [Fact]
    public void Format_UpperCasesAndSpacesLetters()
    {
        Assert.Equal($"P{S}E{S}O{S}P{S}L{S}E", SectionHeadings.Format("People"));
    }

    [Fact]
    public void Format_KeepsWordSpacesAndSpacesAroundThem()
    {
        Assert.Equal($"A{S} {S}B", SectionHeadings.Format("a b"));
    }

    [Fact]
    public void Format_TrimsSurroundingSpaces()
    {
        Assert.Equal($"G{S}O", SectionHeadings.Format("  go  "));
    }

    [Fact]
    public void Format_SingleCharacter_HasNoSpacing()
    {
        Assert.Equal("X", SectionHeadings.Format("x"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Format_BlankHeading_IsEmpty(string? heading)
    {
        Assert.Equal(string.Empty, SectionHeadings.Format(heading));
    }

    [Fact]
    public void Format_DoesNotSplitCombiningMarkOrSurrogatePair()
    {
        // "e" + combining acute accent, then a character outside the Basic Multilingual Plane.
        string heading = "é\U0001D400";

        Assert.Equal($"É{S}\U0001D400", SectionHeadings.Format(heading));
    }

    [Fact]
    public void Format_DoesNotDependOnCurrentCulture()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            // Turkish upper-cases "i" to a dotted capital I.
            CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
            Assert.Equal($"I{S}D", SectionHeadings.Format("id"));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
