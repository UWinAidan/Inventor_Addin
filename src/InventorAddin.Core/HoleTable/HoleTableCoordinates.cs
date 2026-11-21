using System.Globalization;
using InventorAddin.Core.Holes;

namespace InventorAddin.Core.HoleTable;

/// <summary>A hole's XDIM and YDIM: the rounded values in the drawing's units, and the text the cells show.</summary>
public sealed record HoleTableCoordinate(double X, double Y, string XText, string YText);

/// <summary>Turns a hole's sheet position into the table's XDIM and YDIM (spec 04, "Origin").</summary>
public static class HoleTableCoordinates
{
    private const double CmPerInch = 2.54;

    /// <param name="hole">The hole, with its centre on the sheet.</param>
    /// <param name="origin">The view's ordinate origin on the sheet.</param>
    /// <param name="viewScale">Model-to-sheet factor (0.5 for 1:2).</param>
    /// <param name="drawingUnits">The units the table is shown in.</param>
    /// <param name="precisionTable">Chooses the decimals through <see cref="HoleTableDecimals"/>.</param>
    public static HoleTableCoordinate For(HoleTableHole hole, SheetPoint origin, double viewScale, HoleUnits drawingUnits, bool precisionTable)
    {
        ArgumentNullException.ThrowIfNull(hole);
        if (viewScale <= 0 || double.IsNaN(viewScale) || double.IsInfinity(viewScale))
        {
            throw new ArgumentOutOfRangeException(nameof(viewScale), "The view scale must be greater than zero.");
        }

        var decimals = HoleTableDecimals.For(precisionTable, drawingUnits);
        var x = Convert((hole.SheetXCm - origin.XCm) / viewScale, drawingUnits, decimals);
        var y = Convert((hole.SheetYCm - origin.YCm) / viewScale, drawingUnits, decimals);
        return new HoleTableCoordinate(x, y, Text(x, decimals), Text(y, decimals));
    }

    private static double Convert(double modelCm, HoleUnits units, int decimals)
    {
        var value = units == HoleUnits.Metric ? modelCm * 10.0 : modelCm / CmPerInch;
        // Strip floating-point noise first, so a true half (0.12499999999) still rounds away from zero.
        value = Math.Round(value, 9, MidpointRounding.AwayFromZero);
        var rounded = Math.Round(value, decimals, MidpointRounding.AwayFromZero);
        return rounded == 0 ? 0.0 : rounded; // never -0
    }

    private static string Text(double value, int decimals) =>
        (value == 0 ? 0.0 : value).ToString("F" + decimals, CultureInfo.InvariantCulture);
}
