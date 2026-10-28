# 135: Rename renames any occurrence with an AWB-format name

- **Milestone:** M4 part 1 (a change to M2's Rename, run in this round)
- **Feature spec:** `docs/features/02-part-creation-and-numbering.md` ("Commands: Rename", question 27)
- **Status:** planned
- **Depends on:** 134
- **Needs:** windows build
- **Parallel-safe with:** 107, 108

## Goal

Aidan answered spec 02 question 27 on 2026-10-28: Rename also renames any occurrence of the renamed file whose name is in the AWB format (`NAME,AWB-#####:n`) to match its file. Before this, Rename renamed only an occurrence named after the old file (or already reading the new one), so a name left stale by Save As and Replace before task 134 (`BRACKET_V4,AWB-10017:1` for `BRACKET_V5,AWB-10022.ipt`) was never fixed.

After this task, renaming `BRACKET_V5,AWB-10022.ipt` to `BRACKET_V6,AWB-10022.ipt` renames that occurrence to `BRACKET_V6,AWB-10022:1`.

## Scope

- `src/InventorAddin.Core/Numbering/RenamePlan.cs`
  - `RenamedOccurrence` also returns the new stem, a colon and the same digits when `occurrenceName` is any name in the AWB format: text before the last comma (not empty), a comma, a part number that `PartNumber` accepts (any number, not only the file's), a colon and one or more digits, ignoring case as now. Reuse `PartNumber`'s parsing; do not write a second part number pattern.
  - Names not in that format are still left alone (`MY_NAME:1`, `BRACKET:1`, `BRACKET,AWB-1234:1`).
  - Update the summary to say so, with Aidan 2026-10-28.
- `src/InventorAddin.Core/Numbering/SaveAsReplacer.cs`
  - `ReplacedOccurrenceName` calls `RenamedOccurrence`. Aidan's answer is about Rename only, so Save As and Replace keeps task 134's rule: an occurrence named after the original only. Split the matching so Save As and Replace does not pick up the wider rule (for example a `RenamedOccurrence` overload or flag, or a separate `AnyAwbName` check used by Rename only). Keep one implementation of the stem and digits matching.
- `tests/InventorAddin.Core.Tests/Numbering/RenamePlanTests.cs` (or where `RenamedOccurrence` is tested)
  - A stale AWB name with another stem and another number is renamed; with the same number; case ignored; several digits (`:12`); no digits; a name with no comma, a bad part number, or an empty name before the comma is left alone; the old-stem and new-stem cases still pass.
- `tests/InventorAddin.Core.Tests/Numbering/SaveAsReplacerTests.cs`
  - A test that `ReplacedOccurrenceName` still gives null for a stale AWB name not made from the original.
- `src/InventorAddin/Commands/RenameService.cs`: no change expected. It calls `RenamedOccurrence` for each occurrence of the renamed file; check that the closed-assembly path (temporary name first, task 065) needs nothing more.

Out of scope:

- Occurrences of files other than the one renamed.
- Save As and Replace's rule (task 134, unchanged).

## Acceptance criteria

- [ ] In an assembly, an occurrence of the renamed file named `OTHER,AWB-nnnnn:1` takes the new file's name with `:1`, open and closed.
- [ ] An occurrence with a name the user gave (not in the AWB format) keeps it.
- [ ] Save As and Replace behaves as after task 134.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes, and the solution builds on Windows.

## Notes for the implementer

- Aidan's test case is the stale `BRACKET_V4,AWB-10017:1` in his `ASSY_GEARBOX`. Use invented names in tests.

## Verification

Filled in by the implementer.

## Follow-ups
