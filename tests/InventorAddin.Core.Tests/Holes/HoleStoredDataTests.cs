using InventorAddin.Core.Holes;
using static InventorAddin.Core.Tests.Holes.HoleTestWorkbook;

namespace InventorAddin.Core.Tests.Holes;

public sealed class HoleStoredDataTests
{
    private static readonly HoleWorkbook Workbook = Create();

    /// <summary>Builds the spec as the add-in would, and stores what it resolved and applied.</summary>
    private static HoleStoredData Stored(HoleSpec spec)
    {
        var result = HoleRules.Build(spec, Workbook, 1);
        Assert.True(result.Succeeded, string.Join(" | ", result.Problems));
        return new HoleStoredData(result.Resolved, HoleAppliedState.From(result.Geometry!));
    }

    public static TheoryData<HoleSpec> EveryKind() => new()
    {
        Clearance(),
        Clearance() with { ClearanceFit = ClearanceFit.Loose, Termination = HoleTermination.Distance, Depth = 12.5m, Flip = true },
        Clearance() with { ClearanceFit = ClearanceFit.Close, Termination = HoleTermination.ToFace },
        Clearance() with { Seat = HoleSeat.Counterbore, Fastener = "TLOW" },
        Clearance() with { Seat = HoleSeat.Counterbore, Termination = HoleTermination.Distance, Depth = 20m, DrillPoint = "Flat" },
        Clearance() with { Seat = HoleSeat.Countersink },
        HoleSpec.ClearanceHole(HoleUnits.Imperial, "#7") with { Seat = HoleSeat.Countersink, Termination = HoleTermination.Distance, Depth = 0.375m },
        Tapped(),
        Tapped() with { ThreadDepth = 6.25m, DrillDepth = 8.1m, DrillPoint = "118 deg" },
        Tapped() with { Termination = HoleTermination.ThroughAll },
        Tapped("#7-36 UNF") with { Units = HoleUnits.Imperial },
        Dowel(),
        Dowel() with { Tolerance = "Test bore", Termination = HoleTermination.Distance, Depth = 14m },
        Dowel() with { PrecisionFit = PrecisionFit.Press, Flip = true },
        Dowel("1/7\"") with { Units = HoleUnits.Imperial, PrecisionFit = PrecisionFit.Slip },
        Tapped() with { Seat = HoleSeat.Counterbore, Fastener = "TLOW" },
        Tapped() with { Seat = HoleSeat.Countersink, Termination = HoleTermination.ThroughAll },
        CboredDowel() with { Termination = HoleTermination.Distance, Depth = 14m },
        CboredDowel() with { CboreDiameter = 7.25m, CboreDepth = 1.5m, CboreDepthExpression = "d0 / 4" },
        CsunkDowel() with { PrecisionFit = PrecisionFit.Press },
        CsunkDowel() with { CsinkDiameter = 6.1m, CsinkAngleDeg = 120m },
    };

    [Theory]
    [MemberData(nameof(EveryKind))]
    public void RoundTrip(HoleSpec spec)
    {
        var stored = Stored(spec);

        var read = HoleStoredData.Read(stored.ToAttributes());

        Assert.True(read.Succeeded, read.Message);
        Assert.Equal("", read.Message);
        Assert.Equal(stored.Spec.Normalised(), read.Data!.Spec);
        Assert.Equal(stored.Applied, read.Data.Applied);

        // What was read builds the same hole.
        var rebuilt = HoleRules.Build(read.Data.Spec, Workbook, 1);
        Assert.Equal(HoleAppliedState.From(rebuilt.Geometry!), stored.Applied);
    }

    [Fact]
    public void RoundTrip_WithIrrelevantChoices_ReadsTheNormalisedSpec()
    {
        var spec = Clearance() with
        {
            Fastener = "TCAP",
            PrecisionFit = PrecisionFit.Press,
            Tolerance = "Test bore",
            ThreadDepth = 3m,
            Depth = 4m,
            DrillPoint = "Flat",
        };
        var stored = new HoleStoredData(spec, new HoleAppliedState(0.55, null, null, null, false));

        var attributes = stored.ToAttributes();
        var read = HoleStoredData.Read(attributes);

        Assert.Equal(HoleSpec.ClearanceHole(HoleUnits.Metric, "T5"), read.Data!.Spec);
        Assert.DoesNotContain(attributes, a => a.Name is HoleStoredData.FastenerName or HoleStoredData.ToleranceName
            or HoleStoredData.ThreadDepthName or HoleStoredData.DepthName or HoleStoredData.DrillPointName
            or HoleStoredData.PrecisionFitName);
    }

