# 134: Save As and Replace renames the occurrence it replaces

- **Milestone:** M4 part 1 (a fix to M2's Save As and Replace, run in this round)
- **Feature spec:** `docs/features/02-part-creation-and-numbering.md` ("Commands: Save As and Replace", "Commands: Rename")
- **Status:** todo
- **Depends on:** none
- **Needs:** windows build
- **Parallel-safe with:** 105, 109, 133

## Goal

Aidan reported on 2026-10-25 that after Rename, `ASSY_GEARBOX` still shows `BRACKET_V5,AWB-10022.ipt` as `BRACKET_V4,AWB-10017:1`, with the assembly open and with it closed.

The log shows Rename is not at fault. It renames occurrences named after the old file: on 2026-10-12 it renamed 2. The stale name came from **Save As and Replace**. On 2026-10-17 it replaced `BRACKET_V4,AWB-10017.ipt` with its copy `BRACKET_V5,AWB-10022.ipt`, and Inventor's `ComponentOccurrence.Replace` keeps the occurrence's name. From then on, the occurrence was no longer named after its file, so each later Rename found nothing to rename ("0 occurrences renamed"; spike 103, Results).

After this task, Save As and Replace gives the replaced occurrence the copy's name when it was named after the original. One Undo reverts the replace and the name together.

## Scope

- `src/InventorAddin.Core/Numbering/SaveAsReplacer.cs`
  - `ReplaceSelection` gains `string? OccurrenceName`, the selected occurrence's name as Inventor reads it.
  - New `public static string? ReplacedOccurrenceName(string? occurrenceName, string? originalFullPath, string? copyFullPath)`. It returns the copy's file name without folder and extension, a colon and the same digits, when `occurrenceName` is the original's file name without folder and extension, a colon and digits (ignoring case). Otherwise it returns null. `BRACKET_V4,AWB-10017:1` becomes `BRACKET_V5,AWB-10022:1`, and `MY_NAME:1` gives null. It reuses `RenamePlan.RenamedOccurrence`'s matching. Do not copy that matching. The new-stem branch of `RenamedOccurrence`, a name already reading the new file, may stay: it gives the same name.
  - `ISaveAsReplaceTarget.ReplaceOccurrence(string copyFullPath)` becomes `ReplaceOccurrence(string copyFullPath, string? newOccurrenceName)`. It returns `string?`: null when no name was asked for or the name was set, else the reason the name could not be set.
  - In `Replace`, step 3 passes the name. When the name could not be set, the replace still stands. A Warn is logged, and the result's message says the occurrence kept its old name, with the reason. Find where the success text is built (`NewPartResult` or the command) and add the line there.
- `tests/InventorAddin.Core.Tests/Numbering/SaveAsReplacerTests.cs`
  - `ReplacedOccurrenceName`: renamed; another name left alone; case ignored; several digits (`:12`); no digits; an original with no number (`BRACKET.ipt` to `BRACKET_2,AWB-10030.ipt`); blank input.
  - `Replace` passes the name to the fake target, and passes null for a user-named occurrence.
  - A name failure keeps the replace and gives the note.
  - The existing tests are updated for the new signature.
- `src/InventorAddin/Commands/SaveAsReplaceCommand.cs`
  - `ReadSelection` fills `OccurrenceName` (`ComSafe.Get(() => occurrence.Name)`).
  - `InventorReplaceTarget.ReplaceOccurrence` calls `_occurrence.Replace(copyFullPath, false)` and then sets `_occurrence.Name = newOccurrenceName`, both inside the one `RunInTransaction`, so one Undo reverts both. A failure of the `Name` set is caught inside the transaction and returned as the reason; it does not abort the replace.
  - For an occurrence inside a sub-assembly (a `ComponentOccurrenceProxy`), set the name on the proxy's `NativeObject`, so the sub-assembly's own occurrence is renamed before the sub-assembly is saved.
- `docs/features/02-part-creation-and-numbering.md`
  - Save As and Replace gets one bullet: "The replaced occurrence takes the copy's name when it was named after the original (`OLD,AWB-10017:1` becomes `NEW,AWB-10022:1`); one given another name keeps it (Default, task 134)."
  - Open question 27 (stale names left before this task) was added by the parent; leave it.

Out of scope:

- Rename's occurrence rule (Aidan, 2026-10-11, unchanged; question 27 is for Aidan).
- Mass renaming of names that are already stale.

## Acceptance criteria

- [ ] After Save As and Replace on an occurrence named `ORIGINAL,AWB-n:1`, the browser shows the copy's name with `:1`. One Undo puts back both the original component and the original name.
- [ ] An occurrence with a name the user gave keeps it.
- [ ] Inside a sub-assembly, the sub-assembly's occurrence is renamed and the sub-assembly saved with it.
- [ ] A later Rename of the copy renames that occurrence (Rename's existing rule now matches).
- [ ] A failure to set the name leaves the replace done, is logged, and is reported in the result.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes, and the solution builds on Windows.

## Notes for the implementer

- Log lines that show the cause, in Aidan's log of 2026-10-17 09:03:43: "Save As Copy: 'C:\CAD_TEST\BRACKET_V4,AWB-10017.ipt' copied to 'C:\CAD_TEST\BRACKET_V5,AWB-10022.ipt' … Save As and Replace: the occurrence of … replaced with AWB-10022". These are Aidan's personal test files. Use invented names in tests.
- In an open assembly, after `Replace`, `Name` still reads the old name (spike 103), so setting the new name changes it directly. The temporary-name step Rename uses (task 065, spike 062) is for closed assemblies opened invisibly, and is not needed here.
- `ComponentOccurrence.Name` is settable (task 058, `RenameService.RenameOccurrences`). `ComponentOccurrenceProxy.NativeObject` is used in spike 032's notes. List it as not confirmed if you find no use in the code.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. In one of your assemblies, select an occurrence named after its file (for example `PLATE,AWB-…:1`) and run Save As and Replace with a new name. The browser shows the new file's name with `:1`.
  2. Press Undo once: the original component and its name are back.
  3. Repeat on an occurrence you renamed by hand: it keeps your name.
  4. Repeat on a component inside a sub-assembly: the sub-assembly's occurrence takes the new name. Open the sub-assembly on its own to check.
  5. After step 1, rename the copy with Rename in Part Properties: the occurrence follows the new name.
  6. Rename the stale `BRACKET_V4,AWB-10017:1` in `ASSY_GEARBOX` by hand once, unless you answer spec 02 question 27 the other way.

## Follow-ups
