# 145: Clicking a sketch in the browser picks its hole centres

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("Hole centres")
- **Status:** done
- **Depends on:** 117, 143
- **Needs:** windows build
- **Parallel-safe with:** 140, 141, 142 (not 143: same file)

## Goal

Aidan's request (2026-11-09): with the AWB Hole window open, clicking a sketch in the model browser picks all of that sketch's hole centres, **replacing** the current picks. This lets him switch from the default sketch to another one. The default when the window opens stays as it is: the newest sketch, lowest in the browser (task 117).

## Scope

- `src/InventorAddin/Holes/HoleSession.cs`: today a sketch selected **before** the window opens gives the centres (around `SketchesNewestFirst` and the `selectedSketch` handling), and clicks during the window add or remove points. While the window is open, a sketch picked in the browser (a `PlanarSketch`, or its proxy in place, through `NativeOf`) replaces the picks with that sketch's hole-centre points. Show the sketch the way the default sketch is shown, and put it back afterwards as today. Highlight and preview follow.
- `src/InventorAddin.Core/Holes/HoleCentrePicks.cs` or `HoleSessionRules.cs` and tests: the replace rule (all the sketch's hole-centre points, as for a sketch selected before the window opens; Aidan's request wins over the earlier wording "as for the default").
- `docs/features/09-awb-hole.md`, "Hole centres": one sentence.

Out of scope:

- Clicking a face to place a centre. Aidan decided against it on 2026-11-09 (130-10).
- Changing which sketch is the default.

## Acceptance criteria

- [ ] Core tests for the replace rule: a sketch with no hole-centre points leaves the picks as they were (and the log says so); points already used by a feature are picked too, as for a sketch selected before the window opens. (Corrected: the brief first said "treated as for the default"; Aidan's request, "picks all of that sketch's hole centres", wins.)
- [ ] A browser click on a sketch during the window replaces the picks; clicking points still adds and removes them.
- [ ] Built on Windows, 0 errors; Core tests pass.

## Notes for the implementer

Check first whether the session's select filter lets a browser sketch through while it is picking. If a browser click does not reach `SelectEvents` (for example because the interaction only allows sketch points), find out what does and log what you found. Do not guess API names; list any you could not confirm.

## Verification

Filled in by the implementer.

- **Ran:** on Windows (Inventor closed), `dotnet build InventorAddin.slnx`: 0 errors, 9 warnings (all existing, none in the changed files); the add-in was deployed to `%APPDATA%\Autodesk\Inventor 2026\Addins`. `dotnet test tests/InventorAddin.Core.Tests`: 4471 passed, 0 failed, after the correction and the review fix below (10 new test cases for the replace rule and `HoleCentrePicks.Replace`).
- **Not compiled (changed under `src/InventorAddin`):** none; `Holes/HoleSession.cs` compiled on Windows. Not yet run in Inventor.
- **What reaches the session:** before this task the picking added only `kSketchPointFilter`, so a browser click on a sketch could not reach `SelectEvents.OnSelect`. The session now also adds `kSketchObjectFilter`. `OnSelect` takes a `PlanarSketch` (or a `PlanarSketchProxy` in place, through `NativeOf`) only when the selection device is not `kGraphicsWindowSelection`; a new `OnPreSelect` handler sets `DoHighlight = false` for sketches in the graphics window, so clicks there pick points only, as before. Every sketch click is logged with the device Inventor reported (`selection device kBrowserSelection`, or "picked in the graphics window"), so the log shows what arrived if a browser click does not work.
- **Behaviour:** the clicked sketch gives all its hole-centre points, used by a feature or not, as a sketch selected before the window opens does (Aidan's request; corrected from a first version that took only unused points). A sketch with no hole-centre points leaves the picks and logs why. A sketch of another document (in place: another part, or the assembly itself) is refused before anything is read or shown, and logged: `InSessionPart` compares the sketch's `Parent` component definition's `Document` with the part (same object, else same full file name), as `IsOurDocument` does (review fix). In an edit, a browser sketch click is logged and ignored (the spec keeps an edited hole in its own sketch).
- **Inventor API members not confirmed:** the signatures of `SelectEvents.OnPreSelect` (`ref object, out bool, ref ObjectCollection, SelectionDeviceEnum, Point, Point2d, View`), `SelectionFilterEnum.kSketchObjectFilter` and `SelectionDeviceEnum.kBrowserSelection` / `kGraphicsWindowSelection`, `PlanarSketch.Parent` and `ComponentDefinition.Document` (both typed `object`) were checked against the 2026 interop assembly and compile. Not confirmed in Inventor: that `kSketchObjectFilter` lets a browser click on a sketch node reach `OnSelect` inside `InteractionEvents` (and with which device), that `DoHighlight = false` stops a sketch being picked in the graphics window, and that in place the browser click arrives as a `PlanarSketchProxy`.
- **Manual checklist for Inventor:**
  1. A part with two sketches of hole-centre points: open AWB Hole (the newest sketch's points are picked). Click the other sketch in the browser: its points are picked and the first sketch's are not. The preview follows. The log has "Sketch... was clicked in the browser: all its N hole centres are picked. (selection device kBrowserSelection)". If nothing happens, send the log: no line means the click never reached the session.
  2. Click a point: it is added or removed as before. Hover a sketch line in the graphics window: the whole sketch is not highlighted, and clicking it does nothing.
  3. Click a sketch without hole-centre points in the browser: the picks stay, and the log says so. Click a sketch whose points an earlier hole already uses: all its points are picked.
  4. OK: one hole feature on the second sketch's points. Both sketches are shown or hidden as before.
  5. The same in a part edited in place.
  6. Edit an AWB hole and click another sketch in the browser: the picks stay, and the log says an edited hole keeps its own sketch.
  7. In place: click a sketch of another part, then a sketch of the assembly itself, in the browser. The picks stay, the log says the sketch is not in the part the hole is made in, and no other sketch changes visibility (also after OK and after Cancel).

## Follow-ups

- If a browser click does not reach `OnSelect` (step 1 gives no log line), the next thing to try is the browser pane's own node events (`BrowserPanes` / `BrowserPane.OnActivate`-style events); not done here as no existing code uses them and they are unconfirmed.
- The existing point-toggle path in `OnSelect` does not check that a picked point belongs to `_doc` in place either: a point of another part's sketch (proxy taken to its native) would be toggled in and shown (`ShowSketch`) outside the session's transaction, and `SameSketch`'s name fallback can confuse a same-named sketch of another document. It should get the same `InSessionPart` check.
- A sketch clicked in the browser with no hole-centre points is refused with a log line only; the window shows nothing. If Aidan wants a status message there, the view-model would need one.
