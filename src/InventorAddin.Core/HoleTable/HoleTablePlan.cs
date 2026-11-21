using InventorAddin.Core.Holes;

namespace InventorAddin.Core.HoleTable;

/// <summary>The two hole tables.</summary>
public enum HoleTableKind
{
    Precision,
    Clearance,
}

/// <summary>
/// The user's changes to a hole table, kept by hole <see cref="HoleTableHole.Id"/> so they survive a rebuild (task 158).
/// Immutable; each <c>With</c> method returns a changed copy.
/// </summary>
public sealed record HoleTableEdits
{
    /// <summary>The text a group's NOTES cell shows when it has no note (spec 04, "Target output").</summary>
    public const string NoNote = "-";

    public static HoleTableEdits None { get; } = new();

    /// <summary>Holes moved to the other table than their classification gives.</summary>
    public IReadOnlySet<string> SwappedIds { get; init; } = new HashSet<string>();

    /// <summary>Holes left out of both tables. A delete beats a split and a swap.</summary>
    public IReadOnlySet<string> DeletedIds { get; init; } = new HashSet<string>();

    /// <summary>Holes with their own group and letter (spec 04 question 2).</summary>
    public IReadOnlySet<string> SplitIds { get; init; } = new HashSet<string>();

    /// <summary>Notes of ordinary groups, by table and the group's description <see cref="HoleTableDescription.Key"/> (a description with no length has the same Key in both tables).</summary>
    public IReadOnlyDictionary<(HoleTableKind Table, string Key), string> NotesByDescription { get; init; } = new Dictionary<(HoleTableKind, string), string>();

    /// <summary>Notes of split groups, by the split hole's <see cref="HoleTableHole.Id"/>.</summary>
    public IReadOnlyDictionary<string, string> NotesBySplitId { get; init; } = new Dictionary<string, string>();

    public HoleTableEdits WithSwapped(string id, bool on = true) => this with { SwappedIds = Toggle(SwappedIds, id, on) };

    public HoleTableEdits WithDeleted(string id, bool on = true) => this with { DeletedIds = Toggle(DeletedIds, id, on) };

    public HoleTableEdits WithSplit(string id, bool on = true) => this with { SplitIds = Toggle(SplitIds, id, on) };

    /// <summary>Sets a group's note; blank text clears it.</summary>
    public HoleTableEdits WithNote(HoleTableKind table, string descriptionKey, string? note) =>
        this with { NotesByDescription = SetNote(NotesByDescription, (table, descriptionKey), note) };

    /// <summary>Sets a split group's note; blank text clears it.</summary>
    public HoleTableEdits WithSplitNote(string holeId, string? note) =>
        this with { NotesBySplitId = SetNote(NotesBySplitId, holeId, note) };

    private static IReadOnlySet<string> Toggle(IReadOnlySet<string> set, string id, bool on)
    {
        var copy = new HashSet<string>(set, StringComparer.Ordinal);
        if (on)
        {
            copy.Add(id);
        }
        else
        {
            copy.Remove(id);
        }

        return copy;
    }

    private static IReadOnlyDictionary<TKey, string> SetNote<TKey>(IReadOnlyDictionary<TKey, string> notes, TKey key, string? note)
        where TKey : notnull
    {
        var copy = new Dictionary<TKey, string>(notes);
        if (string.IsNullOrWhiteSpace(note))
        {
            copy.Remove(key);
        }
        else
        {
            copy[key] = note;
        }

        return copy;
    }
}

/// <summary>One row of a group: a hole instance, its tag, its 1-based number in the group and its coordinate text.</summary>
public sealed record HoleTableRow(HoleTableHole Hole, string Tag, int Number, string XText, string YText);

/// <summary>One group of identical holes in one table.</summary>
/// <param name="Letter">A to Z, then AA, AB; precision groups first, clearance groups continue the sequence.</param>
/// <param name="SplitHoleId">The hole this group was split off for; null for an ordinary group.</param>
/// <param name="Note">The user's note, or null.</param>
public sealed record HoleTableGroup(
    string Letter,
    HoleTableKind Table,
    HoleTableDescription Description,
    IReadOnlyList<HoleTableRow> Rows,
    string? SplitHoleId,
    string? Note)
{
    /// <summary>What the NOTES cell shows: the note, or <c>-</c>.</summary>
    public string NotesText => string.IsNullOrWhiteSpace(Note) ? HoleTableEdits.NoNote : Note;
}

