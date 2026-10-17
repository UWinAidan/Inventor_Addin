# 088: Hole feature in Inventor: create, change, tolerance, colour, stored data, read back

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md`; spike results in `docs/tasks/081-spike-awb-hole.md`
- **Status:** todo
- **Depends on:** 084
- **Needs:** windows build (Inventor API)
- **Parallel-safe with:** 085, 086, 087

## Goal

A thin add-in class that turns Core's `HoleGeometry` into a real `HoleFeature` and back: create one from sketch points, change it in place, apply or remove the dowel tolerance, colour and stored data, and read any hole into a `HoleSpec` for editing. No window here.

## Scope

- `src/InventorAddin/Holes/` (new): the hole writer and reader, the fit appearance helper
- Core: an interface for it if the session (089) needs one for tests

## Acceptance criteria

- [ ] Create from `HoleGeometry` and a collection of sketch points with `CreateSketchPlacementDefinition`, direction `kPositiveExtentDirection` into the material (flip uses the other), the `...Extent2` methods for angled and flat drill points, through all, distance and to-face; counterbore and countersink seats.
- [ ] Tapped holes through `HoleFeatures._CreateTapInfo(..., kThreadTapDrillDiameter, ..., tapDrill)` with the tap drill in thread table units, so the model has the workbook's tap drill (spike 081). If the hidden member is missing or fails, fall back to `CreateTapInfo` (Inventor's minor diameter) and say so in the result and the log.
- [ ] Change an existing hole in place: centres (`HoleCenterPoints`), diameter, depth, termination, seat (`SetCBore`, `SetCSink`, `SetDrilled`) and drill point; a change of tapped to untapped or back recreates the feature in the same place in the browser (or is refused with a message, if that cannot be done: say which).
- [ ] After every create or change, `HealthStatus` and `Faces.Count` are checked; an unhealthy hole is reported as a refusal (spike 081: a wrong direction gives a hole that cuts nothing, without an error).
- [ ] Dowel: tolerance with `SetToFits(kLimitsFitsStackedTolerance, code, "")` or `SetToDeviation(upper, lower)`, model at nominal; colour on **every face** (`HoleFeature.Faces`, bottom included) with a per-fit appearance in the part (`Assets.Add(kAssetTypeAppearance, "Generic", ...)`, `generic_diffuse` from the workbook RGB; reuse it if present, update its colour if the workbook changed).
- [ ] Removing the dowel: `Tolerance.SetToDefault()`, each face `AppearanceSourceType = kPartAppearance`, the `AWB_Hole` attribute set deleted (spike 081 round 5).
- [ ] Stored data: write and read Core's `HoleStoredData` on the `AWB_Hole` attribute set.
- [ ] Read back any hole into a `HoleSpec` for edit mode: from stored data when present, else from its geometry (type, seat, diameter, depth, termination, thread), marked as an Inventor hole.
- [ ] All COM reads through `ComSafe`; nothing here opens transactions (the session does).
- [ ] `dotnet build InventorAddin.slnx` builds

## Notes for the implementer

- Resolve the workbook with `LibraryPaths.HoleWorkbook` and read it with `HoleWorkbookReader.Read(LibraryPath)`, so problems say which workbook it was. `MaterialLibraryMessages.AddinFolderUnknown` names only the material library and finish list; give the hole workbook its own wording if it is needed (task 086 follow-up).

- Every member above is in spike 081's confirmed list; anything else must be checked against the interop (`Autodesk.Inventor.Interop.dll`) before use.
- Units: Core gives centimetres; the tap drill is in table units by design.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:** (exercised through 089 and 090)

## Follow-ups
