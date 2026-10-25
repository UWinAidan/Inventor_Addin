using InventorAddin.Core.Logging;
using InventorAddin.Core.PartProperties;
using InventorAddin.Core.Settings;
using InventorAddin.Core.ViewModels;

namespace InventorAddin.Core.Numbering;

/// <summary>
/// What <see cref="NewPartCreator"/> needs from Inventor: the default template for a kind of file and units, and a new
/// document made from it. The add-in supplies it; tests use a fake.
/// </summary>
public interface INewPartDocuments
{
    /// <summary>The full path of Inventor's default template, or null or blank when there is none.</summary>
    string? FindTemplate(NewFileKind kind, UnitsSystem units);

    /// <summary>Creates a new, visible, unsaved document of <paramref name="kind"/> from <paramref name="templatePath"/>.</summary>
    INewPartDocument Create(NewFileKind kind, string templatePath);
}

/// <summary>A document <see cref="INewPartDocuments.Create"/> made.</summary>
public interface INewPartDocument
{
    /// <summary>Writes the properties; throws when one cannot be written.</summary>
    void WriteProperties(IReadOnlyList<PropertyWrite> writes);

    /// <summary>Saves the document under <paramref name="fullPath"/>; it then carries that name.</summary>
    void SaveAs(string fullPath);

    /// <summary>Closes the document without saving it.</summary>
    void CloseWithoutSaving();
}

/// <summary>What <see cref="NewPartCreator.Create"/> made.</summary>
/// <param name="PartNumber">The number assigned, for example <c>AWB-10001</c>.</param>
/// <param name="FullPath">Where the new file was saved.</param>
/// <param name="Offline">True when the number came from this PC's copy and waits for the next sync.</param>
/// <param name="DuplicateMessages">One line per duplicate the sync found (<see cref="PartNumberAssignment.DescribeDuplicate"/>).</param>
/// <param name="Note">
/// Something that went partly wrong after the file was made, for the user (for example Save As and Replace's
/// <see cref="SaveAsReplacer.OccurrenceNameKeptMessage"/>); null when nothing did.
/// </param>
public sealed record NewPartResult(
    string PartNumber,
    string FullPath,
    bool Offline,
    IReadOnlyList<string> DuplicateMessages,
    string? Note = null);

/// <summary>
/// New Part could not finish. <see cref="Exception.Message"/> is fit to show the user and says whether a number was
/// used. <see cref="AssignedNumber"/> is that number, or null when nothing was assigned.
/// </summary>
public sealed class NewPartException : Exception
{
    public NewPartException(string message, string? assignedNumber = null, Exception? inner = null)
        : base(message, inner)
    {
        AssignedNumber = assignedNumber;
    }

    /// <summary>The number taken from the register before the failure, or null when none was taken.</summary>
    public string? AssignedNumber { get; }
}

/// <summary>
/// The New Part steps from spec 02 ("Commands: New Part"), in this order: find the template; check the folder; assign
/// the next number, checking under the register lock that no file with that name exists before its row is written;
/// check again that the file does not exist; create the document from the template; write
/// <see cref="CreationProperties"/>; save it under <see cref="PartFileName.Build"/> in the chosen folder. Rows are never
/// removed, so a failure after the number was assigned leaves it in the register, closes the half-made document
/// without saving, logs the exception and says which number was used.
/// </summary>
public sealed class NewPartCreator
{
    public const string NotFullPathMessage = @"Choose a full folder path, such as C:\CAD. Nothing was assigned.";

    public const string OfflineNoticeFormat =
        "Part number {0} was assigned offline; it will be added to the shared register at the next sync.";

    /// <summary>How long the chosen folder has to answer before it counts as unreachable.</summary>
    public static readonly TimeSpan FolderProbeTimeout = RegisterStore.ProbeTimeout;

    private static readonly char[] FolderSeparators = { '\\', '/' };

    private readonly RegisterStore _store;
    private readonly INewPartDocuments _documents;
    private readonly string? _defaultDesigner;
    private readonly ILog _log;
    private readonly Func<string, bool> _folderExists;

    /// <param name="store">The session's register.</param>
    /// <param name="documents">Finds templates and creates documents in Inventor.</param>
    /// <param name="defaultDesigner">Designer, Author and Detailer for the new file, from Settings; blank writes none.</param>
    /// <param name="log">Where failures are recorded.</param>
    /// <param name="folderExists">
    /// Whether a folder exists. By default <see cref="Directory.Exists(string)"/> on a background task that counts as
    /// "no" after <see cref="FolderProbeTimeout"/>, because a disconnected drive can block for many seconds.
    /// </param>
    public NewPartCreator(
        RegisterStore store,
        INewPartDocuments documents,
        string? defaultDesigner,
        ILog? log = null,
        Func<string, bool>? folderExists = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(documents);

        _store = store;
        _documents = documents;
        _defaultDesigner = defaultDesigner;
        _log = log ?? NullLog.Instance;
        _folderExists = folderExists ?? ProbeFolder;
    }

