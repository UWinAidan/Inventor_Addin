# 215: Print template layout fixes from the first build

- **Milestone:** M5 (from Aidan's task 187 check, 2026-11-22)
- **Feature spec:** `docs/features/05-drawing-tools.md` ("Decided for M5", "Template additions"); `docs/ui-mockups/title-block.html`
- **Status:** todo
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

- [ ] Sizes match the mockup; tests pin them.
- [ ] Tolerance classes and values are in aligned columns; the edge note fits inside the box on both templates.
- [ ] Each row's values share their labels' baseline.
- [ ] The border is centred: 10 mm on ISO and 12.7 mm on ANSI, all four sides.
- [ ] The fit test passes for every fixed text and label.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes.

## Notes for the implementer

Read DECISIONS.md rows dated 2026-11-22 first. Keep every number in Core; the builder (187) only draws.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:** (copied into `docs/checklists/m5.md` by the parent for the combined check, task 209)
  1. …

## Follow-ups

Things noticed but not done.
