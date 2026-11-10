namespace InventorAddin.Core.Holes;

/// <summary>
/// Which holes are precision holes, and the colour they take (task 113; spec 09, "Toleranced clearance and Simple
/// holes"; spec 04 question 1). One rule, used for the fit colour now and by the hole table wizard (M4 part 2) to sort
/// holes into its precision and clearance tables.
/// </summary>
public static class HolePrecision
{
    /// <summary>The name a Custom tolerance carries in <see cref="HoleTolerance.Name"/>.</summary>
    public const string CustomName = "Custom";

    /// <summary>
    /// True for the kinds that take the Toleranced checkbox: Clearance (AWB Clearance) and Simple (Aidan, 2026-10-24).
    /// Not a dowel, which is always toleranced, nor a tapped or taper tapped hole, whose thread class is separate.
    /// </summary>
    public static bool CanBeToleranced(HoleKind kind) => kind is HoleKind.Clearance or HoleKind.Simple;

    /// <summary>
    /// True for a precision hole (Aidan, 2026-10-24): a dowel, or a toleranced Clearance or Simple hole.
    /// </summary>
    public static bool IsPrecision(HoleSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        return spec.Kind == HoleKind.Dowel || (spec.Toleranced && CanBeToleranced(spec.Kind));
    }

    /// <summary>
    /// True for a precision hole as the hole table sees it: a hole with AWB data by <see cref="IsPrecision(HoleSpec)"/>;
    /// a hole with no AWB data (or data that cannot be read) when Inventor has a tolerance on its diameter (Default,
    /// spec 04 question 1).
    /// </summary>
    /// <param name="awbSpec">The hole's stored AWB spec, or null when it has none that can be read.</param>
    /// <param name="inventorToleranceOnDiameter">Inventor reports a tolerance on the hole's diameter.</param>
    public static bool IsPrecision(HoleSpec? awbSpec, bool inventorToleranceOnDiameter) =>
        awbSpec is not null ? IsPrecision(awbSpec) : inventorToleranceOnDiameter;

    /// <summary>
    /// The fit whose colour a precision hole takes (Default, spec 09 question 17): a dowel's own Fit; a toleranced
    /// hole's Precision Tol row's fit, <paramref name="row"/>; Normal for a toleranced hole with a Custom tolerance. Null
    /// for a hole that is not precision, which is not coloured, and for a toleranced hole whose row is not known.
    /// </summary>
    public static PrecisionFit? ColourFit(HoleSpec spec, PrecisionToleranceRow? row)
    {
        ArgumentNullException.ThrowIfNull(spec);

        if (spec.Kind == HoleKind.Dowel)
        {
            return spec.PrecisionFit;
        }

        if (!IsPrecision(spec))
        {
            return null;
        }

        return spec.CustomTolerance ? PrecisionFit.Normal : row?.Fit;
    }

    /// <summary>
    /// The Precision Tol row a toleranced Clearance or Simple hole takes, as <see cref="HoleRules.Build"/> finds it
    /// (task 113): the row of the spec's tolerance name for its units, the one of its fit first when two share the name;
    /// the units' Default row when no name is given. Null for a Custom tolerance, a dowel (whose row is of its own fit),
    /// a hole that is not toleranced, and a name with no row.
    /// </summary>
    public static PrecisionToleranceRow? TolerancedRow(HoleSpec spec, HoleWorkbook workbook)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(workbook);

        return spec.Kind != HoleKind.Dowel && IsPrecision(spec) && !spec.CustomTolerance
            ? HoleRules.FindAnyTolerance(workbook, spec.Units, spec.Tolerance, spec.PrecisionFit)
            : null;
    }

    /// <summary>
    /// <see cref="ColourFit(HoleSpec, PrecisionToleranceRow?)"/> with a toleranced hole's row found in the workbook
    /// (<see cref="TolerancedRow"/>): the fit whose colour the hole's faces take on OK (task 138, for the window's
    /// swatch).
    /// </summary>
    public static PrecisionFit? ColourFitIn(HoleSpec spec, HoleWorkbook workbook) =>
        ColourFit(spec, TolerancedRow(spec, workbook));

    /// <summary>
    /// The colour every face of a hole takes on OK, from the workbook's Settings tab
    /// (<see cref="ColourFitIn"/>); null for a hole that is not coloured.
    /// </summary>
    public static HoleColour? ColourIn(HoleSpec spec, HoleWorkbook workbook)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        return ColourFitIn(spec, workbook) is { } fit ? workbook.Settings.FitColour(fit) : null;
    }

    /// <summary>
    /// The colour every face of a precision hole takes, from the workbook's Settings tab
    /// (<see cref="ColourFit(HoleSpec, PrecisionToleranceRow?)"/>); null for a hole that is not coloured.
    /// </summary>
    public static HoleColour? Colour(HoleSpec spec, PrecisionToleranceRow? row, HoleSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return ColourFit(spec, row) is { } fit ? settings.FitColour(fit) : null;
    }
}
