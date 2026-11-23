# 237: Title block values sit on their baseline whatever the letters

- **Milestone:** M5 (from spike 218, 2026-11-23)
- **Feature spec:** `docs/features/05-drawing-tools.md`
- **Status:** done (built on Windows; not run in Inventor)
- **Depends on:** 218, 223
- **Needs:** windows build; Aidan rebuilds both templates
- **Parallel-safe with:** none

## Goal

Spike 218 found why values in a row sit at different heights (WEIGHT above DATE, MATERIAL above MATERIAL SIZE): with `kAlignTextLower` Inventor puts the bottom of the actual letters at the origin, so a value with descenders (p, g, commas) is lifted about 0.47 mm at 2.8 mm. `kAlignTextUpper` at origin y = baseline + cap height puts every value on its baseline (within 0.1 mm in the spike); `kAlignTextBaseline` is refused.

## Scope

- `PrintTemplateBuilder.AddText` (and the field value placement): upper vertical justification, origin from Core (`PrintTextFit.CapHeightMm(size)` above the layout baseline).
- Core: the origin rule, tests.
- Labels and fixed text too if they use the same path, so nothing else moves.

## Acceptance criteria

- [ ] After a rebuild, MATERIAL, MATERIAL SIZE and FINISH values, and DESIGNER, DETAILER, DATE and WEIGHT values, each share one baseline on a real drawing.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes; `dotnet build InventorAddin.slnx -p:DeployToInventor=false` has 0 errors; every Inventor API member is confirmed in a spike or existing code, or listed under Verification.

## Verification

Filled in by the implementer.

- **Ran:** `dotnet build InventorAddin.slnx -p:DeployToInventor=false`: 0 errors (11 warnings, not from this change). `dotnet test tests/InventorAddin.Core.Tests`: 5345 passed, 0 failed. Compiled on Windows; not run in Inventor.
- **Not compiled (changed under `src/InventorAddin`):** none (built on Windows). `PrintTemplateBuilder.AddText` is built but untested in Inventor.
- **Inventor API members not confirmed:** none new. `TextBox.VerticalJustification = kAlignTextUpper` and `TextBox.Origin` are the members spike 218 used; `kAlignTextUpper` placement at baseline + cap height was confirmed there.
- **Manual checklist for Inventor:**
  1. Rebuild both templates (metric and imperial) with the Print Template builder.
  2. Create Print from a part whose Material has descenders (e.g. a comma or p), and has Material Size, Finish Spec, Designer, Detailer and a weight.
  3. Zoom on the title block. MATERIAL, MATERIAL SIZE and FINISH values should share one baseline; DESIGNER, DETAILER, DATE and WEIGHT values likewise. Labels and fixed text should not have moved visibly.
  4. Check the builder log for new placement warnings and for the width-ratio line (should still be near 1).
  5. Check the rotated small part number beside the title block is where it was.

## Follow-ups

- Core: `PrintTextFit.UpperOriginYMm(baselineYMm, sizeMm)` is the origin rule; `AddText` uses it for every unrotated text (labels, values, fixed text, triad labels). The rotated small part number keeps lower justification and the range-box shift; spike 218 did not test rotated upper justification, and it is caps and digits, so descenders are rarely a problem there.
- Hole table text (`HoleTableMarker`, lower justification) was out of scope and not changed.
