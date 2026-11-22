# 215: Print template layout fixes from the first build

- **Milestone:** M5 (from Aidan's task 187 check, 2026-11-22)
- **Feature spec:** `docs/features/05-drawing-tools.md` ("Decided for M5", "Template additions"); `docs/ui-mockups/title-block.html`
- **Status:** done
- **Depends on:** 185, 214
- **Needs:** Core only
- **Parallel-safe with:** 217, 221

## Goal

Aidan's check of the first templates found the title block text too big for its cells, the tolerance box crammed with its edge note running out of the box, and FILE NAME's value sitting higher than its label. The border is to be centred on the sheet. This task fixes the layout data in Core, and proves by test that no text overflows its cell.

## Scope

- `src/InventorAddin.Core/Drawing/PrintTemplateLayout.cs` and `PrintTemplateLayoutTests`.
- **Text sizes and spacing:** take every text size and position from `docs/ui-mockups/title-block.html` (the mockup's source): labels, values, the part number and REV, and the tolerance box.
  - Aidan reported the built text as bigger than the mockup's sizes for its cells (labels 1.8 mm and values 2.8 mm, per his note). Read the HTML for the real numbers and use them.
- **Tolerance box:**
  - Lay the linear tolerances out in aligned columns as the mockup does: the class in one column and the tolerance in the next, with a gap. So "X.XX" and "±0.05" are separate, aligned texts, and ".XXX" and "±.005" likewise.
  - Angles go beside them.
  - The edge note "REMOVE BURRS, BREAK SHARP EDGES." wraps onto as many lines as it needs inside the box, metric and imperial.
- **Baselines:** in each row, every value sits on the same baseline as its label (FILE NAME's value was higher than its label and than PROJECT's). Inline label and value pairs share one y.
- **Margins:** equal margins all round, so the border is centred: 10 mm on ISO sheets and 12.7 mm on ANSI (Aidan, 2026-11-22, overturning question 25's 20 mm filing edge).
  - Re-check the title block fit, the zones and the triad rule (task 214) with the new border.
- **Fit check in Core:**
  - Add a width estimate for Tahoma text (per-character advance widths for the characters used, in em, from Tahoma's metrics; document the source, or use a conservative average and say so).
  - Add a test that every fixed text and every label fits inside its cell with at least 0.5 mm padding on both templates.
  - Expose the estimate (for example `PrintTextFit.WidthMm(text, sizeMm)`) so tasks 216 and 218 can reuse it.

## Acceptance criteria

- [x] Sizes match the mockup; tests pin them.
- [x] Tolerance classes and values are in aligned columns; the edge note fits inside the box on both templates.
- [x] Each row's values share their labels' baseline.
- [x] The border is centred: 10 mm on ISO and 12.7 mm on ANSI, all four sides.
- [x] The fit test passes for every fixed text and label.
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes.

## Notes for the implementer

Read DECISIONS.md rows dated 2026-11-22 first. Keep every number in Core; the builder (187) only draws.

## Verification

- **Ran:** `dotnet build src/InventorAddin.Core` (0 warnings, 0 errors); `dotnet test tests/InventorAddin.Core.Tests`: 5161 passed, 0 failed. New tests are in `PrintTemplateFitTests` (sizes, tolerance columns, edge note wrap, baselines, fit on both templates, `PrintTextFit`) and `PrintTemplateLayoutTests` (centred border). Updated pinned old values in `PrintTemplateLayoutTests` (margins, A4 border now 190 wide) and in `InitialViewsPlanTests.UsableArea_IsInsideTheBorderAndAboveTheTitleBlock` (left edge 1.0 cm, was 2.0; outside the listed files but a direct consequence of the margin decision).
- **Not compiled (changed under `src/InventorAddin`):** none changed. `PrintTemplateBuilder.cs` still compiles against Core in principle: `SheetSizeSpec.FilingMarginMm` and `OtherMarginMm` remain as computed properties (both now equal the one `MarginMm`). Not built on Windows in this task.
- **Inventor API members not confirmed:** none used.
- **What changed in the layout:** margins 10 mm (ISO) and 12.7 mm (ANSI) on all four sides; inline label and value (PROJECT, FILE NAME) now share the value's baseline, and the inline value starts after the measured width of "LABEL:" plus 2 mm; MATERIAL SIZE (2.5 mm) shares the Material row's value baseline; the tolerance box is re-spaced (class column, tolerance column, divider, angles computed from measured widths, edge note wrapped when needed; at the mockup's size it fits on one line); `PrintTextFit` (new) gives `WidthMm`, `CapHeightMm`, `Wrap`, from Tahoma advance widths measured on Windows.
- **Text sizes:** the numbers in the mockup and in the code were already identical (labels 1.8, values 2.8, project/file name/status/material size 2.5, description 4, part number and revision 7 bold, tolerance heading and note 1.8, units line 2.5 bold, class/tolerance/angle texts 2.4). So the data was not "bigger" than the mockup. The mockup's sizes are SVG `font-size`, the em. If Inventor's FontSize override is the cap height rather than the em, the built text would be about 1/0.727 = 1.38 times bigger than the mockup, which matches Aidan's report. See Follow-ups.
- **Manual checklist for Inventor:** (copied into `docs/checklists/m5.md` by the parent for the combined check, task 209)
  1. Build both templates with Build Print Template (metric and imperial) and open a new drawing from each.
  2. Border: on an A3 sheet the gap from the border to the sheet edge is the same on all four sides (10 mm); on ANSI B it is 12.7 mm on all four sides. The zone letters and numbers still sit between the frame and the border.
  3. PROJECT and FILE NAME: each value sits on the same line as its label, and the two values are level with each other; the value does not touch the colon.
  4. Tolerance box: "X", "X.X", "X.XX" (or ".X", ".XX", ".XXX") form one column and their tolerances a second column beside it; ANGLES and its value sit to the right of a divider; the edge note sits on one line inside the box (after task 216's text size fix) and nothing crosses the box edge.
  5. Every label (DESCRIPTION, PART NUMBER, MATERIAL SIZE and so on) sits fully inside its cell, and the values in the Material row share a baseline.
  6. On A4 and ANSI A portrait, the triad still sits above the title block's left end.

## Follow-ups

- Task 187's builder: confirm whether `StyleOverride FontSize` is the em or the cap height in Inventor. If cap height, multiply the layout's size by `PrintTextFit.CapHeightEm` (0.727; FontSize = size × 0.727) in `PrintTemplateBuilder.AddText` (or switch the layout to cap heights). This is the most likely cause of the "text too big" report, since the layout sizes equal the mockup's. Task 216/218 value shrinking should use the same convention.
- `PrintTemplateBuilder` still reads `FilingMarginMm` and `OtherMarginMm`; they can be replaced by `MarginMm` when next touched.
- The small part number's glyphs clear the zone frame by only 0.2 mm on ISO sheets (inset 3 mm from the sheet edge, frame 5 mm inside a 10 mm margin; unchanged geometry, the same as before). A 2.5 mm inset would give 0.7 mm; left alone because the mockup and tests say 3 mm.
- The part number row keeps the mockup's lower baseline for the 2.5 mm Status value next to the 7 mm part number and revision; tell Aidan if he wants them on one baseline.
- Docs still say "20 mm filing edge": the mockup's A3 caption in `docs/ui-mockups/title-block.html` and the task 185 notes. Not edited here.
