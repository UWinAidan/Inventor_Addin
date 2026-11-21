using InventorAddin.Core.HoleTable;

namespace InventorAddin.Core.Tests.HoleTable;

public class HoleTableMarkSyncTests
{
    private static IReadOnlySet<string> Roles(params string[] roles) => roles.ToHashSet();

    private static readonly IReadOnlySet<string> Full = Roles(HoleTableMarks.TagRole, HoleTableMarks.CentreMarkRole);
    private static readonly IReadOnlySet<string> FullPrecision = Roles(HoleTableMarks.TagRole, HoleTableMarks.CentreMarkRole, HoleTableMarks.PrecisionMarkRole);

    [Fact]
    public void A_hole_with_complete_marks_and_the_same_tag_is_left_alone()
    {
        var plan = HoleTableMarkSync.Decide(
            [new("a", "A1", false)],
            new Dictionary<string, string> { ["a"] = "A1" },
            new Dictionary<string, IReadOnlySet<string>> { ["a"] = Full });
        Assert.Empty(plan.Place);
        Assert.Empty(plan.Remove);
    }

    [Fact]
    public void A_new_hole_is_marked()
    {
        var plan = HoleTableMarkSync.Decide(
            [new("a", "A1", false), new("b", "A2", false)],
            new Dictionary<string, string> { ["a"] = "A1" },
            new Dictionary<string, IReadOnlySet<string>> { ["a"] = Full });
        Assert.Equal(["b"], plan.Place);
    }

    [Fact]
    public void A_changed_tag_is_marked_again()
    {
        var plan = HoleTableMarkSync.Decide(
            [new("a", "A1", false), new("b", "A2", false)],
            new Dictionary<string, string> { ["a"] = "A1", ["b"] = "A3" },
            new Dictionary<string, IReadOnlySet<string>> { ["a"] = Full, ["b"] = Full });
        Assert.Equal(["b"], plan.Place);
    }

    [Fact]
    public void A_hole_that_is_gone_has_its_marks_removed()
    {
        var plan = HoleTableMarkSync.Decide(
            [new("a", "A1", false)],
            new Dictionary<string, string> { ["a"] = "A1", ["z"] = "A2" },
            new Dictionary<string, IReadOnlySet<string>> { ["a"] = Full, ["z"] = Full });
        Assert.Empty(plan.Place);
        Assert.Equal(["z"], plan.Remove);
    }

    [Fact]
    public void A_missing_mark_is_put_back()
    {
        var plan = HoleTableMarkSync.Decide(
            [new("a", "A1", false)],
            new Dictionary<string, string> { ["a"] = "A1" },
            new Dictionary<string, IReadOnlySet<string>> { ["a"] = Roles(HoleTableMarks.TagRole) });
        Assert.Equal(["a"], plan.Place);
    }

    [Fact]
    public void A_precision_hole_needs_the_precision_mark_and_a_clearance_hole_must_not_have_one()
    {
        var plan = HoleTableMarkSync.Decide(
            [new("p", "A1", true), new("c", "B1", false)],
            new Dictionary<string, string> { ["p"] = "A1", ["c"] = "B1" },
            new Dictionary<string, IReadOnlySet<string>> { ["p"] = Full, ["c"] = FullPrecision });
        Assert.Equal(["p", "c"], plan.Place);
    }

    [Fact]
    public void With_no_previous_tags_every_hole_is_marked_again()
    {
        var plan = HoleTableMarkSync.Decide(
            [new("a", "A1", false)],
            null,
            new Dictionary<string, IReadOnlySet<string>> { ["a"] = Full });
        Assert.Equal(["a"], plan.Place);
    }

    [Fact]
    public void A_hole_with_no_marks_at_all_is_marked()
    {
        var plan = HoleTableMarkSync.Decide(
            [new("a", "A1", false)],
            new Dictionary<string, string> { ["a"] = "A1" },
            new Dictionary<string, IReadOnlySet<string>>());
        Assert.Equal(["a"], plan.Place);
    }

    [Theory]
    [InlineData(0, 0, 0, "")]
    [InlineData(2, 0, 0, "Marked 2 holes on the view.")]
    [InlineData(0, 0, 1, "Removed the marks of 1 hole that is gone.")]
    [InlineData(1, 1, 3, "Marked 1 hole on the view. 1 hole could not be marked (see the log). Removed the marks of 3 holes that are gone.")]
    public void The_summary_names_what_changed(int placed, int skipped, int removed, string expected) =>
        Assert.Equal(expected, HoleTableMarkSync.Summary(placed, skipped, removed));

    [Fact]
    public void The_state_attribute_set_is_the_brand_and_not_the_definitions_or_the_marks()
    {
        Assert.Equal("AWB_HoleTable", HoleTableNames.StateAttributeSetName);
        Assert.NotEqual(HoleTableNames.DrawingAttributeSetName, HoleTableNames.StateAttributeSetName);
        Assert.NotEqual(HoleTableMarks.AttributeSetName, HoleTableNames.StateAttributeSetName);
    }
}

public class HoleTableSheetRecordNameTests
{
    [Fact]
    public void The_sheet_record_set_is_the_brand_and_distinct_from_the_other_sets()
    {
        Assert.Equal("AWB_HoleTableViews", HoleTableNames.SheetRecordAttributeSetName);
        var others = new[]
        {
            HoleTableNames.StateAttributeSetName, HoleTableNames.DrawingAttributeSetName,
            HoleTableKeys.AttributeSetName, HoleTableMarks.AttributeSetName,
        };
        Assert.DoesNotContain(HoleTableNames.SheetRecordAttributeSetName, others);
    }

    [Fact]
    public void The_sheet_record_attribute_never_starts_with_a_digit()
    {
        Assert.Equal("View_0123abcd", HoleTableNames.SheetRecordAttributeName("0123abcd"));
        Assert.Equal("View_a_b", HoleTableNames.SheetRecordAttributeName("a:b"));
    }

    [Fact]
    public void An_unreadable_stored_state_has_its_own_message()
    {
        Assert.Equal("The stored hole table data is version 9; this add-in reads version 1.", HoleTableMessages.StoredStateUnreadable("The stored hole table data is version 9; this add-in reads version 1."));
        Assert.Equal("The stored hole table data could not be read.", HoleTableMessages.StoredStateUnreadable(null));
        Assert.DoesNotContain("Create them first", HoleTableMessages.StoredStateUnreadable(null));
    }
}
