# 223: Title block fixes from the rebuild

- **Milestone:** M5 (from Aidan's check of the task 216 rebuild, 2026-11-22)
- **Feature spec:** `docs/features/05-drawing-tools.md`; tasks 187, 215, 216
- **Status:** todo
- **Depends on:** 216
- **Needs:** **windows** build; Aidan rebuilds both templates afterwards
- **Parallel-safe with:** none (shares the build folder)

## Goal

Aidan's check of the rebuilt templates passed the text widths (the width ratio mean is 1.00), the FILE NAME level, STATUS and the metric period. Four things are still wrong. Fix them in the builder and layout so the next rebuild is clean.

## Scope

1. **False "extends past its cell" warnings.** REV and SHEET fit by eye, but the builder warns that they extend past their cells. Fix the measuring in `PrintTemplateBuilder` (the range box read-back): find out what it measures, for example field placeholder text instead of the printed result, or the wrong cell bounds, and measure what prints. Real overflows must still warn.
2. **Decimal markers only on AWB styles.** About 70 warnings come from "Setting the decimal marker of ... style" failing (E_FAIL) on the styles of other standards (JIS, DIN, GOST, ANSI and so on). Set the marker only on the dimension and feature control frame styles the AWB standards actually use (the standard's object defaults and the styles they reference), and do not touch or log the rest.
3. **Gap above the edge note.** In the tolerance box the "X.XX ±0.05" line touches "REMOVE BURRS, BREAK SHARP EDGES." Tighten the tolerance lines' spacing, or lower the edge note, so there is a clear gap; nothing may leave the box. The numbers belong in `PrintTemplateLayout` (Core), with tests.
4. **One baseline per row.** Values in one row sit at different heights: MATERIAL SIZE's value sits lower than MATERIAL's, and WEIGHT's sits higher than DATE's. Every value in a row has the same baseline. Find why these values differ (a different size, justification, a field whose range box differs, the RangeBox shift) and fix it at the cause. Add a Core test that every value in a row shares one baseline.

Out of scope: long values (task 218); refreshing an old Finish Spec (task 224).

## Acceptance criteria

- [ ] A build of both templates logs no "extends past its cell" warning for REV or SHEET, and no decimal marker warning for a style the AWB standards do not use.
- [ ] The metric dimension style still prints a period.
- [ ] Core tests cover the tolerance box gap and the shared row baselines.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes.
- [ ] `dotnet build InventorAddin.slnx -p:DeployToInventor=false` has 0 errors.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:** Aidan rebuilds both templates with the developer button and checks items 1 to 4 on the sheet and in the log.

## Follow-ups

Things noticed but not done.
