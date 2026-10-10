# 047: Rename, with references updated across the CAD folders

- **Milestone:** M2
- **Feature spec:** `docs/features/02-part-creation-and-numbering.md` (Commands: Rename); `docs/features/07-part-properties.md` (Part name)
- **Status:** done (add-in not compiled)
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

- [ ] `RenamePlan.NewFileName(string fullFileName, string newName, PartNumber? registeredNumber)`: keeps the folder and extension; `{number},{new_name}` with the file's registered Part Number when it has one (`RegisterStore.HasNumber` on the Part Number property), otherwise the number the old file name carries (`PartFileName.TryReadNumber`); `{new_name}` when there is neither (spec 02; parent decision, 2026-10-10). So a file numbered by Generate or Mass Update gains its number when renamed
- [ ] `RenamePlan.SearchFolders(string? workFolder, IEnumerable<string> otherCadFolders, string renamedFile)`: the work folder and each other CAD folder, blanks dropped, duplicates and folders inside another listed folder removed (ignoring case); plus the renamed file's own folder when the file lies in none of them (Default)
- [ ] `RenamePlan.CandidateFiles(IEnumerable<string> files)`: `.iam`, `.idw`, `.dwg` and `.ipn`, skipping `OldVersions` folders; `RenamePlan.FilesToSearch(IEnumerable<string> folders)` lists them from disk, each once, skipping a folder that does not exist (temporary folder tests). Listing runs off the UI thread with a timeout, since a CAD folder on a dropped drive can block (spike 032, question 8)
- [ ] Register: `RegisterStore.UpdateEntry(PartNumber, string newPath, string newName)` rewrites that entry's `FilePath` and `Name` under the lock (the only change the add-in ever makes to an existing row; Aidan's "rows are never removed or reordered" still holds). Offline: the update waits in a pending-updates file and is applied at the next sync
- [ ] The view-model: new name (pre-filled with the current name), validation from `PartFileName` (a comma is refused when the new file name will have no number: spec 02 question 18), a preview of the new file name, a target-exists check supplied by the caller, and a result list (updated files, files that failed with the reason)

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

- Rename's search area (the CAD folders from Settings: the work folder and the other CAD folders) is Aidan's (spec 02 question 23). Skipping `OldVersions`, adding the renamed file's own folder when it lies in none of them, and updating the register entry are Defaults.
- Rename cannot be undone with one Undo; the confirmation says so.

## Verification

Filled in by the implementer.

- **Ran:** cloud session (`CLAUDE_CODE_REMOTE=true`).
  - `dotnet build src/InventorAddin.Core`: succeeded, 0 warnings, 0 errors.
  - `dotnet test tests/InventorAddin.Core.Tests`: 1796 passed, 0 failed (new: `RenamePlanTests`, `PartRenamerTests`, `RenameViewModelTests`, the Rename tests in `PartPropertiesViewModelTests`, the UpdateEntry tests in `RegisterStoreTests`, the TryUpdate tests in `PartRegisterTests`). The XAML style-rule test covers the new and changed windows.
  - `src/InventorAddin` was not built: it needs the Inventor interop assembly.
- **Not compiled (changed under `src/InventorAddin`):** `Commands/RenameService.cs` (new), `Commands/PartPropertiesCommand.cs`, `UI/RenameWindow.xaml` and `.xaml.cs` (new), `UI/PartPropertiesWindow.xaml`.
- **Inventor API members not confirmed:**
  - `SilentOperation` is set only around `Documents.Open(path, false)` for a closed file and restored in `finally`, as spike 032 did. The old value is read first; the property is documented read/write.
  - `foreach (Document d in InventorHost.App.Documents)`: the spike used `Documents.Cast<Document>()`, which needs the same `IEnumerable`; expected to compile.
  - Confirmed by spike 032 or existing add-in code, compiled there: `Document.SaveAs(string, bool)`, `Document.Save()`, `Document.Close(true)`, `Document.Dirty`, `Document.FullFileName`, `Documents.Open(path, false)`, `Document.File.ReferencedFileDescriptors`, `FileDescriptor.FullFileName`, `FileDescriptor.ReplaceReference(string)`.
  - Not seen in Inventor: what `Documents.Open` does with a plain AutoCAD `.dwg` (not an Inventor drawing) in a CAD folder. If it throws, that file is listed as not updated and the old file is kept (checklist 12).
