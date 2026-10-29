using System.Globalization;
using InventorAddin.Core.Holes;

namespace InventorAddin.Core.ViewModels;

/// <summary>
/// One entry of an AWB Hole dropdown or icon row: the value the view-model takes and the text the window shows. Bind
/// the list's items to the options and its selected value (<c>SelectedValuePath="Value"</c>) to the field. A choice
/// shown as an icon button (Type, Seat, Termination, Drill point; task 104) names its icon in <see cref="Icon"/>
/// (<see cref="HoleIcons"/>); the text is its tooltip and its name for screen readers.
/// </summary>
public sealed record HoleOption<T>(T Value, string Text)
{
    /// <summary>The <see cref="HoleIcons"/> name the icon button shows, or null for a choice shown as text.</summary>
    public string? Icon { get; init; }
}

/// <summary>
/// One entry of the Fit dropdown: the fit's name and, for dowel and precision holes, its colour from the workbook's
/// Settings tab as <c>#RRGGBB</c> (null for clearance fits, which are not coloured).
/// </summary>
public sealed record HoleFitOption(string Value, string Text, string? Swatch)
{
    /// <summary>A colour as <c>#RRGGBB</c>.</summary>
    public static string SwatchOf(HoleColour colour)
    {
        ArgumentNullException.ThrowIfNull(colour);
        return $"#{colour.R:X2}{colour.G:X2}{colour.B:X2}";
    }
}

/// <summary>
/// One entry of the Tolerance dropdown (task 098): the Precision Tol row's name as the value, the tolerance at the
/// chosen size as the main text (<c>H7  +0.012 / 0</c>, or a Limits row's <c>0 / -0.020</c>), and the row's name as
/// secondary text when it differs from the main text (else null). The closed dropdown shows the main text only.
/// </summary>
public sealed record HoleToleranceOption(string Value, string Text, string? Detail)
{
    /// <summary>After the code of an ISO row whose size ISO 286 does not cover.</summary>
    public const string NotInIso286Text = "not in ISO 286";

    /// <summary>
    /// The main text for a Precision Tol row at a size: an ISO row's code and its limits from <see cref="Iso286"/>
    /// (only the code when the size is not known, the code and <see cref="NotInIso286Text"/> when ISO 286 does not
    /// give limits); a Limits row's Upper and Lower. A row with neither a code nor both limits shows its name.
    /// </summary>
    /// <param name="nominal">The size's nominal diameter in the row's units, or null when it is not known.</param>
    public static string TextFor(PrecisionToleranceRow row, decimal? nominal)
    {
        ArgumentNullException.ThrowIfNull(row);

        var name = (row.Name ?? "").Trim();
        if (row.Kind == ToleranceKind.Limits)
        {
            return row.Upper is { } upper && row.Lower is { } lower
                ? LimitsText(upper, lower, row.Units)
                : name;
        }

        var code = (row.IsoCode ?? "").Trim();
        if (code.Length == 0)
        {
            return name;
        }

        if (nominal is not { } size)
        {
            return code;
        }

        var limits = Iso286.HoleLimits(HoleRules.IsoNominalMm(size, row.Units), code);
        if (!limits.Succeeded)
        {
            return $"{code}  {NotInIso286Text}";
        }

        var upperValue = row.Units == HoleUnits.Imperial ? limits.UpperUm / 25400m : limits.UpperUm / 1000m;
        var lowerValue = row.Units == HoleUnits.Imperial ? limits.LowerUm / 25400m : limits.LowerUm / 1000m;
        return $"{code}  {LimitsText(upperValue, lowerValue, row.Units)}";
    }

    /// <summary>Upper and lower limits as <c>+0.012 / 0</c>, in the units' length (see <see cref="FormatLimit"/>).</summary>
    public static string LimitsText(decimal upper, decimal lower, HoleUnits units) =>
        $"{FormatLimit(upper, units)} / {FormatLimit(lower, units)}";

    /// <summary>
    /// One limit: mm to 3 decimals, inches to 4, with a plus or minus sign; a limit that rounds to zero is <c>0</c>.
    /// </summary>
    public static string FormatLimit(decimal value, HoleUnits units)
    {
        var decimals = units == HoleUnits.Imperial ? 4 : 3;
        var rounded = Math.Round(value, decimals, MidpointRounding.AwayFromZero);
        if (rounded == 0m)
        {
            return "0";
        }

        var text = Math.Abs(rounded).ToString(decimals == 4 ? "0.0000" : "0.000", CultureInfo.InvariantCulture);
        return (rounded > 0m ? "+" : "-") + text;
    }
}

/// <summary>
/// View-model for the AWB Hole window (spec 09, "The window" and "Types and seats"). It holds the fields in the
/// window's order, the options each lists, the checks from <see cref="HoleRules"/>, the <see cref="HoleGeometry"/>
/// the add-in previews, OK and Cancel, and the status line. The add-in sets <see cref="CentreCount"/> as centres are
/// picked, previews on <see cref="PreviewNeeded"/>, and calls <see cref="ShowRefused"/> when Inventor refuses a change.
/// </summary>
/// <remarks>
/// <para>
/// Each list holds only what applies to the fields above it, in workbook order, without <c>Show = N</c> rows and
/// without repeats (a size, fastener, tolerance or drill point listed twice in the workbook shows once; the first row
/// wins, as <see cref="HoleRules"/> finds it). When a field changes, each later choice is kept when the new list still
/// holds it and otherwise takes its default: the first entry, Normal for a fit, the Default = Y row for a tolerance or
/// drill point (the first such row in workbook order, <see cref="HoleRules.FindTolerance"/> and
/// <see cref="HoleRules.FindDrillPoint"/>), and the Tapped row's depths for a tapped hole. A Units switch keeps the size
/// with the nearest nominal diameter in the other units, else the first. A tapped hole's Seat and Fastener type lists
/// follow its Size instead (task 099): the seats and Screw values of the Counterbore and Countersink rows of the same
/// size (<see cref="HoleRules.SameSizeCounterbores"/>).
/// </para>
/// <para>
/// The seat is kept when the units, type, standard or size change, as long as the new choice offers it (task 123,
/// Aidan 2026-10-25; this replaces task 085's reset to None on a change of type). When it does not, the seat is None,
/// with no message, and the seat the user chose last is remembered for the window's life: a change back to a choice
/// that offers it brings it back. Choosing a seat, None included, is what is remembered; a cascade never changes it.
/// </para>
/// <para>
/// A dowel hole lists every seat and has no Fastener type; its seat is sized in its own boxes (spec 09, "Dowel and
/// precision holes"; task 106): the counterbore diameter and depth, or the countersink diameter and angle. When the
/// window opens on a dowel, and whenever its units or size change, the boxes take that size's start values: the last
/// values kept with OK for the units and size (<see cref="HoleLastDowelSeats"/>), else the Dowel row's seat columns or
/// the same-size Counterbore or Countersink row (<see cref="HoleRules.DowelSeatStart"/>), value by value, else empty,
/// which <see cref="HoleRules"/> asks to be filled. A value typed in the window is kept while the units and size stay,
/// also across a change of seat; an edited dowel opens on its own values. Leaving AWB Dowel drops them. The counterbore
/// depth takes Inventor expressions like the depth; the diameters and the angle take numbers.
/// </para>
/// <para>
/// A blind clearance or dowel hole whose depth was not typed in this window starts at the last depth used in its units
/// (<see cref="HoleLastDepths"/>), else at the Settings tab's "Blind depth multiplier" x the size's nominal diameter
/// (spec 09 question 7), and follows that rule when it becomes blind or its size or units change. A depth typed in this
/// window, or an edited hole's own depth, is kept (and converted when the units change). Emptying the depth box leaves
/// it empty, so OK is disabled, until the next change the rule follows.
/// </para>
/// <para>
/// A choice that is not in the workbook, or is hidden, is kept and listed while the fields above it are unchanged, so
/// an edited hole shows exactly what it was made with; <see cref="HoleRules"/> reports a size that is not in the
/// workbook beside the Size field.
/// </para>
/// <para>
/// A setter given a value its list does not hold (a dropdown writes null when its items change) ignores it.
/// <see cref="PreviewNeeded"/> is raised once for each user change that changes <see cref="Spec"/>, after every field
/// it cascades into is settled; never while the view-model is built, and not for <see cref="CentreCount"/>, which the
/// add-in sets itself.
/// </para>
/// </remarks>
public sealed class AwbHoleViewModel : ObservableObject
{
    public const string CommandName = Branding.ShortName + " Hole";
    public const string EditHeaderTitleText = "Edit " + CommandName;

    public const string RefusedText = "Inventor could not make this hole; the last good hole is kept.";

    public const string InventorHoleNoteText =
        "This hole was made by Inventor's Hole command. The closest " + CommandName + " settings are shown; OK makes it an " +
        CommandName + ".";

    public const string MetricText = "Metric";
    public const string ImperialText = "Imperial";
    public const string NoneText = "None";
    public const string CounterboreText = "Counterbore";
    public const string CountersinkText = "Countersink";
    public const string DistanceText = "Distance";
    public const string ThroughAllText = "Through All";
    public const string ToText = "To";

    public const string NormalFitName = "Normal";

    public static IReadOnlyList<HoleOption<HoleUnits>> UnitsOptions { get; } =
    [
        new(HoleUnits.Metric, MetricText),
        new(HoleUnits.Imperial, ImperialText),
    ];

    public static IReadOnlyList<HoleOption<string>> TypeOptions { get; } =
    [
        new(HoleStandards.ClearanceType, HoleStandards.ClearanceType) { Icon = HoleIcons.Clearance },
        new(HoleStandards.TappedType, HoleStandards.TappedType) { Icon = HoleIcons.Tapped },
    ];

    // Termination To needs the end face picked, which the window cannot do yet (task 090): To is listed only for an
    // edited hole that already ends at a face, so the window shows what the hole is.
    private static readonly IReadOnlyList<HoleOption<HoleTermination>> PickableTerminations =
    [
        new(HoleTermination.Distance, DistanceText) { Icon = HoleIcons.Distance },
        new(HoleTermination.ThroughAll, ThroughAllText) { Icon = HoleIcons.ThroughAll },
    ];

    private static readonly IReadOnlyList<HoleOption<HoleTermination>> TerminationsWithTo =
    [
        new(HoleTermination.Distance, DistanceText) { Icon = HoleIcons.Distance },
        new(HoleTermination.ThroughAll, ThroughAllText) { Icon = HoleIcons.ThroughAll },
        new(HoleTermination.ToFace, ToText) { Icon = HoleIcons.ToFace },
    ];

    /// <summary>The window says this under Termination while an edited hole still ends at a face.</summary>
    public const string ToFaceNoteText =
        CommandName + " cannot pick the face a hole ends at yet. Choose Distance or Through All to change this hole.";

