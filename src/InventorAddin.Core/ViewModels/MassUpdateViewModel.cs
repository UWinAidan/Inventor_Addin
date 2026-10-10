using InventorAddin.Core.Numbering;
using InventorAddin.Core.Numbering.MassUpdate;
using InventorAddin.Core.PartProperties;

namespace InventorAddin.Core.ViewModels;

/// <summary>The outcome of one file's update, reported back by the add-in after Apply.</summary>
/// <param name="FullPath">The file's full path, as in the plan.</param>
/// <param name="Error">Why the update failed, or null when it was done.</param>
public sealed record FileUpdateResult(string FullPath, string? Error = null);

/// <summary>Carries the plan the user asked to apply.</summary>
public sealed class MassUpdateApplyEventArgs : EventArgs
{
    public MassUpdateApplyEventArgs(MassUpdatePlan plan) => Plan = plan;

    public MassUpdatePlan Plan { get; }
}

/// <summary>Carries the folder the user asked to scan.</summary>
public sealed class MassUpdateScanEventArgs : EventArgs
{
    public MassUpdateScanEventArgs(string folder) => Folder = folder;

    public string Folder { get; }
}

/// <summary>One scanned file in the Mass Update list: its state, three tick boxes, and the result after Apply.</summary>
public sealed class MassUpdateRow : ObservableObject
{
    public const string HasNumberText = "Has a number";
    public const string NotInRegisterText = "Not in the register";
    public const string NoNumberText = "No number";
    public const string DoneText = "Done";

    private bool _register;
    private bool _assign;
    private bool _replaceNames;
    private string? _resultText;
    private bool? _succeeded;

    /// <param name="state">The file's state.</param>
    /// <param name="defaultDesignerIsSet">False when the Default designer is blank, so Replace names is not offered.</param>
    /// <param name="scanDuplicate">
    /// Set when an earlier scanned file carries the same unregistered number (<see cref="MassUpdatePlan.ScanDuplicates"/>):
    /// Register is then not offered, and the state says why.
    /// </param>
    public MassUpdateRow(ScannedFileState state, bool defaultDesignerIsSet, MassUpdateDuplicate? scanDuplicate = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        State = state;
        ScanDuplicate = scanDuplicate;
        CanReplaceNames = defaultDesignerIsSet && state.Offers(MassUpdateActions.ReplaceNames);
        ReplaceNamesNote = !defaultDesignerIsSet && state.Offers(MassUpdateActions.ReplaceNames)
            ? MassUpdateViewModel.ReplaceNamesNeedsDefaultDesignerText
            : null;
    }

    public ScannedFileState State { get; }

    /// <summary>The earlier file carrying this file's unregistered number, or null.</summary>
    public MassUpdateDuplicate? ScanDuplicate { get; }

    public string FullPath => State.File.FullPath;

    /// <summary>The file name with its extension, without the folder.</summary>
    public string FileName => FullPath[(FullPath.LastIndexOfAny(['\\', '/']) + 1)..];

    /// <summary>The Part Number as read, trimmed.</summary>
    public string PartNumberText => PropertyValues.NormaliseText(State.File.PartNumber);

    /// <summary>
    /// The file's state: the reason it offers no action, or the number state and, when present, which names mean the
    /// user, joined with <c>"; "</c>. A name that means the user (the Windows user name or one of the Other names) is
    /// called "your name" (Default, task 048). A number an earlier file also carries, unregistered, reads
    /// <see cref="MassUpdateViewModel.DuplicateText"/>.
    /// </summary>
    public string StateText
    {
        get
        {
            if (State.BlockedReason is { } reason)
            {
                return reason;
            }

            var number = ScanDuplicate is { } duplicate
                ? MassUpdateViewModel.DuplicateText(duplicate)
                : State.NumberState switch
                {
                    NumberState.Registered => HasNumberText,
                    NumberState.WellFormedUnregistered => NotInRegisterText,
                    _ => NoNumberText,
                };

            var names = (State.DesignerIsMe, State.DetailerIsMe) switch
            {
                (true, true) => "your name as Designer and Detailer",
                (true, false) => "your name as Designer",
                (false, true) => "your name as Detailer",
                _ => null,
            };

            return names is null ? number : $"{number}; {names}";
        }
    }

    /// <summary>False when Register does not apply, or an earlier file carries the same number.</summary>
    public bool CanRegister => State.Offers(MassUpdateActions.Register) && ScanDuplicate is null;

    public bool CanAssign => State.Offers(MassUpdateActions.Assign);

