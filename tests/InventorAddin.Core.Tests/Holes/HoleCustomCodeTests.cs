using InventorAddin.Core.Holes;

namespace InventorAddin.Core.Tests.Holes;

/// <summary>
/// A Custom tolerance given as a typed ISO 286 code (task 150; spec 09, "Toleranced clearance and Simple holes"): the
/// shaft classes in <see cref="Iso286"/>, the code built, checked, stored, read back and compared. Sizes are invented.
/// </summary>
public sealed class HoleCustomCodeTests
{
    private static readonly HoleWorkbook Workbook = HoleTestWorkbook.Create();

    private static HoleSpec TolClearance() => HoleTestWorkbook.Clearance() with { Toleranced = true };

    private static HoleSpec TolSimple(decimal diameter = 6.5m) => HoleTestWorkbook.Clearance() with
    {
        Kind = HoleKind.Simple,
        Size = "",
        Diameter = diameter,
        Toleranced = true,
    };

    private static HoleSpec Code(HoleSpec spec, string? code) => spec with { CustomTolerance = true, CustomCode = code };

    private static HoleBuildResult Built(HoleSpec spec)
    {
        var result = HoleRules.Build(spec, Workbook, 1);
        Assert.True(result.Succeeded, string.Join(" | ", result.Problems));
        return result;
    }

    private static InventorHoleFacts FactsOf(HoleGeometry g) => new(
        g.Seat, false, null, null, null, g.HoleDiameterCm, g.Termination, g.DepthCm, g.CounterboreDiameterCm,
        g.CounterboreDepthCm, g.CountersinkDiameterCm, g.CountersinkAngleDeg, g.DrillPoint?.IsFlat ?? false,
        g.DrillPoint?.AngleDeg, g.Tolerance?.IsoCode, g.Tolerance?.UpperCm, g.Tolerance?.LowerCm, g.Flip);

    // ---- Iso286: shaft classes and either case ----

    // Values from the ISO 286-2 tables of limits for shafts.
    [Theory]
    [InlineData(6, "h7", 0.0, -0.012)]
    [InlineData(6, "k6", 0.009, 0.001)]
    [InlineData(6, "p6", 0.020, 0.012)]
    [InlineData(6, "js6", 0.004, -0.004)]
    [InlineData(10, "f7", -0.013, -0.028)]
    [InlineData(20, "g6", -0.007, -0.020)]
    [InlineData(20, "k6", 0.015, 0.002)]
    [InlineData(20, "k8", 0.033, 0.0)]     // above IT7, ei = 0
    [InlineData(20, "m6", 0.021, 0.008)]
    [InlineData(20, "n6", 0.028, 0.015)]
    [InlineData(2, "k6", 0.006, 0.0)]
    [InlineData(50, "h9", 0.0, -0.062)]
    public void ShaftLimits_KnownValues(double nominalMm, string code, double upper, double lower)
    {
        var limits = Iso286.ShaftLimits(nominalMm, code);

        Assert.True(limits.Succeeded, limits.Message);
        Assert.Equal(upper, limits.UpperMm, 9);
        Assert.Equal(lower, limits.LowerMm, 9);
        Assert.Equal(limits, Iso286.Limits(nominalMm, code));
    }

    [Fact]
    public void Limits_KeepsTheCase_H7IsAHoleAndH7LowercaseAShaft()
    {
        var hole = Iso286.Limits(6, "H7");
        var shaft = Iso286.Limits(6, "h7");

        Assert.Equal(("H7", 0.012, 0.0), (hole.Code, hole.UpperMm, hole.LowerMm));
        Assert.Equal(("h7", 0.0, -0.012), (shaft.Code, shaft.UpperMm, shaft.LowerMm));
        Assert.False(Iso286.IsShaftCode("H7"));
        Assert.True(Iso286.IsShaftCode(" k6"));
    }

    [Theory]
    [InlineData("Z9x")]
    [InlineData("Js7")]
    [InlineData("hH7")]
    [InlineData("7")]
    [InlineData("H")]
    [InlineData("z6")]
    [InlineData("h12")]
    [InlineData("")]
    public void Limits_RefusesWhatIsNotACoveredCode(string code)
    {
        var limits = Iso286.Limits(6, code);

        Assert.False(limits.Succeeded);
        Assert.NotEqual("", limits.Message);
    }

