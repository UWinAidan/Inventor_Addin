using System.Globalization;
using System.Text.RegularExpressions;

namespace InventorAddin.Core.Drawing;

/// <summary>
/// What to do to one title block value so it fits its cell (task 238, spec 05 questions 30 to 32, spikes 218 and 241).
/// Sizes are the em in millimetres, like the rest of the template layout. <see cref="OriginYMm"/> is the text box's origin
/// (upper justification, so the top of the capitals), in title block coordinates.
/// </summary>
/// <param name="SizeMm">The text size (em).</param>
/// <param name="WidthScale">Inventor's text width scale, between <see cref="TitleBlockFit.MinWidthScale"/> and 1.</param>
/// <param name="Lines">How many lines the longest value takes at this size and scale (1 for every field but Description).</param>
/// <param name="OriginYMm">Where the box's origin goes.</param>
/// <param name="BoxWidthMm">Only for a value that wraps (Description on two lines): the width to give the box. Null otherwise: a width must never be set on a single-line field.</param>
/// <param name="Warning">Set when even the smallest, narrowest text does not fit; names the field and how much too long it is.</param>
public sealed record TitleBlockFitResult(
    PrintField Field,
    double SizeMm,
    double WidthScale,
    int Lines,
    double OriginYMm,
    double? BoxWidthMm,
    string? Warning);

/// <summary>
/// Decides how a title block value is narrowed, shrunk and (Description) wrapped to fit its cell. Every value is first narrowed to
/// <see cref="MinWidthScale"/> (0.6, Aidan), then made smaller down to <see cref="MinSizeMm"/> (1.8 mm, Default). Description may
/// instead go onto two lines at <see cref="TwoLineSizeMm"/> before it shrinks (Default, spike 241); a single-line field never wraps.
/// Below the floor it warns, naming the field. The title block definition is shared by every sheet, so the decision takes every
/// value the field has on any sheet and gives one size, scale and origin that fits them all.
/// </summary>
public static class TitleBlockFit
{
    /// <summary>The narrowest width scale (Aidan, spec 05 question 30).</summary>
    public const double MinWidthScale = 0.6;

    /// <summary>The smallest text size (Default, spec 05 question 32): the title block's label size.</summary>
    public const double MinSizeMm = PrintTemplateLayout.LabelSizeMm;

    /// <summary>Description's size on two lines (Default, spike 241): two lines fit its cell with margin (about 3.65 is the most).</summary>
    public const double TwoLineSizeMm = 3.4;

    /// <summary>How far the top of line 1's capitals sits below the DESCRIPTION label's baseline (spike 241: label 47.6, origin 46.3).</summary>
    public const double TwoLineOriginGapMm = 1.3;

    /// <summary>The space kept between a value's right end and its cell's edge (Description: 112 of the cell's 115 mm less its 1.5 mm left inset, spike 241).</summary>
    public const double RightPaddingMm = 1.5;

    /// <summary>The size steps the shrink moves in.</summary>
    public const double SizeStepMm = 0.05;

    /// <summary>The widest a value may be in <paramref name="cell"/>, in millimetres.</summary>
    public static double AvailableWidthMm(TitleBlockCell cell) => cell.RightMm - cell.ValueXMm - RightPaddingMm;

    /// <summary>The cells whose values are fitted: the field cells, but not the centred ones (the revision letter).</summary>
    public static bool IsFitted(TitleBlockCell cell) => cell.Field != null && cell.ValueAnchor == TextAnchor.Start;

    /// <summary>
    /// The size, scale and origin for the values <paramref name="values"/> a field has on the drawing's sheets. Blank values are ignored.
    /// With none, the result is the cell's own size, scale 1 and the layout's origin, so a field that once shrank goes back.
    /// </summary>
    public static TitleBlockFitResult Fit(TitleBlockCell cell, IEnumerable<string> values)
    {
        ArgumentNullException.ThrowIfNull(cell);
        ArgumentNullException.ThrowIfNull(values);
        if (cell.Field is not { } field)
            throw new ArgumentException("The cell has no field.", nameof(cell));

        var texts = values.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim()).Distinct().ToList();
        double avail = AvailableWidthMm(cell);
        double size0 = cell.ValueSizeMm;
        double MaxWidth(double size) => texts.Count == 0 ? 0 : texts.Max(t => PrintTextFit.WidthMm(t, size, cell.Bold));
        TitleBlockFitResult OneLine(double size, double scale, string? warning = null) =>
            new(field, size, scale, 1, PrintTextFit.UpperOriginYMm(cell.ValueBaselineYMm, size), null, warning);

        // 1. One line at the cell's size, narrowed as far as the floor.
        double w0 = MaxWidth(size0);
        if (w0 <= avail)
            return OneLine(size0, 1);
        double scale0 = RoundDown(avail / w0);
        if (scale0 >= MinWidthScale)
            return OneLine(size0, scale0);

