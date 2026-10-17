using InventorAddin.Core.Holes;

namespace InventorAddin.Core.Tests.Holes;

public sealed class HoleInventorNamesTests
{
    [Theory]
    [InlineData(PrecisionFit.Press, "AWB Press fit", "Awb_HoleFit_Press")]
    [InlineData(PrecisionFit.Normal, "AWB Normal fit", "Awb_HoleFit_Normal")]
    [InlineData(PrecisionFit.Slip, "AWB Slip fit", "Awb_HoleFit_Slip")]
    public void FitAppearance_Names(PrecisionFit fit, string displayName, string name)
    {
        Assert.Equal(displayName, HoleFitAppearances.DisplayName(fit));
        Assert.Equal(name, HoleFitAppearances.Name(fit));
        Assert.Equal(fit, HoleFitAppearances.FitFor(displayName));
        Assert.Equal(fit, HoleFitAppearances.FitFor("  " + displayName.ToUpperInvariant() + " "));
        Assert.True(HoleFitAppearances.IsFitAppearance(displayName));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Default")]
    [InlineData("AWB Press")]
    public void FitAppearance_OtherNames_AreNotOurs(string? displayName)
    {
        Assert.Null(HoleFitAppearances.FitFor(displayName));
        Assert.False(HoleFitAppearances.IsFitAppearance(displayName));
    }

    [Theory]
    [InlineData("M6x1", HoleUnits.Metric, "6")]
    [InlineData("M8x1.25", HoleUnits.Metric, "8")]
    [InlineData(" M10 ", HoleUnits.Metric, "10")]
    [InlineData("1/4-20 UNC", HoleUnits.Imperial, "1/4")]
    [InlineData("10-32 UNF", HoleUnits.Imperial, "10")]
    [InlineData("#4-40 UNC", HoleUnits.Imperial, "4")]
    [InlineData("1/2 UNC", HoleUnits.Imperial, "1/2")]
    public void NominalSize(string designation, HoleUnits units, string expected)
    {
        Assert.Equal(expected, HoleThreadNames.NominalSize(designation, units));
    }

    private static readonly HoleThread Metric = new("M5x0.8", TappedRow.MetricThreadType, "6H", 4.2, HoleUnits.Metric, false, 0.75);
    private static readonly HoleThread Imperial = new("7-36 UNF", TappedRow.ImperialThreadType, "2B", 0.12, HoleUnits.Imperial, false, 0.5);

    public static TheoryData<object?, bool?> MetricTapDrills() => new()
    {
        { 4.2, true },
        { 0.42, true },
        { 4.3, false },
        { 4.2f, true },
        { 4.2m, true },
        { "4.2", true },
        { "4.2 mm", true },
        { "0.42 cm", true },
        { "0.42 mm", false },
        { "4.3 mm", false },
        { "about four", null },
        { null, null },
        { true, null },
    };

    [Theory]
    [MemberData(nameof(MetricTapDrills))]
    public void TapDrillMatches_Metric(object? reported, bool? expected)
    {
        Assert.Equal(expected, HoleThreadNames.TapDrillMatches(reported, Metric));
    }

    [Theory]
    [InlineData(0.12, true)]
    [InlineData(0.3048, true)]
    [InlineData("0.12 in", true)]
    [InlineData("0.12\"", true)]
    [InlineData("3.048 mm", true)]
    [InlineData("0.13 in", false)]
    public void TapDrillMatches_Imperial(object reported, bool expected)
    {
        Assert.Equal(expected, HoleThreadNames.TapDrillMatches(reported, Imperial));
    }
}
