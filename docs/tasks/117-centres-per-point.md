# 117: Default hole centres per point, like Inventor's Hole

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("The window": hole centres; question 11)
- **Status:** done (awaiting review)
- **Depends on:** 094
- **Needs:** windows build (Core rule first)
- **Parallel-safe with:** 110, 111, 112, 113, 119

## Goal

Aidan, 2026-10-24 (question 11): with nothing selected, AWB Hole picks centres per point, like Inventor's Hole: the newest visible sketch that has hole-centre points no feature uses yet, and only those points. This replaces task 094's per-sketch rule, which skipped a sketch whole once any feature consumed it.

## Scope

- `src/InventorAddin.Core/Holes/HoleSessionRules.cs`: the choice of sketch and of the points picked, from a list of sketches with their visibility, age and, per hole-centre point, whether a feature uses it. A selected sketch or selected points still win, consumed or not (task 094). Tests.
- `src/InventorAddin/Holes/HoleSession.cs`: read which points a feature already uses (from the sketch's dependent features, or each point's dependents, as is cheapest and reliable; check the members against existing usage or the API reference) and pass them to the rule; the start log line says how the sketch and points were chosen.

## Acceptance criteria

- [ ] A sketch with four hole centres, two used by an earlier hole, opens AWB Hole with the other two picked.
- [ ] A sketch whose centres are all used is skipped for the next newest with free centres.
- [ ] Selecting a sketch first still picks all its points.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes; the solution builds on Windows.

## Verification

What changed:

- Core `HoleSessionRules.DefaultCentres(selectedSketch, sketchOfSelectedPoint, sketchesNewestFirst, holeCentres, usedByFeature)` replaces task 094's `DefaultSketch`. It returns a `DefaultCentres<TSketch, TPoint>` record (sketch, points, `DefaultCentresFrom`, hole centres in the sketch) with `Describe()` for the log. A selected sketch, else the sketch of selected points, gives all its hole centres, used or not; else the newest visible sketch with at least one hole centre no feature uses gives only those. Visibility is read first and lazily; which points are used is asked only when nothing is selected. 9 new tests replace the 9 `DefaultSketch` tests.
- Add-in `HoleSession.StartDefaultPicks` passes the sketches (newest first, lazy visibility), each sketch's hole-centre points, and a lazy check against the points the part's hole features use (`HoleFeatures` -> `HoleCenterPoints`, read once on first use). A point counts as used when it is the same object, or in a sketch of the same name at an equal position (as `SamePoint` already compares). The start log line now says, for example, `3 centre(s) from sketch Sketch4 (newest visible with free centres, 2 of 4 hole centres, visible, consumed)`.
- `sketch.Consumed` is no longer part of the choice; it still shows in the log note.

Ran on Aidan's PC (Windows), 2026-11-03:

- `dotnet test tests/InventorAddin.Core.Tests`: passed 4185, failed 0.
- `dotnet build InventorAddin.slnx` (Inventor not running, deploy on): 0 errors, 8 warnings (the reviewer's no-deploy build gave 4), all CS8600 in files this task did not touch (`CreatePrintCommand`, `NewPartCommand`, `SaveAsCopyCommand`, `SaveAsReplaceCommand`).

Not compiled: none; `src/InventorAddin/Holes/HoleSession.cs` built on Windows. Not yet run in Inventor.

Inventor API members: all already used in the repo, none unconfirmed. `ComponentDefinition.Features.HoleFeatures` (HoleSession `FindHoleAgain`, PartExtractor), `HoleFeature.HoleCenterPoints` (HoleSession `StartEditPicks`, PartExtractor), `PlanarSketch.SketchPoints`, `SketchPoint.HoleCenter`, `SketchPoint.Parent`, `SketchPoint.Geometry3d`, `Point.IsEqualTo`, `PlanarSketch.Visible`, `PlanarSketch.Name` (all HoleSession). The behaviour is not confirmed in Inventor: that `HoleCenterPoints` returns the `SketchPoint`s of a sketch-placed hole (spike 081 round 4 relied on it for edits).

Default (for `DECISIONS.md`): only hole features count as using a hole-centre point. Other features that might use a sketch point (an iFeature, a sheet-metal punch, a feature that only uses the sketch's profile) are not read, so their points still count as free; a sketch consumed by an extrude but whose centres no hole uses is picked from.

Manual test in Inventor (AWB Hole has taken over Hole, or use the AWB Hole button):

1. New part. Sketch A with one hole-centre point; finish it. Then sketch B (newer) with four points marked as hole centres (Point, Center Point). Finish the sketch. Make an Inventor Hole (or AWB Hole) on two of them only: open Hole, which picks all four; deselect two (Ctrl-click) so two stay picked, OK.
2. Make sketch B visible again (right-click, Visibility). Clear the selection. Click AWB Hole. Expect "2 picked" and the other two points highlighted blue. The log (`%APPDATA%\AwbAddin\awbaddin.log`) shows `newest visible with free centres, 2 of 4 hole centres`. Cancel.
3. Make a hole on the remaining two of sketch B, OK. Show sketch B again and make sure sketch A is visible. Clear the selection, click AWB Hole: sketch B (all used) is skipped and sketch A's point is picked. Cancel.
4. Select sketch B (all four centres used) in the browser, then click AWB Hole: all four are picked; the log says `selected, all 4 hole centres`. Cancel.
5. Select one point of that sketch, then AWB Hole: all four picked; the log says `of the selected points, all 4 hole centres`. Cancel.
6. In an assembly, edit the part in place and repeat step 2: the same two points are picked.

## Follow-ups

- Should features other than holes (iFeatures, punches) count as using a hole-centre point? Only holes are read (Default above).
