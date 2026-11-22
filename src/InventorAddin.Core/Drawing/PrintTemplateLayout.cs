namespace InventorAddin.Core.Drawing;

/// <summary>Metric (ISO sheets) or imperial (ANSI sheets): the two print templates.</summary>
public enum PrintUnits
{
    Metric,
    Imperial,
}

/// <summary>What a title block cell reads. Task 187 maps each one to an Inventor property or drawing value.</summary>
public enum PrintField
{
    PartNumber,
    Description,
    Revision,
    /// <summary>The custom Status text property (task 210), not Inventor's Design Status (Aidan, 2026-11-22).</summary>
    Status,
    Material,
    /// <summary>The custom Material Size property the Stock Size command fills.</summary>
    MaterialSize,
    /// <summary>The custom Finish Spec property (task 211).</summary>
    FinishSpec,
    Designer,
    /// <summary>The drawing's Author, printed as the Detailer.</summary>
    DrawingAuthor,
    DrawingCreationDate,
    Mass,
    Scale,
    SheetNumberOfCount,
    Units,
    SheetSize,
    Project,
    FileName,
}

/// <summary>Where the text sits horizontally relative to its x.</summary>
public enum TextAnchor
{
    Start,
    Centre,
}

/// <summary>The four edges of the border.</summary>
public enum SheetEdge
{
    Top,
    Bottom,
    Left,
    Right,
}

