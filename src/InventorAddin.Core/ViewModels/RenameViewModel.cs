using System.Globalization;
using InventorAddin.Core.Logging;
using InventorAddin.Core.Models;
using InventorAddin.Core.Numbering;
using InventorAddin.Core.PartProperties;

namespace InventorAddin.Core.ViewModels;

/// <summary>
/// Renames the open file from the Part Properties window (spec 02, "Commands: Rename"; task 047). The add-in supplies
/// it: it shows the Rename window and does the rename. Tests use a fake.
/// </summary>
public interface IPartRenamer
{
    /// <summary>
    /// Asks for the new name and renames the file. Null when nothing was renamed (cancelled, or the file could not be
    /// saved under its new name). Throws, with a message fit to show, only when something unexpected went wrong.
    /// </summary>
    PartRenameOutcome? Rename();
}

/// <summary>What a rename changed in the open file.</summary>
/// <param name="NewFullFileName">The file's new full path.</param>
/// <param name="NewName">The new part name, with spaces.</param>
/// <param name="DescriptionWritten">True when Description was set to <paramref name="NewName"/>.</param>
public sealed record PartRenameOutcome(string NewFullFileName, string NewName, bool DescriptionWritten);

/// <summary>
/// View-model for the Rename window (spec 02, "Commands: Rename"; task 047). It asks for the new part name, pre-filled
/// with the current one, validates it (<see cref="RenamePlan.ValidateNewName"/>, then the caller's check that the new
/// file does not already exist), and previews the new file name. Rename hands a <see cref="RenameRequest"/> to the
/// caller's runner, which confirms, renames and returns the <see cref="RenameResult"/>; the window then shows the result
/// list: the files updated, the files that were not with the reason, and any other note.
/// </summary>
/// <remarks>The window has three states: editing the name, running (with a progress line), and done.</remarks>
public sealed class RenameViewModel : ObservableObject
{
    public const string HeaderTitleText = "Rename";
    public const string UpdatedPrefix = "Updated: ";
    public const string NotUpdatedPrefix = "Not updated: ";
    public const string RenamedToPrefix = ", renamed to ";
    public const string NothingElseText = "No other file in the CAD folders uses it.";
    public const string RunErrorPrefix = "Rename failed: ";
    public const string StartingText = "Looking for files that use it...";
    public const string CancelButtonText = "Cancel";
    public const string CloseButtonText = "Close";

    private readonly string _fullFileName;
    private readonly Func<string, bool> _targetExists;
    private readonly Func<RenameRequest, Action<string>, RenameResult?> _run;
    private readonly ILog _log;
    private readonly PartNumber? _registeredNumber;
    private readonly DocumentKind _kind;

    private string _newName;
    private string? _validationMessage;
    private string _newFullFileName = string.Empty;
    private bool _isRunning;
    private string _progressText = string.Empty;
    private RenameResult? _result;
    private string? _runError;

