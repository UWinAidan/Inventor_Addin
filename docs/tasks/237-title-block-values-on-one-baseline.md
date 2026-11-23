# 237: Title block values sit on their baseline whatever the letters

- **Milestone:** M5 (from spike 218, 2026-11-23)
- **Feature spec:** `docs/features/05-drawing-tools.md`
- **Status:** todo
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

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**

## Follow-ups

Things noticed but not done.