    [Fact]
    public void ShaftLimits_RefusesAHoleClass_AndHoleLimitsStillRefusesAShaftClass()
    {
        Assert.False(Iso286.ShaftLimits(6, "H7").Succeeded);
        Assert.False(Iso286.HoleLimits(6, "h7").Succeeded);
    }

    // ---- Rules ----

    [Fact]
    public void Build_AHoleCode_OnATolerancedClearanceHole_IsAnIsoFit_AtItsClearanceDiameter_WithTheNormalColour()
    {
        // T5 Normal is 5.5 mm: H8 there (3 to 6 mm) is +0.018 / 0.
        var build = Built(Code(TolClearance(), "H8"));
        var tolerance = build.Geometry!.Tolerance!;

        Assert.Equal(HolePrecision.CustomName, tolerance.Name);
        Assert.Equal("H8", tolerance.IsoCode);
        Assert.False(tolerance.IsShaftCode);
        Assert.Equal(0.0018, tolerance.UpperCm, 9);
        Assert.Equal(0.0, tolerance.LowerCm, 9);
        Assert.Equal(HoleSettings.Starter.NormalFitColour, build.Geometry.FitColour);
        Assert.Equal("H8", build.Resolved.CustomCode);
        Assert.Null(build.Resolved.CustomUpper);
    }

    [Fact]
    public void Build_AShaftCode_OnASimpleHole_KeepsItsCase_AtItsDiameter()
    {
        // k6 at 6.5 mm (6 to 10 mm): +0.010 / +0.001.
        var tolerance = Built(Code(TolSimple(6.5m), "k6")).Geometry!.Tolerance!;

        Assert.Equal("k6", tolerance.IsoCode);
        Assert.True(tolerance.IsShaftCode);
        Assert.Equal(0.0010, tolerance.UpperCm, 9);
        Assert.Equal(0.0001, tolerance.LowerCm, 9);
    }

    [Fact]
    public void Build_ACodeOnADowel_KeepsTheDowelsFitColour()
    {
        var build = Built(Code(HoleTestWorkbook.Dowel() with { PrecisionFit = PrecisionFit.Press }, "P7"));

        Assert.Equal("P7", build.Geometry!.Tolerance!.IsoCode);
        Assert.Equal(HoleSettings.Starter.PressFitColour, build.Geometry.FitColour);
    }

    [Fact]
    public void Build_ACodeIgnoresTypedLimits()
    {
        var spec = Code(TolClearance(), "H7") with { CustomUpper = 0.01m, CustomLower = 0.02m };

        var build = Built(spec);

        Assert.Equal("H7", build.Geometry!.Tolerance!.IsoCode);
        Assert.Null(build.Resolved.CustomUpper);
        Assert.Null(build.Resolved.CustomLower);
    }

    [Theory]
    [InlineData("Z9x")]
    [InlineData("Js7")]
    public void Build_AnInvalidCode_IsReportedUnderTheCodeBox(string code)
    {
        var build = HoleRules.Build(Code(TolClearance(), code), Workbook, 1);

        Assert.False(build.Succeeded);
        Assert.Contains(build.Problems, p => p.Field == HoleField.ToleranceCode);
        Assert.DoesNotContain(build.Problems, p => p.Field is HoleField.ToleranceUpper or HoleField.ToleranceLower);
    }

    [Fact]
    public void Build_ACodeWithNoLimitsAtTheSize_IsReportedUnderTheCodeBox()
    {
        // K9 has limits only up to 3 mm, and nothing has limits at 900 mm.
        var atSize = HoleRules.Build(Code(TolClearance(), "K9"), Workbook, 1);
        var tooBig = HoleRules.Build(Code(HoleTestWorkbook.Dowel("900 mm"), "H7"), Workbook, 1);

        Assert.Contains(atSize.Problems, p => p.Field == HoleField.ToleranceCode && p.Message.Contains("K9"));
        Assert.Contains(tooBig.Problems, p => p.Field == HoleField.ToleranceCode && p.Message.Contains("500"));
    }

    [Fact]
    public void Build_ASizeChange_WorksTheLimitsOutAgain()
    {
        var small = Built(Code(TolSimple(5m), "H7")).Geometry!.Tolerance!;
        var large = Built(Code(TolSimple(20m), "H7")).Geometry!.Tolerance!;

        Assert.Equal(0.0012, small.UpperCm, 9);
        Assert.Equal(0.0021, large.UpperCm, 9);
    }

