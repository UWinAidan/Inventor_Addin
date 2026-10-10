# 041: Mass Update scan result and view-model (Core)

- **Milestone:** M2
- **Feature spec:** `docs/features/02-part-creation-and-numbering.md` (Commands: Mass Update; Windows user name as Designer or Detailer)
- **Status:** todo
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

- [ ] `ScanFolder.FilesToScan(string root)` lists `.ipt` and `.iam` files under `root`, including subfolders, skipping any folder named `OldVersions` (ignoring case), sorted by path. Tested on a temporary folder tree

File state

- [ ] `ScannedFile` record: full path, Part Number, Designer, Detailer, part type, kind, `bool OpenInInventor`, and `string? ReadError`
- [ ] `ScannedFileState.For(ScannedFile, PartRegister, string windowsUserName)` gives: `NumberState` (`Registered`, `WellFormedUnregistered`, `None`), `bool DesignerIsWindowsUser`, `bool DetailerIsWindowsUser` (same comparison as `PersonNames`, task 038, but without the default-designer condition), and which of the three actions apply: Register (`WellFormedUnregistered`), Assign (`None`), Replace names (either name is the Windows user)
- [ ] A file open in Inventor, or with a read error, offers no action and says why (`"Open in Inventor; close it first."`, or the read error)

Plan

- [ ] `MassUpdatePlan.Build(IEnumerable<(ScannedFile file, MassUpdateActions ticked)>, string defaultDesigner)` returns one `FileUpdate` per file with at least one ticked action that applies: the register action (`RegisterExisting(PartNumber)` or `AssignNew`), and the property writes for name replacement (Designer and Detailer set to `defaultDesigner`; Detailer only if it was the Windows user name). An action that does not apply to the file is ignored
- [ ] Replace names with a blank `defaultDesigner` is not offered (the view-model disables it with `"Set Default designer in Settings first."`)
- [ ] Registering an existing number creates a `RegisterEntry` with that number, the file's name (`PartName.FromFileName`), part type, kind and path. When two scanned files carry the same unregistered number, the second is reported as a duplicate in the plan, not registered

View-model

- [ ] Holds the folder (default given by the caller), a Scan command (the caller does the scanning and hands back `ScannedFile`s), rows with the state text and three tick boxes per row, "tick all" for each action, counts (`"12 files, 3 to register, 5 to number, 7 with the Windows user name"`)
- [ ] `ConfirmText`: `"Change N files? This saves them and cannot be undone with Undo."`; Apply produces the plan
- [ ] After Apply, the caller reports per-file results back and each row shows done or the error
- [ ] Tests for each state, the plan, the counts and the confirmation text, with invented paths and names
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- Aidan asked for bulk replacement of the Windows user name; making the folder scan and Mass Update one command, and its three actions, are Defaults (spec 02).
- Assigning a number here never renames the file.
- Keep the view-model independent of Inventor: the add-in passes data in and receives the plan out.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:** none (covered by 048)

## Follow-ups

Things noticed but not done.
