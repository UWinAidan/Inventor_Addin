using System.Globalization;

namespace InventorAddin.Core.ViewModels;

/// <summary>
/// View-model for the About window: which build of the add-in Inventor loaded, and where its log is.
/// Read-only; every value is fixed when it is constructed.
/// </summary>
public sealed class AboutViewModel : ObservableObject
{
    public const string TitleText = Branding.ProductName;
    public const string Unknown = "unknown";
    public const string BuildDateFormat = "yyyy-MM-dd HH:mm";

    /// <summary>Inventor's major version plus this gives its release year (30 is Inventor 2026).</summary>
    public const int ReleaseYearOffset = 1996;

    /// <summary>The first major version the year mapping holds for: 13, Inventor 2009.</summary>
    public const int FirstYearNamedMajorVersion = 13;

    /// <param name="version">The add-in's informational version, shown as is (it may carry a <c>+commit</c> suffix).</param>
    /// <param name="buildDate">The UTC build time in ISO 8601, or null. An unparseable value shows as "unknown".</param>
    /// <param name="inventorMajorVersion">Inventor's major version (30 for Inventor 2026), or null when it could not be read.</param>
    /// <param name="logFolder">The folder the log is written to, or null when no log file is being written.</param>
    /// <param name="timeZone">The zone to show the build time in. Defaults to the local zone; tests pass a fixed one.</param>
    public AboutViewModel(
        string? version,
        string? buildDate,
        int? inventorMajorVersion,
        string? logFolder,
        TimeZoneInfo? timeZone = null)
    {
        Version = OrUnknown(version);
        BuildDate = FormatBuildDate(buildDate, timeZone ?? TimeZoneInfo.Local);
        InventorVersion = inventorMajorVersion?.ToString(CultureInfo.InvariantCulture) ?? Unknown;
        InventorRelease = FormatRelease(inventorMajorVersion);
        LogFolder = OrUnknown(logFolder);
    }

    public string Title => TitleText;

    public string Version { get; }

    /// <summary>The build time in <see cref="BuildDateFormat"/>, in the requested zone, or "unknown".</summary>
    public string BuildDate { get; }

    public string InventorVersion { get; }

    /// <summary>
    /// Inventor's release year (2026 for major version 30), or "unknown" when the major version could not
    /// be read or is older than Inventor 2009. Shown in the row labelled "Inventor version".
    /// </summary>
    public string InventorRelease { get; }

    /// <summary>The line under the header title.</summary>
    public string HeaderSubline => $"Version {Version}";

    public string LogFolder { get; }

    private static string OrUnknown(string? value) =>
        string.IsNullOrWhiteSpace(value) ? Unknown : value.Trim();

    private static string FormatRelease(int? majorVersion) =>
        majorVersion is int major && major >= FirstYearNamedMajorVersion
            ? ((long)major + ReleaseYearOffset).ToString(CultureInfo.InvariantCulture)
            : Unknown;

    private static string FormatBuildDate(string? buildDate, TimeZoneInfo timeZone)
    {
        if (string.IsNullOrWhiteSpace(buildDate))
            return Unknown;

        // A value without an offset is taken as UTC, which is what the build writes.
        if (!DateTimeOffset.TryParse(
                buildDate.Trim(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal,
                out DateTimeOffset parsed))
            return Unknown;

        return TimeZoneInfo.ConvertTime(parsed, timeZone).ToString(BuildDateFormat, CultureInfo.InvariantCulture);
    }
}
