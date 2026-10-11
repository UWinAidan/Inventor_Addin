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
/// <para>
/// Save As and Replace (task 046) uses the same dialog through <see cref="ForReplace"/>: the header and the Save button
/// say Save As and Replace, the original is the selected component, and <see cref="Warning"/> carries
/// <see cref="SaveAsReplacer.SubAssemblyWarning"/> when the component sits inside a sub-assembly.
/// </para>
/// Designer and Detailer are not fields: they come from <c>DefaultDesigner</c> in Settings, as in New Part. A blank
/// part type on the original pre-fills the kind's default (A for an assembly, W for a weldment), as Part Properties
/// and New Part do (Default).
/// </remarks>
public sealed class SaveAsCopyViewModel : ObservableObject
{
    public const string HeaderTitleText = "Save As Copy";

    /// <summary>The header and window title for Save As and Replace (<see cref="ForReplace"/>).</summary>
    public const string ReplaceHeaderTitleText = "Save As and Replace";

    public const string SaveCopyButtonText = "_Save copy";

    public const string SaveAndReplaceButtonText = "_Save and replace";

    private readonly string _originalFullFileName;
    private readonly string _extension;
    private readonly bool _replace;
    private readonly string _warning;

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
        : this(originalFullFileName, kind, originalPartType, defaultDesigner, replace: false, warning: null)
    {
    }

    private SaveAsCopyViewModel(
        string originalFullFileName,
        DocumentKind kind,
        string? originalPartType,
        string? defaultDesigner,
        bool replace,
        string? warning)
    {
        ArgumentNullException.ThrowIfNull(originalFullFileName);

        _replace = replace;
        _warning = warning?.Trim() ?? string.Empty;
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
        _preview = NewFileNameCheck.For(_partName, _extension, Folder, Kind);

        SaveCommand = new RelayCommand(Save, () => CanSave);
    }

    /// <summary>
    /// The dialog for Save As and Replace (task 046): the same fields, preview and validation as Save As Copy, for the
    /// selected component's file.
    /// </summary>
    /// <param name="componentFullFileName">The selected component's file; it has been saved.</param>
    /// <param name="kind">The component's kind.</param>
    /// <param name="componentPartType">The component's Part Type, as read.</param>
    /// <param name="defaultDesigner">Designer and Detailer for the copy, from Settings.</param>
    /// <param name="warning">
    /// <see cref="SaveAsReplacer.SubAssemblyWarning"/> when the component is inside a sub-assembly; null or blank for a
    /// top-level component.
    /// </param>
    public static SaveAsCopyViewModel ForReplace(
        string componentFullFileName,
        DocumentKind kind,
        string? componentPartType,
        string? defaultDesigner,
        string? warning) =>
        new(componentFullFileName, kind, componentPartType, defaultDesigner, replace: true, warning);

    /// <summary>Raised when Save has stored <see cref="Request"/> and the window should close.</summary>
    public event EventHandler? CloseRequested;

    /// <summary>True for the Save As and Replace dialog (<see cref="ForReplace"/>).</summary>
    public bool IsReplace => _replace;

    /// <summary>The header and window title: Save As Copy, or Save As and Replace.</summary>
    public string HeaderTitle => _replace ? ReplaceHeaderTitleText : HeaderTitleText;

    /// <summary>The Save button's text, with its access key.</summary>
    public string SaveButtonText => _replace ? SaveAndReplaceButtonText : SaveCopyButtonText;

    /// <summary>
    /// Save As and Replace only: what else changes and cannot be undone (a sub-assembly that is saved). Empty otherwise.
    /// </summary>
    public string Warning => _warning;

    public bool HasWarning => _warning.Length > 0;

    /// <summary>The original's file name, without its folder, for example <c>MOUNTING_BRACKET,AWB-10001.ipt</c>.</summary>
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
            _preview = NewFileNameCheck.For(_partName, _extension, Folder, Kind);
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

        Request = new SaveAsCopyRequest(_originalFullFileName, Kind, PartFileName.ForKind(_partName, Kind), _partType);
        OnPropertyChanged(nameof(Request));
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private static string FileNameOnly(string fullFileName) =>
        fullFileName[(fullFileName.LastIndexOfAny(['\\', '/']) + 1)..];
}
