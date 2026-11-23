namespace InventorAddin.Core.Drawing;

/// <summary>
/// Which sheet the Initial Views window starts on, and which larger one it offers when the views do not fit (task 232). Pure: the plan
/// for a sheet is passed in.
/// </summary>
public static class InitialViewsSheetRule
{
    /// <summary>
    /// With the suggested scale, the smallest sheet is the first at which the views get a scale of at least 1:2 (Default: below that the
    /// views are too small to read, so the next sheet is better). A scale the user chose must fit as it is.
    /// </summary>
    public const double MinimumSuggestedScale = 0.5;

    /// <summary>The sheets of a units system a print can use: those whose border holds the title block, smallest first.</summary>
    public static IReadOnlyList<SheetSizeSpec> SheetsFor(PrintUnits units) =>
        PrintTemplateLayout.For(units).SheetSizes.Where(s => s.TitleBlockFits).OrderBy(s => s.WidthMm * s.HeightMm).ToList();

    /// <summary>
    /// The smallest of <paramref name="sheets"/> that fits the plan from <paramref name="planOn"/>; the largest when none does.
    /// <paramref name="suggestedScale"/> says the scale is the plan's own pick, not the user's.
    /// </summary>
    public static SheetSizeSpec Smallest(
        IReadOnlyList<SheetSizeSpec> sheets, Func<SheetSizeSpec, InitialViewsResult> planOn, bool suggestedScale)
    {
        ArgumentNullException.ThrowIfNull(sheets);
        ArgumentNullException.ThrowIfNull(planOn);
        if (sheets.Count == 0)
        {
            throw new ArgumentException("There are no sheets to choose from.", nameof(sheets));
        }

        foreach (var sheet in sheets)
        {
            if (FitsWell(planOn(sheet), suggestedScale))
            {
                return sheet;
            }
        }

        return sheets[^1];
    }

    /// <summary>The next larger sheet after <paramref name="current"/> at which the plan fits, or null when there is none (or the plan already fits).</summary>
    public static SheetSizeSpec? NextThatFits(
        IReadOnlyList<SheetSizeSpec> sheets, SheetSizeSpec current, Func<SheetSizeSpec, InitialViewsResult> planOn)
    {
        ArgumentNullException.ThrowIfNull(sheets);
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(planOn);

        if (planOn(current).Fits)
        {
            return null;
        }

        var index = sheets.ToList().FindIndex(s => s.FormatName == current.FormatName);
        for (var i = index + 1; i < sheets.Count && index >= 0; i++)
        {
            if (planOn(sheets[i]).Fits)
            {
                return sheets[i];
            }
        }

        return null;
    }

    private static bool FitsWell(InitialViewsResult plan, bool suggestedScale) =>
        plan.Views.Count == 0 || (plan.Fits && (!suggestedScale || plan.Scale >= MinimumSuggestedScale));
}
