using InventorAddin.Core.Models;

namespace InventorAddin.Core.Materials;

/// <summary>Why a document or a selected component is not given the material and finish.</summary>
public enum MaterialFinishSkip
{
    /// <summary>Not skipped: it is a part that can be changed.</summary>
    None,

    /// <summary>A sub-assembly (spec 03, question 15).</summary>
    SubAssembly,

    /// <summary>A virtual component, which has no file.</summary>
    Virtual,

    /// <summary>A suppressed occurrence, whose document cannot be reached.</summary>
    Suppressed,

    /// <summary>The file is read-only on disk.</summary>
    ReadOnlyFile,

    /// <summary>Inventor will not let the document be modified, for example a library file.</summary>
    NotModifiable,

    /// <summary>Something that is neither a part nor an assembly, or whose document could not be read.</summary>
    NotAPart,
}

/// <summary>
/// One candidate for the material and finish: the part document, or one selected component of an assembly.
/// </summary>
/// <param name="Item">
/// What the add-in applies to when the candidate is kept (an Inventor document). A skipped candidate may have none.
/// </param>
/// <param name="Name">The name used in notes and the log: the occurrence's name or the file name.</param>
/// <param name="FileKey">
/// The file the candidate's document is saved as, so a part selected through several occurrences is changed once;
/// compared ignoring case. Blank never matches another candidate.
/// </param>
/// <param name="Skip">Why it is skipped, or <see cref="MaterialFinishSkip.None"/>.</param>
public sealed record MaterialFinishCandidate<T>(T Item, string Name, string FileKey, MaterialFinishSkip Skip);

/// <summary>The parts the material and finish are applied to, and what was left out.</summary>
/// <param name="Parts">The kept candidates, in selection order, once per file.</param>
/// <param name="Notes">One note per skipped component, for every Apply's result (spec 03, question 15).</param>
/// <param name="StopMessage">Set when no part is left: the command shows it instead of the window.</param>
public sealed record MaterialFinishTargetPlan<T>(
    IReadOnlyList<MaterialFinishCandidate<T>> Parts,
    IReadOnlyList<string> Notes,
    string? StopMessage);

/// <summary>
/// The rules and wording behind the Material and Finish targets (spec 03; task 074): which documents are changed,
/// what is skipped, and the notes, messages and log lines an Apply produces. The add-in reads Inventor and applies;
/// every decision and every message is here.
/// </summary>
public static class MaterialFinishTargeting
{
    /// <summary>Shown when an assembly has no selected part that can be changed (Aidan, 2026-10-12).</summary>
    public const string SelectPartsMessage = "Select one or more parts in the assembly, then run Material and Finish.";

    /// <summary>Shown when the active document is neither a part nor an assembly.</summary>
    public const string WrongDocumentMessage = "Material and Finish works on parts and assemblies.";

    /// <summary>Shown when the part document is a read-only file.</summary>
    public const string PartReadOnlyMessage = "This part's file is read-only, so its material and finish cannot be changed.";

    /// <summary>Shown when Inventor will not let the part document be modified.</summary>
    public const string PartNotModifiableMessage =
        "This part cannot be modified (for example a library file), so its material and finish cannot be changed.";

    /// <summary>Prefix of every log line the targets write.</summary>
    public const string LogPrefix = "Material and Finish: ";

    /// <summary>
    /// True when the command should work on the part being edited rather than on the document open in the window:
    /// the window holds an assembly and a part inside it is edited in place (Default, task 075, as Part Properties
    /// does). Then the part is the target, whatever is selected. Otherwise the open document is used: a part
    /// document, or the assembly's selection (also while a sub-assembly is edited in place, since the selection is
    /// held by the top-level assembly, spike 052).
    /// </summary>
    /// <param name="open">The kind of the document open in the window (<c>ActiveDocument</c>).</param>
    /// <param name="edited">The kind of the document being edited (<c>ActiveEditDocument</c>).</param>
    public static bool TargetsEditedPart(DocumentKind open, DocumentKind edited) =>
        open is DocumentKind.Assembly or DocumentKind.WeldmentAssembly
        && edited is DocumentKind.Part or DocumentKind.SheetMetalPart;

