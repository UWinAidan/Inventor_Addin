using InventorAddin.Core.Drawing;
using InventorAddin.Core.Materials;
using InventorAddin.Core.PartProperties;

namespace InventorAddin.Core.Tests.Materials;

public sealed class FinishSpecBackfillTests
{
    private static Finish Finish(string name = "Test finish", string? printName = null, string min = "0.013", string max = "0.025") =>
        new("TF1", name, FinishKind.Plating, [], null, min, max, true, "TEST NOTE.", printName);

    private static PropertyWrite Spec(string text) => PropertyWrite.SetText(PartPropertyStorage.FinishSpec, text);

    [Fact]
    public void InList_UsesPrintNameAndListThickness_Metric()
    {
        var list = new[] { Finish("Test finish, long wording", "TEST, MID") };

        var write = FinishSpecBackfill.For("Test finish, long wording", "ignored", null, list, PrintUnits.Metric);

        Assert.Equal(Spec("TEST, MID, 13-25 µm"), write);
    }

    [Fact]
    public void InList_Imperial_UsesInches()
    {
        var list = new[] { Finish("Test finish", min: "0.0102", max: "0.0152") };

        var write = FinishSpecBackfill.For("Test finish", null, "  ", list, PrintUnits.Imperial);

        Assert.Equal(Spec("Test finish, .0004-.0006 IN"), write);
    }

    [Fact]
    public void InList_MatchIgnoresCaseAndSpaces()
    {
        var write = FinishSpecBackfill.For("  TEST FINISH ", null, null, [Finish()], PrintUnits.Metric);

        Assert.Equal(Spec("Test finish, 13-25 µm"), write);
    }

    [Fact]
    public void NotInList_BuildsFromStoredRange_Metric()
    {
        var write = FinishSpecBackfill.For("Old coat", "0.005-0.018 mm", null, [Finish()], PrintUnits.Metric);

        Assert.Equal(Spec("Old coat, 5-18 µm"), write);
    }

    [Fact]
    public void NotInList_BuildsFromStoredRange_Imperial()
    {
        var write = FinishSpecBackfill.For("Old coat", "0.0102-0.0152 mm", null, [], PrintUnits.Imperial);

        Assert.Equal(Spec("Old coat, .0004-.0006 IN"), write);
    }

    [Fact]
    public void NotInList_SingleStoredValue_IsOneNumber()
    {
        var write = FinishSpecBackfill.For("Old coat", "0.025 mm", null, [], PrintUnits.Metric);

        Assert.Equal(Spec("Old coat, 25 µm"), write);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("n/a")]
    [InlineData("1-2-3 mm")]
    public void NotInList_NoReadableThickness_NameAlone(string? thickness)
    {
        var write = FinishSpecBackfill.For("Old coat", thickness, null, [], PrintUnits.Metric);

        Assert.Equal(Spec("Old coat"), write);
    }

    [Fact]
    public void InList_ZeroThickness_NameAlone()
    {
        var write = FinishSpecBackfill.For("Test finish", null, null, [Finish(min: "0", max: "0")], PrintUnits.Metric);

        Assert.Equal(Spec("Test finish"), write);
    }

    [Fact]
    public void SpecAlreadySet_ReturnsNothing()
    {
        Assert.Null(FinishSpecBackfill.For("Test finish", "0.013-0.025 mm", "MY OWN SPEC", [Finish()], PrintUnits.Metric));
    }

    private static Finish Long() => Finish("Test finish, long wording", "TEST, MID");

    [Theory]
    [InlineData("Test finish, long wording, 13-25 µm", PrintUnits.Metric, "TEST, MID, 13-25 µm")]
    [InlineData("Test finish, long wording, .0005-.0010 IN", PrintUnits.Metric, "TEST, MID, 13-25 µm")]
    [InlineData("Test finish, long wording, 13-25 µm", PrintUnits.Imperial, "TEST, MID, .0005-.0010 IN")]
    [InlineData("Test finish, long wording", PrintUnits.Metric, "TEST, MID, 13-25 µm")]
    public void OldLongNameSpec_IsRefreshed(string stored, PrintUnits units, string expected)
    {
        var write = FinishSpecBackfill.For("Test finish, long wording", null, stored, [Long()], units);

        Assert.NotNull(write);
        Assert.Equal(expected, write!.TextValue);
        Assert.True(write.IsRefresh);
    }

    [Fact]
    public void OldLongNameSpec_NoThickness_IsRefreshedToPrintNameAlone()
    {
        var finish = Finish("Test finish, long wording", "TEST, MID", min: "0", max: "0");

        var write = FinishSpecBackfill.For("Test finish, long wording", null, "Test finish, long wording", [finish], PrintUnits.Metric);

        Assert.Equal("TEST, MID", write!.TextValue);
    }

    [Fact]
    public void CurrentSpec_IsLeft()
    {
        Assert.Null(FinishSpecBackfill.For("Test finish, long wording", null, "TEST, MID, 13-25 µm", [Long()], PrintUnits.Metric));
    }

    [Fact]
    public void HandWrittenSpec_IsLeft()
    {
        Assert.Null(FinishSpecBackfill.For("Test finish, long wording", null, "Test finish, long wording, ZINC", [Long()], PrintUnits.Metric));
    }

    [Fact]
    public void FinishWithNoPrintName_LongSpecIsLeft()
    {
        Assert.Null(FinishSpecBackfill.For("Test finish", null, "Test finish, 13-25 µm", [Finish()], PrintUnits.Metric));
    }

    [Fact]
    public void OldLongNameSpec_FinishNotInList_IsLeft()
    {
        Assert.Null(FinishSpecBackfill.For("Other coat", null, "Test finish, long wording, 13-25 µm", [Long()], PrintUnits.Metric));
    }

    [Fact]
    public void Describe_SaysFilledAndRefreshed()
    {
        var fill = PropertyWrite.SetText(PartPropertyStorage.RevisionNumber, "A");
        var refresh = Spec("X") with { IsRefresh = true };

        Assert.Equal("Filled in Revision Number; Finish Spec refreshed",
            InventorAddin.Core.Materials.PrintPropertyBackfill.Describe([refresh, fill]));
        Assert.Equal("Finish Spec refreshed", InventorAddin.Core.Materials.PrintPropertyBackfill.Describe([refresh]));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NoFinish_ReturnsNothing(string? finish)
    {
        Assert.Null(FinishSpecBackfill.For(finish, "0.013-0.025 mm", null, [Finish()], PrintUnits.Metric));
    }
}
