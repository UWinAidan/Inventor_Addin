using InventorAddin.Core.Models;
using InventorAddin.Core.Numbering;
using InventorAddin.Core.PartProperties;

namespace InventorAddin.Core.Tests.Numbering;

public sealed class CreationPropertiesTests
{
    private static readonly PartNumber Number = PartNumber.Parse("AWB-10042");

    private static (PropertyLocation Location, string? Text)[] Summary(IReadOnlyList<PropertyWrite> writes) =>
        writes.Select(w => (w.Location, w.TextValue)).ToArray();

    [Fact]
    public void Build_EveryPropertyPresent_InOrder()
    {
        var writes = CreationProperties.Build(Number, "mounting_bracket", "M", "Test Designer");

        Assert.Equal(
            new (PropertyLocation, string?)[]
            {
                (PartPropertyStorage.PartNumber, "AWB-10042"),
                (PartPropertyStorage.Description, "MOUNTING BRACKET"),
                (PartPropertyStorage.Designer, "Test Designer"),
                (PartPropertyStorage.Author, "Test Designer"),
                (PartPropertyStorage.Detailer, "Test Designer"),
                (PartPropertyStorage.PartType, "M"),
            },
            Summary(writes));
        Assert.All(writes, w => Assert.Equal(PropertyWriteAction.Set, w.Action));
    }

    [Fact]
    public void Build_NormalisesNameAndDesigner()
    {
        var writes = CreationProperties.Build(Number, "  Mounting   Bracket ", "M", "  Test Designer  ");

        Assert.Equal("MOUNTING BRACKET", writes.Single(w => w.Location == PartPropertyStorage.Description).TextValue);
        Assert.Equal("Test Designer", writes.Single(w => w.Location == PartPropertyStorage.Designer).TextValue);
        Assert.Equal("Test Designer", writes.Single(w => w.Location == PartPropertyStorage.Author).TextValue);
        Assert.Equal("Test Designer", writes.Single(w => w.Location == PartPropertyStorage.Detailer).TextValue);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Build_BlankDesigner_LeavesOutDesignerAuthorAndDetailer(string? designer)
    {
        var writes = CreationProperties.Build(Number, "plate", "M", designer);

        Assert.Equal(
            new[] { PartPropertyStorage.PartNumber, PartPropertyStorage.Description, PartPropertyStorage.PartType },
            writes.Select(w => w.Location).ToArray());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Build_BlankPartType_LeavesOutPartType(string? partType)
    {
        var writes = CreationProperties.Build(Number, "plate", partType, "Test Designer");

        Assert.DoesNotContain(writes, w => w.Location == PartPropertyStorage.PartType);
        Assert.Equal(5, writes.Count);
    }

    [Theory]
    [InlineData("pm", "PM")]
    [InlineData(" a ", "A")]
    public void Build_LowerCasePartType_IsWrittenInTheListsSpelling(string typed, string expected)
    {
        var writes = CreationProperties.Build(Number, "plate", typed, null);

        Assert.Equal(expected, writes.Single(w => w.Location == PartPropertyStorage.PartType).TextValue);
    }

    [Fact]
    public void Build_PartTypeNotInTheList_IsWrittenTrimmed()
    {
        var writes = CreationProperties.Build(Number, "plate", " XYZ ", null);

        Assert.Equal("XYZ", writes.Single(w => w.Location == PartPropertyStorage.PartType).TextValue);
    }

    [Fact]
    public void Build_KeepsACommaInTheName()
    {
        var writes = CreationProperties.Build(Number, "plate, 10mm", null, null);

        Assert.Equal("PLATE, 10MM", writes.Single(w => w.Location == PartPropertyStorage.Description).TextValue);
    }

    [Fact]
    public void Build_DescriptionOfAnAssembly_IsItsPrefixedName_InCapitals()
    {
        var name = PartFileName.ForKind("gearbox été", DocumentKind.Assembly);

        var writes = CreationProperties.Build(Number, name, "A", null);

        Assert.Equal("ASSY GEARBOX ÉTÉ", writes.Single(w => w.Location == PartPropertyStorage.Description).TextValue);
    }

    [Theory]
    [InlineData("")]
    [InlineData("a/b")]
    [InlineData("CON")]
    public void Build_InvalidName_Throws(string name)
    {
        Assert.Throws<ArgumentException>(() => CreationProperties.Build(Number, name, null, null));
    }

    // NewFileKind

    [Theory]
    [InlineData(NewFileKind.Part, "Part", ".ipt", DocumentKind.Part)]
    [InlineData(NewFileKind.SheetMetalPart, "Sheet metal part", ".ipt", DocumentKind.SheetMetalPart)]
    [InlineData(NewFileKind.Assembly, "Assembly", ".iam", DocumentKind.Assembly)]
    public void NewFileKind_HasDisplayNameExtensionAndDocumentKind(
        NewFileKind kind, string displayName, string extension, DocumentKind documentKind)
    {
        Assert.Equal(displayName, kind.DisplayName());
        Assert.Equal(extension, kind.Extension());
        Assert.Equal(documentKind, kind.DocumentKindOf());
    }

    [Fact]
    public void NewFileKinds_All_ListsEveryKindInOrder()
    {
        Assert.Equal(
            new[] { NewFileKind.Part, NewFileKind.SheetMetalPart, NewFileKind.Assembly },
            NewFileKinds.All);
    }

    [Fact]
    public void NewFileKind_UndefinedValue_Throws()
    {
        var undefined = (NewFileKind)99;

        Assert.Throws<ArgumentOutOfRangeException>(() => undefined.Extension());
        Assert.Throws<ArgumentOutOfRangeException>(() => undefined.DocumentKindOf());
    }
}