    /// <summary>False when neither name means the user, or the Default designer is blank.</summary>
    public bool CanReplaceNames { get; }

    /// <summary>
    /// <see cref="MassUpdateViewModel.ReplaceNamesNeedsDefaultDesignerText"/> when a name in the file means the user
    /// but the Default designer is blank; null otherwise.
    /// </summary>
    public string? ReplaceNamesNote { get; }

    /// <summary>True when the row shows a tick box or the Replace names note; the window hides the line otherwise.</summary>
    public bool HasActions => CanRegister || CanAssign || CanReplaceNames || ReplaceNamesNote is not null;

    /// <summary>Ticked to register the file's number. Stays false when the action does not apply.</summary>
    public bool Register
    {
        get => _register;
        set => SetProperty(ref _register, value && CanRegister);
    }

    /// <summary>Ticked to assign a new number. Stays false when the action does not apply.</summary>
    public bool Assign
    {
        get => _assign;
        set => SetProperty(ref _assign, value && CanAssign);
    }

    /// <summary>Ticked to replace the names that mean the user. Stays false when the action is not offered.</summary>
    public bool ReplaceNames
    {
        get => _replaceNames;
        set => SetProperty(ref _replaceNames, value && CanReplaceNames);
    }

    /// <summary>The ticked actions as flags.</summary>
    public MassUpdateActions Ticked =>
        (Register ? MassUpdateActions.Register : MassUpdateActions.None)
        | (Assign ? MassUpdateActions.Assign : MassUpdateActions.None)
        | (ReplaceNames ? MassUpdateActions.ReplaceNames : MassUpdateActions.None);

    /// <summary>After Apply: <see cref="DoneText"/>, the error, or why the number was not registered. Null before.</summary>
    public string? ResultText
    {
        get => _resultText;
        private set => SetProperty(ref _resultText, value);
    }

    /// <summary>After Apply: true when done, false on an error. Null before.</summary>
    public bool? Succeeded
    {
        get => _succeeded;
        private set
        {
            if (SetProperty(ref _succeeded, value))
            {
                OnPropertyChanged(nameof(ResultIsDone));
                OnPropertyChanged(nameof(ResultIsError));
            }
        }
    }

    /// <summary>True when Apply reported this file done; for the window, which shows the result in the muted style.</summary>
    public bool ResultIsDone => _succeeded == true;

    /// <summary>True when Apply reported an error for this file; for the window, which shows it in the error style.</summary>
    public bool ResultIsError => _succeeded == false;

    internal void SetResult(string text, bool succeeded)
    {
        ResultText = text;
        Succeeded = succeeded;
    }
}

/// <summary>
/// View-model for the Mass Update window (spec 02, "Commands: Mass Update"). Independent of Inventor: the add-in
/// scans when <see cref="ScanRequested"/> fires and hands the files to <see cref="ShowScan"/>, applies the plan
/// carried by <see cref="ApplyRequested"/> after confirming with <see cref="ConfirmText"/>, then reports each file
/// back through <see cref="ReportResults"/>. While the add-in scans or applies it calls <see cref="BeginWork"/> and
/// <see cref="EndWork"/>, which disable the other commands, and shows progress with <see cref="ShowStatus"/>; a scan
/// stops between files once <see cref="IsCancelRequested"/> is true (<see cref="CancelCommand"/>).
/// </summary>
public sealed class MassUpdateViewModel : ObservableObject
{
    public const string ReplaceNamesNeedsDefaultDesignerText = "Set Default designer in Settings first.";
    public const string HeaderTitleText = "Mass Update";
    public const string HeaderSublineText = "Register, number and fix names in a folder of files";
    public const string CancellingText = "Stopping after this file...";
    public const string ScanAgainText = "Scan again to make more changes.";
    public const string NoFilesText = "No part or assembly files in this folder.";

    private readonly IReadOnlyList<string> _namesThatMeanMe;
    private readonly string _defaultDesigner;
    private readonly RelayCommand _scanCommand;
    private readonly RelayCommand _applyCommand;
    private readonly RelayCommand _cancelCommand;

    private string _folder;
    private IReadOnlyList<MassUpdateRow> _rows = Array.Empty<MassUpdateRow>();
    private bool _isBusy;
    private bool _canCancel;
    private bool _isCancelRequested;
    private bool _applied;
    private string? _statusText;
    private bool _statusIsError;

