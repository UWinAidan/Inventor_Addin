using InventorAddin.Core.HoleTable;

namespace InventorAddin.Core.Tests.HoleTable;

public class HoleTableNamesTests
{
    [Fact]
    public void Definition_name_has_the_brand_prefix_sheet_view_and_kind()
    {
        Assert.Equal("AWB_HoleTable_k1_k2_Precision", HoleTableNames.Definition("k1", "k2", HoleTableKind.Precision));
        Assert.Equal("AWB_HoleTable_k1_k2_Clearance", HoleTableNames.Definition("k1", "k2", HoleTableKind.Clearance));
    }

    [Fact]
    public void A_task_163_key_is_kept_as_it_is()
    {
        var sheet = Guid.NewGuid().ToString("N");
        var view = Guid.NewGuid().ToString("N");
        Assert.Equal($"AWB_HoleTable_{sheet}_{view}_Clearance", HoleTableNames.Definition(sheet, view, HoleTableKind.Clearance));
    }

    [Fact]
    public void Characters_Inventor_may_refuse_become_underscores()
    {
        Assert.Equal("Sheet_1", HoleTableNames.Clean("Sheet:1"));
        Assert.Equal("Bob_s_view", HoleTableNames.Clean("Bob's view"));
        Assert.Equal("A_B_C", HoleTableNames.Clean("A/B" + (char)92 + "C"));
        Assert.Equal("keep-this_1", HoleTableNames.Clean("keep-this_1"));
    }

    [Fact]
    public void A_blank_name_cleans_to_an_underscore()
    {
        Assert.Equal("_", HoleTableNames.Clean(""));
        Assert.Equal("_", HoleTableNames.Clean(null));
    }

    [Fact]
    public void Transaction_names_carry_the_brand()
    {
        Assert.StartsWith("AWB ", HoleTableNames.CreateTransactionName);
        Assert.StartsWith("AWB ", HoleTableNames.AlignTransactionName);
        Assert.Contains("precision", HoleTableNames.GenerateTransactionName(HoleTableKind.Precision));
    }

    [Fact]
    public void Anchor_is_the_title_block_max_corner()
    {
        var p = HoleTableAlign.Anchor(new SheetBox(1, 2, 30, 5), new SheetBox(0, 0, 40, 40));
        Assert.Equal(new SheetPoint(30, 5), p);
    }

    [Fact]
    public void Anchor_without_a_title_block_is_the_border_bottom_right()
    {
        var p = HoleTableAlign.Anchor(null, new SheetBox(1, 2, 40, 30));
        Assert.Equal(new SheetPoint(40, 2), p);
    }

    [Fact]
    public void Anchor_with_neither_is_null()
    {
        Assert.Null(HoleTableAlign.Anchor(null, null));
    }

    [Fact]
    public void Messages_name_the_tables_drawn()
    {
        Assert.Equal("Drew the Precision and Clearance tables for VIEW1.", HoleTableMessages.Drawn([HoleTableKind.Precision, HoleTableKind.Clearance], "VIEW1"));
        Assert.Equal("Drew the Clearance table for VIEW1.", HoleTableMessages.Drawn([HoleTableKind.Clearance], "VIEW1"));
        Assert.Equal("Aligned 1 table.", HoleTableMessages.Aligned(1));
        Assert.Equal("Aligned 2 tables.", HoleTableMessages.Aligned(2));
    }

    [Fact]
    public void Part_two_onward_has_a_suffix_and_part_one_keeps_the_plain_name()
    {
        Assert.Equal("AWB_HoleTable_k1_k2_Clearance", HoleTableNames.Definition("k1", "k2", HoleTableKind.Clearance, 1));
        Assert.Equal("AWB_HoleTable_k1_k2_Clearance_2", HoleTableNames.Definition("k1", "k2", HoleTableKind.Clearance, 2));
    }

    [Fact]
    public void PartOf_reads_the_part_back_and_ignores_other_names()
    {
        var k = HoleTableKind.Clearance;
        Assert.Equal(1, HoleTableNames.PartOf("AWB_HoleTable_k1_k2_Clearance", "k1", "k2", k));
        Assert.Equal(3, HoleTableNames.PartOf("AWB_HoleTable_k1_k2_Clearance_3", "k1", "k2", k));
        Assert.Equal(0, HoleTableNames.PartOf("AWB_HoleTable_k1_k2_Clearance_1", "k1", "k2", k));
        Assert.Equal(0, HoleTableNames.PartOf("AWB_HoleTable_k1_k2_Clearance_x", "k1", "k2", k));
        Assert.Equal(0, HoleTableNames.PartOf("AWB_HoleTable_k1_k2_Precision", "k1", "k2", k));
        Assert.Equal(0, HoleTableNames.PartOf("AWB_HoleTable_k1_k3_Clearance", "k1", "k2", k));
        Assert.Equal(0, HoleTableNames.PartOf(null, "k1", "k2", k));
    }

    [Fact]
    public void IsViewTable_accepts_parts_of_either_table()
    {
        Assert.True(HoleTableNames.IsViewTable("AWB_HoleTable_k1_k2_Precision", "k1", "k2"));
        Assert.True(HoleTableNames.IsViewTable("AWB_HoleTable_k1_k2_Clearance_2", "k1", "k2"));
        Assert.False(HoleTableNames.IsViewTable("AWB_HoleTable_k1_k9_Clearance_2", "k1", "k2"));
    }
}
