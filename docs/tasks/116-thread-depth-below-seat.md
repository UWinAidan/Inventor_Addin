# 116: Thread and drill depths measured from the bottom of the seat

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("Tapped holes"; question 12)
- **Status:** done, built on Windows, not yet run in Inventor
- **Depends on:** 114
- **Needs:** windows build (Core first, then the writer and reader)
- **Parallel-safe with:** none

## Goal

Aidan, 2026-10-24 (question 12): a seated tapped or taper tapped hole's thread and drill depths are measured from the bottom of the seat, so an M6 counterbored hole keeps its full thread below the counterbore.

## Scope

- Core (`HoleGeometry.cs`, `HoleRules.cs`, `HoleDiagram.cs`, `AwbHoleViewModel.cs`): the window's thread and drill depths are below the seat; the seat's depth is the counterbore or spotface depth, or for a countersink the depth where the cone meets the hole diameter (Default); the depth written to Inventor is the window's depth plus the seat's depth; read-back subtracts it. The diagram dimensions the depths from the seat bottom. Depth expressions (task 100) keep working: the written expression adds the seat depth to the user's (for example `(d0) + 6 mm` or the seat's parameter), so the hole stays parametric and the window shows the user's own expression on Edit.
- Stored data: record that depths are below the seat (a version bump), so a hole made before this task opens with its depths converted, not shifted.
- `src/InventorAddin/Holes/HoleFeatureWriter.cs`, `HoleFeatureReader.cs`: as spike 108 question 3 found Inventor measures them.
- A blind clearance, dowel or Simple hole's Depth is unchanged (Default, question 16).

## Acceptance criteria

- [ ] An M6 blind tapped hole with a 6 mm counterbore and thread depth 9 has 9 mm of thread below the counterbore in Inventor.
- [ ] Changing the seat depth keeps the thread length below it.
- [ ] Edit opens the hole with the depths below the seat; an older hole opens with its depths converted.
- [x] Core tests for each seat; `dotnet test tests/InventorAddin.Core.Tests` passes; the solution builds on Windows.

The first three are met in Core (the geometry written to Inventor, the read-back, the old-version conversion; tested) and wait on Aidan's run in Inventor (checklist below).

## Verification

