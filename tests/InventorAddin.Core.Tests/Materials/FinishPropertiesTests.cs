using InventorAddin.Core.Materials;
using InventorAddin.Core.PartProperties;

namespace InventorAddin.Core.Tests.Materials;

public sealed class FinishPropertiesTests
{
    private static Finish Finish(
        string code = "TF1",
        string name = "Test finish",
        string min = "0.013",
        string max = "0.025",
        string note = "TEST NOTE.") =>
        new(code, name, FinishKind.Plating, ["Metal"], "Test Shine", min, max, true, note);

    [Fact]
    public void For_Finish_SetsAllFour_InOrder()
    {
        var writes = FinishProperties.For(Finish());

        Assert.Equal(
            [
                PropertyWrite.SetText(PartPropertyStorage.Finish, "Test finish"),
                PropertyWrite.SetText(PartPropertyStorage.FinishCode, "TF1"),
                PropertyWrite.SetText(PartPropertyStorage.FinishThickness, "0.013-0.025 mm"),
                PropertyWrite.SetText(PartPropertyStorage.FinishNote, "TEST NOTE."),
            ],
            writes);
    }

    [Fact]
    public void For_Null_RemovesAllFour()
    {
        var writes = FinishProperties.For(null);

        Assert.Equal(
            [
                PropertyWrite.Remove(PartPropertyStorage.Finish),
                PropertyWrite.Remove(PartPropertyStorage.FinishCode),
                PropertyWrite.Remove(PartPropertyStorage.FinishThickness),
                PropertyWrite.Remove(PartPropertyStorage.FinishNote),
            ],
            writes);
    }

    [Fact]
    public void For_ZeroThickness_RemovesThicknessOnly()
    {
        var writes = FinishProperties.For(Finish(min: "0", max: "0"));

        Assert.Equal(PropertyWrite.Remove(PartPropertyStorage.FinishThickness), writes[2]);
        Assert.Equal(PropertyWriteAction.Set, writes[0].Action);
        Assert.Equal(PropertyWriteAction.Set, writes[1].Action);
        Assert.Equal(PropertyWriteAction.Set, writes[3].Action);
    }

    [Fact]
    public void For_BlankNote_RemovesNote()
    {
        var writes = FinishProperties.For(Finish(note: ""));

        Assert.Equal(PropertyWrite.Remove(PartPropertyStorage.FinishNote), writes[3]);
    }

    [Fact]
    public void For_PrintNote_WrittenAsIs_IncludingLineBreaks()
    {
        var writes = FinishProperties.For(Finish(note: "LINE ONE,\r\nLINE TWO"));

        Assert.Equal("LINE ONE,\r\nLINE TWO", writes[3].TextValue);
    }

    [Theory]
    [InlineData("0.013", "0.025", "0.013-0.025 mm")]
    [InlineData("0", "0.002", "0-0.002 mm")]
    [InlineData("0.025", "0.025", "0.025 mm")]
    [InlineData("0.05", "0.050", "0.050 mm")]
    [InlineData("0", "0", "")]
    [InlineData("0.000", "0", "")]
    public void ThicknessText_FollowsTheDefault(string min, string max, string expected)
    {
        Assert.Equal(expected, FinishProperties.ThicknessText(Finish(min: min, max: max)));
    }

    [Fact]
    public void AllFourLocations_AreCustomText()
    {
        Assert.All(
            FinishProperties.For(Finish()),
            w =>
            {
                Assert.True(w.Location.IsCustom);
                Assert.Equal(PropertyValueKind.Text, w.Location.Kind);
            });
    }
}
