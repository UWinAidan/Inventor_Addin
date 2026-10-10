using System.Globalization;
using InventorAddin.Core.Logging;
using InventorAddin.Core.Numbering;
using InventorAddin.Core.PartProperties;

namespace InventorAddin.Core.ViewModels;

/// <summary>
/// One entry in the Part type dropdown. <see cref="Code"/> is what the field shows and what is stored;
/// <see cref="DisplayText"/> is what the open dropdown lists.
/// </summary>
public sealed record PartTypeOption(string Code, string FullName, string DisplayText)
{
    /// <summary>The blank entry: no part type.</summary>
    public static readonly PartTypeOption Blank = new(string.Empty, string.Empty, string.Empty);

    public static PartTypeOption From(PartType type) => new(type.Code, type.FullName, type.DisplayText);

    /// <summary>An entry for a code the file holds that is not in <see cref="PartTypes.All"/>.</summary>
    public static PartTypeOption Unknown(string code) =>
        new(code, PartTypes.UnknownCodeText, $"{code}: {PartTypes.UnknownCodeText}");
}

/// <summary>
/// View-model for the Part Properties window (spec 07). It holds what was read from the file as the original,
/// the edited fields, the pre-fills, Detailer following Designer, validation, dirty tracking, Apply and OK, and
/// the status line. Writes go through an <see cref="IPropertyWriteTarget"/>; Cancel needs no command because
/// nothing is written until Apply or OK. It also shows the part number by spec 02 and offers Generate part number
/// for a file without one, through an <see cref="IPartNumberAssigner"/>.
/// </summary>
/// <remarks>
/// Part type codes are held in the list's spelling: a file code that <see cref="PartTypes.Find"/> matches in a
/// different case (<c>m</c> for <c>M</c>) is shown as the listed code, so the dropdown selects it. As with any
/// value the window shows that the file does not hold, that counts as a pending change and Apply writes the
/// listed code. A code not in the list at all is kept exactly as the file holds it (trimmed).
/// A file that cannot be edited shows what the file holds, without pre-fills or Detailer following.
/// </remarks>
public sealed class PartPropertiesViewModel : ObservableObject
{
    public const string NotSavedFileName = "Not saved yet";
    public const string NoWeightText = "-";
    public const string NotSetText = "Not set";
    public const string NewFileTitlePrefix = "New ";
    public const string HeaderSeparator = " · ";
    public const string PreFillNotice = "Pre-filled values will be written when you apply.";
    public const string SavedNotice = "Saved to the file. Save the document to keep the changes.";
    public const string WriteErrorPrefix = "Could not write the properties: ";
    public const string NotInRegisterNote = "Not in the register";
    public const string GenerateNotSavedNote = RegisterPartNumberAssigner.NotSavedMessage;
    public const string GeneratedNoticeFormat = "Part number {0} assigned and written. Save the document to keep it.";
    public const string GeneratedOfflineSuffix = " Assigned offline; it will be added to the shared register at the next sync.";
    public const string GenerateErrorPrefix = "Generate part number failed: ";
    public const string DuplicatesHeading =
        "The register sync found part numbers given to two files. Both rows are kept in the register; fix them by hand.";

    public const string DesignerFieldName = "Designer";
    public const string DetailerFieldName = "Detailer";

    private readonly IPropertyWriteTarget _target;
    private readonly IPartNumberAssigner _assigner;
    private readonly CultureInfo _culture;
    private readonly ILog _log;

    // What the file holds: as read at open, then as written by each successful apply.
    private PartPropertiesSnapshot _original;

    private string _partType;
    private string _designer;
    private string _detailer;
    private string _costText;
    private bool _detailerFollowsDesigner;

    // True when the window opened with values the file does not hold yet (open question 5).
    private readonly bool _pendingAtOpen;
    private bool _appliedOnce;

    // The part number as shown (spec 02, "Display in Part Properties"). Changed by a successful Generate.
    private string _partNumberDisplay;
    private bool _isPartNumberWellFormed;
    private bool _isPartNumberAssigned;

    // The result of the last Apply or Generate: the status text and whether it was a failure.
    private string? _lastResult;
    private bool _lastFailed;

