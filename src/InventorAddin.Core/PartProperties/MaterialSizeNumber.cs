using System.Globalization;

namespace InventorAddin.Core.PartProperties;

/// <summary>
/// The number format of every Material Size text (task 230, shared by the stock size text of task 205): 2 decimals in mm or 3 in
/// inches, trailing zeros dropped, the invariant period, so "1.5", "65" and "0.5".
/// </summary>
public static class MaterialSizeNumber
{
    private const double MmPerCm = 10.0;
    private const double CmPerInch = 2.54;

    /// <summary>A length in Inventor's centimetres, shown in mm or inches.</summary>
    public static string FromCm(double cm, bool imperial) =>
        Round(imperial ? cm / CmPerInch : cm * MmPerCm, imperial);

    /// <summary>A length already in mm (<paramref name="imperial"/> false) or inches (true).</summary>
    public static string FromUnits(decimal value, bool imperial) => Round((double)value, imperial);

    private static string Round(double value, bool imperial) =>
        Math.Round(value, imperial ? 3 : 2, MidpointRounding.AwayFromZero)
            .ToString(imperial ? "0.###" : "0.##", CultureInfo.InvariantCulture);
}