    [Fact]
    public void Attributes_AreVersionedStringsAndNumbers()
    {
        var attributes = Stored(Dowel() with { Termination = HoleTermination.Distance, Depth = 14m }).ToAttributes();

        Assert.Equal("AWB_Hole", HoleStoredData.AttributeSetName);
        Assert.Equal(new HoleAttribute("Version", 2), attributes[0]);
        Assert.All(attributes, a => Assert.True(a.Value is string or int or double, a.Name));
        Assert.Equal(attributes.Count, attributes.Select(a => a.Name).Distinct().Count());

        var byName = attributes.ToDictionary(a => a.Name, a => a.Value);
        Assert.Equal("Metric", byName["Units"]);
        Assert.Equal("Dowel", byName["Kind"]);
        Assert.Equal("None", byName["Seat"]);
        Assert.Equal("7 mm", byName["Size"]);
        Assert.Equal("Normal", byName["PrecisionFit"]);
        Assert.Equal("Test location", byName["Tolerance"]);
        Assert.Equal("Distance", byName["Termination"]);
        Assert.Equal(14.0, byName["Depth"]);
        Assert.Equal("90 deg", byName["DrillPoint"]);
        Assert.Equal(0, byName["Flip"]);
        Assert.Equal("G7", byName["AppliedToleranceIsoCode"]);
        Assert.Equal(1, byName["AppliedColoured"]);
    }

    [Fact]
    public void Read_AcceptsNumbersOfOtherTypes_AndIgnoresUnknownNames()
    {
        var attributes = Stored(Tapped()).ToAttributes()
            .Select(a => a.Name == "Version" ? new HoleAttribute("Version", 1.0) : a)
            .Select(a => a.Value is double d ? new HoleAttribute(a.Name, (decimal)d) : a)
            .Append(new HoleAttribute("SomethingNewer", "x"));

        var read = HoleStoredData.Read(attributes);

        Assert.True(read.Succeeded, read.Message);
        Assert.Equal(7.5m, read.Data!.Spec.ThreadDepth);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(0)]
    public void UnknownVersion_CannotBeRead(int version)
    {
        var attributes = Stored(Clearance()).ToAttributes()
            .Select(a => a.Name == "Version" ? new HoleAttribute("Version", version) : a);

        var read = HoleStoredData.Read(attributes);

        Assert.False(read.Succeeded);
        Assert.Null(read.Data);
        Assert.StartsWith("Cannot read the AWB hole data", read.Message);
        Assert.Contains($"version {version}", read.Message);
    }

    [Fact]
    public void NoAttributes_CannotBeRead()
    {
        var read = HoleStoredData.Read([]);

        Assert.False(read.Succeeded);
        Assert.Contains("Version", read.Message);
    }

    [Theory]
    [InlineData("Units")]
    [InlineData("Kind")]
    [InlineData("Size")]
    [InlineData("Tolerance")]
    [InlineData("PrecisionFit")]
    [InlineData("Depth")]
    [InlineData("Flip")]
    [InlineData("AppliedDiameterCm")]
    [InlineData("AppliedColoured")]
    public void MissingRequiredValue_CannotBeRead(string name)
    {
        var attributes = Stored(Dowel() with { Termination = HoleTermination.Distance, Depth = 14m }).ToAttributes()
            .Where(a => a.Name != name);

        var read = HoleStoredData.Read(attributes);

        Assert.False(read.Succeeded);
        Assert.Contains(name, read.Message);
        Assert.Contains("missing", read.Message);
    }

