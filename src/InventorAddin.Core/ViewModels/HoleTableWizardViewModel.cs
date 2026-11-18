using System.Collections.ObjectModel;
using InventorAddin.Core.HoleTable;

namespace InventorAddin.Core.ViewModels;

/// <summary>One hole instance in a Hole Table Wizard list (spec 04, "Target dialog").</summary>
public sealed class HoleTableWizardRow : ObservableObject
{
    private readonly HoleTableWizardViewModel _owner;
    private readonly HoleTableGroup _group;
    private readonly HoleTableRow _row;

    internal HoleTableWizardRow(HoleTableWizardViewModel owner, HoleTableGroup group, HoleTableRow row)
    {
        _owner = owner;
        _group = group;
        _row = row;
    }

    public string HoleId => _row.Hole.Id;

    public HoleTableKind Table => _group.Table;

    public string Tag => _row.Tag;

    public string GroupLetter => _group.Letter;

    public string Description => _group.Description.PlainText;

    /// <summary>The Split checkbox: this hole gets its own group and letter.</summary>
    public bool IsSplit
    {
        get => _group.SplitHoleId is not null;
        set
        {
            if (value != IsSplit)
            {
                _owner.SetSplit(HoleId, value);
            }
        }
    }

    /// <summary>The group's NOTES text, empty when it has none. Editing it changes the whole group's note.</summary>
    public string Notes
    {
        get => _group.Note ?? "";
        set
        {
            if (value != Notes)
            {
                _owner.SetNote(this, value);
            }
        }
    }

    internal HoleTableGroup Group => _group;
}

/// <summary>One group of a Hole Table Wizard plan: its letter, description and NOTES (the Edit Table tab).</summary>
public sealed class HoleTableWizardGroup : ObservableObject
{
    private readonly HoleTableWizardViewModel _owner;
    private readonly HoleTableGroup _group;

    internal HoleTableWizardGroup(HoleTableWizardViewModel owner, HoleTableGroup group)
    {
        _owner = owner;
        _group = group;
    }

    public string Letter => _group.Letter;

    public HoleTableKind Table => _group.Table;

    public string Description => _group.Description.PlainText;

    /// <summary>The group's NOTES text, empty when it has none.</summary>
    public string Notes
    {
        get => _group.Note ?? "";
        set
        {
            if (value != Notes)
            {
                _owner.SetGroupNote(_group, value);
            }
        }
    }
}

/// <summary>One stored view found out of date, or one that could not be checked (task 171).</summary>
/// <param name="Reason">The reasons in words (<c>2 holes moved, 1 hole added.</c>), or why the check or the Update failed.</param>
public sealed record HoleTableStoredViewCheck(string SheetKey, string SheetName, string ViewKey, string ViewName, string Reason)
{
    /// <summary>For example <c>VIEW1 on Sheet:1: 2 holes moved.</c></summary>
    public string Text => HoleTableMessages.ViewLine(ViewName, SheetName, Reason);
}

/// <summary>
/// The logic of the Hole Table Wizard's Create Tables tab. It reads the drawing through <see cref="IHoleTableSource"/>
/// and builds the lists with <see cref="HoleTablePlan"/>; the add-in binds a window to it. The Edit Table tab and
/// Update (task 162).
/// </summary>
public sealed class HoleTableWizardViewModel : ObservableObject
{
    public const string NoViewSelectedText = "Pick a view, then click Analyze View.";

    private readonly IHoleTableSource _source;
    private HoleTableViewAnalysis? _analysis;
    private SheetPoint _origin;
    private HoleTableEdits _edits = HoleTableEdits.None;
    private HoleTableStoredTags? _tags;
    private HoleTableRefresh? _lastRefresh;
    private HoleTableViewInfo? _selectedStored;
    private HoleTablePlan? _plan;
    private string? _selectedSheet;
    private string? _selectedView;
    private HoleTableWizardRow? _selectedPrecision;
    private HoleTableWizardRow? _selectedClearance;
    private string _status = NoViewSelectedText;
    private bool _updatingSelection;

