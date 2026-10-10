namespace InventorAddin.Core.Numbering;

/// <summary>A pending number that the register already held when it was synced. Both rows are kept.</summary>
/// <param name="Number">The number handed out twice.</param>
/// <param name="Existing">The first row in the register that holds the number.</param>
/// <param name="Added">The pending row appended by the sync.</param>
public sealed record RegisterDuplicate(PartNumber Number, RegisterEntry Existing, RegisterEntry Added);

/// <summary>What <see cref="PartRegister.Merge"/> produced: the merged register and each duplicate it appended.</summary>
public sealed record SyncResult(PartRegister Register, IReadOnlyList<RegisterDuplicate> Duplicates);

/// <summary>
/// The part register as data (spec 02, "The register"): one entry per number handed out, in the order they were
/// handed out. Immutable. Rows are never removed or reordered; the only change to an existing row is a renamed
/// file's new path and name (<see cref="TryUpdate"/>, task 047).
/// </summary>
public sealed class PartRegister
{
    private readonly RegisterEntry[] _entries;
    private readonly Dictionary<PartNumber, RegisterEntry> _firstByNumber;

    /// <summary>An empty register.</summary>
    public static PartRegister Empty { get; } = new([]);

    public PartRegister(IEnumerable<RegisterEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        _entries = entries.ToArray();
        _firstByNumber = new Dictionary<PartNumber, RegisterEntry>();
        foreach (var entry in _entries)
        {
            ArgumentNullException.ThrowIfNull(entry, nameof(entries));
            _firstByNumber.TryAdd(entry.Number, entry);
        }
    }

    /// <summary>Every entry, in register order.</summary>
    public IReadOnlyList<RegisterEntry> Entries => _entries;

    /// <summary>True when some entry holds <paramref name="number"/>.</summary>
    public bool Contains(PartNumber number) => _firstByNumber.ContainsKey(number);

    /// <summary>
    /// The number to hand out next: one more than the highest in the register, or <c>AWB-10001</c> when it is empty.
    /// Throws <see cref="InvalidOperationException"/> when the sequence is full; see <see cref="PartNumber.Next"/>.
    /// </summary>
    public PartNumber NextNumber() => PartNumber.Next(_entries.Select(e => e.Number));

    /// <summary>
    /// Spec 02, "Has a number": true only when <paramref name="partNumberProperty"/> parses as a part number and the
    /// register holds it. Which file the entry names does not matter, because files move.
    /// </summary>
    public bool HasNumber(string? partNumberProperty) =>
        PartNumber.TryParse(partNumberProperty, out var number) && Contains(number);

    /// <summary>A new register with <paramref name="entry"/> after the existing entries.</summary>
    public PartRegister Append(RegisterEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return new PartRegister(_entries.Append(entry));
    }

    /// <summary>
    /// A renamed file (task 047): a new register in which one row holding <paramref name="number"/> has
    /// <paramref name="newPath"/> as its <c>FilePath</c> and <paramref name="newName"/> as its <c>Name</c>; every other
    /// value, and every other row, is kept in place. The row is the first one holding the number whose path is already
    /// <paramref name="newPath"/>, so an update applied again (a queued update after a write that got part way) changes
    /// nothing more; else the first whose path is <paramref name="oldPath"/>, so the right row is changed when a number
    /// was handed out twice; otherwise the first row holding the number (Default). Paths are compared ignoring case.
    /// False, with <paramref name="updated"/> this register, when no row holds the number.
    /// </summary>
    public bool TryUpdate(
        PartNumber number,
        string newPath,
        string newName,
        string? oldPath,
        out PartRegister updated)
    {
        ArgumentNullException.ThrowIfNull(newPath);
        ArgumentNullException.ThrowIfNull(newName);

        updated = this;
        var first = -1;
        var byOldPath = -1;
        var byNewPath = -1;
        for (var i = 0; i < _entries.Length; i++)
        {
            if (_entries[i].Number != number)
            {
                continue;
            }

            if (first < 0)
            {
                first = i;
            }

            if (byNewPath < 0 && string.Equals(_entries[i].FilePath, newPath, StringComparison.OrdinalIgnoreCase))
            {
                byNewPath = i;
            }

            if (byOldPath < 0 && !string.IsNullOrWhiteSpace(oldPath)
                && string.Equals(_entries[i].FilePath, oldPath, StringComparison.OrdinalIgnoreCase))
            {
                byOldPath = i;
            }
        }

        var index = byNewPath >= 0 ? byNewPath : byOldPath >= 0 ? byOldPath : first;
        if (index < 0)
        {
            return false;
        }

        var entries = (RegisterEntry[])_entries.Clone();
        entries[index] = entries[index] with { FilePath = newPath, Name = newName };
        updated = new PartRegister(entries);
        return true;
    }

    /// <summary>
    /// Syncs <paramref name="pending"/> entries into <paramref name="shared"/> (spec 02, "Syncing"). Each pending entry
    /// is appended in order. One whose number the register (or an earlier pending entry) already holds is still
    /// appended and reported as a <see cref="RegisterDuplicate"/>. One the register already holds exactly (same
    /// number, file path and time, as after a sync interrupted once it had written) is not appended again and is not
    /// a duplicate.
    /// </summary>
    public static SyncResult Merge(PartRegister shared, IEnumerable<RegisterEntry> pending)
    {
        ArgumentNullException.ThrowIfNull(shared);
        ArgumentNullException.ThrowIfNull(pending);

        var merged = new List<RegisterEntry>(shared._entries);
        var firstByNumber = new Dictionary<PartNumber, RegisterEntry>(shared._firstByNumber);
        var duplicates = new List<RegisterDuplicate>();

        foreach (var entry in pending)
        {
            ArgumentNullException.ThrowIfNull(entry, nameof(pending));

            if (merged.Any(e => IsSameAssignment(e, entry)))
            {
                continue;
            }

            if (firstByNumber.TryGetValue(entry.Number, out var existing))
            {
                duplicates.Add(new RegisterDuplicate(entry.Number, existing, entry));
            }
            else
            {
                firstByNumber.Add(entry.Number, entry);
            }

            merged.Add(entry);
        }

        return new SyncResult(new PartRegister(merged), duplicates);
    }

    /// <summary>
    /// Two rows record the same assignment when number, file path and time match. The path is compared ignoring case,
    /// as Windows does; the time to the second, as the CSV stores it.
    /// </summary>
    private static bool IsSameAssignment(RegisterEntry a, RegisterEntry b) =>
        a.Number == b.Number
        && string.Equals(a.FilePath, b.FilePath, StringComparison.OrdinalIgnoreCase)
        && ToWholeSeconds(a.AssignedAt) == ToWholeSeconds(b.AssignedAt);

    private static long ToWholeSeconds(DateTimeOffset value) => value.UtcTicks / TimeSpan.TicksPerSecond;
}
