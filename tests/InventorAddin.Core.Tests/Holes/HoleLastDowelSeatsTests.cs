using InventorAddin.Core.Holes;
using InventorAddin.Core.Settings;
using static InventorAddin.Core.Tests.Holes.HoleTestWorkbook;

namespace InventorAddin.Core.Tests.Holes;

public sealed class HoleLastDowelSeatsTests
{
    private static Dictionary<string, HoleDowelSeat> Map(string size, HoleDowelSeat seat) => new() { [size] = seat };

    [Fact]
    public void For_GivesTheSeatOfTheUnitsAndSize_IgnoringCaseAndSpaces()
    {
        var seats = new HoleLastDowelSeats(
            Map("5 mm", new HoleDowelSeat(8m, 2m, null, null)),
            Map("1/4\"", new HoleDowelSeat(null, null, 0.3m, 82m)));

        Assert.Equal(new HoleDowelSeat(8m, 2m, null, null), seats.For(HoleUnits.Metric, " 5 MM "));
        Assert.Equal(new HoleDowelSeat(null, null, 0.3m, 82m), seats.For(HoleUnits.Imperial, "1/4\""));
        Assert.Equal(HoleDowelSeat.Empty, seats.For(HoleUnits.Imperial, "5 mm"));
        Assert.Equal(HoleDowelSeat.Empty, seats.For(HoleUnits.Metric, "6 mm"));
        Assert.Equal(HoleDowelSeat.Empty, seats.For(HoleUnits.Metric, null));
        Assert.Equal(HoleDowelSeat.Empty, HoleLastDowelSeats.None.For(HoleUnits.Metric, "5 mm"));
    }

    [Fact]
    public void For_DropsValuesASeatCannotTake()
    {
        var seats = new HoleLastDowelSeats(Map("5 mm", new HoleDowelSeat(0m, -1m, 7m, 180m)), null);

        Assert.Equal(new HoleDowelSeat(null, null, 7m, null), seats.For(HoleUnits.Metric, "5 mm"));
    }

    [Fact]
    public void From_ReadsTheSettingsFile()
    {
        var settings = new AddinSettings { LastDowelSeatsIn = Map("1/4\"", new HoleDowelSeat(0.3m, 0.1m, null, null)) };

        var seats = HoleLastDowelSeats.From(settings);

        Assert.Equal(new HoleDowelSeat(0.3m, 0.1m, null, null), seats.For(HoleUnits.Imperial, "1/4\""));
        Assert.Equal(HoleDowelSeat.Empty, HoleLastDowelSeats.From(new AddinSettings()).For(HoleUnits.Metric, "5 mm"));
    }

    [Fact]
    public void Remember_KeepsACounterbore_ForItsUnitsAndSize()
    {
        var updated = HoleLastDowelSeats.Remember(new AddinSettings(), CboredDowel() with { CboreDiameter = 8m, CboreDepth = 2m });

        Assert.NotNull(updated);
        Assert.Equal(new HoleDowelSeat(8m, 2m, null, null), updated.LastDowelSeatsMm["5 mm"]);
        Assert.Empty(updated.LastDowelSeatsIn);
    }

    [Fact]
    public void Remember_ACountersink_KeepsTheCounterboreOfTheSize_AndAnExistingKeysSpelling()
    {
        var settings = new AddinSettings { LastDowelSeatsMm = Map("5 MM", new HoleDowelSeat(8m, 2m, 6m, 82m)) };

        var updated = HoleLastDowelSeats.Remember(settings, CsunkDowel() with { CsinkDiameter = 7m, CsinkAngleDeg = 100m });

        Assert.NotNull(updated);
        var seat = Assert.Single(updated.LastDowelSeatsMm);
        Assert.Equal("5 MM", seat.Key);
        Assert.Equal(new HoleDowelSeat(8m, 2m, 7m, 100m), seat.Value);
    }

