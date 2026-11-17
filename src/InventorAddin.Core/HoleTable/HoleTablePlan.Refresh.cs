using InventorAddin.Core.Holes;

namespace InventorAddin.Core.HoleTable;

/// <summary>A group whose description changed on Update while it kept its letter.</summary>
/// <param name="OldKey">The description key stored with the letter.</param>
/// <param name="NewText">The group's description now, as plain text.</param>
public sealed record HoleTableDescriptionChange(string Letter, string OldKey, string NewText);

/// <summary>What Update did, for the log and the status line.</summary>
/// <param name="Plan">The refreshed plan.</param>
/// <param name="Edits">The edits with every <c>Id</c> that matched no hole dropped.</param>
/// <param name="AddedIds">Holes with no stored tag, now tagged.</param>
/// <param name="GoneIds">Holes that had a stored tag and are no longer in the view.</param>
/// <param name="DescriptionChanges">Groups that kept their letter with a new description.</param>
public sealed record HoleTableRefresh(
    HoleTablePlan Plan,
    HoleTableEdits Edits,
    IReadOnlyList<string> AddedIds,
    IReadOnlyList<string> GoneIds,
    IReadOnlyList<HoleTableDescriptionChange> DescriptionChanges)
{
    /// <summary>The counts for the status line, for example <c>2 holes added, 1 hole gone, 1 description changed.</c></summary>
    public string Summary => HoleTableMessages.RefreshSummary(this);
}

public sealed partial record HoleTablePlan
{
    /// <summary>
    /// The highest letter and numbers ever used before this plan (carried by <see cref="Refresh"/>), so a retired tag is not given out again.
    /// Null for a plan from <see cref="Build"/>, which starts afresh.
    /// </summary>
    public HoleTableStoredTags? HighWater { get; init; }

    /// <summary>The tags and what each letter stands for, as stored.</summary>
    public HoleTableStoredTags ToStoredTags()
    {
        var byHole = new Dictionary<string, HoleTableTag>(StringComparer.Ordinal);
        var groups = new List<HoleTableStoredGroup>();
        foreach (var group in AllGroups)
        {
            groups.Add(new HoleTableStoredGroup(group.Letter, group.Table, group.Description.Key, group.SplitHoleId));
            foreach (var row in group.Rows)
            {
                byHole[row.Hole.Id] = new HoleTableTag(group.Letter, row.Number);
            }
        }

        var tags = new HoleTableStoredTags(byHole, groups);
        var high = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var letter in tags.Letters.Concat(HighWater?.Letters ?? Enumerable.Empty<string>()).Distinct(StringComparer.Ordinal))
        {
            high[letter] = Math.Max(tags.HighestNumber(letter), HighWater?.HighestNumber(letter) ?? 0);
        }

