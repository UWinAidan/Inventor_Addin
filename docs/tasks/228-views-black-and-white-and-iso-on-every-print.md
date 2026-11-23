# 228: Every view black and white, and an iso on every print

- **Milestone:** M5 (from Aidan's chunk 2 check, 2026-11-23)
- **Feature spec:** `docs/features/05-drawing-tools.md`
- **Status:** todo
- **Depends on:** 192
- **Needs:** windows build
- **Parallel-safe with:** none

## Goal

Aidan overturned the Default "the iso is its own shaded view": every view, iso included, is black and white (visible lines, no shading). Sheet metal prints get no iso today; every print gets one, sheet metal included.

## Scope

- `InitialViewsPlan`/`InitialViewsViewModel` (Core): sheet metal offers and ticks Iso; layout tests.
- `InitialViewsPlacer`: no shaded style; the iso uses the same black-and-white style as the other views.

## Acceptance criteria

- [ ] A part, a sheet metal part and an assembly each get an iso, unshaded.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes; `dotnet build InventorAddin.slnx -p:DeployToInventor=false` has 0 errors; every Inventor API member is confirmed in a spike or existing code, or listed under Verification.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**

## Follow-ups

Things noticed but not done.
