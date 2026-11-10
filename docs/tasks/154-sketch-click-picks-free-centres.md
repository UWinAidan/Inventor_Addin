# 154: A sketch clicked in the browser, or selected before the window opens, picks its free hole centres

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md`
- **Status:** done
- **Depends on:** 145, 117
- **Needs:** windows build
- **Parallel-safe with:** 152, 153 (disjoint files; run one after another anyway, they share the build)

## Goal

Decision (Aidan, 2026-11-10, checklist 145-3): clicking a sketch in the browser while the AWB Hole window is open picks only its **free** hole centres, the hole-centre points no feature uses yet. Used points can still be picked by clicking them directly. If every hole-centre point of that sketch is used, all of them are picked. The same rule applies to a sketch selected before AWB Hole opens (task 117, checklist 117-4). This replaces task 145's "all its hole centres" and task 094/117's "a selected sketch gives all its centres".

## Scope

- `src/InventorAddin.Core/Holes/HoleSessionRules.cs` and tests: `CentresOfPickedSketch` and the selected-sketch branch of `DefaultCentres` take the free points, or all points when none is free. Update the doc comments and the log text (for example "its 3 free hole centres of 5 are picked" and "all 5 are used, so all are picked").
- `src/InventorAddin/Holes/HoleSession.cs`: pass the "used by a feature" check the default path already uses to the browser-click path. The sketch of a selected **point** (not a selected sketch) is unchanged: it still gives all its centres.
- `docs/features/09-awb-hole.md`, "Hole centres": update the sentences for a selected sketch and a browser-clicked sketch.

Out of scope:

- The default sketch when nothing is selected (unchanged: newest visible with free centres).
- Clicking single points (unchanged).

## Acceptance criteria

- [ ] Core tests: a sketch with some used points gives only the free ones; a sketch with every point used gives all; a sketch with no hole centres leaves the picks; the selected-sketch branch of `DefaultCentres` follows the same rule; a selected point's sketch still gives all.
- [ ] `dotnet build InventorAddin.slnx` on Windows, 0 errors; Core tests pass.

## Notes for the implementer

`DefaultCentres` already filters with `usedByFeature` for the newest-sketch branch; reuse it. Inventor may be running: check `tasklist //FI "IMAGENAME eq Inventor.exe"` and build with `-p:DeployToInventor=false` while it runs.

## Verification

Filled in by the implementer.

- **Ran:** on Windows, `dotnet test tests/InventorAddin.Core.Tests`: 4569 passed, 0 failed. `dotnet build InventorAddin.slnx -p:DeployToInventor=false` (also with `--no-incremental`): 0 errors, 8 warnings, none in the files this task changed. Not deployed (Inventor may be running), and not run in Inventor.
- **Not compiled (changed under `src/InventorAddin`):** none. `src/InventorAddin/Holes/HoleSession.cs` was changed and compiles on Windows.
- **Inventor API members not confirmed:** none new. `HoleFeature.Name` and `HoleFeatures` were already used in `HoleSession.cs`.
- **What changed beyond the brief:** the browser-click path asks `CentresUsedByHoles(except: _hole)`, which skips the session's own preview hole (matched as the same object or by name). Without that, after the preview hole is made on a sketch's free points, clicking that sketch in the browser would count those points as used, and the click would pick all of the sketch's centres. The default path runs before any preview hole exists, so it is unchanged. The spec says this in one sentence.
- **Manual checklist for Inventor:**
  1. Open a part with a sketch (Sketch A) that has 5 hole-centre points, and make an Inventor Hole on 2 of them. Add a second sketch (Sketch B) with 2 hole-centre points, none used.
  2. Select Sketch A in the browser, then click AWB Hole. Expected: the 3 free points are picked (blue), the centres line says "3 picked", and the log's start line says "selected, 3 free of 5 hole centres".
  3. With the window open, click Sketch B in the browser, then Sketch A again. Expected: Sketch B gives its 2 points; Sketch A gives its 3 free points again, not 5 (the preview hole does not count as using them). The log says "its 3 free hole centres of 5 are picked".
  4. Click one of Sketch A's 2 used points in the model. Expected: it is added ("4 picked").
  5. Cancel. Make an Inventor Hole on Sketch A's other 3 points, so all 5 are used. Click Sketch A in the browser with the AWB Hole window open (start it from Sketch B or nothing selected). Expected: all 5 are picked and the log says "all 5 of its hole centres are used, so all are picked". Do the same by selecting Sketch A before opening AWB Hole: all 5, log "selected, all 5 hole centres are used, so all are picked".
  6. Cancel. Select one hole-centre point of Sketch A (not the sketch), then click AWB Hole. Expected: all 5 of Sketch A's centres are picked, as before this task.

## Follow-ups

- None.
