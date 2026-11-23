# 229: Sheet metal: front view on the base face, and bend notes

- **Milestone:** M5 (from Aidan's chunk 2 check, 2026-11-23)
- **Feature spec:** `docs/features/05-drawing-tools.md`
- **Status:** todo
- **Depends on:** 225, 228
- **Needs:** windows build
- **Parallel-safe with:** none

## Goal

On sheet metal the folded front view shows the part edge-on. The front view looks straight at the flat pattern's base face, longest edge horizontal; the other views project from it. The flat pattern view gets bend notes (direction, angle, radius) if it has none.

## Scope

- Core: the rule (base face normal, longest edge horizontal) as data the adapter applies.
- `InitialViewsPlacer`: base view placed with spike 225's orientation call; projected views from it.
- Bend notes on the flat pattern view as spike 225 found.

## Acceptance criteria

- [ ] Aidan's sheet metal test part prints with the base face seen flat in the front view, longest edge horizontal, and bend notes on the flat pattern.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes; `dotnet build InventorAddin.slnx -p:DeployToInventor=false` has 0 errors; every Inventor API member is confirmed in a spike or existing code, or listed under Verification.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**

## Follow-ups

Things noticed but not done.