    /// <param name="fullFileName">The open file's full path; it has been saved.</param>
    /// <param name="targetExists">
    /// True when a file already exists at the given full path (or is open in Inventor under it). Called as the name is
    /// typed, for a valid name only.
    /// </param>
    /// <param name="run">
    /// Confirms and renames, reporting progress through the action it is given. Returns null when the user did not
    /// confirm, so the window stays open for another name.
    /// </param>
    /// <param name="log">Where an unexpected failure is recorded.</param>
    /// <param name="registeredNumber">
    /// The file's Part Number when the register holds it (<c>RegisterStore.HasNumber</c>), otherwise null. The new file
    /// name keeps it (<see cref="RenamePlan.NumberToKeep"/>).
    /// </param>
    /// <param name="kind">
    /// The open file's kind. An assembly's or weldment's new name gets <see cref="PartFileName.AssemblyPrefix"/>
    /// (<see cref="PartFileName.ForKind"/>).
    /// </param>
    public RenameViewModel(
        string fullFileName,
        Func<string, bool> targetExists,
        Func<RenameRequest, Action<string>, RenameResult?> run,
        ILog? log = null,
        PartNumber? registeredNumber = null,
        DocumentKind kind = DocumentKind.Part)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullFileName);
        ArgumentNullException.ThrowIfNull(targetExists);
        ArgumentNullException.ThrowIfNull(run);

        _fullFileName = fullFileName.Trim();
        _targetExists = targetExists;
        _run = run;
        _log = log ?? NullLog.Instance;

        CurrentName = PartName.FromFileName(_fullFileName);
        _newName = CurrentName;
        _registeredNumber = registeredNumber;
        _kind = kind;
        NumberKept = RenamePlan.NumberToKeep(_fullFileName, registeredNumber);

        RenameCommand = new RelayCommand(Rename, () => CanRename);
        Validate();
    }

    /// <summary>Raised when the window should close (Close after the rename).</summary>
    public event EventHandler? CloseRequested;

    public string HeaderTitle => HeaderTitleText;

    /// <summary>The current file name, without its folder.</summary>
    public string HeaderSubline => RenamePlan.FileNameOnly(_fullFileName);

    /// <summary>The file's current full path.</summary>
    public string FullFileName => _fullFileName;

    /// <summary>The current part name (<see cref="PartName.FromFileName"/>).</summary>
    public string CurrentName { get; }

    /// <summary>The part number the new file name ends with (<see cref="RenamePlan.NumberToKeep"/>), or null for none.</summary>
    public PartNumber? NumberKept { get; }

    /// <summary>The new part name as typed; pre-filled with <see cref="CurrentName"/>.</summary>
    public string NewName
    {
        get => _newName;
        set
        {
            value ??= string.Empty;
            if (_newName == value || !IsEditing)
                return;

            _newName = value;
            OnPropertyChanged();
            Validate();
        }
    }

    /// <summary>The new file name without its folder; empty while the name cannot be used for another reason than the file existing.</summary>
    public string NewFileNamePreview => RenamePlan.FileNameOnly(_newFullFileName);

    /// <summary>The new full path; empty while the name is not valid.</summary>
    public string NewFullFileName => _newFullFileName;

    /// <summary>The first problem with the new name, or null.</summary>
    public string? ValidationMessage => _validationMessage;

    /// <summary>True while the name can be edited: before Rename, or after the user did not confirm.</summary>
    public bool IsEditing => !_isRunning && !IsDone;

    public bool IsRunning => _isRunning;

    /// <summary>True once the rename has run (whatever its outcome) or failed unexpectedly.</summary>
    public bool IsDone => _result is not null || _runError is not null;

    public bool CanRename => IsEditing && _validationMessage is null;

    /// <summary>Hands the request to the runner, then shows the result.</summary>
    public RelayCommand RenameCommand { get; }

    /// <summary>The text of the button that closes the window: Cancel before the rename, Close after it.</summary>
    public string CloseText => IsDone ? CloseButtonText : CancelButtonText;

    /// <summary>False while the rename runs: the window must not close part way.</summary>
    public bool CanClose => !_isRunning;

    /// <summary>The step under way while running.</summary>
    public string ProgressText => _progressText;

    /// <summary>The result, once the rename has run; null before, or when it failed unexpectedly.</summary>
    public RenameResult? Result => _result;

    /// <summary>What the Part Properties window takes back: null unless the file was renamed.</summary>
    public PartRenameOutcome? Outcome => _result is { Renamed: true } r
        ? new PartRenameOutcome(r.Request.NewFullFileName, r.Request.NewName, r.DescriptionWritten)
        : null;

    /// <summary>The footer line: the validation message, the progress, or the summary of the result.</summary>
    public string StatusText
    {
        get
        {
            if (_runError is not null)
                return _runError;
            if (_result is not null)
                return Summary(_result);
            if (_isRunning)
                return _progressText;
            return _validationMessage ?? string.Empty;
        }
    }

    /// <summary>True when <see cref="StatusText"/> is a problem, so it shows in the error style.</summary>
    public bool HasError =>
        _runError is not null
        || (_result is not null && _result.HasProblems)
        || (IsEditing && _validationMessage is not null);

    /// <summary>The result list, one line per file and note; empty before the rename has run.</summary>
    public string ResultText => _result is null ? string.Empty : ResultLines(_result);

    /// <summary>
    /// The summary of a result: renamed or not, how many files were updated and how many were not, how many occurrences
    /// were renamed and drawings renamed (when any), and whether the old file was kept.
    /// </summary>
    public static string Summary(RenameResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (!result.Renamed)
            return "Not renamed. Nothing was changed.";

        var text = string.Create(
            CultureInfo.InvariantCulture,
            $"Renamed to {RenamePlan.FileNameOnly(result.Request.NewFullFileName)}. {Files(result.UpdatedCount)} updated");
        if (result.FailedCount > 0)
            text += string.Create(CultureInfo.InvariantCulture, $", {result.FailedCount} not updated");
        if (result.OccurrencesRenamed > 0)
        {
            text += result.OccurrencesRenamed == 1
                ? ", 1 occurrence renamed"
                : string.Create(CultureInfo.InvariantCulture, $", {result.OccurrencesRenamed} occurrences renamed");
        }

        if (result.DrawingsRenamed > 0)
        {
            text += result.DrawingsRenamed == 1
                ? ", 1 drawing renamed"
                : string.Create(CultureInfo.InvariantCulture, $", {result.DrawingsRenamed} drawings renamed");
        }

        text += ".";
        if (!result.OldFileDeleted)
            text += " The old file was kept.";
        return text;
    }

    /// <summary>
    /// The result list: <see cref="UpdatedPrefix"/> and each updated file (with <see cref="RenamedToPrefix"/> and the new
    /// name for a drawing renamed with the model), <see cref="NotUpdatedPrefix"/> and each file
    /// that was not with the reason, then each note. <see cref="NothingElseText"/> when the file was renamed and no other
    /// file was changed.
    /// </summary>
    public static string ResultLines(RenameResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var lines = new List<string>();
        if (result.Renamed && result.Files.Count == 0)
            lines.Add(NothingElseText);

        lines.AddRange(result.Files.Where(f => f.Updated).Select(f =>
            UpdatedPrefix + f.FullFileName +
            (f.RenamedTo is { } renamedTo ? RenamedToPrefix + RenamePlan.FileNameOnly(renamedTo) : string.Empty)));
        lines.AddRange(result.Files.Where(f => !f.Updated).Select(f => $"{NotUpdatedPrefix}{f.FullFileName}: {f.Problem}"));
        lines.AddRange(result.Notes.Select(n => n.Text));
        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>Raises <see cref="CloseRequested"/>.</summary>
    public void Close() => CloseRequested?.Invoke(this, EventArgs.Empty);

    private static string Files(int n) => n == 1 ? "1 file" : string.Create(CultureInfo.InvariantCulture, $"{n} files");

    private void Validate()
    {
        _newFullFileName = string.Empty;
        _validationMessage = RenamePlan.ValidateNewName(_fullFileName, _newName, _registeredNumber, _kind);
        if (_validationMessage is null)
        {
            _newFullFileName = RenamePlan.NewFileName(_fullFileName, _newName, _registeredNumber, _kind);
            if (_targetExists(_newFullFileName))
                _validationMessage = RenamePlan.TargetExistsMessage(RenamePlan.FileNameOnly(_newFullFileName));
        }

        OnPropertyChanged(nameof(NewFullFileName));
        OnPropertyChanged(nameof(NewFileNamePreview));
        OnPropertyChanged(nameof(ValidationMessage));
        OnStateChanged();
    }

    private void Rename()
    {
        if (!CanRename)
            return;

        var request = new RenameRequest(_fullFileName, _newFullFileName, PartFileName.ForKind(_newName, _kind));

        _isRunning = true;
        _progressText = StartingText;
        OnStateChanged();

        RenameResult? result;
        try
        {
            result = _run(request, SetProgress);
        }
        catch (Exception ex)
        {
            // A UI boundary: the window shows the reason; the log has the detail.
            _log.Error($"Rename of '{_fullFileName}' failed.", ex);
            _runError = RunErrorPrefix + ex.Message;
            _isRunning = false;
            OnStateChanged();
            return;
        }

        _isRunning = false;
        _result = result;
        if (result is null)
        {
            // Not confirmed: back to editing.
            _progressText = string.Empty;
            Validate();
            return;
        }

        OnPropertyChanged(nameof(Result));
        OnPropertyChanged(nameof(Outcome));
        OnPropertyChanged(nameof(ResultText));
        OnStateChanged();
    }

    private void SetProgress(string text)
    {
        _progressText = text ?? string.Empty;
        OnPropertyChanged(nameof(ProgressText));
        OnPropertyChanged(nameof(StatusText));
    }

    private void OnStateChanged()
    {
        OnPropertyChanged(nameof(IsEditing));
        OnPropertyChanged(nameof(IsRunning));
        OnPropertyChanged(nameof(IsDone));
        OnPropertyChanged(nameof(CanRename));
        OnPropertyChanged(nameof(CloseText));
        OnPropertyChanged(nameof(CanClose));
        OnPropertyChanged(nameof(ProgressText));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(HasError));
        RenameCommand.RaiseCanExecuteChanged();
    }
}
