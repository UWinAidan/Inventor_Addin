using InventorAddin.Core.Numbering;
using InventorAddin.Core.PartProperties;
using InventorAddin.Core.Settings;

namespace InventorAddin.Core.ViewModels;

/// <summary>
/// What the New Part dialog asks the command to create. <see cref="PartName"/> is normalised
/// (<see cref="PartFileName.NormaliseName"/>), <see cref="PartType"/> is the code in the list's spelling or
/// <c>""</c>, and <see cref="Folder"/> is trimmed.
/// </summary>
public sealed record NewPartRequest(NewFileKind Kind, string PartName, string PartType, UnitsSystem Units, string Folder);

/// <summary>One entry in the File type dropdown.</summary>
public sealed record NewFileKindOption(NewFileKind Kind, string DisplayText);

/// <summary>
/// View-model for the New Part dialog (spec 02, "Commands: New Part"). It holds the fields, keeps the part type
/// following the kind until the user picks one, previews the file name with a placeholder number, and validates
/// the name, folder and path length. Create stores a <see cref="NewPartRequest"/> in <see cref="Request"/> and
/// raises <see cref="CloseRequested"/>; the command assigns the number and creates the file.
/// </summary>
/// <remarks>
/// Designer and Detailer are not fields: they come from <c>DefaultDesigner</c> in Settings (Default). Whether the
/// target file already exists is checked by the command, which knows the real number.
/// </remarks>
public sealed class NewPartViewModel : ObservableObject
{
    public const string HeaderTitleText = "New Part";

    /// <summary>Stands in for the part number in the preview. As long as a real number, so the path check holds.</summary>
    public const string NumberPlaceholder = "AWB-?????";

    public const string ChooseFolderMessage = "Choose a folder.";

    private static readonly IReadOnlyList<NewFileKindOption> AllKindOptions =
        NewFileKinds.All.Select(kind => new NewFileKindOption(kind, kind.DisplayName())).ToList().AsReadOnly();

    private static readonly IReadOnlyList<UnitsOption> AllUnitsOptions = new[]
    {
        new UnitsOption(UnitsSystem.Metric, "Metric"),
        new UnitsOption(UnitsSystem.Imperial, "Imperial"),
    };

    private NewFileKind _kind = NewFileKind.Part;
    private string _partType;
    private bool _partTypeChosen;
    private string _partName = string.Empty;
    private UnitsSystem _units;
    private string _folder;

    private string _fileNamePreview = string.Empty;
    private string? _validationMessage;

    public NewPartViewModel(string? defaultDesigner, UnitsSystem defaultUnits, string? defaultFolder)
    {
        Designer = PropertyValues.NormaliseText(defaultDesigner);
        _units = defaultUnits;
        _folder = defaultFolder ?? string.Empty;
        _partType = DefaultPartType(_kind);

        var options = new List<PartTypeOption> { PartTypeOption.Blank };
        options.AddRange(PartTypes.All.Select(PartTypeOption.From));
        PartTypeOptions = options.AsReadOnly();

        CreateCommand = new RelayCommand(Create, () => CanCreate);
        Recalculate();
    }

    /// <summary>Raised when Create has stored <see cref="Request"/> and the window should close.</summary>
    public event EventHandler? CloseRequested;

    public string HeaderTitle => HeaderTitleText;

    public string HeaderSubline => Branding.ProductName;

    /// <summary>The Designer and Detailer the new file will get (<c>DefaultDesigner</c>, trimmed); may be blank.</summary>
    public string Designer { get; }

    /// <summary>Part, Sheet metal part, Assembly.</summary>
    public IReadOnlyList<NewFileKindOption> KindOptions => AllKindOptions;

    /// <summary>The blank entry, then <see cref="PartTypes.All"/>, as in Part Properties.</summary>
    public IReadOnlyList<PartTypeOption> PartTypeOptions { get; }

    public IReadOnlyList<UnitsOption> UnitsOptions => AllUnitsOptions;

