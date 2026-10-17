using InventorAddin.Core.Holes;
using InventorAddin.Core.Materials;

namespace InventorAddin.Core.Tests.Holes;

public sealed class HoleWorkbookMessagesTests
{
    private static readonly LibraryPath Starter = new(@"C:\addin\libraries\AWB_Holes.xlsx", IsStarter: true);
    private static readonly LibraryPath FromSettings = new(@"D:\shared\Test holes.xlsx", IsStarter: false);

    [Fact]
    public void Missing_Starter_NamesPathAndStarter()
    {
        Assert.Equal(
            @"The starter hole workbook was not found at C:\addin\libraries\AWB_Holes.xlsx. Reinstall the add-in, or set the hole data in Settings.",
            HoleWorkbookMessages.Missing(Starter));
    }

    [Fact]
    public void Missing_Settings_NamesPathAndSettings()
    {
        Assert.Equal(
            @"The hole workbook set in Settings was not found at D:\shared\Test holes.xlsx. Check the hole data path in Settings.",
            HoleWorkbookMessages.Missing(FromSettings));
    }

    [Fact]
    public void Unreadable_Settings_WithReason()
    {
        Assert.Equal(
            @"Could not read the hole workbook set in Settings at D:\shared\Test holes.xlsx (The file is in use). Close it in Excel, check that it is an .xlsx file, or check the hole data path in Settings.",
            HoleWorkbookMessages.Unreadable(FromSettings, " The file is in use. "));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public void Unreadable_Starter_NoReason(string? reason)
    {
        Assert.Equal(
            @"Could not read the starter hole workbook at C:\addin\libraries\AWB_Holes.xlsx. Reinstall the add-in, or set the hole data in Settings.",
            HoleWorkbookMessages.Unreadable(Starter, reason));
    }

    [Fact]
    public void Messages_RejectNull()
    {
        Assert.Throws<ArgumentNullException>(() => HoleWorkbookMessages.Missing(null!));
        Assert.Throws<ArgumentNullException>(() => HoleWorkbookMessages.Unreadable(null!, "x"));
    }
}
