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
}
