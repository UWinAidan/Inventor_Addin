using InventorAddin.Core.Holes;

namespace InventorAddin.Core.Tests.Holes;

/// <summary>
/// Toleranced Clearance and Simple holes and the Custom tolerance in Core (task 113; spec 09, "Toleranced clearance and
/// Simple holes"): the precision rule, the colour, built, checked, stored, read back and compared for the re-apply
/// offer. Sizes and values are invented.
/// </summary>
public sealed class HoleTolerancedTests
{
    private static readonly HoleWorkbook Workbook = HoleTestWorkbook.Create();

    private static HoleSpec TolClearance(string? tolerance = null) => HoleTestWorkbook.Clearance() with
    {
        Toleranced = true,
        Tolerance = tolerance,
    };

    private static HoleSpec Simple(decimal diameter = 6.5m) => HoleTestWorkbook.Clearance() with
    {
        Kind = HoleKind.Simple,
        Size = "",
        Diameter = diameter,
    };

    private static HoleSpec Custom(HoleSpec spec, decimal? upper, decimal? lower) => spec with
    {
        CustomTolerance = true,
        CustomUpper = upper,
        CustomLower = lower,
    };

    private static HoleBuildResult Build(HoleSpec spec) => HoleRules.Build(spec, Workbook, 1);

    private static HoleBuildResult Built(HoleSpec spec)
    {
        var result = Build(spec);
        Assert.True(result.Succeeded, string.Join(" | ", result.Problems));
        return result;
    }

    private static InventorHoleFacts FactsOf(HoleGeometry g) => new(
        g.Seat, false, null, null, null, g.HoleDiameterCm, g.Termination, g.DepthCm, g.CounterboreDiameterCm,
        g.CounterboreDepthCm, g.CountersinkDiameterCm, g.CountersinkAngleDeg, g.DrillPoint?.IsFlat ?? false,
        g.DrillPoint?.AngleDeg, g.Tolerance?.IsoCode, g.Tolerance?.UpperCm, g.Tolerance?.LowerCm, g.Flip);

    // ---- The precision rule ----

    [Theory]
    [InlineData(HoleKind.Clearance, true)]
    [InlineData(HoleKind.Simple, true)]
    [InlineData(HoleKind.Dowel, false)]
    [InlineData(HoleKind.Tapped, false)]
    [InlineData(HoleKind.TaperTapped, false)]
    public void CanBeToleranced_OnlyClearanceAndSimple(HoleKind kind, bool expected) =>
        Assert.Equal(expected, HolePrecision.CanBeToleranced(kind));

    [Theory]
    [InlineData(HoleKind.Dowel, false, true)]
    [InlineData(HoleKind.Clearance, false, false)]
    [InlineData(HoleKind.Clearance, true, true)]
    [InlineData(HoleKind.Simple, true, true)]
    [InlineData(HoleKind.Simple, false, false)]
    [InlineData(HoleKind.Tapped, true, false)]
    [InlineData(HoleKind.TaperTapped, true, false)]
    public void IsPrecision_ADowelOrATolerancedClearanceOrSimpleHole(HoleKind kind, bool toleranced, bool expected)
    {
        var spec = HoleTestWorkbook.Clearance() with { Kind = kind, Toleranced = toleranced };
        Assert.Equal(expected, HolePrecision.IsPrecision(spec));
        Assert.Equal(expected, spec.IsPrecision);
    }

    [Fact]
    public void IsPrecision_AHoleWithNoAwbData_GoesByInventorsTolerance()
    {
        Assert.True(HolePrecision.IsPrecision(null, inventorToleranceOnDiameter: true));
        Assert.False(HolePrecision.IsPrecision(null, inventorToleranceOnDiameter: false));
        // AWB data decides when there is some.
        Assert.False(HolePrecision.IsPrecision(HoleTestWorkbook.Clearance(), inventorToleranceOnDiameter: true));
        Assert.True(HolePrecision.IsPrecision(HoleTestWorkbook.Dowel(), inventorToleranceOnDiameter: false));
    }

