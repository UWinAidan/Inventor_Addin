using InventorAddin.Core.HoleTable;
using InventorAddin.Core.Tests.HoleTable;
using InventorAddin.Core.ViewModels;

namespace InventorAddin.Core.Tests.ViewModels;

public class HoleTableOutOfDatePromptViewModelTests
{
    private static HoleTableStoredViewCheck View(string key, string reason = "1 hole moved.") => new("s1", "Sheet:1", key, key.ToUpperInvariant(), reason);

    [Fact]
    public void ShowsTheHeadingAndOneLinePerView()
    {
        var vm = new HoleTableOutOfDatePromptViewModel(new StoredFakeSource(), "Plate.idw", [View("v1"), View("v2", "1 hole added.")]);
        Assert.Equal("Some hole tables in Plate.idw are out of date:", vm.Heading);
        Assert.Equal(["V1 on Sheet:1: 1 hole moved.", "V2 on Sheet:1: 1 hole added."], vm.ViewLines);
        Assert.Equal("Update now", vm.UpdateNowText);
        Assert.Equal("Later", vm.LaterText);
    }

    [Fact]
    public void UpdateNow_EveryViewWorks_ClosesWithUpdated()
    {
        var source = new StoredFakeSource();
        source.Move("P#1", 3);
        var vm = new HoleTableOutOfDatePromptViewModel(source, "Plate.idw", [View("v1")]);
        var outcomes = new List<HoleTableOutOfDatePromptOutcome>();
        vm.CloseRequested += outcomes.Add;

        vm.UpdateNowCommand.Execute(null);

        Assert.Equal([HoleTableOutOfDatePromptOutcome.Updated], outcomes);
        Assert.Contains("update", source.Calls);
        Assert.StartsWith("V1 on Sheet:1: Updated.", vm.ResultText);
    }

    [Fact]
    public void UpdateNow_OneFails_StaysOpen_ListsIt_AndLaterSaysClose()
    {
        var source = new StoredFakeSource { UpdateThrows = true };
        source.Move("P#1", 3);
        var vm = new HoleTableOutOfDatePromptViewModel(source, "Plate.idw", [View("v1")]);
        var outcomes = new List<HoleTableOutOfDatePromptOutcome>();
        vm.CloseRequested += outcomes.Add;

        vm.UpdateNowCommand.Execute(null);

        Assert.Empty(outcomes);
        Assert.Equal("Some hole tables in Plate.idw could not be updated:", vm.Heading);
        Assert.Equal(["V1 on Sheet:1: Update failed. Could not update the tables: boom"], vm.ViewLines);
        Assert.Equal("Close", vm.LaterText);
        Assert.Contains("Update failed.", vm.ResultText);

        vm.LaterCommand.Execute(null);
        Assert.Equal([HoleTableOutOfDatePromptOutcome.Later], outcomes);
    }

    [Fact]
    public void UpdateNow_EachViewRunsThroughTheRunner_AndOnlyFailuresRemain()
    {
        var calls = new List<string>();
        HoleTableOutOfDatePromptViewModel.UpdateRunner runner = (string s, string v, string sn, string vn, out string text) =>
        {
            calls.Add(v);
            text = v == "v2" ? "nope" : "ok.";
            return v != "v2";
        };
        var vm = new HoleTableOutOfDatePromptViewModel(new StoredFakeSource(), "D.idw", [View("v1"), View("v2"), View("v3")], runner);

        vm.UpdateNowCommand.Execute(null);

        Assert.Equal(["v1", "v2", "v3"], calls);
        Assert.Equal(["V2 on Sheet:1: Update failed. nope"], vm.ViewLines);
    }

    [Fact]
    public void Later_ClosesWithLater()
    {
        var vm = new HoleTableOutOfDatePromptViewModel(new StoredFakeSource(), "Plate.idw", [View("v1")]);
        var outcomes = new List<HoleTableOutOfDatePromptOutcome>();
        vm.CloseRequested += outcomes.Add;
        vm.LaterCommand.Execute(null);
        Assert.Equal([HoleTableOutOfDatePromptOutcome.Later], outcomes);
    }
}
