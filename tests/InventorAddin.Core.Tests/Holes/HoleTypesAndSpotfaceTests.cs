using InventorAddin.Core.Holes;
using InventorAddin.Core.Settings;

namespace InventorAddin.Core.Tests.Holes;

/// <summary>
/// The Simple and Taper Tapped types and the Spotface seat in Core (task 112; spec 09, "Types and seats", "Simple
/// holes", "Taper tapped holes"): built, checked, stored, read back, compared for the re-apply offer, remembered and
/// drawn. Sizes and values are invented.
/// </summary>
public sealed class HoleTypesAndSpotfaceTests
{
    // T5 (5 mm nominal) has a Spotface row, so M5 and the 5 mm dowel take it too; #7 has a hidden one and a shown one.
    private static readonly HoleWorkbook Workbook = HoleTestWorkbook.Create() with
    {
        Spotface =
        [
            new SpotfaceRow(5, HoleUnits.Metric, "T5", 11m, 0.5m, false),
            new SpotfaceRow(6, HoleUnits.Imperial, "#7", 0.5m, 0.03m, true),
            new SpotfaceRow(7, HoleUnits.Imperial, "#7", 0.4m, 0.02m, false),
        ],
        TaperTapped =
        [
            new TaperTappedRow(5, HoleUnits.Metric, "Rc 1/4", "BSP Taper Internal", 11.2m, 16.8m, 22.4m, false),
            new TaperTappedRow(6, HoleUnits.Imperial, "1/4", "NPT", 0.44m, 0.66m, 0.88m, false),
        ],
    };

    private static HoleSpec Simple(decimal? diameter = 6.5m) => HoleTestWorkbook.Clearance() with
    {
        Kind = HoleKind.Simple,
        Size = "",
        Diameter = diameter,
    };

    private static HoleSpec Taper(string size = "Rc 1/4") => HoleTestWorkbook.Clearance() with
    {
        Kind = HoleKind.TaperTapped,
        Size = size,
        Termination = HoleTermination.Distance,
    };

    private static HoleBuildResult Build(HoleSpec spec) => HoleRules.Build(spec, Workbook, 1);

    private static HoleBuildResult Built(HoleSpec spec)
    {
        var result = Build(spec);
        Assert.True(result.Succeeded, string.Join(" | ", result.Problems));
        return result;
    }

    private static InventorHoleFacts FactsOf(HoleGeometry g) => new(
        g.Seat,
        g.Thread is not null,
        g.Thread?.Designation,
        g.Thread?.ThreadType,
        g.Thread is { FullDepth: false, IsTapered: false } thread ? thread.ThreadDepthCm : null,
        g.HoleDiameterCm,
        g.Termination,
        g.DepthCm,
        g.CounterboreDiameterCm,
        g.CounterboreDepthCm,
        g.CountersinkDiameterCm,
        g.CountersinkAngleDeg,
        g.DrillPoint?.IsFlat ?? false,
        g.DrillPoint?.AngleDeg,
        g.Tolerance?.IsoCode,
        g.Tolerance?.UpperCm,
        g.Tolerance?.LowerCm,
        g.Flip)
    {
        TaperedThread = g.Thread?.IsTapered ?? false,
        SpotfaceDiameterCm = g.SpotfaceDiameterCm,
        SpotfaceDepthCm = g.SpotfaceDepthCm,
    };

    // ---- Spec ----

    [Fact]
    public void Normalised_ASimpleHole_HasNoSizeFitOrFastener_ButKeepsItsDiameter()
    {
        var spec = (Simple() with
        {
            Size = "T5",
            Fastener = "TCAP",
            ClearanceFit = ClearanceFit.Loose,
            DiameterExpression = " d0 ",
            ThreadDepth = 3m,
        }).Normalised();

        Assert.Equal("", spec.Size);
        Assert.Null(spec.Fastener);
        Assert.Equal(ClearanceFit.Normal, spec.ClearanceFit);
        Assert.Equal(6.5m, spec.Diameter);
        Assert.Equal("d0", spec.DiameterExpression);
        Assert.Null(spec.ThreadDepth);
    }

    [Fact]
    public void Normalised_ADiameter_GoesWithASimpleHoleOnly()
    {
        var spec = (HoleTestWorkbook.Clearance() with { Diameter = 4m, DiameterExpression = "d0" }).Normalised();

        Assert.Null(spec.Diameter);
        Assert.Null(spec.DiameterExpression);
    }

    [Fact]
    public void Normalised_ATaperTappedHole_KeepsItsDrillDepth_AndHasNoThreadDepth()
    {
        var spec = (Taper() with { ThreadDepth = 9m, DrillDepth = 20m, DrillDepthExpression = "d0", Depth = 4m })
            .Normalised();

        Assert.Null(spec.ThreadDepth);
        Assert.Equal(20m, spec.DrillDepth);
        Assert.Equal("d0", spec.DrillDepthExpression);
        Assert.Null(spec.Depth);
    }