    [Fact]
    public void Remember_AnImperialDowel_KeepsOtherSizes()
    {
        var settings = new AddinSettings { LastDowelSeatsIn = Map("1/8\"", new HoleDowelSeat(0.2m, 0.06m, null, null)) };
        var kept = CsunkDowel("1/7\"") with { Units = HoleUnits.Imperial, CsinkDiameter = 0.2m, CsinkAngleDeg = 90m };

        var updated = HoleLastDowelSeats.Remember(settings, kept);

        Assert.NotNull(updated);
        Assert.Equal(2, updated.LastDowelSeatsIn.Count);
        Assert.Equal(new HoleDowelSeat(null, null, 0.2m, 90m), updated.LastDowelSeatsIn["1/7\""]);
        Assert.Empty(updated.LastDowelSeatsMm);
    }

    [Fact]
    public void Remember_TheSameValuesAgain_HasNothingToSave()
    {
        var settings = HoleLastDowelSeats.Remember(new AddinSettings(), CboredDowel())!;

        Assert.Null(HoleLastDowelSeats.Remember(settings, CboredDowel()));
    }

    [Fact]
    public void Remember_NothingForAnUnseatedDowel_OrAnyOtherKind()
    {
        var settings = new AddinSettings();

        Assert.Null(HoleLastDowelSeats.Remember(settings, Dowel("5 mm")));
        Assert.Null(HoleLastDowelSeats.Remember(settings, CboredDowel() with { Kind = HoleKind.Clearance, Size = "T5" }));
        Assert.Null(HoleLastDowelSeats.Remember(settings, Tapped() with { Seat = HoleSeat.Counterbore }));
        Assert.Null(HoleLastDowelSeats.Remember(settings, CboredDowel() with { CboreDiameter = null, CboreDepth = 0m }));
        Assert.Null(HoleLastDowelSeats.SeatToRemember(Dowel()));
    }

    [Fact]
    public void SeatToRemember_IsTheSeatsValues_TheOtherSeatsNull()
    {
        var remembered = HoleLastDowelSeats.SeatToRemember(CboredDowel() with { CsinkDiameter = 6m, CboreDepthExpression = "d0" });

        Assert.Equal((HoleUnits.Metric, "5 mm", new HoleDowelSeat(9.5m, 5.2m, null, null)), remembered);
    }

    [Fact]
    public void RememberKept_KeepsTheBlindDepthAndTheDowelSeat_InOneChange()
    {
        var kept = CboredDowel() with { Termination = HoleTermination.Distance, Depth = 14m };

        var updated = HoleLastDowelSeats.RememberKept(new AddinSettings(), kept);

        Assert.NotNull(updated);
        Assert.Equal(14m, updated.LastBlindDepthMm);
        Assert.Equal(new HoleDowelSeat(9.5m, 5.2m, null, null), updated.LastDowelSeatsMm["5 mm"]);
        Assert.Null(HoleLastDowelSeats.RememberKept(updated, kept));

        // Only one of the two.
        Assert.Equal(20m, HoleLastDowelSeats.RememberKept(updated, kept with { Depth = 20m })!.LastBlindDepthMm);
        Assert.Empty(HoleLastDowelSeats.RememberKept(new AddinSettings(), Dowel() with { Termination = HoleTermination.Distance, Depth = 9m })!.LastDowelSeatsMm);
        Assert.Null(HoleLastDowelSeats.RememberKept(new AddinSettings(), Clearance()));
    }

    [Fact]
    public void DowelSeat_Or_FillsEachMissingValue()
    {
        var seat = new HoleDowelSeat(8m, null, null, 100m).Or(new HoleDowelSeat(9m, 2m, 7m, 90m));

        Assert.Equal(new HoleDowelSeat(8m, 2m, 7m, 100m), seat);
        Assert.Equal(new HoleDowelSeat(8m, null, null, null), new HoleDowelSeat(8m, null, null, null).Or(null));
        Assert.False(seat.IsEmpty);
    }
}
