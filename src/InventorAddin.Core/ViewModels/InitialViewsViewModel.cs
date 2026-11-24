using System.Collections.ObjectModel;
using System.Globalization;
using InventorAddin.Core.Drawing;

namespace InventorAddin.Core.ViewModels;

/// <summary>One tick box in the Initial Views window.</summary>
public sealed class InitialViewChoice : ObservableObject
{
    private readonly InitialViewsViewModel _owner;
    private bool _isChecked;

    internal InitialViewChoice(InitialViewsViewModel owner, InitialViewKind kind, string label, bool isChecked)
    {
        _owner = owner;
        Kind = kind;
        Label = label;
        _isChecked = isChecked;
    }

    public InitialViewKind Kind { get; }
    public string Label { get; }

    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            if (SetProperty(ref _isChecked, value))
                _owner.ViewChecked(this);
        }
    }

    /// <summary>Sets the tick for the owner's own rule, raising the change but not asking the owner to re-plan.</summary>
    internal void SetByRule(bool value) => SetProperty(ref _isChecked, value, nameof(IsChecked));
}

/// <summary>One entry of the scale list. <see cref="Scale"/> is null for the suggested entry.</summary>
public sealed class InitialViewScaleChoice : ObservableObject
{
    private string _label;

    internal InitialViewScaleChoice(string label, double? scale)
    {
        _label = label;
        Scale = scale;
    }

    public double? Scale { get; }

    public string Label
    {
        get => _label;
        internal set => SetProperty(ref _label, value);
    }

    public override string ToString() => Label;
}

/// <summary>One entry of the units list (Metric or Imperial).</summary>
public sealed class InitialViewUnitsChoice
{
    internal InitialViewUnitsChoice(PrintUnits units, string label)
    {
        Units = units;
        Label = label;
    }

    public PrintUnits Units { get; }
    public string Label { get; }
    public override string ToString() => Label;
}

/// <summary>One entry of the sheet size list, for example "A3, 420 x 297 mm".</summary>
public sealed class InitialViewSheetChoice
{
    internal InitialViewSheetChoice(SheetSizeSpec sheet, string label)
    {
        Sheet = sheet;
        Label = label;
    }

    public SheetSizeSpec Sheet { get; }
    public string Label { get; }
    public override string ToString() => Label;
}

/// <summary>One entry of the Front view list (task 233).</summary>
public sealed class InitialViewFrontChoice
{
    internal InitialViewFrontChoice(FrontViewChoice choice)
    {
        Choice = choice;
        Label = FrontViewRule.LabelFor(choice);
    }

    public FrontViewChoice Choice { get; }
    public string Label { get; }
    public override string ToString() => Label;
}

/// <summary>
/// View-model for the Initial Views window that Create Print and Place Initial Views show (spec 05, "Decided for M5: Place
/// Initial Views"; task 190). It starts with the model kind's views ticked and the suggested scale selected, re-runs
/// <see cref="InitialViewsPlan.Plan"/> when a view is ticked or the scale changes, and shows the plan's messages as a warning.
/// OK stores <see cref="Result"/> and raises <see cref="CloseRequested"/>; Cancel closes with no result.
/// </summary>
/// <remarks>
/// The top and right views are projected from the front view, so ticking either ticks Front, and unticking Front unticks both
/// (Default). A sheet metal part has no such dependency. The flat pattern is offered even when its size is not known; the plan
/// then leaves it out and says so.
/// </remarks>
public sealed class InitialViewsViewModel : ObservableObject
{
    public const string HeaderTitleText = "Initial Views";
    public const string OkButtonText = "_OK";
    public const string CancelButtonText = "Cancel";
    public const string SuggestedLabelPrefix = "Suggested";

    private readonly InitialViewModelKind _model;
    // The model's range box: width is X, height is Y, depth is Z. The front view's own box follows the Front view choice (task 233).
    private readonly InitialViewsBoxCm _box;
    private readonly IReadOnlyDictionary<InitialViewKind, ViewSizeCm>? _givenMeasured;
    private readonly InitialViewFrontChoice[] _frontOptions = Enum.GetValues<FrontViewChoice>().Select(c => new InitialViewFrontChoice(c)).ToArray();
    private InitialViewFrontChoice _selectedFront;
    private FrontViewSelection _frontSelection;
    private readonly FlatPatternSizeCm? _flatPattern;
    private IReadOnlyDictionary<InitialViewKind, ViewSizeCm>? _measured;
    private SheetSizeSpec _sheet;
    private SheetAreaCm _area;
    private readonly InitialViewScaleChoice _suggested;
    private bool _updating = true;

