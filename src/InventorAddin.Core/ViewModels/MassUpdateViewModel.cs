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

    public MassUpdateRow(ScannedFileState state, bool defaultDesignerIsSet)
    {
        ArgumentNullException.ThrowIfNull(state);
        State = state;
        CanReplaceNames = defaultDesignerIsSet && state.Offers(MassUpdateActions.ReplaceNames);
        ReplaceNamesNote = !defaultDesignerIsSet && state.Offers(MassUpdateActions.ReplaceNames)
            ? MassUpdateViewModel.ReplaceNamesNeedsDefaultDesignerText
            : null;
    }

    public ScannedFileState State { get; }

    public string FullPath => State.File.FullPath;

    /// <summary>The file name with its extension, without the folder.</summary>
    public string FileName => FullPath[(FullPath.LastIndexOfAny(['\\', '/']) + 1)..];

    /// <summary>The Part Number as read, trimmed.</summary>
    public string PartNumberText => PropertyValues.NormaliseText(State.File.PartNumber);

    /// <summary>
    /// The file's state: the reason it offers no action, or the number state and, when present, which names mean the
    /// user (the Windows user name or one of the Other names), joined with <c>"; "</c>. The text still says
    /// "Windows user name" for either.
    /// </summary>
    public string StateText
    {
        get
        {
            if (State.BlockedReason is { } reason)
            {
                return reason;
            }

            var number = State.NumberState switch
            {
                NumberState.Registered => HasNumberText,
                NumberState.WellFormedUnregistered => NotInRegisterText,
                _ => NoNumberText,
            };

            var names = (State.DesignerIsMe, State.DetailerIsMe) switch
            {
                (true, true) => "Windows user name as Designer and Detailer",
                (true, false) => "Windows user name as Designer",
                (false, true) => "Windows user name as Detailer",
                _ => null,
            };

            return names is null ? number : $"{number}; {names}";
        }
    }

    public bool CanRegister => State.Offers(MassUpdateActions.Register);

    public bool CanAssign => State.Offers(MassUpdateActions.Assign);

    /// <summary>False when neither name means the user, or the Default designer is blank.</summary>
    public bool CanReplaceNames { get; }

    /// <summary>
    /// <see cref="MassUpdateViewModel.ReplaceNamesNeedsDefaultDesignerText"/> when a name in the file means the user
    /// but the Default designer is blank; null otherwise.
    /// </summary>
    public string? ReplaceNamesNote { get; }

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

    /// <summary>After Apply: true when done, false on an error or a duplicate number. Null before.</summary>
    public bool? Succeeded
    {
        get => _succeeded;
        private set => SetProperty(ref _succeeded, value);
    }

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
/// back through <see cref="ReportResults"/>.
/// </summary>
public sealed class MassUpdateViewModel : ObservableObject
{
    public const string ReplaceNamesNeedsDefaultDesignerText = "Set Default designer in Settings first.";

    private readonly IReadOnlyList<string> _namesThatMeanMe;
    private readonly string _defaultDesigner;
    private readonly RelayCommand _scanCommand;
    private readonly RelayCommand _applyCommand;

