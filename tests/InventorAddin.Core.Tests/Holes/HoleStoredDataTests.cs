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
        Dowel("5 mm") with { Seat = HoleSeat.Counterbore, Termination = HoleTermination.Distance, Depth = 14m },
        Dowel("5 mm") with { Seat = HoleSeat.Countersink, PrecisionFit = PrecisionFit.Press },
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
        Assert.Equal(new HoleAttribute("Version", 1), attributes[0]);
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
    [InlineData(2)]
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
        { Clearance() with { Seat = HoleSeat.Counterbore }, HoleStoredData.FastenerName },
        { Clearance() with { Seat = HoleSeat.Countersink, Fastener = " " }, HoleStoredData.FastenerName },
        { Dowel(), HoleStoredData.ToleranceName },
        { Tapped() with { Seat = HoleSeat.Counterbore }, HoleStoredData.FastenerName },
        { Dowel("5 mm") with { Seat = HoleSeat.Countersink, Tolerance = "Test bore" }, HoleStoredData.FastenerName },
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
    public void ASeatedDowel_WithoutItsFastener_CannotBeRead()
    {
        var attributes = Stored(Dowel("5 mm") with { Seat = HoleSeat.Counterbore }).ToAttributes()
            .Where(a => a.Name != HoleStoredData.FastenerName);

        var read = HoleStoredData.Read(attributes);

        Assert.False(read.Succeeded);
        Assert.Contains(HoleStoredData.FastenerName, read.Message);
    }

    [Fact]
    public void ToAttributes_ResolvedSpec_ReadsBack()
    {
        // The same specs once HoleRules has filled in the defaults.
        foreach (var spec in new[]
                 {
                     Clearance() with { Seat = HoleSeat.Counterbore }, Dowel(), Tapped() with { Seat = HoleSeat.Countersink },
                     Dowel("5 mm") with { Seat = HoleSeat.Counterbore },
                 })
        {
            Assert.True(HoleStoredData.Read(Stored(spec).ToAttributes()).Succeeded);
        }
    }
}
