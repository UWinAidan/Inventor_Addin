# 192: Place Initial Views window and command; Create Print shows it

- **Milestone:** M5
- **Feature spec:** `docs/features/05-drawing-tools.md` ("Decided for M5: Place Initial Views")
- **Status:** todo
- **Depends on:** 183, 188, 189, 190, 191
- **Needs:** windows build
- **Parallel-safe with:** none

## Goal

Create Print shows the Initial Views window and places the confirmed views; a Place Initial Views button on a drawing does the same on the active sheet for the drawing's model.

## Scope

- `src/InventorAddin/UI/InitialViewsWindow.xaml(.cs)` bound to `InitialViewsViewModel`.
- `src/InventorAddin/Drawing/InitialViewsPlacer.cs`: base, projected, iso and flat pattern views by spike 183's calls, in one transaction.
- `src/InventorAddin/Commands/CreatePrintCommand.cs`: window before creating; Cancel creates nothing.
- `src/InventorAddin/Commands/PlaceInitialViewsCommand.cs` + ribbon button (drawing environment).

Out of scope: dimensions, notes.

## Acceptance criteria

- [ ] Part: front, top, right and iso placed as the plan says; sheet metal: folded front and flat pattern; assembly: front and iso.
- [ ] Cancel in Create Print's window creates no drawing.
- [ ] Place Initial Views on a drawing with no views asks for the model (or uses the drawing's model when it has one) and places the views; one Undo removes them.
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
