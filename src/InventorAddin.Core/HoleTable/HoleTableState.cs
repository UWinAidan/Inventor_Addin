using System.Text.Json;
using System.Text.Json.Serialization;

namespace InventorAddin.Core.HoleTable;

/// <summary>A hole's tag: its group letter and its number in that group (<c>B7</c>).</summary>
public sealed record HoleTableTag(string Letter, int Number)
{
    public string Text => Letter + Number;
}

/// <summary>
/// What a letter stood for when the tables were written: the table, the group's description key, and the split hole
/// for a split group. Update uses it to tell a moved hole from one whose description changed.
/// </summary>
public sealed record HoleTableStoredGroup(string Letter, HoleTableKind Table, string DescriptionKey, string? SplitHoleId);

/// <summary>The tags in force: each hole <c>Id</c> mapped to its letter and number, and what each letter stands for.</summary>
public sealed record HoleTableStoredTags(
    IReadOnlyDictionary<string, HoleTableTag> ByHole,
    IReadOnlyList<HoleTableStoredGroup> Groups,
    int? HighLetterIndex = null,
    IReadOnlyDictionary<string, int>? HighNumbers = null)
{
    public static HoleTableStoredTags Empty { get; } = new(new Dictionary<string, HoleTableTag>(), []);

    /// <summary>
    /// The 0-based index of the next letter a new group takes: after the highest letter ever used, not only the letters in
    /// use now. Stored values missing from older text fall back to the highest letter among the stored tags and groups.
    /// </summary>
    public int NextLetterIndex =>
        Math.Max(
            HighLetterIndex ?? 0,
            Math.Max(
                ByHole.Values.Select(t => HoleTablePlan.LetterIndex(t.Letter) + 1).DefaultIfEmpty(0).Max(),
                Groups.Select(g => HoleTablePlan.LetterIndex(g.Letter) + 1).DefaultIfEmpty(0).Max()));

    /// <summary>The highest number ever used in a letter, so a retired tag is never given out again; 0 for a letter never used.</summary>
    public int HighestNumber(string letter) =>
        Math.Max(
            HighNumbers is not null && HighNumbers.TryGetValue(letter, out var n) ? n : 0,
            ByHole.Values.Where(t => t.Letter == letter).Select(t => t.Number).DefaultIfEmpty(0).Max());

    /// <summary>Every letter with a number on record.</summary>
    public IEnumerable<string> Letters =>
        (HighNumbers?.Keys ?? Enumerable.Empty<string>()).Concat(ByHole.Values.Select(t => t.Letter)).Distinct(StringComparer.Ordinal);

    public bool Equals(HoleTableStoredTags? other) =>
        other is not null
        && ByHole.Count == other.ByHole.Count
        && ByHole.All(p => other.ByHole.TryGetValue(p.Key, out var t) && t == p.Value)
        && Groups.SequenceEqual(other.Groups)
        && NextLetterIndex == other.NextLetterIndex
        && Letters.Concat(other.Letters).Distinct(StringComparer.Ordinal).All(l => HighestNumber(l) == other.HighestNumber(l));

    public override int GetHashCode() => HashCode.Combine(ByHole.Count, Groups.Count, NextLetterIndex);
}
/// <summary>
/// Everything the wizard must remember about one view's tables, saved by the add-in as a string (spike 160, C10).
/// </summary>
public sealed record HoleTableState(
    int Version,
    string SheetName,
    string ViewName,
    HoleTableEdits Edits,
    HoleTableStoredTags Tags)
{
    public const int CurrentVersion = 1;

    /// <summary>The state to store for a plan.</summary>
    public static HoleTableState From(string sheetName, string viewName, HoleTableEdits edits, HoleTablePlan plan) =>
        new(CurrentVersion, sheetName, viewName, edits, plan.ToStoredTags());

    public bool Equals(HoleTableState? other) =>
        other is not null
        && Version == other.Version
        && SheetName == other.SheetName
        && ViewName == other.ViewName
        && Tags.Equals(other.Tags)
        && Edits.SwappedIds.SetEquals(other.Edits.SwappedIds)
        && Edits.DeletedIds.SetEquals(other.Edits.DeletedIds)
        && Edits.SplitIds.SetEquals(other.Edits.SplitIds)
        && Same(Edits.NotesByDescription, other.Edits.NotesByDescription)
        && Same(Edits.NotesBySplitId, other.Edits.NotesBySplitId);

    public override int GetHashCode() => HashCode.Combine(Version, SheetName, ViewName, Tags.ByHole.Count);

    private static bool Same<TKey>(IReadOnlyDictionary<TKey, string> a, IReadOnlyDictionary<TKey, string> b)
        where TKey : notnull =>
        a.Count == b.Count && a.All(p => b.TryGetValue(p.Key, out var v) && v == p.Value);
}

