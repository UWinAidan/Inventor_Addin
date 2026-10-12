# 065: Rename renames occurrences in closed assemblies

- **Milestone:** M2 (fix run 2)
- **Feature spec:** `docs/features/02-part-creation-and-numbering.md` ("Commands: Rename")
- **Status:** todo
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

- [ ] `RenamePlan.RenamedOccurrence(name, oldFile, newFile)` also returns the new name when `name` is already the **new** file name without extension, a colon and one or more digits (ignoring case), so these occurrences are set again. The old-name cases and the near misses stay as now. Tested
- [ ] A Core rule gives a temporary occurrence name, for example `RenamePlan.TemporaryOccurrenceName(string newName, Guid? guid = null)`: letters, digits and dashes only (no `~`), cannot be mistaken for a file name with a number, and keeps the `:N` suffix of `newName`, for example `AWBRENAME-3f2a…:1`. Tested with a fixed GUID
- [ ] `RenameOccurrences` sets each matching occurrence's `Name` to the temporary name and then to the new name. If the second set fails it tries to put the original name back, and reports the occurrence as a problem as now (with the name it is left under, when that is not the original). Each occurrence set to the new name counts as renamed, as now
- [ ] This applies to every assembly Rename updates, open or closed, and to the documents saved by `SaveRepointedDocuments` (they share `UpdateAndSave`)
- [ ] `dotnet build InventorAddin.slnx -tl:off -p:DeployToInventor=false` succeeds and `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- Read spike 062's Results, section 1, first. `ComponentOccurrence.Name` set is confirmed; the two-step set is the confirmed fix. No new Inventor members are needed.
- `Dirty = true` and `Save` already follow the occurrence renames in `UpdateAndSave`.
- An occurrence whose name already equals the new name in an **open** assembly (for example one Aidan renamed by hand to exactly that) is also set twice; that is harmless.
- Task 061's case-only rename goes through a temporary file name; its occurrences now match in both steps through the new-name rule. Do not change task 061's file steps.

## Verification

Filled in by the implementer.

- **Ran:**
- **Changed under `src/InventorAddin` (compiled locally, not tested in Inventor):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. …

## Follow-ups

Things noticed but not done.
