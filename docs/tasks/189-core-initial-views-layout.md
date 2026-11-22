# 189: Initial views: which views and where (Core)

- **Milestone:** M5
- **Feature spec:** `docs/features/05-drawing-tools.md` ("Decided for M5: Place Initial Views", "Core logic: Initial view layout")
- **Status:** todo
- **Depends on:** none
- **Needs:** cloud
- **Parallel-safe with:** 185, 186

## Goal

Core decides the initial views for a part, a sheet metal part and an assembly, one scale for them all, and each view's centre on the sheet in third angle, clear of the title block.

## Scope

- `src/InventorAddin.Core/Drawing/InitialViewsPlan.cs` (+ small records): input the model kind, its range box size in cm, the sheet size and the usable area (inside the border, minus the title block and its two new rows, from `PrintTemplateLayout`); output the views (front, top, right, iso; folded front and flat pattern; front and iso), a standard scale by `PrintPlan.ChooseScale`'s rule, and each view's centre.
- Tests.

Out of scope: the window (190), Inventor (192).

## Acceptance criteria

- [ ] Part: front bottom-left, top above it, right to its right (third angle), iso in the top-right; all four fit inside the usable area at the chosen scale with a gap between views (Default 15 mm, say so).
- [ ] Sheet metal: folded front and flat pattern side by side; when the flat pattern size is not known, the plan says so and the flat pattern is left out.
- [ ] Assembly: front and iso.
- [ ] The scale is the largest standard scale at which every chosen view fits; the iso view's size is estimated from the box (document the estimate).
- [ ] A subset of views (the user unticks some) is laid out again without gaps where the missing views were.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- Reuse `PrintPlan.ChooseScale`'s list of standard scales; do not make a second list.
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