/// <summary>The result of reading a stored state: the state, or null and the reason.</summary>
public sealed record HoleTableStateReadResult(HoleTableState? State, string? Reason);

/// <summary>Writes and reads <see cref="HoleTableState"/> as JSON. Reading never throws.</summary>
public static class HoleTableStateJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// A stored text longer than this is logged as a warning: Inventor keeps an attribute string in about 32 KB, so the
    /// state would soon stop fitting (task 166).
    /// </summary>
    public const int WarnLength = 28000;

    public static string Write(HoleTableState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var dto = new Dto
        {
            Version = state.Version,
            Sheet = state.SheetName,
            View = state.ViewName,
            Swapped = Sorted(state.Edits.SwappedIds),
            Deleted = Sorted(state.Edits.DeletedIds),
            Split = Sorted(state.Edits.SplitIds),
            Notes = state.Edits.NotesByDescription
                .OrderBy(p => p.Key.Table).ThenBy(p => p.Key.Key, StringComparer.Ordinal)
                .Select(p => new NoteDto { Table = p.Key.Table.ToString(), Key = p.Key.Key, Note = p.Value })
                .ToList(),
            SplitNotes = state.Edits.NotesBySplitId
                .OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new NoteDto { Key = p.Key, Note = p.Value })
                .ToList(),
            NextLetter = state.Tags.NextLetterIndex,
            HighNumbers = state.Tags.Letters.OrderBy(l => l, StringComparer.Ordinal).ToDictionary(l => l, l => state.Tags.HighestNumber(l), StringComparer.Ordinal),
            Groups = state.Tags.Groups.Select(g => new GroupDto
            {
                Letter = g.Letter,
                Table = g.Table.ToString(),
                Key = g.DescriptionKey,
                SplitId = g.SplitHoleId,
                Holes = state.Tags.ByHole
                    .Where(p => p.Value.Letter == g.Letter)
                    .OrderBy(p => p.Value.Number)
                    .ToDictionary(p => p.Key, p => p.Value.Number, StringComparer.Ordinal),
            }).ToList(),
        };
        return JsonSerializer.Serialize(dto, Options);
    }

    public static HoleTableStateReadResult Read(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new(null, "The stored hole table data is empty.");
        }

        try
        {
            var dto = JsonSerializer.Deserialize<Dto>(text, Options);
            if (dto is null)
            {
                return new(null, "The stored hole table data is not a hole table record.");
            }

            if (dto.Version != HoleTableState.CurrentVersion)
            {
                return new(null, $"The stored hole table data is version {dto.Version}; this add-in reads version {HoleTableState.CurrentVersion}.");
            }

            var edits = new HoleTableEdits
            {
                SwappedIds = new HashSet<string>(Ids(dto.Swapped), StringComparer.Ordinal),
                DeletedIds = new HashSet<string>(Ids(dto.Deleted), StringComparer.Ordinal),
                SplitIds = new HashSet<string>(Ids(dto.Split), StringComparer.Ordinal),
                NotesByDescription = Items(dto.Notes).ToDictionary(
                    n => (ParseTable(n.Table), n.Key ?? throw new FormatException("A note has no key.")),
                    n => n.Note ?? ""),
                NotesBySplitId = Items(dto.SplitNotes).ToDictionary(
                    n => n.Key ?? throw new FormatException("A note has no key."),
                    n => n.Note ?? "",
                    StringComparer.Ordinal),
            };

            var byHole = new Dictionary<string, HoleTableTag>(StringComparer.Ordinal);
            var groups = new List<HoleTableStoredGroup>();
            foreach (var g in Items(dto.Groups))
            {
                var letter = g.Letter ?? throw new FormatException("A group has no letter.");
                if (HoleTablePlan.LetterIndex(letter) < 0)
                {
                    throw new FormatException($"'{letter}' is not a group letter.");
                }

                groups.Add(new HoleTableStoredGroup(letter, ParseTable(g.Table), g.Key ?? "", g.SplitId));
                foreach (var (id, number) in g.Holes ?? [])
                {
                    if (number < 1)
                    {
                        throw new FormatException("A tag number is below 1.");
                    }

                    byHole[id] = new HoleTableTag(letter, number);
                }
            }

            var high = dto.HighNumbers?.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
            var tags = new HoleTableStoredTags(byHole, groups, dto.NextLetter, high);
            return new(new HoleTableState(dto.Version, dto.Sheet ?? "", dto.View ?? "", edits, tags), null);
        }
        catch (Exception ex)
        {
            return new(null, "The stored hole table data could not be read: " + ex.Message);
        }
    }

    private static IEnumerable<string> Ids(List<string>? ids) =>
        (ids ?? []).Select(i => i ?? throw new FormatException("An id is empty."));

    private static IEnumerable<T> Items<T>(List<T>? items) where T : class =>
        (items ?? []).Select(i => i ?? throw new FormatException("A list entry is empty."));

    private static HoleTableKind ParseTable(string? text) =>
        Enum.TryParse<HoleTableKind>(text, out var kind) && Enum.IsDefined(kind)
            ? kind
            : throw new FormatException($"'{text}' is not a table.");

    private static List<string> Sorted(IEnumerable<string> ids) => ids.OrderBy(i => i, StringComparer.Ordinal).ToList();

    private sealed class Dto
    {
        [JsonPropertyName("v")] public int Version { get; set; }

        [JsonPropertyName("sheet")] public string? Sheet { get; set; }

        [JsonPropertyName("view")] public string? View { get; set; }

        [JsonPropertyName("swap")] public List<string>? Swapped { get; set; }

        [JsonPropertyName("del")] public List<string>? Deleted { get; set; }

        [JsonPropertyName("split")] public List<string>? Split { get; set; }

        [JsonPropertyName("notes")] public List<NoteDto>? Notes { get; set; }

        [JsonPropertyName("splitNotes")] public List<NoteDto>? SplitNotes { get; set; }

        [JsonPropertyName("groups")] public List<GroupDto>? Groups { get; set; }

        [JsonPropertyName("nl")] public int? NextLetter { get; set; }

        [JsonPropertyName("hn")] public Dictionary<string, int>? HighNumbers { get; set; }
    }

    private sealed class NoteDto
    {
        [JsonPropertyName("t")] public string? Table { get; set; }

        [JsonPropertyName("k")] public string? Key { get; set; }

        [JsonPropertyName("n")] public string? Note { get; set; }
    }

    private sealed class GroupDto
    {
        [JsonPropertyName("l")] public string? Letter { get; set; }

        [JsonPropertyName("t")] public string? Table { get; set; }

        [JsonPropertyName("k")] public string? Key { get; set; }

        [JsonPropertyName("s")] public string? SplitId { get; set; }

        [JsonPropertyName("h")] public Dictionary<string, int>? Holes { get; set; }
    }
}