    // Cached state, recomputed after every change.
    private bool _hasChanges;
    private string? _validationMessage;

    /// <param name="snapshot">What was read from the file.</param>
    /// <param name="target">Where Apply writes.</param>
    /// <param name="hasNumber">
    /// Spec 02, "Has a number", for a Part Number property value: the add-in passes <see cref="RegisterStore.HasNumber"/>,
    /// which reads only this PC's copy of the register.
    /// </param>
    /// <param name="assigner">What the Generate button calls.</param>
    /// <param name="defaultDesigner">The Default designer from Settings.</param>
    /// <param name="windowsUserName">The Windows user name, which counts as a blank Designer or Detailer.</param>
    /// <param name="culture">The culture the cost is shown and typed in.</param>
    /// <param name="log">Where writes and failures are logged.</param>
    public PartPropertiesViewModel(
        PartPropertiesSnapshot snapshot,
        IPropertyWriteTarget target,
        Func<string?, bool> hasNumber,
        IPartNumberAssigner assigner,
        string? defaultDesigner,
        string? windowsUserName,
        CultureInfo culture,
        ILog? log = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(hasNumber);
        ArgumentNullException.ThrowIfNull(assigner);
        ArgumentNullException.ThrowIfNull(culture);

        _original = snapshot;
        _target = target;
        _assigner = assigner;
        _culture = culture;
        _log = log ?? NullLog.Instance;

        IsEditable = snapshot.EditBlock == EditBlock.None;
        EditBlockMessage = PartPropertiesSnapshot.EditBlockMessage(snapshot.EditBlock);

        Weight = snapshot.WeightDisplay ?? NoWeightText;

        // Display text for the restyled window (spec 08). The never-saved title and subline are defaults
        // (spec 08, open question 4).
        var saved = !string.IsNullOrWhiteSpace(snapshot.FullFileName);
        var kindName = DocumentKindNames.For(snapshot.DocumentKind);
        HeaderTitle = saved
            ? PartName.FromFileName(snapshot.FullFileName)
            : NewFileTitlePrefix + kindName.ToLowerInvariant();
        HeaderSubline = kindName + HeaderSeparator + FileNameFrom(snapshot.FullFileName);

        // Spec 02: a Part Number in the format is shown as the number, registered or not; anything else, including
        // blank and Inventor's file-name fallback, is not a number. PartProperties.PartNumberDisplay: the property of
        // the same name hides the class here.
        _isPartNumberWellFormed = Numbering.PartNumber.TryParse(snapshot.PartNumber, out var partNumber);
        _partNumberDisplay = _isPartNumberWellFormed
            ? partNumber.ToString()
            : PartProperties.PartNumberDisplay.NotAssignedText;
        _isPartNumberAssigned = hasNumber(snapshot.PartNumber);
        IsSaved = saved;

        MaterialDisplay = DisplayOrNotSet(snapshot.Material, out var materialSet);
        IsMaterialSet = materialSet;
        FinishDisplay = DisplayOrNotSet(snapshot.Finish, out var finishSet);
        IsFinishSet = finishSet;

        // Part type: listed codes in the list's spelling; anything else kept as the file holds it.
        var fileType = PropertyValues.NormaliseText(snapshot.PartType);
        var listed = PartTypes.Find(fileType);
        _partType = listed?.Code ?? fileType;
        if (_partType.Length == 0 && IsEditable)
        {
            _partType = PartTypes.DefaultFor(snapshot.DocumentKind)?.Code ?? string.Empty;
        }

        var options = new List<PartTypeOption> { PartTypeOption.Blank };
        options.AddRange(PartTypes.All.Select(PartTypeOption.From));
        if (fileType.Length > 0 && listed is null)
        {
            options.Add(PartTypeOption.Unknown(fileType));
        }

        PartTypeOptions = options.AsReadOnly();

        // A Designer or Detailer equal to the Windows user name counts as blank (spec 02).
        _designer = snapshot.Designer ?? string.Empty;
        if (IsEditable && IsBlankName(_designer, windowsUserName, defaultDesigner))
        {
            _designer = PropertyValues.NormaliseText(defaultDesigner);
        }

        _detailer = snapshot.Detailer ?? string.Empty;
        if (IsEditable && IsBlankName(_detailer, windowsUserName, defaultDesigner))
        {
            _detailerFollowsDesigner = true;
            _detailer = _designer;
        }

        _costText = PropertyValues.FormatCost(snapshot.Cost, culture);

        ApplyCommand = new RelayCommand(() => Apply(), () => CanApply);
        OkCommand = new RelayCommand(Ok, () => IsValid || !IsEditable);
        GenerateCommand = new RelayCommand(Generate, () => CanGenerate && IsSaved);

        Recalculate();
        _pendingAtOpen = _hasChanges;
    }

