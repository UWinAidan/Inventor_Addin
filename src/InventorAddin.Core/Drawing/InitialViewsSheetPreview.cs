namespace InventorAddin.Core.Drawing;

/// <summary>A rectangle on the sheet, in mm from the sheet's bottom-left corner, y up.</summary>
public sealed record PreviewRectMm(double LeftMm, double BottomMm, double RightMm, double TopMm)
{
    public double WidthMm => RightMm - LeftMm;
    public double HeightMm => TopMm - BottomMm;
}

/// <summary>One planned view on the preview: its kind, the words drawn in it and its rectangle.</summary>
public sealed record PreviewView(InitialViewKind Kind, string Label, PreviewRectMm Rect);

/// <summary>
/// What the Initial Views window draws (task 234): the sheet, its border, the title block band, the usable area and the planned view
/// rectangles, all in mm from the sheet's bottom-left corner with y up. The control only draws it, scaled to fit.
/// </summary>
public sealed record InitialViewsSheetPreview(
    double SheetWidthMm,
    double SheetHeightMm,
    PreviewRectMm Border,
    PreviewRectMm TitleBlock,
    PreviewRectMm UsableArea,
    IReadOnlyList<PreviewView> Views,
    string ScaleText,
    string FrontText,
    bool Fits)
{
    /// <summary>Builds the preview from a sheet, the plan made for it and the front view's words.</summary>
    /// <param name="sheet">The sheet (its size, border and units).</param>
    /// <param name="plan">The plan for that sheet; its view rectangles are in cm.</param>
    /// <param name="scaleText">The scale as drawings write it, for example "1:2".</param>
    /// <param name="frontText">The front view's direction, for example "Front" or "Top".</param>
    public static InitialViewsSheetPreview Build(SheetSizeSpec sheet, InitialViewsResult plan, string scaleText, string frontText)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(plan);

        var border = new PreviewRectMm(sheet.BorderLeftMm, sheet.BorderBottomMm, sheet.BorderRightMm, sheet.BorderTopMm);
        // The title block sits in the border's bottom-right corner.
        (double tx, double ty) = sheet.TitleBlockOriginMm;
        var titleBlock = new PreviewRectMm(
            tx, ty, tx + PrintTemplateLayout.TitleBlockWidthMm, ty + PrintTemplateLayout.TitleBlockHeightMm);
        SheetAreaCm area = InitialViewsPlan.UsableArea(sheet);
        var usable = new PreviewRectMm(area.LeftCm * 10, area.BottomCm * 10, area.RightCm * 10, area.TopCm * 10);

        var views = plan.Views.Select(v => new PreviewView(
            v.Kind,
            LabelFor(v.Kind, frontText),
            new PreviewRectMm(
                (v.CentreXCm - v.WidthCm / 2) * 10,
                (v.CentreYCm - v.HeightCm / 2) * 10,
                (v.CentreXCm + v.WidthCm / 2) * 10,
                (v.CentreYCm + v.HeightCm / 2) * 10))).ToList();

        return new InitialViewsSheetPreview(
            sheet.WidthMm, sheet.HeightMm, border, titleBlock, usable, views, scaleText, frontText, plan.Fits);
    }

    private static string LabelFor(InitialViewKind kind, string frontText) => kind switch
    {
        InitialViewKind.Front or InitialViewKind.FoldedFront => string.IsNullOrEmpty(frontText) ? "Front" : $"Front: {frontText}",
        InitialViewKind.Top => "Top",
        InitialViewKind.Right => "Right",
        InitialViewKind.Iso => "Iso",
        InitialViewKind.FlatPattern => "Flat",
        _ => kind.ToString(),
    };
}
