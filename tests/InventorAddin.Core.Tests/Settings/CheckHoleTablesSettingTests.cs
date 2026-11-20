using InventorAddin.Core.Settings;
using InventorAddin.Core.ViewModels;

namespace InventorAddin.Core.Tests.Settings;

public sealed class CheckHoleTablesSettingTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "InventorAddinTests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder))
            Directory.Delete(_folder, recursive: true);
    }

    [Fact]
    public void DefaultsToOn()
    {
        Assert.True(new AddinSettings().CheckHoleTablesAutomatically);
    }

    [Fact]
    public void AFileWithoutTheKey_LoadsAsOn()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(Path.Combine(_folder, SettingsStore.FileName), """{ "schemaVersion": 1, "defaultDesigner": "A. Person" }""");

        Assert.True(new SettingsStore(_folder).Load().Settings.CheckHoleTablesAutomatically);
    }

    [Fact]
    public void TheValueRoundTrips()
    {
        var store = new SettingsStore(_folder);
        store.Save(new AddinSettings { CheckHoleTablesAutomatically = false });
        Assert.False(store.Load().Settings.CheckHoleTablesAutomatically);

        store.Save(new AddinSettings { CheckHoleTablesAutomatically = true });
        Assert.True(store.Load().Settings.CheckHoleTablesAutomatically);
    }

    [Fact]
    public void ItIsPartOfEqualityAndTheHash()
    {
        var on = new AddinSettings();
        var off = new AddinSettings { CheckHoleTablesAutomatically = false };
        Assert.NotEqual(on, off);
        Assert.NotEqual(on.GetHashCode(), off.GetHashCode());
        Assert.Equal(on, off with { CheckHoleTablesAutomatically = true });
    }

    [Fact]
    public void TheViewModel_EditsItAndMakesTheWindowDirty()
    {
        var vm = new SettingsViewModel(new AddinSettings(), new SettingsStore(_folder));
        Assert.True(vm.CheckHoleTablesAutomatically);

        vm.CheckHoleTablesAutomatically = false;

        Assert.False(vm.CheckHoleTablesAutomatically);
        Assert.True(vm.IsDirty);
        Assert.EndsWith("hole table check off", vm.HolesSummary);

        vm.CheckHoleTablesAutomatically = true;
        Assert.False(vm.IsDirty);
    }

    [Fact]
    public void TheLabelHasAnAccessKeyThatNoOtherSettingsLabelUses()
    {
        Assert.Equal("Check hole _tables when a drawing opens or updates", SettingsViewModel.CheckHoleTablesLabel);
        string[] others =
        [
            "_Default designer", "Other _names", "New file _units", "_Register folder", "_Browse...", "_Work folder",
            "Brows_e...", "_Other folders", "_Material library", "_Finish list", "_Hole data",
            "Use _Inventor's Hole command", "_Show developer tools",
        ];
        Assert.DoesNotContain('t', others.Select(AccessKey));
        Assert.Equal('t', AccessKey(SettingsViewModel.CheckHoleTablesLabel));
    }

    private static char AccessKey(string label) => char.ToLowerInvariant(label[label.IndexOf('_') + 1]);

    [Fact]
    public void TheHolesSummary_SaysWhenTheCheckIsOff()
    {
        Assert.Equal("starter hole data, AWB Hole", SettingsSummaries.Holes("", false));
        Assert.Equal("starter hole data, AWB Hole", SettingsSummaries.Holes("", false, true));
        Assert.Equal("starter hole data, AWB Hole, hole table check off", SettingsSummaries.Holes("", false, false));
    }
}