    [Fact]
    public void Normalised_ASpotface_KeepsItsSizesAndTheFit_AndDropsTheFastenerAndOtherSeats()
    {
        var spec = (HoleTestWorkbook.Clearance() with
        {
            Seat = HoleSeat.Spotface,
            Fastener = "TCAP",
            ClearanceFit = ClearanceFit.Close,
            SpotfaceDiameter = 12m,
            SpotfaceDepth = 0m,
            SpotfaceDepthExpression = " d0/8 ",
            CboreDiameter = 9m,
        }).Normalised();

        Assert.Null(spec.Fastener);
        Assert.Equal(ClearanceFit.Close, spec.ClearanceFit);
        Assert.Equal((12m, 0m, "d0/8"), (spec.SpotfaceDiameter, spec.SpotfaceDepth, spec.SpotfaceDepthExpression));
        Assert.Null(spec.CboreDiameter);

        var noSpotface = (spec with { Seat = HoleSeat.Countersink }).Normalised();
        Assert.Null(noSpotface.SpotfaceDiameter);
        Assert.Null(noSpotface.SpotfaceDepth);
        Assert.Null(noSpotface.SpotfaceDepthExpression);
    }

    // ---- Simple holes ----

    [Fact]
    public void Simple_IsAPlainHoleOfItsDiameter()
    {
        var result = Built(Simple() with
        {
            Termination = HoleTermination.Distance,
            Depth = 13m,
            DiameterExpression = "d0",
        });
        var g = result.Geometry!;

        Assert.Equal(HoleKind.Simple, g.Kind);
        Assert.Equal(0.65, g.HoleDiameterCm, 12);
        Assert.Equal(1.3, g.DepthCm!.Value, 12);
        Assert.Equal("d0", g.DiameterExpression);
        Assert.Null(g.Thread);
        Assert.Null(g.Tolerance);
        Assert.False(g.IsColoured);
        Assert.Equal("90 deg", result.Resolved.DrillPoint);
    }

    [Theory]
    [InlineData(null, "Enter a diameter.")]
    [InlineData("0", "Enter a diameter greater than zero.")]
    [InlineData("-1", "Enter a diameter greater than zero.")]
    public void Simple_NeedsADiameterGreaterThanZero(string? diameter, string message)
    {
        var result = Build(Simple(diameter is null ? null : decimal.Parse(diameter)));

        var problem = Assert.Single(result.Problems);
        Assert.Equal((HoleField.Diameter, message), (problem.Field, problem.Message));
    }

    [Fact]
    public void Simple_NeedsNoSize()
    {
        Assert.DoesNotContain(Built(Simple()).Problems, p => p.Field == HoleField.Size);
    }

    [Theory]
    [InlineData(HoleSeat.Counterbore, HoleField.CounterboreDiameter)]
    [InlineData(HoleSeat.Countersink, HoleField.CountersinkDiameter)]
    [InlineData(HoleSeat.Spotface, HoleField.SpotfaceDiameter)]
    public void Simple_ASeatFollowsNoRow_AndAsksForEveryValue(HoleSeat seat, HoleField first)
    {
        var result = Build(Simple() with { Seat = seat });

        Assert.Equal(2, result.Problems.Count);
        Assert.Equal(first, result.Problems[0].Field);
    }

    [Fact]
    public void Simple_ASeatFromItsBoxes_IsBuilt()
    {
        var g = Built(Simple() with { Seat = HoleSeat.Counterbore, CboreDiameter = 11m, CboreDepth = 4m }).Geometry!;

        Assert.Equal((1.1, 0.4), (g.CounterboreDiameterCm!.Value, g.CounterboreDepthCm!.Value));
    }

    [Fact]
    public void Simple_ASeatNoWiderThanTheHole_IsRefused()
    {
        var result = Build(Simple() with { Seat = HoleSeat.Countersink, CsinkDiameter = 6.5m, CsinkAngleDeg = 90m });

        var problem = Assert.Single(result.Problems);
        Assert.Equal(HoleField.CountersinkDiameter, problem.Field);
        Assert.Equal("The hole (6.5 mm) is not smaller than its countersink (6.5 mm).", problem.Message);
    }

    // ---- Taper tapped holes ----

    [Fact]
    public void TaperTapped_IsTheRowsTaperThread_AtItsTapDrill_WithTheRowsDrillDepth()
    {
        var result = Built(Taper());
        var g = result.Geometry!;

        Assert.Equal(HoleKind.TaperTapped, g.Kind);
        Assert.Equal(1.12, g.HoleDiameterCm, 12);
        Assert.Equal(2.24, g.DepthCm!.Value, 12);
        Assert.NotNull(g.Thread);
        Assert.True(g.Thread!.IsTapered);
        Assert.Equal(("Rc 1/4", "BSP Taper Internal", ""), (g.Thread.Designation, g.Thread.ThreadType, g.Thread.Class));
        Assert.False(g.Thread.FullDepth);
        Assert.Equal(22.4m, result.Resolved.DrillDepth);
        Assert.Null(result.Resolved.ThreadDepth);
    }

    [Fact]
    public void TaperTapped_TakesATypedDrillDepth_AndItsExpression()
    {
        var g = Built(Taper() with { DrillDepth = 30m, DrillDepthExpression = "d0" }).Geometry!;

        Assert.Equal(3.0, g.DepthCm!.Value, 12);
        Assert.Equal("d0", g.DepthExpression);
    }

