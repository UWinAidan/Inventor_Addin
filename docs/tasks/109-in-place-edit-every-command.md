# 109: Every AWB command works in a part edited in place

- **Milestone:** M4 part 1
- **Feature spec:** specs 02, 03 and 07 (each command's own section); `docs/features/09-awb-hole.md` ("The command")
- **Status:** done
- **Depends on:** 103
- **Needs:** windows build
- **Parallel-safe with:** 105 (disjoint files)

## Goal

Every AWB tool must work while a part is edited in place, not only AWB Hole (Aidan, 2026-10-24). Each command acts on the document being edited: the part, or the sub-assembly, edited in place. Its window fills from that document, and one Undo in the assembly window reverts any change it made.

Spike 103 found that Material and Finish and Part Properties already do this. In the code, Create Print and Save As Copy use `ActiveDocument`, so in place they act on the assembly. This task makes the choice of document one Core rule for every command, moves those two commands onto it, and records each other command as checked.

## Scope

- `src/InventorAddin.Core/Ribbon/CommandDocuments.cs` (new), with tests in `tests/InventorAddin.Core.Tests/Ribbon/CommandDocumentsTests.cs`:
  - `enum CommandDocument { Edited, TopLevel, None }`.
  - `static CommandDocument For(string commandName)`, from the `CommandNames` constants:
    - **Edited** (`ActiveEditDocument`): Part Properties, Material and Finish, Create Print, Save As Copy, Export Model Data, AWB Hole and AWB Hole Edit.
    - **TopLevel** (`ActiveDocument`): Save As and Replace, which reads the top-level assembly's selection by design (task 060, spike 052). Also New Part, which only takes the folder of the document open in the window (Default: unchanged).
    - **None**: Settings, About, Mass Update (a folder) and Export Libraries.
  - An unknown name gives `Edited`.
  - A test lists every constant in `CommandNames` (by reflection), so a new command must be given its row.
  - Material and Finish keeps its own `MaterialFinishTargeting` (task 075). It is listed as `Edited` because the part being edited is its target.
- `src/InventorAddin/Commands/CreatePrintCommand.cs`: the model is `ActiveEditDocument`, through the rule. In place it prints the part being edited, or the sub-assembly being edited. This folds in the task 044 follow-up ("prints the assembly while a part is edited in place").
- `src/InventorAddin/Commands/SaveAsCopyCommand.cs`: the original is `ActiveEditDocument`, through the rule. In place it copies the part being edited, and the copy opens in its own window as today. The in-place edit is not ended.
- `src/InventorAddin/Commands/NewPartCommand.cs`, `ExportDataCommands.cs`, `DocumentEditCommand.cs`, `SaveAsReplaceCommand.cs`, `PartPropertiesCommand.cs`: these already use the document the rule gives. Each reads it through `CommandDocuments.For(InternalName)`, through one small helper on `AddinCommand` that returns the `Document?`, so the rule is the only place the choice is made.
  - `PartPropertiesCommand`'s transaction stays on `ActiveDocument`: one Undo in the assembly window must revert it (task 006). That is the transaction document, not the target, and the rule does not change it.
  - `AwbHoleCommand` is changed by task 105. Leave it alone here.
- `docs/features/02-part-creation-and-numbering.md`, Create Print and Save As Copy: one line each. "While a part or sub-assembly is edited in place, it acts on that part or sub-assembly (Default, spike 103)."
- `docs/features/07-part-properties.md`: no change. It already acts on the document being edited.

Out of scope:

- AWB Hole (task 105) and the mini toolbar (task 133).
- Rename (task 134 changes Save As and Replace only).
- Mass Update's folder choice.

## Acceptance criteria

- [ ] `CommandDocuments.For` gives the rows above, and the reflection test fails for a `CommandNames` constant with no explicit row.
- [ ] With a part edited in place, Create Print makes and opens the part's print (in the part's `Prints` folder), not the assembly's.
- [ ] With a part edited in place, Save As Copy copies the part, and its dialog is pre-filled from the part.
- [ ] Every command in the table opens and acts on the document its row says, in place and in a sub-assembly edited in place.
- [ ] Out of place nothing changes: `ActiveEditDocument` is `ActiveDocument` there.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes, and the solution builds on Windows.

