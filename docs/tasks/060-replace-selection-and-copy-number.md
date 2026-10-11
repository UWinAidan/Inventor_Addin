# 060: Save As and Replace finds the selection in a sub-assembly; copies check their number

- **Milestone:** M2 (fix run after run 2)
- **Feature spec:** `docs/features/02-part-creation-and-numbering.md` ("Commands: Save As Copy"; "Commands: Save As and Replace")
- **Status:** todo
- **Depends on:** 052, 055
- **Needs:** windows build (local session); Core tested
- **Parallel-safe with:** none

## Goal

Fix bugs 4 and 6 from Aidan's run 2 checks.

- **Bug 4.** Save As and Replace on a part selected inside a sub-assembly said "Select one component". Spike 052 found that while a sub-assembly is edited in place, `ActiveEditDocument` is the sub-assembly and its `SelectSet` is empty; the selection is in `ActiveDocument.SelectSet` (the top-level assembly), as a `ComponentOccurrenceProxy`. `DocumentEditCommand` hands commands `ActiveEditDocument`, so the command looked in the empty set. Picking the part in the browser without editing in place already works.
- **Bug 6.** Aidan saw a copy without its new part number. Spike 052 found every copy from his checks has its own number on disk, so the cause was not found (task 053 fixes the copy's pre-filled name, which may be what he saw). Each copy must get its own number, so the add-in now reads the copy's Part Number back after saving it and stops with an error if it is wrong.

## Scope

- `src/InventorAddin/Commands/DocumentEditCommand.cs` (a way for a command to use the top-level document)
- `src/InventorAddin/Commands/SaveAsReplaceCommand.cs`
- `src/InventorAddin/Commands/InventorNewDocument.cs` (read the Part Number back)
- `src/InventorAddin.Core/Numbering/SaveAsCopyCreator.cs` (`ICopiedDocument`, the read-back step, the message), and `SaveAsReplacer.cs` if it does not go through the same step
- The matching tests under `tests/InventorAddin.Core.Tests/`

Out of scope:

- Replacing every occurrence (question 16 stays open).

## Acceptance criteria

- [ ] `DocumentEditCommand` gets a protected virtual hook for the document a command works on, defaulting to `InventorHost.ActiveEditDocument` so every other command behaves as now. `SaveAsReplaceCommand` overrides it with `InventorHost.ActiveDocument` (the top-level assembly), so the kind check, the selection, the transaction and the replace all use the top-level assembly
- [ ] With a sub-assembly edited in place, the selected part is read from the top-level assembly's `SelectSet` as a `ComponentOccurrenceProxy` and handled exactly as the not-in-place case (the sub-assembly is the proxy's parent's document, replaced inside it and saved, with the existing warning)
- [ ] When the selection is not one component, the command logs what it found (the count and each item's `Type`), so a future report can be diagnosed from `awbaddin.log`
- [ ] `ICopiedDocument` gets a read of the copy's Part Number. After `Save()`, `SaveAsCopyCreator` reads it back; if it is not the number just assigned, the step fails like any other failure after assignment (the copy is closed and removed as now, the number stays in the register) with a message saying the copy did not keep its new number. Tested with a fake that returns the wrong number
- [ ] The success log line names the number read back
- [ ] Save As and Replace goes through the same check (it shares `SaveAsCopyCreator`'s copy step, or calls the same check). Tested
- [ ] `dotnet build InventorAddin.slnx -tl:off -p:DeployToInventor=false` succeeds and `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- Read spike 052's Results, sections 3 and 4, first. Confirmed there: `Application.ActiveDocument`, `ActiveEditDocument`, `Document.SelectSet`, `SelectSet.Count`, `SelectSet[int]`, `ComponentOccurrenceProxy`, `ComponentOccurrence.ParentOccurrence`.
- Reading an Inventor object's `Type` for the log: the existing code reads typed members; `ObjectTypeEnum` comes from the item's `Type` property, which every Inventor object has. If you cannot read it without `dynamic`, log the .NET type name (`Microsoft.VisualBasic.Information.TypeName` gave `ComponentOccurrenceProxy` in the spike) and say so in Verification.
- A transaction on the top-level assembly while a sub-assembly is edited in place has not been tried; list it in the manual checklist.

## Verification

Filled in by the implementer.

- **Ran:**
- **Changed under `src/InventorAddin` (compiled locally, not tested in Inventor):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. …

## Follow-ups

Things noticed but not done.
