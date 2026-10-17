using InventorAddin.Core.Holes;

namespace InventorAddin.Core.Tests.Holes;

public sealed class Iso286Tests
{
    private static void AssertLimits(double nominalMm, string code, double upperMm, double lowerMm)
    {
        var limits = Iso286.HoleLimits(nominalMm, code);
        Assert.True(limits.Succeeded, limits.Message);
        Assert.Equal(upperMm, limits.UpperMm, 9);
        Assert.Equal(lowerMm, limits.LowerMm, 9);
        Assert.Equal("", limits.Message);
    }

    // Values Inventor 2026 gave in spike 081, 6 mm.

    [Theory]
    [InlineData("H7", 0.012, 0.0)]
    [InlineData("N7", -0.004, -0.016)]
    [InlineData("P7", -0.008, -0.020)]
    [InlineData("G7", 0.016, 0.004)]
    [InlineData("F7", 0.022, 0.010)]
    public void SixMillimetres_MatchesInventor(string code, double upper, double lower)
    {
        AssertLimits(6, code, upper, lower);
    }

    // Values from the ISO 286-2 tables of limits.

    [Theory]
    [InlineData(10, "H7", 0.015, 0.0)]
    [InlineData(25, "H7", 0.021, 0.0)]
    [InlineData(50, "H8", 0.039, 0.0)]
    [InlineData(20, "N7", -0.007, -0.028)]   // delta case: -15 + 8
    [InlineData(20, "K7", 0.006, -0.015)]    // delta case: -2 + 8
    [InlineData(20, "M7", 0.0, -0.021)]      // delta case: -8 + 8
    [InlineData(20, "P7", -0.014, -0.035)]   // delta case: -22 + 8
    [InlineData(20, "P8", -0.022, -0.055)]   // above IT7, no delta
    [InlineData(20, "N9", 0.0, -0.052)]      // above IT8, ES = 0
    [InlineData(20, "M9", -0.008, -0.060)]   // above IT8, no delta
    [InlineData(40, "K6", 0.003, -0.013)]
    [InlineData(40, "N6", -0.012, -0.028)]
    [InlineData(40, "M6", -0.004, -0.020)]
    [InlineData(100, "G6", 0.034, 0.012)]
    [InlineData(100, "F8", 0.090, 0.036)]
    [InlineData(100, "H11", 0.220, 0.0)]
    [InlineData(300, "M6", -0.009, -0.041)]  // ISO special case for M6 over 250 to 315
    [InlineData(300, "M7", 0.0, -0.052)]
    [InlineData(450, "P7", -0.045, -0.108)]
    [InlineData(500, "H7", 0.063, 0.0)]
    [InlineData(2, "K7", 0.0, -0.010)]
    [InlineData(2, "N7", -0.004, -0.014)]
    [InlineData(2, "P7", -0.006, -0.016)]
    [InlineData(2, "K9", 0.0, -0.025)]
    [InlineData(2, "N9", -0.004, -0.029)]
    [InlineData(8, "H5", 0.006, 0.0)]
    [InlineData(8, "H10", 0.058, 0.0)]
    public void KnownValues(double nominalMm, string code, double upper, double lower)
    {
        AssertLimits(nominalMm, code, upper, lower);
    }

    [Theory]
    [InlineData(25, "JS7", 0.010, -0.010)]   // IT7 = 21, odd, rounded down to 20
    [InlineData(25, "JS6", 0.0065, -0.0065)] // IT6 = 13, halved exactly
    [InlineData(10, "JS7", 0.007, -0.007)]   // IT7 = 15
    [InlineData(6, "JS5", 0.0025, -0.0025)]  // IT5 = 5
    [InlineData(50, "JS8", 0.019, -0.019)]   // IT8 = 39
    [InlineData(18, "JS9", 0.021, -0.021)]   // IT9 = 43
    [InlineData(18, "JS8", 0.013, -0.013)]   // IT8 = 27
    [InlineData(30, "JS11", 0.065, -0.065)]  // IT11 = 130, even
    public void JS_IsSymmetric(double nominalMm, string code, double upper, double lower)
    {
        AssertLimits(nominalMm, code, upper, lower);
    }

    // Size steps run "over" the lower bound "up to and including" the upper bound.

    [Theory]
    [InlineData(3, 0.010)]
    [InlineData(3.0001, 0.012)]
    [InlineData(0.5, 0.010)]
    [InlineData(18, 0.018)]
    [InlineData(18.01, 0.021)]
    [InlineData(30, 0.021)]
    [InlineData(30.5, 0.025)]
    [InlineData(500, 0.063)]
    public void SizeBoundaries_BelongToTheLowerStep(double nominalMm, double h7Upper)
    {
        AssertLimits(nominalMm, "H7", h7Upper, 0.0);
    }

    [Fact]
    public void Deviations_InMicrometres()
    {
        var limits = Iso286.HoleLimits(6, "N7");
        Assert.Equal(-4m, limits.UpperUm);
        Assert.Equal(-16m, limits.LowerUm);
        Assert.Equal("N7", limits.Code);
        Assert.Equal(6, limits.NominalMm);
    }

    [Fact]
    public void Code_IsTrimmed()
    {
        var limits = Iso286.HoleLimits(6, "  H7 ");
        Assert.True(limits.Succeeded);
        Assert.Equal("H7", limits.Code);
    }