    [Fact]
    public void ColourFit_IsTheRowsFit_NormalForCustom_TheDowelsOwnFit_AndNoneWhenNotPrecision()
    {
        var press = Workbook.PrecisionTolerances.Single(r => r.Name == "Test press");

        Assert.Equal(PrecisionFit.Press, HolePrecision.ColourFit(TolClearance(), press));
        Assert.Equal(PrecisionFit.Normal, HolePrecision.ColourFit(Custom(TolClearance(), 0.1m, 0m), press));
        Assert.Equal(
            PrecisionFit.Slip,
            HolePrecision.ColourFit(HoleTestWorkbook.Dowel() with { PrecisionFit = PrecisionFit.Slip }, press));
        Assert.Equal(
            PrecisionFit.Slip,
            HolePrecision.ColourFit(Custom(HoleTestWorkbook.Dowel() with { PrecisionFit = PrecisionFit.Slip }, 1, 0), null));
        Assert.Null(HolePrecision.ColourFit(HoleTestWorkbook.Clearance(), press));
        Assert.Null(HolePrecision.ColourFit(TolClearance(), null));
    }

    [Fact]
    public void Colour_IsTheSettingsColourOfTheColourFit()
    {
        var press = Workbook.PrecisionTolerances.Single(r => r.Name == "Test press");
        Assert.Equal(HoleSettings.Starter.PressFitColour, HolePrecision.Colour(TolClearance(), press, HoleSettings.Starter));
        Assert.Equal(
            HoleSettings.Starter.NormalFitColour,
            HolePrecision.Colour(Custom(TolClearance(), 0.1m, 0m), null, HoleSettings.Starter));
        Assert.Null(HolePrecision.Colour(HoleTestWorkbook.Clearance(), press, HoleSettings.Starter));
    }

    // ---- Spec ----

    [Fact]
    public void Normalised_DropsTolerancedOnAKindThatCannotTakeIt()
    {
        var tapped = HoleTestWorkbook.Tapped() with
        {
            Toleranced = true, Tolerance = "Test bore", CustomTolerance = true, CustomUpper = 1, CustomLower = 0,
        };
        var normalised = tapped.Normalised();

        Assert.False(normalised.Toleranced);
        Assert.Null(normalised.Tolerance);
        Assert.False(normalised.CustomTolerance);
        Assert.Null(normalised.CustomUpper);
        Assert.Null(normalised.CustomLower);
    }

    [Fact]
    public void Normalised_AnUntolerancedClearanceHole_HasNoToleranceOrCustomLimits()
    {
        var spec = HoleTestWorkbook.Clearance() with
        {
            Tolerance = "Test bore", PrecisionFit = PrecisionFit.Press, CustomTolerance = true, CustomUpper = 1,
        };
        var normalised = spec.Normalised();

        Assert.Null(normalised.Tolerance);
        Assert.Equal(PrecisionFit.Normal, normalised.PrecisionFit);
        Assert.False(normalised.CustomTolerance);
        Assert.Null(normalised.CustomUpper);
    }

    [Fact]
    public void Normalised_Custom_DropsTheRowName_AndATolerancedHolesFit()
    {
        var normalised = Custom(TolClearance("Test bore") with { PrecisionFit = PrecisionFit.Press }, 0.1m, 0m)
            .Normalised();

        Assert.True(normalised.Toleranced);
        Assert.True(normalised.CustomTolerance);
        Assert.Null(normalised.Tolerance);
        Assert.Equal(PrecisionFit.Normal, normalised.PrecisionFit);
        Assert.Equal(0.1m, normalised.CustomUpper);
        Assert.Equal(0m, normalised.CustomLower);
    }

    // ---- Rules ----

    [Fact]
    public void Build_ATolerancedClearanceHole_TakesTheFirstDefaultRowOfAnyFit_AtItsClearanceDiameter()
    {
        var result = Built(TolClearance());
        var g = result.Geometry!;

        // The first Default = Y row for Metric is "Test location" (G7, Normal fit).
        Assert.Equal("Test location", result.Resolved.Tolerance);
        Assert.Equal(PrecisionFit.Normal, result.Resolved.PrecisionFit);
        Assert.Equal("G7", g.Tolerance!.IsoCode);
        // G7 at the 5.5 mm Normal clearance diameter (3 to 6 mm): +0.016 / +0.004 mm.
        Assert.Equal(0.0016, g.Tolerance.UpperCm, 9);
        Assert.Equal(0.0004, g.Tolerance.LowerCm, 9);
        Assert.Equal(0.55, g.HoleDiameterCm, 9);
        Assert.Equal(HoleSettings.Starter.NormalFitColour, g.FitColour);
        Assert.True(g.IsColoured);
    }

