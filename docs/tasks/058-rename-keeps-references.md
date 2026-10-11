# 058: Rename never breaks a reference, and renames occurrences

- **Milestone:** M2 (fix run after run 2)
- **Feature spec:** `docs/features/02-part-creation-and-numbering.md` ("Commands: Rename")
- **Status:** todo
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

- [ ] **Every file that uses the renamed file is saved with the new reference.** In `InventorRenameTarget`, a file found in the search, open or closed, counts as using the renamed file when any of its direct references is the old path **or** the new path (an open document repointed by the `SaveAs`). Old references are replaced with `ReplaceReference`; then `Dirty = true` is set and the document saved; a closed one is closed with `Close(true)` afterwards. `UpdateReferences` returns true for such a file even when nothing needed replacing
- [ ] **Open documents outside the search** that reference the new path after the `SaveAs` (every document in `Documents`, visible or not; skipping never-saved ones and the renamed one) are also marked dirty and saved, without needing `Dirty` to be true first. A file already handled in the search is not saved twice
- [ ] **Occurrences:** a Core rule, for example `RenamePlan.RenamedOccurrence(string occurrenceName, string oldFullFileName, string newFullFileName)`, returns the new name when the occurrence name is the old file name without extension, a colon and one or more digits (ignoring case), keeping the digits: `BRACKET,AWB-10005:2` becomes `BRACKET_2,AWB-10005:2`. Otherwise it returns null. Tested, including names with commas, a different number of digits, a near miss (`BRACKET,AWB-10005:2a`, `BRACKET,AWB-10005 copy:1`) and case
- [ ] In each assembly the target updates or saves, each top-level occurrence (`AssemblyComponentDefinition.Occurrences`) whose referenced file is the new path (`ComponentOccurrence.ReferencedDocumentDescriptor.FullDocumentName`, compared ignoring case) and whose name the rule renames gets `ComponentOccurrence.Name` set. A failure to rename one occurrence is logged and reported as a note, and does not stop the rename. The result says how many occurrences were renamed
- [ ] **The old file is deleted only when nothing still uses it.** Before deleting, `PartRenamer` asks the target for the open documents (visible or not) that still reference the old path; if there are any, the old file is kept and the result names them. This is in addition to the existing rules (a failed file or a skipped folder keeps it). Tested in Core with a fake target
- [ ] `PartRenamer` tests cover: an open document repointed but not dirty is saved (the fake target models the spike's behaviour); a closed file with the old reference is updated; a document still referencing the old path keeps the old file; occurrence renames counted
- [ ] The log line at the end says how many files were updated, how many occurrences were renamed, and whether the old file was deleted
- [ ] `dotnet build InventorAddin.slnx -tl:off -p:DeployToInventor=false` succeeds and `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- Read spike 052's Results first (`docs/tasks/052-spike-rename-copy-selection.md`). Members confirmed there: `Document.Dirty` (get and set), `Save`, `Close(bool)`, `ComponentOccurrence.Name` (set), `File.ReferencedFileDescriptors`, `FileDescriptor.FullFileName`, `ReplaceReference`. `ComponentOccurrence.ReferencedDocumentDescriptor.FullDocumentName` was confirmed by spike 032 (question 4).
- Set `Dirty = true` right before each `Save()`, after the occurrence renames, so the save always writes.
- The existing stop for open documents with unsaved changes stays: those would be saved with the user's changes.
- Keep the add-in thin: the occurrence-name rule, the decision to keep or delete the old file, and the result text are Core.

## Verification

Filled in by the implementer.

- **Ran:**
- **Changed under `src/InventorAddin` (compiled locally, not tested in Inventor):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. …

## Follow-ups

Things noticed but not done.
