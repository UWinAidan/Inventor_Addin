# 094: The hole's sketch is consumed under it after OK

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("The window", hole centres)
- **Status:** todo
- **Depends on:** 090
- **Needs:** windows build
- **Parallel-safe with:** 093, 096, 098

## Goal

Fix the bug Aidan found in the 090 check (step 8): after OK the sketch is hidden but stays in the browser as a separate shared sketch. It must end up consumed under the hole, as with Inventor's Hole.

## Scope

- `src/InventorAddin/Holes/HoleSession.cs` (showing the sketch and putting it back), Core `HoleSessionRules` if the rule changes, with tests

## Acceptance criteria

- [ ] After OK on a new hole, the sketch is under the hole in the browser, not shared and not visible, exactly as Inventor's Hole leaves it.
- [ ] A sketch that was already shared before the command stays shared, and keeps its visibility.
- [ ] Cancel leaves the sketch exactly as it was (shared state and visibility); the session's transaction abort may already do this; confirm it.
- [ ] Editing a hole (092 uses this path): the sketch is put back to how it was before the edit.
- [ ] The cause is in Verification. Likely: spike 081 found that the preview hole consumes the sketch, so the session sets `PlanarSketch.Visible = true`; making a consumed sketch visible shares it, and setting `Visible = false` afterwards hides it but leaves it shared. `PlanarSketch.Shared` (get and set) and `PlanarSketch.Consumed` exist in the interop; record `Shared` when the sketch is shown and set it back on OK.
- [ ] The sketch stays visible and its points clickable while the window is open.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes; `dotnet build InventorAddin.slnx -p:DeployToInventor=false` builds

## Notes for the implementer

- You may test the API with a throwaway console app driving its own invisible Inventor, as described in task 093's notes. Do not attach to an Inventor Aidan has open.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. New sketch with three points, AWB Hole, OK: the sketch is under the hole in the browser, hidden, not shared. One Ctrl+Z: the hole and the consumption go, the sketch is as before.
  2. Share a sketch first (right-click, Share Sketch), AWB Hole on it, OK: still shared and visible.
  3. Cancel: the sketch is as before.

## Follow-ups