    [Fact]
    public void TaperTapped_CanBeThroughAll()
    {
        var g = Built(Taper() with { Termination = HoleTermination.ThroughAll }).Geometry!;

        Assert.Null(g.DepthCm);
        Assert.True(g.Thread!.IsTapered);
    }

    [Fact]
    public void TaperTapped_ASizeNotInTheTab_IsAProblem()
    {
        var result = Build(Taper("Rc 9"));

        var problem = Assert.Single(result.Problems);
        Assert.Equal(HoleField.Size, problem.Field);
        Assert.Equal("Rc 9 is not in the Taper Tapped tab for Metric holes.", problem.Message);
    }

    [Fact]
    public void TaperTapped_ACounterbore_IsRefused()
    {
        var result = Build(Taper() with { Seat = HoleSeat.Counterbore, CboreDiameter = 20m, CboreDepth = 2m });

        var problem = Assert.Single(result.Problems);
        Assert.Equal((HoleField.Seat, HoleRules.TaperCounterboreText), (problem.Field, problem.Message));
    }

    [Fact]
    public void TaperTapped_ACountersinkOrSpotface_IsSizedInItsBoxes_WiderThanTheTapDrill()
    {
        var csink = Built(Taper() with { Seat = HoleSeat.Countersink, CsinkDiameter = 14m, CsinkAngleDeg = 90m });
        var spotface = Build(Taper() with { Seat = HoleSeat.Spotface, SpotfaceDiameter = 11m, SpotfaceDepth = 1m });

        Assert.Equal(1.4, csink.Geometry!.CountersinkDiameterCm!.Value, 12);
        var problem = Assert.Single(spotface.Problems);
        Assert.Equal(HoleField.SpotfaceDiameter, problem.Field);
        Assert.Equal("The tap drill (11.2 mm) is not smaller than its spotface (11 mm).", problem.Message);
    }

    [Fact]
    public void TaperTapped_ASpotfaceTakesNoRow()
    {
        var result = Build(Taper() with { Seat = HoleSeat.Spotface });

        Assert.Equal(
            [HoleField.SpotfaceDiameter, HoleField.SpotfaceDepth], result.Problems.Select(p => p.Field).ToArray());
    }

    // ---- Spotfaces ----

    [Fact]
    public void Spotface_OnAClearanceHole_TakesTheFit_AndTheSpotfaceRowOfItsSize()
    {
        var result = Built(HoleTestWorkbook.Clearance() with { Seat = HoleSeat.Spotface, ClearanceFit = ClearanceFit.Loose });
        var g = result.Geometry!;

        Assert.Equal(HoleSeat.Spotface, g.Seat);
        Assert.Equal(0.58, g.HoleDiameterCm, 12);
        Assert.Equal((1.1, 0.05), (g.SpotfaceDiameterCm!.Value, g.SpotfaceDepthCm!.Value));
        Assert.Null(g.CounterboreDiameterCm);
        Assert.Equal((11m, 0.5m), (result.Resolved.SpotfaceDiameter, result.Resolved.SpotfaceDepth));
        Assert.Null(result.Resolved.Fastener);
    }

    [Fact]
    public void Spotface_ItsBoxesComeBeforeTheRow()
    {
        var g = Built(HoleTestWorkbook.Clearance() with
        {
            Seat = HoleSeat.Spotface,
            SpotfaceDiameter = 14m,
            SpotfaceDepthExpression = "d0/8",
            SpotfaceDepth = 2m,
        }).Geometry!;

        Assert.Equal((1.4, 0.2), (g.SpotfaceDiameterCm!.Value, g.SpotfaceDepthCm!.Value));
        Assert.Equal("d0/8", g.SpotfaceDepthExpression);
    }

    [Fact]
    public void Spotface_ADepthOfZero_IsAllowed_ANegativeOneIsNot()
    {
        var zero = Built(HoleTestWorkbook.Clearance() with { Seat = HoleSeat.Spotface, SpotfaceDepth = 0m });
        var negative = Build(HoleTestWorkbook.Clearance() with { Seat = HoleSeat.Spotface, SpotfaceDepth = -0.1m });

        Assert.Equal(0.0, zero.Geometry!.SpotfaceDepthCm);
        var problem = Assert.Single(negative.Problems);
        Assert.Equal((HoleField.SpotfaceDepth, "The spotface depth is less than zero."), (problem.Field, problem.Message));
    }

    [Fact]
    public void Spotface_AsDeepAsABlindHole_IsRefused()
    {
        var result = Build(HoleTestWorkbook.Clearance() with
        {
            Seat = HoleSeat.Spotface,
            Termination = HoleTermination.Distance,
            Depth = 0.5m,
        });

        var problem = Assert.Single(result.Problems);
        Assert.Equal(HoleField.Depth, problem.Field);
        Assert.Equal("The spotface depth (0.5 mm) is not less than the hole depth (0.5 mm).", problem.Message);
    }

