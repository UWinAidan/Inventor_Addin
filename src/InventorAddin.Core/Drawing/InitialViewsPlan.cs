namespace InventorAddin.Core.Drawing;

/// <summary>The kind of model the initial views are for.</summary>
public enum InitialViewModelKind
{
    Part,
    SheetMetalPart,
    Assembly,
}

/// <summary>One of the views Place Initial Views can put on the sheet.</summary>
public enum InitialViewKind
{
    Front,
    Top,
    Right,
    Iso,
    /// <summary>A sheet metal part's folded model, as the front view.</summary>
    FoldedFront,
    FlatPattern,
}

/// <summary>
/// The model's range box as the front view sees it, in cm: the width and height of the front view and the depth along the viewing
/// direction. The add-in maps Inventor's range box axes to these three; Core does not know which axis is which.
/// </summary>
public sealed record InitialViewsBoxCm(double WidthCm, double HeightCm, double DepthCm);

/// <summary>The size of a sheet metal part's flat pattern, in cm (<c>FlatPattern.Length</c> by <c>Width</c>), as the view shows it.</summary>
public sealed record FlatPatternSizeCm(double LengthCm, double WidthCm);

/// <summary>The size of one view at scale 1, in cm, as a placed view reports it (<c>DrawingView.Width</c> and <c>Height</c> divided by its scale).</summary>
public sealed record ViewSizeCm(double WidthCm, double HeightCm);

/// <summary>A rectangle on the sheet, in cm from the sheet's bottom-left corner, y up.</summary>
public sealed record SheetAreaCm(double LeftCm, double BottomCm, double RightCm, double TopCm)
{
    public double WidthCm => RightCm - LeftCm;
    public double HeightCm => TopCm - BottomCm;
}

/// <summary>One view of the plan: its centre on the sheet and its size at the plan's scale, in cm.</summary>
public sealed record PlannedView(InitialViewKind Kind, double CentreXCm, double CentreYCm, double WidthCm, double HeightCm);

/// <summary>The result of <see cref="InitialViewsPlan.Plan"/>.</summary>
/// <param name="Views">The views to place, in the order front, top, right, iso (folded front, flat pattern).</param>
/// <param name="Scale">A standard scale (1.0 is 1:1, 0.5 is 1:2).</param>
/// <param name="Fits">False when even the smallest standard scale leaves the views larger than the share of the area Create Print's rule allows.</param>
/// <param name="Messages">What the window shows the user: a flat pattern left out, no views chosen, views that do not fit.</param>
public sealed record InitialViewsResult(
    IReadOnlyList<PlannedView> Views,
    double Scale,
    bool Fits,
    IReadOnlyList<string> Messages);

/// <summary>
/// Which initial views a model gets and where they go (spec 05, "Decided for M5: Place Initial Views"; spike 183 round 3).
/// A view's position is its centre. Third angle: the right view sits to the right of the front view, the top view above it.
/// Pure arithmetic in cm; the add-in reads the model, calls this, and places the views.
/// </summary>
public static class InitialViewsPlan
{
    /// <summary>The clear gap between neighbouring views on the sheet, in cm (Default: 15 mm).</summary>
    public const double ViewGapCm = 1.5;

    public const string FlatPatternUnknownMessage =
        "The flat pattern size is not known, so the flat pattern is left out.";

    public const string NoViewsMessage = "No views are chosen, so nothing will be placed.";

    public const string DoesNotFitMessage =
        "The views do not fit the usable area at the smallest standard scale.";

    public const string DoesNotFitAtScaleMessage =
        "The views do not fit the usable area at this scale. Choose a smaller scale or fewer views.";

    /// <summary>The views a model kind offers, in placing order. The flat pattern is only offered for sheet metal.</summary>
    public static IReadOnlyList<InitialViewKind> DefaultViews(InitialViewModelKind model) => model switch
    {
        InitialViewModelKind.Part => new[] { InitialViewKind.Front, InitialViewKind.Top, InitialViewKind.Right, InitialViewKind.Iso },
        InitialViewModelKind.SheetMetalPart => new[] { InitialViewKind.FoldedFront, InitialViewKind.FlatPattern, InitialViewKind.Iso },
        _ => new[] { InitialViewKind.Front, InitialViewKind.Iso },
    };