    /// <summary>Raised when OK has finished and the window should close.</summary>
    public event EventHandler? CloseRequested;

    /// <summary>
    /// Raised once after a successful Generate whose sync found numbers handed out twice, with one line per duplicate.
    /// The add-in shows them in a message box; the register has already logged each one as a warning.
    /// </summary>
    public event EventHandler<IReadOnlyList<string>>? DuplicatesReported;

    // Read-only display.

    /// <summary>The mass in the document's units, or <see cref="NoWeightText"/> when it could not be read.</summary>
    public string Weight { get; }

    // Display text for the restyled window (spec 08). Each "Is...Set" flag is false when the window shows
    // placeholder text, so it can use the placeholder colour.

    /// <summary>
    /// The header title: the file name without its extension (<see cref="PartName.FromFileName"/>) for a saved
    /// file; for a never-saved file "New " and the kind name in lower case, for example <c>New sheet metal part</c>.
    /// </summary>
    public string HeaderTitle { get; }

    /// <summary>
    /// The line under the header title: the kind name (<see cref="DocumentKindNames"/>), <see cref="HeaderSeparator"/>,
    /// and the file name with its extension or <see cref="NotSavedFileName"/>, for example
    /// <c>Assembly · Gearbox.iam</c> or <c>Part · Not saved yet</c>.
    /// </summary>
    public string HeaderSubline { get; }

    /// <summary>
    /// The Part Number as <see cref="Numbering.PartNumber.ToString"/> writes it when it is in the format, registered
    /// or not; otherwise <see cref="PartProperties.PartNumberDisplay.NotAssignedText"/>. After a successful Generate,
    /// the new number.
    /// </summary>
    public string PartNumberDisplay => _partNumberDisplay;

    /// <summary>
    /// True when <see cref="PartNumberDisplay"/> is a number rather than the placeholder, so the window shows it in
    /// the identifier font.
    /// </summary>
    public bool IsPartNumberWellFormed => _isPartNumberWellFormed;

    /// <summary>
    /// Spec 02, "Has a number": the result of the caller's <c>hasNumber</c> for the file's Part Number, or true after
    /// a successful Generate.
    /// </summary>
    public bool IsPartNumberAssigned => _isPartNumberAssigned;

    /// <summary>True when the Part Number is in the format but the register does not hold it (spec 02, question 13).</summary>
    public bool IsPartNumberUnregistered => _isPartNumberWellFormed && !_isPartNumberAssigned;

    /// <summary><see cref="NotInRegisterNote"/> when <see cref="IsPartNumberUnregistered"/>, otherwise null.</summary>
    public string? PartNumberNote => IsPartNumberUnregistered ? NotInRegisterNote : null;

    /// <summary>False for a file that has never been saved.</summary>
    public bool IsSaved { get; }

    /// <summary>
    /// Whether the window shows the Generate part number button: the file has no registered number and can be edited.
    /// The button is disabled for a never-saved file; see <see cref="GenerateNote"/>.
    /// </summary>
    public bool CanGenerate => !_isPartNumberAssigned && IsEditable;

    /// <summary><see cref="GenerateNotSavedNote"/> while the button is shown for a never-saved file, otherwise null.</summary>
    public string? GenerateNote => CanGenerate && !IsSaved ? GenerateNotSavedNote : null;

    /// <summary>True when <see cref="GenerateNote"/> is not null.</summary>
    public bool HasGenerateNote => GenerateNote is not null;

    /// <summary>The material trimmed, or <see cref="NotSetText"/> when blank.</summary>
    public string MaterialDisplay { get; }

