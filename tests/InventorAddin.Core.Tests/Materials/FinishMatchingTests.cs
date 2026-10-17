using InventorAddin.Core.Materials;

namespace InventorAddin.Core.Tests.Materials;

public sealed class FinishMatchingTests
{
    private static readonly LibraryMaterial MetalOne = new("Metalone, Hard", "Metal", "Test Shine");
    private static readonly LibraryMaterial Plastic = new("Plasticone", "Polymer", null);
    private static readonly LibraryMaterial NoCategory = new("Oddium", "", null);

    private static Finish Finish(string code, string name, FinishKind kind, params string[] appliesTo) =>
        new(code, name, kind, appliesTo, null, "0", "0", false, "");

    private static readonly Finish Shiny = Finish("SH", "Shiny plate", FinishKind.Plating, "Metal");
    private static readonly Finish Dark = Finish("DK", "Dark conversion", FinishKind.Conversion, "metal ", "Polymer");
    private static readonly Finish Any = Finish("ANY", "Any process", FinishKind.HeatTreat);
    private static readonly Finish RedPaint = Finish("PR", "Red paint", FinishKind.Paint);
    private static readonly Finish Special = Finish("SP", "Special coat", FinishKind.Anodize, " oddium ");

    private static readonly Finish[] List = [Shiny, Dark, Any, RedPaint, Special];

    private static IReadOnlyList<string> Codes(IEnumerable<Finish> finishes) => finishes.Select(f => f.Code).ToList();

    [Fact]
    public void Suits_EmptyAppliesTo_SuitsEverything()
    {
        Assert.True(FinishMatching.Suits(Any, MetalOne));
        Assert.True(FinishMatching.Suits(Any, Plastic));
        Assert.True(FinishMatching.Suits(Any, NoCategory));
    }

    [Fact]
    public void Suits_Category_IgnoringCaseAndSpaces()
    {
        Assert.True(FinishMatching.Suits(Shiny, MetalOne));
        Assert.True(FinishMatching.Suits(Dark, MetalOne));
        Assert.True(FinishMatching.Suits(Dark, Plastic));
        Assert.False(FinishMatching.Suits(Shiny, Plastic));
    }

    [Fact]
    public void Suits_MaterialName_IgnoringCaseAndSpaces()
    {
        Assert.True(FinishMatching.Suits(Special, NoCategory));
        Assert.False(FinishMatching.Suits(Special, MetalOne));
    }

    [Fact]
    public void Suits_MaterialWithNoCategory_OnlyByNameOrAll()
    {
        Assert.False(FinishMatching.Suits(Shiny, NoCategory));
        Assert.False(FinishMatching.Suits(Dark, NoCategory));
    }

    [Fact]
    public void Suits_BlankEntry_DoesNotMatchBlankCategory()
    {
        var blankEntry = Finish("BL", "Blank entry", FinishKind.Plating, " ");

        Assert.False(FinishMatching.Suits(blankEntry, NoCategory));
    }

    [Fact]
    public void Suits_PartOfName_DoesNotMatch()
    {
        var partial = Finish("PA", "Partial", FinishKind.Plating, "Metalone");

        Assert.False(FinishMatching.Suits(partial, MetalOne));
    }

    [Fact]
    public void For_SuitingFinishes_InFileOrder()
    {
        Assert.Equal(["SH", "DK", "ANY", "PR"], Codes(FinishMatching.For(List, MetalOne, true, null)));
        Assert.Equal(["DK", "ANY", "PR"], Codes(FinishMatching.For(List, Plastic, true, null)));
        Assert.Equal(["ANY", "PR", "SP"], Codes(FinishMatching.For(List, NoCategory, true, null)));
    }

    [Fact]
    public void For_ShowPaintsOff_HidesPaint()
    {
        Assert.Equal(["SH", "DK", "ANY"], Codes(FinishMatching.For(List, MetalOne, false, null)));
    }

    [Fact]
    public void For_Search_OnName_IgnoringCase_Trimmed()
    {
        Assert.Equal(["SH"], Codes(FinishMatching.For(List, MetalOne, true, "  PLATE ")));
    }

    [Fact]
    public void For_Search_OnCode()
    {
        Assert.Equal(["DK"], Codes(FinishMatching.For(List, MetalOne, true, "dk")));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void For_BlankSearch_KeepsEverything(string? search)
    {
        Assert.Equal(4, FinishMatching.For(List, MetalOne, true, search).Count);
    }

    [Fact]
    public void For_Combined_PaintsHiddenAndSearch()
    {
        // "d" is in "Dark conversion" and "Red paint" only; the paint is hidden when paints are off.
        Assert.Equal(["DK"], Codes(FinishMatching.For(List, MetalOne, false, "d")));
        Assert.Equal(["DK", "PR"], Codes(FinishMatching.For(List, MetalOne, true, "d")));
    }

    [Fact]
    public void For_NoMaterial_IsEmpty()
    {
        Assert.Empty(FinishMatching.For(List, null, true, null));
    }
}
