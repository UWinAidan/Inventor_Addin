# 035: Register model and CSV format (Core)

- **Milestone:** M2
- **Feature spec:** `docs/features/02-part-creation-and-numbering.md` (The register: Format; Syncing; Part number: Has a number)
- **Status:** todo
- **Depends on:** 033
- **Needs:** cloud
- **Parallel-safe with:** 034, 037, 038, 042

## Goal

Core has the register as data: one entry per number handed out, read from and written to CSV, with the rules for the next number, "has a number", and merging offline entries into the shared register with duplicates reported. No file access here; task 036 puts it on disk.

## Scope

- `src/InventorAddin.Core/Numbering/RegisterEntry.cs` (new)
- `src/InventorAddin.Core/Numbering/RegisterCsv.cs` (new)
- `src/InventorAddin.Core/Numbering/PartRegister.cs` (new)
- `tests/InventorAddin.Core.Tests/Numbering/RegisterCsvTests.cs` (new)
- `tests/InventorAddin.Core.Tests/Numbering/PartRegisterTests.cs` (new)

Out of scope:

- Files, folders, locks and the local copy (036).

## Acceptance criteria

Entry

- [ ] `sealed record RegisterEntry(PartNumber Number, string Name, string PartType, DocumentKind Kind, string FilePath, DateTimeOffset AssignedAt, string AssignedBy, string Computer)`. Text fields are never null (null becomes `""`)

CSV

- [ ] `RegisterCsv.Header` is `Number,Name,PartType,Kind,FilePath,AssignedAt,AssignedBy,Computer`
- [ ] `RegisterCsv.Write(IEnumerable<RegisterEntry>)` returns the CSV text: the header, then one line per entry in the order given, lines ending `\r\n` (Excel on Windows). A value containing a comma, a double quote, `\r` or `\n` is wrapped in double quotes with each quote doubled. `AssignedAt` is UTC ISO 8601 with seconds and `Z` (`2026-10-10T14:03:00Z`). `Kind` is the `DocumentKind` member name
- [ ] `RegisterCsv.Read(string text)` returns a `RegisterReadResult` with the entries in file order and a list of warnings. It accepts `\n` or `\r\n`, a UTF-8 byte order mark, quoted fields with doubled quotes and line breaks inside quotes, and blank lines (skipped). Columns are found by header name, ignoring case and order, so a column Aidan adds in Excel is ignored, not an error
- [ ] A row that cannot be read (bad number, bad date, unknown kind, wrong field count) is skipped with a warning naming its line number. A missing required column (`Number`) makes the whole text unreadable: `Read` throws `FormatException`
- [ ] Round trip: `Read(Write(entries))` gives back equal entries, including names with commas, quotes and line breaks

Register rules

- [ ] `PartRegister` wraps a list of entries (constructor takes `IEnumerable<RegisterEntry>`), immutable, exposing `Entries`
- [ ] `Contains(PartNumber)`; `NextNumber()` is `PartNumber.Next` over every entry's number
- [ ] `HasNumber(string? partNumberProperty)`: true only when the text parses as a part number and `Contains` it (spec: "in the format and in the register"; which file the entry names does not matter)
- [ ] `PartRegister Append(RegisterEntry)` returns a new register with the entry at the end
- [ ] `static SyncResult Merge(PartRegister shared, IEnumerable<RegisterEntry> pending)`: appends every pending entry to the shared register in order and returns the merged register plus a list of `RegisterDuplicate(PartNumber Number, RegisterEntry Existing, RegisterEntry Added)`, one for each pending entry whose number the shared register (or an earlier pending entry) already holds. An entry already in the shared register exactly (same number, path and time; a sync that was interrupted after writing) is not appended again and is not a duplicate
- [ ] Tests for every rule, with invented names and paths (`C:\Work\AWB-10001,test_plate.ipt`, user `testuser`, computer `TESTPC`)
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- The CSV format and its columns are a Default logged in `DECISIONS.md`. Aidan may open the file in Excel, which is why the order and header are fixed and extra columns are tolerated.
- Write the CSV reader by hand (a small state machine over characters); do not add a NuGet package for it.
- Rows are never removed or reordered by the add-in; keep `PartRegister` append-only.
- `DocumentKind` is in `InventorAddin.Core.Models`.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:** none

## Follow-ups

Things noticed but not done.