    [Fact]
    public void Spotface_OnAClearanceSizeWithNoRow_AsksForItsValues()
    {
        var result = Build(HoleTestWorkbook.Clearance("TBIG") with { Seat = HoleSeat.Spotface });

        Assert.Equal(
            [HoleField.SpotfaceDiameter, HoleField.SpotfaceDepth], result.Problems.Select(p => p.Field).ToArray());
    }

    [Fact]
    public void Spotface_OnATappedHole_TakesTheRowOfTheSameSize_AndMustBeWiderThanTheThread()
    {
        var built = Built(HoleTestWorkbook.Tapped() with { Seat = HoleSeat.Spotface });
        var narrow = Build(HoleTestWorkbook.Tapped() with { Seat = HoleSeat.Spotface, SpotfaceDiameter = 5m });

        Assert.Equal(1.1, built.Geometry!.SpotfaceDiameterCm!.Value, 12);
        Assert.Null(built.Resolved.Fastener);
        var problem = Assert.Single(narrow.Problems);
        Assert.Equal("The thread M5 (5 mm) is not smaller than its spotface (5 mm).", problem.Message);
    }

    [Fact]
    public void Spotface_OnADowel_TakesTheRowOfTheSameSize()
    {
        var dowel = HoleTestWorkbook.Dowel("5 mm") with { Seat = HoleSeat.Spotface, Tolerance = "Test bore" };

        var g = Built(dowel).Geometry!;

        Assert.Equal((1.1, 0.05), (g.SpotfaceDiameterCm!.Value, g.SpotfaceDepthCm!.Value));
        Assert.True(g.IsColoured);
    }

    [Fact]
    public void SpotfaceRowFor_MatchesTheSameSize_ShownRowsFirst()
    {
        Assert.Equal(11m, HoleRules.SpotfaceRowFor(Workbook, HoleUnits.Metric, HoleKind.Clearance, "t5")!.SpotfaceDiameter);
        Assert.Equal(11m, HoleRules.SpotfaceRowFor(Workbook, HoleUnits.Metric, HoleKind.Tapped, "M5")!.SpotfaceDiameter);
        Assert.Equal(11m, HoleRules.SpotfaceRowFor(Workbook, HoleUnits.Metric, HoleKind.Dowel, "5 mm")!.SpotfaceDiameter);
        Assert.Equal(
            0.4m,
            HoleRules.SpotfaceRowFor(Workbook, HoleUnits.Imperial, HoleKind.Tapped, "#7-36 UNF")!.SpotfaceDiameter);
        Assert.Null(HoleRules.SpotfaceRowFor(Workbook, HoleUnits.Metric, HoleKind.Dowel, "7 mm"));
        Assert.Null(HoleRules.SpotfaceRowFor(Workbook, HoleUnits.Metric, HoleKind.TaperTapped, "Rc 1/4"));
        Assert.Null(HoleRules.SpotfaceRowFor(Workbook, HoleUnits.Metric, HoleKind.Simple, "T5"));
        Assert.Null(HoleRules.SpotfaceRowFor(HoleTestWorkbook.Create(), HoleUnits.Metric, HoleKind.Clearance, "T5"));
    }

    [Fact]
    public void SeatRowStart_AndDowelSeatStart_CarryTheSpotfaceRow()
    {
        var clearance = HoleRules.SeatRowStart(Workbook, HoleUnits.Metric, HoleKind.Clearance, "T5", "TCAP");
        var dowel = HoleRules.DowelSeatStart(Workbook, HoleUnits.Metric, "5 mm");

        Assert.Equal((9.5m, 11m, 0.5m), (clearance.CboreDiameter, clearance.SpotfaceDiameter, clearance.SpotfaceDepth));
        Assert.Equal((11m, 0.5m), (dowel.SpotfaceDiameter, dowel.SpotfaceDepth));
    }

    // ---- Seat check ----

    [Fact]
    public void SeatCheck_ASpotfaceThatMatches_HasNoDifferences_AndOneThatDoesNot_IsReported()
    {
        var g = Built(HoleTestWorkbook.Clearance() with { Seat = HoleSeat.Spotface }).Geometry!;
        var facts = FactsOf(g);

        Assert.Empty(HoleSeatCheck.Differences(g, facts));
        Assert.Equal(
            ["the spotface depth is 1 mm; it should be 0.5 mm."],
            HoleSeatCheck.Differences(g, facts with { SpotfaceDepthCm = 0.1 }));
        Assert.Equal(
            ["the seat is none; it should be a spotface."],
            HoleSeatCheck.Differences(g, facts with { Seat = HoleSeat.None }));
    }

    // ---- Stored data ----

