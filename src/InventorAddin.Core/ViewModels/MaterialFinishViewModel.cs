using InventorAddin.Core.Logging;
using InventorAddin.Core.Materials;
using InventorAddin.Core.PartProperties;

namespace InventorAddin.Core.ViewModels;

/// <summary>
/// What the Material and Finish window's header shows when it opens (spec 03, "Target window"): the target, and the
/// current material, finish name and finish code. Each value may be blank, or <see cref="VariesText"/> when the
/// selected parts differ.
/// </summary>
/// <param name="Target">The part's file name, or <see cref="PartsSelectedText"/> in an assembly.</param>
/// <param name="Material">The current material's display name.</param>
/// <param name="FinishName">The current <c>Finish</c> iProperty.</param>
/// <param name="FinishCode">The current <c>Finish Code</c> iProperty.</param>
public sealed record MaterialFinishCurrent(string Target, string? Material, string? FinishName, string? FinishCode)
{
    /// <summary>Shown for a value the selected parts do not share.</summary>
    public const string VariesText = "Varies";

    /// <summary>For example <c>"1 part selected"</c> or <c>"3 parts selected"</c>.</summary>
    public static string PartsSelectedText(int count) => count == 1 ? "1 part selected" : $"{count} parts selected";

    /// <summary>The header for one part document: its file name and its values.</summary>
    public static MaterialFinishCurrent ForPart(string fileName, string? material, string? finishName, string? finishCode) =>
        new(fileName ?? "", Clean(material), Clean(finishName), Clean(finishCode));

    /// <summary>
    /// The header for the selected parts of an assembly: <see cref="PartsSelectedText"/>, and each value shared by
    /// every part (<see cref="Combine"/>).
    /// </summary>
    public static MaterialFinishCurrent ForParts(
        IReadOnlyCollection<(string? Material, string? FinishName, string? FinishCode)> parts)
    {
        ArgumentNullException.ThrowIfNull(parts);
        return new MaterialFinishCurrent(
            PartsSelectedText(parts.Count),
            Combine(parts.Select(p => p.Material)),
            Combine(parts.Select(p => p.FinishName)),
            Combine(parts.Select(p => p.FinishCode)));
    }

    /// <summary>
    /// The value every part shares, trimmed (the first spelling kept), compared ignoring case; a blank value counts as
    /// a value. <see cref="VariesText"/> when they differ; <c>""</c> when there are none.
    /// </summary>
    public static string Combine(IEnumerable<string?> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        string? first = null;
        foreach (var value in values)
        {
            var text = Clean(value);
            if (first is null)
            {
                first = text;
            }
            else if (!string.Equals(first, text, StringComparison.OrdinalIgnoreCase))
            {
                return VariesText;
            }
        }

        return first ?? "";
    }

    /// <summary>True when <paramref name="value"/> is <see cref="VariesText"/>.</summary>
    public static bool IsVaries(string? value) => string.Equals(Clean(value), VariesText, StringComparison.Ordinal);

    private static string Clean(string? value) => PropertyValues.NormaliseText(value);
}

/// <summary>One material in the Materials list: its name, category, and the star that marks it preferred.</summary>
public sealed class MaterialRow : ObservableObject
{
    private bool _isPreferred;

    internal MaterialRow(LibraryMaterial material, bool isPreferred, Action<MaterialRow> toggleStar)
    {
        Material = material;
        _isPreferred = isPreferred;
        ToggleStarCommand = new RelayCommand(() => toggleStar(this));
    }

    public LibraryMaterial Material { get; }

    /// <summary>The material's display name.</summary>
    public string Name => Material.DisplayName ?? "";

    /// <summary>The material's category, or <c>""</c>.</summary>
    public string Category => Material.Category ?? "";

    /// <summary>True when the material is in the preferred materials; the window shows a filled star.</summary>
    public bool IsPreferred
    {
        get => _isPreferred;
        internal set => SetProperty(ref _isPreferred, value);
    }

    /// <summary>Adds the material to the preferred materials, or removes it, and saves straight away.</summary>
    public RelayCommand ToggleStarCommand { get; }
}

/// <summary>One entry in the Finishes list: a finish, or <see cref="None"/>.</summary>
public sealed class FinishRow
{
    public const string NoneText = "None";