    [Fact]
    public void SeatedHoleWithoutFastener_CannotBeRead()
    {
        var attributes = Stored(Clearance() with { Seat = HoleSeat.Counterbore }).ToAttributes()
            .Where(a => a.Name != HoleStoredData.FastenerName);

        Assert.False(HoleStoredData.Read(attributes).Succeeded);
    }

    [Fact]
    public void TappedWithoutDepths_ReadsAsWorkbookDefaults()
    {
        var attributes = Stored(Tapped()).ToAttributes()
            .Where(a => a.Name is not (HoleStoredData.ThreadDepthName or HoleStoredData.DrillDepthName));

        var read = HoleStoredData.Read(attributes);

        Assert.True(read.Succeeded, read.Message);
        Assert.Null(read.Data!.Spec.ThreadDepth);
        Assert.Null(read.Data.Spec.DrillDepth);
    }

    [Theory]
    [InlineData("Kind", "Simple")]
    [InlineData("Kind", "1")]
    [InlineData("Units", "")]
    [InlineData("Flip", "yes")]
    [InlineData("Flip", 2)]
    [InlineData("Size", 5)]
    [InlineData("AppliedDiameterCm", "0.55")]
    [InlineData("Version", 1.5)]
    public void WrongValue_CannotBeRead(string name, object value)
    {
        var attributes = Stored(Clearance()).ToAttributes()
            .Select(a => a.Name == name ? new HoleAttribute(name, value) : a);

        var read = HoleStoredData.Read(attributes);

        Assert.False(read.Succeeded);
        Assert.Contains(name, read.Message);
    }

    [Fact]
    public void NotANumber_CannotBeRead()
    {
        var attributes = Stored(Clearance()).ToAttributes()
            .Select(a => a.Name == "AppliedDiameterCm" ? new HoleAttribute(a.Name, double.NaN) : a);

        Assert.False(HoleStoredData.Read(attributes).Succeeded);
    }

    public static TheoryData<HoleSpec, string> UnresolvedSpecs() => new()
    {
        { Clearance() with { Seat = HoleSeat.Counterbore }, HoleStoredData.CboreDiameterName },
        { Clearance() with { Seat = HoleSeat.Counterbore, CboreDiameter = 9.5m, CboreDepth = 5.2m }, HoleStoredData.FastenerName },
        { Clearance() with { Seat = HoleSeat.Countersink, Fastener = " ", CsinkDiameter = 10.4m, CsinkAngleDeg = 90m }, HoleStoredData.FastenerName },
        { Dowel(), HoleStoredData.ToleranceName },
        { Tapped() with { Seat = HoleSeat.Counterbore }, HoleStoredData.CboreDiameterName },
        { Tapped() with { Seat = HoleSeat.Countersink, CsinkDiameter = 7m }, HoleStoredData.CsinkAngleName },
        { CsunkDowel() with { CsinkDiameter = null, Tolerance = "Test bore" }, HoleStoredData.CsinkDiameterName },
        { CsunkDowel() with { CsinkAngleDeg = null, Tolerance = "Test bore" }, HoleStoredData.CsinkAngleName },
        { CboredDowel() with { CboreDiameter = null, Tolerance = "Test bore" }, HoleStoredData.CboreDiameterName },
        { CboredDowel() with { CboreDepth = null, Tolerance = "Test bore" }, HoleStoredData.CboreDepthName },
        { Clearance() with { Termination = HoleTermination.Distance }, HoleStoredData.DepthName },
        { Clearance(" "), HoleStoredData.SizeName },
    };

    [Theory]
    [MemberData(nameof(UnresolvedSpecs))]
    public void ToAttributes_UnresolvedSpec_Throws(HoleSpec spec, string missing)
    {
        var stored = new HoleStoredData(spec, new HoleAppliedState(0.55, null, null, null, false));

        var ex = Assert.Throws<ArgumentException>(() => stored.ToAttributes());
        Assert.Contains(missing, ex.Message);
    }

    [Fact]
    public void ASeatedTappedHole_StoresItsSeatAndFastener()
    {
        var attributes = Stored(Tapped() with { Seat = HoleSeat.Counterbore }).ToAttributes();

        Assert.Contains(attributes, a => a.Name == HoleStoredData.SeatName && Equals(a.Value, "Counterbore"));
        Assert.Contains(attributes, a => a.Name == HoleStoredData.FastenerName && Equals(a.Value, "TCAP"));
    }

