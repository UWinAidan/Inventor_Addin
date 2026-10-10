# 035: Register model and CSV format (Core)

- **Milestone:** M2
- **Feature spec:** `docs/features/02-part-creation-and-numbering.md` (The register: Format; Syncing; Part number: Has a number)
- **Status:** done
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

- [x] `sealed record RegisterEntry(PartNumber Number, string Name, string PartType, DocumentKind Kind, string FilePath, DateTimeOffset AssignedAt, string AssignedBy, string Computer)`. Text fields are never null (null becomes `""`)

CSV

- [x] `RegisterCsv.Header` is `Number,Name,PartType,Kind,FilePath,AssignedAt,AssignedBy,Computer`
- [x] `RegisterCsv.Write(IEnumerable<RegisterEntry>)` returns the CSV text: the header, then one line per entry in the order given, lines ending `\r\n` (Excel on Windows). A value containing a comma, a double quote, `\r` or `\n` is wrapped in double quotes with each quote doubled. `AssignedAt` is UTC ISO 8601 with seconds and `Z` (`2026-10-10T14:03:00Z`). `Kind` is the `DocumentKind` member name
- [x] `RegisterCsv.Read(string text)` returns a `RegisterReadResult` with the entries in file order and a list of warnings. It accepts `\n` or `\r\n`, a UTF-8 byte order mark, quoted fields with doubled quotes and line breaks inside quotes, and blank lines (skipped). Columns are found by header name, ignoring case and order, so a column Aidan adds in Excel is ignored, not an error
- [x] A row that cannot be read (bad number, bad date, unknown kind, wrong field count) is skipped with a warning naming its line number. A missing required column (`Number`) makes the whole text unreadable: `Read` throws `FormatException`
- [x] Round trip: `Read(Write(entries))` gives back equal entries, including names with commas, quotes and line breaks

Register rules

- [x] `PartRegister` wraps a list of entries (constructor takes `IEnumerable<RegisterEntry>`), immutable, exposing `Entries`
- [x] `Contains(PartNumber)`; `NextNumber()` is `PartNumber.Next` over every entry's number
- [x] `HasNumber(string? partNumberProperty)`: true only when the text parses as a part number and `Contains` it (spec: "in the format and in the register"; which file the entry names does not matter)
- [x] `PartRegister Append(RegisterEntry)` returns a new register with the entry at the end
- [x] `static SyncResult Merge(PartRegister shared, IEnumerable<RegisterEntry> pending)`: appends every pending entry to the shared register in order and returns the merged register plus a list of `RegisterDuplicate(PartNumber Number, RegisterEntry Existing, RegisterEntry Added)`, one for each pending entry whose number the shared register (or an earlier pending entry) already holds. An entry already in the shared register exactly (same number, path and time; a sync that was interrupted after writing) is not appended again and is not a duplicate
- [x] Tests for every rule, with invented names and paths (`C:\Work\AWB-10001,test_plate.ipt`, user `testuser`, computer `TESTPC`)
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- The CSV format and its columns are a Default logged in `DECISIONS.md`. Aidan may open the file in Excel, which is why the order and header are fixed and extra columns are tolerated.
- Write the CSV reader by hand (a small state machine over characters); do not add a NuGet package for it.
- Rows are never removed or reordered by the add-in; keep `PartRegister` append-only.
- `DocumentKind` is in `InventorAddin.Core.Models`.

## Verification

Filled in by the implementer.

- **Ran:** `dotnet build src/InventorAddin.Core`: succeeded, 0 warnings, 0 errors. `dotnet test tests/InventorAddin.Core.Tests`: 1125 passed, 0 failed (72 of them are the new `RegisterCsvTests` and `PartRegisterTests`). The working tree also held uncommitted changes from tasks 034, 037 and 038 when these ran.
- **Not compiled (changed under `src/InventorAddin`):** none; this task changes Core and tests only.
- **Inventor API members not confirmed:** none used.
- **Manual checklist for Inventor:** none

Choices the brief left open, made here for review:

- Text that holds no rows at all (empty, a byte order mark only, or blank lines only) reads as an empty register with no warnings, rather than throwing for a missing `Number` column. A header without `Number` still throws.
- Only `Number` is required. A missing optional column reads as blank (`""`, `DocumentKind.Unknown`, `DateTimeOffset.MinValue`) with one warning per missing column. A blank `Kind` or `AssignedAt` cell reads the same way, with no warning and without skipping the row.
- `Kind` matches `DocumentKind` member names ignoring case; a number such as `3` is rejected. `AssignedAt` reads any ISO 8601 date, assumed UTC when it has no offset, and is returned in UTC.
- Data cells are not trimmed, so the round trip is exact; only `Number` (through `PartNumber.TryParse`) and `Kind`/`AssignedAt` ignore surrounding spaces. Header names are trimmed.
- The round trip is exact for times in whole seconds; `Write` drops fractions of a second.
- `Merge`'s "already there exactly" check compares the number, the file path ignoring case (Windows paths), and the time to the second (what the CSV keeps). It checks against the register as merged so far, so a row repeated exactly in the pending list is added once. `RegisterDuplicate.Existing` is the first row in the register that holds the number.
- Added `PartRegister.Empty` and public column-name constants on `RegisterCsv`; not in the brief, but small and used by tests.

## Follow-ups

Things noticed but not done.

- A row that cannot be read is skipped, so its number does not count towards `NextNumber`. If Aidan breaks a row in Excel (for example the date), the add-in could hand out that number again. Task 036 should show the read warnings to the user, and it may be worth asking Aidan whether a row with a good `Number` but another bad value should still count as issued. Not decided here, because the brief says such rows are skipped.
