# 058: Rename never breaks a reference, and renames occurrences

- **Milestone:** M2 (fix run after run 2)
- **Feature spec:** `docs/features/02-part-creation-and-numbering.md` ("Commands: Rename")
- **Status:** done (Core tested; add-in compiled locally, not tested in Inventor)
- **Depends on:** 052, 053
- **Needs:** windows build (local session); Core tested
- **Parallel-safe with:** none

## Goal

Fix bugs 1 and 3 from Aidan's run 2 checks.

1. Rename broke a drawing's reference to the renamed part. Spike 052 found the cause: after `SaveAs(new, false)`, Inventor repoints open documents in memory but leaves them `Dirty = False`, and `Save()` writes nothing for a document that is not dirty. Rename saved only dirty documents, so every open drawing and assembly that used the part kept the old path on disk, and then the old file was deleted. A rename must never break a reference.
2. After Rename, assemblies still showed occurrences named after the old file. Rename now renames an occurrence of the renamed file whose name is the old file name, a colon and a number.

## Scope

- `src/InventorAddin.Core/Numbering/RenamePlan.cs` (`PartRenamer`, `IRenameTarget`, the occurrence-name rule, messages)
- `src/InventorAddin/Commands/RenameService.cs` (`InventorRenameTarget`)
- `src/InventorAddin.Core/ViewModels/RenameViewModel.cs` only if the result text needs it
- The matching tests under `tests/InventorAddin.Core.Tests/`

Out of scope:

- Renaming drawings (task 059).

## Acceptance criteria

