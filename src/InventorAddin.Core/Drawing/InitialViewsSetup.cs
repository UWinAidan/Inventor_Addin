using InventorAddin.Core.Models;

namespace InventorAddin.Core.Drawing;

/// <summary>
/// The choices the add-in makes before it shows the Initial Views window (task 192): which model kind a document is, and which
/// sheet size the views are laid out on. Pure, so the add-in only reads Inventor and calls this.
/// </summary>
public static class InitialViewsSetup
{
    /// <summary>How close a sheet's size must be to a template sheet's to count as that sheet, in mm.</summary>
    public const double SheetMatchToleranceMm = 1.0;

    /// <summary>The model kind for a part or assembly document kind; null for any other kind (a drawing, a presentation, unknown).</summary>
    public static InitialViewModelKind? ModelKindFor(DocumentKind kind) => kind switch
    {
        DocumentKind.Part => InitialViewModelKind.Part,
        DocumentKind.SheetMetalPart => InitialViewModelKind.SheetMetalPart,
        DocumentKind.Assembly or DocumentKind.WeldmentAssembly => InitialViewModelKind.Assembly,
        _ => null,
    };

    /// <summary>The template's default sheet (A3 metric, ANSI B imperial): the sheet Create Print makes.</summary>
    public static SheetSizeSpec DefaultSheet(PrintUnits units)
    {
        PrintTemplateLayout layout = PrintTemplateLayout.For(units);
        return layout.SheetSizes.First(s => s.Name == layout.DefaultSheetName);
    }

    /// <summary>
    /// The sheet spec for a sheet that is <paramref name="widthCm"/> by <paramref name="heightCm"/>: the template sheet of that size
    /// (either units) when there is one, so its border and title block are known. Any other sheet gets a plain spec in
    /// <paramref name="fallbackUnits"/> with that template's border margin, so the usable area is the sheet less the border and the
    /// title block band (Default).
    /// </summary>
    public static SheetSizeSpec SheetFor(double widthCm, double heightCm, PrintUnits fallbackUnits)
    {
        double widthMm = widthCm * 10;
        double heightMm = heightCm * 10;

        foreach (SheetSizeSpec spec in PrintTemplateLayout.Metric.SheetSizes.Concat(PrintTemplateLayout.Imperial.SheetSizes))
        {
            if (Math.Abs(spec.WidthMm - widthMm) <= SheetMatchToleranceMm && Math.Abs(spec.HeightMm - heightMm) <= SheetMatchToleranceMm)
                return spec;
        }

        SheetSizeSpec margins = DefaultSheet(fallbackUnits);
        return new SheetSizeSpec("Sheet", fallbackUnits, widthMm, heightMm, 0, 0, margins.MarginMm);
    }
}
