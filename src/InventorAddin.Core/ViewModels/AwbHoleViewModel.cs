using System.Globalization;
using InventorAddin.Core.Holes;

namespace InventorAddin.Core.ViewModels;

/// <summary>
/// One entry of an AWB Hole dropdown: the value the view-model takes and the text the window shows. Bind the
/// dropdown's items to the options and its selected value (<c>SelectedValuePath="Value"</c>) to the field.
/// </summary>
public sealed record HoleOption<T>(T Value, string Text);

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
/// with the nearest nominal diameter in the other units, else the first. Changing the Type puts the seat back to None
/// (spike 081 round 5: a seat is kept unless the user changes the type).
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
        new(HoleStandards.ClearanceType, HoleStandards.ClearanceType),
        new(HoleStandards.TappedType, HoleStandards.TappedType),
    ];

    // Termination To needs the end face picked, which the window cannot do yet (task 090): To is listed only for an
    // edited hole that already ends at a face, so the window shows what the hole is.
    private static readonly IReadOnlyList<HoleOption<HoleTermination>> PickableTerminations =
    [
        new(HoleTermination.Distance, DistanceText),
        new(HoleTermination.ThroughAll, ThroughAllText),
    ];

    private static readonly IReadOnlyList<HoleOption<HoleTermination>> TerminationsWithTo =
    [
        new(HoleTermination.Distance, DistanceText),
        new(HoleTermination.ThroughAll, ThroughAllText),
        new(HoleTermination.ToFace, ToText),
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

    /// <summary>The message when the command is started in a document that is not a part.</summary>
    public const string WrongDocumentText = CommandName + " works in part files. Open a part and try again.";

    /// <summary>When the hole workbook gives nothing to make a hole from and no reason was reported.</summary>
    public const string NoHoleSizesText =
        "The hole workbook has no hole sizes. Check the hole data in Settings.";

    private static readonly IReadOnlyList<HoleOption<HoleSeat>> AllSeats =
    [
        new(HoleSeat.None, NoneText),
        new(HoleSeat.Counterbore, CounterboreText),
        new(HoleSeat.Countersink, CountersinkText),
    ];

    private static readonly IReadOnlyList<HoleOption<HoleSeat>> NoSeat = [new(HoleSeat.None, NoneText)];

    private readonly HoleWorkbook _workbook;
    private readonly string _partName;
    private readonly HoleLastDepths _lastDepths;

    // The fields, in window order.
    private HoleUnits _units;
    private string _type = HoleStandards.ClearanceType;
    private HoleSeat _seat;
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

    // The lists that depend on other fields.
    private IReadOnlyList<HoleOption<HoleSeat>> _seatOptions = AllSeats;
    private IReadOnlyList<HoleOption<string>> _standardOptions = [];
    private IReadOnlyList<HoleOption<string>> _fastenerOptions = [];
    private IReadOnlyList<HoleOption<string>> _sizeOptions = [];
    private IReadOnlyList<HoleFitOption> _fitOptions = [];
    private IReadOnlyList<HoleOption<string>> _toleranceOptions = [];
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

    // Depth box text that is not a number, by field; cleared by every change that reaches the fields.
    private readonly Dictionary<HoleField, string> _invalidText = [];

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
        HoleLastDepths? lastDepths)
    {
        ArgumentNullException.ThrowIfNull(workbook);

        _workbook = workbook;
        _partName = (partName ?? "").Trim();
        _lastDepths = lastDepths ?? HoleLastDepths.None;
        IsEdit = editSpec is not null;
        IsInventorHole = madeByInventor;
        InventorHoleNotes = (inventorHoleNotes ?? [])
            .Select(n => (n ?? "").Trim())
            .Where(n => n.Length > 0)
            .ToList()
            .AsReadOnly();
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
            _depthUnits = _units;
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
        _lastRaisedSpec = _spec;
    }

    /// <summary>A new hole: the workbook's default units, a clearance hole with no seat, through all.</summary>
    /// <param name="workbook">The hole workbook as read (task 082).</param>
    /// <param name="partName">The part's file name, for the header subline.</param>
    /// <param name="problems">The workbook's problems; the status line counts them at open, not as an error.</param>
    /// <param name="lastDepths">The last blind depth used in each units; a blind hole starts at it.</param>
    public AwbHoleViewModel(
        HoleWorkbook workbook,
        string partName,
        IEnumerable<HoleWorkbookProblem>? problems = null,
        HoleLastDepths? lastDepths = null)
        : this(workbook, partName, null, false, null, problems, lastDepths)
    {
    }

    /// <summary>
    /// Edit mode: the window opens on <paramref name="spec"/>, the hole's stored spec, exactly as it is, before the
    /// first preview (spike 081 round 5).
    /// </summary>
    public static AwbHoleViewModel ForEdit(
        HoleWorkbook workbook,
        string partName,
        HoleSpec spec,
        IEnumerable<HoleWorkbookProblem>? problems = null,
        HoleLastDepths? lastDepths = null)
    {
        ArgumentNullException.ThrowIfNull(spec);
        return new AwbHoleViewModel(workbook, partName, spec, false, null, problems, lastDepths);
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
        HoleLastDepths? lastDepths = null)
    {
        ArgumentNullException.ThrowIfNull(closestSpec);
        return new AwbHoleViewModel(workbook, partName, closestSpec, true, notes, problems, lastDepths);
    }

    /// <summary>
    /// Edit mode from the add-in's read-back of a hole (task 088, <c>HoleFeatureReader.ReadSpec</c>): an AWB hole opens
    /// on its stored spec (<see cref="ForEdit"/>), any other hole on the closest spec with the read-back's notes
    /// (<see cref="ForInventorHole"/>).
    /// </summary>
    public static AwbHoleViewModel ForReadBack(
        HoleWorkbook workbook,
        string partName,
        HoleReadBackResult readBack,
        IEnumerable<HoleWorkbookProblem>? problems = null,
        HoleLastDepths? lastDepths = null)
    {
        ArgumentNullException.ThrowIfNull(readBack);
        return readBack.IsInventorHole
            ? ForInventorHole(workbook, partName, readBack.Spec, readBack.Notes, problems, lastDepths)
            : ForEdit(workbook, partName, readBack.Spec, problems, lastDepths);
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

    /// <summary>Clearance or Tapped. Changing it puts the seat back to None.</summary>
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

            Change(() =>
            {
                if (Set(ref _type, type, nameof(Type)))
                {
                    Set(ref _seat, HoleSeat.None, nameof(Seat));
                }
            });
        }
    }

    /// <summary>The seats for the standard: None, Counterbore, Countersink; None only for a dowel hole.</summary>
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

            Change(() => Set(ref _seat, value, nameof(Seat)));
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

    /// <summary>The Screw values of the seat's tab for the units, in workbook order; empty unless the hole is seated.</summary>
    public IReadOnlyList<HoleOption<string>> FastenerOptions => _fastenerOptions;

    public bool ShowsFastener => IsSeatedClearance;

    public string? Fastener
    {
        get => _fastener;
        set => SetFromList(_fastenerOptions, value, nameof(Fastener), v => Set(ref _fastener, v, nameof(Fastener)));
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

    /// <summary>The Precision Tol rows for the units and fit; empty unless the hole is a dowel.</summary>
    public IReadOnlyList<HoleOption<string>> ToleranceOptions => _toleranceOptions;

    public bool ShowsTolerance => Kind == HoleKind.Dowel;

    public string? Tolerance
    {
        get => _tolerance;
        set => SetFromList(_toleranceOptions, value, nameof(Tolerance), v => Set(ref _tolerance, v, nameof(Tolerance)));
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
    // with a message beside it, and the number keeps its last value (task 090).

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

    /// <summary>True while a depth box holds text that is not a number; OK is disabled until it is fixed.</summary>
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

    /// <summary><see cref="NotANumberText"/> while the box holds text that is not a number, else the field's checks.</summary>
    public string? ThreadDepthProblem => NotANumber(HoleField.ThreadDepth) ?? ProblemFor(HoleField.ThreadDepth);

    /// <summary><see cref="NotANumberText"/> while the box holds text that is not a number, else the field's checks.</summary>
    public string? DrillDepthProblem => NotANumber(HoleField.DrillDepth) ?? ProblemFor(HoleField.DrillDepth);

    /// <summary><see cref="NotANumberText"/> while the box holds text that is not a number, else the field's checks.</summary>
    public string? DepthProblem => NotANumber(HoleField.Depth) ?? ProblemFor(HoleField.Depth);

    public string? DrillPointProblem => ProblemFor(HoleField.DrillPoint);

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

    private bool IsSeatedClearance => Kind == HoleKind.Clearance && _seat != HoleSeat.None;

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

        // Seat.
        SetList(ref _seatOptions, (kind == HoleKind.Dowel ? NoSeat : AllSeats).ToList(), nameof(SeatOptions));
        if (!_seatOptions.Any(o => o.Value == _seat))
        {
            Set(ref _seat, HoleSeat.None, nameof(Seat));
        }

        var seated = kind == HoleKind.Clearance && _seat != HoleSeat.None;

        // Fastener type.
        var fastenerContext = new ListContext(_units, kind, _seat, null, null, null);
        var keepFastener = keepExact || fastenerContext == _fastenerContext;
        _fastenerContext = fastenerContext;
        if (seated)
        {
            var screws = _seat == HoleSeat.Counterbore
                ? _workbook.Counterbore.Where(r => r.Units == _units).Select(r => (r.Screw, r.Hidden))
                : _workbook.Countersink.Where(r => r.Units == _units).Select(r => (r.Screw, r.Hidden));
            SetList(ref _fastenerOptions, Options(screws, keepFastener ? _fastener : null), nameof(FastenerOptions));
            Set(ref _fastener, Find(_fastenerOptions, _fastener) ?? FirstOf(_fastenerOptions), nameof(Fastener));
        }
        else
        {
            SetList(ref _fastenerOptions, [], nameof(FastenerOptions));
            Set(ref _fastener, null, nameof(Fastener));
        }

        // Size.
        var previousSizeContext = _sizeContext;
        var sizeContext = new ListContext(_units, kind, _seat, _fastener, null, null);
        var keepSize = keepExact || sizeContext == previousSizeContext;
        _sizeContext = sizeContext;
        SetList(ref _sizeOptions, Options(SizeRows(sizeContext), keepSize ? _size : null), nameof(SizeOptions));
        var size = Find(_sizeOptions, _size);
        if (size is null && previousSizeContext is { } previous && previous.Units != _units)
        {
            size = Nearest(previous, _size, sizeContext);
        }

        Set(ref _size, size ?? FirstOf(_sizeOptions), nameof(Size));

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
            var rows = _workbook.PrecisionTolerances
                .Where(r => r.Units == _units && r.Fit == precisionFit)
                .Select(r => (r.Name, Hidden: false));
            SetList(ref _toleranceOptions, Options(rows, keepTolerance ? _tolerance : null), nameof(ToleranceOptions));
            Set(
                ref _tolerance,
                Find(_toleranceOptions, _tolerance)
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
            SetList(ref _drillPointOptions, Options(points, _drillPoint), nameof(DrillPointOptions));
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
            _flip).Normalised();

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
    ];

    private void Reevaluate()
    {
        (_spec, _build, _checks) = Evaluate();
        foreach (var name in DerivedProperties)
        {
            OnPropertyChanged(name);
        }

        OkCommand.RaiseCanExecuteChanged();
    }

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
        _invalidText.TryGetValue(field, out var text) ? text : FormatNumber(value);

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
        decimal? value = null;
        if (raw.Trim().Length > 0)
        {
            if (!TryParseNumber(raw, out var parsed))
            {
                _invalidText[field] = raw;
                OnPropertyChanged(textProperty);
                OnPropertyChanged(ProblemPropertyOf(field));
                OnPropertyChanged(nameof(HasTextThatIsNotANumber));
                OnPropertyChanged(nameof(CanOk));
                OkCommand.RaiseCanExecuteChanged();
                return;
            }

            value = parsed;
        }

        var wasInvalid = _invalidText.Remove(field);
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

    private string? NotANumber(HoleField field) => _invalidText.ContainsKey(field) ? NotANumberText : null;

    private static string ProblemPropertyOf(HoleField field) => field switch
    {
        HoleField.ThreadDepth => nameof(ThreadDepthProblem),
        HoleField.DrillDepth => nameof(DrillDepthProblem),
        _ => nameof(DepthProblem),
    };

    private static string? Find(IReadOnlyList<HoleOption<string>> options, string? value) =>
        options.FirstOrDefault(o => SameText(o.Value, value))?.Value;

    private static string? FirstOf(IReadOnlyList<HoleOption<string>> options) => options.FirstOrDefault()?.Value;

    private static bool SameText(string? a, string? b) =>
        a is not null && b is not null
        && string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
}
