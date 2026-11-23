# 226: Decimal period on every dimension style a print uses

- **Milestone:** M5 (from Aidan's chunk 2 check, 2026-11-23)
- **Feature spec:** `docs/features/05-drawing-tools.md` (task 223)
- **Status:** todo
- **Depends on:** 225
- **Needs:** windows build; Aidan rebuilds both templates
- **Parallel-safe with:** none

## Goal

A Create Print drawing still shows "20,00", "120,00", "260,00" on ordinate and linear dimensions: the builder set the period on only 2 dimension styles. Set it on every style the drawing's dimensions actually use (ordinate, linear, radial, angular, hole notes), as spike 225 found, and fix or quieten the 3 feature control frame E_FAIL warnings per template.

## Scope

- `PrintTemplateBuilder` decimal marker step, using spike 225's findings: the drawing's dimensions all use 'Default (ISO)' (LeaderText 'Default - Method 1b (ISO)'), and Inventor replaces them with the style library's comma versions on `Documents.Add` because the names match. Copy each to an "AWB ..." name (`style.Copy`), set the period on the copy, and point every object-defaults member that named the original at the copy. The feature control frame style needs `LinearUnits` set to mm (inches on imperial) before its marker can be set; do not turn on alternate units. The imperial template already prints a period; only its frame style needs the fix.
- Create Print: wrap `Documents.Add` in `Application.SilentOperation` (restored after) so the Style Conflict dialog cannot stop it.
- The log line names the styles set.

## Acceptance criteria

- [ ] On a fresh Create Print drawing every dimension kind prints a period.
- [ ] A template build logs no decimal marker warning, or one explained line per style that cannot be set.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes; `dotnet build InventorAddin.slnx -p:DeployToInventor=false` has 0 errors; every Inventor API member is confirmed in a spike or existing code, or listed under Verification.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**

## Follow-ups

Things noticed but not done.