    /// <summary>
    /// The room the views may use, in cm: inside the border and above the title block (Default). The title block already includes
    /// the project row and the Material Size and Finish fields (180 by 55 mm). Anything the parts list or notes need on top of that
    /// is a later task's.
    /// </summary>
    public static SheetAreaCm UsableArea(SheetSizeSpec sheet)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        return new SheetAreaCm(
            sheet.BorderLeftMm / 10,
            (sheet.BorderBottomMm + PrintTemplateLayout.TitleBlockHeightMm) / 10,
            sheet.BorderRightMm / 10,
            sheet.BorderTopMm / 10);
    }

    /// <summary>
    /// The estimated size of the isometric view of a box, in cm at scale 1. A true isometric view shows each axis at
    /// sqrt(2/3) of its length with the two horizontal axes 30 degrees off the horizontal, so the box's outline is
    /// <c>0.7071 (W + D)</c> wide and <c>0.8165 H + 0.4082 (W + D)</c> high. Inventor's iso view is that projection; the outline of
    /// the model itself is no larger than its box, so this is a safe upper estimate.
    /// </summary>
    public static (double WidthCm, double HeightCm) IsoSizeCm(InitialViewsBoxCm box)
    {
        ArgumentNullException.ThrowIfNull(box);
        double across = box.WidthCm + box.DepthCm;
        return (Math.Sqrt(0.5) * across, Math.Sqrt(2.0 / 3.0) * box.HeightCm + Math.Sqrt(2.0 / 3.0) * 0.5 * across);
    }

    /// <summary>Chooses the views, the one scale and each view's centre.</summary>
    /// <param name="model">The kind of model.</param>
    /// <param name="box">The model's range box, mapped to the front view.</param>
    /// <param name="flatPattern">The flat pattern's size, or null when it is not known (a sheet metal part only).</param>
    /// <param name="area">The room for the views, <see cref="UsableArea"/>.</param>
    /// <param name="requested">
    /// The views the user kept ticked, or null for all of the model kind's. Views the model kind does not offer are ignored. The
    /// rest are laid out again, with no gap left where a view was dropped.
    /// </param>
    /// <param name="fixedScale">
    /// A scale the user chose, or null to pick the largest standard scale that fits. A fixed scale is used as given; when the views
    /// do not fit at it the result has <c>Fits</c> false and <see cref="DoesNotFitAtScaleMessage"/>.
    /// </param>
    /// <param name="measured">
    /// The real size at scale 1 of any of the views, read from views placed in a scratch drawing (task 227). A measured view replaces
    /// the estimate from the box, which is only an upper bound (the model's outline is inside its range box, and an assembly's range
    /// box can be much larger than its geometry). Null, or a view missing from it, keeps the estimate.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">A size is zero, negative or not a finite number.</exception>
    public static InitialViewsResult Plan(
        InitialViewModelKind model,
        InitialViewsBoxCm box,
        FlatPatternSizeCm? flatPattern,
        SheetAreaCm area,
        IEnumerable<InitialViewKind>? requested = null,
        double? fixedScale = null,
        IReadOnlyDictionary<InitialViewKind, ViewSizeCm>? measured = null)
    {
        ArgumentNullException.ThrowIfNull(box);
        ArgumentNullException.ThrowIfNull(area);
        RequirePositive(box.WidthCm, nameof(box));
        RequirePositive(box.HeightCm, nameof(box));
        RequirePositive(box.DepthCm, nameof(box));
        RequirePositive(area.WidthCm, nameof(area));
        RequirePositive(area.HeightCm, nameof(area));
        if (flatPattern is not null)
        {
            RequirePositive(flatPattern.LengthCm, nameof(flatPattern));
            RequirePositive(flatPattern.WidthCm, nameof(flatPattern));
        }

        if (fixedScale is double fs)
            RequirePositive(fs, nameof(fixedScale));

        var messages = new List<string>();
        HashSet<InitialViewKind>? wanted = requested is null ? null : new HashSet<InitialViewKind>(requested);

        var kinds = new List<InitialViewKind>();
        foreach (InitialViewKind kind in DefaultViews(model))
        {
            if (wanted is not null && !wanted.Contains(kind))
                continue;
            if (kind == InitialViewKind.FlatPattern && flatPattern is null)
            {
                messages.Add(FlatPatternUnknownMessage);
                continue;
            }

            kinds.Add(kind);
        }

        if (kinds.Count == 0)
        {
            messages.Add(NoViewsMessage);
            return new InitialViewsResult(Array.Empty<PlannedView>(), 1.0, true, messages);
        }

        // Size of each view at scale 1.
        (double isoW, double isoH) = IsoSizeCm(box);
        var sizes = new Dictionary<InitialViewKind, (double W, double H)>
        {
            [InitialViewKind.Front] = (box.WidthCm, box.HeightCm),
            [InitialViewKind.FoldedFront] = (box.WidthCm, box.HeightCm),
            [InitialViewKind.Top] = (box.WidthCm, box.DepthCm),
            [InitialViewKind.Right] = (box.DepthCm, box.HeightCm),
            [InitialViewKind.Iso] = (isoW, isoH),
        };
        if (flatPattern is not null)
            sizes[InitialViewKind.FlatPattern] = (flatPattern.LengthCm, flatPattern.WidthCm);
        if (measured is not null)
        {
            foreach ((InitialViewKind kind, ViewSizeCm size) in measured)
            {
                RequirePositive(size.WidthCm, nameof(measured));
                RequirePositive(size.HeightCm, nameof(measured));
                sizes[kind] = (size.WidthCm, size.HeightCm);
            }
        }

        // Create Print's scale rule (Aidan, 2026-11-20): the arranged views fit within PrintPlan.SheetFillFraction of the area.
        double maxWidth = area.WidthCm * PrintPlan.SheetFillFraction;
        double maxHeight = area.HeightCm * PrintPlan.SheetFillFraction;

        if (fixedScale is double chosen)
        {
            Layout fixedLayout = Arrange(model, kinds, sizes, chosen);
            bool fixedFits = fixedLayout.WidthCm <= maxWidth + 1e-9 && fixedLayout.HeightCm <= maxHeight + 1e-9;
            if (!fixedFits)
                messages.Add(DoesNotFitAtScaleMessage);
            return Result(fixedLayout, area, chosen, fixedFits, messages);
        }

        foreach (double scale in PrintPlan.StandardScales)
        {
            Layout layout = Arrange(model, kinds, sizes, scale);
            if (layout.WidthCm <= maxWidth + 1e-9 && layout.HeightCm <= maxHeight + 1e-9)
                return Result(layout, area, scale, true, messages);
        }

        double smallest = PrintPlan.StandardScales[^1];
        messages.Add(DoesNotFitMessage);
        return Result(Arrange(model, kinds, sizes, smallest), area, smallest, false, messages);
    }

    // The grid cell of each view (column 0 left, row 0 bottom). A part: front and top stacked on the left, right and iso stacked
    // on the right. Sheet metal: the folded front with the iso above it, the flat pattern on the right. Assembly: one row, the
    // second view in the right column.
    private static (int Col, int Row) Cell(InitialViewModelKind model, InitialViewKind kind) => kind switch
    {
        InitialViewKind.Iso when model == InitialViewModelKind.SheetMetalPart => (0, 1),
        InitialViewKind.Front or InitialViewKind.FoldedFront => (0, 0),
        InitialViewKind.Top => (0, 1),
        InitialViewKind.Right => (1, 0),
        InitialViewKind.FlatPattern => (1, 0),
        _ => model == InitialViewModelKind.Part ? (1, 1) : (1, 0),
    };

    private sealed record Layout(
        IReadOnlyList<(InitialViewKind Kind, double W, double H, int Col, int Row)> Items,
        double[] ColumnWidths,
        double[] RowHeights,
        double WidthCm,
        double HeightCm);

    private static Layout Arrange(InitialViewModelKind model, List<InitialViewKind> kinds,
        Dictionary<InitialViewKind, (double W, double H)> sizes, double scale)
    {
        var items = kinds.Select(k =>
        {
            (int col, int row) = Cell(model, k);
            return (Kind: k, W: sizes[k].W * scale, H: sizes[k].H * scale, Col: col, Row: row);
        }).ToList();

        double[] colW = new double[2], rowH = new double[2];
        foreach (var it in items)
        {
            colW[it.Col] = Math.Max(colW[it.Col], it.W);
            rowH[it.Row] = Math.Max(rowH[it.Row], it.H);
        }

        double width = colW.Sum() + ViewGapCm * (colW.Count(c => c > 0) - 1);
        double height = rowH.Sum() + ViewGapCm * (rowH.Count(r => r > 0) - 1);
        return new Layout(items, colW, rowH, width, height);
    }

    private static InitialViewsResult Result(Layout layout, SheetAreaCm area, double scale, bool fits, List<string> messages)
    {
        // The group is centred in the area; an empty column or row takes no space and no gap.
        double x0 = area.LeftCm + (area.WidthCm - layout.WidthCm) / 2;
        double y0 = area.BottomCm + (area.HeightCm - layout.HeightCm) / 2;

        double[] colLeft = new double[2], rowBottom = new double[2];
        double x = x0;
        for (int c = 0; c < 2; c++)
        {
            colLeft[c] = x;
            if (layout.ColumnWidths[c] > 0)
                x += layout.ColumnWidths[c] + ViewGapCm;
        }

        double y = y0;
        for (int r = 0; r < 2; r++)
        {
            rowBottom[r] = y;
            if (layout.RowHeights[r] > 0)
                y += layout.RowHeights[r] + ViewGapCm;
        }

        var views = new List<PlannedView>();
        foreach (var it in layout.Items)
        {
            double cellW = layout.ColumnWidths[it.Col], cellH = layout.RowHeights[it.Row];
            // Centred in its cell, except the right view, which hugs the front view on its left, and the top view, which hugs
            // the front view below it, so the gap to the front view is exactly the view gap.
            double cx = it.Kind == InitialViewKind.Right
                ? colLeft[it.Col] + it.W / 2
                : colLeft[it.Col] + cellW / 2;
            double cy = it.Kind == InitialViewKind.Top
                ? rowBottom[it.Row] + it.H / 2
                : rowBottom[it.Row] + cellH / 2;
            views.Add(new PlannedView(it.Kind, cx, cy, it.W, it.H));
        }

        return new InitialViewsResult(views, scale, fits, messages);
    }

    private static void RequirePositive(double value, string name)
    {
        if (!(value > 0) || double.IsInfinity(value))
            throw new ArgumentOutOfRangeException(name, value, "A size must be a positive, finite number.");
    }
}
