using InventorAddin.Core.Materials;

namespace InventorAddin.Core.Tests.Materials;

public sealed class StockSizeEligibilityTests
{
    [Fact]
    public void ASolidPart_MayRun()
    {
        Assert.Null(StockSizeEligibility.DeclineReason(isSheetMetal: false, hasFlatPattern: false));
        Assert.Null(StockSizeEligibility.DeclineReason(isSheetMetal: false, hasFlatPattern: true));
    }

    [Fact]
    public void ASheetMetalPartWithAFlatPattern_IsDeclined_AndSaysItsSizeComesFromTheFlatPattern()
    {
        var reason = StockSizeEligibility.DeclineReason(isSheetMetal: true, hasFlatPattern: true);

        Assert.Equal(StockSizeEligibility.SheetMetal, reason);
        Assert.Contains("flat pattern", reason);
    }

    [Fact]
    public void ASheetMetalPartWithoutAFlatPattern_IsDeclined_AndSaysToCreateOne()
    {
        var reason = StockSizeEligibility.DeclineReason(isSheetMetal: true, hasFlatPattern: false);

        Assert.Equal(StockSizeEligibility.SheetMetalWithoutFlatPattern, reason);
        Assert.Contains("Create the flat pattern", reason);
    }
}