    /// <summary>
    /// The folder the New Part dialog starts in (spec 02, question 23): the folder of the active document when it has
    /// been saved, otherwise <paramref name="workFolder"/> trimmed, otherwise <c>""</c> (Browse is needed). Both
    /// <c>\</c> and <c>/</c> count as folder separators. The project workspace is not used (spike 032 found it blank).
    /// </summary>
    public static string DefaultFolder(string? activeDocumentFullFileName, string? workFolder)
    {
        var active = activeDocumentFullFileName?.Trim() ?? string.Empty;
        var separator = active.LastIndexOfAny(FolderSeparators);
        if (separator > 0)
        {
            var folder = active[..separator];

            // A drive root such as C:\ keeps its separator.
            return folder.EndsWith(':') ? folder + active[separator] : folder;
        }

        return workFolder?.Trim() ?? string.Empty;
    }

    /// <summary>
    /// Names the kind and units, for example
    /// <c>Inventor has no default sheet metal part template for imperial units. Nothing was assigned.</c>
    /// </summary>
    public static string MissingTemplateMessage(NewFileKind kind, UnitsSystem units) =>
        $"Inventor has no default {kind.DisplayName().ToLowerInvariant()} template for {UnitsName(units)} units. " +
        "Nothing was assigned.";

    public static string FolderMissingMessage(string folder) =>
        $"The folder '{folder}' does not exist or cannot be reached. Nothing was assigned.";

    public static string ExistsBeforeAssignMessage(string fullPath) =>
        $"'{fullPath}' already exists, so nothing was created. Nothing was assigned; the register may be missing " +
        "that number (Mass Update can register it).";

    public static string ExistsAfterAssignMessage(string number, string fullPath) =>
        $"Part number {number} was assigned, but '{fullPath}' already exists, so nothing was created. " +
        "The number stays in the register.";

    public static string FailedAfterAssignMessage(string number, NewFileKind kind, string reason) =>
        $"Part number {number} was assigned, but the {kind.DisplayName().ToLowerInvariant()} could not be created: " +
        $"{reason} The number stays in the register.";