        return tags with { HighLetterIndex = Math.Max(tags.NextLetterIndex, HighWater?.NextLetterIndex ?? 0), HighNumbers = high };
    }

    /// <summary>The 0-based position of a letter (<c>A</c> is 0, <c>AA</c> is 26); the inverse of <see cref="Letter"/>. -1 for text that is not a letter.</summary>
    public static int LetterIndex(string? letter)
    {
        if (string.IsNullOrEmpty(letter) || letter.Any(c => c is < 'A' or > 'Z'))
        {
            return -1;
        }

        var n = 0;
        foreach (var c in letter)
        {
            n = n * 26 + (c - 'A' + 1);
        }

        return n - 1;
    }

    /// <summary>
    /// Update (spec 04 question 18): rebuilds the plan from the current holes and keeps every remaining hole's tag.
    /// A new hole in an existing group takes the next number after the highest ever used in it; a new group takes the next letter
    /// after the highest ever used (the high-water marks in the stored tags). Holes that are gone leave gaps. Only <see cref="Build"/> closes gaps and reorders.
    /// A hole keeps its tag while it stays in the group of its letter: same table and description, or still the split hole
    /// of a split group. A hole that no longer fits (it was swapped, split or unsplit, or its description changed while
    /// its group-mates' did not) is placed like a new hole. A whole group whose description changed keeps its letter.
    /// </summary>
    /// <param name="previous">The stored tags; null for none, which gives the same plan as <see cref="Build"/>.</param>
    public static HoleTableRefresh Refresh(
        IEnumerable<HoleTableHole> holes,
        HoleTableEdits? edits,
        HoleTableStoredTags? previous,
        SheetPoint origin,
        double viewScale,
        HoleUnits drawingUnits)
    {
        ArgumentNullException.ThrowIfNull(holes);
        edits ??= HoleTableEdits.None;
        previous ??= HoleTableStoredTags.Empty;

        var all = holes.OrderBy(h => h.Id, StringComparer.Ordinal).ToList();
        var ids = all.Select(h => h.Id).ToHashSet(StringComparer.Ordinal);
        var cleaned = edits with
        {
            SwappedIds = edits.SwappedIds.Where(ids.Contains).ToHashSet(StringComparer.Ordinal),
            DeletedIds = edits.DeletedIds.Where(ids.Contains).ToHashSet(StringComparer.Ordinal),
            SplitIds = edits.SplitIds.Where(ids.Contains).ToHashSet(StringComparer.Ordinal),
            NotesBySplitId = edits.NotesBySplitId.Where(p => ids.Contains(p.Key)).ToDictionary(p => p.Key, p => p.Value),
        };
        var stale = edits.SwappedIds.Concat(edits.DeletedIds).Concat(edits.SplitIds)
            .Where(id => !ids.Contains(id))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();

        var infos = new List<HoleInfo>();
        foreach (var hole in all)
        {
            if (edits.DeletedIds.Contains(hole.Id))
            {
                continue;
            }

            var precision = hole.IsPrecision != edits.SwappedIds.Contains(hole.Id);
            var table = precision ? HoleTableKind.Precision : HoleTableKind.Clearance;
            var description = HoleTableDescriptions.For(hole, HoleTableDecimals.For(precision, hole.Units));
            infos.Add(new HoleInfo(hole, table, description, edits.SplitIds.Contains(hole.Id)));
        }

        var gone = previous.ByHole.Keys.Where(id => !ids.Contains(id)).OrderBy(id => id, StringComparer.Ordinal).ToList();

        // Slot sort keys, as Build computes them: over every hole of the (table, description key) slot.
        var slotKeys = infos
            .GroupBy(i => (i.Table, i.Description.Key))
            .ToDictionary(
                g => g.Key,
                g => new HoleTableGroupKey(
                    g.Min(i => i.Hole.Type),
                    g.Select(i => i.Hole.DrillDiameterCm).Aggregate((double?)null, MinDiameter),
                    g.Key.Key));

        var stored = previous.Groups.GroupBy(g => g.Letter).ToDictionary(g => g.Key, g => g.First());
        var membersByLetter = infos
            .Where(i => previous.ByHole.ContainsKey(i.Hole.Id))
            .GroupBy(i => previous.ByHole[i.Hole.Id].Letter)
            .OrderBy(g => LetterIndex(g.Key))
            .ThenBy(g => g.Key, StringComparer.Ordinal)
            .ToList();

        var groups = new List<Working>();
        var plainClaims = new Dictionary<(HoleTableKind, string), Working>();
        var pending = new List<HoleInfo>();
        var changes = new List<HoleTableDescriptionChange>();
        var claimed = new HashSet<string>(StringComparer.Ordinal);

        // Pass 1: letters whose stored description still matches keep their group.
        foreach (var letterGroup in membersByLetter)
        {
            var letter = letterGroup.Key;
            if (!stored.TryGetValue(letter, out var was))
            {
                continue;
            }

            if (was.SplitHoleId is { } splitId)
            {
                var holder = letterGroup.FirstOrDefault(i => i.Hole.Id == splitId && i.Split);
                if (holder is null)
                {
                    continue;
                }

                var group = new Working(letter, holder.Table, holder.Description, splitId);
                group.Members.Add((holder, previous.ByHole[splitId].Number));
                groups.Add(group);
                claimed.Add(letter);
                if (holder.Table != was.Table || holder.Description.Key != was.DescriptionKey)
                {
                    changes.Add(new HoleTableDescriptionChange(letter, was.DescriptionKey, holder.Description.PlainText));
                }
            }
            else
            {
                var matching = letterGroup.Where(i => !i.Split && i.Table == was.Table && i.Description.Key == was.DescriptionKey).ToList();
                if (matching.Count == 0 || plainClaims.ContainsKey((was.Table, was.DescriptionKey)))
                {
                    continue;
                }

                var group = new Working(letter, was.Table, matching[0].Description, null);
                foreach (var info in matching)
                {
                    group.Members.Add((info, previous.ByHole[info.Hole.Id].Number));
                }

                groups.Add(group);
                plainClaims[(was.Table, was.DescriptionKey)] = group;
                claimed.Add(letter);
            }
        }

        // Pass 2: letters whose whole group changed description keep their letter on the most common new description.
        foreach (var letterGroup in membersByLetter.Where(g => !claimed.Contains(g.Key)))
        {
            var letter = letterGroup.Key;
            var plain = letterGroup.Where(i => !i.Split).ToList();
            var best = plain
                .GroupBy(i => (i.Table, i.Description.Key))
                .OrderByDescending(g => g.Count())
                .ThenBy(g => g.Key.Table)
                .ThenBy(g => g.Key.Key, StringComparer.Ordinal)
                .FirstOrDefault();
            if (best is not null && !plainClaims.ContainsKey(best.Key)
                && stored.TryGetValue(letter, out var was) && was.SplitHoleId is null)
            {
                var group = new Working(letter, best.Key.Table, best.First().Description, null);
                foreach (var info in best)
                {
                    group.Members.Add((info, previous.ByHole[info.Hole.Id].Number));
                }

                groups.Add(group);
                plainClaims[best.Key] = group;
                claimed.Add(letter);
                changes.Add(new HoleTableDescriptionChange(letter, was.DescriptionKey, group.Description.PlainText));
                pending.AddRange(plain.Where(i => !best.Contains(i)));
                pending.AddRange(letterGroup.Where(i => i.Split));
            }
        }

        var placed = groups.SelectMany(g => g.Members).Select(m => m.Hole.Hole.Id).ToHashSet(StringComparer.Ordinal);
        pending.AddRange(infos.Where(i => !placed.Contains(i.Hole.Id) && !pending.Contains(i)));
        var added = pending.Where(i => !previous.ByHole.ContainsKey(i.Hole.Id)).Select(i => i.Hole.Id).ToList();

        // Place the holes that kept no tag, in instance order within their group (as Build sorts them).
        var orderIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        var fresh = new List<Working>();
        foreach (var slot in pending.GroupBy(p => (p.Table, p.Description.Key)))
        {
            var plain = slot.Where(i => !i.Split).Select(i => i.Hole).ToList();
            var infoById = slot.ToDictionary(i => i.Hole.Id, StringComparer.Ordinal);
            if (plain.Count > 0)
            {
                if (!plainClaims.TryGetValue(slot.Key, out var group))
                {
                    var first = infoById[plain[0].Id];
                    group = new Working(null, first.Table, first.Description, null);
                    plainClaims[slot.Key] = group;
                    fresh.Add(group);
                }

                var number = Math.Max(
                    group.Members.Count == 0 ? 0 : group.Members.Max(m => m.Number),
                    group.Letter is null ? 0 : previous.HighestNumber(group.Letter));
                foreach (var hole in HoleTableOrder.SortInstances(plain))
                {
                    group.Members.Add((infoById[hole.Id], ++number));
                }
            }

            var index = 0;
            foreach (var hole in HoleTableOrder.SortInstances(slot.Where(i => i.Split).Select(i => i.Hole)))
            {
                var info = infoById[hole.Id];
                var splitGroup = new Working(null, info.Table, info.Description, hole.Id);
                splitGroup.Members.Add((info, 1));
                fresh.Add(splitGroup);
                orderIndex[hole.Id] = index++;
            }
        }

        // New groups take the next letters after the highest in use, in Build's order.
        fresh.Sort((a, b) =>
        {
            var byTable = a.Table.CompareTo(b.Table);
            if (byTable != 0)
            {
                return byTable;
            }

            var byKey = HoleTableOrder.Groups(a.Table == HoleTableKind.Precision)
                .Compare(slotKeys[(a.Table, a.Description.Key)], slotKeys[(b.Table, b.Description.Key)]);
            if (byKey != 0)
            {
                return byKey;
            }

            var bySlot = string.CompareOrdinal(a.Description.Key, b.Description.Key);
            if (bySlot != 0)
            {
                return bySlot;
            }

            var aSplit = a.SplitHoleId is null ? 0 : 1;
            var bSplit = b.SplitHoleId is null ? 0 : 1;
            if (aSplit != bSplit)
            {
                return aSplit.CompareTo(bSplit);
            }

            return aSplit == 0 ? 0 : orderIndex[a.SplitHoleId!].CompareTo(orderIndex[b.SplitHoleId!]);
        });
        var next = Math.Max(previous.NextLetterIndex, groups.Count == 0 ? 0 : groups.Max(g => LetterIndex(g.Letter)) + 1);
        foreach (var group in fresh)
        {
            group.Letter = Letter(next++);
            groups.Add(group);
        }

        var precisionGroups = new List<HoleTableGroup>();
        var clearanceGroups = new List<HoleTableGroup>();
        foreach (var group in groups.OrderBy(g => LetterIndex(g.Letter)))
        {
            var isPrecision = group.Table == HoleTableKind.Precision;
            var rows = group.Members.OrderBy(m => m.Number).Select(m =>
            {
                var c = HoleTableCoordinates.For(m.Hole.Hole, origin, viewScale, drawingUnits, isPrecision);
                return new HoleTableRow(m.Hole.Hole, group.Letter + m.Number, m.Number, c.XText, c.YText);
            }).ToList();
            var note = group.SplitHoleId is { } sid
                ? cleaned.NotesBySplitId.GetValueOrDefault(sid)
                : cleaned.NotesByDescription.GetValueOrDefault((group.Table, group.Description.Key));
            (isPrecision ? precisionGroups : clearanceGroups)
                .Add(new HoleTableGroup(group.Letter, group.Table, group.Description, rows, group.SplitHoleId, note));
        }

        var plan = new HoleTablePlan(precisionGroups, clearanceGroups, stale) { HighWater = previous };
        return new HoleTableRefresh(plan, cleaned, added, gone, changes);
    }

    private sealed record HoleInfo(HoleTableHole Hole, HoleTableKind Table, HoleTableDescription Description, bool Split);

    private sealed class Working(string? letter, HoleTableKind table, HoleTableDescription description, string? splitHoleId)
    {
        public string Letter { get; set; } = letter!;

        public HoleTableKind Table { get; } = table;

        public HoleTableDescription Description { get; } = description;

        public string? SplitHoleId { get; } = splitHoleId;

        public List<(HoleInfo Hole, int Number)> Members { get; } = [];
    }
}
