using InventorAddin.Core.Drawing;
using Xunit;

namespace InventorAddin.Core.Tests.Drawing;

public class LibraryFolderRuleTests
{
    private static readonly string[] Libraries = { @"C:\Libs\Vendor", @"D:\Shared\Parts\" };

    [Theory]
    [InlineData(@"C:\Libs\Vendor\a.ipt", true)]
    [InlineData(@"c:\libs\vendor\Sub\Deeper\a.ipt", true)]
    [InlineData("C:/Libs/Vendor/a.ipt", true)]
    [InlineData(@"D:\Shared\Parts\a.ipt", true)]
    [InlineData(@"C:\Libs\VendorTwo\a.ipt", false)]
    [InlineData(@"C:\Libs\Vendor", false)]
    [InlineData(@"C:\Libs\a.ipt", false)]
    [InlineData(@"E:\Work\a.ipt", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsUnder_MatchesWholeFolders(string? file, bool expected) =>
        Assert.Equal(expected, LibraryFolderRule.IsUnder(file, Libraries));

    [Fact]
    public void IsUnder_EmptyNullOrBlankList_IsFalse()
    {
        Assert.False(LibraryFolderRule.IsUnder(@"C:\Libs\a.ipt", Array.Empty<string>()));
        Assert.False(LibraryFolderRule.IsUnder(@"C:\Libs\a.ipt", null));
        Assert.False(LibraryFolderRule.IsUnder(@"C:\Libs\a.ipt", new[] { "", "  " }));
    }

    [Theory]
    [InlineData("/")]
    [InlineData(@"\")]
    public void IsUnder_BareSlashEntry_IsSkipped(string folder) =>
        Assert.False(LibraryFolderRule.IsUnder(@"\srv\a.ipt", new[] { folder }));

    [Theory]
    [InlineData(@"C:\Proj\a.ipj", @"Libs\Std", @"C:\Proj\Libs\Std")]
    [InlineData(@"C:\Proj\a.ipj", @"..\Shared\Lib", @"C:\Shared\Lib")]
    [InlineData(@"C:\Proj\a.ipj", @".\Lib", @"C:\Proj\Lib")]
    [InlineData(@"C:\Proj\a.ipj", @"D:\Lib", @"D:\Lib")]
    [InlineData(@"C:\Proj\a.ipj", @"\srv\share\Lib", @"\srv\share\Lib")]
    [InlineData(@"C:\Proj\a.ipj", "Libs/Std", @"C:\Proj\Libs\Std")]
    [InlineData(@"C:\Proj\a.ipj", "", "")]
    public void ResolveProjectFolder_TakesRelativePathsFromTheProjectFolder(string project, string folder, string expected) =>
        Assert.Equal(expected, LibraryFolderRule.ResolveProjectFolder(project, folder));
}
