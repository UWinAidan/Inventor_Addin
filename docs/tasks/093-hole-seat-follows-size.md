# 093: Counterbore and countersink follow a size change

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("Types and seats")
- **Status:** done
- **Depends on:** 090
- **Needs:** windows build
- **Parallel-safe with:** 094, 096, 098

## Goal

Fix the bug Aidan found in the 090 check (step 11): with a counterbore seat, increasing the hole size changed the through hole but not the counterbore. A counterbore or countersink must always match the workbook row after every change, up or down, for every seat.

## Scope

- `src/InventorAddin/Holes/HoleFeatureWriter.cs` (`Change`, `SetSeat`)
- Core only if the change order needs a rule there, with tests

## Acceptance criteria

- [ ] Changing the size of a counterbored hole up and down several steps leaves the counterbore diameter and depth equal to the row's, every time; the same for a countersink's diameter and angle. (API behaviour confirmed in the spike; awaits Aidan's check in the add-in)
- [ ] Changing between Counterbore, Countersink and None still works, in both directions. (API behaviour confirmed in the spike; awaits Aidan's check in the add-in)
- [x] The cause is found and written in Verification. Likely: `HoleFeature.SetCBore` and `SetCSink` change the hole's type but do not change the sizes of a hole that already has that seat. If so, write the hole's own seat parameters (check the members against the interop, for example `CBoreDiameter`, `CBoreDepth`, `CSinkDiameter`, `CSinkAngle`) for a hole that already has the seat, keeping the order rule (a growing value after what bounds it, a shrinking one before).
- [x] After each change the writer reads the seat back and logs a warning when it does not match the geometry, so a seat that did not follow shows in `awbaddin.log`.
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes; `dotnet build InventorAddin.slnx -p:DeployToInventor=false` builds

## Notes for the implementer

- The interop is at `C:\Program Files\Autodesk\Inventor 2026\Bin\Autodesk.Inventor.Interop.dll`; check member names by reflection.
- You may test the API with a throwaway console app in the scratchpad that starts its own invisible Inventor (`Activator.CreateInstance(Type.GetTypeFromProgID("Inventor.Application"))`, `Visible = false`), makes a scratch block with a sketch point and a counterbored hole, changes its diameter and seat sizes, and reads them back, as spike 081 did. Quit that Inventor at the end. Do not attach to an Inventor Aidan has open.

## Verification

- **Cause (confirmed with a throwaway console app in the scratchpad, driving its own invisible Inventor 2026, quit at the end):** `HoleFeature.SetCBore` and `SetCSink` change a hole's seat type, but on a hole that already has that seat they do nothing and raise no error. A counterbored hole stepped 3.4 to 4.5 to 5.5 to 6.6 to 9 to 11 mm and back, with `SetCBore(new values)` in the writer's order, kept its first counterbore (6.5 x 3.4 mm) every time; a countersunk hole kept its first 6.6 mm countersink (and went unhealthy from 9 mm up, the hole being wider than the sink); `SetCSink` with a new angle on a countersunk hole kept the old angle. Switching seat works and takes the new values: counterbore to countersink to none to counterbore to countersink to counterbore, each read back with the values passed. Writing the hole's own parameters works: `CBoreDiameter.Value`, `CBoreDepth.Value`, `CSinkDiameter.Value` and `CSinkAngle.Value` followed every step up and down, for Through All and for a 15 mm Distance counterbore, with the existing order rule (seat before the diameter when growing, after when shrinking), all healthy; one `UndoTransaction` of a step inside a transaction put the hole diameter and counterbore back.
- **Fix:** `SetSeat` now writes those four parameters (only when they differ) for a hole that already has the seat, and calls `SetCBore`/`SetCSink`/`SetDrilled` only to change the seat. After every create, change and re-make, `Check` reads the hole back (`HoleFeatureReader.ReadFacts`) and logs a warning to `awbaddin.log` for each seat value that does not match the geometry; the comparison is Core's new `HoleSeatCheck.Differences` (`src/InventorAddin.Core/Holes/HoleSeatCheck.cs`, tests in `tests/InventorAddin.Core.Tests/Holes/HoleSeatCheckTests.cs`).
- **Ran:** `dotnet build InventorAddin.slnx -p:DeployToInventor=false -tl:off` on Windows: succeeded, 0 errors, 8 warnings (all CS8600 in `Commands/` files this task did not touch). `dotnet test tests/InventorAddin.Core.Tests`: 3222 passed, 0 failed, 0 skipped.
- **Not compiled (changed under `src/InventorAddin`):** none; `src/InventorAddin/Holes/HoleFeatureWriter.cs` was built on Windows. Not yet run inside the add-in in Inventor.
- **Inventor API members not confirmed:** none. `HoleFeature.CBoreDiameter`, `CBoreDepth`, `CSinkDiameter`, `CSinkAngle` (`Parameter`, get-only properties) and `Parameter.Value` set were checked by reflection on the 2026 interop and run in the spike.
- **Manual checklist for Inventor:**
  1. Clearance with a counterbore, Through All: change the size up five steps and down five steps. After each, measure the counterbore: diameter and depth match the workbook row. The same with Distance.
  2. The same with a countersink: diameter and angle match.
  3. Counterbore to Countersink to None and back while previewing: each matches its row.
  4. After the steps above, open `awbaddin.log` in the AwbAddin data folder: no "does not match its seat" warnings.

## Follow-ups

- Spike 081's results list `SetCBore` and `SetCSink` under "editing a hole afterwards works"; that holds only for changing the seat type. Worth a note in `docs/tasks/081-spike-awb-hole.md` or the spec so later work does not rely on them to resize a seat.
- In `Change`, the seat is ordered against the hole diameter only. For a Distance counterbore, a counterbore made deeper than a hole made shallower in the same step (or the reverse) is not ordered against the hole depth; it did not come up with workbook rows, but if a refusal shows in the log for that case, order the counterbore depth against the extent too.
