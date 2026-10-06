using System.Globalization;
using InventorAddin.Core.Logging;
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
/// nothing is written until Apply or OK.
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
    public const string PreFillNotice = "Pre-filled values will be written when you apply.";
    public const string SavedNotice = "Saved to the file. Save the document to keep the changes.";
    public const string WriteErrorPrefix = "Could not write the properties: ";

    public const string DesignerFieldName = "Designer";
    public const string DetailerFieldName = "Detailer";

    private readonly IPropertyWriteTarget _target;
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

    // The result of the last apply: the status text and whether it was a failure.
    private string? _applyResult;
    private bool _applyFailed;

    // Cached state, recomputed after every change.
    private bool _hasChanges;
    private string? _validationMessage;

    public PartPropertiesViewModel(
        PartPropertiesSnapshot snapshot,
        IPropertyWriteTarget target,
        string? defaultDesigner,
        CultureInfo culture,
        ILog? log = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(culture);

        _original = snapshot;
        _target = target;
        _culture = culture;
        _log = log ?? NullLog.Instance;

        IsEditable = snapshot.EditBlock == EditBlock.None;
        EditBlockMessage = PartPropertiesSnapshot.EditBlockMessage(snapshot.EditBlock);

        FileName = FileNameFrom(snapshot.FullFileName);
        PartNumber = snapshot.PartNumber ?? string.Empty;
        PartName = PartProperties.PartName.FromFileName(snapshot.FullFileName);
        Material = snapshot.Material ?? string.Empty;
        Finish = snapshot.Finish ?? string.Empty;
        Weight = snapshot.WeightDisplay ?? NoWeightText;

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

        _designer = snapshot.Designer ?? string.Empty;
        if (IsEditable && PropertyValues.NormaliseText(_designer).Length == 0)
        {
            _designer = PropertyValues.NormaliseText(defaultDesigner);
        }

        _detailer = snapshot.Detailer ?? string.Empty;
        if (IsEditable && PropertyValues.NormaliseText(_detailer).Length == 0)
        {
            _detailerFollowsDesigner = true;
            _detailer = _designer;
        }

        _costText = PropertyValues.FormatCost(snapshot.Cost, culture);

        ApplyCommand = new RelayCommand(() => Apply(), () => CanApply);
        OkCommand = new RelayCommand(Ok, () => IsValid || !IsEditable);

        Recalculate();
        _pendingAtOpen = _hasChanges;
    }

    /// <summary>Raised when OK has finished and the window should close.</summary>
    public event EventHandler? CloseRequested;

    // Read-only display.

    /// <summary>The header: the file name with its extension, or <see cref="NotSavedFileName"/>.</summary>
    public string FileName { get; }

    public string PartNumber { get; }

    /// <summary>The file name without its extension; blank for a never-saved file.</summary>
    public string PartName { get; }

    public string Material { get; }

    public string Finish { get; }

    /// <summary>The mass in the document's units, or <see cref="NoWeightText"/> when it could not be read.</summary>
    public string Weight { get; }

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
            OnEditChanged();
        }
    }

    /// <summary>The full name of <see cref="PartType"/>, for the field's tooltip.</summary>
    public string PartTypeFullName => PartTypes.FullNameFor(_partType);

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
    /// The status line: the edit block message, else the validation message, else the result of the last apply,
    /// else the pre-fill notice, else empty.
    /// </summary>
    public string StatusText
    {
        get
        {
            if (EditBlockMessage is not null)
                return EditBlockMessage;
            if (_validationMessage is not null)
                return _validationMessage;
            if (_applyResult is not null)
                return _applyResult;
            if (ShowPreFillNotice)
                return PreFillNotice;
            return string.Empty;
        }
    }

    /// <summary>True when <see cref="StatusText"/> is a validation or write error, so the window shows it in red.</summary>
    public bool HasError =>
        EditBlockMessage is null && (_validationMessage is not null || (_applyResult is not null && _applyFailed));

    /// <summary>Writes the changes and stays open. Enabled when there are changes, all valid, and the file is editable.</summary>
    public RelayCommand ApplyCommand { get; }

    /// <summary>Applies when there are changes, then closes. Does not close when that apply fails.</summary>
    public RelayCommand OkCommand { get; }

    private bool CanApply => IsEditable && _hasChanges && IsValid;

    // Shown until the first successful apply, while the opening pre-fills have not all been undone.
    private bool ShowPreFillNotice => _pendingAtOpen && !_appliedOnce && _hasChanges;

    private static string FileNameFrom(string? fullFileName)
    {
        if (string.IsNullOrWhiteSpace(fullFileName))
            return NotSavedFileName;

        return fullFileName[(fullFileName.LastIndexOfAny(new[] { '\\', '/' }) + 1)..];
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
            _applyResult = WriteErrorPrefix + ex.Message;
            _applyFailed = true;
            OnStateChanged();
            return false;
        }

        // Property names and actions only, never values: they can hold personal data.
        _log.Info($"Part properties written ({writes.Count}): "
            + string.Join(", ", writes.Select(w => $"{w.Action} {w.Location.PropertyName}")) + ".");

        _original = Rebase(_original, writes);
        _appliedOnce = true;
        _applyResult = SavedNotice;
        _applyFailed = false;

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
