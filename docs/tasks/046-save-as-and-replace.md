# 046: Save As and Replace

- **Milestone:** M2
- **Feature spec:** `docs/features/02-part-creation-and-numbering.md` (Commands: Save As and Replace)
- **Status:** todo
- **Depends on:** 032 (its Results must be filled in), 045
- **Needs:** cloud, add-in not compiled
- **Parallel-safe with:** none (shares the ribbon files)

## Goal

From inside an assembly, a Save As and Replace button copies the selected component under a new number and replaces that occurrence with the copy.

## Before starting

Read the Results of task 032, question 7. If they are empty, stop.

## Scope

- `src/InventorAddin/Commands/SaveAsReplaceCommand.cs` (new), reusing the Save As Copy window and view-model
- `src/InventorAddin/UI/RibbonSetup.cs`, `src/InventorAddin.Core/Ribbon/RibbonModels.cs`, `RibbonLayout.cs` and its tests

## Acceptance criteria

- [ ] A small **Save As and Replace** button on the CAD Automation panel of the Assembly ribbon, after Save As Copy, with the `SaveAsReplace` icon
- [ ] Needs exactly one component occurrence selected in the active assembly (`doc.SelectSet`, 1-based; a `ComponentOccurrence`, which a `ComponentOccurrenceProxy` inside a sub-assembly also is); otherwise `"Select one component in the assembly first."`
- [ ] Opens the Save As Copy dialog for the selected component's file; on Create makes the copy as 045 does (in the component's folder), then replaces **only the selected occurrence** with `occurrence.Replace(copyPath, false)` (spike 032: constraints are kept). The copy is not opened in its own window
- [ ] A top-level occurrence: the replace runs in one transaction on the assembly, so one Undo puts the original back
- [ ] An occurrence inside a sub-assembly (Aidan, spec 02 question 25): before Create, the dialog says the sub-assembly (named) will change and be saved, so every assembly that uses it changes and Undo cannot put it back. The replace edits the sub-assembly (spike 032), which is then saved. A read-only sub-assembly stops with a message before the copy is made
- [ ] A read-only or library component stops with a message
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- Added after spike 032 (question 7): a selection inside a sub-assembly is a `ComponentOccurrenceProxy` (`Type` = `kComponentOccurrenceProxyObject`); `proxy.ParentOccurrence.Name` names the sub-assembly occurrence, and `proxy.Replace(path, false)` marks the sub-assembly document dirty, not the top assembly. The sub-assembly document to save is the one that holds `proxy.NativeObject`: reach it through the native occurrence's parent definition (`((ComponentOccurrence)proxy.NativeObject).Parent` is an `AssemblyComponentDefinition`, whose `Document` is the sub-assembly). These two members were not run in the spike: list them under members not confirmed. Deeper nesting works the same way, since `NativeObject` is the occurrence in its own assembly.
- `Replace(path, true)` replaces every occurrence of that file; do not use it (question 16 default).

- Replacing only the selected occurrence is a Default (spec 02, question 16).

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. In an assembly with two occurrences of one part, select one, Save As and Replace: only that occurrence now uses the copy, and its constraints hold
  2. Undo once: the original is back in that place (the copy file stays on disk and in the register)
  3. Nothing selected: the message

## Follow-ups

Things noticed but not done.
