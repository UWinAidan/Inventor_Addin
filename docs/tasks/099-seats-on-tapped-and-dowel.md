# 099: Counterbore and countersink on tapped and dowel holes

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("Types and seats"; questions 6 and 9)
- **Status:** done
- **Depends on:** 093, 098
- **Needs:** windows build
- **Parallel-safe with:** 096

## Goal

Aidan, 090 check: a counterbore or countersink seat is allowed on tapped holes and on dowel holes. A tapped hole uses the row of the same size in the Counterbore or Countersink tab (Aidan, question 6). A dowel hole's seat sizes come from the same tabs, matched the same way, until Aidan answers question 9 (Default).

## Scope

- Core: `HoleRules` (drop the refusal of a seat on a tapped hole; the size match; checks), `HoleSpec`/`HoleGeometry`, `HoleStoredData` and `HoleReadBack` if a seated tapped or dowel hole stores or reads differently, `AwbHoleViewModel` (Seat and Fastener type for Tapped and AWB Dowel; Fit and Tolerance stay for a seated dowel), with tests
- `src/InventorAddin/Holes/HoleFeatureWriter.cs` (a seat with a tap info; a seated dowel's tolerance and colour); `HoleFitWriter` if needed

## Acceptance criteria

- [x] **Matching a seat row** (Default for both, spec 09): the Counterbore or Countersink rows, same units, whose Size has a Clearance row with the same nominal diameter as the thread's or the dowel's nominal. For example the thread `M6` (nominal 6) and a 6 mm dowel both use the `M6` rows; `#10-32 UNF` uses `#10`; `1/4-20 UNC` and a 1/4 in dowel use `1/4`. The Fastener type list holds those rows' Screw values. A size with no matching seat row offers no seat other than None, and a seat already chosen says why it cannot be kept.
- [x] Tapped with a seat: the thread and tap drill as today, the seat from the row; blind and Through All; full-depth and blind thread rules as today. Inventor makes it with the seat methods taking a tap info, as the writer does for untapped holes.
- [x] Dowel with a seat: nominal diameter, tolerance and fit colour as today; every face coloured, the seat faces too (Aidan: every face); Fit and Tolerance stay shown.
- [x] Checks: the seat must be wider than the hole (the tap drill's major diameter for a tapped hole: the thread's nominal), and a blind hole deeper than the seat; refusals say which value is wrong.
- [x] Changing a seated tapped or dowel hole's size, seat or type works in both directions without refusals (task 093's order rule), and editing (stored data, read back) keeps the seat.
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes; `dotnet build InventorAddin.slnx -p:DeployToInventor=false` builds

## Verification

Filled in by the implementer.

- **Ran:**
  - `dotnet build src/InventorAddin.Core`: 0 warnings, 0 errors.
  - `dotnet test tests/InventorAddin.Core.Tests`: 3329 passed, 0 failed (3291 before; the 7 tests that expected no seat on tapped and dowel holes were replaced).
  - `dotnet build InventorAddin.slnx -p:DeployToInventor=false -tl:off` on Aidan's PC: 0 errors; 16 CS8600 warnings, all in `Commands/` files this task did not touch (CreatePrint, NewPart, SaveAsCopy, SaveAsReplace).
  - Spike (throwaway console app driving its own Inventor 2026 through `Activator.CreateInstance`, quit at the end) on a 30 mm block, with the writer's own calls:
    - `AddCBoreByThroughAllExtent`, `AddCBoreByDistanceExtent2`, `AddCSinkByThroughAllExtent` and `AddCSinkByDistanceExtent2` take the `CreateTapInfo` tap info in place of the diameter (M6x1 and 10-32 UNF). The hole is healthy, the thread shows (pitch kept), and setting the tap drill afterwards (task 095) still models it at 5 mm.
    - `SetCBore`, the counterbore parameters, `SetCSink`, the countersink parameters and `SetDrilled` all work in place on a tapped hole, keeping its thread, pitch and tap drill. So does changing the drill depth and thread depth of a blind counterbored tapped hole. No writer change was needed, only its doc comment.
    - Drill depth is measured from the placement plane, counterbore included (drill 12, cbore 6: the bottom is at 12). The countersink cone runs down to the tap drill, as the rules work it out.
    - A 6 mm dowel with a counterbore, then changed to a countersink: `hole.Faces` includes the seat faces (4 faces, then 3), all take the fit appearance, and the H7 fit tolerance stays on `HoleDiameter`.
    - On a tapped hole `HoleDiameter.Value` cannot be read. `Change` already treats that as "wider", so the seat goes first.
- **Not compiled (changed under `src/InventorAddin`):** none. `src/InventorAddin/Holes/HoleFeatureWriter.cs` changed only in a doc comment and was built on Windows.
- **Inventor API members not confirmed:** none new. Every call used was run in the spike.
- **Manual checklist for Inventor:**
  1. Tapped M6 with a counterbore (SHCS): the counterbore is the `M6` SHCS row's, the thread and tap drill as before; Through All and blind. The same with a countersink. An imperial thread (`#10-32 UNF`) uses the `#10` rows.
  2. Dowel 6 mm with a counterbore: the hole at 6 mm with its tolerance, the counterbore from the `M6` row, every face, seat included, in the fit colour.
  3. Change size, seat and type on these holes while previewing: no refusal; OK, edit again (after 092): the seat is kept.
  4. Tapped M6 with a countersink, then choose a thread with no Countersink row of its size: the seat stays Countersink, with a message beside it saying no Countersink row is that size and to set Seat to None; OK is disabled. Set Seat to None: the message goes. The Seat list shows only the seats the size has rows for.
  5. Fastener type on a seated tapped hole lists only the screws with a row of that size (for example SHCS and LHCS for `M6` if the workbook has both); picking another changes the counterbore in the preview.

## Follow-ups

- **Thread depth on a counterbored blind tapped hole (question for Aidan, spec 09).** Inventor measures the drill depth from the placement plane, counterbore included (spike: drill 12 with a 6 mm counterbore leaves 6 mm of tapped hole below it). If the thread depth is measured the same way (the spike did not settle where the thread ends), a counterbore uses up part of the thread: an M6 with drill 10, thread 9 and a 6 mm counterbore would keep about 3 mm of thread. The default thread depth (1.5 x D) and drill depth do not allow for the seat. Should they be measured from the bottom of the counterbore (added to its depth) for a seated tapped hole? Today they are not, and nothing checks that the thread runs past the counterbore.
- **Inventor accepts a seat narrower than the thread.** A counterbore wider than the tap drill but narrower than the thread's nominal (5.5 mm on M6) builds without error. As this task asks, the rules refuse it; a note for Aidan in case that is too strict.
- **Fastener type sits above Size in the window**, but for a tapped or dowel hole its list depends on the size below it. The window mockup (tasks 102 and 104) could move it under Size for those holes.
- **A size whose same-size seat rows are all hidden (`Show = N`)** offers no seat. A hole that already has one keeps it, and the hidden row is still used.
- **Spec 09 question 9 is still open.** A dowel's seat comes from the same-size Counterbore or Countersink rows (Default). If Aidan picks seat columns on the Dowel tab instead, `HoleRules.BuildSeatOfSameSize` and the view-model's `RefreshSeats` and `RefreshFasteners` are the places to change.