    [Fact]
    public void ASeatedDowel_StoresItsOwnSeatSizes_AndNoFastener()
    {
        var attributes = Stored(CboredDowel() with { CboreDiameter = 7.25m, CboreDepthExpression = "d0 / 4" })
            .ToAttributes();
        var byName = attributes.ToDictionary(a => a.Name, a => a.Value);

        Assert.Equal(7.25, byName[HoleStoredData.CboreDiameterName]);
        Assert.Equal(5.2, byName[HoleStoredData.CboreDepthName]);
        Assert.Equal("d0 / 4", byName[HoleStoredData.CboreDepthExpressionName]);
        Assert.False(byName.ContainsKey(HoleStoredData.FastenerName));
        Assert.False(byName.ContainsKey(HoleStoredData.CsinkDiameterName));
        Assert.False(byName.ContainsKey(HoleStoredData.CsinkAngleName));

        var sink = Stored(CsunkDowel()).ToAttributes().ToDictionary(a => a.Name, a => a.Value);
        Assert.Equal(10.4, sink[HoleStoredData.CsinkDiameterName]);
        Assert.Equal(90.0, sink[HoleStoredData.CsinkAngleName]);
        Assert.False(sink.ContainsKey(HoleStoredData.CboreDiameterName));
    }

    [Fact]
    public void ASeatedDowelStoredBeforeTask106_ReadsWithItsSeatSizesNull()
    {
        // Version 1 data from task 099: a fastener from the same-size row, no seat sizes.
        var attributes = Stored(CboredDowel()).ToAttributes()
            .Where(a => a.Name is not HoleStoredData.CboreDiameterName and not HoleStoredData.CboreDepthName)
            .Append(new HoleAttribute(HoleStoredData.FastenerName, "TCAP"));

        var read = HoleStoredData.Read(attributes);

        Assert.True(read.Succeeded, read.Message);
        Assert.Equal(HoleSeat.Counterbore, read.Data!.Spec.Seat);
        Assert.Null(read.Data.Spec.CboreDiameter);
        Assert.Null(read.Data.Spec.CboreDepth);
        Assert.Null(read.Data.Spec.Normalised().Fastener);
    }

    [Fact]
    public void SeatSizes_OfAnotherSeat_AreNotRead()
    {
        var attributes = Stored(Tapped() with { Seat = HoleSeat.Counterbore }).ToAttributes()
            .Append(new HoleAttribute(HoleStoredData.CsinkDiameterName, 9.0))
            .Append(new HoleAttribute(HoleStoredData.CsinkAngleName, 90.0));

        var read = HoleStoredData.Read(attributes);

        Assert.True(read.Succeeded, read.Message);
        Assert.Equal(9.5m, read.Data!.Spec.CboreDiameter);
        Assert.Null(read.Data.Spec.CsinkDiameter);
        Assert.Null(read.Data.Spec.CsinkAngleDeg);
    }

    [Theory]
    [InlineData(HoleKind.Clearance, "T5", HoleSeat.Counterbore)]
    [InlineData(HoleKind.Clearance, "T5", HoleSeat.Countersink)]
    [InlineData(HoleKind.Tapped, "M5", HoleSeat.Counterbore)]
    public void EverySeatedHole_StoresItsSeatDimensions(HoleKind kind, string size, HoleSeat seat)
    {
        // Task 126: the seat is described by its dimensions on every kind, typed or from the row.
        var spec = (kind == HoleKind.Tapped ? Tapped(size) : Clearance(size)) with { Seat = seat };
        var byName = Stored(spec).ToAttributes().ToDictionary(a => a.Name, a => a.Value);

        if (seat == HoleSeat.Counterbore)
        {
            Assert.Equal(9.5, byName[HoleStoredData.CboreDiameterName]);
            Assert.Equal(5.2, byName[HoleStoredData.CboreDepthName]);
        }
        else
        {
            Assert.Equal(10.4, byName[HoleStoredData.CsinkDiameterName]);
            Assert.Equal(90.0, byName[HoleStoredData.CsinkAngleName]);
        }

        Assert.True(byName.ContainsKey(HoleStoredData.FastenerName));
    }

