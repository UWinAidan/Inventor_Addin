# 238: Fit long title block values to their cells

- **Milestone:** M5 (from spike 218, 2026-11-23)
- **Feature spec:** `docs/features/05-drawing-tools.md`
- **Status:** todo
- **Depends on:** 218, 237
- **Needs:** windows build
- **Parallel-safe with:** none

## Goal

A value longer than its cell is narrowed to fit on each drawing (Aidan's safety net, spike 218). Inventor has no fit-to-cell member; `TextBox.WidthScale` narrows a value at full height without moving its baseline, but only inside `TitleBlockDefinition.Edit` on the drawing's own copy of the definition, which every sheet shares.

## Scope

- Core: `TitleBlockFit` takes the cell width, size, bold flag and the resolved text, and returns `WidthScale = min(1, available / PrintTextFit.WidthMm(...))`, not below a floor (Default 0.6, spec 05 question 30); below the floor it returns the floor and a warning. Tests.
- Add-in: a step at the end of Create Print and an "Update title block" step (find the right existing command or add one to the drawing ribbon; Default: run it in Create Print and in Place Initial Views), in one transaction: for each sheet with a view, read `TitleBlock.GetResultText` for each field, keep the smallest scale per field, then `Edit` the drawing's title block definition once, set `WidthScale` on those field boxes, `ExitEdit(true)`. One Undo reverts it.
- Do not use prompted entry or write a shortened value into the part (spike 218).

## Acceptance criteria

- [ ] A part with a long Description and Finish Spec prints both inside their cells, narrowed, with a warning below the floor.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes; `dotnet build InventorAddin.slnx -p:DeployToInventor=false` has 0 errors; every Inventor API member is confirmed in a spike or existing code, or listed under Verification.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**

## Follow-ups

Things noticed but not done.
