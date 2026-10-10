# 041: Mass Update scan result and view-model (Core)

- **Milestone:** M2
- **Feature spec:** `docs/features/02-part-creation-and-numbering.md` (Commands: Mass Update; Windows user name as Designer or Detailer)
- **Status:** done
- **Depends on:** 033, 034, 035, 038
- **Needs:** cloud
- **Parallel-safe with:** 039, 040, 042

## Goal

Core decides everything Mass Update shows and does: which files to scan, what state each file is in, which actions apply to it, and the list of changes to make. Task 048 adds the window and the Inventor side that opens files.

## Scope

- `src/InventorAddin.Core/Numbering/MassUpdate/` (new folder): `ScanFolder.cs`, `ScannedFile.cs`, `MassUpdatePlan.cs`
- `src/InventorAddin.Core/ViewModels/MassUpdateViewModel.cs` (new)
- `tests/InventorAddin.Core.Tests/Numbering/MassUpdate/` and `tests/InventorAddin.Core.Tests/ViewModels/MassUpdateViewModelTests.cs` (new)

Out of scope:

- Opening, reading or saving Inventor files (048).

## Acceptance criteria

Which files

- [x] `ScanFolder.FilesToScan(string root)` lists `.ipt` and `.iam` files under `root`, including subfolders, skipping any folder named `OldVersions` (ignoring case), sorted by path. Tested on a temporary folder tree

File state

- [x] `ScannedFile` record: full path, Part Number, Designer, Detailer, part type, kind, `bool OpenInInventor`, and `string? ReadError`
- [x] `ScannedFileState.For(ScannedFile, PartRegister, string windowsUserName)` gives: `NumberState` (`Registered`, `WellFormedUnregistered`, `None`), `bool DesignerIsWindowsUser`, `bool DetailerIsWindowsUser` (same comparison as `PersonNames`, task 038, but without the default-designer condition), and which of the three actions apply: Register (`WellFormedUnregistered`), Assign (`None`), Replace names (either name is the Windows user)
- [x] A file open in Inventor, or with a read error, offers no action and says why (`"Open in Inventor; close it first."`, or the read error)

Plan

- [x] `MassUpdatePlan.Build(IEnumerable<(ScannedFile file, MassUpdateActions ticked)>, string defaultDesigner)` returns one `FileUpdate` per file with at least one ticked action that applies: the register action (`RegisterExisting(PartNumber)` or `AssignNew`), and the property writes for name replacement (Designer and Detailer set to `defaultDesigner`; Detailer only if it was the Windows user name). An action that does not apply to the file is ignored
- [x] Replace names with a blank `defaultDesigner` is not offered (the view-model disables it with `"Set Default designer in Settings first."`)
- [x] Registering an existing number creates a `RegisterEntry` with that number, the file's name (`PartName.FromFileName`), part type, kind and path. When two scanned files carry the same unregistered number, the second is reported as a duplicate in the plan, not registered

View-model

- [x] Holds the folder (default given by the caller), a Scan command (the caller does the scanning and hands back `ScannedFile`s), rows with the state text and three tick boxes per row, "tick all" for each action, counts (`"12 files, 3 to register, 5 to number, 7 with the Windows user name"`)
- [x] `ConfirmText`: `"Change N files? This saves them and cannot be undone with Undo."`; Apply produces the plan
- [x] After Apply, the caller reports per-file results back and each row shows done or the error
- [x] Tests for each state, the plan, the counts and the confirmation text, with invented paths and names
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- Aidan asked for bulk replacement of the Windows user name; making the folder scan and Mass Update one command, and its three actions, are Defaults (spec 02).
- Assigning a number here never renames the file.
- Keep the view-model independent of Inventor: the add-in passes data in and receives the plan out.

## Verification

Filled in by the implementer.

- **Ran:** `dotnet build src/InventorAddin.Core`: 0 warnings, 0 errors. `dotnet test tests/InventorAddin.Core.Tests`: 1284 passed, 0 failed (86 of them in the new Mass Update tests: `ScanFolderTests`, `ScannedFileStateTests`, `MassUpdatePlanTests`, `MassUpdateViewModelTests`). Cloud session (`CLAUDE_CODE_REMOTE=true`).
- **Not compiled (changed under `src/InventorAddin`):** none; this task changes Core and tests only.
- **Inventor API members not confirmed:** none used.
- **Manual checklist for Inventor:** none (covered by 048)

Implementation notes for review and for 048:

- `MassUpdatePlan.Build` takes `(ScannedFileState state, MassUpdateActions ticked)` pairs rather than `(ScannedFile, ...)`: deciding whether an action applies needs the register and the Windows user name, and the state already carries both results (and the `ScannedFile` as `state.File`). The view-model builds the states in `ShowScan`.
- Replace names writes Designer only where Designer was the Windows user name, and Detailer only where Detailer was, following spec 02 ("replace a Designer or Detailer equal to the Windows user name"). The brief's wording could be read as always writing Designer; the spec reading never overwrites a real name.
- Duplicates: the first file in scan order that carries an unregistered number keeps it, ticked or not (also if it is open in Inventor); any later file ticked to register the same number is reported in `MassUpdatePlan.Duplicates` and not registered. Its name replacement, if ticked, still happens.
- `ConfirmText` uses the singular for one file: `"Change 1 file? This saves it and cannot be undone with Undo."`.
- Counts count the files each action applies to (not the ticked ones); files open in Inventor or not read count only in the file total. "with the Windows user name" counts files offering Replace names, even when Default designer is blank.
- Flow for 048: `ScanCommand` raises `ScanRequested` (folder trimmed); the add-in syncs the register, reads the files listed by `ScanFolder.FilesToScan`, and calls `ShowScan(files, register)`. `ApplyCommand` raises `ApplyRequested` with the plan; the add-in confirms with `ConfirmText`, then for each `FileUpdate`: `RegisterExisting` appends `EntryFor(number, ...)` to the register; `AssignNew` picks the next number, appends `EntryFor(next, ...)` and writes `FileUpdate.PartNumberWrite(next)`; then makes `Writes` and saves. It reports back with `ReportResults(FileUpdateResult[])`.
- Spec 02 question 19 (Default: refuse to assign or sync while the shared register has an unreadable row) bears on 048, not here: the add-in must stop before registering or assigning in that case. Question 18 does not apply (Mass Update never renames).

## Follow-ups

Things noticed but not done.

- A file carrying a number that the register holds for a different file (for example a copy made in Explorer) shows as "Has a number", because "has a number" ignores the path by design. Mass Update could flag numbers carried by two scanned files even when registered. Needs a decision by Aidan.
- A register-only update writes nothing to the file, so 048 need not save it; the confirmation still counts it as a changed file.
