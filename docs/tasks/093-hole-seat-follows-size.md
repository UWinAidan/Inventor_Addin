# 093: Counterbore and countersink follow a size change

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("Types and seats")
- **Status:** todo
- **Depends on:** 090
- **Needs:** windows build
- **Parallel-safe with:** 094, 096, 098

## Goal

Fix the bug Aidan found in the 090 check (step 11): with a counterbore seat, increasing the hole size changed the through hole but not the counterbore. A counterbore or countersink must always match the workbook row after every change, up or down, for every seat.

## Scope

- `src/InventorAddin/Holes/HoleFeatureWriter.cs` (`Change`, `SetSeat`)
- Core only if the change order needs a rule there, with tests

## Acceptance criteria

- [ ] Changing the size of a counterbored hole up and down several steps leaves the counterbore diameter and depth equal to the row's, every time; the same for a countersink's diameter and angle.
- [ ] Changing between Counterbore, Countersink and None still works, in both directions.
- [ ] The cause is found and written in Verification. Likely: `HoleFeature.SetCBore` and `SetCSink` change the hole's type but do not change the sizes of a hole that already has that seat. If so, write the hole's own seat parameters (check the members against the interop, for example `CBoreDiameter`, `CBoreDepth`, `CSinkDiameter`, `CSinkAngle`) for a hole that already has the seat, keeping the order rule (a growing value after what bounds it, a shrinking one before).
- [ ] After each change the writer reads the seat back and logs a warning when it does not match the geometry, so a seat that did not follow shows in `awbaddin.log`.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes; `dotnet build InventorAddin.slnx -p:DeployToInventor=false` builds

## Notes for the implementer

- The interop is at `C:\Program Files\Autodesk\Inventor 2026\Bin\Autodesk.Inventor.Interop.dll`; check member names by reflection.
- You may test the API with a throwaway console app in the scratchpad that starts its own invisible Inventor (`Activator.CreateInstance(Type.GetTypeFromProgID("Inventor.Application"))`, `Visible = false`), makes a scratch block with a sketch point and a counterbored hole, changes its diameter and seat sizes, and reads them back, as spike 081 did. Quit that Inventor at the end. Do not attach to an Inventor Aidan has open.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. Clearance with a counterbore, Through All: change the size up five steps and down five steps. After each, measure the counterbore: diameter and depth match the workbook row. The same with Distance.
  2. The same with a countersink: diameter and angle match.
  3. Counterbore to Countersink to None and back while previewing: each matches its row.

## Follow-ups