        bool isDescription = field == PrintField.Description;
        double floorSize = Math.Min(MinSizeMm, size0);

        if (isDescription)
        {
            // 2. Two lines at the two-line size, narrowed as little as possible.
            double twoLine = Math.Min(TwoLineSizeMm, size0);
            if (FitsLines(texts, cell.Bold, twoLine, MinWidthScale, avail, 2))
                return Wrapped(cell, texts, twoLine, LargestScaleFitting(texts, cell.Bold, twoLine, avail, 2), avail);

            // 3. Two lines at the narrowest scale, smaller and smaller.
            for (double s = twoLine - SizeStepMm; s >= floorSize - 1e-9; s -= SizeStepMm)
            {
                double size = Math.Max(floorSize, Math.Round(s / SizeStepMm) * SizeStepMm);
                if (FitsLines(texts, cell.Bold, size, MinWidthScale, avail, 2))
                    return Wrapped(cell, texts, size, MinWidthScale, avail);
            }

            // 4. Does not fit: smallest and narrowest, and say so.
            int needLines = texts.Max(t => PrintTextFit.Wrap(t, floorSize, avail / MinWidthScale, cell.Bold).Count);
            double total = texts.Max(t => PrintTextFit.WidthMm(t, floorSize, cell.Bold)) * MinWidthScale;
            double percent = Math.Max(0, (total / (2 * avail) - 1) * 100);
            string warning = $"{Name(cell)} is too long for its cell: even at {Num(floorSize)} mm and {Num(MinWidthScale)} width it needs {needLines.ToString(CultureInfo.InvariantCulture)} lines, "
                + $"about {Num(percent, 0)}% more than two lines hold.";
            return Wrapped(cell, texts, floorSize, MinWidthScale, avail, warning);
        }

        // 2. One line at 0.6, as large as it can be.
        double fitSize = size0 * avail / (MinWidthScale * w0);
        fitSize = Math.Floor(fitSize / SizeStepMm + 1e-9) * SizeStepMm;
        if (fitSize >= floorSize - 1e-9)
        {
            fitSize = Math.Min(size0, fitSize);
            double scale = RoundDown(Math.Min(1, avail / MaxWidth(fitSize)));
            return OneLine(fitSize, Math.Max(MinWidthScale, scale));
        }

