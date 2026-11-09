# 145: Clicking a sketch in the browser picks its hole centres

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("Hole centres")
- **Status:** todo
- **Depends on:** 117, 143
- **Needs:** windows build
- **Parallel-safe with:** 140, 141, 142 (not 143: same file)

## Goal

Aidan's request (2026-11-09): with the AWB Hole window open, clicking a sketch in the model browser picks all of that sketch's hole centres, **replacing** the current picks. This lets him switch from the default sketch to another one. The default when the window opens stays as it is: the newest sketch, lowest in the browser (task 117).

## Scope

- `src/InventorAddin/Holes/HoleSession.cs`: today a sketch selected **before** the window opens gives the centres (around `SketchesNewestFirst` and the `selectedSketch` handling), and clicks during the window add or remove points. While the window is open, a sketch picked in the browser (a `PlanarSketch`, or its proxy in place, through `NativeOf`) replaces the picks with that sketch's hole-centre points. Show the sketch the way the default sketch is shown, and put it back afterwards as today. Highlight and preview follow.
- `src/InventorAddin.Core/Holes/HoleCentrePicks.cs` or `HoleSessionRules.cs` and tests: the replace rule (which points of the sketch count, as for the default).
- `docs/features/09-awb-hole.md`, "Hole centres": one sentence.

Out of scope:

- Clicking a face to place a centre. Aidan decided against it on 2026-11-09 (130-10).
- Changing which sketch is the default.

## Acceptance criteria

- [ ] Core tests for the replace rule: a sketch with no usable points leaves the picks as they were (and the log says so); points already used by a feature are treated as for the default.
- [ ] A browser click on a sketch during the window replaces the picks; clicking points still adds and removes them.
- [ ] Built on Windows, 0 errors; Core tests pass.

## Notes for the implementer

Check first whether the session's select filter lets a browser sketch through while it is picking. If a browser click does not reach `SelectEvents` (for example because the interaction only allows sketch points), find out what does and log what you found. Do not guess API names; list any you could not confirm.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. A part with two sketches of hole-centre points: open AWB Hole (the newest sketch's points are picked). Click the other sketch in the browser: its points are picked and the first sketch's are not. The preview follows.
  2. Click a point: it is added or removed as before.
  3. OK: one hole feature on the second sketch's points. Both sketches are shown or hidden as before.
  4. The same in a part edited in place.

## Follow-ups
