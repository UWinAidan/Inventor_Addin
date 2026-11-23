# 223: Title block fixes from the rebuild

- **Milestone:** M5 (from Aidan's check of the task 216 rebuild, 2026-11-22)
- **Feature spec:** `docs/features/05-drawing-tools.md`; tasks 187, 215, 216
- **Status:** done in code (passed review); WEIGHT vs DATE baseline open; waits on Aidan's rebuild
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

- **Ran:** `dotnet build InventorAddin.slnx -p:DeployToInventor=false`: 0 errors. `dotnet test tests/InventorAddin.Core.Tests`: 5315 passed, 0 failed (new tests in `PrintTemplateFitTests`: row sizes and baselines, tolerance gap, sample values fit; `PrintTemplateLayoutTests` Material Size now 2.8).
- **Causes found** (from the code, the 2026-11-22 build log, and a read-only look at `libraries/Templates/AWB Metric.idw`, opened invisibly and closed without saving):
  1. REV and SHEET: a field text's `RangeBox` measures the property's name, not what prints (the log said REV extended 32.3 mm each side, SHEET 25.59 right: "REVISION NUMBER" at 7 mm bold, "Sheet Number OF Number of sheets"). Field values are no longer measured by range box; Core's `SampleOverflowMm` checks a typical value (`SampleValue`) against the cell. Labels and fixed text are still measured.
  2. Decimal markers: the builder set every style in the drawing, including the other standards' (E_FAIL, 56 warnings per build). It now sets only the dimension styles and the feature control frame style named by the AWB object defaults (the copied standard), after the table styles step.
  3. Tolerance gap: the edge note's first baseline moved from 19.0 to 20.4 mm below the box top (new `EdgeNoteBaselineDownMm`; the box is 22 mm); a test requires 2 mm clear above the capitals.
  4. Baselines: in the saved template every value in a row has the same origin y, so the layout was already equal; the difference is the size. Inventor puts the baseline a font-size-dependent distance above the point, so Material Size (2.5 mm) sat lower than Material (2.8 mm). Material Size is now 2.8 mm. WEIGHT higher than DATE: both are 2.8 mm with identical origins (9.9 mm), so the layout cannot explain it. Guess: the resolved Mass text ("kg" physical property field) lays out differently from the Date field (in the template Mass's range box is empty, Date's is 2.12 mm tall). Not confirmed without a real drawing, and NOT fixed: see Follow-ups.
- **Not compiled (changed under `src/InventorAddin`):** none; built on Windows with 0 errors. Not run in Inventor.
- **Inventor API members not confirmed:** none new. The `ObjectDefaultsStyle` members used (AngularDimensionStyle, BaselineDimensionStyle, BendNoteStyle, BendTagStyle, ChainDimensionStyle, ChamferNoteStyle, DiameterDimensionStyle, HoleNoteStyle, HoleTagStyle, LeaderTextStyle, LinearDimensionStyle, OrdinateDimensionStyle, OrdinateSetDimensionStyle, PunchNoteStyle, RadialDimensionStyle, ThreadNoteStyle, FeatureControlFrameStyle) were checked by reflection on the interop DLL and compile. That the markers read back as a period is shown by the log line, as before.
- **Defaults chosen:** Material Size value is 2.8 mm (the mockup said 2.5); the edge note moves down rather than tightening the tolerance lines; the sample values are invented.
- **Manual checklist for Inventor:** Aidan rebuilds both templates with the developer button, then:
  1. The log has no "extends past its cell" for REV or SHEET and no "Setting the decimal marker of ..." warning; the "decimal marker set to a period on N dimension style(s)" line ends "0 did not read back as a period".
  2. In a metric drawing a dimension prints a period (260.00).
  3. In the tolerance box there is a clear gap between "X.XX +-0.05" and "REMOVE BURRS, BREAK SHARP EDGES.", and nothing leaves the box.
  4. On a drawing with a mass: MATERIAL SIZE's value is level with MATERIAL's and FINISH's; check whether WEIGHT is level with DATE, DETAILER and DESIGNER (tell the parent if not).

## Follow-ups

- WEIGHT's baseline against DATE's is unexplained (Verification, cause 4). If it is still higher after the rebuild, compare the resolved Mass and Date texts on a real drawing; the likely fix is a per-field baseline correction in the builder.
- Task 218 (shrinking long values) must keep every value in a row the same size, or the row's baselines drift again.
- The part number row still has a 2.5 mm Status beside the 7 mm part number and revision on a lower baseline (task 215 note); the same size dependence applies.