    /// <summary>
    /// The plan for a part document: the part itself, or a stop message when it cannot be changed.
    /// </summary>
    public static MaterialFinishTargetPlan<T> ForPart<T>(MaterialFinishCandidate<T> part)
    {
        ArgumentNullException.ThrowIfNull(part);

        return part.Skip switch
        {
            MaterialFinishSkip.None => new(new[] { part }, Array.Empty<string>(), null),
            MaterialFinishSkip.ReadOnlyFile => Stop<T>(PartReadOnlyMessage),
            MaterialFinishSkip.NotModifiable => Stop<T>(PartNotModifiableMessage),
            _ => throw new ArgumentException($"A part document cannot be skipped as {part.Skip}.", nameof(part)),
        };
    }

    /// <summary>
    /// The plan for the selected components of an assembly (Aidan, 2026-10-12): every candidate that is not skipped,
    /// once per <see cref="MaterialFinishCandidate{T}.FileKey"/>, in selection order. Each skipped component gives one
    /// note (the same note once). <paramref name="otherSelected"/> counts selected things that are not components (a
    /// face, a sketch); they give one note. With no part left, <see cref="SelectPartsMessage"/> stops the command.
    /// </summary>
    public static MaterialFinishTargetPlan<T> ForAssembly<T>(IEnumerable<MaterialFinishCandidate<T>> selected, int otherSelected = 0)
    {
        ArgumentNullException.ThrowIfNull(selected);

        var parts = new List<MaterialFinishCandidate<T>>();
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var notes = new List<string>();

        foreach (var candidate in selected)
        {
            if (candidate.Skip != MaterialFinishSkip.None)
            {
                AddOnce(notes, SkippedNote(candidate.Name, candidate.Skip));
                continue;
            }

            var key = (candidate.FileKey ?? "").Trim();
            if (key.Length == 0 || files.Add(key))
            {
                parts.Add(candidate);
            }
        }

        if (otherSelected > 0)
        {
            notes.Add(otherSelected == 1
                ? "Skipped: 1 selected item that is not a component."
                : $"Skipped: {otherSelected} selected items that are not components.");
        }

        return parts.Count == 0
            ? new(Array.Empty<MaterialFinishCandidate<T>>(), notes.AsReadOnly(), SelectPartsMessage)
            : new(parts.AsReadOnly(), notes.AsReadOnly(), null);
    }

    /// <summary>For example <c>"Skipped: Frame:1 (a sub-assembly)"</c>.</summary>
    public static string SkippedNote(string name, MaterialFinishSkip skip)
    {
        var reason = skip switch
        {
            MaterialFinishSkip.SubAssembly => "a sub-assembly",
            MaterialFinishSkip.Virtual => "a virtual component",
            MaterialFinishSkip.Suppressed => "suppressed",
            MaterialFinishSkip.ReadOnlyFile => "read-only file",
            MaterialFinishSkip.NotModifiable => "cannot be modified",
            MaterialFinishSkip.NotAPart => "not a part",
            _ => throw new ArgumentOutOfRangeException(nameof(skip), skip, "Not a reason to skip."),
        };

        return $"Skipped: {Display(name)} ({reason})";
    }

    /// <summary>
    /// The appearance name to look up for <paramref name="finish"/>, trimmed; null when there is no finish or the
    /// finish keeps the material's appearance (a blank name), so nothing is looked up (task 069 reports a blank name
    /// as Missing).
    /// </summary>
    public static string? AppearanceToFind(Finish? finish)
    {
        var name = (finish?.AppearanceName ?? "").Trim();
        return name.Length == 0 ? null : name;
    }

