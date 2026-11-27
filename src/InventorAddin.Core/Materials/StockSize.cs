using InventorAddin.Core.Drawing;
using InventorAddin.Core.PartProperties;

namespace InventorAddin.Core.Materials;

/// <summary>The stock shape the Stock Size command works out (spec 05, "Added for M5", item 5).</summary>
public enum StockShape
{
    /// <summary>Plate: "PL 12 x 65 x 130", thickness, width, length.</summary>
    Plate,

    /// <summary>Round bar: "Ø30 x 85", diameter, length.</summary>
    Round,
}

/// <summary>
/// The stock size for a part. <see cref="Text"/> is the Material Size text, or null with <see cref="Problem"/> saying why not. The sizes
/// are in <see cref="Units"/> (mm for metric, inches for imperial): <see cref="Primary"/> is the thickness (plate) or diameter (round),
/// <see cref="Width"/> is the plate width (null for round) and <see cref="Length"/> the length.
/// </summary>
public sealed record StockSizeResult(
    StockShape Shape,
    PrintUnits Units,
    string? Text,
    string? Problem,
    decimal? Primary,
    decimal? Width,
    decimal? Length)
{
    public bool Succeeded => Text is not null;

    internal static StockSizeResult Fail(StockShape shape, PrintUnits units, string problem) =>
        new(shape, units, null, problem, null, null, null);
}

/// <summary>
/// The stock size rule (spec 05, "Added for M5", item 5; task 205). From the part's box, the three lengths in centimetres in any order
/// (Inventor's units; the add-in passes the oriented minimum range box's lengths), and a <see cref="StockWorkbook"/>:
/// <list type="bullet">
/// <item>Plate: thickness = smallest + 2 x allowance, up to the next listed thickness; width = middle + 2 x allowance and length = largest +
/// 2 x allowance, each up to the next step. Text "PL 12 x 65 x 130".</item>
/// <item>Round: diameter = the larger of the two smaller + 2 x allowance, up to the next listed diameter; length = largest + 2 x allowance,
/// up to the next step. Text "Ø30 x 85".</item>
/// </list>
/// The numbers use <see cref="MaterialSizeNumber"/>, the format of the sheet metal Material Size text (task 230), and carry no unit
/// suffix. A size larger than the biggest listed thickness or diameter is not guessed: the result has a problem and no text.
/// </summary>
public static class StockSize
{
    /// <summary>The prefix of a plate text.</summary>
    public const string PlatePrefix = "PL ";

    /// <summary>The prefix of a round bar text.</summary>
    public const string RoundPrefix = "Ø";

    private const decimal CmPerInch = 2.54m;
    private const decimal MmPerCm = 10m;

    /// <summary>A value this far over a listed size or step (mm or inches) is rounded to it, not up past it: 12.0000001 is 12.</summary>
    private const decimal Tolerance = 0.0001m;

    /// <summary>The stock size of a part whose box is <paramref name="boxCm"/> (three lengths in cm, any order).</summary>
    public static StockSizeResult For(StockShape shape, IReadOnlyList<double> boxCm, PrintUnits units, StockWorkbook workbook)
    {
        ArgumentNullException.ThrowIfNull(boxCm);
        ArgumentNullException.ThrowIfNull(workbook);

        if (boxCm.Count != 3 || boxCm.Any(v => !(v > 0) || double.IsInfinity(v)))
        {
            return StockSizeResult.Fail(shape, units, "The part's size could not be read, so no stock size was worked out. Type the size.");
        }

        var imperial = units == PrintUnits.Imperial;
        var sizes = boxCm
            .Select(cm => imperial ? (decimal)cm / CmPerInch : (decimal)cm * MmPerCm)
            .Order()
            .ToArray();
        var data = workbook.For(units);
        var allowance2 = 2 * data.AllowancePerSide;

        var length = RoundUp(sizes[2] + allowance2, data.Step);
        if (shape == StockShape.Round)
        {
            var diameter = sizes[1] + allowance2;
            if (!TryListed(data.RoundDiameters, diameter, out var listed))
            {
                return StockSizeResult.Fail(shape, units, TooBig("round bar diameter", diameter, data.RoundDiameters, imperial, "Round"));
            }

            return new StockSizeResult(
                shape, units, $"{RoundPrefix}{Text(listed, imperial)} x {Text(length, imperial)}", null, listed, null, length);
        }

        var thickness = sizes[0] + allowance2;
        if (!TryListed(data.PlateThicknesses, thickness, out var listedThickness))
        {
            return StockSizeResult.Fail(shape, units, TooBig("plate thickness", thickness, data.PlateThicknesses, imperial, "Plate"));
        }

        var width = RoundUp(sizes[1] + allowance2, data.Step);
        return new StockSizeResult(
            shape,
            units,
            $"{PlatePrefix}{Text(listedThickness, imperial)} x {Text(width, imperial)} x {Text(length, imperial)}",
            null,
            listedThickness,
            width,
            length);
    }

    /// <summary>A plate. See <see cref="For"/>.</summary>
    public static StockSizeResult Plate(IReadOnlyList<double> boxCm, PrintUnits units, StockWorkbook workbook) =>
        For(StockShape.Plate, boxCm, units, workbook);

    /// <summary>A round bar. See <see cref="For"/>.</summary>
    public static StockSizeResult Round(IReadOnlyList<double> boxCm, PrintUnits units, StockWorkbook workbook) =>
        For(StockShape.Round, boxCm, units, workbook);

    /// <summary>The smallest listed size that is at least <paramref name="needed"/>; false when the list is empty or all are smaller.</summary>
    private static bool TryListed(IReadOnlyList<decimal> list, decimal needed, out decimal listed)
    {
        foreach (var size in list.Order())
        {
            if (size + Tolerance >= needed)
            {
                listed = size;
                return true;
            }
        }

        listed = 0;
        return false;
    }

    private static decimal RoundUp(decimal value, decimal step) =>
        Math.Max(1, Math.Ceiling((value - Tolerance) / step)) * step;

    private static string Text(decimal value, bool imperial) => MaterialSizeNumber.FromUnits(value, imperial);

    private static string TooBig(string what, decimal needed, IReadOnlyList<decimal> list, bool imperial, string tab)
    {
        var unit = imperial ? "in" : "mm";
        var need = $"{Text(needed, imperial)} {unit}";
        return list.Count == 0
            ? $"The stock workbook lists no {what} sizes for {(imperial ? "imperial" : "metric")} parts on its {tab} tab, so none was chosen for {need}. Add some, or type the size."
            : $"The {what} needed, {need} with the allowance, is larger than the biggest listed, {Text(list.Max(), imperial)} {unit}. " +
              $"Add a larger one to the {tab} tab of the stock workbook, or type the size.";
    }
}