    /// <param name="defaultFolder">The folder shown at open: the Work folder from Settings.</param>
    /// <param name="namesThatMeanMe">
    /// The Windows user name and the Other names from Settings (<see cref="PersonNames.NamesThatMeanMe"/>).
    /// </param>
    /// <param name="defaultDesigner">The Default designer from Settings, which replaces those names.</param>
    public MassUpdateViewModel(string? defaultFolder, IReadOnlyCollection<string> namesThatMeanMe, string? defaultDesigner)
    {
        ArgumentNullException.ThrowIfNull(namesThatMeanMe);
        _folder = defaultFolder ?? "";
        _namesThatMeanMe = namesThatMeanMe.ToList().AsReadOnly();
        _defaultDesigner = PropertyValues.NormaliseText(defaultDesigner);
        _scanCommand = new RelayCommand(RequestScan, () => !_isBusy && PropertyValues.NormaliseText(_folder).Length > 0);
        _applyCommand = new RelayCommand(RequestApply, () => !_isBusy && !_applied && UpdateCount > 0);
        _cancelCommand = new RelayCommand(RequestCancel, () => _isBusy && _canCancel && !_isCancelRequested);
    }

    public string HeaderTitle => HeaderTitleText;

    public string HeaderSubline => HeaderSublineText;

    /// <summary>Raised by <see cref="ScanCommand"/>. The add-in scans the folder and calls <see cref="ShowScan"/>.</summary>
    public event EventHandler<MassUpdateScanEventArgs>? ScanRequested;

    /// <summary>
    /// Raised by <see cref="ApplyCommand"/> with the plan for the current ticks. The add-in confirms with
    /// <see cref="ConfirmText"/>, applies the plan, and calls <see cref="ReportResults"/>.
    /// </summary>
    public event EventHandler<MassUpdateApplyEventArgs>? ApplyRequested;