/// <summary>One sheet size and its border. Coordinates are millimetres, origin the sheet's bottom-left, y up.</summary>
public sealed record SheetSizeSpec(
    string Name,
    PrintUnits Units,
    double WidthMm,
    double HeightMm,
    int ZonesAcross,
    int ZonesDown,
    double FilingMarginMm,
    double OtherMarginMm)
{
    public bool IsPortrait => HeightMm > WidthMm;

    /// <summary>The sheet format's name in the template (Create Print, task 188, picks a sheet by it): "AWB ISO A3", "AWB ANSI B".</summary>
    public string FormatName => (Units == PrintUnits.Metric ? "AWB ISO " : "AWB ANSI ") + Name;

    public double BorderLeftMm => FilingMarginMm;
    public double BorderRightMm => WidthMm - OtherMarginMm;
    public double BorderBottomMm => OtherMarginMm;
    public double BorderTopMm => HeightMm - OtherMarginMm;
    public double BorderWidthMm => BorderRightMm - BorderLeftMm;
    public double BorderHeightMm => BorderTopMm - BorderBottomMm;

    /// <summary>Where the title block's bottom-left corner goes (spike 183: its bottom-right sits on the border's inner corner).</summary>
    public (double X, double Y) TitleBlockOriginMm =>
        (BorderRightMm - PrintTemplateLayout.TitleBlockWidthMm, BorderBottomMm);

    public bool TitleBlockFits =>
        BorderWidthMm >= PrintTemplateLayout.TitleBlockWidthMm - 1e-9
        && BorderHeightMm >= PrintTemplateLayout.TitleBlockHeightMm - 1e-9;

    /// <summary>Zone numbers, left to right: 1, 2, 3 ...</summary>
    public IReadOnlyList<string> ZoneColumnLabels =>
        Enumerable.Range(1, ZonesAcross).Select(i => i.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToList();

    /// <summary>Zone letters, from the top: A, B, C ...</summary>
    public IReadOnlyList<string> ZoneRowLabels =>
        Enumerable.Range(0, ZonesDown).Select(i => ((char)('A' + i)).ToString()).ToList();

    /// <summary>The x of each zone's left edge across the border, plus the right edge: <see cref="ZonesAcross"/> + 1 values.</summary>
    public IReadOnlyList<double> ZoneColumnEdgesXMm =>
        Enumerable.Range(0, ZonesAcross + 1).Select(i => BorderLeftMm + BorderWidthMm * i / ZonesAcross).ToList();

    /// <summary>The y of each zone's edge, from the top of the border down: <see cref="ZonesDown"/> + 1 values.</summary>
    public IReadOnlyList<double> ZoneRowEdgesYMm =>
        Enumerable.Range(0, ZonesDown + 1).Select(i => BorderTopMm - BorderHeightMm * i / ZonesDown).ToList();

    /// <summary>The centring arrow of one edge: a triangle, tip first, pointing in across the zone frame.</summary>
    public IReadOnlyList<(double X, double Y)> CentringArrow(SheetEdge edge)
    {
        double mx = (BorderLeftMm + BorderRightMm) / 2, my = (BorderBottomMm + BorderTopMm) / 2;
        const double tipIn = PrintTemplateLayout.CentringArrowTipInsetMm;
        const double baseOut = PrintTemplateLayout.ZoneFrameOffsetMm + 0.5;
        const double half = PrintTemplateLayout.CentringArrowHalfWidthMm;
        return edge switch
        {
            SheetEdge.Top => new[] { (mx, BorderTopMm - tipIn), (mx - half, BorderTopMm + baseOut), (mx + half, BorderTopMm + baseOut) },
            SheetEdge.Bottom => new[] { (mx, BorderBottomMm + tipIn), (mx - half, BorderBottomMm - baseOut), (mx + half, BorderBottomMm - baseOut) },
            SheetEdge.Left => new[] { (BorderLeftMm + tipIn, my), (BorderLeftMm - baseOut, my - half), (BorderLeftMm - baseOut, my + half) },
            _ => new[] { (BorderRightMm - tipIn, my), (BorderRightMm + baseOut, my - half), (BorderRightMm + baseOut, my + half) },
        };
    }

    /// <summary>
    /// Where the X/Y triad's origin goes, in border coordinates (from the border's bottom-left corner). Normally the triad's own
    /// inset in from the corner. When the title block reaches far enough left to lie under the triad (A4 and ANSI A portrait, where
    /// the block fills the border's width) the origin is the same inset in from the left edge and that inset above the block's top
    /// edge. Decided by geometry, not by sheet name.
    /// </summary>
    public (double X, double Y) TriadOriginMm
    {
        get
        {
            var t = PrintTemplateLayout.Triad;
            double blockLeft = TitleBlockOriginMm.X - BorderLeftMm;
            bool blockUnderTriad = blockLeft <= t.OriginXMm + PrintTemplateLayout.TriadExtentMm;
            return blockUnderTriad
                ? (t.OriginXMm, PrintTemplateLayout.TitleBlockHeightMm + t.OriginYMm)
                : (t.OriginXMm, t.OriginYMm);
        }
    }

    /// <summary>The small part number's start point in sheet coordinates: in the right margin, level with the border's bottom.</summary>
    public (double X, double Y) SmallPartNumberMm =>
        (WidthMm - PrintTemplateLayout.SmallPartNumberEdgeInsetMm, BorderBottomMm);
}

/// <summary>
/// A line of the title block. Coordinates are millimetres in the block's own frame: origin the block's bottom-left, y up
/// (<see cref="PrintTemplateLayout.OriginNote"/>).
/// </summary>
public sealed record TitleBlockLine(double X1Mm, double Y1Mm, double X2Mm, double Y2Mm, double WeightMm);

/// <summary>
/// A rectangle cell of the title block. Task 187 draws each cell's four edges at <see cref="PrintTemplateLayout.CellLineWeightMm"/>.
/// Field cells (<see cref="Field"/> set) print a model value at the Value* position; labels are drawn at the Label* position
/// (inline cells draw the label followed by a colon, so "PROJECT:" and "FILE NAME:").
/// </summary>
/// <param name="FixedText">
/// Descriptive only, never drawn from here: it names what the fixed cell holds (the logo's file name, the tolerance sentence, the
/// projection caption, the URL). The drawable content is <see cref="PrintTemplateLayout.Texts"/>, <see cref="PrintTemplateLayout.Logo"/>
/// and <see cref="PrintTemplateLayout.Symbol"/>; drawing FixedText as well would print it twice.
/// </param>
public sealed record TitleBlockCell(
    string Name,
    double XMm,
    double YMm,
    double WidthMm,
    double HeightMm,
    string? Label,
    PrintField? Field,
    string? FixedText,
    double ValueSizeMm,
    bool Bold,
    TextAnchor ValueAnchor,
    bool Inline,
    double LabelXMm,
    double LabelBaselineYMm,
    double ValueXMm,
    double ValueBaselineYMm)
{
    public double RightMm => XMm + WidthMm;
    public double TopMm => YMm + HeightMm;
}

/// <summary>A fixed piece of text at a point (its baseline start, or its centre when <see cref="Anchor"/> is Centre).</summary>
public sealed record TitleBlockText(
    string CellName,
    string Text,
    double XMm,
    double BaselineYMm,
    double SizeMm,
    bool Bold,
    bool IsLabel,
    TextAnchor Anchor);

/// <summary>The logo picture's place in the block (its file is in <c>libraries/Branding/</c>).</summary>
public sealed record TitleBlockLogo(string FileName, double XMm, double YMm, double WidthMm, double HeightMm);

/// <summary>
/// A field printed outside the block's cells: the small part number in the right margin. It is placed by
/// <see cref="SheetSizeSpec.SmallPartNumberMm"/> (<see cref="EdgeInsetMm"/> from the sheet's right edge, level with the border's bottom),
/// not in block coordinates.
/// </summary>
public sealed record TitleBlockMarginField(PrintField Field, double SizeMm, double RotationDegrees, double EdgeInsetMm);

/// <summary>One column of the parts list or the revision table.</summary>
public sealed record TableColumn(string Title, string Source, double WidthMm);

/// <summary>
/// The third angle projection symbol, in block coordinates: a frustum, two circles and centre lines. The centre lines are drawn as
/// dashed centre lines (long dash, short gap, short dash, short gap).
/// </summary>
public sealed record ProjectionSymbol(
    IReadOnlyList<(double X, double Y)> Frustum,
    IReadOnlyList<TitleBlockLine> CentreLines,
    (double X, double Y) CircleCentre,
    double OuterRadiusMm,
    double InnerRadiusMm);

/// <summary>The X/Y triad. <see cref="OriginXMm"/> and <see cref="OriginYMm"/> are its inset from the border's bottom-left corner (or from the title block's top-left, see <see cref="SheetSizeSpec.TriadOriginMm"/>, which gives the origin per sheet in border coordinates).</summary>
/// <remarks>
/// The axis lines are drawn at <see cref="AxisWeightMm"/>; the X and Y labels sit <see cref="LabelDistanceMm"/> out along their axes.
/// Label positions are the labels' lower-left corners relative to the triad's origin, and <see cref="ExtentMm"/> is derived from them,
/// so the drawing and <see cref="SheetSizeSpec.TriadOriginMm"/> cannot drift apart.
/// </remarks>
public sealed record TriadSpec(double OriginXMm, double OriginYMm, double AxisLengthMm, double ArrowLengthMm, double ArrowHalfWidthMm,
    double LabelSizeMm, double AxisWeightMm = 0.4, double LabelDistanceMm = 14.5)
{
    public string XLabel => "X";
    public string YLabel => "Y";

    /// <summary>The X label's lower-left corner: beyond the x axis's end, centred (about) on the axis.</summary>
    public (double X, double Y) XLabelMm => (LabelDistanceMm, -LabelSizeMm / 2);

    /// <summary>The Y label's lower-left corner: above the y axis's end, centred (about) on the axis.</summary>
    public (double X, double Y) YLabelMm => (-LabelSizeMm * 0.35, LabelDistanceMm);

    /// <summary>How far the labels reach from the origin along an axis: the label distance plus the label height.</summary>
    public double ExtentMm => LabelDistanceMm + LabelSizeMm;
}

/// <summary>
/// The whole print template as data (spec 05, "Decided for M5" and "Added for M5"; mockup <c>docs/ui-mockups/title-block.html</c>):
/// sheet sizes, border, zones, triad, the title block's lines, cells and fixed text, and the table columns.
/// Task 187 walks it and draws. Millimetres everywhere, whatever the units of the template.
/// </summary>
public sealed class PrintTemplateLayout
{
    /// <summary>
    /// Title block coordinates: origin the block's bottom-left corner, x right, y up (spike 183: the block's bottom-right sits
    /// on the border's inner corner and the sketch origin is the block's bottom-left; text may lie outside the block).
    /// The mockup's y runs down from the top, so every y is converted once, through <see cref="FromTop"/>.
    /// </summary>
    public const string OriginNote = "Block bottom-left, x right, y up, millimetres.";

    public const double TitleBlockWidthMm = 180;
    public const double TitleBlockHeightMm = 55;

    /// <summary>The project and file name row on top of the approved 50 mm block.</summary>
    public const double TopRowHeightMm = 5;

    public const string FontName = "Tahoma";

    public const double LabelSizeMm = 1.8;
    public const double ValueSizeMm = 2.8;
    public const double DescriptionSizeMm = 4;
    public const double PartNumberSizeMm = 7;
    public const double SmallFieldSizeMm = 2.5;

    /// <summary>Outline weight as drawn in the mockup (0.7). A reviewer remembered 0.35; task 187 confirms the real weight against the mockup.</summary>
    public const double OutlineWeightMm = 0.7;
    public const double RowLineWeightMm = 0.5;
    public const double CellLineWeightMm = 0.35;

    // Left section.
    public const double LogoCellMm = 22;
    public const double LogoSizeMm = 19;
    public const double LogoMarginMm = 1.5;
    public const double LeftSectionWidthMm = 65;
    public const double ToleranceBoxHeightMm = 22;
    public const double ProjectionCellHeightMm = 20;
    public const double UrlCellHeightMm = 8;

    // The small part number: 2.5 mm, rotated 90 degrees (reads upward, glyphs extend to the left of the baseline), its baseline
    // 3 mm in from the sheet's right edge as in the mockup (7 mm outside the ISO border, 9.7 mm outside the ANSI one, so clear
    // of the zone frame), level with the border's bottom.
    public const double SmallPartNumberEdgeInsetMm = 3;
    public const double SmallPartNumberRotationDegrees = 90;

    /// <summary>How far 2.5 mm upper-case digits and letters reach left of the rotated baseline (about 0.7 of the text height).</summary>
    public const double SmallPartNumberGlyphHeightMm = 1.8;

    // Zone frame and centring arrows.
    public const double ZoneFrameOffsetMm = 5;
    public const double ZoneLabelSizeMm = 3;
    public const double CentringArrowHalfWidthMm = 2.2;
    public const double CentringArrowTipInsetMm = 1.5;

    public const string ThirdAngleText = "THIRD ANGLE PROJECTION";
    public const string ToleranceHeading = "UNLESS OTHERWISE SPECIFIED:";
    public const string EdgeNote = "REMOVE BURRS, BREAK SHARP EDGES.";

    /// <summary>Spec 05, metric general tolerances, word for word.</summary>
    public const string MetricToleranceText =
        "UNLESS OTHERWISE SPECIFIED: DIMENSIONS IN MM. TOLERANCES: X ±0.5, X.X ±0.2, X.XX ±0.05, ANGLES ±0.5°. REMOVE BURRS, BREAK SHARP EDGES.";

    /// <summary>Spec 05, imperial general tolerances, with the same heading as the metric box.</summary>
    public const string ImperialToleranceText =
        "UNLESS OTHERWISE SPECIFIED: DIMENSIONS IN INCHES. TOLERANCES: .X ±.03, .XX ±.01, .XXX ±.005, ANGLES ±0.5°. REMOVE BURRS, BREAK SHARP EDGES.";

    public const string ToleranceCellName = "Tolerances";
    public const string LogoCellName = "Logo";
    public const string ProjectionCellName = "Projection";
    public const string UrlCellName = "Url";

    /// <summary>Converts a y measured down from the top of the block, for an item <paramref name="height"/> high, to the y of its bottom edge.</summary>
    public static double FromTop(double yDown, double height = 0) => TitleBlockHeightMm - yDown - height;

    /// <summary>A baseline measured down from the block's top, as a y up from its bottom.</summary>
    private static double Baseline(double yDown) => TitleBlockHeightMm - yDown;

    /// <summary>How far the triad's labels reach from its origin (17.5 mm), derived from <see cref="Triad"/>.</summary>
    public static double TriadExtentMm => Triad.ExtentMm;

    /// <summary>The sheet size made last, so the saved template's own sheet is the one Create Print most often wants.</summary>
    public string DefaultSheetName => Units == PrintUnits.Metric ? "A3" : "B";

    public static TriadSpec Triad { get; } = new(5, 5, 11, 13, 1, 3);

    public PrintUnits Units { get; }
    public IReadOnlyList<SheetSizeSpec> SheetSizes { get; }
    public string ToleranceText { get; }
    /// <summary>What the Units cell reads for this template: MM or IN.</summary>
    public string UnitsText { get; }
    /// <summary>The unit the Weight cell prints.</summary>
    public string MassUnit { get; }
    public IReadOnlyList<TitleBlockLine> Lines { get; }
    public IReadOnlyList<TitleBlockCell> Cells { get; }
    public IReadOnlyList<TitleBlockText> Texts { get; }
    public TitleBlockLogo Logo { get; }
    public ProjectionSymbol Symbol { get; }
    public TitleBlockMarginField SmallPartNumber { get; }

    public static IReadOnlyList<TableColumn> PartsListColumns { get; } = new[]
    {
        new TableColumn("ITEM", "Item", 12),
        new TableColumn("QTY", "Quantity", 12),
        new TableColumn("PART NUMBER", "Part Number", 35),
        new TableColumn("DESCRIPTION", "Description", 60),
        new TableColumn("MAKE/BUY", "Make/Buy", 18),
        new TableColumn("MANUFACTURER", "Vendor", 30),
        new TableColumn("MANUFACTURER PART NUMBER", "Stock Number", 35),
    };

    /// <summary>Default (spec 05 gives no columns): rev, description, date, by.</summary>
    public static IReadOnlyList<TableColumn> RevisionTableColumns { get; } = new[]
    {
        new TableColumn("REV", "Revision", 12),
        new TableColumn("DESCRIPTION", "Description", 80),
        new TableColumn("DATE", "Date", 25),
        new TableColumn("BY", "By", 20),
    };

    public static IReadOnlyList<SheetSizeSpec> IsoSheets { get; } = new[]
    {
        new SheetSizeSpec("A4", PrintUnits.Metric, 210, 297, 4, 6, 20, 10),
        new SheetSizeSpec("A3", PrintUnits.Metric, 420, 297, 8, 6, 20, 10),
        new SheetSizeSpec("A2", PrintUnits.Metric, 594, 420, 12, 8, 20, 10),
        new SheetSizeSpec("A1", PrintUnits.Metric, 841, 594, 16, 12, 20, 10),
        new SheetSizeSpec("A0", PrintUnits.Metric, 1189, 841, 24, 16, 20, 10),
    };

    /// <summary>ANSI A to E (A portrait, the rest landscape), 0.5 in border, the filing edge 20 mm as on the ISO sheets.</summary>
    public static IReadOnlyList<SheetSizeSpec> AnsiSheets { get; } = new[]
    {
        new SheetSizeSpec("A", PrintUnits.Imperial, 215.9, 279.4, 4, 2, 20, 12.7),
        new SheetSizeSpec("B", PrintUnits.Imperial, 431.8, 279.4, 4, 4, 20, 12.7),
        new SheetSizeSpec("C", PrintUnits.Imperial, 558.8, 431.8, 8, 4, 20, 12.7),
        new SheetSizeSpec("D", PrintUnits.Imperial, 863.6, 558.8, 8, 4, 20, 12.7),
        new SheetSizeSpec("E", PrintUnits.Imperial, 1117.6, 863.6, 16, 8, 20, 12.7),
    };

    public static PrintTemplateLayout Metric { get; } = new(PrintUnits.Metric);
    public static PrintTemplateLayout Imperial { get; } = new(PrintUnits.Imperial);

    public static PrintTemplateLayout For(PrintUnits units) => units == PrintUnits.Metric ? Metric : Imperial;

    private PrintTemplateLayout(PrintUnits units)
    {
        bool metric = units == PrintUnits.Metric;
        Units = units;
        SheetSizes = metric ? IsoSheets : AnsiSheets;
        ToleranceText = metric ? MetricToleranceText : ImperialToleranceText;
        UnitsText = metric ? "MM" : "IN";
        MassUnit = metric ? "kg" : "lb";

        var cells = new List<TitleBlockCell>();
        var texts = new List<TitleBlockText>();

        // Field cells: the mockup's x, y (down from the top of the block), w, h.
        void Field(string name, double x, double yDown, double w, double h, string label, PrintField field, double size,
            bool bold = false, bool centre = false, bool inline = false)
        {
            double labelX = x + 1, labelBase, valueX, valueBase;
            if (inline)
            {
                labelBase = Baseline(yDown + h / 2 + 0.65);
                valueX = x + 1 + label.Length * 1.25 + 2;
                valueBase = Baseline(yDown + h / 2 + size * 0.36);
            }
            else
            {
                labelBase = Baseline(yDown + 2.4);
                valueX = centre ? x + w / 2 : x + 1.5;
                valueBase = Baseline(yDown + h - (h - 2.6 - size) / 2 - 0.6);
            }
            cells.Add(new TitleBlockCell(name, x, FromTop(yDown, h), w, h, label, field, null, size, bold,
                centre ? TextAnchor.Centre : TextAnchor.Start, inline, labelX, labelBase, valueX, valueBase));
        }

        Field("Project", 0, 0, 90, 5, "PROJECT", PrintField.Project, SmallFieldSizeMm, inline: true);
        Field("FileName", 90, 0, 90, 5, "FILE NAME", PrintField.FileName, SmallFieldSizeMm, inline: true);
        Field("Description", 65, 5, 115, 12, "DESCRIPTION", PrintField.Description, DescriptionSizeMm);
        Field("PartNumber", 65, 17, 71, 14, "PART NUMBER", PrintField.PartNumber, PartNumberSizeMm, bold: true);
        Field("Revision", 136, 17, 16, 14, "REV", PrintField.Revision, PartNumberSizeMm, bold: true, centre: true);
        Field("Status", 152, 17, 28, 14, "STATUS", PrintField.Status, SmallFieldSizeMm);
        Field("Material", 65, 31, 40, 8, "MATERIAL", PrintField.Material, ValueSizeMm);
        Field("MaterialSize", 105, 31, 30, 8, "MATERIAL SIZE", PrintField.MaterialSize, SmallFieldSizeMm);
        Field("Finish", 135, 31, 45, 8, "FINISH", PrintField.FinishSpec, ValueSizeMm);
        Field("Designer", 65, 39, 28.75, 8, "DESIGNER", PrintField.Designer, ValueSizeMm);
        Field("Detailer", 93.75, 39, 28.75, 8, "DETAILER", PrintField.DrawingAuthor, ValueSizeMm);
        Field("Date", 122.5, 39, 28.75, 8, "DATE", PrintField.DrawingCreationDate, ValueSizeMm);
        Field("Weight", 151.25, 39, 28.75, 8, "WEIGHT", PrintField.Mass, ValueSizeMm);
        Field("Scale", 65, 47, 28.75, 8, "SCALE", PrintField.Scale, ValueSizeMm);
        Field("Sheet", 93.75, 47, 28.75, 8, "SHEET", PrintField.SheetNumberOfCount, ValueSizeMm);
        Field("Units", 122.5, 47, 28.75, 8, "UNITS", PrintField.Units, ValueSizeMm);
        Field("Size", 151.25, 47, 28.75, 8, "SIZE", PrintField.SheetSize, ValueSizeMm);

        // Left section, 65 wide: logo and tolerances on top, the projection across, the URL across the bottom.
        TitleBlockCell Fixed(string name, double x, double yDown, double w, double h, string fixedText) =>
            new(name, x, FromTop(yDown, h), w, h, null, null, fixedText, ValueSizeMm, false, TextAnchor.Start, false, 0, 0, 0, 0);

        cells.Add(Fixed(LogoCellName, 0, TopRowHeightMm, LogoCellMm, LogoCellMm, Branding.PrintLogoFileName));
        cells.Add(Fixed(ToleranceCellName, LogoCellMm, TopRowHeightMm, LeftSectionWidthMm - LogoCellMm, ToleranceBoxHeightMm, ToleranceText));
        cells.Add(Fixed(ProjectionCellName, 0, TopRowHeightMm + ToleranceBoxHeightMm, LeftSectionWidthMm, ProjectionCellHeightMm, ThirdAngleText));
        cells.Add(Fixed(UrlCellName, 0, TopRowHeightMm + ToleranceBoxHeightMm + ProjectionCellHeightMm, LeftSectionWidthMm, UrlCellHeightMm, Branding.WebsiteUrl));
        Cells = cells;

        Logo = new TitleBlockLogo(Branding.PrintLogoFileName, LogoMarginMm,
            FromTop(TopRowHeightMm + LogoMarginMm, LogoSizeMm), LogoSizeMm, LogoSizeMm);

        // Tolerance box texts: heading, units, linear tolerances stacked, angles beside them, the edge note.
        TitleBlockText T(string text, double x, double yDownInBlock, double size, bool bold = false, bool label = false) =>
            new(ToleranceCellName, text, x, Baseline(yDownInBlock + TopRowHeightMm), size, bold, label, TextAnchor.Start);
        texts.Add(T(ToleranceHeading, 23, 2.4, LabelSizeMm, label: true));
        texts.Add(T(metric ? "DIMENSIONS IN MM" : "DIMENSIONS IN INCHES", 23, 5.9, 2.5, bold: true));
        texts.Add(T("TOLERANCES:", 23, 8.7, LabelSizeMm, label: true));
        string[][] rows = metric
            ? new[] { new[] { "X", "±0.5" }, new[] { "X.X", "±0.2" }, new[] { "X.XX", "±0.05" } }
            : new[] { new[] { ".X", "±.03" }, new[] { ".XX", "±.01" }, new[] { ".XXX", "±.005" } };
        for (int i = 0; i < rows.Length; i++)
        {
            texts.Add(T(rows[i][0], 24, 11.8 + i * 2.9, 2.4));
            texts.Add(T(rows[i][1], 31, 11.8 + i * 2.9, 2.4));
        }
        texts.Add(T("ANGLES", 47, 11.8, 2.4));
        texts.Add(T("±0.5°", 47, 14.7, 2.4));
        texts.Add(T(EdgeNote, 23, 21.1, LabelSizeMm));
        // Projection and URL cells.
        texts.Add(new TitleBlockText(ProjectionCellName, ThirdAngleText, 1, Baseline(TopRowHeightMm + 24.4), LabelSizeMm, false, true, TextAnchor.Start));
        texts.Add(new TitleBlockText(UrlCellName, Branding.WebsiteUrl, LeftSectionWidthMm / 2, Baseline(TopRowHeightMm + 47), ValueSizeMm, false, false, TextAnchor.Centre));
        Texts = texts;

        // Lines other than the cell edges: the outline, the row under the project row, the angles divider.
        Lines = new List<TitleBlockLine>
        {
            new(0, 0, TitleBlockWidthMm, 0, OutlineWeightMm),
            new(TitleBlockWidthMm, 0, TitleBlockWidthMm, TitleBlockHeightMm, OutlineWeightMm),
            new(TitleBlockWidthMm, TitleBlockHeightMm, 0, TitleBlockHeightMm, OutlineWeightMm),
            new(0, TitleBlockHeightMm, 0, 0, OutlineWeightMm),
            new(0, FromTop(TopRowHeightMm), TitleBlockWidthMm, FromTop(TopRowHeightMm), RowLineWeightMm),
            new(LeftSectionWidthMm, 0, LeftSectionWidthMm, FromTop(TopRowHeightMm), CellLineWeightMm),
            new(45, FromTop(TopRowHeightMm + 9.8), 45, FromTop(TopRowHeightMm + 18.6), CellLineWeightMm),
        };

        // Third angle symbol: frustum on the left, circles on the right, centre lines through both (block coordinates).
        double sx = 27, sy = FromTop(TopRowHeightMm + 32.5);
        Symbol = new ProjectionSymbol(
            new[] { (sx - 4.5, sy + 3.5), (sx + 3.5, sy + 2.1), (sx + 3.5, sy - 2.1), (sx - 4.5, sy - 3.5) },
            new TitleBlockLine[]
            {
                new(sx - 6, sy, sx + 5, sy, 0.18),
                new(sx + 7.5, sy, sx + 16.5, sy, 0.18),
                new(sx + 12, sy - 4.8, sx + 12, sy + 4.8, 0.18),
            },
            (sx + 12, sy), 3.5, 2.1);

        SmallPartNumber = new TitleBlockMarginField(PrintField.PartNumber, SmallFieldSizeMm, SmallPartNumberRotationDegrees,
            SmallPartNumberEdgeInsetMm);
    }
}
