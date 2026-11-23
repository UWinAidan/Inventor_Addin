using InventorAddin.Core.Drawing;
using InventorAddin.Core.Models;

namespace InventorAddin.Core.Tests.Drawing;

public sealed class InitialViewsSetupTests
{
    [Theory]
    [InlineData(DocumentKind.Part, InitialViewModelKind.Part)]
    [InlineData(DocumentKind.SheetMetalPart, InitialViewModelKind.SheetMetalPart)]
    [InlineData(DocumentKind.Assembly, InitialViewModelKind.Assembly)]
    [InlineData(DocumentKind.WeldmentAssembly, InitialViewModelKind.Assembly)]
    public void ModelKindFor_MapsPartsAndAssemblies(DocumentKind kind, InitialViewModelKind expected) =>
        Assert.Equal(expected, InitialViewsSetup.ModelKindFor(kind));

    [Theory]
    [InlineData(DocumentKind.Drawing)]
    [InlineData(DocumentKind.Presentation)]
    [InlineData(DocumentKind.Unknown)]
    public void ModelKindFor_IsNullForOtherDocuments(DocumentKind kind) =>
        Assert.Null(InitialViewsSetup.ModelKindFor(kind));

    [Fact]
    public void DefaultSheet_IsA3Metric_AndAnsiBImperial()
    {
        Assert.Equal("A3", InitialViewsSetup.DefaultSheet(PrintUnits.Metric).Name);
        Assert.Equal("B", InitialViewsSetup.DefaultSheet(PrintUnits.Imperial).Name);
    }

    [Fact]
    public void SheetFor_MatchesATemplateSheetBySize()
    {
        Assert.Equal("A3", InitialViewsSetup.SheetFor(42.0, 29.7, PrintUnits.Imperial).Name);
        Assert.Equal(PrintUnits.Imperial, InitialViewsSetup.SheetFor(43.18, 27.94, PrintUnits.Metric).Units);
    }

    [Fact]
    public void SheetFor_AnUnknownSize_GetsAPlainSpecWithTheFallbackMargin()
    {
        SheetSizeSpec spec = InitialViewsSetup.SheetFor(30, 20, PrintUnits.Metric);
        Assert.Equal(300, spec.WidthMm, 9);
        Assert.Equal(200, spec.HeightMm, 9);
        Assert.Equal(10, spec.MarginMm, 9);
        SheetAreaCm area = InitialViewsPlan.UsableArea(spec);
        Assert.Equal(1.0, area.LeftCm, 9);
        Assert.Equal(29.0, area.RightCm, 9);
        Assert.Equal(1.0 + 5.5, area.BottomCm, 9);
    }
}
