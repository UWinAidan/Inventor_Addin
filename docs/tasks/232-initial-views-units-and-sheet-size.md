# 232: Initial Views: Metric or Imperial, and the sheet size

- **Milestone:** M5 (from Aidan's chunk 2 check, 2026-11-23)
- **Feature spec:** `docs/features/05-drawing-tools.md` (task 192)
- **Status:** todo
- **Depends on:** 227
- **Needs:** windows build
- **Parallel-safe with:** none

## Goal

There is no way to make an imperial print today. The Initial Views window gets a Metric | Imperial choice, defaulting to the part's units, which picks the AWB Metric or AWB Imperial template, and a sheet size dropdown (A4 to A0 for metric, ANSI A to E for imperial) defaulting to the smallest sheet that fits at the chosen scale. When a chosen scale does not fit, the window offers the next sheet size instead of only a warning.

## Scope

- `InitialViewsViewModel` (Core): units, sheet list per units, default sheet rule, next-size offer; tests.
- `InitialViewsWindow`, `CreatePrintCommand` (template and sheet format from the choice), `PlaceInitialViewsCommand` (sheet choice applies to the active sheet only if it can be resized; otherwise Default: the sheet list is hidden there).

- FINISH and WEIGHT follow the print's units, not the part's (Aidan, 2026-11-23): an inch part on a metric print prints the µm Finish Spec and kg, a metric part on an imperial print the inch spec and lb. Default (spec 05 question 33): when the units differ, Create Print writes the print-units Finish Spec and Mass as custom properties of the drawing (made by the same Core formatting as the part's own) and points that drawing's title block definition's FINISH and WEIGHT fields at them; when they match, the fields stay on the part's live properties.

## Acceptance criteria

- [ ] Create Print can make an imperial print of a metric part and an A2 print of a large part.
- [ ] An inch part on a metric print shows the µm Finish Spec and kg; a metric part on an imperial print shows the inch spec and lb.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes; `dotnet build InventorAddin.slnx -p:DeployToInventor=false` has 0 errors; every Inventor API member is confirmed in a spike or existing code, or listed under Verification.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**

## Follow-ups

Things noticed but not done.