    /// <summary>
    /// What to tell the user after a successful create, or null when there is nothing to say: the offline notice
    /// (<see cref="OfflineNoticeFormat"/>) and the duplicates found by the sync
    /// (<see cref="PartPropertiesViewModel.FormatDuplicates"/>), as Generate part number reports them, then the
    /// result's <see cref="NewPartResult.Note"/>.
    /// </summary>
    public static string? Notice(NewPartResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var parts = new List<string>();
        if (result.Offline)
        {
            parts.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture, OfflineNoticeFormat, result.PartNumber));
        }

        if (result.DuplicateMessages.Count > 0)
        {
            parts.Add(PartPropertiesViewModel.FormatDuplicates(result.DuplicateMessages));
        }

        if (!string.IsNullOrWhiteSpace(result.Note))
        {
            parts.Add(result.Note);
        }

        return parts.Count == 0 ? null : string.Join(Environment.NewLine + Environment.NewLine, parts);
    }

    /// <summary>Runs the steps for <paramref name="request"/>. The new document is left open.</summary>
    /// <exception cref="NewPartException">A step failed; the message says why and whether a number was used.</exception>
    public NewPartResult Create(NewPartRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var folder = request.Folder.Trim();

        // 1. The template, before anything else, so a missing one costs no number.
        var template = _documents.FindTemplate(request.Kind, request.Units)?.Trim();
        if (string.IsNullOrEmpty(template))
        {
            throw new NewPartException(MissingTemplateMessage(request.Kind, request.Units));
        }

        // 2. The folder: a full path that can be reached.
        CheckFolder(folder, _folderExists);

        // 3. The number. The name holds the number, so the path is built and checked once the number is known, before
        // its row is written; a file already there costs no number.
        var (assignment, path) = AssignNumber(
            _store,
            _log,
            "a new part",
            number => FullPathFor(folder, number, request),
            fullPath => new RegisterRequest(request.PartName, request.PartType, request.Kind.DocumentKindOf(), fullPath));

        var assigned = assignment.PartNumber.ToString();

        // 4. Check again, create, write, save. Any failure from here leaves the number in the register.
        INewPartDocument? document = null;
        try
        {
            if (File.Exists(path))
            {
                throw new NewPartException(ExistsAfterAssignMessage(assigned, path), assigned);
            }

            document = _documents.Create(request.Kind, template);
            document.WriteProperties(
                CreationProperties.Build(assignment.PartNumber, request.PartName, request.PartType, _defaultDesigner));
            document.SaveAs(path);
        }
        catch (Exception ex)
        {
            _log.Error(
                $"Part number {assigned} was assigned for '{path}', but the new file could not be created. " +
                "The register entry stays.", ex);

            if (document is not null)
            {
                CloseQuietly(document, assigned);
            }

            if (ex is NewPartException)
            {
                throw;
            }

            throw new NewPartException(FailedAfterAssignMessage(assigned, request.Kind, ex.Message), assigned, ex);
        }

        var duplicates = assignment.Duplicates.Select(PartNumberAssignment.DescribeDuplicate).ToList();
        return new NewPartResult(assigned, path, assignment.Offline, duplicates.AsReadOnly());
    }

    /// <summary>
    /// Stops with a <see cref="NewPartException"/> unless <paramref name="folder"/> is a full path that
    /// <paramref name="folderExists"/> finds. Nothing is assigned before this. Shared with
    /// <see cref="SaveAsCopyCreator"/>.
    /// </summary>
    internal static void CheckFolder(string folder, Func<string, bool> folderExists)
    {
        if (folder.Length == 0 || !Path.IsPathFullyQualified(folder))
        {
            throw new NewPartException(NotFullPathMessage);
        }

        if (!folderExists(folder))
        {
            throw new NewPartException(FolderMissingMessage(folder));
        }
    }

    /// <summary>
    /// Assigns the next number for a new file whose name holds it. <paramref name="fullPathFor"/> builds the file's full
    /// path from the number about to be assigned (it may throw a <see cref="NewPartException"/> for a path that is too
    /// long); a file already at that path stops before the row is written, so it costs no number. Register failures
    /// become <see cref="NewPartException"/>s that say whether a number may have been taken. Shared with
    /// <see cref="SaveAsCopyCreator"/>.
    /// </summary>
    /// <param name="what">For the log, for example <c>a new part</c>.</param>
    internal static (RegisterAssignment Assignment, string FullPath) AssignNumber(
        RegisterStore store,
        ILog log,
        string what,
        Func<PartNumber, string> fullPathFor,
        Func<string, RegisterRequest> requestFor)
    {
        string? fullPath = null;
        RegisterAssignment assignment;
        try
        {
            assignment = store.Assign(number =>
            {
                fullPath = fullPathFor(number);
                if (File.Exists(fullPath))
                {
                    throw new TargetExistsException(fullPath);
                }

                return requestFor(fullPath);
            });
        }
        catch (TargetExistsException ex)
        {
            throw new NewPartException(ExistsBeforeAssignMessage(ex.FullPath), inner: ex);
        }
        catch (NewPartException)
        {
            throw;
        }
        catch (Exception ex) when (ex is RegisterBusyException or RegisterUnreadableException)
        {
            throw new NewPartException(ex.Message, inner: ex);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The shared file may already hold the new row (for example when only the local copy failed).
            log.Error($"Could not update the part register while assigning a number for {what}.", ex);
            throw new NewPartException(
                $"The part register could not be updated: {ex.Message} A number may already have been taken in the " +
                "register; check it before trying again.", inner: ex);
        }
        catch (InvalidOperationException ex)
        {
            // The sequence is full; nothing was assigned.
            throw new NewPartException(ex.Message, inner: ex);
        }

        return (assignment, fullPath!);
    }

    /// <summary>
    /// <paramref name="folder"/> joined with <see cref="PartFileName.Build"/>; a path too long for Windows stops with a
    /// <see cref="NewPartException"/> before anything is assigned.
    /// </summary>
    internal static string FullPathFor(string folder, PartNumber number, string partName, string extension)
    {
        var fileName = PartFileName.Build(number, partName, extension);
        var problem = PartFileName.ValidatePath(folder, fileName);
        if (problem is not null)
        {
            throw new NewPartException(problem + " Nothing was assigned.");
        }

        return Path.Combine(folder, fileName);
    }

    private static string FullPathFor(string folder, PartNumber number, NewPartRequest request) =>
        FullPathFor(folder, number, request.PartName, request.Kind.Extension());

    private void CloseQuietly(INewPartDocument document, string assigned)
    {
        try
        {
            document.CloseWithoutSaving();
        }
        catch (Exception closeEx)
        {
            _log.Error($"The half-made document for part number {assigned} could not be closed.", closeEx);
        }
    }

    private static string UnitsName(UnitsSystem units) => units switch
    {
        UnitsSystem.Metric => "metric",
        UnitsSystem.Imperial => "imperial",
        _ => units.ToString().ToLowerInvariant(),
    };

    /// <summary>
    /// <see cref="Directory.Exists(string)"/> on a background task that counts as "no" after
    /// <see cref="FolderProbeTimeout"/>, because a disconnected drive can block for many seconds.
    /// </summary>
    internal static bool ProbeFolder(string folder)
    {
        var probe = Task.Run(() => Directory.Exists(folder));
        try
        {
            return probe.Wait(FolderProbeTimeout) && probe.Result;
        }
        catch (AggregateException)
        {
            return false;
        }
    }

    /// <summary>Thrown inside the register callback so that no row is written for a name already taken on disk.</summary>
    private sealed class TargetExistsException : Exception
    {
        public TargetExistsException(string fullPath)
            : base($"'{fullPath}' already exists.")
        {
            FullPath = fullPath;
        }

        public string FullPath { get; }
    }
}
