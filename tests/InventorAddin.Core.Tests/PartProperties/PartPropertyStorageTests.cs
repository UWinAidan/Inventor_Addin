using InventorAddin.Core.PartProperties;

namespace InventorAddin.Core.Tests.PartProperties;

public sealed class PartPropertyStorageTests
{
    [Fact]
    public void PropertySetNames_MatchInventorInternalNames()
    {
        Assert.Equal("Inventor Summary Information", PropertySetNames.Summary);
        Assert.Equal("Inventor Document Summary Information", PropertySetNames.DocumentSummary);
        Assert.Equal("Design Tracking Properties", PropertySetNames.DesignTracking);
        Assert.Equal("Inventor User Defined Properties", PropertySetNames.UserDefined);
    }

    public static TheoryData<string, string, string, PropertyValueKind, bool> SpecTable() => new()
    {
        { nameof(PartPropertyStorage.PartNumber), PropertySetNames.DesignTracking, "Part Number", PropertyValueKind.Text, false },
        { nameof(PartPropertyStorage.Description), PropertySetNames.DesignTracking, "Description", PropertyValueKind.Text, false },
        { nameof(PartPropertyStorage.PartType), PropertySetNames.UserDefined, "Part Type", PropertyValueKind.Text, true },
        { nameof(PartPropertyStorage.Designer), PropertySetNames.DesignTracking, "Designer", PropertyValueKind.Text, false },
        { nameof(PartPropertyStorage.Detailer), PropertySetNames.UserDefined, "Detailer", PropertyValueKind.Text, true },
        { nameof(PartPropertyStorage.Cost), PropertySetNames.DesignTracking, "Cost", PropertyValueKind.Currency, false },
        { nameof(PartPropertyStorage.Finish), PropertySetNames.UserDefined, "Finish", PropertyValueKind.Text, true },
    };

    [Theory]
    [MemberData(nameof(SpecTable))]
    public void Storage_MatchesSpecTable(string field, string setName, string propertyName, PropertyValueKind kind, bool isCustom)
    {
        var location = (PropertyLocation)typeof(PartPropertyStorage).GetField(field)!.GetValue(null)!;

        Assert.Equal(new PropertyLocation(setName, propertyName, kind), location);
        Assert.Equal(isCustom, location.IsCustom);
    }

    [Theory]
    [InlineData(PropertySetNames.Summary)]
    [InlineData(PropertySetNames.DocumentSummary)]
    [InlineData(PropertySetNames.DesignTracking)]
    public void IsCustom_FalseForStandardSets(string setName)
    {
        Assert.False(new PropertyLocation(setName, "Anything", PropertyValueKind.Text).IsCustom);
    }

    [Fact]
    public void IsCustom_TrueForUserDefinedSet()
    {
        Assert.True(new PropertyLocation(PropertySetNames.UserDefined, "Anything", PropertyValueKind.Text).IsCustom);
    }

    // ---- PropertyWrite ----

    [Fact]
    public void SetText_CarriesTextOnly()
    {
        var write = PropertyWrite.SetText(PartPropertyStorage.Designer, "A. Person");

        Assert.Equal(PropertyWriteAction.Set, write.Action);
        Assert.Equal(PartPropertyStorage.Designer, write.Location);
        Assert.Equal("A. Person", write.TextValue);
        Assert.Null(write.CurrencyValue);
    }

    [Fact]
    public void SetCurrency_CarriesCurrencyOnly()
    {
        var write = PropertyWrite.SetCurrency(PartPropertyStorage.Cost, 12.5m);

        Assert.Equal(PropertyWriteAction.Set, write.Action);
        Assert.Equal(12.5m, write.CurrencyValue);
        Assert.Null(write.TextValue);
    }

    [Fact]
    public void Remove_CarriesNoValue()
    {
        var write = PropertyWrite.Remove(PartPropertyStorage.Detailer);

        Assert.Equal(PropertyWriteAction.Remove, write.Action);
        Assert.Null(write.TextValue);
        Assert.Null(write.CurrencyValue);
    }

    [Fact]
    public void SetText_OnCurrencyLocation_Throws()
    {
        Assert.Throws<ArgumentException>(() => PropertyWrite.SetText(PartPropertyStorage.Cost, "1"));
    }

    [Fact]
    public void SetCurrency_OnTextLocation_Throws()
    {
        Assert.Throws<ArgumentException>(() => PropertyWrite.SetCurrency(PartPropertyStorage.Designer, 1m));
    }

    [Fact]
    public void Remove_OnStandardLocation_Throws()
    {
        Assert.Throws<ArgumentException>(() => PropertyWrite.Remove(PartPropertyStorage.Designer));
    }

    [Fact]
    public void Writes_WithSameContent_AreEqual()
    {
        Assert.Equal(
            PropertyWrite.SetText(PartPropertyStorage.Detailer, "X"),
            PropertyWrite.SetText(PartPropertyStorage.Detailer, "X"));
        Assert.NotEqual(
            PropertyWrite.SetText(PartPropertyStorage.Detailer, "X"),
            PropertyWrite.Remove(PartPropertyStorage.Detailer));
    }

    [Fact]
    public void ToString_DescribesTheWrite()
    {
        Assert.Equal(
            "Set Design Tracking Properties/Designer = \"A. Person\"",
            PropertyWrite.SetText(PartPropertyStorage.Designer, "A. Person").ToString());
        Assert.Equal(
            "Set Design Tracking Properties/Cost = 12.5",
            PropertyWrite.SetCurrency(PartPropertyStorage.Cost, 12.5m).ToString());
        Assert.Equal(
            "Remove Inventor User Defined Properties/Detailer",
            PropertyWrite.Remove(PartPropertyStorage.Detailer).ToString());
    }
}
