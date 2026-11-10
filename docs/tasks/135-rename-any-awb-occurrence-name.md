# 135: Rename renames any occurrence with an AWB-format name

- **Milestone:** M4 part 1 (a change to M2's Rename, run in this round)
- **Feature spec:** `docs/features/02-part-creation-and-numbering.md` ("Commands: Rename", question 27)
- **Status:** done
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

Implemented on Aidan's Windows PC (Inventor 2026 installed, Inventor open, so no deploy).

What changed:

- `RenamePlan.RenamedOccurrence` now also renames any name in the AWB format. The match is one private
  `NewOccurrenceName(..., anyAwbName)`; the AWB check (`AwbNameDigits`, public as `IsAwbOccurrenceName`) splits at the
  last colon, reuses `DigitsAfter` for the digits and `PartFileName.SplitStem` (so `PartNumber.TryParse`) for the
  name and number, and requires the name before the comma not to be empty.
- New `RenamePlan.RenamedOccurrenceNamedAfterFile`: the task 134 rule only (old or new stem). `SaveAsReplacer.ReplacedOccurrenceName`
  calls it, so Save As and Replace is unchanged.
- `RenameService.cs`: not changed. The closed-assembly path needs nothing more: the file check still limits renames to
  occurrences of the renamed file, and the temporary-name-first step works whether a closed assembly's `Name` reads the
  stale stored name (matched by the AWB rule) or the new file name (matched by the new-stem rule, as before).
- Tests: two existing near-miss cases (`XBRACKET,AWB-10005:1`, `XBRACKET_2,AWB-10005:1`) are now AWB-format names that
  Rename renames by Aidan's answer, so they were removed from the "left alone" lists; new cases cover them.

Ran:

- `dotnet test tests/InventorAddin.Core.Tests`: first run 3698 passed, 1 failed
  (`NewPartCreatorTests.FailureAfterAssigning_ACloseThatFailsIsLogged_AndTheOriginalMessageIsKept`, a message about a
  folder under `AppData\Local` instead of "Disk full."; not related to this task). Re-run: 3699 passed, 0 failed; that
  test class alone: 35 passed. It looks flaky (see Follow-ups).
- `dotnet build InventorAddin.slnx -p:DeployToInventor=false -tl:off`: 0 errors, 8 warnings (all CS8600 in command
  files this task did not touch).

Not compiled: none. No file under `src/InventorAddin` was changed, and the solution built on Windows.

Inventor API members not confirmed: none (no add-in code changed).

Manual test checklist (Inventor 2026, after deploying):

1. Open an assembly with an occurrence of `WIDGET_V5,AWB-1xxxx.ipt` whose occurrence name is stale, in the AWB format
   but for another file (for example `ASSY_GEARBOX` with `BRACKET_V4,AWB-10017:1` on `BRACKET_V5,AWB-10022.ipt`).
2. Select that part in the browser, run AWB Rename, and rename it to `BRACKET_V6,AWB-10022.ipt`.
   Expect: the occurrence is now `BRACKET_V6,AWB-10022:1`.
3. Repeat with the assembly closed (only the part open) and reopen the assembly. Expect the same new occurrence name.
4. Give another occurrence of the same part a name of your own (`MY_NAME:2`) and rename the part again.
   Expect: `MY_NAME:2` is kept.
5. Save As and Replace: in an assembly, give an occurrence a stale AWB name (`OTHER,AWB-10017:1`) and run Save As and
   Replace on it. Expect: the component is replaced and the occurrence keeps `OTHER,AWB-10017:1` (task 134 rule). An
   occurrence named after the original still takes the copy's name.

## Follow-ups

- `NewPartCreatorTests.FailureAfterAssigning_ACloseThatFailsIsLogged_AndTheOriginalMessageIsKept` failed once and
  passed on re-run (the message named a folder under `AppData\Local` instead of "Disk full."). Looks like a flaky test
  that depends on the real temp folder; worth a look.
- The AWB rule uses `PartNumber.TryParse`, which trims white space, so `NAME, AWB-10017:1` and `NAME,AWB-10017 :1`
  count as AWB names and are renamed. This matches how file names are read (`PartFileName.SplitStem`). If Aidan wants
  a stricter match, it is one check in `RenamePlan.AwbNameDigits`.
