namespace InventorAddin.Core;

/// <summary>
/// The add-in's brand: every name a user or the file system sees. Nothing else in <c>src/</c> repeats these strings;
/// ribbon and command ids are built from <see cref="IdPrefix"/>. The <c>.addin</c> manifest is static XML and carries
/// its own copy of <see cref="ProductName"/>, so change it there too.
/// Changing <see cref="IdPrefix"/> makes Inventor treat the tab, panels and commands as new.
/// </summary>
public static class Branding
{
    /// <summary>Text on the ribbon tab.</summary>
    public const string TabName = "AWB Addin";

    /// <summary>Product name: About title, window titles, log lines, error text.</summary>
    public const string ProductName = "AWB Addin";

    /// <summary>Prefix of the internal ids of the tab, panels and commands.</summary>
    public const string IdPrefix = "Awb";

    /// <summary>Folder name under %APPDATA% for settings and log files, and under the temp folder for exports.</summary>
    public const string DataFolderName = "AwbAddin";

    /// <summary>Log file name without its extension; rolled-over files are <c>{LogFileBaseName}.N.log</c>.</summary>
    public const string LogFileBaseName = "awbaddin";

    /// <summary>Name of the current log file.</summary>
    public const string LogFileName = LogFileBaseName + ".log";
}
