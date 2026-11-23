using InventorAddin.Core.Drawing;
using InventorAddin.Core.PartProperties;

namespace InventorAddin.Core.Tests.PartProperties;

public sealed class SheetMetalSizeTextTests
{
    [Fact]
    public void Metric_ThicknessThenSmallerThenLargerSide() =>
        Assert.Equal("1.5 x 60 x 120 mm", SheetMetalSizeText.For(0.15, 12.0, 6.0, PrintUnits.Metric));

    [Fact]
    public void Metric_LengthAlreadySmaller_SameText() =>
        Assert.Equal("1.5 x 60 x 120 mm", SheetMetalSizeText.For(0.15, 6.0, 12.0, PrintUnits.Metric));

    [Fact]
    public void Metric_RoundsToTwoDecimalsAndDropsZeros() =>
        Assert.Equal("2 x 45.37 x 100.5 mm", SheetMetalSizeText.For(0.2, 4.5372, 10.05, PrintUnits.Metric));

    [Fact]
    public void Imperial_DropsTrailingZeros() =>
        Assert.Equal("0.06 x 2 x 4 in", SheetMetalSizeText.For(0.1524, 5.08, 10.16, PrintUnits.Imperial));

    [Fact]
    public void Imperial_RoundsToThreeDecimals() =>
        Assert.Equal("0.063 x 2.362 x 4.724 in", SheetMetalSizeText.For(0.16, 12.0, 6.0, PrintUnits.Imperial));

    [Fact]
    public void UsesAPeriodWhateverTheCulture()
    {
        var saved = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("fr-FR");
            Assert.Equal("1.5 x 60 x 120 mm", SheetMetalSizeText.For(0.15, 6.0, 12.0, PrintUnits.Metric));
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = saved;
        }
    }

    [Theory]
    [InlineData(null, 6.0, 12.0)]
    [InlineData(0.15, null, 12.0)]
    [InlineData(0.15, 6.0, null)]
    [InlineData(0.0, 6.0, 12.0)]
    [InlineData(0.15, -6.0, 12.0)]
    [InlineData(0.15, double.NaN, 12.0)]
    [InlineData(0.15, 6.0, double.PositiveInfinity)]
    public void UnusableSize_GivesNull(double? t, double? l, double? w)
    {
        Assert.Null(SheetMetalSizeText.For(t, l, w, PrintUnits.Metric));
        Assert.Null(SheetMetalSizeText.WriteFor(t, l, w, PrintUnits.Metric, "old"));
    }

    [Fact]
    public void Write_BlankStored_IsAFill()
    {
        var write = SheetMetalSizeText.WriteFor(0.15, 6.0, 12.0, PrintUnits.Metric, "  ");
        Assert.NotNull(write);
        Assert.Equal(PartPropertyStorage.MaterialSize, write!.Location);
        Assert.False(write.IsRefresh);
    }

    [Fact]
    public void Write_DifferentStored_IsARefresh()
    {
        var write = SheetMetalSizeText.WriteFor(0.15, 6.0, 12.0, PrintUnits.Metric, "1 x 50 x 100 mm");
        Assert.NotNull(write);
        Assert.True(write!.IsRefresh);
    }

    [Fact]
    public void Write_SameStored_IsNull() =>
        Assert.Null(SheetMetalSizeText.WriteFor(0.15, 6.0, 12.0, PrintUnits.Metric, " 1.5 x 60 x 120 mm "));

    [Fact]
    public void MaterialSize_IsACustomTextProperty()
    {
        Assert.True(PartPropertyStorage.MaterialSize.IsCustom);
        Assert.Equal("Material Size", PartPropertyStorage.MaterialSize.PropertyName);
        Assert.Equal(PropertyValueKind.Text, PartPropertyStorage.MaterialSize.Kind);
    }
}
