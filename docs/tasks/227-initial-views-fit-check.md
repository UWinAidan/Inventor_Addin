# 227: Initial Views fit check matches what is placed

- **Milestone:** M5 (from Aidan's chunk 2 check, 2026-11-23)
- **Feature spec:** `docs/features/05-drawing-tools.md` (task 189, 190, 192)
- **Status:** todo
- **Depends on:** 192
- **Needs:** Core, plus reading placed view sizes on Windows
- **Parallel-safe with:** none

## Goal

The Initial Views window says 1:2 does not fit A3 although the views fit fine at 1:2 after OK, and an assembly on A3 got 1:20 with the views filling a small part of the sheet. The fit estimate (probably the iso size and the assembly range box) is too cautious.

## Scope

- `InitialViewsPlan` (Core): estimate each view from the model box seen along that view direction, the iso from the box's projected outline (not the 3D diagonal or a padded guess); tests with boxes whose real placed sizes are known from spike 183 C13 or from reading placed views.
- Check the iso view is placed at the sheet scale (Aidan saw it look a different scale from the front view on the assembly); fix if not.
- Optionally read placed view sizes back after placing and log planned vs placed.

## Acceptance criteria

- [ ] On Aidan's test part, 1:2 is offered as fitting A3 when the placed views fit.
- [ ] The assembly test gets the largest standard scale whose placed views fit within the fill fraction.
- [ ] The iso view's scale equals the sheet scale.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes; `dotnet build InventorAddin.slnx -p:DeployToInventor=false` has 0 errors; every Inventor API member is confirmed in a spike or existing code, or listed under Verification.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**

## Follow-ups

Things noticed but not done.
