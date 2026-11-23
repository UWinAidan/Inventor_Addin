# 233: Initial Views: the Front view choice and Auto

- **Milestone:** M5 (from Aidan's chunk 2 check, 2026-11-23)
- **Feature spec:** `docs/features/05-drawing-tools.md` (task 192)
- **Status:** todo
- **Depends on:** 225, 229
- **Needs:** windows build
- **Parallel-safe with:** none

## Goal

The window gets a "Front view" dropdown (Auto, Front, Back, Top, Bottom, Left, Right), default Auto. Auto for ordinary parts and assemblies picks the direction with the largest projected area, longest side horizontal; sheet metal uses the base face rule (task 229). This also fixes the assembly print whose front view was edge-on with no proper top view.

## Scope

- Core: the Auto rule from the model's extents, tests; the plan's view boxes follow the chosen direction.
- Adapter: the base view orientation from spike 225.

## Acceptance criteria

- [ ] Aidan's test assembly prints with a full front view and a proper top view on Auto.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes; `dotnet build InventorAddin.slnx -p:DeployToInventor=false` has 0 errors; every Inventor API member is confirmed in a spike or existing code, or listed under Verification.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**

## Follow-ups

Things noticed but not done.