## Notes for the implementer

- Spike 103's Results hold the audit (`docs/tasks/103-spike-in-place-edit.md`, "The audit"). The spike log only showed Material and Finish, Part Properties and About run in place. The rest are on the checklist below.
- `InventorHost.ActiveEditDocument` and `ActiveDocument` already exist. In a part's own window they are the same document.
- Create Print's existing checks (part or assembly, saved, template) and its messages are unchanged. They now run on the edited document.
- The rule is plain data: no `if` on document kinds in the add-in for it.

## Verification

Filled in by the implementer.

- **Ran:** on Windows, `dotnet build InventorAddin.slnx -tl:off -p:DeployToInventor=false`: build succeeded, 0 errors, 8 warnings (all CS8600 on the existing `Forms.Cursor previousCursor = Forms.Cursor.Current;` lines in Create Print, New Part, Save As Copy and Save As and Replace, untouched by this task). `dotnet test tests/InventorAddin.Core.Tests`: 3617 passed, 0 failed, 0 skipped.
- **Not compiled (changed under `src/InventorAddin`):** none. Everything changed was built on Windows against Inventor 2026. Not yet run in Inventor.
- **Inventor API members not confirmed:** none. The only members used are `Application.ActiveDocument` and `Application.ActiveEditDocument` through the existing `InventorHost` properties, and `Document.FullFileName`, all already in use.
- **What changed:** `CommandDocuments.For` (Core) holds every command's row. `AddinCommand.CommandTargetDocument()` maps the row to `ActiveEditDocument`, `ActiveDocument` or null, and is the only place the choice is made. `DocumentEditCommand.DocumentToEdit` now defaults to it, so Part Properties (Edited) and Save As and Replace (TopLevel; its `ActiveDocument` override removed) both read it. Create Print, Save As Copy, New Part and Export Model Data call it directly. Create Print and Save As Copy move from `ActiveDocument` to the edited document; the other commands act on the same document as before.
- **Manual checklist for Inventor:** for each step, edit one of your parts in place inside an assembly (double-click it), then:
  1. **Create Print**: the print made and opened is the part's, in the part's `Prints` folder.
  2. **Save As Copy**: the dialog shows the part's name, and the copy is of the part. The assembly is unchanged and still has the part in place.
  3. **New Part**: the new part opens on its own in the assembly's folder, and the in-place edit is not disturbed.
  4. **Save As and Replace**, with a component selected in the browser: it replaces that occurrence in the assembly, as it does outside an in-place edit.
  5. **Part Properties**: change Designer and apply. Return to the assembly and press Undo once: Designer reverts. This check has been open since task 006.
  6. **Material and Finish**: apply a finish, then Undo once in the assembly: it reverts.
  7. **Export Model Data**: it exports the part.
  8. **Mass Update, Settings, About**: they open and work.
  9. Repeat steps 1, 2, 5 and 7 with a **sub-assembly** edited in place: each acts on the sub-assembly.

## Follow-ups

- **Part Properties' transaction is not on `ActiveDocument`.** The brief says it "stays on `ActiveDocument`". In the code, only Change beside Material starts its transaction on `ActiveDocument`. The property writes (`TransactionWriteTarget`) and Generate run `RunInTransaction` on the edited document, the part while it is edited in place. This task left that unchanged. If checklist step 5 (one Undo in the assembly window reverts Designer) fails, that is the likely cause: start those transactions on `ActiveDocument`, as Material and Finish does (spike 067 section 4).
- **Material and Finish still has its own `ActiveDocument` override** of `DocumentToEdit` (in `MaterialFinishCommand.cs`, not in this task's files). It is the transaction and selection document; the target comes from `MaterialFinishTargeting`, so behaviour matches its `Edited` row. It also reads `ActiveEditDocument` directly. A later task could make it read the TopLevel document through a named helper, so no command bypasses the rule by hand.
- `AwbHoleCommand` and `HoleEditRedirect` read `ActiveEditDocument` directly (task 105 files, left alone). They could switch to `CommandTargetDocument()` when next touched.