        // 3. Does not fit at the floor.
        double over = MaxWidth(floorSize) * MinWidthScale - avail;
        double overPercent = over / avail * 100;
        return OneLine(floorSize, MinWidthScale,
            $"{Name(cell)} is too long for its cell: even at {Num(floorSize)} mm and {Num(MinWidthScale)} width it is {Num(over)} mm ({Num(overPercent, 0)}%) too long.");
    }

    private static TitleBlockFitResult Wrapped(
        TitleBlockCell cell, List<string> texts, double size, double scale, double avail, string? warning = null)
    {
        int lines = texts.Count == 0 ? 1 : texts.Max(t => PrintTextFit.Wrap(t, size, avail / scale, cell.Bold).Count);
        lines = Math.Min(2, Math.Max(1, lines));
        double origin = lines >= 2
            ? cell.LabelBaselineYMm - TwoLineOriginGapMm
            : PrintTextFit.UpperOriginYMm(cell.ValueBaselineYMm, size);
        return new TitleBlockFitResult(cell.Field!.Value, size, scale, lines, origin, lines >= 2 ? avail : null, warning);
    }

    private static bool FitsLines(List<string> texts, bool bold, double size, double scale, double avail, int maxLines) =>
        texts.All(t => PrintTextFit.Wrap(t, size, avail / scale, bold).Count <= maxLines);

    /// <summary>The largest scale in [<see cref="MinWidthScale"/>, 1] at which every text still fits <paramref name="maxLines"/> lines (a smaller scale gives each line more room).</summary>
    private static double LargestScaleFitting(List<string> texts, bool bold, double size, double avail, int maxLines)
    {
        if (FitsLines(texts, bold, size, 1, avail, maxLines))
            return 1;
        double lo = MinWidthScale, hi = 1;
        for (int i = 0; i < 40; i++)
        {
            double mid = (lo + hi) / 2;
            if (FitsLines(texts, bold, size, mid, avail, maxLines))
                lo = mid;
            else
                hi = mid;
        }

        // Rounded down: a smaller scale only gives more room, so it still fits.
        return Math.Max(MinWidthScale, RoundDown(lo));
    }

    private static double RoundDown(double scale) => Math.Floor(scale * 1000 + 1e-9) / 1000;

    private static string Name(TitleBlockCell cell) => cell.Label ?? cell.Name;

    private static string Num(double value, int decimals = 1) =>
        value.ToString(decimals == 0 ? "0" : "0." + new string('#', decimals), CultureInfo.InvariantCulture);

    // --- Reading and writing a field's formatted text and state ---

    private static readonly Regex StyleOverrideTag = new(@"<StyleOverride\b[^>]*>", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex FontSizeAttribute = new(@"\bFontSize\s*=\s*(['""])([^'""]*)\1", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex PropertyName = new(@"<Property\b[^>]*?\bProperty\s*=\s*(['""])(?<name>[^'""]*)\1", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex DerivedId = new(@"<DerivedProperty\b[^>]*?\bDerivedID\s*=\s*(['""])(?<id>\d+)\1", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>Which field a title block text box holds, from the property it reads; null for plain text and fields not fitted (Units).</summary>
    public static PrintField? FieldOf(string? formatted)
    {
        if (string.IsNullOrEmpty(formatted))
            return null;

        var property = PropertyName.Match(formatted);
        if (property.Success)
        {
            return property.Groups["name"].Value switch
            {
                "Part Number" => PrintField.PartNumber,
                "Description" => PrintField.Description,
                "Revision Number" => PrintField.Revision,
                "User Status" => PrintField.Status,
                "Material" => PrintField.Material,
                "Material Size" => PrintField.MaterialSize,
                "Finish Spec" => PrintField.FinishSpec,
                "Designer" => PrintField.Designer,
                "Author" => PrintField.DrawingAuthor,
                "Creation Time" => PrintField.DrawingCreationDate,
                "Mass" => PrintField.Mass,
                "Project" => PrintField.Project,
                _ => null,
            };
        }

        var derived = DerivedId.Match(formatted);
        if (derived.Success)
        {
            return derived.Groups["id"].Value switch
            {
                "29707" => PrintField.Scale,
                "29704" => PrintField.SheetNumberOfCount,
                "29705" => PrintField.SheetSize,
                "29697" => PrintField.FileName,
                _ => null,
            };
        }

        return null;
    }

    /// <summary>The cap height in centimetres Inventor's <c>FontSize</c> override means (spike 241) for an em of <paramref name="sizeMm"/>.</summary>
    public static double FontSizeCm(double sizeMm) => PrintTextFit.CapHeightMm(sizeMm) / 10;

    /// <summary>The em in millimetres of a <c>FontSize</c> override given in centimetres (the first one in the text); null when it has none.</summary>
    public static double? SizeMmOf(string? formatted)
    {
        if (string.IsNullOrEmpty(formatted))
            return null;
        foreach (Match tag in StyleOverrideTag.Matches(formatted))
        {
            var attribute = FontSizeAttribute.Match(tag.Value);
            if (attribute.Success && double.TryParse(attribute.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double cm))
                return cm * 10 / PrintTextFit.CapHeightEm;
        }

        return null;
    }

    /// <summary>
    /// The formatted text with every <c>StyleOverride</c>'s font size set to an em of <paramref name="sizeMm"/>. A StyleOverride without a
    /// size gets one; text with no StyleOverride is wrapped in one. Fonts, bold and the fields are untouched. Inventor stores the size to
    /// 4 decimal places of a centimetre.
    /// </summary>
    public static string WithSizeMm(string formatted, double sizeMm)
    {
        ArgumentNullException.ThrowIfNull(formatted);
        string cm = FontSizeCm(sizeMm).ToString("0.#####", CultureInfo.InvariantCulture);
        if (!StyleOverrideTag.IsMatch(formatted))
            return $"<StyleOverride FontSize='{cm}'>{formatted}</StyleOverride>";

        return StyleOverrideTag.Replace(formatted, tag =>
        {
            if (FontSizeAttribute.IsMatch(tag.Value))
                return FontSizeAttribute.Replace(tag.Value, m => $"FontSize={m.Groups[1].Value}{cm}{m.Groups[1].Value}");
            return tag.Value.Insert("<StyleOverride".Length, $" FontSize='{cm}'");
        });
    }

    /// <summary>
    /// True when a box's state differs from the decision, so it needs editing. The size and origin are compared with the rounding
    /// Inventor applies (size: 4 decimal places of a centimetre). A box that is to wrap needs its width; one that is not is left alone,
    /// since a wide box holding one line prints the same as a fitted one (spike 241).
    /// </summary>
    public static bool NeedsChange(
        TitleBlockFitResult target, double? currentSizeMm, double currentWidthScale, double currentOriginYMm, double? currentWidthMm)
    {
        if (currentSizeMm is not { } size || Math.Abs(size - target.SizeMm) > 0.01)
            return true;
        if (Math.Abs(currentWidthScale - target.WidthScale) > 0.0015)
            return true;
        if (Math.Abs(currentOriginYMm - target.OriginYMm) > 0.02)
            return true;
        return target.BoxWidthMm is { } width && (currentWidthMm is not { } now || Math.Abs(now - width) > 0.05);
    }
}