    [Fact]
    public void ATypedClearanceCounterbore_RoundTrips_WithItsExpression()
    {
        var stored = Stored(Clearance() with
        {
            Seat = HoleSeat.Counterbore,
            CboreDiameter = 11m,
            CboreDepth = 4m,
            CboreDepthExpression = "d0 / 2",
        });

        var read = HoleStoredData.Read(stored.ToAttributes());

        Assert.True(read.Succeeded, read.Message);
        Assert.Equal(11m, read.Data!.Spec.CboreDiameter);
        Assert.Equal(4m, read.Data.Spec.CboreDepth);
        Assert.Equal("d0 / 2", read.Data.Spec.CboreDepthExpression);
        Assert.Equal("TCAP", read.Data.Spec.Fastener);
    }

    [Fact]
    public void ATappedSeatWithNoRowOfItsSize_StoresNoFastener_AndReadsBack()
    {
        var spec = Tapped() with { Seat = HoleSeat.Countersink, Fastener = null, CsinkDiameter = 7m, CsinkAngleDeg = 90m };
        var stored = new HoleStoredData(
            spec with { DrillDepth = 10m, ThreadDepth = 7.5m, DrillPoint = "90 deg" },
            new HoleAppliedState(0.42, null, null, null, false));

        var attributes = stored.ToAttributes();
        var read = HoleStoredData.Read(attributes);

        Assert.DoesNotContain(attributes, a => a.Name == HoleStoredData.FastenerName);
        Assert.True(read.Succeeded, read.Message);
        Assert.Null(read.Data!.Spec.Fastener);
        Assert.Equal(7m, read.Data.Spec.CsinkDiameter);
    }

    [Fact]
    public void Version1_ASeatedClearanceHole_ReadsWithItsRowAndNoDimensions_AndResolvesToTheRow()
    {
        // Data written before task 126: version 1, a fastener, and no seat dimensions.
        var attributes = Stored(Clearance() with { Seat = HoleSeat.Counterbore, Fastener = "TLOW" }).ToAttributes()
            .Where(a => a.Name is not (HoleStoredData.CboreDiameterName or HoleStoredData.CboreDepthName))
            .Select(a => a.Name == HoleStoredData.VersionName ? new HoleAttribute(a.Name, 1) : a);

        var read = HoleStoredData.Read(attributes);

        Assert.True(read.Succeeded, read.Message);
        Assert.Equal("TLOW", read.Data!.Spec.Fastener);
        Assert.Null(read.Data.Spec.CboreDiameter);
        var rebuilt = HoleRules.Build(read.Data.Spec, Workbook, 1);
        Assert.Equal(8.5m, rebuilt.Resolved.CboreDiameter);
        Assert.Equal(3.1m, rebuilt.Resolved.CboreDepth);
    }

    [Fact]
    public void Version1_ASeatedTappedHoleWithoutFastener_CannotBeRead_ButVersion2_Can()
    {
        var attributes = Stored(Tapped() with { Seat = HoleSeat.Counterbore }).ToAttributes()
            .Where(a => a.Name != HoleStoredData.FastenerName)
            .ToList();

        Assert.True(HoleStoredData.Read(attributes).Succeeded);
        var version1 = attributes.Select(a => a.Name == HoleStoredData.VersionName ? new HoleAttribute(a.Name, 1) : a);
        Assert.False(HoleStoredData.Read(version1).Succeeded);
    }

    [Fact]
    public void ToAttributes_ResolvedSpec_ReadsBack()
    {
        // The same specs once HoleRules has filled in the defaults.
        foreach (var spec in new[]
                 {
                     Clearance() with { Seat = HoleSeat.Counterbore }, Dowel(), Tapped() with { Seat = HoleSeat.Countersink },
                     CboredDowel(), CsunkDowel(),
                 })
        {
            Assert.True(HoleStoredData.Read(Stored(spec).ToAttributes()).Succeeded);
        }
    }
}