    /// <summary>The window says this under the centres of an edited hole whose centres are not sketch points.</summary>
    public const string CentresNotPickableText =
        "This hole's centres are not sketch points, so none can be added or removed here.";

    /// <summary>The window says this under the centres while they can be picked.</summary>
    public const string CentresHelpText = "Click a point in the model to add or remove a hole centre.";

    /// <summary>Beside a depth box whose text is not a number.</summary>
    public const string NotANumberText = "Enter a number.";

    /// <summary>Beside the countersink angle box.</summary>
    public const string AngleUnitText = "deg";

    /// <summary>The message when the command is started in a document that is not a part.</summary>
    public const string WrongDocumentText = CommandName + " works in part files. Open a part and try again.";

    /// <summary>When the hole workbook gives nothing to make a hole from and no reason was reported.</summary>
    public const string NoHoleSizesText =
        "The hole workbook has no hole sizes. Check the hole data in Settings.";

    private static readonly IReadOnlyList<HoleOption<HoleSeat>> AllSeats =
    [
        new(HoleSeat.None, NoneText) { Icon = HoleIcons.SeatNone },
        new(HoleSeat.Counterbore, CounterboreText) { Icon = HoleIcons.Counterbore },
        new(HoleSeat.Countersink, CountersinkText) { Icon = HoleIcons.Countersink },
    ];

    private readonly HoleWorkbook _workbook;
    private readonly string _partName;
    private readonly HoleLastDepths _lastDepths;
    private readonly HoleLastDowelSeats _lastDowelSeats;
    private readonly IHoleExpressionChecker? _expressionChecker;

    // The fields, in window order.
    private HoleUnits _units;
    private string _type = HoleStandards.ClearanceType;
    private HoleSeat _seat;

    // The seat the user chose last (or the edited hole's own): the seat shown whenever the current choice offers it,
    // else None (task 123). Task 124 keeps typed values across the same cascades in the same way: what the user chose
    // is held apart from what the cascade shows.
    private HoleSeat _wantedSeat;
    private ListContext? _seatContext;
    private string _standard = HoleStandards.ClearanceStandard;
    private string? _fastener;
    private string? _size;
    private string? _fit;
    private string? _tolerance;
    private decimal? _threadDepth;
    private decimal? _drillDepth;
    private HoleTermination _termination;
    private decimal? _depth;
    private string? _drillPoint;
    private bool _flip;

    // A dowel's seat sizes (task 106), in the units' length (the angle in degrees), and the units and size they were
    // started for.
    private decimal? _cboreDiameter;
    private decimal? _cboreDepth;
    private decimal? _csinkDiameter;
    private decimal? _csinkAngle;
    private ListContext? _dowelSeatContext;

    // The lists that depend on other fields.
    private IReadOnlyList<HoleOption<HoleSeat>> _seatOptions = AllSeats;
    private IReadOnlyList<HoleOption<string>> _standardOptions = [];
    private IReadOnlyList<HoleOption<string>> _fastenerOptions = [];
    private IReadOnlyList<HoleOption<string>> _sizeOptions = [];
    private IReadOnlyList<HoleFitOption> _fitOptions = [];
    private IReadOnlyList<HoleToleranceOption> _toleranceOptions = [];
    private IReadOnlyList<HoleOption<string>> _drillPointOptions = [];

    // What each list was last built for: a choice not in the workbook is kept only while this is unchanged.
    private ListContext? _fastenerContext;
    private ListContext? _sizeContext;
    private ListContext? _toleranceContext;
    private ListContext? _depthsContext;
    private HoleUnits _depthUnits;

    // The blind depth: whether the user typed it in this window (or it is the edited hole's own), and what the rule's
    // depth was last worked out for.
    private bool _depthKept;
    private ListContext? _blindDepthContext;

    private int _centreCount;
    private bool _canPickCentres = true;

    // The diagram (task 104): drawn from the fields after every change; whether it is open is the add-in's to keep.
    private HoleDiagramInput _diagramInput = new();
    private HoleDiagram? _diagram;
    private bool _isDiagramExpanded = true;

    // Depth box text that is not a number, by field, and Inventor's reason when it gave one; cleared by every change
    // that reaches the fields.
    private readonly Dictionary<HoleField, string> _invalidText = [];
    private readonly Dictionary<HoleField, string> _invalidReason = [];

    // The expression each depth was typed as (task 100), with the value it came to. It holds only while the depth
    // still has that value: a depth changed any other way (a units switch, a new size's defaults) is a plain number.
    private readonly Dictionary<HoleField, (string Text, decimal Value)> _expressions = [];

    private HoleSpec _spec;
    private HoleBuildResult _build;
    private IReadOnlyList<HoleCheck> _checks = [];
    private HoleSpec? _lastRaisedSpec;
    private bool _changing;

    private string _statusText = "";
    private bool _statusIsError;
    private IReadOnlyList<string> _statusNotes = [];
    private bool _isRefused;
    private readonly string _openStatusText;
    private readonly IReadOnlyList<string> _openStatusNotes;

    private AwbHoleViewModel(
        HoleWorkbook workbook,
        string partName,
        HoleSpec? editSpec,
        bool madeByInventor,
        IEnumerable<string>? inventorHoleNotes,
        IEnumerable<HoleWorkbookProblem>? problems,
        HoleLastDepths? lastDepths,
        IHoleExpressionChecker? expressionChecker,
        HoleLastDowelSeats? lastDowelSeats,
        HoleReapplyOffer? reapplyOffer = null)
    {
        ArgumentNullException.ThrowIfNull(workbook);

        _workbook = workbook;
        _partName = (partName ?? "").Trim();
        _lastDepths = lastDepths ?? HoleLastDepths.None;
        _lastDowelSeats = lastDowelSeats ?? HoleLastDowelSeats.None;
        _expressionChecker = expressionChecker;
        IsEdit = editSpec is not null;
        IsInventorHole = madeByInventor;
        InventorHoleNotes = (inventorHoleNotes ?? [])
            .Select(n => (n ?? "").Trim())
            .Where(n => n.Length > 0)
            .ToList()
            .AsReadOnly();
        // Only a hole opened on its stored AWB data can be re-applied (task 092).
        ReapplyDifferences = editSpec is not null && !madeByInventor && reapplyOffer is { IsOffered: true }
            ? reapplyOffer.Differences
            : [];
        TerminationOptions = editSpec?.Termination == HoleTermination.ToFace ? TerminationsWithTo : PickableTerminations;

        if (editSpec is null)
        {
            _units = workbook.Settings.DefaultUnits;
            _fit = NormalFitName;
            _termination = HoleTermination.ThroughAll;
            _depthUnits = _units;
            Refresh(keepExact: false);
        }
        else
        {
            var spec = editSpec.Normalised();
            _units = spec.Units;
            _type = HoleStandards.TypeName(spec.Kind);
            _standard = HoleStandards.StandardName(spec.Kind);
            _seat = spec.Seat;
            _wantedSeat = spec.Seat;
            _fastener = spec.Fastener;
            _size = spec.Size;
            _fit = spec.Kind == HoleKind.Dowel ? spec.PrecisionFit.ToString() : spec.ClearanceFit.ToString();
            _tolerance = spec.Tolerance;
            _threadDepth = spec.ThreadDepth;
            _drillDepth = spec.DrillDepth;
            _termination = spec.Termination;
            _depth = spec.Depth;
            _depthKept = spec.Depth is not null;
            _drillPoint = spec.DrillPoint;
            _flip = spec.Flip;
            _cboreDiameter = spec.CboreDiameter;
            _cboreDepth = spec.CboreDepth;
            _csinkDiameter = spec.CsinkDiameter;
            _csinkAngle = spec.CsinkAngleDeg;
            _depthUnits = _units;
            OpenExpression(HoleField.ThreadDepth, spec.ThreadDepthExpression, ref _threadDepth);
            OpenExpression(HoleField.DrillDepth, spec.DrillDepthExpression, ref _drillDepth);
            OpenExpression(HoleField.Depth, spec.DepthExpression, ref _depth);
            OpenExpression(HoleField.CounterboreDepth, spec.CboreDepthExpression, ref _cboreDepth);
            Refresh(keepExact: true);
        }

        OkCommand = new RelayCommand(() => OkRequested?.Invoke(this, EventArgs.Empty), () => CanOk);
        CancelCommand = new RelayCommand(() => CancelRequested?.Invoke(this, EventArgs.Empty));

        var problemList = (problems ?? []).ToList();
        _openStatusText = WorkbookProblemsText(problemList.Count);
        _openStatusNotes = problemList.Select(p => p.ToString()).ToList().AsReadOnly();
        _statusText = _openStatusText;
        _statusNotes = _openStatusNotes;

        (_spec, _build, _checks) = Evaluate();
        _diagramInput = BuildDiagramInput();
        _lastRaisedSpec = _spec;
    }

    /// <summary>A new hole: the workbook's default units, a clearance hole with no seat, through all.</summary>
    /// <param name="workbook">The hole workbook as read (task 082).</param>
    /// <param name="partName">The part's file name, for the header subline.</param>
    /// <param name="problems">The workbook's problems; the status line counts them at open, not as an error.</param>
    /// <param name="lastDepths">The last blind depth used in each units; a blind hole starts at it.</param>
    /// <param name="expressionChecker">
    /// Reads depth text that is not a plain number as Inventor does (task 100); without one, such text is refused.
    /// </param>
    /// <param name="lastDowelSeats">The last dowel seat used for each units and size; a dowel's seat starts at it.</param>
    public AwbHoleViewModel(
        HoleWorkbook workbook,
        string partName,
        IEnumerable<HoleWorkbookProblem>? problems = null,
        HoleLastDepths? lastDepths = null,
        IHoleExpressionChecker? expressionChecker = null,
        HoleLastDowelSeats? lastDowelSeats = null)
        : this(workbook, partName, null, false, null, problems, lastDepths, expressionChecker, lastDowelSeats)
    {
    }

    /// <summary>
    /// Edit mode: the window opens on <paramref name="spec"/>, the hole's stored spec, exactly as it is, before the
    /// first preview (spike 081 round 5).
    /// </summary>
    /// <param name="reapplyOffer">
    /// What no longer matches the hole's stored data (<see cref="HoleReapplyRules.Check"/>; task 092): the window says
    /// so (<see cref="ReapplyNote"/>) and OK re-applies the stored settings. Null or empty makes no offer.
    /// </param>
    public static AwbHoleViewModel ForEdit(
        HoleWorkbook workbook,
        string partName,
        HoleSpec spec,
        IEnumerable<HoleWorkbookProblem>? problems = null,
        HoleLastDepths? lastDepths = null,
        IHoleExpressionChecker? expressionChecker = null,
        HoleLastDowelSeats? lastDowelSeats = null,
        HoleReapplyOffer? reapplyOffer = null)
    {
        ArgumentNullException.ThrowIfNull(spec);
        return new AwbHoleViewModel(
            workbook, partName, spec, false, null, problems, lastDepths, expressionChecker, lastDowelSeats,
            reapplyOffer);
    }

