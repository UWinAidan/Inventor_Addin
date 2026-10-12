# 065: Rename renames occurrences in closed assemblies

- **Milestone:** M2 (fix run 2)
- **Feature spec:** `docs/features/02-part-creation-and-numbering.md` ("Commands: Rename")
- **Status:** done
- **Depends on:** 062
- **Needs:** windows build (local session); Core tested
- **Parallel-safe with:** none

## Goal

Bug from Aidan's checks, 2026-10-12: Rename with the assembly closed updates its reference but not the occurrence names in its browser. Closed assemblies must get the same occurrence renaming as open ones.

Spike 062 found the cause: in a closed assembly opened invisibly, after `ReplaceReference`, `ComponentOccurrence.Name` already reads the **new** file name (`BRACKET_V2,AWB-90001:1`) while the name stored in the file, and shown in the browser, stays the old one. `RenamePlan.RenamedOccurrence` looks for the old name, so nothing is renamed. Setting `Name` to the value it already reads does nothing; setting a temporary name first and then the new name works.

## Scope

- `src/InventorAddin.Core/Numbering/RenamePlan.cs` (`RenamedOccurrence`, a temporary occurrence name rule)
- `src/InventorAddin/Commands/RenameService.cs` (`RenameOccurrences`)
- The matching tests under `tests/InventorAddin.Core.Tests/`

## Acceptance criteria

- [x] `RenamePlan.RenamedOccurrence(name, oldFile, newFile)` also returns the new name when `name` is already the **new** file name without extension, a colon and one or more digits (ignoring case), so these occurrences are set again. The old-name cases and the near misses stay as now. Tested
- [x] A Core rule gives a temporary occurrence name, for example `RenamePlan.TemporaryOccurrenceName(string newName, Guid? guid = null)`: letters, digits and dashes only (no `~`), cannot be mistaken for a file name with a number, and keeps the `:N` suffix of `newName`, for example `AWBRENAME-3f2a…:1`. Tested with a fixed GUID
- [x] `RenameOccurrences` sets each matching occurrence's `Name` to the temporary name and then to the new name. If the second set fails it tries to put the original name back, and reports the occurrence as a problem as now (with the name it is left under, when that is not the original). Each occurrence set to the new name counts as renamed, as now
- [x] This applies to every assembly Rename updates, open or closed, and to the documents saved by `SaveRepointedDocuments` (they share `UpdateAndSave`)
- [x] `dotnet build InventorAddin.slnx -tl:off -p:DeployToInventor=false` succeeds and `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- Read spike 062's Results, section 1, first. `ComponentOccurrence.Name` set is confirmed; the two-step set is the confirmed fix. No new Inventor members are needed.
- `Dirty = true` and `Save` already follow the occurrence renames in `UpdateAndSave`.
- An occurrence whose name already equals the new name in an **open** assembly (for example one Aidan renamed by hand to exactly that) is also set twice; that is harmless.
- Task 061's case-only rename goes through a temporary file name; its occurrences now match in both steps through the new-name rule. Do not change task 061's file steps.

## Verification

Filled in by the implementer.

- **Ran:** `dotnet build InventorAddin.slnx -tl:off -p:DeployToInventor=false`: succeeded, 0 errors, 8 warnings (CS8600 in `CreatePrintCommand`, `SaveAsReplaceCommand`, `NewPartCommand`, `SaveAsCopyCommand`; none in files this task changed). `dotnet test tests/InventorAddin.Core.Tests`: 2165 passed, 0 failed, 0 skipped.
- **Core:** `RenamePlan.RenamedOccurrence` also matches the new file name, a colon and digits. New: `RenamePlan.TemporaryOccurrencePrefix` (`AWBRENAME-`, built from `Branding.ShortName`), `RenamePlan.TemporaryOccurrenceName(newName, guid)`, and `RenamePlan.OccurrenceLeftUnderText(original, leftUnder)` (the text added to a problem when the original name cannot be put back). Tests in `RenamePlanTests.cs`.
- **Changed under `src/InventorAddin` (compiled locally, not tested in Inventor):** `src/InventorAddin/Commands/RenameService.cs`. `RenameOccurrences` sets the temporary name, then the new name. If the first set fails, the occurrence is reported and left alone. If the second fails, `RestoreOccurrenceName` tries the original name and, when that fails too, the problem names the name it is left under. This covers every document that goes through `UpdateAndSave`: searched files, the update step of drawings, and `SaveRepointedDocuments`.
- **Inventor API members not confirmed:** none new. `ComponentOccurrence.Name` (get and set, including the two-step set) is confirmed by spike 062.
- **Manual checklist for Inventor:** use a test part `BRACKET,AWB-90001.ipt` in the work folder and an assembly `ASSY_TEST,AWB-90002.iam` with two occurrences of it under their default names (`BRACKET,AWB-90001:1`, `:2`) and a third renamed by hand to `LEFT`.
  1. **Closed assembly.** Close everything but the part. Open Part Properties on the part, click Rename, type `BRACKET_V2`, confirm. The result says the assembly was updated with 2 occurrences renamed, and the log (`%APPDATA%\AwbAddin\awbaddin.log`) reads `... updated, 2 occurrences renamed.` Open the assembly: the browser shows `BRACKET_V2,AWB-90001:1` and `:2`; `LEFT` is unchanged.
  2. **Open assembly.** With the assembly open in its own window, open the part and rename it to `BRACKET_V3`. The assembly's browser shows `BRACKET_V3,AWB-90001:1` and `:2` straight away; `LEFT` is unchanged; the result counts 2 occurrences renamed. Close and reopen the assembly: the names stay.
  3. **Assembly opened only through its parent.** Make `ASSY_TOP,AWB-90003.iam` holding `ASSY_TEST` as a subassembly. Close everything, open only `ASSY_TOP`, open the part from inside it and rename it to `BRACKET_V4`. The result lists `ASSY_TEST` as updated with 2 occurrences renamed. Close everything, open `ASSY_TEST` on its own: the browser shows `BRACKET_V4,AWB-90001:1` and `:2`.
  4. In each case, no occurrence is left with a name starting `AWBRENAME-`.
  5. Case-only rename (task 061): make a lower-case part `bracket_lc,AWB-1xxxx.ipt` (saved by hand) and an assembly using it twice, close both, open the part and Rename it to `BRACKET LC` (the box types in capitals). Open the assembly: the browser shows `BRACKET_LC,AWB-1xxxx:1` and `:2`, and no `~RENAME-` or `AWBRENAME-` names remain.

## Follow-ups

Things noticed but not done.

- The fake `IRenameTarget` in `PartRenamerTests.cs` counts occurrences through `RenamedOccurrence` only; it does not model the two-step set or the restore of the original name. Those are add-in code, checked only by the manual list above.
- `RenameOccurrences` renames top-level occurrences only; occurrences of the renamed file inside a subassembly are renamed when that subassembly is itself found and updated. Unchanged by this task.