    [Fact]
    public void Build_ATolerancedClearanceHole_TakesARowOfAnotherFit_AndItsColour()
    {
        var result = Built(TolClearance("Test press"));

        Assert.Equal(PrecisionFit.Press, result.Resolved.PrecisionFit);
        Assert.Equal(HoleSettings.Starter.PressFitColour, result.Geometry!.FitColour);
        Assert.Null(result.Geometry.Tolerance!.IsoCode);
        Assert.Equal(0, result.Geometry.Tolerance.UpperCm, 9);
        Assert.Equal(-0.002, result.Geometry.Tolerance.LowerCm, 9);
    }

    [Fact]
    public void Build_AnUntolerancedClearanceHole_HasNoToleranceOrColour()
    {
        var g = Built(HoleTestWorkbook.Clearance() with { Tolerance = "Test press" }).Geometry!;
        Assert.Null(g.Tolerance);
        Assert.Null(g.FitColour);
    }

    [Fact]
    public void Build_ATolerancedCounterbore_PutsTheToleranceOnTheThroughHole()
    {
        var spec = TolClearance("Test bore") with { Seat = HoleSeat.Counterbore, Fastener = "TCAP" };
        var g = Built(spec).Geometry!;

        // TCAP's through hole is the Normal clearance diameter, 5.5 mm; H7 there is +0.012 / 0.
        Assert.Equal(0.55, g.HoleDiameterCm, 9);
        Assert.Equal("H7", g.Tolerance!.IsoCode);
        Assert.Equal(0.0012, g.Tolerance.UpperCm, 9);
        Assert.Equal(0.95, g.CounterboreDiameterCm!.Value, 9);
    }

    [Fact]
    public void Build_ATolerancedSimpleHole_TakesTheToleranceAtItsDiameter()
    {
        var g = Built(Simple(6.5m) with { Toleranced = true, Tolerance = "Test bore" }).Geometry!;

        // H7 at 6.5 mm (6 to 10 mm): +0.015 / 0.
        Assert.Equal(0.0015, g.Tolerance!.UpperCm, 9);
        Assert.Equal(0, g.Tolerance.LowerCm, 9);
        Assert.Equal(HoleSettings.Starter.NormalFitColour, g.FitColour);
    }

    [Fact]
    public void Build_ATolerancedRowThatIsNotInTheTab_IsReportedUnderTolerance()
    {
        var result = Build(TolClearance("No such row"));
        Assert.False(result.Succeeded);
        Assert.Contains(result.Problems, p => p.Field == HoleField.Tolerance && p.Message.Contains("No such row"));
    }

    [Fact]
    public void Build_ATolerancedHole_WithNoRowsForItsUnits_IsReported()
    {
        var workbook = Workbook with { PrecisionTolerances = [] };
        var result = HoleRules.Build(TolClearance(), workbook, 1);
        Assert.Contains(result.Problems, p => p.Field == HoleField.Tolerance);
    }

    [Fact]
    public void Build_Custom_TakesTheLimits_AsDeviations_WithTheNormalColour()
    {
        var result = Built(Custom(TolClearance(), 0.05m, -0.01m));
        var g = result.Geometry!;

        Assert.Equal(HolePrecision.CustomName, g.Tolerance!.Name);
        Assert.Null(g.Tolerance.IsoCode);
        Assert.Equal(0.005, g.Tolerance.UpperCm, 9);
        Assert.Equal(-0.001, g.Tolerance.LowerCm, 9);
        Assert.Equal(HoleSettings.Starter.NormalFitColour, g.FitColour);
        Assert.Null(result.Resolved.Tolerance);
        Assert.True(result.Resolved.CustomTolerance);
    }

