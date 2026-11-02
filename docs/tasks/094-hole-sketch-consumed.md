# 094: The hole's sketch is consumed under it after OK

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("The window", hole centres)
- **Status:** done
- **Depends on:** 090
- **Needs:** windows build
- **Parallel-safe with:** 093, 096, 098

## Goal

Fix the bug Aidan found in the 090 check (step 8): after OK the sketch is hidden but stays in the browser as a separate shared sketch. It must end up consumed under the hole, as with Inventor's Hole.

## Scope

- `src/InventorAddin/Holes/HoleSession.cs` (showing the sketch and putting it back), Core `HoleSessionRules` if the rule changes, with tests

## Acceptance criteria

- [x] After OK on a new hole, the sketch is under the hole in the browser, not shared and not visible, exactly as Inventor's Hole leaves it.
- [x] A sketch that was already shared before the command stays shared, and keeps its visibility.
- [x] Cancel leaves the sketch exactly as it was (shared state and visibility); the session's transaction abort may already do this; confirm it.
- [x] Editing a hole (092 uses this path): the sketch is put back to how it was before the edit.
- [x] The cause is in Verification. Likely: spike 081 found that the preview hole consumes the sketch, so the session sets `PlanarSketch.Visible = true`; making a consumed sketch visible shares it, and setting `Visible = false` afterwards hides it but leaves it shared. `PlanarSketch.Shared` (get and set) and `PlanarSketch.Consumed` exist in the interop; record `Shared` when the sketch is shown and set it back on OK.
- [x] The sketch stays visible and its points clickable while the window is open.
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes; `dotnet build InventorAddin.slnx -p:DeployToInventor=false` builds

## Notes for the implementer

- You may test the API with a throwaway console app driving its own invisible Inventor, as described in task 093's notes. Do not attach to an Inventor Aidan has open.

## Verification

- **Cause (spike in the scratchpad, its own invisible Inventor 2026, quit at the end, not in the repo):** the brief's guess is wrong. Showing a consumed sketch does not share it: create a hole in an inner step (the sketch is consumed and hidden), set `Visible = true`, change the hole (including `HoleCenterPoints` and the Recreate path: `SetEndOfPart`, `Delete(true, true, true)`, add again), set `Visible = false`, and the sketch reads `Shared = false`, `Consumed = true`, and sits under the hole in the Model browser (`BrowserPanes` "PmDefault" dump), exactly as with no session. What does share it is **a hole on a sketch that another feature already consumes**: the second hole makes Inventor set `Shared = true` (the sketch then appears at the top level and under both holes), and `Shared = false` is refused with E_FAIL while two features use it. That matches Aidan's log of the 090 check: at 23:49:05 Hole2 was made on Sketch3, at 23:49:12 the next AWB Hole started "from sketch Sketch3" again and made Hole3 on it, and again at 23:54:55 (Hole4); the same on Sketch2. Each such OK left the sketch hidden but shared. The log cannot tell how Sketch3 was chosen the second time (selected, sketch of a selected point, or still visible); the start log line now says (see below). A second way to be left shared: a session puts a second hole on a consumed sketch, then the picks move off it (the preview hole is removed): the sketch stays shared with one consumer; `Shared = false` then works.
- **What changed:**
  - Core `HoleSessionRules`: `UnshareAfter(wasShared, isShared)` (true when the session left the sketch shared and it was not before); `SketchVisibleAfter` takes `sharedAfter` (default false): a sketch shared at the end keeps its visibility, as Inventor does not hide a shared sketch when a feature consumes it. After review, `DefaultSketch`'s fallback (nothing selected) also skips a sketch a feature already consumes: its tuple gains a lazy `Consumed` flag, read only for a visible sketch, and it needs visible, not consumed, and hole centres. The selected-sketch and selected-point branches are unchanged, so a deliberate selection of a consumed sketch still wins (and ends shared, as above). 13 new test cases in `HoleSessionRulesTests`. Spec 09's centres line says "newest visible unconsumed"; its open question 11 asks about per-point behaviour.
  - `HoleSession.SketchesNewestFirst` passes `() => ComSafe.Get(() => sketch.Consumed)` as the new flag.
  - `HoleSession`: records `Shared` with `Visible` when it shows a sketch; on OK and Cancel, for every shown sketch, sets `Shared = false` if `UnshareAfter` says so (logs Info "SketchN stays shared: more than one feature uses it." when Inventor refuses), then sets visibility by `SketchVisibleAfter`. Still inside the outer transaction on OK, so one Undo reverts it. The start log line now says how a new hole's sketch was chosen and its state before the session showed it, for example "new hole: 3 centre(s) from sketch Sketch3 (newest visible unconsumed, visible)" or "(selected, hidden, consumed)".
  - Spike results for the rest: Cancel (outer `Abort`) puts back `Visible`, `Shared` and `Consumed` in every case tried, including a second hole that had shared the sketch; one Undo after OK puts the sketch back unconsumed and visible. An edit of a consumed hidden sketch ends hidden and not shared; an edit of a shared visible sketch stays shared and visible. A pre-shared sketch stays visible when a hole consumes it.
- **Ran:** `dotnet build InventorAddin.slnx -p:DeployToInventor=false -tl:off`: succeeded, no new warnings (the CS8600 warnings are in other files and were there before). `dotnet test tests/InventorAddin.Core.Tests`: 3235 passed, 0 failed.
- **Not compiled (changed under `src/InventorAddin`):** none; `Holes/HoleSession.cs` compiled against the Inventor 2026 interop. It has not run inside Inventor's UI; the spike ran the same API calls in an invisible Inventor, without the picking or the window.
- **Inventor API members not confirmed:** none. `PlanarSketch.Shared` (get, set), `PlanarSketch.Consumed` (get) and `PlanarSketch.Visible` were exercised in the spike. Not confirmed: that a visible Inventor behaves like the invisible one when a consumed sketch is shown (the spike could not show the UI).
- **Manual checklist for Inventor:**
  1. New sketch with three points, AWB Hole, OK: the sketch is under the hole in the browser, hidden, not shared. One Ctrl+Z: the hole and the consumption go, the sketch is as before. The log's start line says "(newest visible unconsumed, visible)".
  2. Share a sketch first (right-click, Share Sketch), AWB Hole on it, OK: still shared and visible.
  3. Cancel: the sketch is as before.
  4. Right after step 1's OK, make the consumed sketch visible, press AWB Hole with nothing selected: the window opens with no centres. Select the sketch, press AWB Hole, OK: the log says "stays shared".
  5. On a hole's consumed sketch made visible, AWB Hole (the second hole's preview appears), click a point of another sketch so the preview moves there, OK: the first sketch is back under its first hole, not shared.

## Follow-ups

- Default for `DECISIONS.md` (task 094, review): with nothing selected, the default sketch skips sketches a feature already consumes (`DefaultSketch` requires visible, not consumed, and hole centres). A selected sketch, or the sketch of selected points, still wins even when consumed; it is shared and logged as described above. Spec 09 open question 11 asks Aidan whether he wants Inventor's per-point behaviour instead.
- Default for `DECISIONS.md` (task 094): a sketch that ends a session shared (shared before, or because the new hole is its second consumer and Inventor refuses to unshare) keeps the visibility it had when the window opened, instead of being hidden like a kept new hole's sketch.
- Open: how Sketch3 was chosen again right after an OK in Aidan's 090 check (selection left over, or the sketch still visible after OK in the visible Inventor). The new start log line answers it; the default no longer picks a consumed sketch, but if a kept hole's sketch is still visible after OK in the visible Inventor, that is a separate bug the spike could not reproduce.