    /// <summary>The "None" entry, first in the list: the part gets no finish.</summary>
    public static readonly FinishRow None = new(null);

    internal FinishRow(Finish? finish) => Finish = finish;

    /// <summary>The finish, or null for <see cref="None"/>.</summary>
    public Finish? Finish { get; }

    public bool IsNone => Finish is null;

    /// <summary>The finish's name, or <see cref="NoneText"/>.</summary>
    public string Name => Finish?.Name ?? NoneText;

    /// <summary>The finish's code, or <c>""</c> for None.</summary>
    public string Code => Finish?.Code ?? "";
}

/// <summary>
/// View-model for the Select Material and Finish window (spec 03, "Target window"). It holds the filters, the two
/// lists, the selection, the stars, Apply, OK and Cancel, and the status line. Applying goes through an
/// <see cref="IMaterialFinishTarget"/>; a star calls the save callback straight away (spec 03, question 11).
/// </summary>
/// <remarks>
/// The filters and search boxes only change what the lists show; they never change the selection. A selected
/// material or finish that a filter hides stays selected (Apply uses it) and shows again when the filter allows it.
/// The selection is <see cref="SelectedMaterial"/> and <see cref="SelectedFinish"/>; the list boxes bind to
/// <see cref="MaterialListSelection"/> and <see cref="FinishListSelection"/>, which are null while the selection is
/// hidden and ignore the null a list box writes when its items change, so WPF cannot clear a hidden selection.
/// <see cref="SelectionText"/> names the selection for the window to show. The selected finish goes back to None only
/// when it no longer suits the selected material. The rows are created once, so a row stays the same object.
/// </remarks>
public sealed class MaterialFinishViewModel : ObservableObject
{
    public const string HeaderSeparator = " · ";
    public const string NoMaterialText = "No material";
    public const string NoFinishText = "No finish";
    public const string AppliedText = "Applied. Save the document to keep the changes.";
    public const string ApplyFailedText = "Could not apply the material and finish.";
    public const string ApplyErrorPrefix = "Could not apply the material and finish: ";
    public const string PreferredSaveErrorPrefix = "Could not save the preferred materials: ";
    public const string FinishListUnreadableText = "The finish list could not be read; see the log.";

    private readonly IReadOnlyList<MaterialRow> _materialRows;
    private readonly IReadOnlyList<FinishRow> _finishRows;
    private readonly Dictionary<LibraryMaterial, MaterialRow> _rowByMaterial = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<Finish, FinishRow> _rowByFinish = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<string> _preferred = new(StringComparer.OrdinalIgnoreCase);
    private readonly Action<string> _savePreferredToggle;
    private readonly IMaterialFinishTarget _target;
    private readonly ILog _log;

    private MaterialFinishCurrent _current;
    private string _selectedCategory = MaterialFilter.All;
    private bool _preferredOnly;
    private bool _showPaints = true;
    private string _materialSearch = "";
    private string _finishSearch = "";
    private IReadOnlyList<MaterialRow> _materials = Array.Empty<MaterialRow>();
    private IReadOnlyList<FinishRow> _finishes = Array.Empty<FinishRow>();
    private MaterialRow? _selectedMaterial;
    private FinishRow? _selectedFinish;
    private string _statusText = "";
    private bool _statusIsError;
    private IReadOnlyList<string> _statusNotes = Array.Empty<string>();

    /// <param name="current">The header: the target and its current values.</param>
    /// <param name="materials">The materials of the library (task 069), in any order.</param>
    /// <param name="finishes">The finishes that loaded from the finish list (task 068), in file order.</param>
    /// <param name="finishProblems">The finish list's problems; shown in the status line at open, not as an error.</param>
    /// <param name="preferred">The preferred material names from Settings.</param>
    /// <param name="savePreferredToggle">
    /// Called with a material's display name when its star is clicked. The add-in toggles it with
    /// <see cref="LibraryPaths.TogglePreferred"/> and saves Settings; an exception leaves the star as it was.
    /// </param>
    /// <param name="target">What Apply and OK call.</param>
    /// <param name="log">Where a failed Apply or save is logged.</param>
    public MaterialFinishViewModel(
        MaterialFinishCurrent current,
        IEnumerable<LibraryMaterial> materials,
        IEnumerable<Finish> finishes,
        IEnumerable<FinishListProblem> finishProblems,
        IEnumerable<string> preferred,
        Action<string> savePreferredToggle,
        IMaterialFinishTarget target,
        ILog? log = null)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(materials);
        ArgumentNullException.ThrowIfNull(finishes);
        ArgumentNullException.ThrowIfNull(finishProblems);
        ArgumentNullException.ThrowIfNull(preferred);
        ArgumentNullException.ThrowIfNull(savePreferredToggle);
        ArgumentNullException.ThrowIfNull(target);