    [Fact]
    public void Build_CustomOnADowel_KeepsTheDowelsFitColour()
    {
        var spec = Custom(HoleTestWorkbook.Dowel() with { PrecisionFit = PrecisionFit.Press }, 0m, -0.02m);
        var g = Built(spec).Geometry!;

        Assert.Equal(HoleSettings.Starter.PressFitColour, g.FitColour);
        Assert.Equal(-0.002, g.Tolerance!.LowerCm, 9);
    }

    [Fact]
    public void Build_CustomInInches_IsConvertedToCentimetres()
    {
        var spec = Custom(HoleSpec.ClearanceHole(HoleUnits.Imperial, "#7") with { Toleranced = true }, 0.001m, 0m);
        Assert.Equal(0.00254, Built(spec).Geometry!.Tolerance!.UpperCm, 9);
    }

    [Fact]
    public void Build_CustomWithEmptyLimits_AsksForBoth()
    {
        var result = Build(Custom(TolClearance(), null, null));

        Assert.False(result.Succeeded);
        Assert.Contains(result.Problems, p => p.Field == HoleField.ToleranceUpper && p.Message == HoleRules.CustomUpperMissingText);
        Assert.Contains(result.Problems, p => p.Field == HoleField.ToleranceLower && p.Message == HoleRules.CustomLowerMissingText);
    }

    [Theory]
    [InlineData(0.01, 0.01)]
    [InlineData(-0.01, 0.01)]
    public void Build_CustomWithUpperNotAboveLower_IsRefused(double upper, double lower)
    {
        var result = Build(Custom(TolClearance(), (decimal)upper, (decimal)lower));

        Assert.False(result.Succeeded);
        var problem = Assert.Single(result.Problems);
        Assert.Equal(HoleField.ToleranceUpper, problem.Field);
        Assert.Contains("is not above the lower limit", problem.Message);
    }

    [Fact]
    public void FindAnyTolerance_PrefersTheGivenFit_WhenTwoRowsShareAName()
    {
        var workbook = Workbook with
        {
            PrecisionTolerances =
            [
                new PrecisionToleranceRow(5, HoleUnits.Metric, PrecisionFit.Normal, "Same", ToleranceKind.Iso, "H7", null, null, false, ""),
                new PrecisionToleranceRow(6, HoleUnits.Metric, PrecisionFit.Press, "Same", ToleranceKind.Iso, "P7", null, null, true, ""),
            ],
        };

        Assert.Equal(PrecisionFit.Normal, HoleRules.FindAnyTolerance(workbook, HoleUnits.Metric, "Same")!.Fit);
        Assert.Equal(PrecisionFit.Press, HoleRules.FindAnyTolerance(workbook, HoleUnits.Metric, "same", PrecisionFit.Press)!.Fit);
        Assert.Equal(PrecisionFit.Press, HoleRules.FindAnyTolerance(workbook, HoleUnits.Metric, null)!.Fit);
        Assert.Null(HoleRules.FindAnyTolerance(workbook, HoleUnits.Imperial, null));
    }

    // ---- Stored data ----

    public static TheoryData<HoleSpec> StoredSpecs() =>
    [
        TolClearance("Test press"),
        TolClearance("Test bore") with { Seat = HoleSeat.Counterbore, Fastener = "TCAP" },
        Simple() with { Toleranced = true, Tolerance = "Test location" },
        Custom(TolClearance(), 0.05m, -0.01m),
        Custom(Simple() with { Toleranced = true }, 0.02m, 0m),
        Custom(HoleTestWorkbook.Dowel() with { PrecisionFit = PrecisionFit.Slip }, 0.01m, 0m),
    ];

    [Theory]
    [MemberData(nameof(StoredSpecs))]
    public void StoredData_RoundTrips(HoleSpec spec)
    {
        var build = Built(spec);
        var stored = new HoleStoredData(build.Resolved, HoleAppliedState.From(build.Geometry!));

        var read = HoleStoredData.Read(stored.ToAttributes());

        Assert.True(read.Succeeded, read.Message);
        Assert.Equal(build.Resolved.Normalised(), read.Data!.Spec);
        Assert.Equal(stored.Applied, read.Data.Applied);
    }

