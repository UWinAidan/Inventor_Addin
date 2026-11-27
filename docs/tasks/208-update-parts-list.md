# 208: Update Parts List command

- **Milestone:** M5
- **Feature spec:** `docs/features/05-drawing-tools.md` ("Added for M5: New tools", item 6)
- **Status:** done (built on Windows; not run in Inventor)
- **Depends on:** 184, 187, 191, 207
- **Needs:** windows build
- **Parallel-safe with:** none

## Goal

A drawing button that fills the Make/Buy cells of every parts list on the sheet; the manufacturer columns already read Vendor and Stock Number from the template's style.

## Scope

- `src/InventorAddin/Drawing/PartsListMakeBuyWriter.cs` (spike 184, question 7); `src/InventorAddin/Commands/UpdatePartsListCommand.cs` + ribbon button.

## Acceptance criteria

- [ ] An assembly print with an AWB part, a Content Center screw and a bought part from a library: Make, Buy, Buy.
- [ ] A parts list with no Make/Buy column gets one added.
- [ ] One Undo reverts.
- [ ] `dotnet build InventorAddin.slnx -p:DeployToInventor=false` has 0 errors; every Inventor API member used is confirmed in the spike results or existing code, or listed under Verification

## Notes for the implementer

- Confirmed calls (spike 184, round 1):
  - Cells: `PartsListRow.Item(columnIndex)` is the cell. Setting `Value` makes it `Static` (an override), which survives `Update()` and save and reopen. `SaveItemOverridesToBOM()` failed; do not use it.
  - Row documents: `PartsListRow.ReferencedFiles.Item(1)` gives `FullFileName` and `ReferencedDocument`.
  - Content Center: `ComponentDefinition.IsContentMember` is true for Content Center parts.
  - Library parts: `DesignProjectManager.ActiveDesignProject.LibraryPaths` lists them (empty in Aidan's Default project).
  - Columns: the template's parts list style already has MAKE/BUY (custom, `CustomPropertyName` "Make/Buy"), MANUFACTURER and MFR PART NO. (spike 183).

- Read spec 05's "Added for M5" section and the 2026-11-21 rows of `DECISIONS.md` first. Defaults are marked there; do not change them.

## Defaults

- The command works on the active sheet's parts lists (all of them), not other sheets.
- A row's part number is the referenced document's Design Tracking Part Number; with no document available, its file name without the extension. Content Center is `PartComponentDefinition.IsContentMember` of that document; library is `LibraryFolderRule.IsUnder` over `BatchPrintTreeReader.LibraryFolders`, read once per command.
- A blank decision keeps a typed value and creates no empty override, but clears a cell that reads exactly MAKE or BUY (case and spaces ignored), left by an earlier run (`ShouldClear`). A row with no referenced file counts as blank (and is cleared the same way).
- The Make/Buy column is found by custom property "Make/Buy", else by heading "MAKE/BUY". A missing one is added at the end as a custom-property column titled "MAKE/BUY".
- A cell already showing the right text is not rewritten.
- A sheet with no parts list gets a message and no transaction.

## Verification

- **Ran:** `dotnet build InventorAddin.slnx -p:DeployToInventor=false`: 0 errors. `dotnet test tests/InventorAddin.Core.Tests`: 5867 total; the last run passed all (earlier runs hit known flaky NewPartCreator and RenamePlan tests). New: `PartsListMakeBuyTests` (incl. `ShouldClear`, summary of failed lists vs cells); ribbon tests updated for the new button.
- **Not compiled (changed under `src/InventorAddin`):** none; all built on Windows. Not run in Inventor: `Commands/UpdatePartsListCommand.cs`, `Drawing/PartsListMakeBuyWriter.cs`, `UI/RibbonSetup.cs`.
- **Inventor API members not confirmed:** all exist in the interop (checked by reflection) and compile. Not confirmed at run time: `PartsListColumns.Add(kCustomProperty, PropertyIdentifier: "Make/Buy")` creating a working custom column, and setting `PartsListColumn.Title` after it; `ReferencedFileDescriptor.ReferencedDocument` returning the document for an unopened file.; also writing `""` to a cell (clearing a static override), and that omitting `TargetIndex` appends the column after the last one. A heading that fails to set is only a warning; the column still counts as added.
- **Manual checklist for Inventor:** (copied into `docs/checklists/m5.md` by the parent for the combined check, task 209)
  1. Open a drawing of an assembly that has an AWB-numbered part, a Content Center screw and a part from a project library folder, with a parts list from the AWB template (it has a MAKE/BUY column).
  2. Click Update Parts List on the drawing ribbon. The message counts Make 1, Buy 2; the cells read MAKE, BUY, BUY.
  3. Save, close and reopen the drawing: the cells still read the same.
  4. Click Undo once: all three cells revert.
  5. Place a parts list from a style with no Make/Buy column and run it: a MAKE/BUY column appears at the end and is filled; one Undo removes the column and the text.
  6. Type a value in the cell of a part that is neither AWB nor library, run again: the typed value stays.
  7. Run on a drawing with a MAKE cell, change that part's Part Number to a non-AWB value, run again: the cell is blank and the message says one cell was cleared.
  8. Run on a drawing with no parts list: the "place a parts list first" message, nothing changes.

## Follow-ups

- Other sheets' parts lists are not updated; add a choice if wanted.
- Rows that merge several files (a ReferencedFiles count above 1) use the first file only.