- [x] **Every file that uses the renamed file is saved with the new reference.** In `InventorRenameTarget`, a file found in the search, open or closed, counts as using the renamed file when any of its direct references is the old path **or** the new path (an open document repointed by the `SaveAs`). Old references are replaced with `ReplaceReference`; then `Dirty = true` is set and the document saved; a closed one is closed with `Close(true)` afterwards. `UpdateReferences` returns true for such a file even when nothing needed replacing
- [x] **Open documents outside the search** that reference the new path after the `SaveAs` (every document in `Documents`, visible or not; skipping never-saved ones and the renamed one) are also marked dirty and saved, without needing `Dirty` to be true first. A file already handled in the search is not saved twice
- [x] **Occurrences:** a Core rule, for example `RenamePlan.RenamedOccurrence(string occurrenceName, string oldFullFileName, string newFullFileName)`, returns the new name when the occurrence name is the old file name without extension, a colon and one or more digits (ignoring case), keeping the digits: `BRACKET,AWB-10005:2` becomes `BRACKET_2,AWB-10005:2`. Otherwise it returns null. Tested, including names with commas, a different number of digits, a near miss (`BRACKET,AWB-10005:2a`, `BRACKET,AWB-10005 copy:1`) and case
- [x] In each assembly the target updates or saves, each top-level occurrence (`AssemblyComponentDefinition.Occurrences`) whose referenced file is the new path (`ComponentOccurrence.ReferencedDocumentDescriptor.FullDocumentName`, compared ignoring case) and whose name the rule renames gets `ComponentOccurrence.Name` set. A failure to rename one occurrence is logged and reported as a note, and does not stop the rename. The result says how many occurrences were renamed
- [x] **The old file is deleted only when nothing still uses it.** Before deleting, `PartRenamer` asks the target for the open documents (visible or not) that still reference the old path; if there are any, the old file is kept and the result names them. This is in addition to the existing rules (a failed file or a skipped folder keeps it). Tested in Core with a fake target
- [x] `PartRenamer` tests cover: an open document repointed but not dirty is saved (the fake target models the spike's behaviour); a closed file with the old reference is updated; a document still referencing the old path keeps the old file; occurrence renames counted
- [x] The log line at the end says how many files were updated, how many occurrences were renamed, and whether the old file was deleted
- [x] `dotnet build InventorAddin.slnx -tl:off -p:DeployToInventor=false` succeeds and `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- Read spike 052's Results first (`docs/tasks/052-spike-rename-copy-selection.md`). Members confirmed there: `Document.Dirty` (get and set), `Save`, `Close(bool)`, `ComponentOccurrence.Name` (set), `File.ReferencedFileDescriptors`, `FileDescriptor.FullFileName`, `ReplaceReference`. `ComponentOccurrence.ReferencedDocumentDescriptor.FullDocumentName` was confirmed by spike 032 (question 4).
- Set `Dirty = true` right before each `Save()`, after the occurrence renames, so the save always writes.
- The existing stop for open documents with unsaved changes stays: those would be saved with the user's changes.
- Keep the add-in thin: the occurrence-name rule, the decision to keep or delete the old file, and the result text are Core.

## Verification

Filled in by the implementer.

- **Ran:** `dotnet build InventorAddin.slnx -tl:off -p:DeployToInventor=false`: build succeeded, only the 4 existing CS8600 warnings (CreatePrintCommand, NewPartCommand, SaveAsCopyCommand, SaveAsReplaceCommand). `dotnet test tests/InventorAddin.Core.Tests`: 2055 passed, 0 failed (baseline 2015, plus 40 new test cases), first run, no timeouts. The new tests use a fake target and do not depend on timing.
- **Changed under `src/InventorAddin` (compiled locally, not tested in Inventor):** `Commands/RenameService.cs` (`InventorRenameTarget`). `UpdateReferences` and `SaveRepointedDocuments` share `UpdateAndSave`: a document uses the file when a direct reference (`doc.File.ReferencedFileDescriptors`) is the old or the new path; old ones get `ReplaceReference`; then `RenameOccurrences`; then `Dirty = true` and `Save()`. `SaveRepointedDocuments` now goes through every document in `Documents`, skipping never-saved ones, the renamed one and the files the search handled, and no longer needs `Dirty` first. It also picks up an open document outside the search that still names the old path (not only the new one), since updating it is safe and lets the old file go. New `OpenDocumentsReferencing(old)`; it fails safe: a document whose references cannot be read counts as still using the old file (logged), so the old file is kept. In `UpdateReferences`, the documents open before `Documents.Open` are recorded; if the returned document was already open (a path spelling `FindOpen` missed, such as a mapped drive against its UNC path) it is updated and saved but not closed (matched by the same RCW or by Inventor's `FullFileName`).
- **Core:** `RenamePlan.RenamedOccurrence(name, oldFile, newFile)` and `RenamePlan.FileOfDocumentName` (drops a trailing `<model state>` from `FullDocumentName` before comparing paths). `IRenameTarget.UpdateReferences` returns `RenameFileUpdate?` (null when the file does not use it) instead of `bool`; `SaveRepointedDocuments(old, new, alreadyHandled)`; new `OpenDocumentsReferencing(old)`. `RenameFileResult` carries `OccurrencesRenamed` and `OccurrenceProblems`; `RenameResult.OccurrencesRenamed`. `PartRenamer` turns each occurrence problem into a problem note (`OccurrenceNotRenamedMessage`), and before deleting asks the target for open documents still on the old path (`OldFileStillUsedMessage`; a failure to ask also keeps the file, `OldFileCheckFailedMessage`). It asks only when no file failed and no folder was skipped, since the file is kept anyway then. The last log line reads `... N updated, M not updated, K occurrences renamed, old file deleted|kept.` `RenameViewModel.Summary` adds ", N occurrence(s) renamed" when any were.
- **Inventor API members not confirmed:** none. Every member was confirmed by spike 052 or existing code: `Document.Dirty` set, `Save`, `Close(bool)`, `File.ReferencedFileDescriptors`, `FileDescriptor.FullFileName`, `ReplaceReference`, `ComponentOccurrence.Name` set (spike 052); `AssemblyDocument.ComponentDefinition.Occurrences`, `ComponentOccurrence.ReferencedDocumentDescriptor.FullDocumentName` (`Extraction/AssemblyExtractor.cs`, spike 032). Not confirmed by a test: whether an occurrence in a closed assembly reads the old or the new path right after `ReplaceReference`; the code accepts either.
- **Manual checklist for Inventor:**
  1. Close Inventor, build and deploy (`dotnet build InventorAddin.slnx`), start Inventor.
  2. In a scratch folder inside the work folder: a numbered part `BRACKET,AWB-1xxxx.ipt`, an assembly using it twice (occurrences `BRACKET,AWB-1xxxx:1` and `:2`) plus one occurrence you renamed by hand to `LEFT`, and its print in `Prints`. Save all.
  3. Open the part, the assembly and the print (all visible, all saved). Go to the part, open Part Properties, click Rename, type `BRACKET 2`, confirm. The result says the assembly and the drawing were updated and "2 occurrences renamed", and the old file is gone from Explorer.
  4. In the assembly browser, the occurrences read `BRACKET_2,AWB-1xxxx:1` and `:2`; `LEFT` is unchanged.
  5. Close everything without saving (nothing should ask to save). Reopen the assembly and the print: no missing-reference dialog, both show the renamed part, the occurrence names stuck.
  6. Repeat with the assembly and the print closed: same result.
  7. Repeat with the assembly open in a folder outside every CAD folder: it is listed as updated, and reopens clean.
  8. Sub-assembly loaded only through its parent: make `ASSY_SUB` that uses the part (occurrence named after the part file) and `ASSY_TOP` that uses `ASSY_SUB`. Save all and close all. Open only `ASSY_TOP` (so `ASSY_SUB` is loaded invisibly, not in its own window) and the part. Rename the part. The result lists `ASSY_SUB` as updated, with its occurrence renamed. Close everything without saving (nothing asks to save), then open `ASSY_SUB` on its own and `ASSY_TOP`: no missing-reference dialog, the part shows under its new name and the occurrence name stuck.
  9. Check `%APPDATA%\AwbAddin\awbaddin.log`: the last Rename line gives files updated, occurrences renamed and "old file deleted".

## Follow-ups

Things noticed but not done.

- Occurrences inside sub-assemblies are only renamed when the sub-assembly itself is a file Rename updates (each assembly renames its own top-level occurrences), which covers every assembly that uses the part directly. Nothing to do unless Aidan sees a case it misses.
- An occurrence whose new name is already taken in the assembly fails and is reported as a note; the add-in does not pick another number. Aidan's call if that should change.
- Spec 02's "Method" paragraph could mention that open documents outside the CAD folders are updated too, and that an open document still on the old path keeps the old file.
