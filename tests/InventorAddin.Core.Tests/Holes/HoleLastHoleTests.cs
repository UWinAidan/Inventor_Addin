using InventorAddin.Core.Holes;
using InventorAddin.Core.Settings;
using static InventorAddin.Core.Tests.Holes.HoleTestWorkbook;

namespace InventorAddin.Core.Tests.Holes;

/// <summary>The last hole kept with OK, per units, in the settings (task 173).</summary>
public sealed class HoleLastHoleTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "awb-last-hole-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, true);
        }
    }

    private static HoleSpec TappedCbore() => Tapped("M5") with
    {
        Seat = HoleSeat.Counterbore,
        Fastener = "TCAP",
        Termination = HoleTermination.Distance,
        DrillPoint = "90 deg",
    };

    [Fact]
    public void Remember_KeepsTheChoices_ForTheUnitsOfTheHole()
    {
        var updated = HoleLastHole.Remember(new AddinSettings(), TappedCbore());

        Assert.NotNull(updated);
        var last = updated.LastHoleMm!;
        Assert.Equal(
            ("Tapped", "M5", "Counterbore", "TCAP", "Distance", "90 deg"),
            (last.Kind, last.Size, last.Seat, last.Fastener, last.Termination, last.DrillPoint));
        Assert.Null(updated.LastHoleIn);
        Assert.Equal(last, HoleLastHole.From(updated).For(HoleUnits.Metric));
        Assert.Null(HoleLastHole.From(updated).For(HoleUnits.Imperial));
    }

    [Fact]
    public void Remember_AnImperialHole_GoesToImperial()
    {
        var updated = HoleLastHole.Remember(new AddinSettings(), Clearance("#5") with { Units = HoleUnits.Imperial });

        Assert.NotNull(updated!.LastHoleIn);
        Assert.Null(updated.LastHoleMm);
    }

    [Fact]
    public void Remember_TheSameHoleAgain_HasNothingToSave()
    {
        var once = HoleLastHole.Remember(new AddinSettings(), TappedCbore())!;

        Assert.Null(HoleLastHole.Remember(once, TappedCbore()));
    }

    [Fact]
    public void Remember_LeavesOutWhatDoesNotApply()
    {
        // A through hole has no drill point; a hole with no seat has no fastener or tolerance.
        var spec = Clearance("T5") with { Fastener = "TCAP", DrillPoint = "90 deg", Tolerance = "N bore" };

        var last = HoleLastHole.Remember(new AddinSettings(), spec)!.LastHoleMm!;

        Assert.Null(last.Fastener);
        Assert.Null(last.DrillPoint);
        Assert.Null(last.Tolerance);
    }

    [Fact]
    public void RememberKept_SavesTheDepthSeatAndHoleTogether()
    {
        var spec = CboredDowel("5 mm") with
        {
            CboreDiameter = 8m,
            CboreDepth = 2m,
            Termination = HoleTermination.Distance,
            Depth = 12m,
            DrillPoint = "90 deg",
        };

        var updated = HoleLastHole.RememberKept(new AddinSettings(), spec)!;

        Assert.Equal(12m, updated.LastBlindDepthMm);
        Assert.Equal(new HoleDowelSeat(8m, 2m, null, null), updated.LastDowelSeatsMm["5 mm"]);
        Assert.Equal("5 mm", updated.LastHoleMm!.Size);
        Assert.Null(HoleLastHole.RememberKept(updated, spec));
    }

    [Fact]
    public void ACustomToleranceWithItsCodeAndLimits_IsKept()
    {
        var spec = Clearance("T5") with { Toleranced = true, CustomTolerance = true, CustomCode = " H8 " };
        var limits = Clearance("T5") with
        {
            Toleranced = true,
            CustomTolerance = true,
            CustomUpper = 0.05m,
            CustomLower = -0.02m,
        };

        var withCode = HoleLastHole.Remember(new AddinSettings(), spec)!.LastHoleMm!;
        var withLimits = HoleLastHole.Remember(new AddinSettings(), limits)!.LastHoleMm!;

        Assert.Equal((true, true, "H8"), (withCode.Toleranced, withCode.CustomTolerance, withCode.CustomCode));
        Assert.Equal(
            (0.05m, -0.02m, (string?)null),
            (withLimits.CustomUpper, withLimits.CustomLower, withLimits.CustomCode));
    }

    [Fact]
    public void TheSettingsFile_KeepsItAcrossARestart_AndAnOldFileLoadsWithNone()
    {
        var store = new SettingsStore(_folder);
        store.Save(HoleLastHole.Remember(new AddinSettings(), TappedCbore())!);

        var loaded = new SettingsStore(_folder).Load().Settings;

        Assert.Equal("M5", loaded.LastHoleMm!.Size);
        Assert.Equal(HoleKind.Tapped, loaded.LastHoleMm.KindValue);
        Assert.Equal(HoleSeat.Counterbore, loaded.LastHoleMm.SeatValue);
        Assert.Equal(HoleTermination.Distance, loaded.LastHoleMm.TerminationValue);

        Directory.CreateDirectory(_folder);
        File.WriteAllText(Path.Combine(_folder, SettingsStore.FileName), "{ \"schemaVersion\": 1 }");
        var old = new SettingsStore(_folder).Load().Settings;
        Assert.Null(old.LastHoleMm);
        Assert.Null(old.LastHoleIn);
    }

    [Fact]
    public void AValueThatDoesNotReadBack_IsIgnored()
    {
        var last = new HoleLastChoices { Kind = "Nonsense", Seat = "Wobbly", Termination = "Sideways" };

        Assert.Null(last.KindValue);
        Assert.Null(last.SeatValue);
        Assert.Null(last.TerminationValue);
    }

    [Fact]
    public void SettingsWithDifferentLastHoles_AreNotEqual()
    {
        var a = HoleLastHole.Remember(new AddinSettings(), TappedCbore())!;
        var b = HoleLastHole.Remember(new AddinSettings(), Tapped("M3"))!;

        Assert.NotEqual(a, b);
        Assert.Equal(a, HoleLastHole.Remember(new AddinSettings(), TappedCbore()));
    }
}
