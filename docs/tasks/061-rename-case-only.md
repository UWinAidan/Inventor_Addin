# 061: Rename allows a change of case only

- **Milestone:** M2 (fix run after run 2)
- **Feature spec:** `docs/features/02-part-creation-and-numbering.md` ("Commands: Rename")
- **Status:** done (Core tested; add-in compiled locally, not tested in Inventor)
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

- [x] `ValidateNewName`: a new path equal to the old one **ignoring case but not exactly** is accepted. A new path exactly equal to the old one is still refused, with a message reworded to say it is the file's current name (no longer mentioning case). Tested
- [x] `RenameService.TargetExists` (or the view-model's use of it) does not report the file being renamed as an existing target for a case-only change
- [x] A Core rule for the temporary name: same folder and extension, a name that cannot be mistaken for a part file name and is not already taken (for example `~RENAME-{guid}{ext}`, checked with the target's exists function). Tested
- [x] `PartRenamer`, for a case-only change, runs the whole rename twice under **one** confirmation: old to temporary, then temporary to final, so every file found, every drawing of the model and every occurrence ends up on the final name. Drawings follow the model's name through both steps (task 059's rule). The register entry is updated once, from the old path to the final path, as any rename does. Tested with a fake target: the final references, drawing names, occurrence names and register entry, and that nothing named with the temporary name is left
- [x] If the first step fails, nothing has changed, as now. If the second step fails, the result says the file is under the temporary name, which name that is, and that Rename can be run again from it; the old file is kept only under task 058's rules. Tested
- [x] The result list does not show each file twice: a file updated in both steps is listed once
- [x] `dotnet build InventorAddin.slnx -tl:off -p:DeployToInventor=false` succeeds and `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- Read tasks 058 and 059 and spike 052 first; this task reuses their steps and Inventor calls and adds none. The second step searches the same file list as the first (the list is made once, before the confirmation).
- Saving a document under a name that differs only in case would write over itself, which is why the temporary name is needed.
- A drawing of the model also changes case only; it goes through the temporary name with the model.

## Verification

Filled in by the implementer.

- **Ran:** `dotnet build InventorAddin.slnx -tl:off -p:DeployToInventor=false`: build succeeded, only the 4 existing CS8600 warnings (CreatePrintCommand, NewPartCommand, SaveAsCopyCommand, SaveAsReplaceCommand; each listed twice, once for the WPF temp project). `dotnet test tests/InventorAddin.Core.Tests`: 2157 passed, 0 failed (baseline 2115), first run, no timeouts. The new tests use a fake target and a fixed GUID; none depends on timing. The two new register tests use the same temporary-folder store as the existing register tests.
- **Core:** `RenamePlan.ValidateNewName` now refuses only a path exactly equal to the current one (ordinal); `SameFileMessage` reads "That is the file's current name. Type a different name." New `RenamePlan.IsCaseOnlyChange(old, new)`, `TemporaryFileName(path, exists, newGuid?)` (`~RENAME-{32 hex}{ext}` in the same folder, skipping taken names, giving up after `TemporaryNameAttempts` = 10 with an `IOException`) and `IsTemporaryFileName`. `ConfirmationMessage` adds a line for a case-only change. `RenameViewModel` does not call `targetExists` for a case-only change, so a lower-case file opens in the Rename window with Rename enabled on its own name. `PartRenamer`'s steps 1 to 5 moved into `RunStep`; `Run` runs it once for an ordinary rename (same calls, notes and log lines as before) and twice for a case-only change (`RunCaseOnly`). The second step searches the same list, with each drawing the first step renamed (and whose old file was deleted) replaced by its temporary name, plus any renamed drawing whose old file stayed. Description is written once, in the second step. The register entry is updated once, old path to final path. The two steps' file lists are merged (`MergeSteps`): each file once, under the name it was found by, with the second step's outcome, both steps' occurrence problems, and the final `RenamedTo`.
- **Fail-safe rules (choices within the brief):** the second step runs only when the first step deleted the old file. Saving under the final name while the old file still exists would write over a file Windows treats as the same one. So when the first step keeps the old file (a file not updated, a folder skipped, an open document still on it, or the delete failed), Rename stops under the temporary name. Every file it updated uses that name, and every file it could not update still uses the old file, which is kept, so nothing breaks. The result says so (`StoppedAtTemporaryNameOldFileKeptMessage`). When the second step's `SaveAs` fails, the file also stays under the temporary name (`StoppedAtTemporaryNameMessage`). In both cases `RenameResult.TemporaryFullFileName` is set, Description is still written, the register entry names the temporary file (where the file is), and Part Properties gets the temporary path as the file's new name (`RenameResult.FullFileNameNow`). When the second step's own old-file rules keep the temporary file (for example its delete fails), the model has its final name and the temporary file is reported as the kept old file, as for any rename. If the temporary name cannot be checked, or the first `SaveAs` fails, nothing is changed.
- **Changed under `src/InventorAddin` (compiled locally, not tested in Inventor):** `Commands/RenameService.cs`, the doc comment on `TargetExists` only. No code change was needed: the view-model skips the exists check for a case-only change, and `PartRenamer` asks the existing `InventorRenameTarget.FileExists` whether the temporary name is free.
- **Inventor API members not confirmed:** none new; the task adds no Inventor calls. Not confirmed by a test in Inventor: (1) that `SaveAs` to the final name, straight after the old file was deleted, writes the file with the new case (Inventor could keep a cached file entry under the old spelling); (2) that Inventor accepts `~` in an occurrence name (`~RENAME-…:1` after the first step). If it does not, those occurrences are reported as not renamed and keep their old name, and the second step does not match them; (3) that a drawing reopened under the temporary name in the first step is found again by `FindOpen` in the second (it should be: the same path, compared ignoring case).
- **Manual checklist for Inventor:**
  1. Close Inventor, build and deploy (`dotnet build InventorAddin.slnx`), start Inventor.
  2. In a scratch folder inside the work folder, make a lower-case numbered part `mounting_bracket,AWB-1xxxx.ipt` (save it under that name by hand), its print `Prints\mounting_bracket,AWB-1xxxx.dwg` (from Create Print, renamed to lower case in Explorer before the part is opened, or made by hand), and an assembly using the part twice (occurrences `mounting_bracket,AWB-1xxxx:1` and `:2`). Save all, close all.
  3. Open the part. Part Properties, Rename. The box shows `MOUNTING BRACKET`, the preview `MOUNTING_BRACKET,AWB-1xxxx.ipt`, and Rename is enabled (no "already exists" message). Click Rename. The confirmation says only the case changes and that it goes through a temporary name, and lists the print "to MOUNTING_BRACKET,AWB-1xxxx.dwg". Confirm once; no second confirmation appears.
  4. The result says "Renamed to MOUNTING_BRACKET,AWB-1xxxx.ipt", lists the assembly once and the print once ("renamed to MOUNTING_BRACKET,AWB-1xxxx.dwg"), "2 occurrences renamed", "1 drawing renamed", and no problem.
  5. In Explorer: the part and the print show the upper-case names; no `~RENAME-` file is left in the folder or in `Prints`.
  6. Open the assembly: no missing-reference dialog; the occurrences read `MOUNTING_BRACKET,AWB-1xxxx:1` and `:2`. Open the print: the view shows the part. Closing everything asks to save nothing.
  7. Repeat from a fresh lower-case part with the assembly and the print open (visible, saved) before renaming: same result; the print window closes and comes back under the upper-case name; the part stays the active window.
  8. Exactly the current name: open Rename on `MOUNTING_BRACKET,AWB-1xxxx.ipt` without changing the name: the window says "That is the file's current name. Type a different name." and Rename is disabled.
  9. Stopped under the temporary name: make another lower-case part and an assembly that uses it, close both, make the assembly read-only in Explorer, open the part and rename it to upper case. The result lists the assembly as not updated and says the file is now named `~RENAME-….ipt`, that the old file was kept, and to run Rename again on it once nothing uses the old file. In Explorer both the old part and the `~RENAME-` part exist; the assembly opens on the old part with no missing-reference dialog. Report what you see.
  10. With a registered number: the register row now names `MOUNTING_BRACKET,AWB-1xxxx.ipt`. In `%APPDATA%\AwbAddin\awbaddin.log`: a line "only the case changes; … is renamed through …~RENAME-….ipt", and the last Rename line "finished '…mounting_bracket…' -> '…MOUNTING_BRACKET…'".

## Follow-ups

Things noticed but not done.

- Running Rename again on a file left under the temporary name: the temporary name holds no number, so the new name keeps the part number only when the register holds it (Part Number set and registered). A file numbered only by its old file name would lose the number on that rerun. It needs the two-step rename to stop part way, but Aidan may want the number kept another way, for example carried in the temporary name, which the brief ruled out ("cannot be mistaken for a part file name").
- When the first step stops because the old file had to be kept, Rename on the temporary file to the final name is refused while the old file exists ("already exists"). The result tells Aidan to delete the old file once nothing uses it. Aidan's call whether Rename should do more here.
- Spec 02's case-only bullet could say that the rename stops under the temporary name when the first step must keep the old file or the second save fails, and that the register then names the temporary file.