        _current = current;
        _savePreferredToggle = savePreferredToggle;
        _target = target;
        _log = log ?? NullLog.Instance;

        foreach (var name in preferred)
        {
            var trimmed = (name ?? "").Trim();
            if (trimmed.Length > 0)
            {
                _preferred.Add(trimmed);
            }
        }

        var materialList = materials.ToList();
        _materialRows = materialList
            .Select(m => new MaterialRow(m, _preferred.Contains(NameOf(m)), ToggleStar))
            .ToList()
            .AsReadOnly();
        foreach (var row in _materialRows)
        {
            _rowByMaterial[row.Material] = row;
        }

        _finishRows = finishes.Select(f => new FinishRow(f)).ToList().AsReadOnly();
        foreach (var row in _finishRows)
        {
            _rowByFinish[row.Finish!] = row;
        }

        Categories = MaterialFilter.Categories(materialList);

        // Ticked only when the library holds a preferred material, so the list never opens empty (Default), and not
        // when the current material is in the library but not preferred, so it is shown and preselected (parent, 072).
        var currentRow = FindCurrentMaterialRow();
        _preferredOnly = _materialRows.Any(r => r.IsPreferred) && (currentRow is null || currentRow.IsPreferred);

        ApplyCommand = new RelayCommand(() => Apply(), () => _selectedMaterial is not null);
        OkCommand = new RelayCommand(Ok, () => _selectedMaterial is not null);
        CancelCommand = new RelayCommand(() => CloseRequested?.Invoke(this, EventArgs.Empty));

        RefreshMaterials();
        Preselect(currentRow);