- **Ran (Windows, Inventor not running):**
  - `dotnet build InventorAddin.slnx -p:DeployToInventor=false`: succeeded, 0 errors. The 4 warnings (CS8600 in the Create Print, New Part, Save As Copy and Save As Replace commands; MSBuild prints each twice) were there before this task.
  - `dotnet build src/InventorAddin.Core`: succeeded.
  - `dotnet test tests/InventorAddin.Core.Tests`: 4295 passed, 0 failed on 2026-11-05. Before this task: 4219 (task 114's count; tasks 115 and 117 came between).
  - Re-run on 2026-11-07, after a rate-limit stop: the build again succeeded with 0 errors. Three runs of the full suite each had 3 or 4 of 4295 failing, all in `Numbering` (`NewPartCreatorTests`, `RegisterStoreTests.RealEnvironment_...`, `SaveAsCopyCreatorTests`, `RenamePlanTests.ListFilesToSearch_ASlowListing_...`). Each ran for 3 s and touches timeouts or the real file system. Run alone, the `Numbering` tests pass (862 of 862), as do the `Holes` tests (1203 of 1203). This task changes no Numbering code, so these look like timing flakes when the suite runs in parallel (follow-up).
  - After review, 2026-11-07 (the `FromTopFace` rounding fix, the imperial countersink test, checklist step 11):
    - Build, with Inventor not running: succeeded, 0 errors, the same 4 CS8600 warnings.
    - `Holes` tests: 1204 of 1204 passed.
    - Full run: 4291 of 4296 passed; 5 `Numbering` tests failed, the same timing tests as above plus `SaveAsReplacerTests.Replace_NameThatCannotBeSet_...`.
    - Split in two: `Numbering` alone passed 862 of 862, and everything else passed 3434 of 3434, so all 4296 pass.
- **The rule (Core, `HoleDepthReference`).** Inventor's depth = the window's depth + `Direction` x the seat depth:
  - tapped or taper tapped, counterbore or countersink: +1 (Inventor measures from the start face; the window from the seat bottom);
  - tapped or taper tapped, spotface: 0 (both measure from the spotface bottom), so a threaded spotface hole is now written as typed, not less the spotface;
  - any other hole with a spotface: -1, as task 114 built (question 16's Default: the Depth stays from the top face);
  - no seat, and a counterbore or countersink on a clearance, dowel or Simple hole: 0.
  - The seat depth is the counterbore or spotface depth, or the countersink's cone down to the modelled hole diameter (`CountersinkDepth`; the tap drill on a tapped hole, the Taper Tapped row's tap drill on a taper tapped one).
  - The same rule goes for the drill depth and a thread depth that is not full (so task 136's Through All thread depth will follow it unchanged).
- **Built, file by file:**
  - `HoleDepthReference.cs`: rewritten from task 114's spotface-only form to a seat depth with a direction; `SeatDepthCm`, `CountersinkDepth`, `SeatDepthOf(facts)`; `ToWindow(facts, holeDiameterCm)` takes the tap drill, since Inventor does not report a tapped hole's diameter (task 099). `FromTopFace` and `Upgrade` convert data stored before this task. **`FirstOfDifference` is tightened** (task 114 follow-up) into `SplitWritten`: `(X) + (Y)` or `(X) - (Y)` reads back as X only when the operator is the seat's and Y is the seat's expression (whitespace ignored) or a length equal to the seat depth; any other form gets the seat taken back off. An X that is a plain length (`(12 mm) + (d1)`) reads back as a number, not as the expression `12 mm`.
  - `HoleRules.cs`: the geometry's `DepthCm`, thread depth and their expressions come from the rule above. Expressions stay parametric: `(d0) + (6 mm)`, `(12 mm) + (d1)` when the counterbore depth is the expression, `(d0) + (3.1 mm)` for a countersink. The checks "seat depth not less than the hole depth" (counterbore, spotface, countersink) now apply only to holes measured from the top face: a threaded hole's drill depth is below the seat and always clears it. This settles task 114's follow-up about checking a thread depth against the seat: with depths below the seat Inventor can no longer be given a thread shallower than the seat, and "thread deeper than drill" and "greater than zero" are still checked.
  - `HoleReadBack.cs`: `Read` converts the facts with the tap drill (new public `TapDrillCm`, from the Tapped or Taper Tapped row) for an Inventor-made hole, and with the applied diameter for an AWB hole; stored data older than version 5 is upgraded before the window opens and is what the re-apply check gets.
  - `HoleReapply.cs`: upgrades older data too, and converts the facts at the applied diameter.
  - `HoleStoredData.cs`: **version 5**. The shape is unchanged; `Version` (read, not written) and `DepthsBelowSeatVersion` are new. Versions 1 to 4 still read, with their version, and `HoleDepthReference.Upgrade` takes the seat depth off a seated blind threaded hole's drill and thread depths (numbers less the seat, expressions as `(expression) - (seat)`), the seat from the stored spec, else from the hole. The seat depth is rounded to 6 decimals in the spec's units, as `HoleRules` rounds a countersink cone (review fix: `FromCm`'s 5 decimals of an inch can be out by 1.27e-5 cm, more than the re-apply tolerance of 1e-5 cm, on an irrational cone). Tested at versions 4 and 1, and with an imperial 82 deg countersink, which fails with the old rounding.
  - `HoleDiagram.cs`: a tapped or taper tapped hole's drill and thread depths are drawn and dimensioned from the seat bottom (the counterbore floor, or where the countersink's cone meets the hole); a seat deeper than the drill is no longer shrunk for a threaded hole, since the drill runs on below it. Other holes are as before.
  - `AwbHoleViewModel.cs`: no change needed. The window's boxes already hold the depths the user types, the diagram input passes them through, and the checks are `HoleRules`'.
  - `HoleFeatureWriter.cs`: comments only. The writer copies the geometry's depths, which already carry the seat depth. `HoleFeatureReader.cs`: no change; the conversion is in Core.
  - Tests: `HoleDepthReferenceTests` (rewritten and extended: each seat on tapped, taper tapped and clearance holes, expressions, the tightened read-back, Inventor-made holes, re-apply, version 4 and version 1 data), `HoleDiagramTests` (dimensions from the seat bottom; two new fit cases), and updates to `HoleRulesTests`, `HoleReapplyRulesTests`, `HoleStoredDataTests`, `HoleTolerancedTests` and `HoleClearanceSeatWithoutRowTests` for the new rule and version.
- **Not compiled:** none. The whole solution was built on Windows. Nothing here has been run in Inventor.
- **Inventor API members:** no new members are used. The writer and reader are unchanged apart from comments.
- **Unconfirmed behaviour (Inventor):**
  - Where Inventor starts a thread depth on a counterbore hole. Taken as the start face, from task 099's observation (an M6 thread of 9 under a 6 mm counterbore kept about 3 mm of thread); not measured in the model.
  - Where it starts a thread depth on a countersink hole (taken as the start face) and on a spotface hole (taken as the spotface bottom, task 114's assumption). Spike 108's log could not show where the thread starts.
  - Whether Inventor keeps an expression such as `(d0) + (6 mm)` as written, so it reads back as `d0`. If it rewrites it, Edit shows `((rewritten)) - (6 mm)`, which is still right in value.
  - A taper tapped hole's countersink cone is worked out at the Taper Tapped row's tap drill; Inventor models its own table tap drill (spec 09 question 21), so the depth below the cone can differ from the model by a fraction of a millimetre.

**Manual checklist for Aidan (Inventor 2026, a block of your own at least 30 mm thick, with a few sketch points):**

1. AWB Hole. Type Tapped, M6, Termination Distance, Seat Counterbore, counterbore depth 6, Thread depth 9, Drill depth 12. The diagram dimensions 9 and 12 from the counterbore floor. OK. Open the hole in Inventor's own Hole dialog (or the Parameters dialog): the drill depth reads 18 and the thread depth 15. In the model, the thread texture runs 9 mm below the counterbore floor. (Acceptance criterion 1.)
2. Edit it in AWB Hole. It opens with Thread depth 9 and Drill depth 12, and no re-apply offer. Change the counterbore depth to 3 and OK. Inventor now reads drill 15 and thread 12: the thread is still 9 below the counterbore. One Undo brings back 18 and 15. (Criteria 2 and 3.)
3. Make the same hole with Thread depth `d0` (any length parameter of yours) and the counterbore depth `d1` (another). Inventor's thread depth reads `(d0) + (d1)`. Change `d1` in Parameters: the thread follows it. Edit: the Thread depth box shows `d0`. If it shows something longer, note what Inventor's parameter holds.
4. Seat Countersink 12 at 90 on an M6 blind hole (tap drill from your workbook), thread 9, drill 12. The diagram dimensions from where the cone meets the hole. Inventor's depths read 12 and 9 plus the cone's depth ((12 - tap drill) / 2 for 90 deg). Check in the model where the thread texture starts.
5. Seat Spotface 11 x 1 on an M6 blind hole, thread 9, drill 12. Inventor's depths read 12 and 9 (it measures them from the spotface bottom). Check in the model that the thread starts at the spotface bottom (task 114's assumption).
6. Taper Tapped NPT 1/4 blind with a countersink: Drill depth is below the cone; Inventor's depth is the drill plus the cone.
7. A clearance hole, blind, Depth 12 with a 2 mm counterbore, and one with a 2 mm spotface: both drills still end 12 mm below the top face (question 16's Default, unchanged).
8. **An older hole:** a seated blind tapped hole made before this build (for example from task 114's checklist). Edit it: it opens with its thread and drill depths 6 less (or the seat's depth less) than they showed before, with no re-apply offer, and OK without changes leaves Inventor's depths as they were.
9. On a counterbored tapped hole made with this build, change the counterbore depth in Inventor's own Hole dialog, then Edit in AWB Hole: the re-apply offer lists the counterbore depth and the drill and thread depths below it; OK puts the thread back to its length below the seat.
10. A counterbored tapped hole with a counterbore deeper than the drill depth (counterbore 14, drill 12): the window shows no problem, and Inventor makes the hole 26 deep.
11. **A seated tapped hole made with Inventor's own Hole command**, opened in AWB Hole: make an M6 blind tapped hole in Inventor's Hole dialog with a 6 mm counterbore, drill 18 and thread 15. Edit it with AWB Hole: it opens with Drill depth 12 and Thread depth 9 (the counterbore taken off). Do the same with a countersink: the depths open less the cone worked out at the workbook's tap drill for M6 ((countersink dia - tap drill) / 2 / tan(angle / 2)), so they can differ by a fraction of a millimetre from the cone in the model if Inventor's tap drill is not the workbook's. With a spotface, the depths open as Inventor shows them.

## Defaults

- **Inventor measures a thread depth from the same place as the drill depth**: the start face with a counterbore (task 099's observation) and a countersink, the spotface bottom with a spotface (task 114). Unconfirmed in the model for the countersink and the spotface (checklist steps 4 and 5).
- **The countersink's seat depth is the cone down to the modelled diameter**: the tap drill on a tapped hole, the Taper Tapped row's tap drill on a taper tapped one (not Inventor's own table tap drill, which the add-in cannot read or set; spec 09 question 21).
- **A countersink's seat depth is written into an expression as a number** (to 6 decimals in the window's units), since its diameter and angle take numbers. A counterbore or spotface depth goes in as its own expression when it was typed as one, else as its length. The hole's own Inventor seat parameter is not referenced: the writer notes Inventor refuses an expression that refers to the hole's own parameters (task 100). So a seat changed in Inventor's own dialog does not carry the thread with it; Edit offers to re-apply.
- **Re-apply on a threaded hole whose seat changed in Inventor** also lists the drill and thread depths, since the length below the seat changed.
- **Seat-depth checks do not apply to threaded holes**: a counterbore, spotface or countersink deeper than the drill depth is fine, since the drill is below it.
- **Older stored data** (versions 1 to 4) is converted when it is read: the seat depth from the stored spec, else from the hole; expressions become `(expression) - (seat)`. An old hole whose thread did not reach below its seat opens with a thread depth of 0 or less, and the window asks for one.
- **An expression read back as `(length) + (seat)`** opens as the plain number, not as the expression `12 mm`.
- **The diagram's summary line** (the collapsed diagram) is unchanged: it does not say "below the seat".

## Follow-ups

- Confirm in Inventor where the thread starts on a countersink and on a spotface hole (checklist steps 4 and 5). If a thread on a spotface hole starts at the start face, `HoleDepthReference.Direction` needs a separate thread direction for that case.
- Spec 09 question 16 is still open: if Aidan wants a blind clearance, dowel or Simple hole's Depth below the seat too, it is a change to `HoleDepthReference.Direction` (and a stored-data version bump, as here).
- Task 136 (Full depth on a Through All tapped hole) can use the same rule: `HoleRules` already converts any thread depth that is not full.
- If Inventor rewrites an expression such as `(d0) + (6 mm)` when it stores it (checklist step 3), `HoleDepthReference.SplitWritten` should accept Inventor's form.
- The diagram's summary line could say the threaded depths are below the seat.
- Four or five `Numbering` tests (timeouts and real-folder probes) fail now and then in a full parallel run on Aidan's PC and pass on their own; they could be made timing-independent or run in their own collection.