    private InitialViewScaleChoice _selectedScale;
    private InitialViewsResult _plan;

    // Units and sheet choice (task 232): only when the window was made with the units constructor (Create Print).
    private readonly bool _canChoose;
    private readonly Func<PrintUnits, FrontViewChoice, IReadOnlyDictionary<InitialViewKind, ViewSizeCm>?>? _measureFor;
    private readonly Dictionary<(PrintUnits, FrontViewChoice), IReadOnlyDictionary<InitialViewKind, ViewSizeCm>?> _measuredByUnits = new();
    private readonly InitialViewUnitsChoice[] _unitsOptions =
    {
        new(PrintUnits.Metric, "Metric"),
        new(PrintUnits.Imperial, "Imperial"),
    };
    private InitialViewUnitsChoice? _selectedUnits;
    private IReadOnlyList<InitialViewSheetChoice> _sheetOptions = Array.Empty<InitialViewSheetChoice>();
    private InitialViewSheetChoice? _selectedSheet;
    private bool _sheetChosen;
    private SheetSizeSpec? _nextSheet;

    /// <summary>
    /// A window for a fixed sheet (Place Initial Views): no units or sheet choice. <paramref name="measured"/> is the measuring for the
    /// Auto front view; <paramref name="measureFront"/>, when given, measures for each Front view choice (called once per choice) and
    /// replaces it. A choice with neither is planned from the range box.
    /// </summary>
    public InitialViewsViewModel(
        InitialViewModelKind model,
        InitialViewsBoxCm box,
        FlatPatternSizeCm? flatPattern,
        SheetSizeSpec sheet,
        IReadOnlyDictionary<InitialViewKind, ViewSizeCm>? measured = null,
        Func<FrontViewChoice, IReadOnlyDictionary<InitialViewKind, ViewSizeCm>?>? measureFront = null)
        : this(model, box, flatPattern, sheet, measured, canChoose: false,
            measureFor: measureFront is null ? null : (_, front) => measureFront(front), partUnits: sheet?.Units ?? PrintUnits.Metric)
    {
    }

    /// <summary>
    /// A window that also offers Metric | Imperial (starting on <paramref name="partUnits"/>) and a sheet size (task 232). The sheet starts on
    /// the smallest that fits (<see cref="InitialViewsSheetRule"/>) and follows the views and scale until the user picks one.
    /// <paramref name="measureFor"/> measures the views with the template of the given units (task 227); it is called once per units, when
    /// those units are first shown, and may return null (the box estimate is used).
    /// </summary>
    public InitialViewsViewModel(
        InitialViewModelKind model,
        InitialViewsBoxCm box,
        FlatPatternSizeCm? flatPattern,
        PrintUnits partUnits,
        Func<PrintUnits, IReadOnlyDictionary<InitialViewKind, ViewSizeCm>?>? measureFor)
        : this(model, box, flatPattern, InitialViewsSetup.DefaultSheet(partUnits), null, canChoose: true,
            measureFor is null ? null : (units, _) => measureFor(units), partUnits)
    {
    }

    /// <summary>
    /// As above, with the measuring told which Front view is chosen (task 233): the views are measured again, once per units and Front view,
    /// when the user changes it.
    /// </summary>
    public InitialViewsViewModel(
        InitialViewModelKind model,
        InitialViewsBoxCm box,
        FlatPatternSizeCm? flatPattern,
        PrintUnits partUnits,
        Func<PrintUnits, FrontViewChoice, IReadOnlyDictionary<InitialViewKind, ViewSizeCm>?>? measureFor)
        : this(model, box, flatPattern, InitialViewsSetup.DefaultSheet(partUnits), null, canChoose: true, measureFor, partUnits)
    {
    }