    public HoleTableWizardViewModel(IHoleTableSource source)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));

        AnalyzeCommand = new RelayCommand(Analyze, () => !string.IsNullOrEmpty(SelectedView));
        SwapCommand = new RelayCommand(Swap, () => SelectedRow is not null);
        DeleteCommand = new RelayCommand(Delete, () => SelectedRow is not null);
        RenumberCommand = new RelayCommand(Renumber, () => _plan is not null);
        GeneratePrecisionCommand = new RelayCommand(() => Generate(HoleTableKind.Precision), () => PrecisionRows.Count > 0);
        GenerateClearanceCommand = new RelayCommand(() => Generate(HoleTableKind.Clearance), () => ClearanceRows.Count > 0);
        CreateTablesCommand = new RelayCommand(CreateTables, () => PrecisionRows.Count + ClearanceRows.Count > 0);
        AlignCommand = new RelayCommand(Align, () => !string.IsNullOrEmpty(SelectedView));
        UpdateCommand = new RelayCommand(() => UpdateSelected(), () => !string.IsNullOrEmpty(SelectedView));
        UpdateOutOfDateCommand = new RelayCommand(UpdateOutOfDate, () => OutOfDateViews.Count > 0);
        DismissStoredCheckCommand = new RelayCommand(DismissStoredCheck, () => OutOfDateViews.Count + CouldNotCheckViews.Count > 0);
        OpenStoredViewCommand = new RelayCommand(() => { if (SelectedStoredView is { } v) OpenStoredView(v.Key); }, () => SelectedStoredView is not null);
        CancelCommand = new RelayCommand(() => CancelRequested?.Invoke(this, EventArgs.Empty));

        Sheets = _source.GetSheets();
        _selectedSheet = (Sheets.FirstOrDefault(s => s.IsActive) ?? Sheets.FirstOrDefault())?.Key;
        LoadViews();
    }

    /// <summary>Raised when Cancel is clicked; the window closes without changes.</summary>
    public event EventHandler? CancelRequested;

    public string DrawingName => _source.DrawingName;

    public IReadOnlyList<HoleTableSheetInfo> Sheets { get; }

    public ObservableCollection<HoleTableViewInfo> Views { get; } = [];

    public ObservableCollection<HoleTableWizardRow> PrecisionRows { get; } = [];

    public ObservableCollection<HoleTableWizardRow> ClearanceRows { get; } = [];

    /// <summary>One entry per group, for editing NOTES per group (the Edit Table tab).</summary>
    public ObservableCollection<HoleTableWizardGroup> Groups { get; } = [];

    /// <summary>The views of the selected sheet that have stored tables: the Edit Table tab's list.</summary>
    public ObservableCollection<HoleTableViewInfo> StoredViews { get; } = [];

    /// <summary>Stored views whose tables no longer match the model, from <see cref="CheckStoredTables"/>.</summary>
    public ObservableCollection<HoleTableStoredViewCheck> OutOfDateViews { get; } = [];

    /// <summary>Stored views the check could not read; never treated as up to date.</summary>
    public ObservableCollection<HoleTableStoredViewCheck> CouldNotCheckViews { get; } = [];

    /// <summary>The text for the window: the out-of-date views, then the ones that could not be checked. Empty when there is nothing to say.</summary>
    public string StoredCheckMessage
    {
        get
        {
            var lines = new List<string>();
            if (OutOfDateViews.Count > 0)
            {
                lines.Add(HoleTableMessages.OutOfDateHeading);
                lines.AddRange(OutOfDateViews.Select(v => v.Text));
            }

            if (CouldNotCheckViews.Count > 0)
            {
                lines.Add(HoleTableMessages.CouldNotCheckHeading);
                lines.AddRange(CouldNotCheckViews.Select(v => v.Text));
            }

            return string.Join("\n", lines);
        }
    }

    public bool HasStoredCheckMessage => StoredCheckMessage.Length > 0;

    /// <summary>The Edit Table tab's selected view.</summary>
    public HoleTableViewInfo? SelectedStoredView
    {
        get => _selectedStored;
        set
        {
            if (SetProperty(ref _selectedStored, value))
            {
                OpenStoredViewCommand.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>The current plan, or null before a successful Analyze View. The add-in's table methods read it.</summary>
    public HoleTablePlan? Plan => _plan;

    /// <summary>The key of the selected sheet.</summary>
    public string? SelectedSheet
    {
        get => _selectedSheet;
        set
        {
            if (SetProperty(ref _selectedSheet, value))
            {
                LoadViews();
            }
        }
    }

    /// <summary>The key of the selected view.</summary>
    public string? SelectedView
    {
        get => _selectedView;
        set
        {
            if (SetProperty(ref _selectedView, value))
            {
                ClearAnalysis(NoViewSelectedText);
                RaiseCommandStates();
            }
        }
    }

    public string Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    public HoleTableWizardRow? SelectedPrecisionRow
    {
        get => _selectedPrecision;
        set
        {
            if (SetProperty(ref _selectedPrecision, value))
            {
                if (value is not null && _selectedClearance is not null && !_updatingSelection)
                {
                    _selectedClearance = null;
                    OnPropertyChanged(nameof(SelectedClearanceRow));
                }

                SelectionChanged();
            }
        }
    }

    public HoleTableWizardRow? SelectedClearanceRow
    {
        get => _selectedClearance;
        set
        {
            if (SetProperty(ref _selectedClearance, value))
            {
                if (value is not null && _selectedPrecision is not null && !_updatingSelection)
                {
                    _selectedPrecision = null;
                    OnPropertyChanged(nameof(SelectedPrecisionRow));
                }

                SelectionChanged();
            }
        }
    }

    /// <summary>The selected row of whichever list holds the selection.</summary>
    public HoleTableWizardRow? SelectedRow => _selectedPrecision ?? _selectedClearance;

    public RelayCommand AnalyzeCommand { get; }

    public RelayCommand SwapCommand { get; }

    public RelayCommand DeleteCommand { get; }

    public RelayCommand RenumberCommand { get; }

    public RelayCommand GeneratePrecisionCommand { get; }

    public RelayCommand GenerateClearanceCommand { get; }

    public RelayCommand CreateTablesCommand { get; }

    public RelayCommand AlignCommand { get; }

    public RelayCommand UpdateCommand { get; }

    /// <summary>Runs Update for every view in <see cref="OutOfDateViews"/>.</summary>
    public RelayCommand UpdateOutOfDateCommand { get; }

    /// <summary>Clears the check's message so the user can go on without updating.</summary>
    public RelayCommand DismissStoredCheckCommand { get; }

    /// <summary>Opens <see cref="SelectedStoredView"/> on the Edit Table tab.</summary>
    public RelayCommand OpenStoredViewCommand { get; }

    public RelayCommand CancelCommand { get; }

    private void LoadViews()
    {
        Views.Clear();
        if (_selectedSheet is not null)
        {
            try
            {
                foreach (var view in _source.GetViews(_selectedSheet))
                {
                    Views.Add(view);
                }
            }
            catch (Exception ex)
            {
                Views.Clear();
                ClearAnalysis(FailureText("list the views", ex));
                _selectedView = null;
                OnPropertyChanged(nameof(SelectedView));
                RaiseCommandStates();
                return;
            }
        }

        // The first view is a convenience; the user can change it.
        var first = Views.FirstOrDefault()?.Key;
        if (!SetProperty(ref _selectedView, first, nameof(SelectedView)))
        {
            // Same name on the new sheet still counts as a new view.
            OnPropertyChanged(nameof(SelectedView));
        }

        ClearAnalysis(NoViewSelectedText);
        RaiseCommandStates();
        RefreshStoredViews();
    }

    private void Analyze()
    {
        // A view with stored tables opens as the Edit Table tab does, so its tags and edits are kept.
        if (_selectedSheet is not null && _selectedView is not null && HasStoredState(_selectedSheet, _selectedView))
        {
            OpenStoredView(_selectedView);
            return;
        }

        if (!ReadView(out var analysis))
        {
            return;
        }

        _edits = HoleTableEdits.None;
        _tags = null;
        Rebuild(null);
        Status = FoundText(analysis);
    }

    private bool HasStoredState(string sheet, string view)
    {
        try
        {
            return _source.ReadState(sheet, view) is not null;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>Clears the lists, reads the selected view and keeps the analysis; false (with the status set) when it cannot be used.</summary>
    private bool ReadView(out HoleTableViewAnalysis analysis)
    {
        analysis = null!;
        if (_selectedSheet is null || _selectedView is null)
        {
            return false;
        }

        ClearAnalysis(NoViewSelectedText);
        try
        {
            analysis = _source.Analyze(_selectedSheet, _selectedView);
        }
        catch (Exception ex)
        {
            Status = FailureText("analyse the view", ex);
            return false;
        }

        if (analysis.FailureMessage is not null)
        {
            Status = analysis.FailureMessage;
            return false;
        }

        var origin = HoleTableOriginResult.From(analysis.Origins, analysis.OriginUnreadable);
        if (origin.IsStop)
        {
            Status = origin.StopMessage!;
            return false;
        }

        _analysis = analysis;
        _origin = origin.Origin!.Value;
        return true;
    }

    private static string FoundText(HoleTableViewAnalysis analysis)
    {
        var found = analysis.Holes.Count == 1 ? "Found 1 hole." : $"Found {analysis.Holes.Count} holes.";
        return analysis.LeftOutCount > 0 ? $"{found} {HoleTableMessages.HolesLeftOut(analysis.LeftOutCount)}" : found;
    }

    /// <summary>The Edit Table tab: opens a view that has stored tables, keeping its tags, swaps, deletes, splits and notes.</summary>
    public void OpenStoredView(string viewKey)
    {
        if (_selectedSheet is null)
        {
            return;
        }

        if (_selectedView != viewKey)
        {
            SelectedView = viewKey;
        }

        HoleTableState? state;
        try
        {
            state = _source.ReadState(_selectedSheet, viewKey);
        }
        catch (Exception ex)
        {
            Status = FailureText("read the stored tables", ex);
            return;
        }

        if (state is null)
        {
            Status = NoStoredTablesText;
            return;
        }

        if (!ReadView(out var analysis))
        {
            return;
        }

        _edits = state.Edits;
        _tags = state.Tags;
        Rebuild(null);
        Status = $"{FoundText(analysis)} Opened the stored tables. {RefreshText()}";
    }

    /// <summary>
    /// Update (spec 04 question 18): reads the view again, keeps every remaining hole's tag, and rewrites the tables. It uses
    /// this window's edits and tags when the view is open here, else the ones stored on the view. True when the tables were
    /// rewritten; <see cref="Status"/> says what happened either way.
    /// </summary>
    private bool UpdateSelected()
    {
        if (_selectedSheet is null || _selectedView is null)
        {
            return false;
        }

        var sheet = _selectedSheet;
        var view = _selectedView;
        var edits = _edits;
        var tags = _tags;
        if (tags is null)
        {
            HoleTableState? stored;
            try
            {
                stored = _source.ReadState(sheet, view);
            }
            catch (Exception ex)
            {
                Status = FailureText("read the stored tables", ex);
                return false;
            }

            if (stored is null)
            {
                Status = NoStoredTablesText;
                return false;
            }

            // The tags come from storage, so the edits made with them do too.
            tags = stored.Tags;
            edits = stored.Edits;
        }

        if (!ReadView(out var analysis))
        {
            return false;
        }

        _edits = edits;
        _tags = tags;
        Rebuild(null);
        var summary = RefreshText();
        var state = CurrentState();
        var plan = _plan!;
        var ok = TryRun("update the tables", () => _source.UpdateTables(sheet, view, plan, state), summary);
        if (ok)
        {
            _tags = plan.ToStoredTags();
        }

        RefreshStoredViews();
        return ok;
    }

    /// <summary>
    /// Update for a stored view that is not open in this window: the same <see cref="HoleTablePlan.Refresh"/> and the same
    /// <see cref="IHoleTableSource.UpdateTables"/>, using the edits and tags stored on the view. It leaves the window's own lists alone.
    /// </summary>
    private bool UpdateStored(string sheet, string view, string sheetName, string viewName, out string text)
    {
        try
        {
            var state = _source.ReadState(sheet, view);
            if (state is null)
            {
                text = NoStoredTablesText;
                return false;
            }

            var analysis = _source.Analyze(sheet, view);
            if (analysis.FailureMessage is not null)
            {
                text = analysis.FailureMessage;
                return false;
            }

            var origin = HoleTableOriginResult.From(analysis.Origins, analysis.OriginUnreadable);
            if (origin.IsStop)
            {
                text = origin.StopMessage!;
                return false;
            }

            var refresh = HoleTablePlan.Refresh(analysis.Holes, state.Edits, state.Tags, origin.Origin!.Value, analysis.ViewScale, analysis.DrawingUnits);
            var next = HoleTableState.From(sheetName, viewName, refresh.Edits, refresh.Plan);
            var result = _source.UpdateTables(sheet, view, refresh.Plan, next);
            text = $"{result} {refresh.Summary}";
            return true;
        }
        catch (Exception ex)
        {
            text = FailureText("update the tables", ex);
            return false;
        }
    }

    /// <summary>
    /// The on-open check (task 171): for every sheet and every view with a stored state, reads the view again and compares it
    /// with what was drawn. Only <see cref="IHoleTableSource.GetKeyedViews"/>, <see cref="IHoleTableSource.ReadState"/> and
    /// <see cref="IHoleTableSource.Analyze"/> are called; no table or state is written (the default <c>GetKeyedViews</c> is
    /// <c>GetViews</c>, which in the add-in may assign view keys until task 172 overrides it). A view that cannot be read is listed as could not be checked, never as up to date. The window calls this
    /// after it opens, so it can show it is busy.
    /// </summary>
    public void CheckStoredTables()
    {
        OutOfDateViews.Clear();
        CouldNotCheckViews.Clear();
        foreach (var sheet in Sheets)
        {
            IReadOnlyList<HoleTableViewInfo> views;
            try
            {
                views = _source.GetKeyedViews(sheet.Key);
            }
            catch (Exception ex)
            {
                CouldNotCheckViews.Add(new(sheet.Key, sheet.Name, "", "All views", FailureText("list the views", ex)));
                continue;
            }

            foreach (var view in views)
            {
                HoleTableStaleness result;
                try
                {
                    var state = _source.ReadState(sheet.Key, view.Key);
                    if (state is null)
                    {
                        continue;
                    }

                    result = HoleTableStaleness.Check(state, _source.Analyze(sheet.Key, view.Key));
                }
                catch (Exception ex)
                {
                    result = HoleTableStaleness.CouldNot(FailureText("check the stored tables", ex));
                }

                var item = new HoleTableStoredViewCheck(sheet.Key, sheet.Name, view.Key, view.Name, result.Text);
                if (result.Kind == HoleTableStalenessKind.OutOfDate)
                {
                    OutOfDateViews.Add(item);
                }
                else if (result.Kind == HoleTableStalenessKind.CouldNotCheck)
                {
                    CouldNotCheckViews.Add(item);
                }
            }
        }

        StoredCheckChanged();
    }

    private void UpdateOutOfDate()
    {
        var results = new List<string>();
        var failed = new List<HoleTableStoredViewCheck>();
        foreach (var item in OutOfDateViews.ToList())
        {
            bool ok;
            string text;
            if (item.SheetKey == _selectedSheet && item.ViewKey == _selectedView && _tags is not null)
            {
                // The view is open in the window: the button's own Update, so the window's edits are kept.
                ok = UpdateSelected();
                text = Status;
            }
            else
            {
                ok = UpdateStored(item.SheetKey, item.ViewKey, item.SheetName, item.ViewName, out text);
            }

            results.Add(HoleTableMessages.ViewLine(item.ViewName, item.SheetName, ok ? text : "Update failed. " + text));
            if (!ok)
            {
                failed.Add(item with { Reason = "Update failed. " + text });
            }
        }

        OutOfDateViews.Clear();
        foreach (var item in failed)
        {
            OutOfDateViews.Add(item);
        }

        RefreshStoredViews();
        Status = string.Join(" ", results);
        StoredCheckChanged();
    }

    private void DismissStoredCheck()
    {
        OutOfDateViews.Clear();
        CouldNotCheckViews.Clear();
        StoredCheckChanged();
    }

    private void StoredCheckChanged()
    {
        OnPropertyChanged(nameof(StoredCheckMessage));
        OnPropertyChanged(nameof(HasStoredCheckMessage));
        UpdateOutOfDateCommand.RaiseCanExecuteChanged();
        DismissStoredCheckCommand.RaiseCanExecuteChanged();
    }

    public const string NoStoredTablesText = "This view has no stored hole tables. Create them first.";

    private void Swap()
    {
        var row = SelectedRow;
        if (row is null)
        {
            return;
        }

        var swapped = _edits.SwappedIds.Contains(row.HoleId);
        _edits = _edits.WithSwapped(row.HoleId, !swapped);
        Rebuild(row.HoleId);
    }

    private void Delete()
    {
        var row = SelectedRow;
        if (row is null)
        {
            return;
        }

        _edits = _edits.WithDeleted(row.HoleId);
        Rebuild(null);
    }

    private void Renumber()
    {
        Rebuild(SelectedRow?.HoleId, renumber: true);
        Status = "Renumbered: every tag is reset from A1 and any gaps are closed.";
    }

    internal void SetSplit(string holeId, bool on)
    {
        _edits = _edits.WithSplit(holeId, on);
        Rebuild(holeId);
    }

    internal void SetNote(HoleTableWizardRow row, string text)
    {
        var group = row.Group;
        _edits = group.SplitHoleId is { } splitId
            ? _edits.WithSplitNote(splitId, text)
            : _edits.WithNote(group.Table, group.Description.Key, text);
        Rebuild(SelectedRow?.HoleId ?? row.HoleId);
    }

    internal void SetGroupNote(HoleTableGroup group, string text)
    {
        _edits = group.SplitHoleId is { } splitId
            ? _edits.WithSplitNote(splitId, text)
            : _edits.WithNote(group.Table, group.Description.Key, text);
        Rebuild(SelectedRow?.HoleId);
    }

    private void Generate(HoleTableKind kind)
    {
        if (_plan is not null && _selectedSheet is not null && _selectedView is not null)
        {
            var sheet = _selectedSheet;
            var view = _selectedView;
            var plan = _plan;
            var state = CurrentState();
            if (TryRun("generate the table", () => _source.GenerateTable(sheet, view, kind, plan, state)))
            {
                _tags = plan.ToStoredTags();
                RefreshStoredViews();
            }
        }
    }

    private void CreateTables()
    {
        if (_plan is not null && _selectedSheet is not null && _selectedView is not null)
        {
            var sheet = _selectedSheet;
            var view = _selectedView;
            var plan = _plan;
            var state = CurrentState();
            if (TryRun("create the tables", () => _source.CreateTables(sheet, view, plan, state)))
            {
                _tags = plan.ToStoredTags();
                RefreshStoredViews();
            }
        }
    }

    /// <summary>Runs a source call and sets the status to its result (and <paramref name="suffix"/>), or to the failure; true on success.</summary>
    private bool TryRun(string what, Func<string> call, string suffix = "")
    {
        try
        {
            var result = call();
            Status = string.IsNullOrEmpty(suffix) ? result : $"{result} {suffix}";
            return true;
        }
        catch (Exception ex)
        {
            Status = FailureText(what, ex);
            return false;
        }
    }

    private void Align()
    {
        if (_selectedSheet is not null && _selectedView is not null)
        {
            Status = Run("align the tables", () => _source.AlignTables(_selectedSheet, _selectedView));
        }
    }

    private static string Run(string what, Func<string> call)
    {
        try
        {
            return call();
        }
        catch (Exception ex)
        {
            return FailureText(what, ex);
        }
    }

    private static string FailureText(string what, Exception ex) =>
        $"Could not {what}: {ex.Message}";

    private void ClearAnalysis(string status)
    {
        _analysis = null;
        _plan = null;
        _edits = HoleTableEdits.None;
        _tags = null;
        _lastRefresh = null;
        Fill(null);
        Status = status;
    }

    private void Rebuild(string? keepSelectedId, bool renumber = false)
    {
        if (_analysis is null)
        {
            return;
        }

        _lastRefresh = null;
        if (_tags is null || renumber)
        {
            _plan = HoleTablePlan.Build(_analysis.Holes, _edits, _origin, _analysis.ViewScale, _analysis.DrawingUnits);
            if (_tags is not null)
            {
                _tags = _plan.ToStoredTags();
            }
        }
        else
        {
            _lastRefresh = HoleTablePlan.Refresh(_analysis.Holes, _edits, _tags, _origin, _analysis.ViewScale, _analysis.DrawingUnits);
            _plan = _lastRefresh.Plan;
            _edits = _lastRefresh.Edits;
        }

        Fill(keepSelectedId);
    }

    /// <summary>What the last Update or Edit Table refresh did, and any stale ids, for the status line.</summary>
    private string RefreshText()
    {
        var parts = new List<string>();
        if (_lastRefresh is not null)
        {
            parts.Add(_lastRefresh.Summary);
        }

        if (_plan is { StaleIds.Count: > 0 })
        {
            parts.Add(HoleTableMessages.StaleIdsDropped(_plan.StaleIds.Count));
        }

        return string.Join(" ", parts);
    }

    private HoleTableState CurrentState()
    {
        var sheet = Sheets.FirstOrDefault(s => s.Key == _selectedSheet)?.Name ?? "";
        var view = Views.FirstOrDefault(v => v.Key == _selectedView)?.Name ?? "";
        return HoleTableState.From(sheet, view, _edits, _plan!);
    }

    /// <summary>Reads which views of the selected sheet have stored tables. Called when the sheet changes and after Create and Update.</summary>
    public void RefreshStoredViews()
    {
        StoredViews.Clear();
        if (_selectedSheet is null)
        {
            return;
        }

        foreach (var view in Views)
        {
            try
            {
                if (_source.ReadState(_selectedSheet, view.Key) is not null)
                {
                    StoredViews.Add(view);
                }
            }
            catch (Exception)
            {
                // A view whose state cannot be read is simply not listed.
            }
        }
    }

    private void Fill(string? keepSelectedId)
    {
        _updatingSelection = true;
        try
        {
            _selectedPrecision = null;
            _selectedClearance = null;
            PrecisionRows.Clear();
            ClearanceRows.Clear();
            Groups.Clear();
            if (_plan is not null)
            {
                foreach (var group in _plan.AllGroups)
                {
                    Groups.Add(new HoleTableWizardGroup(this, group));
                }

                foreach (var group in _plan.Precision)
                {
                    foreach (var row in group.Rows)
                    {
                        PrecisionRows.Add(new HoleTableWizardRow(this, group, row));
                    }
                }

                foreach (var group in _plan.Clearance)
                {
                    foreach (var row in group.Rows)
                    {
                        ClearanceRows.Add(new HoleTableWizardRow(this, group, row));
                    }
                }
            }

            if (keepSelectedId is not null)
            {
                _selectedPrecision = PrecisionRows.FirstOrDefault(r => r.HoleId == keepSelectedId);
                _selectedClearance = _selectedPrecision is null
                    ? ClearanceRows.FirstOrDefault(r => r.HoleId == keepSelectedId)
                    : null;
            }
        }
        finally
        {
            _updatingSelection = false;
        }

        OnPropertyChanged(nameof(SelectedPrecisionRow));
        OnPropertyChanged(nameof(SelectedClearanceRow));
        OnPropertyChanged(nameof(SelectedRow));
        OnPropertyChanged(nameof(Plan));
        RaiseCommandStates();
    }

    private void SelectionChanged()
    {
        OnPropertyChanged(nameof(SelectedRow));
        SwapCommand.RaiseCanExecuteChanged();
        DeleteCommand.RaiseCanExecuteChanged();
    }

    private void RaiseCommandStates()
    {
        AnalyzeCommand.RaiseCanExecuteChanged();
        SwapCommand.RaiseCanExecuteChanged();
        DeleteCommand.RaiseCanExecuteChanged();
        RenumberCommand.RaiseCanExecuteChanged();
        GeneratePrecisionCommand.RaiseCanExecuteChanged();
        GenerateClearanceCommand.RaiseCanExecuteChanged();
        CreateTablesCommand.RaiseCanExecuteChanged();
        AlignCommand.RaiseCanExecuteChanged();
        UpdateCommand.RaiseCanExecuteChanged();
    }
}