    /// <summary>
    /// The kind of file. Until the user picks a part type, changing it sets <see cref="PartType"/> to the kind's
    /// default (<see cref="PartTypes.DefaultFor"/>: A for an assembly, blank otherwise).
    /// </summary>
    public NewFileKind Kind
    {
        get => _kind;
        set
        {
            if (_kind == value)
                return;

            _kind = value;
            OnPropertyChanged();
            if (!_partTypeChosen)
            {
                SetPartType(DefaultPartType(value));
            }

            OnStateChanged();
        }
    }

    /// <summary>
    /// The part type code, held in the list's spelling. Setting it to a different value counts as the user's
    /// choice, after which it no longer follows <see cref="Kind"/>.
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

            _partTypeChosen = true;
            SetPartType(normalised);
        }
    }

    /// <summary>The full name of <see cref="PartType"/>; empty for a blank code.</summary>
    public string PartTypeFullName => PartTypes.FullNameFor(_partType);

    /// <summary>The part name as typed.</summary>
    public string PartName
    {
        get => _partName;
        set
        {
            value ??= string.Empty;
            if (_partName == value)
                return;

            _partName = value;
            OnPropertyChanged();
            OnStateChanged();
        }
    }

    public UnitsSystem Units
    {
        get => _units;
        set => SetProperty(ref _units, value);
    }

    /// <summary>The folder the file is saved in, as typed or browsed.</summary>
    public string Folder
    {
        get => _folder;
        set
        {
            value ??= string.Empty;
            if (_folder == value)
                return;

            _folder = value;
            OnPropertyChanged();
            OnStateChanged();
        }
    }

    /// <summary>
    /// The file name the part will get, with <see cref="NumberPlaceholder"/> for the number, for example
    /// <c>AWB-?????,mounting_bracket.ipt</c>; empty while the name is invalid.
    /// </summary>
    public string FileNamePreview => _fileNamePreview;

    /// <summary>
    /// The first problem: the part name (<see cref="PartFileName.ValidateName"/>), then a blank folder
    /// (<see cref="ChooseFolderMessage"/>), then the path length (<see cref="PartFileName.ValidatePath"/>). Null when
    /// there is none.
    /// </summary>
    public string? ValidationMessage => _validationMessage;

    public bool CanCreate => _validationMessage is null;

    /// <summary>Stores <see cref="Request"/> and raises <see cref="CloseRequested"/>. Enabled when <see cref="CanCreate"/>.</summary>
    public RelayCommand CreateCommand { get; }

    /// <summary>What to create, set by <see cref="CreateCommand"/>; null until then.</summary>
    public NewPartRequest? Request { get; private set; }

    private static string DefaultPartType(NewFileKind kind) =>
        PartTypes.DefaultFor(kind.DocumentKindOf())?.Code ?? string.Empty;

    private void SetPartType(string code)
    {
        if (_partType == code)
            return;

        _partType = code;
        OnPropertyChanged(nameof(PartType));
        OnPropertyChanged(nameof(PartTypeFullName));
    }

    private void Recalculate()
    {
        var folder = _folder.Trim();
        var nameProblem = PartFileName.ValidateName(_partName);

        _fileNamePreview = nameProblem is null
            ? NumberPlaceholder + PartFileName.NumberSeparator + PartFileName.Build(null, _partName, _kind.Extension())
            : string.Empty;

        _validationMessage = nameProblem
            ?? (folder.Length == 0 ? ChooseFolderMessage : null)
            ?? PartFileName.ValidatePath(folder, _fileNamePreview);
    }

    private void Create()
    {
        if (!CanCreate)
            return;

        Request = new NewPartRequest(_kind, PartFileName.NormaliseName(_partName), _partType, _units, _folder.Trim());
        OnPropertyChanged(nameof(Request));
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnStateChanged()
    {
        Recalculate();
        OnPropertyChanged(nameof(FileNamePreview));
        OnPropertyChanged(nameof(ValidationMessage));
        OnPropertyChanged(nameof(CanCreate));
        CreateCommand.RaiseCanExecuteChanged();
    }
}
