using InventorAddin.Core.Drawing;
using InventorAddin.Core.PartProperties;

namespace InventorAddin.Core.Tests.PartProperties;

public sealed class MassTextTests
{
    [Theory]
    [InlineData(4.331, "4.33 kg")]
    [InlineData(4.336, "4.34 kg")]
    [InlineData(0.0, "0.00 kg")]
    [InlineData(12.5, "12.50 kg")]
    public void Metric_IsKilogramsWithTwoDecimals(double kg, string expected) =>
        Assert.Equal(expected, MassText.For(kg, PrintUnits.Metric));

    [Theory]
    [InlineData(4.3318, "9.55 lb")]
    [InlineData(1.0, "2.20 lb")]
    [InlineData(0.0, "0.00 lb")]
    [InlineData(100.0, "220.46 lb")]
    public void Imperial_IsPoundsWithTwoDecimals(double kg, string expected) =>
        Assert.Equal(expected, MassText.For(kg, PrintUnits.Imperial));

    [Fact]
    public void UsesAPeriodWhateverTheCulture()
    {
        var saved = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("fr-FR");
            Assert.Equal("1234.50 kg", MassText.For(1234.5, PrintUnits.Metric));
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = saved;
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(-1.0)]
    public void UnusableMass_GivesNoText(double? kg)
    {
        Assert.Null(MassText.For(kg, PrintUnits.Metric));
        Assert.Null(MassText.WriteFor(kg, PrintUnits.Metric, "4.33 kg"));
    }

    [Fact]
    public void WriteFor_BlankStored_IsAFill()
    {
        var write = MassText.WriteFor(4.331, PrintUnits.Metric, null);

        Assert.NotNull(write);
        Assert.Equal(PartPropertyStorage.Mass, write!.Location);
        Assert.Equal("4.33 kg", write.TextValue);
        Assert.False(write.IsRefresh);
    }

    [Fact]
    public void WriteFor_SameText_WritesNothing() =>
        Assert.Null(MassText.WriteFor(4.331, PrintUnits.Metric, " 4.33 kg "));

    [Fact]
    public void WriteFor_DifferentText_IsARefresh()
    {
        var write = MassText.WriteFor(4.3318, PrintUnits.Imperial, "4.33 kg");

        Assert.Equal("9.55 lb", write!.TextValue);
        Assert.True(write.IsRefresh);
    }

    [Fact]
    public void MassIsACustomTextProperty()
    {
        Assert.True(PartPropertyStorage.Mass.IsCustom);
        Assert.Equal("Mass", PartPropertyStorage.Mass.PropertyName);
        Assert.Equal(PropertyValueKind.Text, PartPropertyStorage.Mass.Kind);
    }
}
