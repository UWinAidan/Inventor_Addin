using InventorAddin.Core.Drawing;
using InventorAddin.Core.Materials;
using InventorAddin.Core.PartProperties;

namespace InventorAddin.Core.ViewModels;

/// <summary>
/// The Stock Size window's model (spec 05, "Added for M5", item 5; task 205): plate or round bar, the worked-out result and the text,
/// which the user can edit before it is written to Material Size. Choosing the other shape works out the result again and drops an
/// edit. When no size could be worked out (too big for the list), the text starts blank and the problem is shown; the user may still type one.
/// </summary>
public sealed class StockSizeViewModel : ObservableObject
{
    private readonly IReadOnlyList<double> _boxCm;
    private readonly StockWorkbook _workbook;
    private StockShape _shape;
    private StockSizeResult _result;
    private string _text;

    /// <param name="boxCm">The part's three box lengths in cm, any order.</param>
    /// <param name="units">The part's units.</param>
    /// <param name="workbook">The stock workbook as read.</param>
    /// <param name="workbookProblems">What reading the workbook reported, shown to the user.</param>
    /// <param name="storedMaterialSize">The Material Size the part has now, or null.</param>
    public StockSizeViewModel(
        IReadOnlyList<double> boxCm,
        PrintUnits units,
        StockWorkbook workbook,
        IReadOnlyList<StockWorkbookProblem>? workbookProblems = null,
        string? storedMaterialSize = null,
        StockShape shape = StockShape.Plate)
    {
        _boxCm = boxCm ?? throw new ArgumentNullException(nameof(boxCm));
        _workbook = workbook ?? throw new ArgumentNullException(nameof(workbook));
        Units = units;
        WorkbookProblems = workbookProblems ?? [];
        StoredMaterialSize = PropertyValues.NormaliseText(storedMaterialSize);
        _shape = shape;
        _result = StockSize.For(shape, _boxCm, units, _workbook);
        _text = _result.Text ?? string.Empty;
    }

    public PrintUnits Units { get; }

    public IReadOnlyList<StockWorkbookProblem> WorkbookProblems { get; }

    /// <summary>The part's Material Size now, trimmed; blank when it has none.</summary>
    public string StoredMaterialSize { get; }

    /// <summary>Plate or round bar; changing it works the result out again and drops an edited text.</summary>
    public StockShape Shape
    {
        get => _shape;
        set
        {
            if (_shape == value)
            {
                return;
            }

            _shape = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsPlate));
            OnPropertyChanged(nameof(IsRound));
            Recalculate();
        }
    }

    public bool IsPlate
    {
        get => _shape == StockShape.Plate;
        set
        {
            if (value)
            {
                Shape = StockShape.Plate;
            }
        }
    }

    public bool IsRound
    {
        get => _shape == StockShape.Round;
        set
        {
            if (value)
            {
                Shape = StockShape.Round;
            }
        }
    }

    /// <summary>The worked-out size for the chosen shape.</summary>
    public StockSizeResult Result => _result;

    /// <summary>The text as shown and edited; it starts as the result's text, blank when none could be worked out.</summary>
    public string Text
    {
        get => _text;
        set
        {
            value ??= string.Empty;
            if (_text == value)
            {
                return;
            }

            _text = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsEdited));
            OnPropertyChanged(nameof(CanWrite));
            OnPropertyChanged(nameof(Message));
        }
    }

    /// <summary>Whether the text differs from the worked-out one (ignoring surrounding spaces).</summary>
    public bool IsEdited => !string.Equals(_text.Trim(), _result.Text ?? string.Empty, StringComparison.Ordinal);

    /// <summary>Whether there is text to write.</summary>
    public bool CanWrite => _text.Trim().Length > 0;

    /// <summary>
    /// What to tell the user under the text: the problem when none could be worked out and nothing is typed yet, and otherwise null.
    /// </summary>
    public string? Message => _result.Succeeded || CanWrite ? null : _result.Problem;

    /// <summary>The problem with the worked-out size even after the user has typed one, so the window can keep it visible; null when none.</summary>
    public string? ResultProblem => _result.Problem;

    /// <summary>The workbook's problems, one per line, or null when it read cleanly.</summary>
    public string? WorkbookProblemsText =>
        WorkbookProblems.Count == 0 ? null : string.Join(Environment.NewLine, WorkbookProblems.Select(p => p.ToString()));

    /// <summary>
    /// The write for Material Size: the text as it stands (edited or not), trimmed, or null when it is blank or already the stored value.
    /// A stored value that differs is a refresh, a blank one a fill.
    /// </summary>
    public PropertyWrite? WriteFor()
    {
        var text = PropertyValues.NormaliseText(_text);
        if (text.Length == 0 || string.Equals(text, StoredMaterialSize, StringComparison.Ordinal))
        {
            return null;
        }

        return PropertyWrite.SetText(PartPropertyStorage.MaterialSize, text) with { IsRefresh = StoredMaterialSize.Length > 0 };
    }

    private void Recalculate()
    {
        _result = StockSize.For(_shape, _boxCm, Units, _workbook);
        _text = _result.Text ?? string.Empty;
        OnPropertyChanged(nameof(Result));
        OnPropertyChanged(nameof(ResultProblem));
        OnPropertyChanged(nameof(Text));
        OnPropertyChanged(nameof(IsEdited));
        OnPropertyChanged(nameof(CanWrite));
        OnPropertyChanged(nameof(Message));
    }
}
