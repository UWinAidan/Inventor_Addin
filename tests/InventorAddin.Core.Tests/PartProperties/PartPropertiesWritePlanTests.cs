using InventorAddin.Core.Models;
using InventorAddin.Core.PartProperties;

namespace InventorAddin.Core.Tests.PartProperties;

public sealed class PartPropertiesWritePlanTests
{
    /// <summary>A saved part whose Description already matches its file name, so it needs no sync.</summary>
    private static readonly PartPropertiesSnapshot Original = new()
    {
        FullFileName = @"C:\Work\Bracket.ipt",
        DocumentKind = DocumentKind.Part,
        PartNumber = "TEST-0001",
        Description = "Bracket",
        PartType = "M",
        Designer = "A. Person",
        Detailer = "B. Person",
        Cost = 12.5m,
        Material = "Test Steel",
        Finish = "Test Paint",
        WeightDisplay = "1.234 kg",
    };

    /// <summary>The edited values that match <see cref="Original"/> exactly.</summary>
    private static readonly PartEditedValues Unchanged = new("M", "A. Person", "B. Person", 12.5m);

    private static IReadOnlyList<PropertyWrite> Build(PartPropertiesSnapshot original, PartEditedValues edited) =>
        PartPropertiesWritePlan.Build(original, edited);

    // ---- No change ----

    [Fact]
    public void NoChange_GivesNoWrites()
    {
        Assert.Empty(Build(Original, Unchanged));
    }

    [Fact]
    public void WhitespaceOnlyEdits_GiveNoWrites()
    {
        var edited = new PartEditedValues("  M ", "\tA. Person  ", " B. Person", 12.50m);

        Assert.Empty(Build(Original, edited));
    }

    [Fact]
    public void WhitespaceAroundOriginalValues_GivesNoWrites()
    {
        var original = Original with { Description = " Bracket ", PartType = "M ", Designer = " A. Person", Detailer = "B. Person\t" };

        Assert.Empty(Build(original, Unchanged));
    }

    [Fact]
    public void NullAndBlank_AreTheSameValue()
    {
        var original = Original with { PartType = null, Designer = "", Detailer = "   ", Cost = null };

        Assert.Empty(Build(original, new PartEditedValues("", null, null, null)));
    }

    // ---- Each field changed alone ----

    [Fact]
    public void PartTypeChanged_SetsPartType()
    {
        var writes = Build(Original, Unchanged with { PartType = "P" });

        Assert.Equal(new[] { PropertyWrite.SetText(PartPropertyStorage.PartType, "P") }, writes);
    }

    [Fact]
    public void PartTypeChanged_ComparedAsTextIncludingCase()
    {
        var writes = Build(Original, Unchanged with { PartType = "m" });

        Assert.Equal(new[] { PropertyWrite.SetText(PartPropertyStorage.PartType, "m") }, writes);
    }

    [Fact]
    public void DesignerChanged_SetsTrimmedDesigner()
    {
        var writes = Build(Original, Unchanged with { Designer = "  C. Person " });

        Assert.Equal(new[] { PropertyWrite.SetText(PartPropertyStorage.Designer, "C. Person") }, writes);
    }

    [Fact]
    public void DetailerChanged_SetsDetailer()
    {
        var writes = Build(Original, Unchanged with { Detailer = "C. Person" });

        Assert.Equal(new[] { PropertyWrite.SetText(PartPropertyStorage.Detailer, "C. Person") }, writes);
    }

    [Fact]
    public void DetailerAdded_WhenFileHadNone_SetsDetailer()
    {
        var writes = Build(Original with { Detailer = null }, Unchanged with { Detailer = "A. Person" });

        Assert.Equal(new[] { PropertyWrite.SetText(PartPropertyStorage.Detailer, "A. Person") }, writes);
    }

    [Fact]
    public void CostChanged_SetsCost()
    {
        var writes = Build(Original, Unchanged with { Cost = 7.25m });

        Assert.Equal(new[] { PropertyWrite.SetCurrency(PartPropertyStorage.Cost, 7.25m) }, writes);
    }

    // ---- Clearing ----

    [Fact]
    public void ClearStandardField_SetsEmptyText()
    {
        var writes = Build(Original, Unchanged with { Designer = "  " });

        Assert.Equal(new[] { PropertyWrite.SetText(PartPropertyStorage.Designer, "") }, writes);
    }

