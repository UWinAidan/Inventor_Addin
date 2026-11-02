# 114: Simple and Taper Tapped holes and spotfaces in Inventor and the window

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("Types and seats", "Simple holes", "Taper tapped holes")
- **Status:** todo (completed from spike 108's Recommendations, 2026-10-28)
- **Depends on:** 108, 112, 119
- **Needs:** windows build
- **Parallel-safe with:** none

## Goal

The AWB Hole window makes, changes and reads back Simple and Taper Tapped holes and spotfaces, and any seat on any type, as real Inventor hole features (Aidan, 2026-10-24).

## Scope

Spike 108's Results and Recommendations (`docs/tasks/108-spike-hole-types-and-spotface.md`) are the API reference for this task.

- `src/InventorAddin/Holes/HoleFeatureWriter.cs`:
  - **Spotface:** `HoleFeatures.AddSpotFaceByThroughAllExtent` / `AddSpotFaceByDistanceExtent2` on a new hole; `SetSpotFace(diameter, depth)` on an existing one. Every seat change (None, Counterbore, Countersink, Spotface) is made in place with `SetDrilled`, `SetCBore`, `SetCSink`, `SetSpotFace`; no recreate.
  - **Depth reference:** Inventor measures a spotface hole's `Depth` from the spotface bottom and a counterbore hole's from the start face. The window measures drill and thread depths from the bottom of the seat (spec 09 question 12), so the writer adds the seat depth for a counterbore and passes the depth unchanged for a spotface. Confirm against task 116 before writing it, and cover the conversion with Core tests (the arithmetic belongs in Core).
  - **Taper Tapped:** `HoleFeatures.CreateTaperedTapInfo(true, threadType, designation)` with the plain `AddDrilledBy...`, `AddCSinkBy...`, `AddSpotFaceBy...` calls; blind and Through All. The thread type and designation are the Taper Tapped tab's, which must match Inventor's thread table (`NPT` / `1/4`, `BSP Taper Internal` / `Rc 1/4`). Do not set `TapDrillDiameter`: Inventor refuses it before Add and ignores it after (spike 108 case 7).
  - **Simple:** the diameter written to the hole's diameter parameter as an expression, as task 100 writes depths.
- `src/InventorAddin/Holes/HoleFeatureReader.cs` (and `HoleReadBack` in Core):
  - `kSpotFaceHole` with `SpotFaceDiameter` and `SpotFaceDepth` reads as a Spotface seat (today: no seat).
  - A `TaperedThreadInfo` reads as Taper Tapped with its type and designation (today the cast to `HoleTapInfo` loses it).
  - A Simple diameter expression is shown without Inventor's trailing ` ul`.
  - Depths are converted back to "from the seat bottom" by the reverse of the writer's rule.
- `src/InventorAddin/Holes/HoleStoredDataStore.cs`: stores the new type, seat and diameter expression as for the existing ones.
- Core view-model (task 112's): **no Counterbore seat on Taper Tapped** (Aidan, 2026-10-28; Inventor's rule): the Counterbore button is disabled for that type, and switching to Taper Tapped from a counterbored hole moves the seat to None with a note. No Thread depth box on a taper tapped hole (Default until a spike round proves a taper thread depth can be set; spec 09 question); the drill depth stays editable.
- `src/InventorAddin/UI/AwbHoleWindow.xaml(.cs)`: the four Type buttons, the Spotface seat button, the Diameter box, the seat boxes for spotfaces and for seats on Simple and Taper Tapped holes, bound to task 112's view-model; the icons from task 119.
- Edit (task 092): a hole made by Inventor's command with a spotface or a tapered thread opens as the right type.

## Acceptance criteria

- [ ] Each type with each allowed seat (Taper Tapped: None, Countersink, Spotface) can be made, changed in the window, OK'd, undone in one step, cancelled, and edited again.
- [ ] Changing the seat on an existing hole keeps the feature (same name and browser place).
- [ ] A spotface hole and a counterbore hole with the same window depth end at the same depth below the seat bottom.
- [ ] Inventor-made spotface and taper tapped holes open in Edit as Spotface and as Taper Tapped with the right thread.
- [ ] A Simple hole's diameter typed as a parameter expression stays linked to that parameter.
- [ ] Counterbore is unavailable on Taper Tapped.
- [ ] Core tests for the depth conversion, the read-back of the new types and the Taper Tapped seat rule.
- [ ] The solution builds on Windows; `dotnet test tests/InventorAddin.Core.Tests` passes.

## Verification

Filled in by the implementer.

## Follow-ups