- **Manual checklist for Inventor:** set Settings, Folders: Work folder to a scratch folder, and Other folders to a second scratch folder.
  1. A test part used by a closed assembly and by a closed drawing, all in the work folder. Open the part, then Part Properties. A **Part name** row sits under Part number, with a **Rename** button (Alt+R). Rename to a new name. The confirmation names both files and the count of files to search, lists the folders, and says Undo cannot revert it. Answer Yes. The file is renamed with the number kept, and the result list shows the assembly and drawing as updated. Part Properties' header, subline and Part name show the new name. Description is the new name in Inventor's iProperties. The old file is gone from disk. Open the assembly and drawing: no missing-reference prompt, and they show the renamed part.
  2. The register row (`part-register.csv` in the register folder) has the new path and name, in the same place, with every other value unchanged.
  3. Same as 1, with the assembly **open** in Inventor (no unsaved changes) and the drawing in the second scratch folder. The assembly is saved and listed as updated, and the drawing is updated too.
  4. Same as 3, but make a change to the open assembly first and do not save it. Rename stops before anything changes, and names the assembly to save or close.
  5. Type a name that already exists in the folder: the footer says so and Rename stays disabled before anything changes. Type the current name in different case: refused as the same file.
  6. An old file without a number and with no Part Number (`old_bracket.ipt`): renamed to just the new name (`new_bracket.ipt`). Typing a comma is refused.
  6a. An old file given a number with Generate (name still `old_bracket.ipt`, Part Number `AWB-1xxxx` in the register): the preview shows `AWB-1xxxx,new_bracket.ipt`, and keeping the same name is allowed (it adds the number). A comma is allowed. The register row gets the new path.
  6b. A file named `AWB-1xxxx,plate.ipt` whose Part Number is blank: the new name keeps `AWB-1xxxx,`. No register row is changed, because the Part Number is not registered.
  7. With the register folder unreachable (Tailscale off), rename a numbered file. The result says the register entry was updated offline. `pending-updates.json` appears in `%APPDATA%\AwbAddin\register`. Reconnect, use Generate on another file (or any register sync): the shared register row now has the new path and the pending updates file is gone.
  8. Make one referencing drawing read-only on disk, then rename. That drawing is listed as not updated with the reason. The old file is kept, and the drawing still opens.
  9. Watch for dialogs while files open invisibly, and for the progress line in the footer while files are checked. During the run, Esc and Cancel do nothing.
  10. Rename, then Generate part number on a file without one: the register row names the new path.
  11. Edit a part in place inside an assembly and open Part Properties: if the assembly has unsaved changes, Rename stops and names it.
  12. Put a plain AutoCAD `.dwg` (made in AutoCAD, not an Inventor drawing) in the work folder, then rename a part. Watch for any dialog when it is opened invisibly. If it is listed as not updated with a reason, the old file is kept: note the reason for a follow-up (skipping non-Inventor `.dwg` files).
  13. Add an unreachable folder to Other folders (for example a mapped drive letter that is disconnected, or a path on the NAS with Tailscale off), then rename. The confirmation lists it under "Not searched … and the old file will be kept", after a wait of about 3 s. Answer Yes. Files in the reachable folders are updated, the result list says the old file was kept and names the skipped folder, and the old file is still on disk.
  14. Register lock busy: create `part-register.lock` in the register folder by hand, then rename a numbered file. The result says the register entry could not be written and is kept on this PC for the next sync, and `pending-updates.json` appears. Delete the lock file and Generate on another file (or any sync): the row now has the new path.

## Follow-ups

Things noticed but not done.

- **Resolved (parent decision, 2026-10-10): the number in the new file name** follows spec 02: the registered Part Number, otherwise the number in the old file name, otherwise none (`RenamePlan.NumberToKeep`). The comma rule follows the result. Log it in `DECISIONS.md`. The brief's criterion and Notes were updated to match.
- **Defaults to log in `DECISIONS.md`:**
  - Offline register updates wait in `%APPDATA%\AwbAddin\register\pending-updates.json`. It is JSON, not CSV, because it holds the old path, which picks the row when a number was handed out twice. An unreadable updates file is refused, as for question 19, rather than dropped. Spec 02's location table should list it.
  - `UpdateEntry` changes the row whose path is the file's old path. Failing that, it changes the first row holding the number.
  - A name that differs only in case from the current one is refused as the same file.
  - Open documents that use the file and have unsaved changes stop the rename before anything changes, because Rename saves each file that uses it.
  - Folder timeouts: 3 s to answer (as the register), 60 s to list.
  - (Parent decision, Default) The old file is kept whenever a search folder was skipped (not reachable, listing timed out or failed), not only when a file failed to update. The confirmation and the result list say so.
  - When the online register write fails (lock busy, a register file not readable in full, an I/O error), the update is queued in `pending-updates.json` like an offline one and applied at the next sync. `PartRegister.TryUpdate` matches a row already holding the new path first, so an update applied twice changes nothing more.
- **Description after Rename (review item 4), checked:** Part Properties has no Description field. Its only Description write is the pre-fill that sets Description to the part name, recomputed from the file name. After Rename the snapshot holds the new path, and the new name when the rename wrote it, so Apply never writes the old name back. Tests cover a pending Description pre-fill followed by Rename and Apply.
- **Spec 08** ("The three windows: Part Properties") does not list the Part name row. Add it: under Part number, the part name or "Not saved yet", with the Rename button. Spec 07 already lists the field.
- **Rename window owner:** `WindowHost.ShowDialog` makes Inventor's main window the owner, so the Rename window is not owned by Part Properties. It opens centred on Inventor. An owner-window overload in `WindowHost` would centre it on Part Properties.
- **Result list:** shown in the shared four-line `MultiLineTextBox`, which scrolls with the mouse wheel but has no visible scroll bar. A list style in `Styles.xaml` would suit a long result better.
