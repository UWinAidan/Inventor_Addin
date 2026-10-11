# 060: Save As and Replace finds the selection in a sub-assembly; copies check their number

- **Milestone:** M2 (fix run after run 2)
- **Feature spec:** `docs/features/02-part-creation-and-numbering.md` ("Commands: Save As Copy"; "Commands: Save As and Replace")
- **Status:** done (compiled locally, not tested in Inventor)
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

- [x] `DocumentEditCommand` gets a protected virtual hook for the document a command works on, defaulting to `InventorHost.ActiveEditDocument` so every other command behaves as now. `SaveAsReplaceCommand` overrides it with `InventorHost.ActiveDocument` (the top-level assembly), so the kind check, the selection, the transaction and the replace all use the top-level assembly
- [x] With a sub-assembly edited in place, the selected part is read from the top-level assembly's `SelectSet` as a `ComponentOccurrenceProxy` and handled exactly as the not-in-place case (the sub-assembly is the proxy's parent's document, replaced inside it and saved, with the existing warning)
- [x] When the selection is not one component, the command logs what it found (the count and each item's `Type`), so a future report can be diagnosed from `awbaddin.log`
- [x] `ICopiedDocument` gets a read of the copy's Part Number. After `Save()`, `SaveAsCopyCreator` reads it back; if it is not the number just assigned, the step fails like any other failure after assignment (the copy is closed and removed as now, the number stays in the register) with a message saying the copy did not keep its new number. Tested with a fake that returns the wrong number
- [x] The success log line names the number read back
- [x] Save As and Replace goes through the same check (it shares `SaveAsCopyCreator`'s copy step, or calls the same check). Tested
- [x] `dotnet build InventorAddin.slnx -tl:off -p:DeployToInventor=false` succeeds and `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- Read spike 052's Results, sections 3 and 4, first. Confirmed there: `Application.ActiveDocument`, `ActiveEditDocument`, `Document.SelectSet`, `SelectSet.Count`, `SelectSet[int]`, `ComponentOccurrenceProxy`, `ComponentOccurrence.ParentOccurrence`.
- Reading an Inventor object's `Type` for the log: the existing code reads typed members; `ObjectTypeEnum` comes from the item's `Type` property, which every Inventor object has. If you cannot read it without `dynamic`, log the .NET type name (`Microsoft.VisualBasic.Information.TypeName` gave `ComponentOccurrenceProxy` in the spike) and say so in Verification.
- A transaction on the top-level assembly while a sub-assembly is edited in place has not been tried; list it in the manual checklist.

## Verification

Filled in by the implementer.

- **Ran:** on Aidan's PC (local, not cloud). `dotnet build InventorAddin.slnx -tl:off -p:DeployToInventor=false`: succeeded, 0 errors, the same 4 existing CS8600 warnings (CreatePrintCommand, SaveAsReplaceCommand cursor line, SaveAsCopyCommand, NewPartCommand). `dotnet test tests/InventorAddin.Core.Tests`: 2115 passed, 0 failed (baseline 2101; 14 new). No timing-dependent tests were added.
- **Changed under `src/InventorAddin` (compiled locally, not tested in Inventor):**
  - `Commands/DocumentEditCommand.cs`: new `protected virtual Document? DocumentToEdit`, default `InventorHost.ActiveEditDocument`; `Execute()` uses it for the null check, the kind check and `Run`.
  - `Commands/SaveAsReplaceCommand.cs`: overrides `DocumentToEdit` with `InventorHost.ActiveDocument`, so the kind check, the selection, the edit-block read and the replace transaction use the top-level assembly. When the selection is not one component it logs `SaveAsReplacer.SelectionNotOneComponentLog` (count and each item's type).
  - `Commands/InventorNewDocument.cs`: `ReadPartNumber()` through `PropertyReader.GetString` (Design Tracking, "Part Number"); an unreadable value reads as null, so the copy fails as "blank".
- **Core:** `ICopiedDocument.ReadPartNumber()`; `SaveAsCopyCreator` reads it back after `Save()` and fails through the usual after-assignment path (copy closed and deleted, row stays) with `FailedAfterAssignMessage(number, NumberNotKeptReason(number, readBack))`, e.g. "Part number AWB-10002 was assigned, but the copy could not be made: The copy did not keep its new number AWB-10002: its Part Number reads 'AWB-10001' after saving. The number stays in the register." `KeptNumber` compares trimmed, ordinal. The success log ends "its Part Number reads back as {number}". `SaveAsReplacer` goes through `SaveAsCopyCreator.Copy`, so it gets the same check (tested top-level and nested: nothing is replaced or saved).
- **Inventor API members not confirmed:** none new. Used: `Application.ActiveDocument`, `ActiveEditDocument`, `Document.SelectSet`, `SelectSet.Count`, `SelectSet[int]`, `ComponentOccurrenceProxy`, property read via `PropertySets[set][name].Value` (all already used or confirmed in spike 052). The item's type is logged with `Microsoft.VisualBasic.Information.TypeName(item)` (the COM type name, e.g. `ComponentOccurrenceProxy`), not the `ObjectTypeEnum` from `Type`, because `SelectSet[int]` returns `object` and reading `Type` would need `dynamic`. Not tried: a transaction on the top-level assembly while a sub-assembly is edited in place.
- **Manual checklist for Inventor:**
  1. Open a top-level assembly holding a sub-assembly that holds a saved part (like spike 052's `ASSY_TOP` / `ASSY_SUB` / `PIN`). Double-click the sub-assembly in the browser to edit it in place, then pick the part (in the browser or the canvas). Click **Save As and Replace**. Expect the dialog (not "Select one component"), with the warning naming the sub-assembly.
  2. Enter a name and OK. Expect: the copy is made with the next number, the part is replaced in the sub-assembly, the sub-assembly is saved, and no error. Check that no error about a transaction appears (this is the untried case), and that edit-in-place still behaves (you can return to the top level).
  3. Repeat without editing in place (pick the part under the sub-assembly from the top level): same result as before this task.
  4. Repeat with a top-level part selected: replaced in the top-level assembly; one Undo puts the original occurrence back.
  5. With nothing selected (and with a face or two components selected), click **Save As and Replace**. Expect "Select one component in the assembly first." In `%APPDATA%\AwbAddin\awbaddin.log`, expect a line like "Save As and Replace: the selection holds 2 items: ComponentOccurrence, ComponentOccurrence."
  6. **Save As Copy** on a part: the copy opens with its new number in Part Properties; the log line ends "its Part Number reads back as AWB-xxxxx" with the same number.
  7. Run a part command (for example Part Properties) while a part is edited in place in an assembly: it still acts on the part being edited (the default `DocumentToEdit` is unchanged).
  8. New case (reviewer): edit a **part** in place inside an assembly, select an occurrence in the browser (try the very occurrence being edited, then another one), and click **Save As and Replace**. Before this task it said it works in an assembly; now it runs on the top-level assembly. Check the replace works or fails with a clear message, and nothing is left half done.

## Follow-ups

Things noticed but not done.

- `InventorNewDocument.ReadPartNumber` cannot tell an unreadable Part Number from a blank one (`PropertyReader.GetString` returns null for both), so a read failure is reported as "blank after saving". The exception is not logged separately.
