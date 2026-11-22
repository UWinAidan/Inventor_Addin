using InventorAddin.Core.Drawing;
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
    public void For_Finish_SetsAllFive_InOrder()
    {
        var writes = FinishProperties.For(Finish(), PrintUnits.Metric);

        Assert.Equal(
            [
                PropertyWrite.SetText(PartPropertyStorage.Finish, "Test finish"),
                PropertyWrite.SetText(PartPropertyStorage.FinishCode, "TF1"),
                PropertyWrite.SetText(PartPropertyStorage.FinishThickness, "0.013-0.025 mm"),
                PropertyWrite.SetText(PartPropertyStorage.FinishNote, "TEST NOTE."),
                PropertyWrite.SetText(PartPropertyStorage.FinishSpec, "Test finish, 13-25 µm"),
            ],
            writes);
    }

    [Fact]
    public void For_Null_RemovesAllFive()
    {
        var writes = FinishProperties.For(null, PrintUnits.Metric);

        Assert.Equal(
            [
                PropertyWrite.Remove(PartPropertyStorage.Finish),
                PropertyWrite.Remove(PartPropertyStorage.FinishCode),
                PropertyWrite.Remove(PartPropertyStorage.FinishThickness),
                PropertyWrite.Remove(PartPropertyStorage.FinishNote),
                PropertyWrite.Remove(PartPropertyStorage.FinishSpec),
            ],
            writes);
    }

    [Fact]
    public void For_ZeroThickness_RemovesThicknessOnly()
    {
        var writes = FinishProperties.For(Finish(min: "0", max: "0"), PrintUnits.Metric);

        Assert.Equal(PropertyWrite.Remove(PartPropertyStorage.FinishThickness), writes[2]);
        Assert.Equal(PropertyWriteAction.Set, writes[0].Action);
        Assert.Equal(PropertyWriteAction.Set, writes[1].Action);
        Assert.Equal(PropertyWriteAction.Set, writes[3].Action);
    }

    [Fact]
    public void For_BlankNote_RemovesNote()
    {
        var writes = FinishProperties.For(Finish(note: ""), PrintUnits.Metric);

        Assert.Equal(PropertyWrite.Remove(PartPropertyStorage.FinishNote), writes[3]);
    }

    [Fact]
    public void For_PrintNote_WrittenAsIs_IncludingLineBreaks()
    {
        var writes = FinishProperties.For(Finish(note: "LINE ONE,\r\nLINE TWO"), PrintUnits.Metric);

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
    public void For_Finish_WritesFinishSpecLast()
    {
        var writes = FinishProperties.For(Finish(name: "Test Coat", min: "0.010", max: "0.015"), PrintUnits.Metric);

        Assert.Equal(5, writes.Count);
        Assert.Equal(PropertyWrite.SetText(PartPropertyStorage.FinishSpec, "Test Coat, 10-15 µm"), writes[4]);
    }

    [Fact]
    public void For_ZeroThickness_FinishSpecHasNoTrailingComma()
    {
        var writes = FinishProperties.For(Finish(name: "Test Coat", min: "0", max: "0"), PrintUnits.Metric);

        Assert.Equal(PropertyWrite.SetText(PartPropertyStorage.FinishSpec, "Test Coat"), writes[4]);
    }

    [Fact]
    public void For_SingleThickness_FinishSpecUsesOneValue()
    {
        var writes = FinishProperties.For(Finish(name: "Test Coat", min: "0.025", max: "0.025"), PrintUnits.Metric);

        Assert.Equal("Test Coat, 25 µm", writes[4].TextValue);
    }

    [Fact]
    public void AllFiveLocations_AreCustomText()
    {
        Assert.All(
            FinishProperties.For(Finish(), PrintUnits.Metric),
            w =>
            {
                Assert.True(w.Location.IsCustom);
                Assert.Equal(PropertyValueKind.Text, w.Location.Kind);
            });
    }

    [Theory]
    [InlineData("0.010", "0.015", PrintUnits.Metric, "Test Coat, 10-15 µm")]
    [InlineData("0.0125", "0.0125", PrintUnits.Metric, "Test Coat, 12.5 µm")]
    [InlineData("0.0125", "0.025", PrintUnits.Metric, "Test Coat, 12.5-25 µm")]
    [InlineData("0.025", "0.025", PrintUnits.Metric, "Test Coat, 25 µm")]
    [InlineData("0.010", "0.015", PrintUnits.Imperial, "Test Coat, .0004-.0006 IN")]
    [InlineData("0.025", "0.025", PrintUnits.Imperial, "Test Coat, .0010 IN")]
    [InlineData("0", "0", PrintUnits.Metric, "Test Coat")]
    [InlineData("0.000", "0", PrintUnits.Imperial, "Test Coat")]
    [InlineData("abc", "0.015", PrintUnits.Metric, "Test Coat")]
    [InlineData("0.010", "", PrintUnits.Imperial, "Test Coat")]
    public void SpecText_UsesThePartsUnits(string min, string max, PrintUnits units, string expected)
    {
        Assert.Equal(expected, FinishProperties.SpecText(Finish(name: "Test Coat", min: min, max: max), units));
    }

    [Theory]
    [InlineData(PrintUnits.Metric, "Test Coat, 10-15 µm")]
    [InlineData(PrintUnits.Imperial, "Test Coat, .0004-.0006 IN")]
    public void For_BothUnits_WritesFiveInOrder_ThicknessStaysMm(PrintUnits units, string spec)
    {
        var writes = FinishProperties.For(Finish(code: "TC", name: "Test Coat", min: "0.010", max: "0.015", note: "NOTE."), units);

        Assert.Equal(
            [
                PropertyWrite.SetText(PartPropertyStorage.Finish, "Test Coat"),
                PropertyWrite.SetText(PartPropertyStorage.FinishCode, "TC"),
                PropertyWrite.SetText(PartPropertyStorage.FinishThickness, "0.010-0.015 mm"),
                PropertyWrite.SetText(PartPropertyStorage.FinishNote, "NOTE."),
                PropertyWrite.SetText(PartPropertyStorage.FinishSpec, spec),
            ],
            writes);
    }
}
