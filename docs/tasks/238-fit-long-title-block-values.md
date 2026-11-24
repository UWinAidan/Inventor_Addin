# 238: Fit long title block values to their cells

- **Milestone:** M5 (from spike 218, 2026-11-23)
- **Feature spec:** `docs/features/05-drawing-tools.md`
- **Status:** todo
- **Depends on:** 241, 218, 237
- **Needs:** windows build
- **Parallel-safe with:** none

## Goal

A value longer than its cell is narrowed to fit on each drawing (Aidan's safety net, spike 218). Inventor has no fit-to-cell member; `TextBox.WidthScale` narrows a value at full height without moving its baseline, but only inside `TitleBlockDefinition.Edit` on the drawing's own copy of the definition, which every sheet shares.

## Scope

Aidan overturned spec 05 question 31 on 2026-11-23 (after this brief was written): every title block value narrows to 0.6 width, then gets smaller down to the floor (question 32, Default 1.8 mm); Description may wrap onto two lines in its cell; single-line fields never wrap and warn only at the floor. Build the way spike 241 found; where this section's older text says otherwise, this paragraph wins.

Spike 241 (Results in `docs/tasks/241-*.md`) settled the how; build its recommendation:
- Core `TitleBlockFit`, every field: one line at the cell's size narrowing to 0.6 width, then smaller text at 0.6 down to the 1.8 mm floor; the origin from `PrintTextFit.UpperOriginYMm` at the new size; single-line fields warn at the floor (naming the field and how much too long).
- Description only (Default): after one line at 0.6, two lines at about 3.4 mm (line 1 cap top about 46.3 mm in title block coordinates, line pitch 1.2 x em) narrowing to 0.6, then shrinking to the floor, then warn. Core counts lines with `PrintTextFit.Wrap(text, size, available / scale)`.
- Add-in step, one transaction: read `GetResultText` on each sheet that has a view and keep the most severe result per field; one `TitleBlockDefinition.Edit`; on each field box set the `StyleOverride` font size (the cap height in cm, 0.0727 x em in mm), `WidthScale` and `Origin`; for Description only also set `TextBox.Width` to the cell width (makes it wrap, stays live); `ExitEdit(true)`. Never set `Width` on a single-line field. Run in Create Print and an Update command. `TextBox.FontSize` does not exist.
- One size per field for the whole drawing (shared definition) (Default).


- Core: `TitleBlockFit` takes the cell width, size, bold flag and the resolved text, and returns `WidthScale = min(1, available / PrintTextFit.WidthMm(...))`, not below a floor of 0.6 (Aidan, spec 05 question 30); below the floor it returns the floor and a warning that names the field and how much too long the value is, for example in mm or percent (Aidan, spec 05 question 31). Tests.
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