    // Failures

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(500.001)]
    [InlineData(1000)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void SizeOutsideRange_Fails(double nominalMm)
    {
        var limits = Iso286.HoleLimits(nominalMm, "H7");
        Assert.False(limits.Succeeded);
        Assert.Contains("500 mm", limits.Message);
        Assert.Equal(0m, limits.UpperUm);
        Assert.Equal(0m, limits.LowerUm);
    }

    [Theory]
    [InlineData("n7", "shaft")]
    [InlineData("h7", "shaft")]
    [InlineData("js7", "shaft")]
    [InlineData("Js7", "capital")]
    [InlineData("r6", "capital")]
    public void LowercaseCode_IsRefused(string code, string expectedInMessage)
    {
        var limits = Iso286.HoleLimits(6, code);
        Assert.False(limits.Succeeded);
        Assert.Contains(expectedInMessage, limits.Message);
    }

    [Theory]
    [InlineData("A11")]
    [InlineData("E9")]
    [InlineData("J7")]
    [InlineData("R7")]
    [InlineData("ZC9")]
    [InlineData("X7")]
    public void UnknownLetter_Fails(string code)
    {
        var limits = Iso286.HoleLimits(6, code);
        Assert.False(limits.Succeeded);
        Assert.Contains("not supported", limits.Message);
    }

    [Theory]
    [InlineData("H4")]
    [InlineData("H12")]
    [InlineData("H01")]
    [InlineData("H0")]
    [InlineData("P99999999999")]
    public void UnknownGrade_Fails(string code)
    {
        var limits = Iso286.HoleLimits(6, code);
        Assert.False(limits.Succeeded);
        Assert.Contains("IT5 to IT11", limits.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("H")]
    [InlineData("7")]
    [InlineData("H7x")]
    [InlineData("H 7")]
    [InlineData("H-7")]
    public void MalformedCode_Fails(string? code)
    {
        var limits = Iso286.HoleLimits(6, code);
        Assert.False(limits.Succeeded);
        Assert.NotEqual("", limits.Message);
    }

    [Theory]
    [InlineData(6, "K9")]
    [InlineData(100, "K11")]
    public void KAboveIT8OverThreeMillimetres_Fails(double nominalMm, string code)
    {
        var limits = Iso286.HoleLimits(nominalMm, code);
        Assert.False(limits.Succeeded);
        Assert.Contains("over 3 mm", limits.Message);
    }

    [Fact]
    public void NAboveIT8UpToOneMillimetre_Fails()
    {
        var limits = Iso286.HoleLimits(1, "N9");
        Assert.False(limits.Succeeded);
        Assert.Contains("1 mm", limits.Message);
    }

    // Every covered combination gives a result without throwing, and the limits are one IT apart.

    [Fact]
    public void EveryCoveredCode_GivesLimitsOneToleranceApart()
    {
        foreach (var upper in Iso286.SizeStepUpperMm)
        foreach (var letter in Iso286.Letters)
        for (var grade = Iso286.MinGrade; grade <= Iso286.MaxGrade; grade++)
        {
            var limits = Iso286.HoleLimits(upper, letter + grade);
            if (!limits.Succeeded)
            {
                Assert.True(letter == "K" && grade > 8 && upper > 3, $"{letter}{grade} at {upper} mm: {limits.Message}");
                continue;
            }

            var it = Iso286.StandardToleranceUm(upper, grade);
            var width = limits.UpperUm - limits.LowerUm;
            if (letter == "JS")
                Assert.True(width == it || width == it - 1, $"JS{grade} at {upper} mm");
            else
                Assert.Equal(it, width);
        }
    }

    // The typed delta table agrees with the typed grades: delta = IT n - IT n-1.

    [Fact]
    public void DeltaTable_MatchesTheGrades()
    {
        foreach (var upper in Iso286.SizeStepUpperMm)
        {
            if (upper == 3)
            {
                for (var grade = 5; grade <= 8; grade++)
                    Assert.Equal(0, Iso286.DeltaUmFor(upper, grade));
                continue;
            }

            Assert.Equal(Iso286.StandardToleranceUm(upper, 5) - Iso286.It4Um(upper), Iso286.DeltaUmFor(upper, 5));
            for (var grade = 6; grade <= 8; grade++)
                Assert.Equal(
                    Iso286.StandardToleranceUm(upper, grade) - Iso286.StandardToleranceUm(upper, grade - 1),
                    Iso286.DeltaUmFor(upper, grade));
        }
    }

    [Fact]
    public void Grades_GrowWithSizeAndGrade()
    {
        double previousUpper = 0;
        foreach (var upper in Iso286.SizeStepUpperMm)
        {
            for (var grade = Iso286.MinGrade; grade < Iso286.MaxGrade; grade++)
                Assert.True(Iso286.StandardToleranceUm(upper, grade) < Iso286.StandardToleranceUm(upper, grade + 1));
            if (previousUpper > 0)
                for (var grade = Iso286.MinGrade; grade <= Iso286.MaxGrade; grade++)
                    Assert.True(Iso286.StandardToleranceUm(previousUpper, grade) <= Iso286.StandardToleranceUm(upper, grade));
            previousUpper = upper;
        }
    }

    [Theory]
    [InlineData(0, 7)]
    [InlineData(501, 7)]
    [InlineData(6, 4)]
    [InlineData(6, 12)]
    public void StandardTolerance_RejectsOutOfRange(double nominalMm, int grade)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Iso286.StandardToleranceUm(nominalMm, grade));
    }
}