    /// <summary>
    /// Edit mode for a hole made by Inventor's Hole command, or one whose AWB data cannot be read: the window opens on
    /// <paramref name="closestSpec"/>, the closest spec the add-in's read-back built from the hole's geometry, and
    /// <see cref="InventorHoleNote"/> says OK makes it an AWB hole (spec 09 question 5).
    /// </summary>
    /// <param name="notes">The read-back's notes, one for each choice it had to guess; shown under the note.</param>
    public static AwbHoleViewModel ForInventorHole(
        HoleWorkbook workbook,
        string partName,
        HoleSpec closestSpec,
        IEnumerable<string>? notes = null,
        IEnumerable<HoleWorkbookProblem>? problems = null,
        HoleLastDepths? lastDepths = null,
        IHoleExpressionChecker? expressionChecker = null,
        HoleLastDowelSeats? lastDowelSeats = null)
    {
        ArgumentNullException.ThrowIfNull(closestSpec);
        return new AwbHoleViewModel(
            workbook, partName, closestSpec, true, notes, problems, lastDepths, expressionChecker, lastDowelSeats);
    }

    /// <summary>
    /// Edit mode from the add-in's read-back of a hole (task 088, <c>HoleFeatureReader.ReadSpec</c>): an AWB hole opens
    /// on its stored spec (<see cref="ForEdit"/>), with <paramref name="reapplyOffer"/> when it no longer matches its
    /// stored data (task 092); any other hole on the closest spec with the read-back's notes
    /// (<see cref="ForInventorHole"/>), and no offer.
    /// </summary>
    public static AwbHoleViewModel ForReadBack(
        HoleWorkbook workbook,
        string partName,
        HoleReadBackResult readBack,
        IEnumerable<HoleWorkbookProblem>? problems = null,
        HoleLastDepths? lastDepths = null,
        IHoleExpressionChecker? expressionChecker = null,
        HoleLastDowelSeats? lastDowelSeats = null,
        HoleReapplyOffer? reapplyOffer = null)
    {
        ArgumentNullException.ThrowIfNull(readBack);
        return readBack.IsInventorHole
            ? ForInventorHole(
                workbook, partName, readBack.Spec, readBack.Notes, problems, lastDepths, expressionChecker,
                lastDowelSeats)
            : ForEdit(
                workbook, partName, readBack.Spec, problems, lastDepths, expressionChecker, lastDowelSeats,
                reapplyOffer);
    }