    [Fact]
    public void StoredData_AToleranceHole_IsTheCurrentVersion_WithItsFlagRowAndFit()
    {
        var build = Built(TolClearance("Test press"));
        var byName = new HoleStoredData(build.Resolved, HoleAppliedState.From(build.Geometry!))
            .ToAttributes().ToDictionary(a => a.Name, a => a.Value);

        Assert.Equal(HoleStoredData.CurrentVersion, byName[HoleStoredData.VersionName]);
        Assert.Equal(1, byName[HoleStoredData.TolerancedName]);
        Assert.Equal("Test press", byName[HoleStoredData.ToleranceName]);
        Assert.Equal("Press", byName[HoleStoredData.PrecisionFitName]);
        Assert.False(byName.ContainsKey(HoleStoredData.CustomToleranceName));
        Assert.Equal(1, byName[HoleStoredData.AppliedColouredName]);
    }

    [Fact]
    public void StoredData_AnUntolerancedClearanceHole_WritesNoToleranceAttributes()
    {
        var names = new HoleStoredData(HoleTestWorkbook.Clearance(), new HoleAppliedState(0.55, null, null, null, false))
            .ToAttributes().Select(a => a.Name).ToList();

        Assert.DoesNotContain(HoleStoredData.TolerancedName, names);
        Assert.DoesNotContain(HoleStoredData.PrecisionFitName, names);
        Assert.DoesNotContain(HoleStoredData.CustomToleranceName, names);
        Assert.DoesNotContain(HoleStoredData.CustomUpperName, names);
    }

    [Fact]
    public void StoredData_CustomWithoutItsLimits_CannotBeStored()
    {
        var stored = new HoleStoredData(
            Custom(TolClearance(), 0.05m, null), new HoleAppliedState(0.55, null, null, null, true));
        Assert.Throws<ArgumentException>(() => stored.ToAttributes());
    }

    [Fact]
    public void StoredData_ATolerancedHoleWithoutItsRow_CannotBeStored()
    {
        var stored = new HoleStoredData(TolClearance(), new HoleAppliedState(0.55, null, null, null, true));
        Assert.Throws<ArgumentException>(() => stored.ToAttributes());
    }

    [Fact]
    public void StoredData_Version3_IgnoresATolerancedFlag()
    {
        var build = Built(TolClearance("Test press"));
        var attributes = new HoleStoredData(build.Resolved, HoleAppliedState.From(build.Geometry!)).ToAttributes()
            .Select(a => a.Name == HoleStoredData.VersionName ? new HoleAttribute(a.Name, 3) : a);

        var read = HoleStoredData.Read(attributes);

        Assert.True(read.Succeeded, read.Message);
        Assert.False(read.Data!.Spec.Toleranced);
        Assert.Null(read.Data.Spec.Tolerance);
    }

    [Fact]
    public void StoredData_CustomWithAMissingLimit_CannotBeRead()
    {
        var build = Built(Custom(TolClearance(), 0.05m, -0.01m));
        var attributes = new HoleStoredData(build.Resolved, HoleAppliedState.From(build.Geometry!)).ToAttributes()
            .Where(a => a.Name != HoleStoredData.CustomLowerName);

        var read = HoleStoredData.Read(attributes);

        Assert.False(read.Succeeded);
        Assert.Contains(HoleStoredData.CustomLowerName, read.Message);
    }

    // ---- Read back from Inventor ----

    [Fact]
    public void ReadBack_AClearanceHoleWithARowsTolerance_OpensTolerancedOnThatRow()
    {
        var g = Built(TolClearance("Test press")).Geometry!;

        var read = HoleReadBack.Read(null, FactsOf(g), Workbook, HoleUnits.Metric);

        Assert.Equal(HoleKind.Clearance, read.Spec.Kind);
        Assert.True(read.Spec.Toleranced);
        Assert.Equal("Test press", read.Spec.Tolerance);
        Assert.Equal(PrecisionFit.Press, read.Spec.PrecisionFit);
        Assert.Empty(read.Notes);
    }

    [Fact]
    public void ReadBack_AClearanceHoleWithLimitsNoRowHas_OpensAsCustom()
    {
        var g = Built(Custom(TolClearance(), 0.05m, -0.01m)).Geometry!;

        var read = HoleReadBack.Read(null, FactsOf(g), Workbook, HoleUnits.Metric);

        Assert.True(read.Spec.Toleranced);
        Assert.True(read.Spec.CustomTolerance);
        Assert.Equal(0.05m, read.Spec.CustomUpper);
        Assert.Equal(-0.01m, read.Spec.CustomLower);
        Assert.Contains(read.Notes, n => n.Contains("Custom"));
        Assert.True(HoleRules.Build(read.Spec, Workbook, 1).Succeeded);
    }