    public bool IsMaterialSet { get; }

    /// <summary>The finish trimmed, or <see cref="NotSetText"/> when blank.</summary>
    public string FinishDisplay { get; }

    public bool IsFinishSet { get; }

    /// <summary>False when the file cannot be edited; every editable field binds its enabled state to this.</summary>
    public bool IsEditable { get; }

    /// <summary>Why the file cannot be edited, or null.</summary>
    public string? EditBlockMessage { get; }

    // Editable fields.

    /// <summary>
    /// The blank entry, then <see cref="PartTypes.All"/>, then the file's own code when it is not in the list.
    /// </summary>
    public IReadOnlyList<PartTypeOption> PartTypeOptions { get; }

    /// <summary>
    /// The part type code. A code in the list is held in the list's spelling whatever case it is set in.
    /// </summary>
    public string PartType
    {
        get => _partType;
        set
        {
            var normalised = PropertyValues.NormaliseText(value);
            normalised = PartTypes.Find(normalised)?.Code ?? normalised;
            if (_partType == normalised)
                return;

            _partType = normalised;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PartTypeFullName));
            OnPropertyChanged(nameof(PartTypeToolTip));
            OnEditChanged();
        }
    }

    /// <summary>The full name of <see cref="PartType"/>, shown beside the dropdown; empty for a blank code.</summary>
    public string PartTypeFullName => PartTypes.FullNameFor(_partType);

    /// <summary>
    /// The dropdown's tooltip: <see cref="PartTypeFullName"/>, or null for a blank code so the window shows no
    /// tooltip rather than an empty one.
    /// </summary>
    public string? PartTypeToolTip => _partType.Length == 0 ? null : PartTypeFullName;

    /// <summary>The designer as typed. While the Detailer follows, setting this sets the Detailer too.</summary>
    public string Designer
    {
        get => _designer;
        set
        {
            value ??= string.Empty;
            if (_designer == value)
                return;

            _designer = value;
            OnPropertyChanged();
            if (_detailerFollowsDesigner && _detailer != value)
            {
                _detailer = value;
                OnPropertyChanged(nameof(Detailer));
            }

            OnEditChanged();
        }
    }

    /// <summary>
    /// The detailer as typed. Setting it to anything other than the current Designer stops it following the
    /// Designer for the rest of the window's life.
    /// </summary>
    public string Detailer
    {
        get => _detailer;
        set
        {
            value ??= string.Empty;
            if (value != _designer)
            {
                _detailerFollowsDesigner = false;
            }

            if (_detailer == value)
                return;

            _detailer = value;
            OnPropertyChanged();
            OnEditChanged();
        }
    }

    /// <summary>Whether the Detailer currently follows the Designer.</summary>
    public bool DetailerFollowsDesigner => _detailerFollowsDesigner;

    /// <summary>The cost as typed, in the window's culture. Blank means no cost (stored as 0).</summary>
    public string CostText
    {
        get => _costText;
        set
        {
            value ??= string.Empty;
            if (_costText == value)
                return;

            _costText = value;
            OnPropertyChanged();
            OnEditChanged();
        }
    }

    // State.

    /// <summary>
    /// True when applying would write something: the write plan for the current values is not empty. An invalid
    /// cost also counts, since the file cannot hold it. Always false for a file that cannot be edited.
    /// </summary>
    public bool HasChanges => _hasChanges;

    /// <summary>The first validation error (Designer, Detailer, then Cost), or null.</summary>
    public string? ValidationMessage => _validationMessage;

    public bool IsValid => _validationMessage is null;

    /// <summary>
    /// The status line: the edit block message, else the validation message, else the result of the last Apply or
    /// Generate, else the pre-fill notice, else empty.
    /// </summary>
    public string StatusText
    {
        get
        {
            if (EditBlockMessage is not null)
                return EditBlockMessage;
            if (_validationMessage is not null)
                return _validationMessage;
            if (_lastResult is not null)
                return _lastResult;
            if (ShowPreFillNotice)
                return PreFillNotice;
            return string.Empty;
        }
    }

    /// <summary>
    /// True when <see cref="StatusText"/> is a validation, write or Generate error, so the window shows it in red.
    /// </summary>
    public bool HasError =>
        EditBlockMessage is null && (_validationMessage is not null || (_lastResult is not null && _lastFailed));

    /// <summary>Writes the changes and stays open. Enabled when there are changes, all valid, and the file is editable.</summary>
    public RelayCommand ApplyCommand { get; }

    /// <summary>Applies when there are changes, then closes. Does not close when that apply fails.</summary>
    public RelayCommand OkCommand { get; }

    /// <summary>
    /// Assigns a number from the register and writes it as the Part Number. Enabled while <see cref="CanGenerate"/>
    /// for a saved file.
    /// </summary>
    public RelayCommand GenerateCommand { get; }

    private bool CanApply => IsEditable && _hasChanges && IsValid;

    // Shown until the first successful apply, while the opening pre-fills have not all been undone.
    private bool ShowPreFillNotice => _pendingAtOpen && !_appliedOnce && _hasChanges;

    /// <summary>
    /// The message box text for <see cref="DuplicatesReported"/>: <see cref="DuplicatesHeading"/>, then one line per
    /// duplicate starting with a dash.
    /// </summary>
    public static string FormatDuplicates(IReadOnlyList<string> duplicateMessages)
    {
        ArgumentNullException.ThrowIfNull(duplicateMessages);
        return DuplicatesHeading + string.Concat(duplicateMessages.Select(m => Environment.NewLine + "- " + m));
    }

    private static string FileNameFrom(string? fullFileName)
    {
        if (string.IsNullOrWhiteSpace(fullFileName))
            return NotSavedFileName;

        return fullFileName[(fullFileName.LastIndexOfAny(new[] { '\\', '/' }) + 1)..];
    }

    private static bool IsBlankName(string value, string? windowsUserName, string? defaultDesigner) =>
        PropertyValues.NormaliseText(value).Length == 0
        || PersonNames.IsWindowsUserPlaceholder(value, windowsUserName, defaultDesigner);

    private static string DisplayOrNotSet(string? value, out bool isSet)
    {
        var text = PropertyValues.NormaliseText(value);
        isSet = text.Length > 0;
        return isSet ? text : NotSetText;
    }

    private string? Validate()
    {
        var error = PropertyValues.ValidateText(DesignerFieldName, _designer)
            ?? PropertyValues.ValidateText(DetailerFieldName, _detailer);
        if (error is not null)
            return error;

        PropertyValues.TryParseCost(_costText, _culture, out _, out error);
        return error;
    }

    private bool TryBuildPlan(out IReadOnlyList<PropertyWrite> writes)
    {
        writes = Array.Empty<PropertyWrite>();
        if (!IsEditable || !PropertyValues.TryParseCost(_costText, _culture, out var cost, out _))
            return false;

        writes = PartPropertiesWritePlan.Build(_original, new PartEditedValues(_partType, _designer, _detailer, cost));
        return true;
    }

    private void Recalculate()
    {
        _validationMessage = Validate();
        if (!IsEditable)
        {
            _hasChanges = false;
        }
        else if (TryBuildPlan(out var writes))
        {
            _hasChanges = writes.Count > 0;
        }
        else
        {
            // An invalid cost: the file cannot hold what is typed, so it is a change (that cannot be applied).
            _hasChanges = true;
        }
    }

    /// <summary>Applies the changes. Returns false only when a write was attempted and failed.</summary>
    private bool Apply()
    {
        if (!CanApply || !TryBuildPlan(out var writes))
            return true;

        // An empty plan never reaches the target.
        if (writes.Count == 0)
            return true;

        try
        {
            _target.Apply(writes);
        }
        catch (Exception ex)
        {
            // A UI boundary: the window shows the reason and stays open so the user can retry or cancel.
            _log.Error("Could not write the part properties.", ex);
            _lastResult = WriteErrorPrefix + ex.Message;
            _lastFailed = true;
            OnStateChanged();
            return false;
        }

        // Property names and actions only, never values: they can hold personal data.
        _log.Info($"Part properties written ({writes.Count}): "
            + string.Join(", ", writes.Select(w => $"{w.Action} {w.Location.PropertyName}")) + ".");

        _original = Rebase(_original, writes);
        _appliedOnce = true;
        _lastResult = SavedNotice;
        _lastFailed = false;

        // Show the cost as it was stored.
        PropertyValues.TryParseCost(_costText, _culture, out var cost, out _);
        var formatted = PropertyValues.FormatCost(cost, _culture);
        if (formatted != _costText)
        {
            _costText = formatted;
            OnPropertyChanged(nameof(CostText));
        }

        OnStateChanged();
        return true;
    }

    /// <summary>
    /// Runs the assigner. On success shows the new number, hides the button and reports any duplicates; on failure
    /// shows the reason and changes nothing else.
    /// </summary>
    private void Generate()
    {
        if (!CanGenerate || !IsSaved)
            return;

        PartNumberAssignment result;
        try
        {
            result = _assigner.Assign();
        }
        catch (Exception ex)
        {
            // A UI boundary, as Apply: the window shows the reason and stays usable.
            _log.Error("Could not generate a part number.", ex);
            _lastResult = GenerateErrorPrefix + ex.Message;
            _lastFailed = true;
            OnStateChanged();
            return;
        }

        // The file now holds the number, so a later Apply must not see it as a change. Part Number is never in the
        // write plan, but the snapshot is kept true to the file.
        _original = _original with { PartNumber = result.PartNumber };
        _isPartNumberWellFormed = Numbering.PartNumber.TryParse(result.PartNumber, out var number);
        _partNumberDisplay = _isPartNumberWellFormed ? number.ToString() : result.PartNumber;
        _isPartNumberAssigned = true;

        _lastResult = string.Format(CultureInfo.InvariantCulture, GeneratedNoticeFormat, _partNumberDisplay)
            + (result.Offline ? GeneratedOfflineSuffix : string.Empty);
        _lastFailed = false;

        OnPropertyChanged(nameof(PartNumberDisplay));
        OnPropertyChanged(nameof(IsPartNumberWellFormed));
        OnPropertyChanged(nameof(IsPartNumberAssigned));
        OnPropertyChanged(nameof(IsPartNumberUnregistered));
        OnPropertyChanged(nameof(PartNumberNote));
        OnPropertyChanged(nameof(CanGenerate));
        OnPropertyChanged(nameof(GenerateNote));
        OnPropertyChanged(nameof(HasGenerateNote));
        GenerateCommand.RaiseCanExecuteChanged();
        OnStateChanged();

        if (result.DuplicateMessages.Count > 0)
        {
            DuplicatesReported?.Invoke(this, result.DuplicateMessages);
        }
    }

    private void Ok()
    {
        if (IsEditable && _hasChanges && !Apply())
            return;

        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>The snapshot as the file holds it after <paramref name="writes"/>.</summary>
    private static PartPropertiesSnapshot Rebase(PartPropertiesSnapshot snapshot, IReadOnlyList<PropertyWrite> writes)
    {
        foreach (var write in writes)
        {
            var text = write.Action == PropertyWriteAction.Remove ? null : write.TextValue;
            var location = write.Location;
            if (location == PartPropertyStorage.Description)
                snapshot = snapshot with { Description = text };
            else if (location == PartPropertyStorage.PartType)
                snapshot = snapshot with { PartType = text };
            else if (location == PartPropertyStorage.Designer)
                snapshot = snapshot with { Designer = text };
            else if (location == PartPropertyStorage.Detailer)
                snapshot = snapshot with { Detailer = text };
            else if (location == PartPropertyStorage.Cost)
                snapshot = snapshot with { Cost = write.CurrencyValue };
        }

        return snapshot;
    }

    private void OnEditChanged()
    {
        OnPropertyChanged(nameof(DetailerFollowsDesigner));
        OnStateChanged();
    }

    private void OnStateChanged()
    {
        Recalculate();
        OnPropertyChanged(nameof(HasChanges));
        OnPropertyChanged(nameof(ValidationMessage));
        OnPropertyChanged(nameof(IsValid));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(HasError));
        ApplyCommand.RaiseCanExecuteChanged();
        OkCommand.RaiseCanExecuteChanged();
    }
}
