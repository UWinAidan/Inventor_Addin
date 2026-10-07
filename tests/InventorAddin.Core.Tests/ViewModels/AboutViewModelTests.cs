using System.Globalization;
using InventorAddin.Core.ViewModels;

namespace InventorAddin.Core.Tests.ViewModels;

public sealed class AboutViewModelTests
{
    private const string LogFolder = @"C:\Users\someone\AppData\Roaming\" + Branding.DataFolderName;

    // Fixed zones so the tests do not depend on the machine's local zone.
    private static readonly TimeZoneInfo PlusTen =
        TimeZoneInfo.CreateCustomTimeZone("Test+10", TimeSpan.FromHours(10), "Test+10", "Test+10");

    private static readonly TimeZoneInfo MinusFiveThirty =
        TimeZoneInfo.CreateCustomTimeZone("Test-0530", TimeSpan.FromHours(-5.5), "Test-0530", "Test-0530");

    private static AboutViewModel Create(
        string? version = "0.1.0",
        string? buildDate = "2026-10-06T15:48:48Z",
        int? inventorMajorVersion = 30,
        string? logFolder = LogFolder,
        TimeZoneInfo? timeZone = null) =>
        new(version, buildDate, inventorMajorVersion, logFolder, timeZone ?? TimeZoneInfo.Utc);

    [Fact]
    public void Title_IsAddinName()
    {
        Assert.Equal(Branding.ProductName, Create().Title);
    }

    [Fact]
    public void NormalValues_FillEveryValue()
    {
        var vm = Create();

        Assert.Equal("0.1.0", vm.Version);
        Assert.Equal("2026-10-06 15:48", vm.BuildDate);
        Assert.Equal("30", vm.InventorVersion);
        Assert.Equal("2026", vm.InventorRelease);
        Assert.Equal(LogFolder, vm.LogFolder);
    }

    [Fact]
    public void Version_WithCommitSuffix_ShownAsIs()
    {
        var vm = Create(version: "0.1.0+62a79bf41cf20c42c0cc4c8d88ee762837230799");

        Assert.Equal("0.1.0+62a79bf41cf20c42c0cc4c8d88ee762837230799", vm.Version);
    }

    [Theory]
    [InlineData("2026-10-06T15:48:48Z", "2026-10-07 01:48")]
    [InlineData("2026-10-06T23:59:00Z", "2026-10-07 09:59")]
    [InlineData("2026-10-06T15:48:48.1234567Z", "2026-10-07 01:48")]
    [InlineData("2026-10-06T17:48:48+02:00", "2026-10-07 01:48")]
    public void BuildDate_ConvertedToGivenZone(string buildDate, string expected)
    {
        Assert.Equal(expected, Create(buildDate: buildDate, timeZone: PlusTen).BuildDate);
    }

    [Fact]
    public void BuildDate_NegativeHalfHourZone_CrossesDayBackwards()
    {
        Assert.Equal("2026-10-05 22:30", Create(buildDate: "2026-10-06T04:00:00Z", timeZone: MinusFiveThirty).BuildDate);
    }

    [Fact]
    public void BuildDate_WithoutOffset_TakenAsUtc()
    {
        Assert.Equal("2026-10-07 01:48", Create(buildDate: "2026-10-06T15:48:48", timeZone: PlusTen).BuildDate);
    }

    [Fact]
    public void BuildDate_DefaultsToLocalZone()
    {
        var vm = new AboutViewModel("0.1.0", "2026-10-06T15:48:48Z", 30, LogFolder);

        var expected = new DateTimeOffset(2026, 10, 6, 15, 48, 48, TimeSpan.Zero)
            .ToLocalTime()
            .ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        Assert.Equal(expected, vm.BuildDate);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BuildDate_Missing_IsUnknown(string? buildDate)
    {
        var vm = Create(buildDate: buildDate);

        Assert.Equal("unknown", vm.BuildDate);
    }

    [Theory]
    [InlineData("not a date")]
    [InlineData("2026-13-45T00:00:00Z")]
    [InlineData("2026-02-30T10:00:00Z")]
    [InlineData("$([System.DateTime]::UtcNow)")]
    [InlineData("BuildDate")]
    public void BuildDate_Garbage_IsUnknown(string buildDate)
    {
        var vm = Create(buildDate: buildDate);

        Assert.Equal("unknown", vm.BuildDate);
    }

    [Fact]
    public void BuildDate_FormatDoesNotDependOnCurrentCulture()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            // A culture with a different date separator and calendar digits.
            CultureInfo.CurrentCulture = new CultureInfo("ar-SA");
            Assert.Equal("2026-10-06 15:48", Create().BuildDate);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Version_Missing_IsUnknown(string? version)
    {
        Assert.Equal("unknown", Create(version: version).Version);
    }

    [Fact]
    public void InventorVersion_Missing_IsUnknown()
    {
        Assert.Equal("unknown", Create(inventorMajorVersion: null).InventorVersion);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void LogFolder_Missing_IsUnknown(string? logFolder)
    {
        Assert.Equal("unknown", Create(logFolder: logFolder).LogFolder);
    }

    [Fact]
    public void HeaderSubline_IsVersion()
    {
        Assert.Equal("Version 0.1.0", Create().HeaderSubline);
        Assert.Equal(
            "Version 0.1.0+62a79bf41cf20c42c0cc4c8d88ee762837230799",
            Create(version: "0.1.0+62a79bf41cf20c42c0cc4c8d88ee762837230799").HeaderSubline);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void HeaderSubline_VersionMissing_IsVersionUnknown(string? version)
    {
        Assert.Equal("Version unknown", Create(version: version).HeaderSubline);
    }

    [Theory]
    [InlineData(30, "2026")]
    [InlineData(29, "2025")]
    [InlineData(13, "2009")]
    [InlineData(12, "unknown")]
    [InlineData(0, "unknown")]
    [InlineData(-1, "unknown")]
    [InlineData(int.MinValue, "unknown")]
    [InlineData(int.MaxValue, "2147485643")]
    [InlineData(null, "unknown")]
    public void InventorRelease_IsYearFromMajorVersion(int? major, string expected)
    {
        Assert.Equal(expected, Create(inventorMajorVersion: major).InventorRelease);
    }

    [Fact]
    public void BuiltAndLogFolderRows_AreBuildDateAndLogFolder()
    {
        var vm = Create();

        Assert.Equal("2026-10-06 15:48", vm.BuildDate);
        Assert.Equal(LogFolder, vm.LogFolder);
    }
}