    public static TheoryData<HoleSpec> NewSpecs() => new()
    {
        Simple() with { DiameterExpression = "d0" },
        Simple() with
        {
            Termination = HoleTermination.Distance, Depth = 12m, DrillPoint = "Flat",
            Seat = HoleSeat.Spotface, SpotfaceDiameter = 9m, SpotfaceDepth = 0m, SpotfaceDepthExpression = "d0/8",
        },
        Taper() with { DrillDepth = 22.4m, DrillDepthExpression = "d0", DrillPoint = "90 deg" },
        Taper() with { Termination = HoleTermination.ThroughAll, Seat = HoleSeat.Countersink, CsinkDiameter = 14m, CsinkAngleDeg = 90m },
        HoleTestWorkbook.Clearance() with { Seat = HoleSeat.Spotface, ClearanceFit = ClearanceFit.Loose, SpotfaceDiameter = 11m, SpotfaceDepth = 0.5m },
        HoleTestWorkbook.Tapped() with { ThreadDepth = 7.5m, DrillDepth = 10m, DrillPoint = "90 deg", Seat = HoleSeat.Spotface, SpotfaceDiameter = 11m, SpotfaceDepth = 0.5m },
    };

    [Theory]
    [MemberData(nameof(NewSpecs))]
    public void StoredData_TheNewKindsAndSeat_ReadBackAsStored(HoleSpec spec)
    {
        var stored = new HoleStoredData(spec, new HoleAppliedState(0.65, null, null, null, false));

        var read = HoleStoredData.Read(stored.ToAttributes());

        Assert.True(read.Succeeded, read.Message);
        Assert.Equal(spec.Normalised(), read.Data!.Spec);
    }

    [Fact]
    public void StoredData_ASimpleHole_StoresItsDiameterAndNoSize_AtTheCurrentVersion()
    {
        var attributes = new HoleStoredData(Simple(), new HoleAppliedState(0.65, null, null, null, false)).ToAttributes();
        var byName = attributes.ToDictionary(a => a.Name, a => a.Value);

        Assert.Equal(HoleStoredData.CurrentVersion, byName[HoleStoredData.VersionName]);
        Assert.Equal("Simple", byName[HoleStoredData.KindName]);
        Assert.Equal(6.5, byName[HoleStoredData.DiameterName]);
        Assert.False(byName.ContainsKey(HoleStoredData.SizeName));
    }

    [Fact]
    public void StoredData_ASimpleHoleWithNoDiameter_CannotBeStored_NorRead()
    {
        var stored = new HoleStoredData(Simple(null), new HoleAppliedState(0.65, null, null, null, false));
        Assert.Throws<ArgumentException>(() => stored.ToAttributes());

        var attributes = new HoleStoredData(Simple(), new HoleAppliedState(0.65, null, null, null, false)).ToAttributes()
            .Where(a => a.Name != HoleStoredData.DiameterName);
        var read = HoleStoredData.Read(attributes);
        Assert.False(read.Succeeded);
        Assert.Contains(HoleStoredData.DiameterName, read.Message);
    }

    [Fact]
    public void StoredData_ASpotfaceWithoutItsSizes_CannotBeStored()
    {
        var stored = new HoleStoredData(
            HoleTestWorkbook.Clearance() with { Seat = HoleSeat.Spotface, SpotfaceDiameter = 11m },
            new HoleAppliedState(0.55, null, null, null, false));

        Assert.Throws<ArgumentException>(() => stored.ToAttributes());
    }

    [Fact]
    public void StoredData_Version2_StillReads()
    {
        var spec = HoleTestWorkbook.Tapped() with
        {
            ThreadDepth = 7.5m, DrillDepth = 10m, DrillPoint = "90 deg",
            Seat = HoleSeat.Counterbore, CboreDiameter = 9.5m, CboreDepth = 5m,
        };
        var attributes = new HoleStoredData(spec, new HoleAppliedState(0.42, null, null, null, false)).ToAttributes()
            .Select(a => a.Name == HoleStoredData.VersionName ? new HoleAttribute(a.Name, 2) : a);

        var read = HoleStoredData.Read(attributes);

        Assert.True(read.Succeeded, read.Message);
        Assert.Equal(spec.Normalised(), read.Data!.Spec);
    }

    // ---- Read back from Inventor ----

    [Fact]
    public void ReadBack_AnInventorSpotfaceHole_IsAClearanceHoleWithItsOwnSpotface()
    {
        var g = Built(HoleTestWorkbook.Clearance() with
        {
            Seat = HoleSeat.Spotface, SpotfaceDiameter = 12m, SpotfaceDepth = 1m,
        }).Geometry!;

        var result = HoleReadBack.Read(null, FactsOf(g), Workbook, HoleUnits.Metric);

        Assert.Equal(HoleKind.Clearance, result.Spec.Kind);
        Assert.Equal(HoleSeat.Spotface, result.Spec.Seat);
        Assert.Equal(("T5", ClearanceFit.Normal), (result.Spec.Size, result.Spec.ClearanceFit));
        Assert.Equal((12m, 1m), (result.Spec.SpotfaceDiameter, result.Spec.SpotfaceDepth));
        Assert.Empty(result.Notes);
    }