    /// <summary>
    /// Why the window cannot open on this workbook, or null when it can: a workbook with no Clearance, Tapped or Dowel
    /// rows has nothing to make a hole from. The message is the workbook's own problems with the file (a missing or
    /// unreadable file), else <see cref="NoHoleSizesText"/>. Rows with problems do not stop the window.
    /// </summary>
    public static string? WorkbookStopMessage(HoleWorkbookReadResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var workbook = result.Workbook;
        if (workbook.Clearance.Count > 0 || workbook.Tapped.Count > 0 || workbook.Dowel.Count > 0)
        {
            return null;
        }

        var fileProblems = result.Problems
            .Where(p => p.Row is null)
            .Select(p => p.ToString().Trim())
            .Where(m => m.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        return fileProblems.Count == 0 ? NoHoleSizesText : string.Join(Environment.NewLine, fileProblems);
    }

    /// <summary>
    /// Raised once for each user change that changes <see cref="Spec"/>: the add-in applies <see cref="Geometry"/> to
    /// the preview hole.
    /// </summary>
    public event EventHandler? PreviewNeeded;

    /// <summary>Raised by OK. The add-in keeps the hole and closes the window.</summary>
    public event EventHandler? OkRequested;

    /// <summary>Raised by Cancel. The add-in puts the part back as it was and closes the window.</summary>
    public event EventHandler? CancelRequested;

    // Header.

    /// <summary>True when the window edits an existing hole.</summary>
    public bool IsEdit { get; }

    /// <summary>True when the edited hole was made by Inventor's Hole command and has no AWB data.</summary>
    public bool IsInventorHole { get; }

    /// <summary><c>AWB Hole</c>, or <c>Edit AWB Hole</c> in edit mode.</summary>
    public string HeaderTitle => IsEdit ? EditHeaderTitleText : CommandName;

    /// <summary>The part's file name.</summary>
    public string HeaderSubline => _partName;

    /// <summary><see cref="InventorHoleNoteText"/> for a hole made by Inventor's Hole command, else null.</summary>
    public string? InventorHoleNote => IsInventorHole ? InventorHoleNoteText : null;

    /// <summary>The read-back's notes for a hole made by Inventor's Hole command; empty otherwise.</summary>
    public IReadOnlyList<string> InventorHoleNotes { get; }

    /// <summary>
    /// True when the edited AWB hole no longer matches its stored data (task 092; spec 09, "The command"): the window
    /// says what differs and that OK re-applies the settings shown, with their fit, tolerance and colour.
    /// </summary>
    public bool HasReapplyOffer => ReapplyDifferences.Count > 0;

    /// <summary><see cref="HoleReapplyRules.HeadingText"/> while there is an offer, else null.</summary>
    public string? ReapplyHeading => HasReapplyOffer ? HoleReapplyRules.HeadingText : null;

    /// <summary>What differs, one line each; empty when there is no offer.</summary>
    public IReadOnlyList<string> ReapplyDifferences { get; }

    /// <summary><see cref="HoleReapplyRules.OfferText"/> while there is an offer, else null.</summary>
    public string? ReapplyNote => HasReapplyOffer ? HoleReapplyRules.OfferText : null;

    // Fields, in the window's order.

    /// <summary>Metric or Imperial; picks the workbook rows. Starts on the workbook's default units.</summary>
    public HoleUnits Units
    {
        get => _units;
        set
        {
            if (!UnitsOptions.Any(o => o.Value == value))
            {
                OnPropertyChanged();
                return;
            }

            Change(() => Set(ref _units, value, nameof(Units)));
        }
    }

    /// <summary>Clearance or Tapped. The seat is kept when the new type offers it (task 123).</summary>
    public string Type
    {
        get => _type;
        set
        {
            if (Find(TypeOptions, value) is not { } type)
            {
                OnPropertyChanged();
                return;
            }

            Change(() => Set(ref _type, type, nameof(Type)));
        }
    }

    /// <summary>
    /// The seats: None, Counterbore, Countersink for a clearance or dowel hole; for a tapped hole, None and the seats
    /// with a row of the same size in their tab (task 099), and an edited hole's own seat until a field above it changes.
    /// </summary>
    public IReadOnlyList<HoleOption<HoleSeat>> SeatOptions => _seatOptions;

    public HoleSeat Seat
    {
        get => _seat;
        set
        {
            if (!_seatOptions.Any(o => o.Value == value))
            {
                OnPropertyChanged();
                return;
            }

            Change(() =>
            {
                _wantedSeat = value;
                Set(ref _seat, value, nameof(Seat));
            });
        }
    }

    /// <summary>AWB Clearance and AWB Dowel for Clearance; AWB Tapped for Tapped (spec 09 question 2).</summary>
    public IReadOnlyList<HoleOption<string>> StandardOptions => _standardOptions;

    public string Standard
    {
        get => _standard;
        set
        {
            if (Find(_standardOptions, value) is not { } standard)
            {
                OnPropertyChanged();
                return;
            }

            Change(() => Set(ref _standard, standard, nameof(Standard)));
        }
    }

    /// <summary>The kind the Type and Standard give.</summary>
    public HoleKind Kind => HoleStandards.KindFor(_standard) ?? HoleKind.Clearance;

    /// <summary>
    /// The Screw values of the seat's tab for the units, in workbook order (for a tapped hole, of the rows of the same
    /// size); empty unless the hole is seated, and for a dowel, whose seat follows no fastener (task 106).
    /// </summary>
    public IReadOnlyList<HoleOption<string>> FastenerOptions => _fastenerOptions;

    /// <summary>True for a seated clearance or tapped hole; never for a dowel (task 106).</summary>
    public bool ShowsFastener => _seat != HoleSeat.None && Kind != HoleKind.Dowel;

    public string? Fastener
    {
        get => _fastener;
        set => SetFromList(_fastenerOptions, value, nameof(Fastener), v => Set(ref _fastener, v, nameof(Fastener)));
    }

    // A dowel's seat sizes (task 106). The window binds the text properties further down.

    /// <summary>True for a dowel with a counterbore: its diameter and depth boxes show.</summary>
    public bool ShowsCounterboreSizes => Kind == HoleKind.Dowel && _seat == HoleSeat.Counterbore;

    /// <summary>True for a dowel with a countersink: its diameter and angle boxes show.</summary>
    public bool ShowsCountersinkSizes => Kind == HoleKind.Dowel && _seat == HoleSeat.Countersink;

    /// <summary>A dowel's counterbore diameter, in the units' length. Null empties the box.</summary>
    public decimal? CounterboreDiameter
    {
        get => _cboreDiameter;
        set
        {
            if (Kind != HoleKind.Dowel)
            {
                OnPropertyChanged();
                return;
            }

            Change(() => Set(ref _cboreDiameter, value, nameof(CounterboreDiameter)));
        }
    }

    /// <summary>A dowel's counterbore depth, in the units' length. Null empties the box.</summary>
    public decimal? CounterboreDepth
    {
        get => _cboreDepth;
        set
        {
            if (Kind != HoleKind.Dowel)
            {
                OnPropertyChanged();
                return;
            }

            Change(() => Set(ref _cboreDepth, value, nameof(CounterboreDepth)));
        }
    }

    /// <summary>A dowel's countersink diameter, in the units' length. Null empties the box.</summary>
    public decimal? CountersinkDiameter
    {
        get => _csinkDiameter;
        set
        {
            if (Kind != HoleKind.Dowel)
            {
                OnPropertyChanged();
                return;
            }

            Change(() => Set(ref _csinkDiameter, value, nameof(CountersinkDiameter)));
        }
    }

    /// <summary>A dowel's countersink included angle, in degrees. Null empties the box.</summary>
    public decimal? CountersinkAngle
    {
        get => _csinkAngle;
        set
        {
            if (Kind != HoleKind.Dowel)
            {
                OnPropertyChanged();
                return;
            }

            Change(() => Set(ref _csinkAngle, value, nameof(CountersinkAngle)));
        }
    }

    /// <summary>The sizes of the tab for the kind, seat, fastener and units, in workbook order.</summary>
    public IReadOnlyList<HoleOption<string>> SizeOptions => _sizeOptions;

    public string? Size
    {
        get => _size;
        set => SetFromList(_sizeOptions, value, nameof(Size), v => Set(ref _size, v, nameof(Size)));
    }

    /// <summary>Close, Normal, Loose for a clearance hole with no seat; Slip, Normal, Press for a dowel; else empty.</summary>
    public IReadOnlyList<HoleFitOption> FitOptions => _fitOptions;

    public bool ShowsFit => _fitOptions.Count > 0;

    public string? Fit
    {
        get => _fit;
        set
        {
            if (_fitOptions.FirstOrDefault(o => SameText(o.Value, value)) is not { } fit)
            {
                OnPropertyChanged();
                return;
            }

            Change(() => Set(ref _fit, fit.Value, nameof(Fit)));
        }
    }

    /// <summary>True for a dowel or precision hole, whose faces take the fit colour.</summary>
    public bool ShowsFitColour => Kind == HoleKind.Dowel;

    /// <summary>The selected fit's colour as <c>#RRGGBB</c> for a dowel hole, else null.</summary>
    public string? FitSwatch => ShowsFitColour ? _fitOptions.FirstOrDefault(o => SameText(o.Value, _fit))?.Swatch : null;

    /// <summary>The selected fit's name beside its swatch, for example <c>Press fit</c>; null unless coloured.</summary>
    public string? FitColourText => ShowsFitColour && _fit is { } fit ? fit + " fit" : null;

    /// <summary>
    /// The Precision Tol rows for the units and fit, each with its tolerance at the chosen size (task 098); empty unless
    /// the hole is a dowel. Rebuilt when the size, units or fit change; the selection is kept.
    /// </summary>
    public IReadOnlyList<HoleToleranceOption> ToleranceOptions => _toleranceOptions;

    public bool ShowsTolerance => Kind == HoleKind.Dowel;

    public string? Tolerance
    {
        get => _tolerance;
        set
        {
            if (_toleranceOptions.FirstOrDefault(o => SameText(o.Value, value)) is not { } found)
            {
                OnPropertyChanged();
                return;
            }

            Change(() => Set(ref _tolerance, found.Value, nameof(Tolerance)));
        }
    }

    /// <summary>True for a tapped hole ending at a distance; through holes are threaded the full depth.</summary>
    public bool ShowsThreadDepths => Kind == HoleKind.Tapped && IsBlind;

    /// <summary>Tapped holes: the thread depth, in the units' length. Null puts back the Tapped row's default.</summary>
    public decimal? ThreadDepth
    {
        get => _threadDepth;
        set
        {
            if (Kind != HoleKind.Tapped)
            {
                OnPropertyChanged();
                return;
            }

            Change(() => Set(ref _threadDepth, value ?? TappedRow()?.ThreadDepth, nameof(ThreadDepth)));
        }
    }

    /// <summary>Tapped holes: the drill depth, in the units' length. Null puts back the Tapped row's default.</summary>
    public decimal? DrillDepth
    {
        get => _drillDepth;
        set
        {
            if (Kind != HoleKind.Tapped)
            {
                OnPropertyChanged();
                return;
            }

            Change(() => Set(ref _drillDepth, value ?? TappedRow()?.DrillDepth, nameof(DrillDepth)));
        }
    }

    /// <summary>
    /// Distance and Through All; To as well only for an edited hole that ends at a face, since the window cannot pick
    /// the end face yet (task 090).
    /// </summary>
    public IReadOnlyList<HoleOption<HoleTermination>> TerminationOptions { get; }

    /// <summary><see cref="ToFaceNoteText"/> while the hole ends at a face; else null.</summary>
    public string? TerminationNote => _termination == HoleTermination.ToFace ? ToFaceNoteText : null;

    public HoleTermination Termination
    {
        get => _termination;
        set
        {
            if (!TerminationOptions.Any(o => o.Value == value))
            {
                OnPropertyChanged();
                return;
            }

            Change(() => Set(ref _termination, value, nameof(Termination)));
        }
    }

    /// <summary>True for a clearance or dowel hole ending at a distance.</summary>
    public bool ShowsDepth => Kind != HoleKind.Tapped && IsBlind;

    /// <summary>
    /// Clearance and dowel holes ending at a distance: the depth, in the units' length; converted when the units
    /// change, and kept while the hole is through. Starts at the last depth used, else the blind depth multiplier x D
    /// (see the remarks). A number set here is kept; null empties it until the next change the rule follows.
    /// </summary>
    public decimal? Depth
    {
        get => _depth;
        set => Change(() =>
        {
            _depthKept = value is not null;
            Set(ref _depth, value, nameof(Depth));
        });
    }

    // The depth boxes. The window binds these texts, not the numbers, so text that is not a number is kept in the box
    // with a message beside it, and the number keeps its last value (task 090). With an expression checker, text that
    // is not a plain number is read as Inventor reads it (task 100): a length with a unit, the part's parameters, or an
    // expression of them. The box then shows the expression, the number holds its value in the units' length, and the
    // spec carries both, so every check uses the value and the add-in writes the expression.

    /// <summary>The length unit of the units chosen, <c>mm</c> or <c>in</c>, beside the depth boxes.</summary>
    public string LengthUnitText => _units == HoleUnits.Imperial ? "in" : "mm";

    /// <summary><see cref="ThreadDepth"/> as text; blank puts back the Tapped row's default.</summary>
    public string ThreadDepthText
    {
        get => TextOf(HoleField.ThreadDepth, _threadDepth);
        set => SetText(HoleField.ThreadDepth, value, nameof(ThreadDepthText), v => ThreadDepth = v);
    }

    /// <summary><see cref="DrillDepth"/> as text; blank puts back the Tapped row's default.</summary>
    public string DrillDepthText
    {
        get => TextOf(HoleField.DrillDepth, _drillDepth);
        set => SetText(HoleField.DrillDepth, value, nameof(DrillDepthText), v => DrillDepth = v);
    }

    /// <summary><see cref="Depth"/> as text; blank clears it.</summary>
    public string DepthText
    {
        get => TextOf(HoleField.Depth, _depth);
        set => SetText(HoleField.Depth, value, nameof(DepthText), v => Depth = v);
    }

    /// <summary><see cref="CounterboreDiameter"/> as text: a number in the units' length; blank empties it.</summary>
    public string CounterboreDiameterText
    {
        get => TextOf(HoleField.CounterboreDiameter, _cboreDiameter);
        set => SetText(HoleField.CounterboreDiameter, value, nameof(CounterboreDiameterText), v => CounterboreDiameter = v);
    }

    /// <summary>
    /// <see cref="CounterboreDepth"/> as text: a number, or an Inventor expression as the depth takes; blank empties it.
    /// </summary>
    public string CounterboreDepthText
    {
        get => TextOf(HoleField.CounterboreDepth, _cboreDepth);
        set => SetText(HoleField.CounterboreDepth, value, nameof(CounterboreDepthText), v => CounterboreDepth = v);
    }

    /// <summary><see cref="CountersinkDiameter"/> as text: a number in the units' length; blank empties it.</summary>
    public string CountersinkDiameterText
    {
        get => TextOf(HoleField.CountersinkDiameter, _csinkDiameter);
        set => SetText(HoleField.CountersinkDiameter, value, nameof(CountersinkDiameterText), v => CountersinkDiameter = v);
    }

    /// <summary><see cref="CountersinkAngle"/> as text: a number of degrees; blank empties it.</summary>
    public string CountersinkAngleText
    {
        get => TextOf(HoleField.CountersinkAngle, _csinkAngle);
        set => SetText(HoleField.CountersinkAngle, value, nameof(CountersinkAngleText), v => CountersinkAngle = v);
    }

    // What an expression comes to (task 104; mockup choice 6): after the unit of a depth box holding an expression, in
    // muted text, for example "= 31 mm". Null for a plain number, an empty box and text Inventor cannot read.

    /// <summary>The value of the depth box's expression, <c>= 31 mm</c>; null unless the box holds one.</summary>
    public string? DepthValueText => ValueTextOf(HoleField.Depth, _depth);

    /// <summary>The value of the thread depth box's expression; null unless the box holds one.</summary>
    public string? ThreadDepthValueText => ValueTextOf(HoleField.ThreadDepth, _threadDepth);

    /// <summary>The value of the drill depth box's expression; null unless the box holds one.</summary>
    public string? DrillDepthValueText => ValueTextOf(HoleField.DrillDepth, _drillDepth);

    /// <summary>The value of a dowel's counterbore depth box's expression; null unless the box holds one.</summary>
    public string? CounterboreDepthValueText => ValueTextOf(HoleField.CounterboreDepth, _cboreDepth);

    /// <summary>
    /// True while a depth box holds text that is not a number, or an expression Inventor cannot read as a length; OK is
    /// disabled until it is fixed.
    /// </summary>
    public bool HasTextThatIsNotANumber => _invalidText.Count > 0;

    /// <summary>The Drill Point rows; empty unless the hole ends at a distance.</summary>
    public IReadOnlyList<HoleOption<string>> DrillPointOptions => _drillPointOptions;

    public bool ShowsDrillPoint => IsBlind;

    public string? DrillPoint
    {
        get => _drillPoint;
        set => SetFromList(_drillPointOptions, value, nameof(DrillPoint), v => Set(ref _drillPoint, v, nameof(DrillPoint)));
    }

    /// <summary>Direction: flipped, as Inventor's Flip.</summary>
    public bool Flip
    {
        get => _flip;
        set => Change(() => Set(ref _flip, value, nameof(Flip)));
    }

    // The diagram (task 104).

    /// <summary>The values the diagram is drawn from, in the window's units, as they stand after the last change.</summary>
    public HoleDiagramInput DiagramInput => _diagramInput;

    /// <summary>
    /// The diagram of the hole as the fields stand (<see cref="HoleDiagram.Draw"/>), in a box of
    /// <see cref="HoleDiagram.DefaultWidth"/> by <see cref="HoleDiagram.DefaultHeight"/>. Redrawn on every change.
    /// </summary>
    public HoleDiagram Diagram => _diagram ??= HoleDiagram.Draw(_diagramInput);

    /// <summary>The line the collapsed diagram shows (<see cref="HoleDiagram.Summary"/>).</summary>
    public string DiagramSummary => HoleDiagram.Summary(_diagramInput);

    /// <summary>
    /// Whether the diagram is open. True to start; the add-in sets it from the settings before the window shows and
    /// keeps it when it changes (<see cref="HoleDiagramState"/>). Not a field of the hole: it raises no preview.
    /// </summary>
    public bool IsDiagramExpanded
    {
        get => _isDiagramExpanded;
        set => SetProperty(ref _isDiagramExpanded, value);
    }

    // What the add-in uses.

    /// <summary>The number of picked hole centres, set by the add-in. OK needs at least one.</summary>
    public int CentreCount
    {
        get => _centreCount;
        set
        {
            var count = Math.Max(0, value);
            if (SetProperty(ref _centreCount, count))
            {
                Reevaluate();
            }
        }
    }

    /// <summary>The picked centres for the window: <c>None picked</c>, <c>1 picked</c>, <c>3 picked</c>.</summary>
    public string CentresText => _centreCount switch
    {
        0 => "None picked",
        var n => $"{n} picked",
    };

    /// <summary>
    /// False, set by the add-in, for an edited hole whose centres are not sketch points: clicks in the model do
    /// nothing, and <see cref="CentresHelp"/> says so (task 089).
    /// </summary>
    public bool CanPickCentres
    {
        get => _canPickCentres;
        set
        {
            if (SetProperty(ref _canPickCentres, value))
            {
                OnPropertyChanged(nameof(CentresHelp));
            }
        }
    }

    /// <summary>How to pick centres, or <see cref="CentresNotPickableText"/> when they cannot be picked.</summary>
    public string CentresHelp => _canPickCentres ? CentresHelpText : CentresNotPickableText;

    /// <summary>The spec the fields give, normalised.</summary>
    public HoleSpec Spec => _spec;

    /// <summary>
    /// What <see cref="HoleRules.Build"/> gives for <see cref="Spec"/>, worked out for at least one centre so the
    /// geometry is there before centres are picked. Store its <see cref="HoleBuildResult.Resolved"/> spec on OK.
    /// </summary>
    public HoleBuildResult BuildResult => _build;

    /// <summary>What the add-in previews and builds; null while <see cref="Checks"/> (other than the centres) has problems.</summary>
    public HoleGeometry? Geometry => _build.Geometry;

    /// <summary>Every check that fails, the centres included, in the order <see cref="HoleRules"/> gives them.</summary>
    public IReadOnlyList<HoleCheck> Checks => _checks;

    public bool HasProblems => _checks.Count > 0;

    /// <summary>The messages for one field, one per line, or null.</summary>
    public string? ProblemFor(HoleField field)
    {
        var messages = _checks.Where(c => c.Field == field).Select(c => c.Message).ToList();
        return messages.Count == 0 ? null : string.Join(Environment.NewLine, messages);
    }

    public string? CentresProblem => ProblemFor(HoleField.Centres);

    public string? SeatProblem => ProblemFor(HoleField.Seat);

    public string? FastenerProblem => ProblemFor(HoleField.Fastener);

    public string? SizeProblem => ProblemFor(HoleField.Size);

    public string? ToleranceProblem => ProblemFor(HoleField.Tolerance);

    /// <summary>Why the box's text is not a length while it is not one, else the field's checks.</summary>
    public string? ThreadDepthProblem => NotANumber(HoleField.ThreadDepth) ?? ProblemFor(HoleField.ThreadDepth);

    /// <summary>Why the box's text is not a length while it is not one, else the field's checks.</summary>
    public string? DrillDepthProblem => NotANumber(HoleField.DrillDepth) ?? ProblemFor(HoleField.DrillDepth);

    /// <summary>Why the box's text is not a length while it is not one, else the field's checks.</summary>
    public string? DepthProblem => NotANumber(HoleField.Depth) ?? ProblemFor(HoleField.Depth);

    public string? DrillPointProblem => ProblemFor(HoleField.DrillPoint);

    /// <summary>Why the box's text is not a number while it is not one, else the field's checks.</summary>
    public string? CounterboreDiameterProblem =>
        NotANumber(HoleField.CounterboreDiameter) ?? ProblemFor(HoleField.CounterboreDiameter);

    /// <summary>Why the box's text is not a length while it is not one, else the field's checks.</summary>
    public string? CounterboreDepthProblem =>
        NotANumber(HoleField.CounterboreDepth) ?? ProblemFor(HoleField.CounterboreDepth);

    /// <summary>Why the box's text is not a number while it is not one, else the field's checks.</summary>
    public string? CountersinkDiameterProblem =>
        NotANumber(HoleField.CountersinkDiameter) ?? ProblemFor(HoleField.CountersinkDiameter);

    /// <summary>Why the box's text is not a number while it is not one, else the field's checks.</summary>
    public string? CountersinkAngleProblem =>
        NotANumber(HoleField.CountersinkAngle) ?? ProblemFor(HoleField.CountersinkAngle);

    /// <summary>
    /// True when the spec builds, at least one centre is picked, and every depth box holds a number (or is blank).
    /// </summary>
    public bool CanOk => _build.Succeeded && _centreCount >= 1 && _invalidText.Count == 0;

    /// <summary>Raises <see cref="OkRequested"/>. Enabled when <see cref="CanOk"/>.</summary>
    public RelayCommand OkCommand { get; }

    /// <summary>Raises <see cref="CancelRequested"/>.</summary>
    public RelayCommand CancelCommand { get; }

    // Status.

    /// <summary>The status line: the workbook's problems at open, or <see cref="RefusedText"/>.</summary>
    public string StatusText => _statusText;

    public bool StatusIsError => _statusIsError;

    /// <summary>Every note behind the status, one per line, for its tooltip; null when there are none.</summary>
    public string? StatusDetails => _statusNotes.Count == 0 ? null : string.Join(Environment.NewLine, _statusNotes);

    public IReadOnlyList<string> StatusNotes => _statusNotes;

    public bool HasStatusDetails => _statusNotes.Count > 0;

    /// <summary>True from <see cref="ShowRefused"/> until the next user change or <see cref="ClearRefused"/>.</summary>
    public bool IsRefused => _isRefused;

    /// <summary>
    /// Inventor refused the last change (spike 081 round 6): the status says so, in the error style, with
    /// <paramref name="reason"/> as its note. Cleared by the next user change.
    /// </summary>
    public void ShowRefused(string? reason)
    {
        var why = (reason ?? "").Trim();
        _isRefused = true;
        OnPropertyChanged(nameof(IsRefused));
        SetStatus(RefusedText, true, why.Length == 0 ? [] : [why]);
    }

    /// <summary>The preview took the change: the refused status goes and the status at open comes back.</summary>
    public void ClearRefused()
    {
        if (!_isRefused)
        {
            return;
        }

        _isRefused = false;
        OnPropertyChanged(nameof(IsRefused));
        SetStatus(_openStatusText, false, _openStatusNotes);
    }

    /// <summary>
    /// The preview took the change, and the add-in has notes on the hole as it was made (task 088: the tap drill
    /// fallback, a hole made again at the end of the browser). The status line shows them, not as an error; its
    /// details list them first, then the workbook's problems. No notes puts back the status at open. A refused status
    /// is cleared either way. Call after each change Inventor took; the notes stay until the next one.
    /// </summary>
    public void ShowNotes(IEnumerable<string>? notes)
    {
        var list = (notes ?? [])
            .Select(n => (n ?? "").Trim())
            .Where(n => n.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (_isRefused)
        {
            _isRefused = false;
            OnPropertyChanged(nameof(IsRefused));
        }

        if (list.Count == 0)
        {
            if (_statusText != _openStatusText || _statusIsError || !_statusNotes.SequenceEqual(_openStatusNotes))
            {
                SetStatus(_openStatusText, false, _openStatusNotes);
            }

            return;
        }

        SetStatus(NotesText(list), false, list.Concat(_openStatusNotes).ToList());
    }

    /// <summary>The status line for the add-in's notes on the hole: the note itself, or how many there are.</summary>
    public static string NotesText(IReadOnlyList<string> notes)
    {
        ArgumentNullException.ThrowIfNull(notes);
        return notes.Count switch
        {
            0 => "",
            1 => notes[0],
            var n => $"{n} notes on this hole; see the details.",
        };
    }

    /// <summary>The status at open for <paramref name="problems"/> workbook problems; <c>""</c> for none.</summary>
    public static string WorkbookProblemsText(int problems) => problems switch
    {
        <= 0 => "",
        1 => "The hole workbook has 1 problem; see the details.",
        _ => $"The hole workbook has {problems} problems; see the details.",
    };

    // The cascade.

    private bool IsBlind => _termination == HoleTermination.Distance;

    private readonly record struct ListContext(
        HoleUnits Units, HoleKind Kind, HoleSeat Seat, string? Fastener, string? Fit, string? Size);

    private void SetFromList(IReadOnlyList<HoleOption<string>> options, string? value, string name, Action<string> set)
    {
        if (Find(options, value) is not { } found)
        {
            OnPropertyChanged(name);
            return;
        }

        Change(() => set(found));
    }

    /// <summary>Applies one user change, settles the fields after it, and raises one preview when the spec changed.</summary>
    private void Change(Action apply)
    {
        if (_changing)
        {
            return;
        }

        _changing = true;
        try
        {
            _invalidText.Clear();
            _invalidReason.Clear();
            apply();
            Refresh(keepExact: false);
            ClearRefused();
            Reevaluate();
        }
        finally
        {
            _changing = false;
        }

        if (_spec != _lastRaisedSpec)
        {
            _lastRaisedSpec = _spec;
            PreviewNeeded?.Invoke(this, EventArgs.Empty);
        }
    }

    private bool Set<T>(ref T field, T value, string name) => SetProperty(ref field, value, name);

    private void SetList<T>(ref IReadOnlyList<T> field, List<T> value, string name)
    {
        if (!field.SequenceEqual(value))
        {
            field = value.AsReadOnly();
            OnPropertyChanged(name);
        }
    }

    /// <summary>Settles every field below the one that changed, top to bottom.</summary>
    private void Refresh(bool keepExact)
    {
        // Standard.
        SetList(
            ref _standardOptions,
            HoleStandards.StandardsFor(_type).Select(s => new HoleOption<string>(s, s)).ToList(),
            nameof(StandardOptions));
        Set(ref _standard, Find(_standardOptions, _standard) ?? _standardOptions[0].Value, nameof(Standard));
        var kind = Kind;

        // Seat, Fastener type and Size. A clearance hole's sizes depend on its seat and fastener; a tapped or dowel
        // hole's seats and fasteners depend on its size, which comes from its own tab (task 099).
        if (kind == HoleKind.Clearance)
        {
            RefreshSeats(kind, keepExact);
            RefreshFasteners(kind, keepExact);
            RefreshSizes(kind, keepExact);
        }
        else
        {
            RefreshSizes(kind, keepExact);
            RefreshSeats(kind, keepExact);
            RefreshFasteners(kind, keepExact);
        }

        var seated = kind == HoleKind.Clearance && _seat != HoleSeat.None;

        // Fit.
        var fits = kind switch
        {
            HoleKind.Dowel => Enum.GetValues<PrecisionFit>()
                .Select(f => new HoleFitOption(
                    f.ToString(), f.ToString(), HoleFitOption.SwatchOf(_workbook.Settings.FitColour(f))))
                .ToList(),
            HoleKind.Clearance when !seated => Enum.GetValues<ClearanceFit>()
                .Select(f => new HoleFitOption(f.ToString(), f.ToString(), null))
                .ToList(),
            _ => [],
        };
        SetList(ref _fitOptions, fits, nameof(FitOptions));
        var fit = _fitOptions.FirstOrDefault(o => SameText(o.Value, _fit))
            ?? _fitOptions.FirstOrDefault(o => o.Value == NormalFitName)
            ?? _fitOptions.FirstOrDefault();
        Set(ref _fit, fit?.Value, nameof(Fit));

        // Tolerance.
        if (kind == HoleKind.Dowel && Enum.TryParse<PrecisionFit>(_fit, out var precisionFit))
        {
            var toleranceContext = new ListContext(_units, kind, HoleSeat.None, null, _fit, null);
            var keepTolerance = keepExact || toleranceContext == _toleranceContext;
            _toleranceContext = toleranceContext;
            var rows = _workbook.PrecisionTolerances.Where(r => r.Units == _units && r.Fit == precisionFit);
            SetList(
                ref _toleranceOptions,
                ToleranceOptionsFor(rows, keepTolerance ? _tolerance : null, Nominal(_units, kind, _size)),
                nameof(ToleranceOptions));
            Set(
                ref _tolerance,
                _toleranceOptions.FirstOrDefault(o => SameText(o.Value, _tolerance))?.Value
                    ?? HoleRules.FindTolerance(_workbook, _units, precisionFit, null)?.Name.Trim(),
                nameof(Tolerance));
        }
        else
        {
            _toleranceContext = null;
            SetList(ref _toleranceOptions, [], nameof(ToleranceOptions));
            Set(ref _tolerance, null, nameof(Tolerance));
        }

        // Thread and drill depths.
        if (kind == HoleKind.Tapped)
        {
            var depthsContext = new ListContext(_units, kind, HoleSeat.None, null, null, _size);
            var row = TappedRow();
            if (keepExact)
            {
                Set(ref _threadDepth, _threadDepth ?? row?.ThreadDepth, nameof(ThreadDepth));
                Set(ref _drillDepth, _drillDepth ?? row?.DrillDepth, nameof(DrillDepth));
            }
            else if (depthsContext != _depthsContext)
            {
                Set(ref _threadDepth, row?.ThreadDepth, nameof(ThreadDepth));
                Set(ref _drillDepth, row?.DrillDepth, nameof(DrillDepth));
            }

            _depthsContext = depthsContext;
        }
        else
        {
            _depthsContext = null;
            Set(ref _threadDepth, null, nameof(ThreadDepth));
            Set(ref _drillDepth, null, nameof(DrillDepth));
        }

        // A dowel's seat sizes: the size's start values when the units or size change, else kept (task 106).
        if (kind == HoleKind.Dowel)
        {
            var seatContext = new ListContext(_units, kind, HoleSeat.None, null, null, _size);
            var start = _lastDowelSeats.For(_units, _size).Or(HoleRules.DowelSeatStart(_workbook, _units, _size));
            if (keepExact)
            {
                // An edited dowel keeps its own values; the seat it does not have starts as a new one would.
                Set(ref _cboreDiameter, _cboreDiameter ?? start.CboreDiameter, nameof(CounterboreDiameter));
                Set(ref _cboreDepth, _cboreDepth ?? start.CboreDepth, nameof(CounterboreDepth));
                Set(ref _csinkDiameter, _csinkDiameter ?? start.CsinkDiameter, nameof(CountersinkDiameter));
                Set(ref _csinkAngle, _csinkAngle ?? start.CsinkAngleDeg, nameof(CountersinkAngle));
            }
            else if (seatContext != _dowelSeatContext)
            {
                _expressions.Remove(HoleField.CounterboreDepth);
                SetDowelSeat(start);
            }

            _dowelSeatContext = seatContext;
        }
        else
        {
            _dowelSeatContext = null;
            _expressions.Remove(HoleField.CounterboreDepth);
            SetDowelSeat(HoleDowelSeat.Empty);
        }

        // Depth: converted when the units change; kept when typed, else the blind depth rule.
        if (_depthUnits != _units)
        {
            if (_depth is { } depth)
            {
                Set(ref _depth, ConvertLength(depth, _depthUnits, _units), nameof(Depth));
            }

            _depthUnits = _units;
        }

        if (kind != HoleKind.Tapped && IsBlind)
        {
            var blindContext = new ListContext(_units, kind, HoleSeat.None, null, null, _size);
            if (!keepExact && !_depthKept && blindContext != _blindDepthContext
                && DefaultBlindDepth(kind) is { } defaultDepth)
            {
                Set(ref _depth, defaultDepth, nameof(Depth));
            }

            _blindDepthContext = blindContext;
        }
        else
        {
            _blindDepthContext = null;
        }

        // Drill point: its list depends on nothing above it, so a choice not in the workbook is always kept.
        if (IsBlind)
        {
            var points = _workbook.DrillPoints.Select(r => (r.Name, r.Hidden));
            SetList(
                ref _drillPointOptions,
                Options(points, _drillPoint).Select(o => o with { Icon = DrillPointIcon(o.Value) }).ToList(),
                nameof(DrillPointOptions));
            Set(
                ref _drillPoint,
                Find(_drillPointOptions, _drillPoint) ?? HoleRules.FindDrillPoint(_workbook, null)?.Name.Trim(),
                nameof(DrillPoint));
        }
        else
        {
            SetList(ref _drillPointOptions, [], nameof(DrillPointOptions));
            Set(ref _drillPoint, null, nameof(DrillPoint));
        }
    }

    /// <summary>
    /// A drill point's icon from its row's angle. A choice the workbook does not have (an edited hole's) is read from
    /// its name: a number at its start is the angle, else <c>Flat</c> in it is a flat bottom; anything else shows the
    /// 118 deg point.
    /// </summary>
    private string DrillPointIcon(string name) => HoleIcons.ForDrillPoint(DrillPointAngle(name));

    /// <summary>The angle of the drill point <paramref name="name"/> (see <see cref="DrillPointIcon"/>); null for flat.</summary>
    private decimal? DrillPointAngle(string? name)
    {
        var text = (name ?? "").Trim();
        var row = _workbook.DrillPoints.FirstOrDefault(r => SameText(r.Name, text));
        if (row is not null)
        {
            return row.AngleDeg;
        }

        var digits = new string(text.TakeWhile(c => char.IsDigit(c) || c == '.').ToArray());
        if (decimal.TryParse(digits, NumberStyles.Float, CultureInfo.InvariantCulture, out var angle) && angle > 0)
        {
            return angle;
        }

        return text.Contains("flat", StringComparison.OrdinalIgnoreCase) ? null : 118m;
    }

    private void SetDowelSeat(HoleDowelSeat seat)
    {
        Set(ref _cboreDiameter, seat.CboreDiameter, nameof(CounterboreDiameter));
        Set(ref _cboreDepth, seat.CboreDepth, nameof(CounterboreDepth));
        Set(ref _csinkDiameter, seat.CsinkDiameter, nameof(CountersinkDiameter));
        Set(ref _csinkAngle, seat.CsinkAngleDeg, nameof(CountersinkAngle));
    }

    /// <summary>
    /// The seats: all three for a clearance hole, and for a dowel, whose seat is sized in the window (task 106). A
    /// tapped hole lists None and each seat whose tab has a shown row of the same size
    /// (<see cref="HoleRules.SameSizeCounterbores"/>); an edited hole's own seat stays listed until the units, kind or
    /// size change, and <see cref="HoleRules"/> then says it cannot be kept when its tab has no row of the size. The
    /// seat shown is the one the user chose last when the list offers it, else None (task 123).
    /// </summary>
    private void RefreshSeats(HoleKind kind, bool keepExact)
    {
        var seatContext = new ListContext(_units, kind, HoleSeat.None, null, null, kind == HoleKind.Tapped ? _size : null);
        var keepSeat = keepExact || seatContext == _seatContext;
        _seatContext = seatContext;

        List<HoleOption<HoleSeat>> seats;
        if (kind != HoleKind.Tapped)
        {
            seats = AllSeats.ToList();
        }
        else
        {
            var nominal = HoleRules.SeatNominal(_workbook, _units, kind, _size);
            seats = AllSeats
                .Where(o => o.Value == HoleSeat.None || (keepSeat && o.Value == _seat)
                    || (nominal is { } n && SameSizeSeatRows(o.Value, n).Any(r => !r.Hidden)))
                .ToList();
        }

        SetList(ref _seatOptions, seats, nameof(SeatOptions));
        Set(ref _seat, _seatOptions.Any(o => o.Value == _wantedSeat) ? _wantedSeat : HoleSeat.None, nameof(Seat));
    }

    /// <summary>
    /// The Fastener type list: the Screw values of the seat's tab for the units (a clearance hole), or of its rows of
    /// the same size (a tapped hole); empty with no seat, and for a dowel (task 106).
    /// </summary>
    private void RefreshFasteners(HoleKind kind, bool keepExact)
    {
        var fastenerContext = new ListContext(
            _units, kind, _seat, null, null, kind == HoleKind.Clearance ? null : _size);
        var keepFastener = keepExact || fastenerContext == _fastenerContext;
        _fastenerContext = fastenerContext;
        if (_seat == HoleSeat.None || kind == HoleKind.Dowel)
        {
            SetList(ref _fastenerOptions, [], nameof(FastenerOptions));
            Set(ref _fastener, null, nameof(Fastener));
            return;
        }

        IEnumerable<(string Screw, bool Hidden)> screws;
        if (kind == HoleKind.Clearance)
        {
            screws = _seat == HoleSeat.Counterbore
                ? _workbook.Counterbore.Where(r => r.Units == _units).Select(r => (r.Screw, r.Hidden))
                : _workbook.Countersink.Where(r => r.Units == _units).Select(r => (r.Screw, r.Hidden));
        }
        else
        {
            screws = HoleRules.SeatNominal(_workbook, _units, kind, _size) is { } nominal
                ? SameSizeSeatRows(_seat, nominal)
                : [];
        }

        SetList(ref _fastenerOptions, Options(screws, keepFastener ? _fastener : null), nameof(FastenerOptions));
        Set(ref _fastener, Find(_fastenerOptions, _fastener) ?? FirstOf(_fastenerOptions), nameof(Fastener));
    }

    /// <summary>The size list for the kind (and a clearance hole's seat and fastener); a units switch keeps the nearest.</summary>
    private void RefreshSizes(HoleKind kind, bool keepExact)
    {
        var previousSizeContext = _sizeContext;
        var sizeContext = kind == HoleKind.Clearance
            ? new ListContext(_units, kind, _seat, _fastener, null, null)
            : new ListContext(_units, kind, HoleSeat.None, null, null, null);
        var keepSize = keepExact || sizeContext == previousSizeContext;
        _sizeContext = sizeContext;
        SetList(ref _sizeOptions, Options(SizeRows(sizeContext), keepSize ? _size : null), nameof(SizeOptions));
        var size = Find(_sizeOptions, _size);
        if (size is null && previousSizeContext is { } previous && previous.Units != _units)
        {
            size = Nearest(previous, _size, sizeContext);
        }

        Set(ref _size, size ?? FirstOf(_sizeOptions), nameof(Size));
    }

    /// <summary>The Screw and hidden flag of the seat tab's rows of the same size as <paramref name="nominal"/>.</summary>
    private IEnumerable<(string Screw, bool Hidden)> SameSizeSeatRows(HoleSeat seat, decimal nominal) => seat switch
    {
        HoleSeat.Counterbore => HoleRules.SameSizeCounterbores(_workbook, _units, nominal).Select(r => (r.Screw, r.Hidden)),
        HoleSeat.Countersink => HoleRules.SameSizeCountersinks(_workbook, _units, nominal).Select(r => (r.Screw, r.Hidden)),
        _ => [],
    };

    /// <summary>
    /// The options from workbook rows: shown rows, each key once (the first spelling), in workbook order. The
    /// <paramref name="keep"/> value is listed too, at its row's place when it is a hidden row and last when the
    /// workbook does not have it.
    /// </summary>
    private static List<HoleOption<string>> Options(IEnumerable<(string Key, bool Hidden)> rows, string? keep)
    {
        var options = new List<HoleOption<string>>();
        foreach (var (key, hidden) in rows)
        {
            var text = (key ?? "").Trim();
            if (text.Length == 0 || (hidden && !SameText(text, keep)) || options.Any(o => SameText(o.Value, text)))
            {
                continue;
            }

            options.Add(new HoleOption<string>(text, text));
        }

        if (!string.IsNullOrWhiteSpace(keep) && !options.Any(o => SameText(o.Value, keep)))
        {
            options.Add(new HoleOption<string>(keep.Trim(), keep.Trim()));
        }

        return options;
    }

    /// <summary>
    /// The Tolerance options from Precision Tol rows, as <see cref="Options"/> builds them (each name once, the first
    /// row wins, in workbook order, <paramref name="keep"/> last when the workbook does not have it), each with its
    /// tolerance at <paramref name="nominal"/> as the main text and its name as secondary text when that differs.
    /// </summary>
    private static List<HoleToleranceOption> ToleranceOptionsFor(
        IEnumerable<PrecisionToleranceRow> rows, string? keep, decimal? nominal)
    {
        var options = new List<HoleToleranceOption>();
        foreach (var row in rows)
        {
            var name = (row.Name ?? "").Trim();
            if (name.Length == 0 || options.Any(o => SameText(o.Value, name)))
            {
                continue;
            }

            var text = HoleToleranceOption.TextFor(row, nominal);
            options.Add(new HoleToleranceOption(name, text, SameText(text, name) ? null : name));
        }

        if (!string.IsNullOrWhiteSpace(keep) && !options.Any(o => SameText(o.Value, keep)))
        {
            options.Add(new HoleToleranceOption(keep.Trim(), keep.Trim(), null));
        }

        return options;
    }

    /// <summary>The size keys of the tab for a context, with their hidden flags.</summary>
    private IEnumerable<(string Key, bool Hidden)> SizeRows(ListContext context) => (context.Kind, context.Seat) switch
    {
        (HoleKind.Tapped, _) => _workbook.Tapped.Where(r => r.Units == context.Units).Select(r => (r.Thread, r.Hidden)),
        (HoleKind.Dowel, _) => _workbook.Dowel.Where(r => r.Units == context.Units).Select(r => (r.Size, r.Hidden)),
        (_, HoleSeat.Counterbore) => _workbook.Counterbore
            .Where(r => r.Units == context.Units && SameText(r.Screw, context.Fastener))
            .Select(r => (r.Size, r.Hidden)),
        (_, HoleSeat.Countersink) => _workbook.Countersink
            .Where(r => r.Units == context.Units && SameText(r.Screw, context.Fastener))
            .Select(r => (r.Size, r.Hidden)),
        _ => _workbook.Clearance.Where(r => r.Units == context.Units).Select(r => (r.Size, r.Hidden)),
    };

    /// <summary>A size's nominal diameter in centimetres; a seated size takes its Clearance row's. Null when not found.</summary>
    private double? NominalCm(ListContext context, string? size) =>
        Nominal(context.Units, context.Kind, size) is { } n ? HoleRules.ToCm(n, context.Units) : null;

    /// <summary>
    /// A size's nominal diameter in the units' length; a seated size takes its Clearance row's. Null when not found.
    /// </summary>
    private decimal? Nominal(HoleUnits units, HoleKind kind, string? size)
    {
        if (string.IsNullOrWhiteSpace(size))
        {
            return null;
        }

        return kind switch
        {
            HoleKind.Tapped => _workbook.Tapped
                .FirstOrDefault(r => r.Units == units && SameText(r.Thread, size))?.Nominal,
            HoleKind.Dowel => _workbook.Dowel
                .FirstOrDefault(r => r.Units == units && SameText(r.Size, size))?.Nominal,
            _ => _workbook.Clearance
                .FirstOrDefault(r => r.Units == units && SameText(r.Size, size))?.Nominal,
        };
    }

    /// <summary>
    /// The depth a blind clearance or dowel hole starts at: the last depth used in the units, else the blind depth
    /// multiplier x the size's nominal diameter. Null when neither is known.
    /// </summary>
    private decimal? DefaultBlindDepth(HoleKind kind) =>
        _lastDepths.For(_units)
        ?? (Nominal(_units, kind, _size) is { } nominal && nominal > 0
            ? _workbook.Settings.DefaultBlindDepth(nominal)
            : null);

    /// <summary>The size in the new list whose nominal is nearest the old size's; null when either is unknown.</summary>
    private string? Nearest(ListContext previous, string? previousSize, ListContext context)
    {
        if (NominalCm(previous, previousSize) is not { } target)
        {
            return null;
        }

        string? best = null;
        var bestDifference = double.MaxValue;
        foreach (var option in _sizeOptions)
        {
            if (NominalCm(context, option.Value) is { } nominal && Math.Abs(nominal - target) < bestDifference - 1e-9)
            {
                best = option.Value;
                bestDifference = Math.Abs(nominal - target);
            }
        }

        return best;
    }

    /// <summary>A length from one units' workbook length to the other's, to 3 decimals of a mm or 4 of an inch.</summary>
    private static decimal ConvertLength(decimal value, HoleUnits from, HoleUnits to)
    {
        if (from == to)
        {
            return value;
        }

        return to == HoleUnits.Imperial
            ? Math.Round(value / 25.4m, 4, MidpointRounding.AwayFromZero)
            : Math.Round(value * 25.4m, 3, MidpointRounding.AwayFromZero);
    }

    private TappedRow? TappedRow() =>
        _workbook.Tapped.FirstOrDefault(r => r.Units == _units && SameText(r.Thread, _size));

    // Checks and geometry.

    private (HoleSpec Spec, HoleBuildResult Build, IReadOnlyList<HoleCheck> Checks) Evaluate()
    {
        var kind = Kind;
        var spec = new HoleSpec(
            _units,
            kind,
            _seat,
            _size ?? "",
            _fastener,
            kind == HoleKind.Clearance && Enum.TryParse<ClearanceFit>(_fit, out var clearanceFit)
                ? clearanceFit
                : ClearanceFit.Normal,
            kind == HoleKind.Dowel && Enum.TryParse<PrecisionFit>(_fit, out var precisionFit)
                ? precisionFit
                : PrecisionFit.Normal,
            _tolerance,
            _threadDepth,
            _drillDepth,
            _termination,
            _depth,
            _drillPoint,
            _flip)
        {
            ThreadDepthExpression = ExpressionOf(HoleField.ThreadDepth, _threadDepth),
            DrillDepthExpression = ExpressionOf(HoleField.DrillDepth, _drillDepth),
            DepthExpression = ExpressionOf(HoleField.Depth, _depth),
            CboreDiameter = _cboreDiameter,
            CboreDepth = _cboreDepth,
            CboreDepthExpression = ExpressionOf(HoleField.CounterboreDepth, _cboreDepth),
            CsinkDiameter = _csinkDiameter,
            CsinkAngleDeg = _csinkAngle,
        }.Normalised();

        var build = HoleRules.Build(spec, _workbook, Math.Max(1, _centreCount));
        var checks = _centreCount >= 1
            ? build.Problems
            : HoleRules.Build(spec, _workbook, 0).Problems;
        return (spec, build, checks);
    }

    private static readonly string[] DerivedProperties =
    [
        nameof(Kind), nameof(ShowsFastener), nameof(ShowsFit), nameof(ShowsFitColour), nameof(FitSwatch),
        nameof(FitColourText), nameof(ShowsTolerance), nameof(ShowsThreadDepths), nameof(ShowsDepth),
        nameof(ShowsDrillPoint), nameof(Spec), nameof(BuildResult), nameof(Geometry), nameof(Checks),
        nameof(HasProblems), nameof(CentresProblem), nameof(SeatProblem), nameof(FastenerProblem), nameof(SizeProblem),
        nameof(ToleranceProblem), nameof(ThreadDepthProblem), nameof(DrillDepthProblem), nameof(DepthProblem),
        nameof(DrillPointProblem), nameof(CanOk), nameof(ThreadDepthText), nameof(DrillDepthText), nameof(DepthText),
        nameof(HasTextThatIsNotANumber), nameof(LengthUnitText), nameof(TerminationNote), nameof(CentresText),
        nameof(ShowsCounterboreSizes), nameof(ShowsCountersinkSizes), nameof(CounterboreDiameterText),
        nameof(CounterboreDepthText), nameof(CountersinkDiameterText), nameof(CountersinkAngleText),
        nameof(CounterboreDiameterProblem), nameof(CounterboreDepthProblem), nameof(CountersinkDiameterProblem),
        nameof(CountersinkAngleProblem), nameof(DepthValueText), nameof(ThreadDepthValueText),
        nameof(DrillDepthValueText), nameof(CounterboreDepthValueText), nameof(DiagramInput), nameof(Diagram),
        nameof(DiagramSummary),
    ];

    private void Reevaluate()
    {
        (_spec, _build, _checks) = Evaluate();
        _diagramInput = BuildDiagramInput();
        _diagram = null;
        foreach (var name in DerivedProperties)
        {
            OnPropertyChanged(name);
        }

        OkCommand.RaiseCanExecuteChanged();
    }

    /// <summary>
    /// The diagram's values from the fields: the hole and seat sizes as the hole is built when it builds (a clearance
    /// hole's diameter depends on its fit and seat), else from the workbook row; a dowel's seat from its boxes; the
    /// depths as they stand, marked when typed as expressions; a dowel's tolerance as its ISO code or its limits.
    /// </summary>
    private HoleDiagramInput BuildDiagramInput()
    {
        var kind = Kind;
        var geometry = _build.Geometry;
        decimal? Length(double? cm) => cm is { } c && double.IsFinite(c) ? HoleReadBack.FromCm(c, _units) : null;

        var tapped = kind == HoleKind.Tapped ? TappedRow() : null;
        var holeDiameter = geometry is not null
            ? Length(geometry.HoleDiameterCm)
            : kind == HoleKind.Tapped ? tapped?.TapDrill : Nominal(_units, kind, _size);

        decimal? cboreDiameter, cboreDepth, csinkDiameter, csinkAngle;
        if (kind == HoleKind.Dowel)
        {
            (cboreDiameter, cboreDepth, csinkDiameter, csinkAngle) = (_cboreDiameter, _cboreDepth, _csinkDiameter, _csinkAngle);
        }
        else
        {
            cboreDiameter = Length(geometry?.CounterboreDiameterCm);
            cboreDepth = Length(geometry?.CounterboreDepthCm);
            csinkDiameter = Length(geometry?.CountersinkDiameterCm);
            csinkAngle = geometry?.CountersinkAngleDeg is { } angle && double.IsFinite(angle)
                ? Math.Round((decimal)angle, 4)
                : null;
        }

        string? tolerance = null;
        if (kind == HoleKind.Dowel && Enum.TryParse<PrecisionFit>(_fit, out var fit)
            && _workbook.PrecisionTolerances.FirstOrDefault(
                r => r.Units == _units && r.Fit == fit && SameText(r.Name, _tolerance)) is { } row)
        {
            tolerance = row.Kind == ToleranceKind.Limits
                ? row.Upper is { } upper && row.Lower is { } lower
                    ? HoleToleranceOption.LimitsText(upper, lower, row.Units)
                    : null
                : string.IsNullOrWhiteSpace(row.IsoCode) ? null : row.IsoCode.Trim();
        }

        var depthField = kind == HoleKind.Tapped ? HoleField.DrillDepth : HoleField.Depth;
        var depth = kind == HoleKind.Tapped ? _drillDepth : _depth;
        return new HoleDiagramInput
        {
            Units = _units,
            Kind = kind,
            Seat = _seat,
            Termination = _termination,
            HoleDiameter = holeDiameter,
            Tolerance = tolerance,
            Depth = depth,
            DepthIsExpression = ExpressionOf(depthField, depth) is not null,
            PointAngleDeg = DrillPointAngle(_drillPoint),
            CounterboreDiameter = cboreDiameter,
            CounterboreDepth = cboreDepth,
            CounterboreDepthIsExpression =
                kind == HoleKind.Dowel && ExpressionOf(HoleField.CounterboreDepth, _cboreDepth) is not null,
            CountersinkDiameter = csinkDiameter,
            CountersinkAngleDeg = csinkAngle,
            ThreadName = kind == HoleKind.Tapped ? geometry?.Thread?.Designation ?? _size : null,
            ThreadDiameter = tapped?.Nominal,
            ThreadDepth = kind == HoleKind.Tapped ? _threadDepth : null,
            ThreadDepthIsExpression = kind == HoleKind.Tapped && ExpressionOf(HoleField.ThreadDepth, _threadDepth) is not null,
        };
    }

    /// <summary>
    /// <c>= value unit</c> for a box holding an expression Inventor read, the value as the diagram shows lengths; null
    /// for a plain number, an empty box, or text that is not a length.
    /// </summary>
    private string? ValueTextOf(HoleField field, decimal? value) =>
        !_invalidText.ContainsKey(field) && value is { } v && ExpressionOf(field, value) is not null
            ? $"= {HoleDiagram.FormatLength(v, _units)} {LengthUnitText}"
            : null;

    private void SetStatus(string text, bool isError, IReadOnlyList<string> notes)
    {
        _statusText = text;
        _statusIsError = isError;
        _statusNotes = notes.ToList().AsReadOnly();
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(StatusIsError));
        OnPropertyChanged(nameof(StatusDetails));
        OnPropertyChanged(nameof(StatusNotes));
        OnPropertyChanged(nameof(HasStatusDetails));
    }

    private string TextOf(HoleField field, decimal? value) =>
        _invalidText.TryGetValue(field, out var text) ? text : ExpressionOf(field, value) ?? FormatNumber(value);

    /// <summary>The expression <paramref name="value"/> was typed as, while the depth still has the value it came to.</summary>
    private string? ExpressionOf(HoleField field, decimal? value) =>
        value is { } v && _expressions.TryGetValue(field, out var expression) && expression.Value == v
            ? expression.Text
            : null;

    /// <summary>
    /// An edited hole's depth expression: checked again when there is a checker, since the part's parameters may have
    /// changed since the hole was made, so the depth takes the value it comes to now; shown as text that is not a
    /// length when Inventor can no longer read it. Without a checker the stored value stands.
    /// </summary>
    private void OpenExpression(HoleField field, string? expression, ref decimal? value)
    {
        if (HoleExpressions.Clean(expression) is not { } text || value is null)
        {
            return;
        }

        if (_expressionChecker is null)
        {
            _expressions[field] = (text, value.Value);
            return;
        }

        var check = _expressionChecker.Check(text, _units);
        if (check is { IsValid: true, ValueCm: { } cm })
        {
            value = HoleReadBack.FromCm(cm, _units);
            _expressions[field] = (text, value.Value);
        }
        else
        {
            _invalidText[field] = text;
            if (check.Reason is { } reason)
            {
                _invalidReason[field] = reason;
            }
        }
    }

    /// <summary>A depth for its box: up to six decimals, no trailing zeros, in the user's number format.</summary>
    public static string FormatNumber(decimal? value) =>
        value is { } v ? v.ToString("0.######", CultureInfo.CurrentCulture) : "";

    /// <summary>
    /// A number typed in a depth box, in the user's number format or with a point as the decimal separator. No
    /// thousands separators, so <c>1,5</c> is not read as 15 where the comma groups thousands.
    /// </summary>
    public static bool TryParseNumber(string? text, out decimal value)
    {
        var trimmed = (text ?? "").Trim();
        value = 0m;
        return trimmed.Length > 0
            && (decimal.TryParse(trimmed, NumberStyles.Float, CultureInfo.CurrentCulture, out value)
                || decimal.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out value));
    }

