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
    private readonly InitialViewsBoxCm _box;
    private readonly FlatPatternSizeCm? _flatPattern;
    private readonly SheetSizeSpec _sheet;
    private readonly SheetAreaCm _area;
    private readonly InitialViewScaleChoice _suggested;
    private bool _updating = true;

    private InitialViewScaleChoice _selectedScale;
    private InitialViewsResult _plan;

    public InitialViewsViewModel(
        InitialViewModelKind model,
        InitialViewsBoxCm box,
        FlatPatternSizeCm? flatPattern,
        SheetSizeSpec sheet)
    {
        ArgumentNullException.ThrowIfNull(box);
        ArgumentNullException.ThrowIfNull(sheet);
        _model = model;
        _box = box;
        _flatPattern = flatPattern;
        _sheet = sheet;
        _area = InitialViewsPlan.UsableArea(sheet);

        Views = new ObservableCollection<InitialViewChoice>(
            InitialViewsPlan.DefaultViews(model).Select(k => new InitialViewChoice(this, k, LabelFor(k), true)));

        _suggested = new InitialViewScaleChoice(SuggestedLabelPrefix, null);
        var scales = new List<InitialViewScaleChoice> { _suggested };
        scales.AddRange(PrintPlan.StandardScales.Select(s => new InitialViewScaleChoice(FormatScale(s), s)));
        ScaleOptions = scales;
        _selectedScale = _suggested;

        _plan = RunPlan();
        _updating = false;
        UpdateSuggestedLabel();

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

    /// <summary>The sheet the print will use, for display, for example "A3, 420 x 297 mm".</summary>
    public string SheetSizeText => FormatSheet(_sheet);

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
        _plan = RunPlan();
        UpdateSuggestedLabel();
        OnPropertyChanged(nameof(Plan));
        OnPropertyChanged(nameof(ScaleText));
        OnPropertyChanged(nameof(Messages));
        OnPropertyChanged(nameof(Warning));
        OnPropertyChanged(nameof(HasWarning));
        OnPropertyChanged(nameof(CanOk));
        OkCommand.RaiseCanExecuteChanged();
    }

    private List<InitialViewKind> Ticked() => Views.Where(v => v.IsChecked).Select(v => v.Kind).ToList();

    private InitialViewsResult RunPlan() =>
        InitialViewsPlan.Plan(_model, _box, _flatPattern, _area, Ticked(), _selectedScale.Scale);

    // The suggested scale is the one the plan picks for the ticked views, whatever scale is selected now.
    private void UpdateSuggestedLabel()
    {
        InitialViewsResult auto = _selectedScale.Scale is null
            ? _plan
            : InitialViewsPlan.Plan(_model, _box, _flatPattern, _area, Ticked());
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

    private static string FormatSheet(SheetSizeSpec sheet) => sheet.Units == PrintUnits.Metric
        ? string.Create(CultureInfo.InvariantCulture, $"{sheet.Name}, {sheet.WidthMm:0.#} x {sheet.HeightMm:0.#} mm")
        : string.Create(CultureInfo.InvariantCulture, $"ANSI {sheet.Name}, {sheet.WidthMm / 25.4:0.##} x {sheet.HeightMm / 25.4:0.##} in");
}
