using System.Text.RegularExpressions;

namespace InventorAddin.Core.Drawing;

/// <summary>The notes to place for a drawing: the numbered list, in order, and the bold bottom lines, in order.</summary>
public sealed record NotesPlan(IReadOnlyList<string> ListNotes, IReadOnlyList<string> BottomNotes)
{
    public static NotesPlan Empty { get; } = new([], []);
}

/// <summary>
/// Which notes a material, a finish and the model's Finish Note give (spec 05, "Added for M5", item 1). A rule's
/// pattern matches the whole name with <c>*</c> (any run) and <c>?</c> (one character), ignoring case. Every matching row
/// adds its note in workbook order; a note is placed once, whatever its placement (compared ignoring case and
/// surrounding spaces). The list reads material notes, then the Finish Note, then the finish's notes (Default,
/// spec 05 question 23).
/// </summary>
public static class NotesRules
{
    /// <summary>Whether <paramref name="pattern"/> matches <paramref name="name"/>; a blank name or pattern matches nothing.</summary>
    public static bool Matches(string? pattern, string? name)
    {
        var trimmedName = name?.Trim() ?? "";
        var trimmedPattern = pattern?.Trim() ?? "";
        if (trimmedName.Length == 0 || trimmedPattern.Length == 0)
        {
            return false;
        }

        var regex = "^" + Regex.Escape(trimmedPattern).Replace("\\*", ".*").Replace("\\?", ".") + "$";
        return Regex.IsMatch(trimmedName, regex, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline);
    }

    /// <summary>
    /// The notes for <paramref name="material"/> and <paramref name="finish"/>. A blank <paramref name="finishNote"/>
    /// adds nothing; a rule note equal to the Finish Note is not repeated.
    /// </summary>
    public static NotesPlan Resolve(NotesWorkbook workbook, string? material, string? finish, string? finishNote)
    {
        ArgumentNullException.ThrowIfNull(workbook);

        var list = new List<string>();
        var bottom = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var finishText = finishNote?.Trim() ?? "";
        if (finishText.Length > 0)
        {
            seen.Add(finishText);
        }

        Add(workbook.MaterialRules, material, list, bottom, seen);
        if (finishText.Length > 0)
        {
            list.Add(finishText);
        }

        Add(workbook.FinishRules, finish, list, bottom, seen);
        return new NotesPlan(list.AsReadOnly(), bottom.AsReadOnly());
    }

    private static void Add(
        IReadOnlyList<NoteRule> rules,
        string? name,
        List<string> list,
        List<string> bottom,
        HashSet<string> seen)
    {
        foreach (var rule in rules)
        {
            var note = rule.Note.Trim();
            if (note.Length == 0 || !Matches(rule.Pattern, name) || !seen.Add(note))
            {
                continue;
            }

            (rule.Placement == NotePlacement.Bottom ? bottom : list).Add(note);
        }
    }
}
