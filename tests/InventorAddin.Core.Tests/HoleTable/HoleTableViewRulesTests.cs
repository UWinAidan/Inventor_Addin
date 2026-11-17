using InventorAddin.Core.HoleTable;
using InventorAddin.Core.Holes;

namespace InventorAddin.Core.Tests.HoleTable;

public sealed class HoleTableViewRulesTests
{
    [Fact]
    public void IsEndOn_ParallelAxis_IsTrue() => Assert.True(HoleTableViewRules.IsEndOn(0, 0, 1, 0, 0, 1));

    [Fact]
    public void IsEndOn_ReversedAxis_IsTrue() => Assert.True(HoleTableViewRules.IsEndOn(0, 0, -1, 0, 0, 1));

    [Fact]
    public void IsEndOn_PerpendicularAxis_IsFalse() => Assert.False(HoleTableViewRules.IsEndOn(1, 0, 0, 0, 0, 1));

    [Fact]
    public void IsEndOn_JustAboveTheBoundary_IsTrue() =>
        Assert.True(HoleTableViewRules.IsEndOn(Math.Sqrt(1 - 0.9991 * 0.9991), 0, 0.9991, 0, 0, 1));

    [Fact]
    public void IsEndOn_ExactlyAtTheBoundary_IsFalse() =>
        Assert.False(HoleTableViewRules.IsEndOn(Math.Sqrt(1 - HoleTableViewRules.EndOnDot * HoleTableViewRules.EndOnDot), 0, HoleTableViewRules.EndOnDot, 0, 0, 1));

    [Fact]
    public void IsEndOn_JustBelowTheBoundary_IsFalse() =>
        Assert.False(HoleTableViewRules.IsEndOn(Math.Sqrt(1 - 0.9989 * 0.9989), 0, 0.9989, 0, 0, 1));

    [Theory]
    [InlineData("kInchLengthUnits")]
    [InlineData("kFootLengthUnits")]
    [InlineData("kMilLengthUnits")]
    [InlineData("kYardLengthUnits")]
    [InlineData("kMileLengthUnits")]
    public void UnitsFor_ImperialLengthUnits_AreImperial(string name) =>
        Assert.Equal(HoleUnits.Imperial, HoleTableViewRules.UnitsFor(name));

    [Theory]
    [InlineData("kMillimeterLengthUnits")]
    [InlineData("kCentimeterLengthUnits")]
    [InlineData("kMeterLengthUnits")]
    [InlineData("kSomethingNew")]
    [InlineData(null)]
    public void UnitsFor_OtherUnits_AreMetric(string? name) =>
        Assert.Equal(HoleUnits.Metric, HoleTableViewRules.UnitsFor(name));

    [Fact]
    public void Keys_UseTheBrandPrefix()
    {
        Assert.Equal("AWB_HoleTableKey", HoleTableKeys.AttributeSetName);
        Assert.Equal("AWB hole table keys", HoleTableKeys.TransactionName);
    }

    private static readonly HoleWorkbook Workbook = HoleWorkbook.Empty with
    {
        TaperTapped =
        [
            new TaperTappedRow(5, HoleUnits.Metric, "Rc 1/4", "BSP Taper Internal", 11.2m, 16.8m, 22.4m, false),
            new TaperTappedRow(6, HoleUnits.Imperial, "1/4", "NPT", 0.44m, 0.66m, 0.88m, false),
        ],
    };

    private static InventorHoleFacts Facts(bool tapped, bool tapered, string? designation, string? type) => new(
        HoleSeat.None, tapped, designation, type, null, 1, HoleTermination.ThroughAll, null,
        null, null, null, null, false, null, null, null, null, false)
    { TaperedThread = tapered };

    [Fact]
    public void TaperTappedRowFor_MatchesSizeAndType_IgnoringCaseAndSpaces()
    {
        var row = HoleReadBack.TaperTappedRowFor(Facts(true, true, " 1/4 ", "npt"), Workbook);
        Assert.Equal(6, row?.Row);
    }

    [Fact]
    public void TaperTappedRowFor_NoThreadType_MatchesOnSizeAlone() =>
        Assert.Equal(5, HoleReadBack.TaperTappedRowFor(Facts(true, true, "Rc 1/4", null), Workbook)?.Row);

    [Fact]
    public void TaperTappedRowFor_WrongTypeOrUnknownSize_IsNull()
    {
        Assert.Null(HoleReadBack.TaperTappedRowFor(Facts(true, true, "1/4", "BSP Taper Internal"), Workbook));
        Assert.Null(HoleReadBack.TaperTappedRowFor(Facts(true, true, "9/9", "NPT"), Workbook));
    }

    [Fact]
    public void TaperTappedRowFor_NotATaperThread_IsNull()
    {
        Assert.Null(HoleReadBack.TaperTappedRowFor(Facts(true, false, "1/4", "NPT"), Workbook));
        Assert.Null(HoleReadBack.TaperTappedRowFor(Facts(false, true, "1/4", "NPT"), Workbook));
    }
}
