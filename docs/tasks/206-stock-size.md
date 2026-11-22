# 206: Stock Size command

- **Milestone:** M5
- **Feature spec:** `docs/features/05-drawing-tools.md` ("Added for M5: New tools", item 5)
- **Status:** todo
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

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:** (copied into `docs/checklists/m5.md` by the parent for the combined check, task 209)
  1. …

## Follow-ups

Things noticed but not done.