    [Fact]
    public void ReadBack_AnInventorTaperTappedHole_IsTaperTappedWithItsRow()
    {
        var g = Built(Taper() with { DrillDepth = 25m, DrillPoint = "90 deg" }).Geometry!;

        var result = HoleReadBack.Read(null, FactsOf(g), Workbook, HoleUnits.Imperial);

        Assert.Equal(HoleKind.TaperTapped, result.Spec.Kind);
        Assert.Equal((HoleUnits.Metric, "Rc 1/4"), (result.Spec.Units, result.Spec.Size));
        Assert.Equal(25m, result.Spec.DrillDepth);
        Assert.Null(result.Spec.ThreadDepth);
        Assert.Empty(result.Notes);
        Assert.True(HoleRules.Build(result.Spec, Workbook, 1).Succeeded);
    }

    [Fact]
    public void ReadBack_ATaperThreadNotInTheTab_HasANote_AndAnEmptySize()
    {
        var facts = FactsOf(Built(Taper()).Geometry!) with { ThreadDesignation = "Rc 3" };

        var result = HoleReadBack.Read(null, facts, Workbook, HoleUnits.Metric);

        Assert.Equal(HoleKind.TaperTapped, result.Spec.Kind);
        Assert.Equal("", result.Spec.Size);
        Assert.Equal("The taper thread Rc 3 is not in the Taper Tapped tab; pick a size.", Assert.Single(result.Notes));
    }

    [Fact]
    public void ReadBack_AHoleNoClearanceRowHas_IsASimpleHole_WithItsSeatAndDepth()
    {
        var g = Built(Simple(6.25m) with
        {
            Termination = HoleTermination.Distance, Depth = 12m, DrillPoint = "Flat",
            Seat = HoleSeat.Spotface, SpotfaceDiameter = 11m, SpotfaceDepth = 3m,
        }).Geometry!;
        // Inventor measures the depth from the spotface's bottom (task 114): 9 mm here, read back as 12.
        Assert.Equal(0.9, g.DepthCm!.Value, 9);
        var facts = FactsOf(g) with { Seat = HoleSeat.Spotface, SpotfaceDiameterCm = 1.1, SpotfaceDepthCm = 0.3 };

        var result = HoleReadBack.Read(null, facts, Workbook, HoleUnits.Metric);

        Assert.Equal(HoleKind.Simple, result.Spec.Kind);
        Assert.Equal(6.25m, result.Spec.Diameter);
        Assert.Equal((HoleSeat.Spotface, 11m, 3m), (result.Spec.Seat, result.Spec.SpotfaceDiameter, result.Spec.SpotfaceDepth));
        Assert.Equal((12m, "Flat"), (result.Spec.Depth, result.Spec.DrillPoint));
        Assert.Single(result.Notes);
    }

    [Fact]
    public void ReadBack_ASimpleHoleDrivenByAParameter_KeepsItsExpression()
    {
        var facts = FactsOf(Built(Simple(6.25m)).Geometry!) with { DiameterExpression = "d0" };

        var result = HoleReadBack.Read(null, facts, Workbook, HoleUnits.Metric);

        Assert.Equal((6.25m, "d0"), (result.Spec.Diameter, result.Spec.DiameterExpression));
    }

    [Fact]
    public void ReadBack_StoredData_OfANewKind_OpensAsStored()
    {
        var spec = Simple() with { Seat = HoleSeat.Spotface, SpotfaceDiameter = 9m, SpotfaceDepth = 0.5m };
        var stored = HoleStoredData.Read(
            new HoleStoredData(spec, new HoleAppliedState(0.65, null, null, null, false)).ToAttributes());

        var result = HoleReadBack.Read(stored, FactsOf(Built(spec).Geometry!), Workbook, HoleUnits.Metric);

        Assert.Equal(HoleSource.Awb, result.Source);
        Assert.Equal(spec.Normalised(), result.Spec);
    }

    // ---- Re-apply offer ----

    [Fact]
    public void Reapply_ASpotfaceChangedInInventor_IsOffered()
    {
        var spec = Built(HoleTestWorkbook.Clearance() with { Seat = HoleSeat.Spotface }).Resolved;
        var g = Built(spec).Geometry!;
        var stored = new HoleStoredData(spec, HoleAppliedState.From(g));

        Assert.False(HoleReapplyRules.Check(stored, FactsOf(g), false).IsOffered);
        var changed = HoleReapplyRules.Check(stored, FactsOf(g) with { SpotfaceDiameterCm = 1.3 }, false);
        Assert.Equal(["The spotface diameter is 13 mm; AWB Hole set 11 mm."], changed.Differences);
        var noSeat = HoleReapplyRules.Check(stored, FactsOf(g) with { Seat = HoleSeat.None }, false);
        Assert.Contains("The hole has no seat; AWB Hole made it with a spotface.", noSeat.Differences);
    }

    [Fact]
    public void Reapply_ATaperTappedHole_ComparesItsDrillDepth_NotItsDiameter()
    {
        var spec = Built(Taper()).Resolved;
        var g = Built(spec).Geometry!;
        var stored = new HoleStoredData(spec, HoleAppliedState.From(g));
        var facts = FactsOf(g) with { HoleDiameterCm = 1.1 };

        Assert.False(HoleReapplyRules.Check(stored, facts, false).IsOffered);
        Assert.Equal(
            ["The drill depth is 20 mm; AWB Hole set 22.4 mm."],
            HoleReapplyRules.Check(stored, facts with { DepthCm = 2.0 }, false).Differences);
    }