/// <summary>The two tables' groups, and the edited <c>Id</c>s that matched no hole.</summary>
public sealed partial record HoleTablePlan(
    IReadOnlyList<HoleTableGroup> Precision,
    IReadOnlyList<HoleTableGroup> Clearance,
    IReadOnlyList<string> StaleIds)
{
    /// <summary>Both tables' groups, precision first, in letter order.</summary>
    public IEnumerable<HoleTableGroup> AllGroups => Precision.Concat(Clearance);

    /// <summary>
    /// Builds the plan: a pure function of its inputs, so Renumber is this run again on the current holes and edits.
    /// The order never depends on quantity, so an added or removed instance changes only its own group's numbers.
    /// The result does not depend on the order of <paramref name="holes"/>.
    /// </summary>
    /// <param name="holes">The view's holes.</param>
    /// <param name="edits">The user's changes; null for none.</param>
    /// <param name="origin">The view's ordinate origin on the sheet (task 157).</param>
    /// <param name="viewScale">Model-to-sheet factor.</param>
    /// <param name="drawingUnits">The units the tables are shown in.</param>
    public static HoleTablePlan Build(
        IEnumerable<HoleTableHole> holes,
        HoleTableEdits? edits,
        SheetPoint origin,
        double viewScale,
        HoleUnits drawingUnits)
    {
        ArgumentNullException.ThrowIfNull(holes);
        edits ??= HoleTableEdits.None;

        // A fixed starting order, so ties never depend on the caller's order.
        var all = holes.OrderBy(h => h.Id, StringComparer.Ordinal).ToList();
        var ids = all.Select(h => h.Id).ToHashSet(StringComparer.Ordinal);
        var stale = edits.SwappedIds.Concat(edits.DeletedIds).Concat(edits.SplitIds)
            .Where(id => !ids.Contains(id))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();

        // Slots: one per (table, description key). A slot holds the ordinary group and the split holes from it.
        var slots = new Dictionary<(HoleTableKind, string), Slot>();
        foreach (var hole in all)
        {
            if (edits.DeletedIds.Contains(hole.Id))
            {
                continue;
            }

            var precision = hole.IsPrecision != edits.SwappedIds.Contains(hole.Id);
            var table = precision ? HoleTableKind.Precision : HoleTableKind.Clearance;
            var description = HoleTableDescriptions.For(hole, HoleTableDecimals.For(precision, hole.Units));
            var key = description.Key;
            if (!slots.TryGetValue((table, key), out var slot))
            {
                slot = new Slot(table, description);
                slots[(table, key)] = slot;
            }

            slot.Holes.Add(hole);
            if (edits.SplitIds.Contains(hole.Id))
            {
                slot.Split.Add(hole);
            }
            else
            {
                slot.Plain.Add(hole);
            }
        }

        var letters = 0;
        var precisionGroups = new List<HoleTableGroup>();
        var clearanceGroups = new List<HoleTableGroup>();
        foreach (var table in new[] { HoleTableKind.Precision, HoleTableKind.Clearance })
        {
            var target = table == HoleTableKind.Precision ? precisionGroups : clearanceGroups;
            var ordered = slots.Where(s => s.Key.Item1 == table)
                .Select(s => s.Value)
                .OrderBy(s => new HoleTableGroupKey(
                        s.Holes.Min(h => h.Type),
                        s.Holes.Select(h => h.DrillDiameterCm).Aggregate((double?)null, MinDiameter),
                        s.Description.Key),
                    HoleTableOrder.Groups(table == HoleTableKind.Precision));

            foreach (var slot in ordered)
            {
                if (slot.Plain.Count > 0)
                {
                    var note = edits.NotesByDescription.GetValueOrDefault((slot.Table, slot.Description.Key));
                    target.Add(MakeGroup(letters++, slot, slot.Plain, null, note));
                }

                foreach (var hole in HoleTableOrder.SortInstances(slot.Split))
                {
                    var note = edits.NotesBySplitId.GetValueOrDefault(hole.Id);
                    target.Add(MakeGroup(letters++, slot, new List<HoleTableHole> { hole }, hole.Id, note));
                }
            }
        }

        return new HoleTablePlan(precisionGroups, clearanceGroups, stale);

        HoleTableGroup MakeGroup(int index, Slot slot, List<HoleTableHole> members, string? splitId, string? note)
        {
            var letter = Letter(index);
            var precision = slot.Table == HoleTableKind.Precision;
            var rows = HoleTableOrder.SortInstances(members).Select((h, i) =>
            {
                var c = HoleTableCoordinates.For(h, origin, viewScale, drawingUnits, precision);
                return new HoleTableRow(h, letter + (i + 1), i + 1, c.XText, c.YText);
            }).ToList();
            return new HoleTableGroup(letter, slot.Table, slot.Description, rows, splitId, note);
        }
    }

    /// <summary>The letter of a group by its 0-based position: A to Z, then AA, AB and on.</summary>
    public static string Letter(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        var text = "";
        var n = index + 1;
        while (n > 0)
        {
            n--;
            text = (char)('A' + n % 26) + text;
            n /= 26;
        }

        return text;
    }

    private static double? MinDiameter(double? a, double? b) =>
        a is null ? b : b is null ? a : Math.Min(a.Value, b.Value);

    private sealed class Slot(HoleTableKind table, HoleTableDescription description)
    {
        public HoleTableKind Table { get; } = table;

        public HoleTableDescription Description { get; } = description;

        public List<HoleTableHole> Holes { get; } = [];

        public List<HoleTableHole> Plain { get; } = [];

        public List<HoleTableHole> Split { get; } = [];
    }
}