    [Fact]
    public void ReadBack_ASimpleHoleWithATolerance_OpensToleranced()
    {
        var g = Built(Simple(6.5m) with { Toleranced = true, Tolerance = "Test bore" }).Geometry!;

        var read = HoleReadBack.Read(null, FactsOf(g), Workbook, HoleUnits.Metric);

        Assert.Equal(HoleKind.Simple, read.Spec.Kind);
        Assert.True(read.Spec.Toleranced);
        Assert.Equal("Test bore", read.Spec.Tolerance);
    }

    [Fact]
    public void ReadBack_AnIsoCodeNoRowHas_OpensAsACustomCode_WithANote()
    {
        // Task 150: before, the default row was taken.
        var facts = FactsOf(Built(HoleTestWorkbook.Clearance()).Geometry!) with { ToleranceIsoCode = "K6" };

        var read = HoleReadBack.Read(null, facts, Workbook, HoleUnits.Metric);

        Assert.True(read.Spec.Toleranced);
        Assert.True(read.Spec.CustomTolerance);
        Assert.Equal("K6", read.Spec.CustomCode);
        Assert.Contains(read.Notes, n => n.Contains("Custom"));
    }

    [Fact]
    public void ReadBack_ADowelWithLimitsNoRowHas_OpensAsCustom()
    {
        var g = Built(Custom(HoleTestWorkbook.Dowel(), 0.03m, 0.01m)).Geometry!;

        var read = HoleReadBack.Read(null, FactsOf(g), Workbook, HoleUnits.Metric);

        Assert.Equal(HoleKind.Dowel, read.Spec.Kind);
        Assert.True(read.Spec.CustomTolerance);
        Assert.Equal(0.03m, read.Spec.CustomUpper);
        Assert.Equal(0.01m, read.Spec.CustomLower);
    }

    [Fact]
    public void ReadBack_FromStoredData_KeepsTheToleranceAsStored()
    {
        var build = Built(Custom(TolClearance(), 0.05m, -0.01m));
        var stored = new HoleStoredData(build.Resolved, HoleAppliedState.From(build.Geometry!));

        var read = HoleReadBack.Read(
            HoleStoredData.Read(stored.ToAttributes()), FactsOf(build.Geometry!), Workbook, HoleUnits.Metric);

        Assert.Equal(HoleSource.Awb, read.Source);
        Assert.Equal(build.Resolved.Normalised(), read.Spec);
    }

    // ---- Re-apply offer ----

    [Fact]
    public void Reapply_ATolerancedHoleWhoseToleranceAndColourWereRemoved_IsOffered()
    {
        var build = Built(TolClearance("Test press"));
        var stored = new HoleStoredData(build.Resolved, HoleAppliedState.From(build.Geometry!));
        var facts = FactsOf(build.Geometry!) with { ToleranceIsoCode = null, ToleranceUpperCm = null, ToleranceLowerCm = null };

        var offer = HoleReapplyRules.Check(stored, facts, coloured: false);

        Assert.True(offer.IsOffered);
        Assert.Equal(2, offer.Differences.Count);
    }

    [Fact]
    public void Reapply_ATolerancedHoleAsMade_IsNotOffered()
    {
        var build = Built(Custom(TolClearance(), 0.05m, -0.01m));
        var stored = new HoleStoredData(build.Resolved, HoleAppliedState.From(build.Geometry!));

        Assert.False(HoleReapplyRules.Check(stored, FactsOf(build.Geometry!), coloured: true).IsOffered);
    }

    // ---- Face reset on OK ----

