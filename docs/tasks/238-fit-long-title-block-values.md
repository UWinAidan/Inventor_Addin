# 238: Fit long title block values to their cells

- **Milestone:** M5 (from spike 218, 2026-11-23)
- **Feature spec:** `docs/features/05-drawing-tools.md`
- **Status:** done (built on Windows; not run in Inventor)
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

- **Ran:** `dotnet build InventorAddin.slnx -p:DeployToInventor=false`: 0 errors. `dotnet test tests/InventorAddin.Core.Tests`: 5506 passed, 4 failed on the first run (the known probe-timeout tests: RenamePlan, SaveAsReplacer, NewPartCreator, RegisterStore); the Numbering tests rerun alone: 863 passed, 0 failed. The 35 new `TitleBlockFitTests` pass. Built on Windows; not run in Inventor.
- **Not compiled (changed under `src/InventorAddin`):** none (built on Windows). `TitleBlockFitter` and the two command hooks are built but not run in Inventor.
- **What was built:**
  - Core `TitleBlockFit` (`src/InventorAddin.Core/Drawing/TitleBlockFit.cs`): the ladder per field, taking every value the field has on any sheet: one line at the cell's size narrowed to 0.6; then one line at 0.6 at the largest size in 0.05 mm steps; warn at 1.8 mm. Description: after one line at 0.6, two lines at 3.4 mm narrowed as little as possible, then 0.6 and smaller down to 1.8 mm, then warn. Origin from `PrintTextFit.UpperOriginYMm`; for two lines, the DESCRIPTION label's baseline minus 1.3 mm (46.3 in the metric block, derived, not hard-coded). Also the formatted-text helpers (field from the property, the `FontSize` read and rewrite) and `NeedsChange`.
  - Add-in `TitleBlockFitter` (`src/InventorAddin/Drawing/TitleBlockFitter.cs`): activates each sheet with a view and reads `GetResultText`, asks Core, and if any box differs edits the definition once (`Edit`, then `FormattedText` size, `Width` for a wrapping Description only, `WidthScale`, `Origin`, `ExitEdit(true)`). It never throws, restores the active sheet, and does nothing (no `Edit`) when every box already matches.
  - Create Print runs it in its own transaction after the FINISH/WEIGHT repointing (so the values read are the ones that print) and before the final Update; Place Initial Views runs it inside its own transaction. Warnings are logged and shown in one message after the command; a failure never stops the print.
- **Inventor API members not confirmed:** none new. Used as spikes 218 and 241 confirmed them: `TextBox.WidthScale`, `TextBox.Width` (set; read added here), `TextBox.Origin`, `TextBox.FormattedText`, `TitleBlock.GetResultText`, `TitleBlockDefinition.Edit/ExitEdit`. Read only here, in existing builder code as a set: `TextBox.Rotation` (read to skip the rotated small part number; if the read throws, `ComSafe` returns null and the box is skipped). `TextBox.Width` read back as a double in cm (spike 241 read 30.911); the `double?` cast in `ComSafe.Get` is assumed fine.
- **Decisions and defaults taken (for the parent to log):**
  - The available width of a value is the cell's right edge minus the value's x minus 1.5 mm (Description 112 mm, as spike 241). Centred fields (the revision letter) are not fitted.
  - The fit step runs after FINISH/WEIGHT are repointed, as its own Edit (two Edits in a unit-different print): the values must be read after the repoint to be the ones that print, and an open `Edit` does not show on `GetResultText`.
  - `TextBox.Width` is set only when the Description needs two lines, and is not reset when it fits one line again (a wide box holding one line prints the same).
  - The width scale is rounded down to 3 decimals and the size to 0.05 mm steps, so the estimate never leaves a value past its cell.
  - Fields with no value on any sheet are left alone; a drawing from another template (no recognised field boxes) is left alone.
  - No separate "Update title block" command: it runs in Create Print and Place Initial Views (the brief's Default).
- **Manual checklist for Inventor** (deploy first, rebuild nothing: the templates do not change; use invented values on a personal part):
  1. Metric. On a metric part, set Description to `BRACKET, MOUNTING, LEFT HAND, WITH TWO COUNTERBORED HOLES AND A SLOT FOR THE SET SCREW, ZINC PLATED STEEL, INVENTED` (about 115 characters), Finish Spec to `ZINC PLATED, CLEAR CHROMATE, 0.0003 IN MINIMUM, BAKED AFTER`, Material to `STEEL, MILD, COLD ROLLED, INVENTED GRADE` and Material Size to `100 x 50 x 6 mm FLAT BAR`. Save it. Run Create Print.
  2. Expect: Description on two lines at a smaller size inside its cell, below the DESCRIPTION label and clear of the line under it; Finish, Material and Material Size narrower and smaller but readable, each on its row's baseline, inside their cells. No warning box. The log has "N title block field(s) fitted".
  3. Short values. Shorten the Description to `PIN, PIVOT`, save, Create Print again on a part with no print (or Place Initial Views on a fresh sheet): values print at the normal size, no scale. Then on the first print, change the part's Description back and forth and run Place Initial Views: sizes return to normal when the value is short.
  4. Warning. Set Finish Spec to 120 characters of invented text. Create Print: a warning box names FINISH and how many mm and percent too long; the text is at the smallest size and 0.6 width, still on one line (never two). Do the same for Description at about 400 characters: the warning names DESCRIPTION.
  5. Undo. After step 2 on Place Initial Views, press Undo once: the views and the fit revert together. On Create Print the fit is its own undo step.
  6. Save and reopen the print: the sizes hold.
  7. Imperial. Repeat steps 1 and 2 with an inch part (Description and Finish Spec as above) and the AWB Imperial template (Settings units, or a part in inches): the same layout, same results.
  8. Two sheets. On the print, add a second sheet and place views of a different part with the short Description `PIN, PIVOT`: that sheet prints at the shared (smaller) size on its own line 1; after switching back, sheet 1 still shows two lines.
  9. Check the Create Print log for the fit line and that nothing else changed (FINISH and WEIGHT repointing still works for a part in the other units).

## Follow-ups

- No stand-alone "Update title block" command: the fit runs in Create Print and Place Initial Views only, so a property changed afterwards (or a sheet added later) is not refitted until one of them runs. A ribbon button on the drawing panel that calls `TitleBlockFitter.Apply` in a `DocumentEditCommand` is a small task.
- Two Edits of the title block definition in a print whose units differ from the part's (the repoint, then the fit). They could be one Edit if the fit took the FINISH and WEIGHT values from the property writes instead of `GetResultText`.
- The centred Revision field is not fitted (a long revision is not expected).
- The fitter's field recognition reads the property names the template builder writes. A hand-edited template with a renamed property would be skipped silently.
- Whether Inventor flags the edited definition as different from the template's is still untried (spikes 218, 241).
- Imperial template geometry is assumed identical to metric in mm (asserted in Core tests); not run in Inventor.
