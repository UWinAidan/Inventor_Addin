using InventorAddin.Core.Holes;

namespace InventorAddin.Core.Tests.Holes;

public sealed class HoleFeatureMessagesTests
{
    [Theory]
    [InlineData("kDriverLostHealth", "driver lost")]
    [InlineData("kInErrorHealth", "in error")]
    [InlineData("", "unknown")]
    [InlineData("Odd", "odd")]
    public void Unhealthy_NamesTheStatusInWords(string health, string words)
    {
        Assert.Equal(
            $"Inventor could not make this hole (its status is {words}). Check the direction (Flip) and the hole centres.",
            HoleFeatureMessages.Unhealthy(health));
    }

    [Theory]
    [InlineData(null, "Inventor could not make this hole.")]
    [InlineData("  ", "Inventor could not make this hole.")]
    [InlineData("The parameter is incorrect.", "Inventor could not make this hole (The parameter is incorrect).")]
    public void Refused(string? reason, string expected)
    {
        Assert.Equal(expected, HoleFeatureMessages.Refused(reason));
    }

    [Fact]
    public void TapDrillFallback_NamesTheThreadAndReason()
    {
        Assert.Equal(
            "M6x1 is modelled at Inventor's minor diameter, not the workbook's tap drill: this Inventor did not accept " +
            "the tap drill (Member not found).",
            HoleFeatureMessages.TapDrillFallback("M6x1", "Member not found."));
        Assert.Equal(
            "M6x1 is modelled at Inventor's minor diameter, not the workbook's tap drill: this Inventor did not accept " +
            "the tap drill.",
            HoleFeatureMessages.TapDrillFallback("M6x1", null));
    }
}