    /// <summary>The folder to scan. Browse sets it.</summary>
    public string Folder
    {
        get => _folder;
        set
        {
            if (SetProperty(ref _folder, value ?? ""))
            {
                _scanCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public RelayCommand ScanCommand => _scanCommand;

    /// <summary>
    /// Apply is disabled while busy, with nothing ticked, and after its results are reported until the next scan,
    /// because the files have changed (an Assign applied twice would give a file two numbers).
    /// </summary>
    public RelayCommand ApplyCommand => _applyCommand;

    /// <summary>Stops a scan between files. Enabled only while a scan runs.</summary>
    public RelayCommand CancelCommand => _cancelCommand;

    /// <summary>True while the add-in scans or applies. The folder, Browse, Scan, Apply and Close are disabled.</summary>
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(IsIdle));
                OnPropertyChanged(nameof(CanCancel));
                RaiseCommandStates();
            }
        }
    }

    /// <summary>The opposite of <see cref="IsBusy"/>, for the window's bindings.</summary>
    public bool IsIdle => !_isBusy;

    /// <summary>True while a scan runs, so the Stop button is shown.</summary>
    public bool CanCancel => _isBusy && _canCancel;

    /// <summary>Set by <see cref="CancelCommand"/>; the add-in stops the scan after the file it is reading.</summary>
    public bool IsCancelRequested => _isCancelRequested;

    /// <summary>The footer's status line: progress, a result or a problem. Null when there is nothing to say.</summary>
    public string? StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    /// <summary>True when <see cref="StatusText"/> is a problem, shown in the error style.</summary>
    public bool StatusIsError
    {
        get => _statusIsError;
        private set => SetProperty(ref _statusIsError, value);
    }

    /// <summary>True once a scan has listed at least one file; the file list is shown then.</summary>
    public bool HasRows => _rows.Count > 0;

    /// <summary>The add-in starts a scan (<paramref name="cancellable"/>) or an apply.</summary>
    public void BeginWork(bool cancellable)
    {
        _canCancel = cancellable;
        _isCancelRequested = false;
        OnPropertyChanged(nameof(IsCancelRequested));
        IsBusy = true;
        ShowStatus(null);
    }

    /// <summary>The add-in finished the scan or apply.</summary>
    public void EndWork()
    {
        _canCancel = false;
        IsBusy = false;
    }

    /// <summary>Shows <paramref name="text"/> on the status line, in the error style when <paramref name="isError"/>.</summary>
    public void ShowStatus(string? text, bool isError = false)
    {
        StatusText = string.IsNullOrWhiteSpace(text) ? null : text;
        StatusIsError = isError && StatusText is not null;
    }

    /// <summary>For example <c>"Reading 3 of 12"</c>.</summary>
    public static string ScanProgressText(int current, int total) => $"Reading {current} of {total}";

    /// <summary>For example <c>"Updating 3 of 7"</c>.</summary>
    public static string ApplyProgressText(int current, int total) => $"Updating {current} of {total}";

    /// <summary>The status after a scan: cancelled part way, no files, or nothing (the counts are shown above the list).</summary>
    public static string? ScanDoneText(int read, int total, bool cancelled) =>
        cancelled ? $"Scan stopped after {read} of {total} files."
        : total == 0 ? NoFilesText
        : null;

    /// <summary>One row per scanned file, in the order scanned.</summary>
    public IReadOnlyList<MassUpdateRow> Rows => _rows;

    /// <summary>
    /// <see cref="ReplaceNamesNeedsDefaultDesignerText"/> when the Default designer is blank, so Replace names is
    /// not offered on any file; null otherwise.
    /// </summary>
    public string? ReplaceNamesDisabledReason =>
        _defaultDesigner.Length == 0 ? ReplaceNamesNeedsDefaultDesignerText : null;

    /// <summary>True when <see cref="ReplaceNamesDisabledReason"/> is set; for the window.</summary>
    public bool HasReplaceNamesDisabledReason => ReplaceNamesDisabledReason is not null;

    /// <summary>
    /// For example <c>"12 files, 3 to register, 5 to number, 7 with your name"</c>: every file listed, then the files
    /// each action applies to ("with your name" counts the Windows user name and the Other names). Files open in
    /// Inventor or not read count only in the first figure; a file whose number an earlier file also carries does not
    /// count to register. Empty before a scan.
    /// </summary>
    public string CountsText
    {
        get
        {
            if (_rows.Count == 0)
            {
                return "";
            }

            var files = _rows.Count == 1 ? "1 file" : $"{_rows.Count} files";
            var register = _rows.Count(r => r.CanRegister);
            var assign = _rows.Count(r => r.CanAssign);
            var names = _rows.Count(r => r.State.Offers(MassUpdateActions.ReplaceNames));
            return $"{files}, {register} to register, {assign} to number, {names} with your name";
        }
    }

    /// <summary>How many files the current ticks would change.</summary>
    public int UpdateCount => BuildPlan().Updates.Count;

    /// <summary>The confirmation asked before Apply, with the number of files the current ticks would change.</summary>
    public string ConfirmText
    {
        get
        {
            var count = UpdateCount;
            var files = count == 1 ? "1 file" : $"{count} files";
            var them = count == 1 ? "it" : "them";
            return $"Change {files}? This saves {them} and cannot be undone with Undo.";
        }
    }

    /// <summary>Ticks or unticks Register on every row it applies to. True when every such row is ticked.</summary>
    public bool TickAllRegister
    {
        get => AllTicked(r => r.CanRegister, r => r.Register);
        set
        {
            SetAll(r => r.Register = value);

            // Raised even when no row changed, so a tick box on a list with nothing to tick goes back to clear.
            OnTicksChanged();
        }
    }

    /// <summary>Ticks or unticks Assign on every row it applies to. True when every such row is ticked.</summary>
    public bool TickAllAssign
    {
        get => AllTicked(r => r.CanAssign, r => r.Assign);
        set
        {
            SetAll(r => r.Assign = value);

            // Raised even when no row changed, so a tick box on a list with nothing to tick goes back to clear.
            OnTicksChanged();
        }
    }

    /// <summary>Ticks or unticks Replace names on every row it is offered on. True when every such row is ticked.</summary>
    public bool TickAllReplaceNames
    {
        get => AllTicked(r => r.CanReplaceNames, r => r.ReplaceNames);
        set
        {
            SetAll(r => r.ReplaceNames = value);

            // Raised even when no row changed, so a tick box on a list with nothing to tick goes back to clear.
            OnTicksChanged();
        }
    }

    /// <summary>The plan made by the last Apply, or null before one.</summary>
    public MassUpdatePlan? LastPlan { get; private set; }

    /// <summary>
    /// Shows the scan result: one row per file, in the order given, with nothing ticked. <paramref name="register"/>
    /// is the register as synced for this run. A file carrying an unregistered number that an earlier file also
    /// carries says so and offers no Register (<see cref="MassUpdatePlan.ScanDuplicates"/>). Apply is enabled again.
    /// </summary>
    public void ShowScan(IEnumerable<ScannedFile> files, PartRegister register)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(register);

        foreach (var row in _rows)
        {
            row.PropertyChanged -= OnRowChanged;
        }

        var states = files.Select(f => ScannedFileState.For(f, register, _namesThatMeanMe)).ToList();
        var duplicates = MassUpdatePlan.ScanDuplicates(states);
        var rows = states
            .Select(state => new MassUpdateRow(
                state,
                _defaultDesigner.Length > 0,
                duplicates.TryGetValue(state.File.FullPath, out var duplicate) ? duplicate : null))
            .ToList();
        foreach (var row in rows)
        {
            row.PropertyChanged += OnRowChanged;
        }

        _rows = rows.AsReadOnly();
        LastPlan = null;
        _applied = false;
        OnPropertyChanged(nameof(Rows));
        OnPropertyChanged(nameof(HasRows));
        OnPropertyChanged(nameof(CountsText));
        OnPropertyChanged(nameof(LastPlan));
        OnTicksChanged();
    }

    /// <summary>The plan for the rows as currently ticked.</summary>
    public MassUpdatePlan BuildPlan() =>
        MassUpdatePlan.Build(_rows.Select(r => (r.State, r.Ticked)), _defaultDesigner);

    /// <summary>
    /// Shows each file's result on its row (matched by path, ignoring case): <see cref="MassUpdateRow.DoneText"/> or
    /// the error. Apply stays disabled until the next scan (<see cref="ApplyCommand"/>).
    /// </summary>
    public void ReportResults(IEnumerable<FileUpdateResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        var byPath = new Dictionary<string, FileUpdateResult>(StringComparer.OrdinalIgnoreCase);
        foreach (var result in results)
        {
            byPath[result.FullPath] = result;
        }

        foreach (var row in _rows)
        {
            if (byPath.TryGetValue(row.FullPath, out var result))
            {
                if (string.IsNullOrWhiteSpace(result.Error))
                {
                    row.SetResult(MassUpdateRow.DoneText, succeeded: true);
                }
                else
                {
                    row.SetResult(result.Error.Trim(), succeeded: false);
                }
            }
        }

        _applied = true;
        _applyCommand.RaiseCanExecuteChanged();
    }

    /// <summary>
    /// A file's number that an earlier scanned file also carries, unregistered, for example
    /// <c>"Not registered: AWB-10005 is also on a.ipt"</c>.
    /// </summary>
    public static string DuplicateText(MassUpdateDuplicate duplicate)
    {
        ArgumentNullException.ThrowIfNull(duplicate);
        var first = duplicate.FirstPath[(duplicate.FirstPath.LastIndexOfAny(['\\', '/']) + 1)..];
        return $"Not registered: {duplicate.Number} is also on {first}";
    }

    private void RequestScan() =>
        ScanRequested?.Invoke(this, new MassUpdateScanEventArgs(PropertyValues.NormaliseText(_folder)));

    private void RequestCancel()
    {
        _isCancelRequested = true;
        OnPropertyChanged(nameof(IsCancelRequested));
        _cancelCommand.RaiseCanExecuteChanged();
        ShowStatus(CancellingText);
    }

    private void RaiseCommandStates()
    {
        _scanCommand.RaiseCanExecuteChanged();
        _applyCommand.RaiseCanExecuteChanged();
        _cancelCommand.RaiseCanExecuteChanged();
    }

    private void RequestApply()
    {
        LastPlan = BuildPlan();
        OnPropertyChanged(nameof(LastPlan));
        ApplyRequested?.Invoke(this, new MassUpdateApplyEventArgs(LastPlan));
    }

    private bool AllTicked(Func<MassUpdateRow, bool> applies, Func<MassUpdateRow, bool> ticked)
    {
        var applicable = _rows.Where(applies).ToList();
        return applicable.Count > 0 && applicable.All(ticked);
    }

    private void SetAll(Action<MassUpdateRow> set)
    {
        foreach (var row in _rows)
        {
            set(row);
        }
    }

    private void OnRowChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MassUpdateRow.Register)
            or nameof(MassUpdateRow.Assign)
            or nameof(MassUpdateRow.ReplaceNames))
        {
            OnTicksChanged();
        }
    }

    private void OnTicksChanged()
    {
        OnPropertyChanged(nameof(TickAllRegister));
        OnPropertyChanged(nameof(TickAllAssign));
        OnPropertyChanged(nameof(TickAllReplaceNames));
        OnPropertyChanged(nameof(UpdateCount));
        OnPropertyChanged(nameof(ConfirmText));
        _applyCommand.RaiseCanExecuteChanged();
    }
}
