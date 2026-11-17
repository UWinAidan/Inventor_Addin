using System.Globalization;
using InventorAddin.Core.HoleTable;
using InventorAddin.Core.Holes;

namespace InventorAddin.Core.Tests.HoleTable;

public class HoleTableStateTests
{
    private static HoleTableHole Plain(string id, double dia, double x, double y, bool precision) => new(
        id, 1, "F", HoleUnits.Metric, HoleTableType.Plain, precision, dia, HoleTermination.ThroughAll, null, false, null,
        null, null, null, null, null, null, false, null, x, y);

    private static string Id(int i) => $"Hole {i}#k\"{i}é";

    private static HoleTableState Sample()
    {
        var holes = new List<HoleTableHole>();
        for (var i = 0; i < 50; i++)
        {
            holes.Add(Plain(Id(i), 0.3 + (i % 5) * 0.1, i, i / 5, i % 7 == 0));
        }

        var edits = HoleTableEdits.None
            .WithSwapped(Id(1)).WithSwapped(Id(2))
            .WithDeleted(Id(3))
            .WithSplit(Id(4)).WithSplit(Id(9))
            .WithNote(HoleTableKind.Clearance, "line1\nline2", "Deburr \"all\" edges")
            .WithNote(HoleTableKind.Precision, "dia 5", "Ream")
            .WithSplitNote(Id(4), "Special");
        var plan = HoleTablePlan.Build(holes, edits, new SheetPoint(0, 0), 1, HoleUnits.Metric);
        Assert.Equal(49, plan.ToStoredTags().ByHole.Count);
        return HoleTableState.From("Sheet:1", "VIEW1", edits, plan);
    }

    [Fact]
    public void Round_trip_gives_an_equal_state()
    {
        var state = Sample();
        var read = HoleTableStateJson.Read(HoleTableStateJson.Write(state));
        Assert.Null(read.Reason);
        Assert.Equal(state, read.State);
        Assert.Equal(2, read.State!.Edits.SwappedIds.Count);
        Assert.Single(read.State.Edits.DeletedIds);
        Assert.Equal(2, read.State.Edits.SplitIds.Count);
        Assert.Equal(2, read.State.Edits.NotesByDescription.Count);
        Assert.Single(read.State.Edits.NotesBySplitId);
    }

    [Fact]
    public void Write_is_deterministic()
    {
        Assert.Equal(HoleTableStateJson.Write(Sample()), HoleTableStateJson.Write(Sample()));
    }

    [Fact]
    public void A_different_state_is_not_equal()
    {
        var state = Sample();
        Assert.NotEqual(state, state with { ViewName = "VIEW2" });
        Assert.NotEqual(state, state with { Edits = state.Edits.WithSwapped("another") });
    }

    [Fact]
    public void A_wrong_version_returns_null_with_a_reason()
    {
        var read = HoleTableStateJson.Read("{\"v\":99,\"groups\":[]}");
        Assert.Null(read.State);
        Assert.Contains("version 99", read.Reason);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not json")]
    [InlineData("{\"v\":1,")]
    [InlineData("[1,2]")]
    [InlineData("null")]
    [InlineData("{\"v\":1,\"groups\":[{\"l\":\"A\",\"t\":\"Nope\",\"h\":{}}]}")]
    [InlineData("{\"v\":1,\"groups\":[{\"t\":\"Precision\"}]}")]
    [InlineData("{\"v\":1,\"groups\":[{\"l\":\"A\",\"t\":\"Precision\",\"h\":{\"x\":\"y\"}}]}")]
    [InlineData("{\"v\":1,\"notes\":[{\"t\":\"Precision\"}]}")]
    [InlineData("{\"v\":1,\"notes\":[null]}")]
    [InlineData("{\"v\":1,\"groups\":[null]}")]
    [InlineData("{\"v\":1,\"splitNotes\":[null]}")]
    [InlineData("{\"v\":1,\"swap\":[null]}")]
    public void Broken_text_returns_null_with_a_reason_and_never_throws(string text)
    {
        var read = HoleTableStateJson.Read(text);
        Assert.Null(read.State);
        Assert.False(string.IsNullOrEmpty(read.Reason));
    }

    [Fact]
    public void Null_text_gives_a_reason()
    {
        Assert.NotNull(HoleTableStateJson.Read(null).Reason);
    }

    [Fact]
    public void Culture_does_not_change_the_text()
    {
        var old = CultureInfo.CurrentCulture;
        try
        {
            var state = Sample();
            var a = HoleTableStateJson.Write(state);
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            Assert.Equal(a, HoleTableStateJson.Write(state));
            Assert.Equal(state, HoleTableStateJson.Read(a).State);
        }
        finally
        {
            CultureInfo.CurrentCulture = old;
        }
    }
}