    [Fact]
    public void Build_InInches_WorksTheCodeOutAtTheSizeInMillimetres()
    {
        // 0.25 in is 6.35 mm (6 to 10 mm): H7 is +0.015 mm.
        var spec = Code(TolSimple(0.25m) with { Units = HoleUnits.Imperial }, "H7");

        Assert.Equal(0.0015, Built(spec).Geometry!.Tolerance!.UpperCm, 9);
    }

    // ---- The spec ----

    [Fact]
    public void Normalised_TrimsTheCode_KeepsItsCase_AndDropsTheTypedLimits()
    {
        var spec = Code(TolClearance(), "  k6 ") with { CustomUpper = 0.01m, CustomLower = 0m };

        var normalised = spec.Normalised();

        Assert.Equal("k6", normalised.CustomCode);
        Assert.Null(normalised.CustomUpper);
        Assert.Null(normalised.CustomLower);
        Assert.True(normalised.HasCustomCode);
    }

    [Fact]
    public void Normalised_ABlankCode_IsNone_AndARowDropsTheCode()
    {
        var blank = Code(TolClearance(), "  ") with { CustomUpper = 0.01m, CustomLower = 0m };
        Assert.Null(blank.Normalised().CustomCode);
        Assert.Equal(0.01m, blank.Normalised().CustomUpper);

        var row = TolClearance() with { Tolerance = "Test bore", CustomCode = "H7" };
        Assert.Null(row.Normalised().CustomCode);
    }

    // ---- Stored data ----

    public static TheoryData<HoleSpec> CodeSpecs() =>
    [
        Code(TolClearance(), "H8"),
        Code(TolSimple(), "k6"),
        Code(TolSimple(), "h7"),
        Code(HoleTestWorkbook.Dowel() with { PrecisionFit = PrecisionFit.Slip }, "G6"),
    ];

    [Theory]
    [MemberData(nameof(CodeSpecs))]
    public void StoredData_RoundTrips_WithTheCodeInItsCase(HoleSpec spec)
    {
        var build = Built(spec);
        var stored = new HoleStoredData(build.Resolved, HoleAppliedState.From(build.Geometry!));

        var read = HoleStoredData.Read(stored.ToAttributes());

        Assert.True(read.Succeeded, read.Message);
        Assert.Equal(build.Resolved.Normalised(), read.Data!.Spec);
        Assert.Equal(spec.CustomCode, read.Data.Spec.CustomCode);
        Assert.Equal(stored.Applied, read.Data.Applied);
    }

    [Fact]
    public void StoredData_ACode_IsVersion6_WithTheCodeAndNoLimits()
    {
        var build = Built(Code(TolSimple(), "k6"));
        var byName = new HoleStoredData(build.Resolved, HoleAppliedState.From(build.Geometry!))
            .ToAttributes().ToDictionary(a => a.Name, a => a.Value);

        Assert.Equal(6, HoleStoredData.CurrentVersion);
        Assert.Equal(6, byName[HoleStoredData.VersionName]);
        Assert.Equal(1, byName[HoleStoredData.CustomToleranceName]);
        Assert.Equal("k6", byName[HoleStoredData.CustomCodeName]);
        Assert.Equal("k6", byName[HoleStoredData.AppliedIsoCodeName]);
        Assert.False(byName.ContainsKey(HoleStoredData.CustomUpperName));
        Assert.False(byName.ContainsKey(HoleStoredData.CustomLowerName));
    }

