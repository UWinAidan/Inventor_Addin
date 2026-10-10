using InventorAddin.Core.Models;
using InventorAddin.Core.Numbering;
using InventorAddin.Core.PartProperties;

namespace InventorAddin.Core.ViewModels;

/// <summary>
/// View-model for the Save As Copy dialog (spec 02, "Commands: Save As Copy"). It asks for the copy's name and part
/// type, pre-filled from the original, and previews and validates the file name as New Part does
/// (<see cref="NewFileNameCheck"/>). The folder and the extension are the original's. Save stores a
/// <see cref="SaveAsCopyRequest"/> in <see cref="Request"/> and raises <see cref="CloseRequested"/>; the command
/// assigns the number and makes the copy (<see cref="SaveAsCopyCreator"/>).
/// </summary>
/// <remarks>
/// Designer and Detailer are not fields: they come from <c>DefaultDesigner</c> in Settings, as in New Part. A blank
/// part type on the original pre-fills the kind's default (A for an assembly, W for a weldment), as Part Properties
/// and New Part do (Default).
/// </remarks>
public sealed class SaveAsCopyViewModel : ObservableObject
{
    public const string HeaderTitleText = "Save As Copy";

    private readonly string _originalFullFileName;
    private readonly string _extension;

    private string _partType;
    private string _partName;
    private NewFileNameCheck _preview;

    /// <param name="originalFullFileName">The open original's full path; it has been saved.</param>
    /// <param name="kind">The original's kind.</param>
    /// <param name="originalPartType">The original's Part Type, as read.</param>
    /// <param name="defaultDesigner">Designer and Detailer for the copy, from Settings.</param>
    public SaveAsCopyViewModel(
        string originalFullFileName,
        DocumentKind kind,
        string? originalPartType,
        string? defaultDesigner)
    {
        ArgumentNullException.ThrowIfNull(originalFullFileName);

        _originalFullFileName = originalFullFileName.Trim();
        Kind = kind;
        Folder = NewPartCreator.DefaultFolder(_originalFullFileName, null);
        _extension = SaveAsCopyCreator.ExtensionOf(_originalFullFileName);
        Designer = PropertyValues.NormaliseText(defaultDesigner);

        var fileType = PropertyValues.NormaliseText(originalPartType);
        _partType = PartTypes.Find(fileType)?.Code ?? fileType;
        if (_partType.Length == 0)
        {
            _partType = PartTypes.DefaultFor(kind)?.Code ?? string.Empty;
        }

        PartTypeOptions = PartTypeOption.ListFor(fileType);

        _partName = InventorAddin.Core.PartProperties.PartName.FromFileName(_originalFullFileName);
        _preview = NewFileNameCheck.For(_partName, _extension, Folder);

        SaveCommand = new RelayCommand(Save, () => CanSave);
    }

    /// <summary>Raised when Save has stored <see cref="Request"/> and the window should close.</summary>
    public event EventHandler? CloseRequested;

    public string HeaderTitle => HeaderTitleText;

    /// <summary>The original's file name, without its folder, for example <c>AWB-10001,mounting_bracket.ipt</c>.</summary>
    public string HeaderSubline => FileNameOnly(_originalFullFileName);

    /// <summary>
    /// <see cref="SaveAsCopyCreator.AssemblyNote"/> for an assembly or weldment, whose components the copy shares with
    /// the original; empty for a part.
    /// </summary>
    public string Note => SaveAsCopyCreator.IsAssembly(Kind) ? SaveAsCopyCreator.AssemblyNote : string.Empty;

    public bool HasNote => Note.Length > 0;

    /// <summary>The original's kind.</summary>
    public DocumentKind Kind { get; }

    /// <summary>The original's folder, where the copy is saved. Not editable.</summary>
    public string Folder { get; }

    /// <summary>The Designer and Detailer the copy will get (<c>DefaultDesigner</c>, trimmed); may be blank.</summary>
    public string Designer { get; }

    /// <summary>The blank entry, <see cref="PartTypes.All"/>, and the original's code when it is not in the list.</summary>
    public IReadOnlyList<PartTypeOption> PartTypeOptions { get; }

    /// <summary>The part type code, held in the list's spelling; pre-filled from the original.</summary>
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
        }
    }

    /// <summary>The full name of <see cref="PartType"/>; empty for a blank code.</summary>
    public string PartTypeFullName => PartTypes.FullNameFor(_partType);

    /// <summary>The copy's part name as typed; pre-filled with the original's (<see cref="InventorAddin.Core.PartProperties.PartName.FromFileName"/>).</summary>
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
            _preview = NewFileNameCheck.For(_partName, _extension, Folder);
            OnPropertyChanged(nameof(FileNamePreview));
            OnPropertyChanged(nameof(ValidationMessage));
            OnPropertyChanged(nameof(CanSave));
            SaveCommand.RaiseCanExecuteChanged();
        }
    }

    /// <summary>The copy's file name with <see cref="NewFileNameCheck.NumberPlaceholder"/>; empty while the name is invalid.</summary>
    public string FileNamePreview => _preview.FileName;

    /// <summary>The first problem (<see cref="NewFileNameCheck.Problem"/>), or null.</summary>
    public string? ValidationMessage => _preview.Problem;

    public bool CanSave => _preview.Problem is null;

    /// <summary>Stores <see cref="Request"/> and raises <see cref="CloseRequested"/>. Enabled when <see cref="CanSave"/>.</summary>
    public RelayCommand SaveCommand { get; }

    /// <summary>What to copy, set by <see cref="SaveCommand"/>; null until then.</summary>
    public SaveAsCopyRequest? Request { get; private set; }

    private void Save()
    {
        if (!CanSave)
            return;

        Request = new SaveAsCopyRequest(_originalFullFileName, Kind, PartFileName.NormaliseName(_partName), _partType);
        OnPropertyChanged(nameof(Request));
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private static string FileNameOnly(string fullFileName) =>
        fullFileName[(fullFileName.LastIndexOfAny(['\\', '/']) + 1)..];
}
