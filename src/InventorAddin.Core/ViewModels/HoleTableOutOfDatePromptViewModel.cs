using System.Collections.ObjectModel;
using InventorAddin.Core.HoleTable;

namespace InventorAddin.Core.ViewModels;

/// <summary>How the out-of-date prompt closed.</summary>
public enum HoleTableOutOfDatePromptOutcome
{
    /// <summary>Every listed view was updated.</summary>
    Updated,

    /// <summary>Later, or Close after some views failed to update.</summary>
    Later,
}

/// <summary>
/// The small window the automatic check shows (task 175): names the drawing's out-of-date views with Update now and Later.
/// Update now updates each view through <see cref="HoleTableStoredUpdate"/> in its own call (the add-in wraps each call in
/// its own transaction, through the optional <c>runUpdate</c> delegate).
/// </summary>
public sealed class HoleTableOutOfDatePromptViewModel : ObservableObject
{
    /// <summary>Runs one view's update. The add-in's version wraps it in a transaction; the default calls it directly.</summary>
    public delegate bool UpdateRunner(string sheetKey, string viewKey, string sheetName, string viewName, out string text);

    private readonly IHoleTableSource _source;
    private readonly string _drawingName;
    private readonly UpdateRunner? _runUpdate;
    private List<HoleTableStoredViewCheck> _views;
    private bool _failed;
    private string _resultText = "";

    public HoleTableOutOfDatePromptViewModel(
        IHoleTableSource source,
        string drawingName,
        IEnumerable<HoleTableStoredViewCheck> outOfDate,
        UpdateRunner? runUpdate = null)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _drawingName = drawingName;
        _runUpdate = runUpdate;
        _views = outOfDate.ToList();
        UpdateNowCommand = new RelayCommand(UpdateNow);
        LaterCommand = new RelayCommand(() => CloseRequested?.Invoke(HoleTableOutOfDatePromptOutcome.Later));
        Refill();
    }

    /// <summary>Raised when the window should close.</summary>
    public event Action<HoleTableOutOfDatePromptOutcome>? CloseRequested;

    public string Heading => _failed ? HoleTableMessages.UpdateFailedHeading(_drawingName) : HoleTableMessages.OutOfDatePromptHeading(_drawingName);

    /// <summary>One line per view: the out-of-date reason, or after a failed Update now the reason it failed.</summary>
    public ObservableCollection<string> ViewLines { get; } = [];

    public string UpdateNowText => HoleTableMessages.UpdateNowButton;

    /// <summary>"Later", or "Close" once the window only reports views that could not be updated.</summary>
    public string LaterText => _failed ? HoleTableMessages.CloseButton : HoleTableMessages.LaterButton;

    public RelayCommand UpdateNowCommand { get; }

    public RelayCommand LaterCommand { get; }

    /// <summary>The views still listed: all of them at first, then only the ones that failed.</summary>
    public IReadOnlyList<HoleTableStoredViewCheck> Views => _views;

    /// <summary>What Update now did, in plain words, for the log. Empty until it runs.</summary>
    public string ResultText
    {
        get => _resultText;
        private set => SetProperty(ref _resultText, value);
    }

    private void UpdateNow()
    {
        var results = new List<string>();
        var failed = new List<HoleTableStoredViewCheck>();
        foreach (var item in _views)
        {
            bool ok;
            string text;
            if (_runUpdate is not null)
            {
                try
                {
                    ok = _runUpdate(item.SheetKey, item.ViewKey, item.SheetName, item.ViewName, out text);
                }
                catch (Exception ex)
                {
                    ok = false;
                    text = $"Could not update the tables: {ex.Message}";
                }
            }
            else
            {
                ok = HoleTableStoredUpdate.Run(_source, item.SheetKey, item.ViewKey, item.SheetName, item.ViewName, out text);
            }

            var reason = ok ? text : HoleTableMessages.UpdateFailedPrefix + text;
            results.Add(HoleTableMessages.ViewLine(item.ViewName, item.SheetName, reason));
            if (!ok)
            {
                failed.Add(item with { Reason = reason });
            }
        }

        ResultText = string.Join(" ", results);
        _views = failed;
        if (failed.Count == 0)
        {
            CloseRequested?.Invoke(HoleTableOutOfDatePromptOutcome.Updated);
            return;
        }

        _failed = true;
        Refill();
        OnPropertyChanged(nameof(Heading));
        OnPropertyChanged(nameof(LaterText));
        OnPropertyChanged(nameof(Views));
    }

    private void Refill()
    {
        ViewLines.Clear();
        foreach (var item in _views)
        {
            ViewLines.Add(item.Text);
        }
    }
}
