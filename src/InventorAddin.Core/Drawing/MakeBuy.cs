using InventorAddin.Core.Numbering;

namespace InventorAddin.Core.Drawing;

/// <summary>What a parts list row's Make/Buy cell says.</summary>
public enum MakeBuyKind
{
    /// <summary>Neither rule applies; the cell is left blank (Default, spec 05 question 20).</summary>
    Blank,

    /// <summary>An AWB-numbered part that is not a library part. "AWB-numbered" means whatever <see cref="PartNumber.TryParse"/> accepts.</summary>
    Make,

    /// <summary>A Content Center part or a file in a library folder.</summary>
    Buy,
}

/// <summary>
/// The Make/Buy rule for a parts list row (spec 05, "Added for M5", item 6, question 20). Library wins: a Content Center
/// part or a file in a library folder is Buy even when its number is AWB-numbered. Otherwise an AWB-numbered part (the
/// <see cref="PartNumber"/> parser decides) is Make, and anything else is blank.
/// </summary>
public static class MakeBuy
{
    /// <summary>The text written for <see cref="MakeBuyKind.Make"/>.</summary>
    public const string MakeText = "MAKE";

    /// <summary>The text written for <see cref="MakeBuyKind.Buy"/>.</summary>
    public const string BuyText = "BUY";

    /// <summary>Decides the kind for one row.</summary>
    /// <param name="partNumber">The row's part number; null or blank is allowed.</param>
    /// <param name="isContentCenter">True when the row's component is a Content Center member.</param>
    /// <param name="isInLibraryFolder">True when the row's file is in a library folder, see <see cref="LibraryFolderRule.IsUnder"/>.</param>
    public static MakeBuyKind Decide(string? partNumber, bool isContentCenter, bool isInLibraryFolder)
    {
        if (isContentCenter || isInLibraryFolder)
        {
            return MakeBuyKind.Buy;
        }

        return PartNumber.IsWellFormed(partNumber) ? MakeBuyKind.Make : MakeBuyKind.Blank;
    }

    /// <summary>The cell text for <paramref name="kind"/>: "MAKE", "BUY" or an empty string.</summary>
    public static string CellText(MakeBuyKind kind) => kind switch
    {
        MakeBuyKind.Make => MakeText,
        MakeBuyKind.Buy => BuyText,
        _ => string.Empty,
    };

    /// <summary>Decides and returns the cell text in one call, for the add-in's per-row loop.</summary>
    public static string CellText(string? partNumber, bool isContentCenter, bool isInLibraryFolder) =>
        CellText(Decide(partNumber, isContentCenter, isInLibraryFolder));
}