    private InitialViewsViewModel(
        InitialViewModelKind model,
        InitialViewsBoxCm box,
        FlatPatternSizeCm? flatPattern,
        SheetSizeSpec sheet,
        IReadOnlyDictionary<InitialViewKind, ViewSizeCm>? measured,
        bool canChoose,
        Func<PrintUnits, FrontViewChoice, IReadOnlyDictionary<InitialViewKind, ViewSizeCm>?>? measureFor,
        PrintUnits partUnits)
    {
        ArgumentNullException.ThrowIfNull(box);
        ArgumentNullException.ThrowIfNull(sheet);
        _model = model;
        _box = box;
        _flatPattern = flatPattern;
        _measured = measured;
        _givenMeasured = measured;
        _selectedFront = _frontOptions.First(o => o.Choice == FrontViewChoice.Auto);
        _frontSelection = FrontViewRule.Select(FrontViewChoice.Auto, model, box);
        _sheet = sheet;
        _area = InitialViewsPlan.UsableArea(sheet);
        _canChoose = canChoose;
        _measureFor = measureFor;
        UseNextSheetCommand = new RelayCommand(UseNextSheet, () => _nextSheet is not null);
        if (canChoose)
        {
            _selectedUnits = _unitsOptions.First(u => u.Units == partUnits);
            LoadUnits(partUnits);
        }
        else if (measureFor is not null)
        {
            LoadMeasured(sheet.Units);
        }

        Views = new ObservableCollection<InitialViewChoice>(
            InitialViewsPlan.DefaultViews(model).Select(k => new InitialViewChoice(this, k, LabelFor(k), true)));

        _suggested = new InitialViewScaleChoice(SuggestedLabelPrefix, null);
        var scales = new List<InitialViewScaleChoice> { _suggested };
        scales.AddRange(PrintPlan.StandardScales.Select(s => new InitialViewScaleChoice(FormatScale(s), s)));
        ScaleOptions = scales;
        _selectedScale = _suggested;

        _plan = RunPlan();
        if (_canChoose)
        {
            ApplyAutoSheet();
            _plan = RunPlan();
        }

        _updating = false;
        UpdateSuggestedLabel();
        RefreshNextSheet();

        OkCommand = new RelayCommand(Ok, () => CanOk);
        CancelCommand = new RelayCommand(Cancel);
    }

    /// <summary>Raised when the window should close (after OK or Cancel).</summary>
    public event EventHandler? CloseRequested;

    public ObservableCollection<InitialViewChoice> Views { get; }

    /// <summary>"Suggested (1:2)" first, then the standard scales, largest first.</summary>
    public IReadOnlyList<InitialViewScaleChoice> ScaleOptions { get; }

    public InitialViewScaleChoice SelectedScale
    {
        get => _selectedScale;
        set
        {
            if (value is null || !SetProperty(ref _selectedScale, value))
                return;
            Replan();
        }
    }

    /// <summary>Auto, Front, Back, Top, Bottom, Left, Right (task 233).</summary>
    public IReadOnlyList<InitialViewFrontChoice> FrontOptions => _frontOptions;

    /// <summary>
    /// Which way the front view looks. Auto (the start) is the direction with the largest projected area, longest side horizontal, or the
    /// base face for a sheet metal part. The other views are projected from it, and the views are measured again for the new direction.
    /// </summary>
    public InitialViewFrontChoice SelectedFront
    {
        get => _selectedFront;
        set
        {
            if (value is null || !SetProperty(ref _selectedFront, value))
                return;
            _frontSelection = FrontViewRule.Select(value.Choice, _model, _box);
            LoadMeasured(_sheet.Units);
            Replan();
        }
    }

    /// <summary>The sheet the print will use, for display, for example "A3, 420 x 297 mm".</summary>
    public string SheetSizeText => FormatSheet(_sheet);

    /// <summary>True when the window offers the units and the sheet size (Create Print); false for a fixed sheet (Place Initial Views).</summary>
    public bool CanChooseSheet => _canChoose;

    /// <summary>Metric, Imperial.</summary>
    public IReadOnlyList<InitialViewUnitsChoice> UnitsOptions => _unitsOptions;

    /// <summary>The print's units: the AWB Metric or the AWB Imperial template. Starts on the part's units.</summary>
    public InitialViewUnitsChoice? SelectedUnits
    {
        get => _selectedUnits;
        set
        {
            if (!_canChoose || value is null || !SetProperty(ref _selectedUnits, value))
                return;
            LoadUnits(value.Units);
            _sheetChosen = false;
            Replan();
            OnPropertyChanged(nameof(SheetOptions));
        }
    }

    /// <summary>The sheet sizes of the chosen units, smallest first (A4 to A0, or ANSI A to E).</summary>
    public IReadOnlyList<InitialViewSheetChoice> SheetOptions => _sheetOptions;