    private void SetText(HoleField field, string? text, string textProperty, Action<decimal?> set)
    {
        var raw = text ?? "";
        var trimmed = raw.Trim();
        decimal? value = null;
        string? expression = null;
        if (trimmed.Length > 0)
        {
            if (TryParseNumber(raw, out var parsed))
            {
                value = parsed;
            }
            else
            {
                // The diameters and the angle take numbers only (task 106); the depths take expressions too.
                var check = TakesExpressions(field)
                    ? _expressionChecker?.Check(trimmed, _units) ?? HoleExpressionCheck.Invalid()
                    : HoleExpressionCheck.Invalid();
                if (check is not { IsValid: true, ValueCm: { } cm })
                {
                    _invalidText[field] = raw;
                    _invalidReason.Remove(field);
                    if (check.Reason is { } reason)
                    {
                        _invalidReason[field] = reason;
                    }

                    OnPropertyChanged(textProperty);
                    OnPropertyChanged(ProblemPropertyOf(field));
                    OnPropertyChanged(ValueTextPropertyOf(field));
                    OnPropertyChanged(nameof(HasTextThatIsNotANumber));
                    OnPropertyChanged(nameof(CanOk));
                    OkCommand.RaiseCanExecuteChanged();
                    return;
                }

                value = HoleReadBack.FromCm(cm, _units);
                expression = trimmed;
            }
        }

        var wasInvalid = _invalidText.Remove(field);
        _invalidReason.Remove(field);
        if (expression is not null && value is { } typed)
        {
            _expressions[field] = (expression, typed);
        }
        else
        {
            _expressions.Remove(field);
        }

        set(value);

        // The number may not have changed ("12.0" for 12, or the old text was not a number): show it as it is.
        OnPropertyChanged(textProperty);
        if (wasInvalid)
        {
            OnPropertyChanged(ProblemPropertyOf(field));
            OnPropertyChanged(nameof(HasTextThatIsNotANumber));
            OnPropertyChanged(nameof(CanOk));
            OkCommand.RaiseCanExecuteChanged();
        }
    }