    [Theory]
    [InlineData(true, true, false, false, true)] // unticked, or moved to a kind that cannot be toleranced
    [InlineData(true, true, true, true, false)] // kept toleranced
    [InlineData(true, false, true, false, true)] // opened with a fit colour
    [InlineData(true, false, false, false, false)]
    [InlineData(false, true, true, false, false)] // a new hole has nothing to reset
    public void ResetsFaceAppearances_ByThePrecisionRule(
        bool edit, bool openedPrecision, bool openedFitColoured, bool keptPrecision, bool expected) =>
        Assert.Equal(
            expected,
            HoleSessionRules.ResetsFaceAppearances(edit, openedPrecision, openedFitColoured, keptPrecision));

    // ---- Diagram ----

    [Fact]
    public void Diagram_ShowsATolerancedHolesToleranceAfterItsDiameter()
    {
        var input = new HoleDiagramInput
        {
            Units = HoleUnits.Metric, Kind = HoleKind.Clearance, HoleDiameter = 5.5m, Tolerance = "+0.050 / -0.010",
        };

        Assert.Contains("+0.050 / -0.010", HoleDiagram.Summary(input));
    }

    // ---- The colour from the workbook (task 138: the window's swatch takes the colour the hole is built with) ----

    [Theory]
    [InlineData(null)]
    [InlineData("Test bore")]
    [InlineData("Test location")]
    [InlineData("Test press")]
    public void Colour_FromTheWorkbook_IsTheColourTheHoleIsBuiltWith(string? tolerance)
    {
        var spec = TolClearance(tolerance).Normalised();

        var built = Built(spec);

        Assert.NotNull(built.Geometry!.FitColour);
        Assert.Equal(built.Geometry.FitColour, HolePrecision.ColourIn(spec, Workbook));
    }

    [Fact]
    public void Colour_FromTheWorkbook_OfASimpleHole_IsTheColourItIsBuiltWith()
    {
        var spec = (Simple() with { Toleranced = true, Tolerance = "Test press" }).Normalised();

        Assert.Equal(HoleSettings.Starter.PressFitColour, HolePrecision.ColourIn(spec, Workbook));
        Assert.Equal(Built(spec).Geometry!.FitColour, HolePrecision.ColourIn(spec, Workbook));
    }

    [Fact]
    public void Colour_FromTheWorkbook_OfACustomTolerance_IsNormal_EvenBeforeItsLimitsAreTyped()
    {
        var typed = Custom(TolClearance(), 0.02m, 0m).Normalised();
        var empty = Custom(TolClearance(), null, null).Normalised();

        Assert.Equal(PrecisionFit.Normal, HolePrecision.ColourFitIn(empty, Workbook));
        Assert.Equal(HoleSettings.Starter.NormalFitColour, HolePrecision.ColourIn(empty, Workbook));
        Assert.Equal(Built(typed).Geometry!.FitColour, HolePrecision.ColourIn(typed, Workbook));
        Assert.Null(HolePrecision.TolerancedRow(typed, Workbook));
    }

    [Fact]
    public void TolerancedRow_IsTheNamedRowForTheUnits_OrTheDefaultRow()
    {
        Assert.Equal("Test press", HolePrecision.TolerancedRow(TolClearance("Test press").Normalised(), Workbook)!.Name);
        Assert.Equal("Test location", HolePrecision.TolerancedRow(TolClearance().Normalised(), Workbook)!.Name);
        Assert.Null(HolePrecision.TolerancedRow(TolClearance("No such row").Normalised(), Workbook));
    }

    [Fact]
    public void Colour_FromTheWorkbook_IsNullForARowThatIsNotThere_AndForAHoleThatIsNotToleranced()
    {
        Assert.Null(HolePrecision.ColourIn(TolClearance("No such row").Normalised(), Workbook));
        Assert.Null(HolePrecision.ColourIn(HoleTestWorkbook.Clearance(), Workbook));
        Assert.Null(HolePrecision.TolerancedRow(HoleTestWorkbook.Clearance(), Workbook));
    }

    [Fact]
    public void Colour_FromTheWorkbook_OfADowel_IsItsFitsColour()
    {
        var dowel = HoleTestWorkbook.Clearance() with { Kind = HoleKind.Dowel, PrecisionFit = PrecisionFit.Slip };

        Assert.Null(HolePrecision.TolerancedRow(dowel, Workbook));
        Assert.Equal(HoleSettings.Starter.SlipFitColour, HolePrecision.ColourIn(dowel, Workbook));
    }
}