    /// <summary>The sheet the print is made on. Picking one stops the window choosing the smallest that fits.</summary>
    public InitialViewSheetChoice? SelectedSheet
    {
        get => _selectedSheet;
        set
        {
            if (!_canChoose || value is null || !SetProperty(ref _selectedSheet, value))
                return;
            _sheetChosen = true;
            _sheet = value.Sheet;
            _area = InitialViewsPlan.UsableArea(_sheet);
            Replan();
        }
    }

    /// <summary>The units the print is made in (the sheet's units on a fixed sheet).</summary>
    public PrintUnits ChosenUnits => _sheet.Units;

    /// <summary>The sheet the print is made on; the template sheet format is <see cref="SheetSizeSpec.FormatName"/>.</summary>
    public SheetSizeSpec ChosenSheet => _sheet;

    /// <summary>Set when the views do not fit the chosen sheet and a larger one does, for example "A2 fits these views at this scale.".</summary>
    public string NextSheetOffer => _nextSheet is null ? string.Empty : $"{FormatSheetName(_nextSheet)} fits these views at this scale.";

    public bool HasNextSheetOffer => _nextSheet is not null;

    /// <summary>The text of the button that takes the offered sheet.</summary>
    public string NextSheetButtonText => _nextSheet is null ? string.Empty : $"Use {FormatSheetName(_nextSheet)}";

    public RelayCommand UseNextSheetCommand { get; }

    /// <summary>The current plan: what OK would hand back.</summary>
    public InitialViewsResult Plan => _plan;

    public string ScaleText => FormatScale(_plan.Scale);

    public IReadOnlyList<string> Messages => _plan.Messages;

    /// <summary>The plan's messages, one per line; empty when there is nothing to warn about.</summary>
    public string Warning => string.Join(Environment.NewLine, _plan.Messages);

    public bool HasWarning => _plan.Messages.Count > 0;

    /// <summary>The plan OK confirmed, or null before OK and after Cancel.</summary>
    public InitialViewsResult? Result { get; private set; }

    /// <summary>False when no view would be placed (none ticked, or only a flat pattern whose size is not known).</summary>
    public bool CanOk => _plan.Views.Count > 0;

    public RelayCommand OkCommand { get; }
    public RelayCommand CancelCommand { get; }

    internal void ViewChecked(InitialViewChoice changed)
    {
        if (_updating)
            return;

        _updating = true;
        try
        {
            InitialViewChoice? front = Views.FirstOrDefault(v => v.Kind == InitialViewKind.Front);
            if (front is not null)
            {
                bool projected = changed.Kind is InitialViewKind.Top or InitialViewKind.Right;
                if (changed.IsChecked && projected)
                {
                    front.SetByRule(true);
                }
                else if (!changed.IsChecked && changed.Kind == InitialViewKind.Front)
                {
                    foreach (InitialViewChoice v in Views.Where(v => v.Kind is InitialViewKind.Top or InitialViewKind.Right))
                        v.SetByRule(false);
                }
            }
        }
        finally
        {
            _updating = false;
        }

        Replan();
    }

    private void Replan()
    {
        if (_canChoose && !_sheetChosen)
            ApplyAutoSheet();
        _plan = RunPlan();
        UpdateSuggestedLabel();
        RefreshNextSheet();
        OnPropertyChanged(nameof(SheetSizeText));
        OnPropertyChanged(nameof(Plan));
        OnPropertyChanged(nameof(ScaleText));
        OnPropertyChanged(nameof(Messages));
        OnPropertyChanged(nameof(Warning));
        OnPropertyChanged(nameof(HasWarning));
        OnPropertyChanged(nameof(CanOk));
        OkCommand.RaiseCanExecuteChanged();
    }

    // Shows the units' sheets and, for them, the measured view sizes (measured once per units, with that units' template: task 227).
    private void LoadUnits(PrintUnits units)
    {
        _sheetOptions = InitialViewsSheetRule.SheetsFor(units).Select(s => new InitialViewSheetChoice(s, FormatSheet(s))).ToList();
        LoadMeasured(units);
    }

    // The measured view sizes for these units and the chosen Front view, measured once each. Without a measuring function the Auto
    // front view keeps the sizes the window was given and any other choice is planned from the range box.
    private void LoadMeasured(PrintUnits units)
    {
        FrontViewChoice front = _selectedFront.Choice;
        if (_measureFor is null)
        {
            _measured = front == FrontViewChoice.Auto ? _givenMeasured : null;
            return;
        }

        if (!_measuredByUnits.TryGetValue((units, front), out var measured))
        {
            measured = _measureFor(units, front);
            _measuredByUnits[(units, front)] = measured;
        }

        _measured = measured;
    }