    /// <summary>
    /// Why the box's text is not a length: Inventor's reason when it gave one, else
    /// <see cref="HoleExpressions.NotALengthText"/> when there is a checker to read expressions, else
    /// <see cref="NotANumberText"/>. Null while the text is a length.
    /// </summary>
    private string? NotANumber(HoleField field) =>
        !_invalidText.ContainsKey(field) ? null
        : _invalidReason.TryGetValue(field, out var reason) ? reason
        : _expressionChecker is null || !TakesExpressions(field) ? NotANumberText
        : HoleExpressions.NotALengthText;

    /// <summary>The boxes that take Inventor expressions: the depths (task 100) and a dowel's counterbore depth (task 106).</summary>
    private static bool TakesExpressions(HoleField field) =>
        field is HoleField.ThreadDepth or HoleField.DrillDepth or HoleField.Depth or HoleField.CounterboreDepth;

    private static string ValueTextPropertyOf(HoleField field) => field switch
    {
        HoleField.ThreadDepth => nameof(ThreadDepthValueText),
        HoleField.DrillDepth => nameof(DrillDepthValueText),
        HoleField.CounterboreDepth => nameof(CounterboreDepthValueText),
        _ => nameof(DepthValueText),
    };

    private static string ProblemPropertyOf(HoleField field) => field switch
    {
        HoleField.ThreadDepth => nameof(ThreadDepthProblem),
        HoleField.DrillDepth => nameof(DrillDepthProblem),
        HoleField.CounterboreDiameter => nameof(CounterboreDiameterProblem),
        HoleField.CounterboreDepth => nameof(CounterboreDepthProblem),
        HoleField.CountersinkDiameter => nameof(CountersinkDiameterProblem),
        HoleField.CountersinkAngle => nameof(CountersinkAngleProblem),
        _ => nameof(DepthProblem),
    };

    private static string? Find(IReadOnlyList<HoleOption<string>> options, string? value) =>
        options.FirstOrDefault(o => SameText(o.Value, value))?.Value;

    private static string? FirstOf(IReadOnlyList<HoleOption<string>> options) => options.FirstOrDefault()?.Value;

    private static bool SameText(string? a, string? b) =>
        a is not null && b is not null
        && string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
}