    private string _folder;
    private IReadOnlyList<MassUpdateRow> _rows = Array.Empty<MassUpdateRow>();

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
        _scanCommand = new RelayCommand(RequestScan, () => PropertyValues.NormaliseText(_folder).Length > 0);
        _applyCommand = new RelayCommand(RequestApply, () => UpdateCount > 0);
    }

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

    public RelayCommand ApplyCommand => _applyCommand;

    /// <summary>One row per scanned file, in the order scanned.</summary>
    public IReadOnlyList<MassUpdateRow> Rows => _rows;

    /// <summary>
    /// <see cref="ReplaceNamesNeedsDefaultDesignerText"/> when the Default designer is blank, so Replace names is
    /// not offered on any file; null otherwise.
    /// </summary>
    public string? ReplaceNamesDisabledReason =>
        _defaultDesigner.Length == 0 ? ReplaceNamesNeedsDefaultDesignerText : null;

    /// <summary>
    /// For example <c>"12 files, 3 to register, 5 to number, 7 with the Windows user name"</c>: every file listed,
    /// then the files each action applies to ("with the Windows user name" counts the Other names too). Files open in
    /// Inventor or not read count only in the first figure.
    /// Empty before a scan.
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
            return $"{files}, {register} to register, {assign} to number, {names} with the Windows user name";
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
        set => SetAll(r => r.Register = value);
    }

    /// <summary>Ticks or unticks Assign on every row it applies to. True when every such row is ticked.</summary>
    public bool TickAllAssign
    {
        get => AllTicked(r => r.CanAssign, r => r.Assign);
        set => SetAll(r => r.Assign = value);
    }

    /// <summary>Ticks or unticks Replace names on every row it is offered on. True when every such row is ticked.</summary>
    public bool TickAllReplaceNames
    {
        get => AllTicked(r => r.CanReplaceNames, r => r.ReplaceNames);
        set => SetAll(r => r.ReplaceNames = value);
    }

    /// <summary>The plan made by the last Apply, or null before one.</summary>
    public MassUpdatePlan? LastPlan { get; private set; }

    /// <summary>
    /// Shows the scan result: one row per file, in the order given, with nothing ticked. <paramref name="register"/>
    /// is the register as synced for this run.
    /// </summary>
    public void ShowScan(IEnumerable<ScannedFile> files, PartRegister register)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(register);

        foreach (var row in _rows)
        {
            row.PropertyChanged -= OnRowChanged;
        }

        var rows = files
            .Select(f => new MassUpdateRow(ScannedFileState.For(f, register, _namesThatMeanMe), _defaultDesigner.Length > 0))
            .ToList();
        foreach (var row in rows)
        {
            row.PropertyChanged += OnRowChanged;
        }

        _rows = rows.AsReadOnly();
        LastPlan = null;
        OnPropertyChanged(nameof(Rows));
        OnPropertyChanged(nameof(CountsText));
        OnPropertyChanged(nameof(LastPlan));
        OnTicksChanged();
    }

    /// <summary>The plan for the rows as currently ticked.</summary>
    public MassUpdatePlan BuildPlan() =>
        MassUpdatePlan.Build(_rows.Select(r => (r.State, r.Ticked)), _defaultDesigner);

    /// <summary>
    /// Shows each file's result on its row (matched by path, ignoring case): <see cref="MassUpdateRow.DoneText"/> or
    /// the error. A row the last plan did not register because another file carries its number also says so.
    /// </summary>
    public void ReportResults(IEnumerable<FileUpdateResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        var byPath = new Dictionary<string, FileUpdateResult>(StringComparer.OrdinalIgnoreCase);
        foreach (var result in results)
        {
            byPath[result.FullPath] = result;
        }

        var duplicates = (LastPlan?.Duplicates ?? Array.Empty<MassUpdateDuplicate>())
            .ToDictionary(d => d.DuplicatePath, StringComparer.OrdinalIgnoreCase);

        foreach (var row in _rows)
        {
            var parts = new List<string>();
            var succeeded = true;
            var any = false;

            if (byPath.TryGetValue(row.FullPath, out var result))
            {
                any = true;
                if (string.IsNullOrWhiteSpace(result.Error))
                {
                    parts.Add(MassUpdateRow.DoneText);
                }
                else
                {
                    parts.Add(result.Error.Trim());
                    succeeded = false;
                }
            }

            if (duplicates.TryGetValue(row.FullPath, out var duplicate))
            {
                any = true;
                succeeded = false;
                parts.Add(DuplicateText(duplicate));
            }

            if (any)
            {
                row.SetResult(string.Join(" ", parts), succeeded);
            }
        }
    }

    /// <summary>For example <c>"Not registered: AWB-10001 is also on AWB-10001,plate.ipt."</c></summary>
    public static string DuplicateText(MassUpdateDuplicate duplicate)
    {
        ArgumentNullException.ThrowIfNull(duplicate);
        var first = duplicate.FirstPath[(duplicate.FirstPath.LastIndexOfAny(['\\', '/']) + 1)..];
        return $"Not registered: {duplicate.Number} is also on {first}.";
    }

    private void RequestScan() =>
        ScanRequested?.Invoke(this, new MassUpdateScanEventArgs(PropertyValues.NormaliseText(_folder)));

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
