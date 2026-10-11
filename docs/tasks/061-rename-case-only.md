# 061: Rename allows a change of case only

- **Milestone:** M2 (fix run after run 2)
- **Feature spec:** `docs/features/02-part-creation-and-numbering.md` ("Commands: Rename")
- **Status:** todo
- **Depends on:** 058, 059
- **Needs:** windows build (local session); Core tested
- **Parallel-safe with:** none

## Goal

Aidan, 2026-10-11: names are now upper case and old lower-case files need converting, so Rename must accept a change of case only (`mounting_bracket,AWB-10001.ipt` to `MOUNTING_BRACKET,AWB-10001.ipt`). Windows treats the two names as one file, so the rename goes in two steps through a temporary name. References, drawings, occurrences and the register are updated the same as for any rename. Replaces the default that refused it (`SameFileMessage`).

## Scope

- `src/InventorAddin.Core/Numbering/RenamePlan.cs` (`ValidateNewName`, `SameFileMessage`, `PartRenamer`, a temporary-name rule)
- `src/InventorAddin.Core/ViewModels/RenameViewModel.cs` if its enabling logic needs it
- `src/InventorAddin/Commands/RenameService.cs` (`TargetExists` must not treat the file itself as taken for a case-only change)
- The matching tests under `tests/InventorAddin.Core.Tests/`

## Acceptance criteria

- [ ] `ValidateNewName`: a new path equal to the old one **ignoring case but not exactly** is accepted. A new path exactly equal to the old one is still refused, with a message reworded to say it is the file's current name (no longer mentioning case). Tested
- [ ] `RenameService.TargetExists` (or the view-model's use of it) does not report the file being renamed as an existing target for a case-only change
- [ ] A Core rule for the temporary name: same folder and extension, a name that cannot be mistaken for a part file name and is not already taken (for example `~RENAME-{guid}{ext}`, checked with the target's exists function). Tested
- [ ] `PartRenamer`, for a case-only change, runs the whole rename twice under **one** confirmation: old to temporary, then temporary to final, so every file found, every drawing of the model and every occurrence ends up on the final name. Drawings follow the model's name through both steps (task 059's rule). The register entry is updated once, from the old path to the final path, as any rename does. Tested with a fake target: the final references, drawing names, occurrence names and register entry, and that nothing named with the temporary name is left
- [ ] If the first step fails, nothing has changed, as now. If the second step fails, the result says the file is under the temporary name, which name that is, and that Rename can be run again from it; the old file is kept only under task 058's rules. Tested
- [ ] The result list does not show each file twice: a file updated in both steps is listed once
- [ ] `dotnet build InventorAddin.slnx -tl:off -p:DeployToInventor=false` succeeds and `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- Read tasks 058 and 059 and spike 052 first; this task reuses their steps and Inventor calls and adds none. The second step searches the same file list as the first (the list is made once, before the confirmation).
- Saving a document under a name that differs only in case would write over itself, which is why the temporary name is needed.
- A drawing of the model also changes case only; it goes through the temporary name with the model.

## Verification

Filled in by the implementer.

- **Ran:**
- **Changed under `src/InventorAddin` (compiled locally, not tested in Inventor):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. …

## Follow-ups

Things noticed but not done.