    /// <summary>
    /// The note for an appearance that was looked up and not used: Missing or Ambiguous (Default, spec 03 questions 9
    /// and 10). Null when it was Found.
    /// </summary>
    public static string? AppearanceNote(string name, AppearanceLookupResult result, int parts)
    {
        ArgumentNullException.ThrowIfNull(result);

        var keeps = parts == 1 ? "the part keeps its material's appearance" : "the parts keep their material's appearance";
        return result.Status switch
        {
            AppearanceLookupStatus.Found => null,
            AppearanceLookupStatus.Ambiguous =>
                $"Appearance '{Display(name)}' names {result.Count} different appearances; {keeps}.",
            _ => $"Appearance '{Display(name)}' was not found; {keeps}.",
        };
    }

    /// <summary>
    /// The index in <paramref name="materials"/> of the first material with this display name, ignoring case and
    /// surrounding spaces (materials are compared by display name, spike 067); -1 when none has it or the name is
    /// blank.
    /// </summary>
    public static int IndexOfMaterial(string? name, IReadOnlyList<LibraryMaterial> materials)
    {
        ArgumentNullException.ThrowIfNull(materials);

        var wanted = (name ?? "").Trim();
        if (wanted.Length == 0)
        {
            return -1;
        }

        for (var i = 0; i < materials.Count; i++)
        {
            if (string.Equals((materials[i].DisplayName ?? "").Trim(), wanted, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>The failure when the chosen material is not in the opened library.</summary>
    public static string MaterialNotInLibraryMessage(string? name) =>
        $"The material '{Display(name)}' is not in the material library. Nothing was changed.";

    /// <summary>For example <c>"Applied to 2 parts. Save to keep the changes."</c> Nothing is saved by the add-in.</summary>
    public static string AppliedMessage(int parts) =>
        parts == 1 ? "Applied to 1 part. Save to keep the changes." : $"Applied to {parts} parts. Save to keep the changes.";

    /// <summary>The failure when applying to one part failed; the transaction was aborted, so nothing changed.</summary>
    public static string FailedMessage(string? partName, string? reason)
    {
        var why = (reason ?? "").Trim();
        if (why.Length == 0)
        {
            return $"Could not apply to {Display(partName)}. Nothing was changed.";
        }

        var end = why[^1] is '.' or '!' or '?' ? "" : ".";
        return $"Could not apply to {Display(partName)}: {why}{end} Nothing was changed.";
    }

    /// <summary>
    /// The one log line per Apply: the targets, the material and the finish, for example
    /// <c>"Material and Finish: apply material 'Steel, Test' and finish 'HC' to 2 parts: A.ipt, B.ipt"</c>.
    /// </summary>
    public static string ApplyLogLine(IEnumerable<string> partNames, string materialName, Finish? finish)
    {
        ArgumentNullException.ThrowIfNull(partNames);

        var names = partNames.Select(Display).ToList();
        var finishText = finish is null ? "no finish" : $"finish '{Display(finish.Code)}'";
        var count = names.Count == 1 ? "1 part" : $"{names.Count} parts";
        return $"{LogPrefix}apply material '{Display(materialName)}' and {finishText} to {count}: {string.Join(", ", names)}";
    }

    /// <summary>The log line for one note.</summary>
    public static string NoteLogLine(string note) => LogPrefix + (note ?? "").Trim();

    private static MaterialFinishTargetPlan<T> Stop<T>(string message) =>
        new(Array.Empty<MaterialFinishCandidate<T>>(), Array.Empty<string>(), message);

    private static void AddOnce(List<string> notes, string note)
    {
        if (!notes.Contains(note, StringComparer.Ordinal))
        {
            notes.Add(note);
        }
    }

    private static string Display(string? text)
    {
        var trimmed = (text ?? "").Trim();
        return trimmed.Length == 0 ? "(unnamed)" : trimmed;
    }
}
