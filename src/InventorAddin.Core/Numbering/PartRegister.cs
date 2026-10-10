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
/// handed out. Immutable and append-only: rows are never removed or reordered.
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
