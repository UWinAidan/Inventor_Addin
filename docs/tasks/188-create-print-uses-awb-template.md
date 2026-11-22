# 188: Create Print uses the AWB template

- **Milestone:** M5
- **Feature spec:** `docs/features/05-drawing-tools.md` ("Decided for M5" (Create Print uses the AWB print template), open question 21)
- **Status:** todo
- **Depends on:** 186, 187
- **Needs:** windows build
- **Parallel-safe with:** none

## Goal

Create Print makes its drawing from the AWB print template for the default units, on an A3 landscape (metric) or ANSI B landscape (imperial) sheet, instead of Inventor's template.

## Scope

- `src/InventorAddin.Core/Drawing/PrintPlan.cs`: the sheet format name per units, the missing-template message naming the path it looked for.
- `src/InventorAddin/Commands/CreatePrintCommand.cs`: template from `LibraryPaths.PrintTemplate`; picks the sheet format as spike 183 showed; keeps the existing front view, scale rule, naming and Part Number write.
- `src/InventorAddin/Inventor/Templates.cs`: `DrawingFor` is no longer used by Create Print; remove it if nothing else calls it.
- Tests for the new `PrintPlan` members.

Out of scope: the initial views window (192); notes (197).

## Acceptance criteria

- [ ] With the starter template, Create Print on a part makes a drawing on A3 landscape (metric) or ANSI B landscape (imperial) whose title block shows the part's values.
- [ ] With a Settings path set, that template is used.
- [ ] A missing template file stops with a message that names the path; nothing is created.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes
- [ ] `dotnet build InventorAddin.slnx -p:DeployToInventor=false` has 0 errors; every Inventor API member used is confirmed in the spike results or existing code, or listed under Verification

## Notes for the implementer

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