    private InitialViewsResult PlanOn(SheetSizeSpec sheet, double? scale) =>
        InitialViewsPlan.Plan(_model, _frontSelection.Box, _flatPattern, InitialViewsPlan.UsableArea(sheet), Ticked(), scale, _measured);

    // The smallest sheet that fits the ticked views at the selected scale; set without marking it as the user's choice.
    private void ApplyAutoSheet()
    {
        var sheets = _sheetOptions.Select(o => o.Sheet).ToList();
        if (sheets.Count == 0)
            return;

        double? scale = _selectedScale.Scale;
        SheetSizeSpec best = InitialViewsSheetRule.Smallest(sheets, s => PlanOn(s, scale), scale is null);
        _sheet = best;
        _area = InitialViewsPlan.UsableArea(best);
        _selectedSheet = _sheetOptions.First(o => o.Sheet.FormatName == best.FormatName);
        OnPropertyChanged(nameof(SelectedSheet));
    }

    private void RefreshNextSheet()
    {
        _nextSheet = _canChoose
            ? InitialViewsSheetRule.NextThatFits(_sheetOptions.Select(o => o.Sheet).ToList(), _sheet, s => PlanOn(s, _selectedScale.Scale))
            : null;
        OnPropertyChanged(nameof(NextSheetOffer));
        OnPropertyChanged(nameof(HasNextSheetOffer));
        OnPropertyChanged(nameof(NextSheetButtonText));
        UseNextSheetCommand.RaiseCanExecuteChanged();
    }

    private void UseNextSheet()
    {
        if (_nextSheet is null)
            return;
        SelectedSheet = _sheetOptions.First(o => o.Sheet.FormatName == _nextSheet.FormatName);
    }

    private List<InitialViewKind> Ticked() => Views.Where(v => v.IsChecked).Select(v => v.Kind).ToList();

    private InitialViewsResult RunPlan() =>
        InitialViewsPlan.Plan(_model, _frontSelection.Box, _flatPattern, _area, Ticked(), _selectedScale.Scale, _measured)
            with { FrontOrientation = _frontSelection.Orientation };

    // The suggested scale is the one the plan picks for the ticked views, whatever scale is selected now.
    private void UpdateSuggestedLabel()
    {
        InitialViewsResult auto = _selectedScale.Scale is null
            ? _plan
            : InitialViewsPlan.Plan(_model, _frontSelection.Box, _flatPattern, _area, Ticked(), null, _measured);
        _suggested.Label = auto.Views.Count == 0
            ? SuggestedLabelPrefix
            : $"{SuggestedLabelPrefix} ({FormatScale(auto.Scale)})";
    }

    private void Ok()
    {
        Result = _plan;
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void Cancel()
    {
        Result = null;
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private static string LabelFor(InitialViewKind kind) => kind switch
    {
        InitialViewKind.Front => "Front",
        InitialViewKind.Top => "Top",
        InitialViewKind.Right => "Right",
        InitialViewKind.Iso => "Isometric",
        InitialViewKind.FoldedFront => "Folded front",
        InitialViewKind.FlatPattern => "Flat pattern",
        _ => kind.ToString(),
    };

    /// <summary>A scale as drawings write it: 0.5 is "1:2", 2 is "2:1", 1 is "1:1".</summary>
    public static string FormatScale(double scale) => scale >= 1
        ? string.Create(CultureInfo.InvariantCulture, $"{Math.Round(scale, 3)}:1")
        : string.Create(CultureInfo.InvariantCulture, $"1:{Math.Round(1 / scale, 3)}");

    private static string FormatSheetName(SheetSizeSpec sheet) => sheet.Units == PrintUnits.Metric ? sheet.Name : $"ANSI {sheet.Name}";

    private static string FormatSheet(SheetSizeSpec sheet) => sheet.Units == PrintUnits.Metric
        ? string.Create(CultureInfo.InvariantCulture, $"{sheet.Name}, {sheet.WidthMm:0.#} x {sheet.HeightMm:0.#} mm")
        : string.Create(CultureInfo.InvariantCulture, $"ANSI {sheet.Name}, {sheet.WidthMm / 25.4:0.##} x {sheet.HeightMm / 25.4:0.##} in");
}
