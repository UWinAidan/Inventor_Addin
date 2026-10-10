# 047: Rename, with references updated across the CAD folders

- **Milestone:** M2
- **Feature spec:** `docs/features/02-part-creation-and-numbering.md` (Commands: Rename); `docs/features/07-part-properties.md` (Part name)
- **Status:** todo
- **Depends on:** 032 (its Results must be filled in), 034, 036, 039, 049, 051
- **Needs:** cloud, add-in not compiled
- **Parallel-safe with:** none (edits the Part Properties window and command)

## Goal

A Rename button beside the part name in Part Properties renames the file, updates every assembly, drawing and presentation in the CAD folders from Settings that references it, sets Description to the new name, and updates the register.

## Before starting

Read the Results of task 032, questions 3, 4 and 5. Build the method that worked there. If they are empty, or no method worked, stop and record why.

## Scope

- `src/InventorAddin.Core/Numbering/RenamePlan.cs` (new), with tests
- `src/InventorAddin.Core/ViewModels/RenameViewModel.cs` (new), with tests
- `src/InventorAddin.Core/Numbering/PartRegister.cs` and `RegisterStore.cs` (update an entry's path and name)
- `src/InventorAddin/Commands/RenameService.cs` (new: does the rename by 032's method)
- `src/InventorAddin/UI/RenameWindow.xaml` and `.xaml.cs` (new)
- `src/InventorAddin/UI/PartPropertiesWindow.xaml`, `src/InventorAddin.Core/ViewModels/PartPropertiesViewModel.cs`, `src/InventorAddin/Commands/PartPropertiesCommand.cs` (the Rename button)

## Acceptance criteria

Core

- [ ] `RenamePlan.NewFileName(string fullFileName, string newName)`: keeps the folder and extension; `{number},{new_name}` when the old file name carries a number (`PartFileName.TryReadNumber`), otherwise `{new_name}`
- [ ] `RenamePlan.SearchFolders(string? workFolder, IEnumerable<string> otherCadFolders, string renamedFile)`: the work folder and each other CAD folder, blanks dropped, duplicates and folders inside another listed folder removed (ignoring case); plus the renamed file's own folder when the file lies in none of them (Default)
- [ ] `RenamePlan.CandidateFiles(IEnumerable<string> files)`: `.iam`, `.idw`, `.dwg` and `.ipn`, skipping `OldVersions` folders; `RenamePlan.FilesToSearch(IEnumerable<string> folders)` lists them from disk, each once, skipping a folder that does not exist (temporary folder tests). Listing runs off the UI thread with a timeout, since a CAD folder on a dropped drive can block (spike 032, question 8)
- [ ] Register: `RegisterStore.UpdateEntry(PartNumber, string newPath, string newName)` rewrites that entry's `FilePath` and `Name` under the lock (the only change the add-in ever makes to an existing row; Aidan's "rows are never removed or reordered" still holds). Offline: the update waits in a pending-updates file and is applied at the next sync
- [ ] The view-model: new name (pre-filled with the current name), validation from `PartFileName` (a comma is refused when the file has no number: spec 02 question 18), a preview of the new file name, a target-exists check supplied by the caller, and a result list (updated files, files that failed with the reason)

Add-in

- [ ] A **Rename** button beside the Part name row in Part Properties, access key R, shown for saved, editable files
- [ ] Rename asks for confirmation naming how many files will be searched, then: renames by 032's method, updates references in each candidate file that references the old path, sets Description to the new name, updates the register entry when the file has a registered number, and shows the result list
- [ ] Files open in Inventor: `SaveAs` repoints open assemblies in session and marks them modified; save each of those. Closed files are updated by the `ReplaceReference` loop (spike 032)
- [ ] Part Properties' header and part name show the new name afterwards
- [ ] A failure part way leaves the old file in place where possible, and the result list says exactly which files were updated and which were not
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- Added after spike 032 (question 4), the method that worked: (1) the renamed document is open (Part Properties' document): `doc.SaveAs(newPath, false)`; open assemblies holding it now point at the new file and are dirty. (2) For each candidate file that is not open: `InventorHost.App.SilentOperation = true` around `Documents.Open(path, false)` (restore in `finally`), then `foreach (FileDescriptor fd in d.File.ReferencedFileDescriptors)`, where `fd.FullFileName` equals the old path ignoring case, `fd.ReplaceReference(newPath)`; if any changed, `d.Save()`; always `d.Close(true)`. A candidate that is open: the same loop on the open document (it works on open documents), then `Save()`. (3) Save the open documents step 1 dirtied. (4) Delete the old file last; if anything failed, keep it and say so. There is no `Document.ReferencedFileDescriptors`: go through `doc.File`.
- Detect an open file with a loop over `Documents` comparing `FullFileName` ignoring case; `get_ItemByName` throws when the file is not open.
- Expect about 1 to 2 s per referencing file and about 60 ms per candidate that does not reference it; show progress.
- The CAD folders replace the project workspace (Aidan, spec 02 question 23).

- Rename's search area (the active project's workspace) is Aidan's. Skipping `OldVersions` and updating the register entry are Defaults.
- Rename cannot be undone with one Undo; the confirmation says so.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. A test part used by a closed assembly and with a closed drawing, all in the workspace: Rename to a new name. The file is renamed (number kept), Description changes, the assembly and drawing open with no missing-reference prompt and show the renamed part
  2. The register row has the new path and name
  3. Same with the assembly open in Inventor
  4. A name that already exists in the folder: refused before anything changes
  5. An old file without a number: renamed to just the new name

## Follow-ups

Things noticed but not done.