        var problems = finishProblems.ToList();
        if (problems.Count > 0)
        {
            SetStatus(FinishProblemsText(problems, _finishRows.Count), false, problems.Select(p => p.ToString()).ToList());
        }
    }

    /// <summary>Raised by OK after a successful apply, and by Cancel, when the window should close.</summary>
    public event EventHandler? CloseRequested;

    // Header.

    /// <summary>The header title: the part's file name, or the number of selected parts.</summary>
    public string HeaderTitle => _current.Target;

    /// <summary>The current material, or <see cref="NoMaterialText"/>.</summary>
    public string CurrentMaterialText => Blank(_current.Material) ? NoMaterialText : _current.Material!.Trim();

    /// <summary>
    /// The current finish, for example <c>"Hard chrome (HC)"</c>; <see cref="NoFinishText"/> when there is none;
    /// <see cref="MaterialFinishCurrent.VariesText"/> when the parts differ.
    /// </summary>
    public string CurrentFinishText
    {
        get
        {
            var name = (_current.FinishName ?? "").Trim();
            var code = (_current.FinishCode ?? "").Trim();
            if (name.Length == 0 && code.Length == 0)
            {
                return NoFinishText;
            }

            if (MaterialFinishCurrent.IsVaries(name) || code.Length == 0)
            {
                return name.Length > 0 ? name : code;
            }

            return name.Length == 0 ? code : $"{name} ({code})";
        }
    }

    /// <summary>The header subline: the current material and finish, for example <c>"Steel, 1018 · No finish"</c>.</summary>
    public string HeaderSubline => CurrentMaterialText + HeaderSeparator + CurrentFinishText;

    /// <summary>The header as last read or applied.</summary>
    public MaterialFinishCurrent Current => _current;

    // Filters.

    /// <summary>The Category dropdown's choices, "All" first (task 069).</summary>
    public IReadOnlyList<string> Categories { get; }

    /// <summary>The chosen category; "All" at open. Null or blank means All.</summary>
    public string SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            var text = string.IsNullOrWhiteSpace(value) ? MaterialFilter.All : value;
            if (SetProperty(ref _selectedCategory, text))
            {
                RefreshMaterials();
            }
        }
    }

    /// <summary>
    /// Shows only starred materials. Ticked at open when the library holds at least one preferred material,
    /// otherwise unticked (Default), so the list never opens empty. Also unticked at open when the current material
    /// is in the library but not preferred, so it is shown and preselected.
    /// </summary>
    public bool PreferredOnly
    {
        get => _preferredOnly;
        set
        {
            if (SetProperty(ref _preferredOnly, value))
            {
                RefreshMaterials();
            }
        }
    }

    /// <summary>Lists finishes of Kind Paint. Ticked at open (Default, spec 03 question 12).</summary>
    public bool ShowPaints
    {
        get => _showPaints;
        set
        {
            if (SetProperty(ref _showPaints, value))
            {
                RefreshFinishes();
            }
        }
    }

    /// <summary>The search box above the Materials list.</summary>
    public string MaterialSearch
    {
        get => _materialSearch;
        set
        {
            if (SetProperty(ref _materialSearch, value ?? ""))
            {
                RefreshMaterials();
            }
        }
    }

    /// <summary>The search box above the Finishes list. None is always listed.</summary>
    public string FinishSearch
    {
        get => _finishSearch;
        set
        {
            if (SetProperty(ref _finishSearch, value ?? ""))
            {
                RefreshFinishes();
            }
        }
    }

    // Lists.

    /// <summary>The materials the filters keep, sorted by name.</summary>
    public IReadOnlyList<MaterialRow> Materials
    {
        get => _materials;
        private set => SetProperty(ref _materials, value);
    }

    /// <summary>
    /// <see cref="FinishRow.None"/> then the finishes that suit the selected material (task 069), in file order.
    /// Empty until a material is selected.
    /// </summary>
    public IReadOnlyList<FinishRow> Finishes
    {
        get => _finishes;
        private set => SetProperty(ref _finishes, value);
    }

    /// <summary>
    /// The selected material, or null. It stays selected while a filter hides it. Changing it refreshes the Finishes
    /// list, and the selected finish goes back to None when it does not suit the new material.
    /// </summary>
    public MaterialRow? SelectedMaterial
    {
        get => _selectedMaterial;
        set
        {
            if (SetProperty(ref _selectedMaterial, value))
            {
                RefreshFinishes();
                OnPropertyChanged(nameof(MaterialListSelection));
                OnPropertyChanged(nameof(SelectionText));
                ApplyCommand.RaiseCanExecuteChanged();
                OkCommand.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// The selected finish: <see cref="FinishRow.None"/> or a finish once a material is selected, else null. It stays
    /// selected while <see cref="ShowPaints"/> or the finish search hides it, and is what Apply uses.
    /// </summary>
    public FinishRow? SelectedFinish
    {
        get => _selectedFinish;
        set
        {
            if (SetProperty(ref _selectedFinish, value))
            {
                OnPropertyChanged(nameof(FinishListSelection));
                OnPropertyChanged(nameof(SelectionIsHidden));
                OnPropertyChanged(nameof(SelectionText));
            }
        }
    }

    /// <summary>
    /// For the Materials list box's selected item: <see cref="SelectedMaterial"/> while the list shows it, else null.
    /// Setting a row selects it; setting null is ignored, because a list box writes null when its items change.
    /// </summary>
    public MaterialRow? MaterialListSelection
    {
        get => _selectedMaterial is not null && _materials.Contains(_selectedMaterial) ? _selectedMaterial : null;
        set
        {
            if (value is not null)
            {
                SelectedMaterial = value;
            }
            else
            {
                // Tell the list box again what it should show.
                OnPropertyChanged(nameof(MaterialListSelection));
            }
        }
    }

    /// <summary>
    /// For the Finishes list box's selected item: <see cref="SelectedFinish"/> while the list shows it, else null.
    /// Setting a row selects it; setting null is ignored, as for <see cref="MaterialListSelection"/>.
    /// </summary>
    public FinishRow? FinishListSelection
    {
        get => _selectedFinish is not null && _finishes.Contains(_selectedFinish) ? _selectedFinish : null;
        set
        {
            if (value is not null)
            {
                SelectedFinish = value;
            }
            else
            {
                OnPropertyChanged(nameof(FinishListSelection));
            }
        }
    }

    /// <summary>True when a filter hides the selected material or finish, so the window can say what is selected.</summary>
    public bool SelectionIsHidden =>
        (_selectedMaterial is not null && MaterialListSelection is null)
        || (_selectedFinish is not null && FinishListSelection is null);

    /// <summary>
    /// The selection, for example <c>"Steel, 1018 · Hard chrome (HC)"</c> or <c>"Steel, 1018 · None"</c>;
    /// <c>""</c> when no material is selected.
    /// </summary>
    public string SelectionText
    {
        get
        {
            if (_selectedMaterial is null)
            {
                return "";
            }

            var finish = _selectedFinish?.Finish is { } f ? $"{f.Name} ({f.Code})" : FinishRow.NoneText;
            return _selectedMaterial.Name + HeaderSeparator + finish;
        }
    }

    // Commands.

    /// <summary>Applies the selection and stays open. Enabled when a material is selected.</summary>
    public RelayCommand ApplyCommand { get; }

    /// <summary>Applies the selection, then closes. Does not close when the apply fails.</summary>
    public RelayCommand OkCommand { get; }

    /// <summary>Closes without applying.</summary>
    public RelayCommand CancelCommand { get; }

    // Status.

    /// <summary>The status line: the result of the last Apply or star, or the finish list's problems at open.</summary>
    public string StatusText => _statusText;

    /// <summary>True when <see cref="StatusText"/> is a failure, shown in the error style.</summary>
    public bool StatusIsError => _statusIsError;

    /// <summary>Every note behind the status, one per line, for its tooltip; null when there are none.</summary>
    public string? StatusDetails => _statusNotes.Count == 0 ? null : string.Join(Environment.NewLine, _statusNotes);

    /// <summary>The notes behind the status.</summary>
    public IReadOnlyList<string> StatusNotes => _statusNotes;

    public bool HasStatusDetails => _statusNotes.Count > 0;

    /// <summary>
    /// The status at open when the finish list had problems, for example <c>"2 finish rows could not be read; see the
    /// log."</c>; <see cref="FinishListUnreadableText"/> when no finish loaded. Problems on one line count once.
    /// </summary>
    public static string FinishProblemsText(IReadOnlyCollection<FinishListProblem> problems, int loadedFinishes)
    {
        ArgumentNullException.ThrowIfNull(problems);

        if (loadedFinishes == 0)
        {
            return FinishListUnreadableText;
        }

        var rows = problems.Select(p => p.Line).Distinct().Count();
        return rows == 1
            ? "1 finish row could not be read; see the log."
            : $"{rows} finish rows could not be read; see the log.";
    }

    /// <summary>
    /// <paramref name="message"/> followed by the number of notes, for example <c>"Applied to 2 parts. 1 note."</c>;
    /// the message alone when there are none.
    /// </summary>
    public static string WithNoteCount(string message, int notes) =>
        notes switch
        {
            <= 0 => message,
            1 => $"{message} 1 note.",
            _ => $"{message} {notes} notes.",
        };

    private void ToggleStar(MaterialRow row)
    {
        var name = NameOf(row.Material);
        if (name.Length == 0)
        {
            return;
        }

        try
        {
            _savePreferredToggle(name);
        }
        catch (Exception ex)
        {
            // A UI boundary: the star stays as it was and the window says why.
            _log.Error("Could not save the preferred materials.", ex);
            SetStatus(PreferredSaveErrorPrefix + ex.Message, true, Array.Empty<string>());
            return;
        }

        var nowPreferred = !_preferred.Contains(name);
        if (nowPreferred)
        {
            _preferred.Add(name);
        }
        else
        {
            _preferred.Remove(name);
        }

        // Every row with this name, in case the library lists one twice.
        foreach (var other in _materialRows)
        {
            if (string.Equals(NameOf(other.Material), name, StringComparison.OrdinalIgnoreCase))
            {
                other.IsPreferred = nowPreferred;
            }
        }

        if (_preferredOnly)
        {
            RefreshMaterials();
        }
    }

    private bool Apply()
    {
        if (_selectedMaterial is not { } material)
        {
            return false;
        }

        var choice = MaterialFinishChoice.For(material.Name, _selectedFinish?.Finish);

        MaterialFinishResult result;
        try
        {
            result = _target.Apply(choice)
                ?? throw new InvalidOperationException("The target returned no result.");
        }
        catch (Exception ex)
        {
            // A UI boundary: the window shows the reason and stays open so the user can retry or cancel.
            _log.Error("Could not apply the material and finish.", ex);
            SetStatus(ApplyErrorPrefix + ex.Message, true, Array.Empty<string>());
            return false;
        }

        var notes = result.Notes ?? Array.Empty<string>();
        if (!result.Succeeded)
        {
            var failed = string.IsNullOrWhiteSpace(result.Message) ? ApplyFailedText : result.Message.Trim();
            SetStatus(WithNoteCount(failed, notes.Count), true, notes);
            return false;
        }

        var message = string.IsNullOrWhiteSpace(result.Message) ? AppliedText : result.Message.Trim();
        SetStatus(WithNoteCount(message, notes.Count), false, notes);

        _current = _current with
        {
            Material = choice.MaterialName,
            FinishName = choice.Finish?.Name ?? "",
            FinishCode = choice.Finish?.Code ?? "",
        };
        OnPropertyChanged(nameof(Current));
        OnPropertyChanged(nameof(CurrentMaterialText));
        OnPropertyChanged(nameof(CurrentFinishText));
        OnPropertyChanged(nameof(HeaderSubline));
        return true;
    }

    private void Ok()
    {
        if (Apply())
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private void RefreshMaterials()
    {
        // Filters change what is shown, never the selection.
        var kept = MaterialFilter.Apply(
            _materialRows.Select(r => r.Material), _selectedCategory, _preferredOnly, _preferred, _materialSearch);
        Materials = kept.Select(m => _rowByMaterial[m]).ToList().AsReadOnly();
        RaiseListSelections();
    }

    private void RefreshFinishes()
    {
        if (_selectedMaterial is null)
        {
            Finishes = Array.Empty<FinishRow>();
            SelectedFinish = null;
            RaiseListSelections();
            return;
        }

        var rows = new List<FinishRow> { FinishRow.None };
        rows.AddRange(FinishMatching.For(_finishRows.Select(r => r.Finish!), _selectedMaterial.Material, _showPaints, _finishSearch)
            .Select(f => _rowByFinish[f]));
        Finishes = rows.AsReadOnly();

        // Back to None only when the selected finish does not suit the selected material; Show paints and the
        // search box only hide it.
        if (_selectedFinish?.Finish is not { } finish || !FinishMatching.Suits(finish, _selectedMaterial.Material))
        {
            SelectedFinish = FinishRow.None;
        }

        RaiseListSelections();
    }

    private void RaiseListSelections()
    {
        OnPropertyChanged(nameof(MaterialListSelection));
        OnPropertyChanged(nameof(FinishListSelection));
        OnPropertyChanged(nameof(SelectionIsHidden));
    }

    /// <summary>The library row for the header's current material, or null when it is blank, varies or not in the library.</summary>
    private MaterialRow? FindCurrentMaterialRow()
    {
        var material = (_current.Material ?? "").Trim();
        if (material.Length == 0 || MaterialFinishCurrent.IsVaries(material))
        {
            return null;
        }

        return _materialRows.FirstOrDefault(r => string.Equals(NameOf(r.Material), material, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Selects the current material when it is in the library, and the current finish when it suits that material
    /// (matched by code, then by name).
    /// </summary>
    private void Preselect(MaterialRow? row)
    {
        if (row is null)
        {
            return;
        }

        SelectedMaterial = row;

        var code = (_current.FinishCode ?? "").Trim();
        var name = (_current.FinishName ?? "").Trim();
        var suiting = _finishRows.Where(f => FinishMatching.Suits(f.Finish!, row.Material)).ToList();
        FinishRow? finish = null;
        if (code.Length > 0 && !MaterialFinishCurrent.IsVaries(code))
        {
            finish = suiting.FirstOrDefault(f => string.Equals(f.Code.Trim(), code, StringComparison.OrdinalIgnoreCase));
        }

        if (finish is null && name.Length > 0 && !MaterialFinishCurrent.IsVaries(name))
        {
            finish = suiting.FirstOrDefault(f => string.Equals(f.Name.Trim(), name, StringComparison.OrdinalIgnoreCase));
        }

        if (finish is not null)
        {
            SelectedFinish = finish;
        }
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

    private static bool Blank(string? value) => string.IsNullOrWhiteSpace(value);

    private static string NameOf(LibraryMaterial material) => (material.DisplayName ?? "").Trim();
}