    // ---- Remembered values ----

    [Fact]
    public void LastSeats_ASimpleSeat_IsRememberedPerUnitsAndSeat()
    {
        var kept = Simple() with { Seat = HoleSeat.Spotface, SpotfaceDiameter = 9m, SpotfaceDepth = 0m };
        var settings = HoleLastDowelSeats.Remember(new AddinSettings(), kept)!;
        settings = HoleLastDowelSeats.Remember(
            settings, Simple() with { Seat = HoleSeat.Counterbore, CboreDiameter = 10m, CboreDepth = 2m })!;

        var last = HoleLastDowelSeats.From(settings);

        var metric = last.ForSimple(HoleUnits.Metric);
        Assert.Equal((10m, 2m, 9m, 0m), (metric.CboreDiameter, metric.CboreDepth, metric.SpotfaceDiameter, metric.SpotfaceDepth));
        Assert.True(last.ForSimple(HoleUnits.Imperial).IsEmpty);
        Assert.Equal(metric, last.ForKind(HoleUnits.Metric, HoleKind.Simple, null));
        Assert.Equal("Simple hole (Metric): spotface 9 mm x 0 mm", HoleLastDowelSeats.RememberedText(kept));
    }

    [Fact]
    public void LastSeats_ATaperTappedSeat_IsRememberedPerUnitsSizeAndSeat()
    {
        var kept = Taper() with { Seat = HoleSeat.Countersink, CsinkDiameter = 14m, CsinkAngleDeg = 90m };
        var settings = HoleLastDowelSeats.Remember(new AddinSettings(), kept)!;

        var last = HoleLastDowelSeats.From(settings);

        Assert.Equal((14m, 90m), (last.ForTaperTapped(HoleUnits.Metric, "rc 1/4").CsinkDiameter, last.ForTaperTapped(HoleUnits.Metric, "Rc 1/4").CsinkAngleDeg));
        Assert.True(last.ForTaperTapped(HoleUnits.Metric, "Rc 1/8").IsEmpty);
        Assert.True(last.ForTaperTapped(HoleUnits.Imperial, "Rc 1/4").IsEmpty);
        Assert.True(last.For(HoleUnits.Metric, "Rc 1/4").IsEmpty);
        Assert.Equal("Rc 1/4 taper tapped hole (Metric): countersink 14 mm at 90 deg", HoleLastDowelSeats.RememberedText(kept));
    }

    [Fact]
    public void LastSeats_ADowelSpotface_IsRemembered_WithItsOtherSeats()
    {
        var settings = HoleLastDowelSeats.Remember(new AddinSettings(), HoleTestWorkbook.CboredDowel())!;
        settings = HoleLastDowelSeats.Remember(
            settings,
            HoleTestWorkbook.Dowel("5 mm") with
            {
                Seat = HoleSeat.Spotface, SpotfaceDiameter = 12m, SpotfaceDepth = 1m, SpotfaceDepthExpression = "d0/8",
            })!;

        var seat = HoleLastDowelSeats.From(settings).For(HoleUnits.Metric, "5 mm");

        Assert.Equal((9.5m, 12m, 1m, "d0/8"), (seat.CboreDiameter, seat.SpotfaceDiameter, seat.SpotfaceDepth, seat.SpotfaceDepthExpression));
    }

    [Fact]
    public void LastSeats_AClearanceSpotface_IsNotRemembered()
    {
        Assert.Null(HoleLastDowelSeats.Remember(
            new AddinSettings(),
            HoleTestWorkbook.Clearance() with { Seat = HoleSeat.Spotface, SpotfaceDiameter = 11m, SpotfaceDepth = 1m }));
    }

    [Fact]
    public void LastSeats_SurviveTheSettingsFile()
    {
        var settings = HoleLastDowelSeats.Remember(
            new AddinSettings(),
            Simple() with { Seat = HoleSeat.Spotface, SpotfaceDiameter = 9m, SpotfaceDepth = 0m })!;
        settings = HoleLastDowelSeats.Remember(
            settings, Taper() with { Seat = HoleSeat.Spotface, SpotfaceDiameter = 16m, SpotfaceDepth = 1m })!;
        settings = HoleLastDepths.Remember(settings, Simple(6.5m))!;

        var json = System.Text.Json.JsonSerializer.Serialize(settings, SettingsJson);
        var back = System.Text.Json.JsonSerializer.Deserialize<AddinSettings>(json, SettingsJson)!;

        Assert.Equal(settings, back);
    }

    private static readonly System.Text.Json.JsonSerializerOptions SettingsJson =
        new() { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase };

    [Fact]
    public void HoleDowelSeat_Valid_KeepsASpotfaceDepthOfZero_AndDropsANegativeOne()
    {
        var zero = (HoleDowelSeat.Empty with { SpotfaceDiameter = 9m, SpotfaceDepth = 0m, SpotfaceDepthExpression = " d0 " }).Valid();
        var bad = (HoleDowelSeat.Empty with { SpotfaceDiameter = 0m, SpotfaceDepth = -1m, SpotfaceDepthExpression = "d0" }).Valid();

        Assert.Equal((9m, 0m, "d0"), (zero.SpotfaceDiameter, zero.SpotfaceDepth, zero.SpotfaceDepthExpression));
        Assert.True(bad.IsEmpty);
    }

