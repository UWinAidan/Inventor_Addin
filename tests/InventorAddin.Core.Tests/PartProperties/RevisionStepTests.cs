using InventorAddin.Core.PartProperties;

namespace InventorAddin.Core.Tests.PartProperties;

public sealed class RevisionStepTests
{
    [Theory]
    [InlineData(null, "A")]
    [InlineData("", "A")]
    [InlineData("   ", "A")]
    [InlineData("1", "A")]
    [InlineData("A-1", "A")]
    [InlineData("A", "B")]
    [InlineData("B", "C")]
    [InlineData("H", "J")]
    [InlineData("N", "P")]
    [InlineData("Y", "Z")]
    [InlineData("Z", "AA")]
    [InlineData("AA", "AB")]
    [InlineData("AH", "AJ")]
    [InlineData("AZ", "BA")]
    [InlineData("ZZ", "AAA")]
    [InlineData("b", "C")]
    [InlineData(" C ", "D")]
    [InlineData("I", "J")]
    [InlineData("O", "P")]
    public void Next_StepsTheRevision(string? revision, string expected)
    {
        Assert.Equal(expected, RevisionStep.Next(revision));
    }

    [Fact]
    public void Next_NeverProducesIOrO()
    {
        var revision = "A";
        for (var i = 0; i < 1500; i++)
        {
            revision = RevisionStep.Next(revision);
            Assert.DoesNotContain('I', revision);
            Assert.DoesNotContain('O', revision);
        }
    }
}