    [Fact]
    public void StoredData_CustomLimits_StoreNoCode()
    {
        var build = Built(TolClearance() with { CustomTolerance = true, CustomUpper = 0.02m, CustomLower = 0m });
        var names = new HoleStoredData(build.Resolved, HoleAppliedState.From(build.Geometry!))
            .ToAttributes().Select(a => a.Name).ToList();

        Assert.DoesNotContain(HoleStoredData.CustomCodeName, names);
        Assert.Contains(HoleStoredData.CustomUpperName, names);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    public void StoredData_OlderCustomLimits_StillRead(int version)
    {
        var build = Built(TolClearance() with { CustomTolerance = true, CustomUpper = 0.02m, CustomLower = -0.01m });
        var attributes = new HoleStoredData(build.Resolved, HoleAppliedState.From(build.Geometry!)).ToAttributes()
            .Select(a => a.Name == HoleStoredData.VersionName ? new HoleAttribute(a.Name, version) : a);

        var read = HoleStoredData.Read(attributes);

        Assert.True(read.Succeeded, read.Message);
        Assert.Equal(version, read.Data!.Version);
        Assert.True(read.Data.Spec.CustomTolerance);
        Assert.Null(read.Data.Spec.CustomCode);
        Assert.Equal(0.02m, read.Data.Spec.CustomUpper);
        Assert.Equal(-0.01m, read.Data.Spec.CustomLower);
    }

    [Fact]
    public void StoredData_ACodeInDataBeforeVersion6_IsNotRead_SoItsMissingLimitsAreReported()
    {
        var build = Built(Code(TolClearance(), "H8"));
        var attributes = new HoleStoredData(build.Resolved, HoleAppliedState.From(build.Geometry!)).ToAttributes()
            .Select(a => a.Name == HoleStoredData.VersionName ? new HoleAttribute(a.Name, 5) : a);

        var read = HoleStoredData.Read(attributes);

        Assert.False(read.Succeeded);
        Assert.Contains(HoleStoredData.CustomUpperName, read.Message);
    }

    // ---- Read back from Inventor, and the re-apply offer ----

    [Fact]
    public void ReadBack_AClearanceHoleWithAnIsoCodeNoRowHas_OpensAsCustomWithThatCode()
    {
        var facts = FactsOf(Built(HoleTestWorkbook.Clearance()).Geometry!) with { ToleranceIsoCode = "K6" };

        var read = HoleReadBack.Read(null, facts, Workbook, HoleUnits.Metric);

        Assert.True(read.Spec.Toleranced);
        Assert.True(read.Spec.CustomTolerance);
        Assert.Equal("K6", read.Spec.CustomCode);
        Assert.Null(read.Spec.Tolerance);
        Assert.Contains(read.Notes, n => n.Contains("Custom"));
        Assert.Equal("K6", HoleRules.Build(read.Spec, Workbook, 1).Geometry!.Tolerance!.IsoCode);
    }

    [Fact]
    public void ReadBack_AShaftCode_KeepsItsCase_AndIsNotTakenForTheRowOfTheHoleClass()
    {
        // The workbook has an H7 row; h7 is a different class.
        var facts = FactsOf(Built(HoleTestWorkbook.Clearance()).Geometry!) with { ToleranceIsoCode = "h7" };

        var read = HoleReadBack.Read(null, facts, Workbook, HoleUnits.Metric);

        Assert.Equal("h7", read.Spec.CustomCode);
        Assert.Null(read.Spec.Tolerance);
    }

    [Fact]
    public void ReadBack_ADowelWithAnIsoCodeNoRowHas_OpensAsCustomWithThatCode()
    {
        var g = Built(Code(HoleTestWorkbook.Dowel(), "F8")).Geometry!;

        var read = HoleReadBack.Read(null, FactsOf(g), Workbook, HoleUnits.Metric);

        Assert.Equal(HoleKind.Dowel, read.Spec.Kind);
        Assert.True(read.Spec.CustomTolerance);
        Assert.Equal("F8", read.Spec.CustomCode);
        Assert.Null(read.Spec.CustomUpper);
    }

    [Fact]
    public void ReadBack_ARowsCode_StillOpensOnTheRow()
    {
        var facts = FactsOf(Built(HoleTestWorkbook.Clearance()).Geometry!) with { ToleranceIsoCode = "H7" };

        var read = HoleReadBack.Read(null, facts, Workbook, HoleUnits.Metric);

        Assert.Equal("Test bore", read.Spec.Tolerance);
        Assert.False(read.Spec.CustomTolerance);
    }

    [Fact]
    public void ReadBack_FromStoredData_OpensWithTheCode()
    {
        var build = Built(Code(TolSimple(), "k6"));
        var stored = new HoleStoredData(build.Resolved, HoleAppliedState.From(build.Geometry!));

        var read = HoleReadBack.Read(
            HoleStoredData.Read(stored.ToAttributes()), FactsOf(build.Geometry!), Workbook, HoleUnits.Metric);

        Assert.Equal("k6", read.Spec.CustomCode);
        Assert.True(read.Spec.CustomTolerance);
    }

    [Fact]
    public void Compare_AStoredCodeAndTheSameCodeInAnotherCase_Differ()
    {
        var stored = new HoleAppliedState(0.65, "h7", 0, -0.0012, true);

        Assert.True(HoleStateComparison.Compare(stored, stored with { }).Matches);
        Assert.False(HoleStateComparison.Compare(stored, stored with { ToleranceIsoCode = "H7" }).Matches);
    }
}