    [Fact]
    public void LastDepths_ASimpleHole_RemembersItsDiameterAndDepth_ATaperTappedHoleNeither()
    {
        var simple = Simple(6.5m) with { DiameterExpression = "d0", Termination = HoleTermination.Distance, Depth = 13m };

        var settings = HoleLastDepths.Remember(new AddinSettings(), simple)!;
        var last = HoleLastDepths.From(settings);

        Assert.Equal(6.5m, last.SimpleDiameterFor(HoleUnits.Metric));
        Assert.Null(last.SimpleDiameterFor(HoleUnits.Imperial));
        Assert.Equal(13m, last.For(HoleUnits.Metric));
        Assert.Null(HoleLastDepths.Remember(new AddinSettings(), Taper() with { DrillDepth = 20m }));
        Assert.Null(HoleLastDepths.DiameterToRemember(HoleTestWorkbook.Clearance()));
    }

    // ---- Diagram ----

    [Fact]
    public void Diagram_Summary_NamesTheNewTypesAndTheSpotface()
    {
        var spotface = new HoleDiagramInput
        {
            Units = HoleUnits.Metric,
            Kind = HoleKind.Simple,
            Seat = HoleSeat.Spotface,
            HoleDiameter = 6.5m,
            SpotfaceDiameter = 11m,
            SpotfaceDepth = 0.5m,
        };
        var taper = new HoleDiagramInput
        {
            Units = HoleUnits.Metric,
            Kind = HoleKind.TaperTapped,
            Termination = HoleTermination.Distance,
            ThreadName = "Rc 1/4",
            HoleDiameter = 11.2m,
            Depth = 22.4m,
        };

        Assert.Equal("⌀6.5, through all, spotface ⌀11 by 0.5", HoleDiagram.Summary(spotface));
        Assert.Equal("Rc 1/4, drill 22.4", HoleDiagram.Summary(taper));
    }

    [Fact]
    public void Diagram_ASpotface_IsDrawnAsAStep_WithItsDiameterAndDepth()
    {
        var input = new HoleDiagramInput
        {
            Units = HoleUnits.Metric,
            Kind = HoleKind.Clearance,
            Seat = HoleSeat.Spotface,
            HoleDiameter = 5.5m,
            SpotfaceDiameter = 11m,
            SpotfaceDepth = 0.5m,
        };

        var diagram = HoleDiagram.Draw(input);
        var texts = diagram.Labels.Select(l => l.Text).ToList();

        Assert.Contains("⌀11", texts);
        Assert.Contains("0.5", texts);
        Assert.Contains("⌀5.5", texts);
    }

    [Fact]
    public void Diagram_ASpotfaceOfDepthZero_DrawsWithNoDepthDimension()
    {
        var input = new HoleDiagramInput
        {
            Units = HoleUnits.Metric,
            Kind = HoleKind.Simple,
            Seat = HoleSeat.Spotface,
            HoleDiameter = 5m,
            SpotfaceDiameter = 9m,
            SpotfaceDepth = 0m,
        };

        var diagram = HoleDiagram.Draw(input);

        Assert.DoesNotContain(diagram.Labels, l => l.Text == "0");
        Assert.Contains(diagram.Labels, l => l.Text == "⌀9");
    }

    [Fact]
    public void Diagram_ATaperThread_IsDrawnAsAThread_WithItsName()
    {
        var input = new HoleDiagramInput
        {
            Units = HoleUnits.Metric,
            Kind = HoleKind.TaperTapped,
            Termination = HoleTermination.Distance,
            ThreadName = "Rc 1/4",
            HoleDiameter = 11.2m,
            Depth = 22.4m,
            PointAngleDeg = 118m,
        };

        var diagram = HoleDiagram.Draw(input);

        Assert.Contains(diagram.Lines, l => l.Ink == DiagramInk.Thread);
        Assert.Contains(diagram.Labels, l => l.Text == "Rc 1/4");
    }

    // ---- Icons ----

    [Fact]
    public void Icons_TheNewTypesAndSeat_HaveTheirOwn()
    {
        Assert.Equal(HoleIcons.Simple, HoleIcons.ForType("simple"));
        Assert.Equal(HoleIcons.TaperTapped, HoleIcons.ForType(HoleStandards.TaperTappedType));
        Assert.Equal(HoleIcons.Tapped, HoleIcons.ForType(HoleStandards.TappedType));
        Assert.Equal(HoleIcons.Clearance, HoleIcons.ForType("anything"));
        Assert.Equal(HoleIcons.Spotface, HoleIcons.ForSeat(HoleSeat.Spotface));
        Assert.Contains(HoleIcons.Simple, HoleIcons.All);
        Assert.Contains(HoleIcons.TaperTapped, HoleIcons.All);
        Assert.Contains(HoleIcons.Spotface, HoleIcons.All);
    }
}
