# 206: Stock Size command

- **Milestone:** M5
- **Feature spec:** `docs/features/05-drawing-tools.md` ("Added for M5: New tools", item 5)
- **Status:** done (Core tests pass; the add-in project built on Windows, 0 errors; not run in Inventor)
- **Depends on:** 184, 187, 191, 205
- **Needs:** windows build
- **Parallel-safe with:** none

## Goal

A part button that reads the part's box, shows the Stock Size window and writes Material Size, which the title block shows.

## Scope

- `src/InventorAddin/UI/StockSizeWindow.xaml(.cs)`; `src/InventorAddin/Commands/StockSizeCommand.cs` + ribbon button (part); the box as spike 184 question 6 chose; one transaction.

## Acceptance criteria

- [ ] A block part gets "PL ..." in Material Size; the print's MATERIAL SIZE field shows it.
- [ ] One Undo removes it.
- [ ] `dotnet build InventorAddin.slnx -p:DeployToInventor=false` has 0 errors; every Inventor API member used is confirmed in the spike results or existing code, or listed under Verification

## Notes for the implementer

- Confirmed calls (spike 184, round 1): use `PartComponentDefinition.OrientedMinimumRangeBox`, the lengths of `DirectionOne`, `DirectionTwo` and `DirectionThree` in cm. On a bar rotated 30° in its sketch it gave the true 10 x 2 x 1 cm, where `RangeBox` gave 9.66 x 6.73 x 1. On an axis-aligned part the two agree.

- Read spec 05's "Added for M5" section and the 2026-11-21 rows of `DECISIONS.md` first. Defaults are marked there; do not change them.

## Defaults

- A sheet metal part is declined, not written (task 205 follow-up; spec 05 question 42): Material Size for it is the flat pattern's (task 230) and the computed value replaces a hand-typed one, so a "PL ..." text would be overwritten at the next refresh. The command shows `StockSizeEligibility.SheetMetal` (or `SheetMetalWithoutFlatPattern`, which says to create the flat pattern) and writes nothing. The rule and the texts are Core's (`StockSizeEligibility.DeclineReason`), tested.
- The button is small, on the Properties panel after Material Finish, on the part ribbon only. Command type `kFilePropertyEditCmdType` (it writes one iProperty, as Part Properties). It targets the edited document (`CommandDocument.Edited`).
- The box is the three lengths of `PartComponentDefinition.OrientedMinimumRangeBox` (`DirectionOne/Two/Three.Length`), each read with `ComSafe.Get`; if any fails the window opens blank with Core's "could not be read" message and the user types the size.
- The window is outside the transaction (as Part Marking); the single property write in `Execute` is the one transaction, so one Undo removes it. Cancel, or OK with no change (blank text, or text equal to the stored one), opens no transaction.
- Shape is a dropdown (Plate, Round bar), not radio buttons: the shared theme has no RadioButton style. The window shows the stored Material Size ("Now"), the problem for the worked-out size (red, stays while a typed size stands), and the workbook's problems (muted).
- The part's units come from `MaterialFinishTargets.UnitsOf(doc)` (already a `PrintUnits`), so `PrintPlan.PrintUnitsFor` is not needed.

## Verification

- **Ran:** `dotnet build InventorAddin.slnx -p:DeployToInventor=false` on Windows: 0 errors. `dotnet test tests/InventorAddin.Core.Tests`: 5817 passed, 0 failed on the second run (the first run had 2 failures, NewPartCreator and one other of the known flaky set, which passed on the rerun). New tests: `StockSizeEligibilityTests` (3), plus the ribbon layout, icon and command-document tests extended for Stock Size.
- **Not compiled (changed under `src/InventorAddin`):** none; all built on Windows. Not run in Inventor: `Commands/StockSizeCommand.cs`, `UI/StockSizeWindow.xaml(.cs)`, the button in `UI/RibbonSetup.cs`.
- **Inventor API members not confirmed:** none by running; all checked by reflection on `Autodesk.Inventor.Interop.dll` (2026): `PartComponentDefinition.OrientedMinimumRangeBox` is an `OrientedBox` with `DirectionOne/Two/Three` as `Vector`, which has `Length`; `SheetMetalComponentDefinition.HasFlatPattern` (also used in `SheetMetalSizeReader`). Spike 184 question 6 confirmed the box values in Inventor. No COM probe was run for this task.
- **Manual checklist for Inventor:** (copied into `docs/checklists/m5.md` by the parent for the combined check, task 209)
  1. Open a block part (a plain extrusion, for example 61 x 123 x 10.2 mm, metric units). On the Part ribbon, Properties panel, a small Stock Size button sits under Material Finish. Click it.
  2. The Stock Size window opens with Shape Plate and a text such as "PL 16 x 65 x 125" (per the stock workbook), the box focused. "Now" is blank.
  3. Click OK. In Part Properties (or iProperties, Custom) Material Size reads that text.
  4. Press Undo once in Inventor: Material Size is gone again. (The Undo list shows "Stock Size".)
  5. Run it again and change Shape to Round bar: the text becomes "Ø.. x ..". Edit the text by hand to "PL 20 x 70 x 130", OK: Material Size holds exactly that text.
  6. Run it a third time and OK without changing anything: nothing is written (no new Undo step).
  7. Open a part modelled off-axis (a bar rotated in its sketch): the size is the true bar, not the axis-aligned box.
  8. Open a part larger than the biggest thickness in the workbook: the window opens with a blank text and a red message; OK is disabled until a size is typed.
  9. Open a sheet metal part (with and without a flat pattern): a message says Material Size comes from the flat pattern; nothing is written.
  10. Open a drawing or assembly: the button is not on the ribbon. Make a print of the block part (Create Print): the title block's MATERIAL SIZE field shows the text.
  11. Switch the part to inches (imperial) and run it: the text is imperial, for example "PL 0.5 x 2.5 x 5".
  12. Point Settings, Stock data, at a missing file: the window still opens (blank text) and shows the workbook's problem in muted text.

## Follow-ups

- Spec 05 question 42 (sheet metal parts declined) is open for Aidan.
- A sheet metal part without a flat pattern gets no Material Size from either command until the flat pattern exists; the message says so.