    [Fact]
    public void ClearCustomFieldThatExisted_RemovesIt()
    {
        var writes = Build(Original, Unchanged with { PartType = null, Detailer = "" });

        Assert.Equal(
            new[]
            {
                PropertyWrite.Remove(PartPropertyStorage.PartType),
                PropertyWrite.Remove(PartPropertyStorage.Detailer),
            },
            writes);
    }

    [Fact]
    public void ClearCustomFieldThatDidNotExist_GivesNoWrite()
    {
        var original = Original with { PartType = null, Detailer = "" };

        Assert.Empty(Build(original, Unchanged with { PartType = " ", Detailer = null }));
    }

    // ---- Cost ----

    [Fact]
    public void CostZeroInFile_AndBlankEdit_GivesNoWrite()
    {
        Assert.Empty(Build(Original with { Cost = 0m }, Unchanged with { Cost = null }));
    }

    [Fact]
    public void CostBlankInFile_AndZeroEdit_GivesNoWrite()
    {
        Assert.Empty(Build(Original with { Cost = null }, Unchanged with { Cost = 0m }));
    }

    [Fact]
    public void ClearNonZeroCost_SetsZero()
    {
        var writes = Build(Original, Unchanged with { Cost = null });

        Assert.Equal(new[] { PropertyWrite.SetCurrency(PartPropertyStorage.Cost, 0m) }, writes);
    }

    [Fact]
    public void CostDifferingOnlyInTrailingZeros_GivesNoWrite()
    {
        Assert.Empty(Build(Original with { Cost = 12.5000m }, Unchanged with { Cost = 12.5m }));
    }

    // ---- Description sync ----

    [Fact]
    public void DescriptionDiffersFromFileName_SetsDescription()
    {
        var writes = Build(Original with { Description = "Old name" }, Unchanged);

        Assert.Equal(new[] { PropertyWrite.SetText(PartPropertyStorage.Description, "Bracket") }, writes);
    }

    [Fact]
    public void DescriptionBlank_SetsDescription()
    {
        var writes = Build(Original with { Description = null }, Unchanged);

        Assert.Equal(new[] { PropertyWrite.SetText(PartPropertyStorage.Description, "Bracket") }, writes);
    }

    [Fact]
    public void DescriptionMatchesFileName_GivesNoWrite()
    {
        var original = Original with { FullFileName = @"C:\Work\Bracket.v2.ipt", Description = "Bracket.v2" };

        Assert.Empty(Build(original, Unchanged));
    }

    [Fact]
    public void DescriptionDiffersOnlyInCase_SetsDescription()
    {
        var writes = Build(Original with { Description = "bracket" }, Unchanged);

        Assert.Equal(new[] { PropertyWrite.SetText(PartPropertyStorage.Description, "Bracket") }, writes);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void NeverSavedFile_NeverTouchesDescription(string fullFileName)
    {
        var original = Original with { FullFileName = fullFileName, Description = "Anything" };

        Assert.Empty(Build(original, Unchanged));
        Assert.Empty(Build(original with { Description = null }, Unchanged));
    }

    // ---- Fields that are never written ----

    [Fact]
    public void PartNumberMaterialFinishAndWeight_NeverProduceWrites()
    {
        var original = Original with { PartNumber = null, Material = null, Finish = null, WeightDisplay = null };

        Assert.Empty(Build(original, Unchanged));
    }

    // ---- Order ----

    [Fact]
    public void EverythingChanged_WritesInSpecFieldOrder()
    {
        var original = Original with { Description = "Old name" };
        var edited = new PartEditedValues("PM", "C. Person", "", 99m);

        var writes = Build(original, edited);

        Assert.Equal(
            new[]
            {
                PropertyWrite.SetText(PartPropertyStorage.Description, "Bracket"),
                PropertyWrite.SetText(PartPropertyStorage.PartType, "PM"),
                PropertyWrite.SetText(PartPropertyStorage.Designer, "C. Person"),
                PropertyWrite.Remove(PartPropertyStorage.Detailer),
                PropertyWrite.SetCurrency(PartPropertyStorage.Cost, 99m),
            },
            writes);
    }

    // ---- Edit blocks and arguments ----

    [Theory]
    [InlineData(EditBlock.ReadOnlyFile)]
    [InlineData(EditBlock.NotModifiable)]
    public void Build_WhenFileCannotBeEdited_Throws(EditBlock block)
    {
        Assert.Throws<ArgumentException>(() => Build(Original with { EditBlock = block }, Unchanged));
    }

    [Fact]
    public void Build_NullArguments_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => PartPropertiesWritePlan.Build(null!, Unchanged));
        Assert.Throws<ArgumentNullException>(() => PartPropertiesWritePlan.Build(Original, null!));
    }
}
