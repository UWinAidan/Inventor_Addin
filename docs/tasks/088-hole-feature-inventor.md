# 088: Hole feature in Inventor: create, change, tolerance, colour, stored data, read back

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md`; spike results in `docs/tasks/081-spike-awb-hole.md`
- **Status:** done
- **Depends on:** 084
- **Needs:** windows build (Inventor API)
- **Parallel-safe with:** 085, 086, 087

## Goal

A thin add-in class that turns Core's `HoleGeometry` into a real `HoleFeature` and back: create one from sketch points, change it in place, apply or remove the dowel tolerance, colour and stored data, and read any hole into a `HoleSpec` for editing. No window here.

## Scope

- `src/InventorAddin/Holes/` (new): the hole writer and reader, the fit appearance helper
- Core: an interface for it if the session (089) needs one for tests

## Acceptance criteria

- [x] Create from `HoleGeometry` and a collection of sketch points with `CreateSketchPlacementDefinition`, direction `kPositiveExtentDirection` into the material (flip uses the other), the `...Extent2` methods for angled and flat drill points, through all, distance and to-face; counterbore and countersink seats.
- [x] Tapped holes through `HoleFeatures._CreateTapInfo(..., kThreadTapDrillDiameter, ..., tapDrill)` with the tap drill in thread table units, so the model has the workbook's tap drill (spike 081). If the hidden member is missing or fails, fall back to `CreateTapInfo` (Inventor's minor diameter) and say so in the result and the log.
- [x] Change an existing hole in place: centres (`HoleCenterPoints`), diameter, depth, termination, seat (`SetCBore`, `SetCSink`, `SetDrilled`) and drill point; a change of tapped to untapped or back recreates the feature in the same place in the browser (or is refused with a message, if that cannot be done: say which).
- [x] After every create or change, `HealthStatus` and `Faces.Count` are checked; an unhealthy hole is reported as a refusal (spike 081: a wrong direction gives a hole that cuts nothing, without an error).
- [x] Dowel: tolerance with `SetToFits(kLimitsFitsStackedTolerance, code, "")` or `SetToDeviation(upper, lower)`, model at nominal; colour on **every face** (`HoleFeature.Faces`, bottom included) with a per-fit appearance in the part (`Assets.Add(kAssetTypeAppearance, "Generic", ...)`, `generic_diffuse` from the workbook RGB; reuse it if present, update its colour if the workbook changed).
- [x] Removing the dowel: `Tolerance.SetToDefault()`, each face `AppearanceSourceType = kPartAppearance`, the `AWB_Hole` attribute set deleted (spike 081 round 5).
- [x] Stored data: write and read Core's `HoleStoredData` on the `AWB_Hole` attribute set.
- [x] Read back any hole into a `HoleSpec` for edit mode: from stored data when present, else from its geometry (type, seat, diameter, depth, termination, thread), marked as an Inventor hole.
- [x] All COM reads through `ComSafe`; nothing here opens transactions (the session does).
- [x] `dotnet build InventorAddin.slnx` builds

## Notes for the implementer

- From task 084 and its review: store `HoleBuildResult.Resolved` (an unresolved spec throws in `ToAttributes`) with the applied state. Angles in `HoleGeometry` are degrees: pass them as unit strings (`"118 deg"`, as the spike did) or convert to radians. `HoleTolerance.UpperCm`/`LowerCm` go to `SetToDeviation` as they are; the tap drill goes to `_CreateTapInfo` in table units. When reading a hole back for the comparison, read deviations in cm and the ISO code only when the tolerance is the fits type.

- Resolve the workbook with `LibraryPaths.HoleWorkbook` and read it with `HoleWorkbookReader.Read(LibraryPath)`, so problems say which workbook it was. `MaterialLibraryMessages.AddinFolderUnknown` names only the material library and finish list; give the hole workbook its own wording if it is needed (task 086 follow-up).

- Every member above is in spike 081's confirmed list; anything else must be checked against the interop (`Autodesk.Inventor.Interop.dll`) before use.
- Units: Core gives centimetres; the tap drill is in table units by design.

## Verification

Filled in by the implementer.

- **Ran:** on Aidan's PC (Windows, Inventor 2026 installed, not deployed): `dotnet build InventorAddin.slnx -p:DeployToInventor=false -tl:off`: Build succeeded, 0 errors, 8 warnings (all CS8600 in existing commands, none in the new files). `dotnet test tests/InventorAddin.Core.Tests`: 3103 passed, 0 failed (this count includes task 085's tests; the `Holes` tests alone: 385 passed, 0 failed). Run again after the review fixes, same results. Nothing was run inside Inventor.
- **Not compiled (changed under `src/InventorAddin`):** none: every new file under `src/InventorAddin/Holes/` (`HoleFeatureWriter.cs`, `HoleWriteResult.cs`, `HoleFitWriter.cs`, `HoleStoredDataStore.cs`, `HoleFeatureReader.cs`, `HoleWorkbookLoader.cs`) compiled against the Inventor 2026 interop. None of it has run in Inventor yet.
- **Core added:** `Holes/HoleReadBack.cs` (`InventorHoleFacts`, `HoleReadBack.Read`: stored data first, else the closest spec from the geometry, marked `HoleSource.Inventor`, with a note per guess), `Holes/HoleInventorNames.cs` (`HoleFitAppearances` names; `HoleThreadNames.NominalSize` for `_CreateTapInfo`; `HoleThreadNames.TapDrillMatches`, which tells whether a hole's reported tap drill is the workbook's), `Holes/HoleFeatureMessages.cs`, and `HoleWorkbookMessages.AddinFolderUnknown`; tests in `tests/InventorAddin.Core.Tests/Holes/` (`HoleReadBackTests`, `HoleInventorNamesTests`, `HoleFeatureMessagesTests`, one more in `HoleWorkbookMessagesTests`).
- **Inventor API members not confirmed** (they compile, since each was checked against the interop by reflection, but none is in spike 081's confirmed list or used in `Extraction/`, so none has run):
  - `Parameter.Value` **set** in database units (`HoleDiameter.Value = cm`, `HoleTapInfo.ThreadDepth.Value = cm`). The spike set `Expression` with unit strings.
  - Angles passed to `Add...Extent2`, `SetDistanceExtent2`, `SetToFaceExtent2`, `SetCSink` and `AddCSink...` as **radians (double)**. The spike passed `"118 deg"` strings.
  - `HoleFeatures.AddCSinkByDistanceExtent2`, `AddCBoreByToFaceExtent2`, `AddCSinkByToFaceExtent2` (the spike says "the matching methods" work; only the drilled and counterbore distance ones are named). Optional drill point arguments are passed as `Type.Missing` when the hole has none.
  - `_CreateTapInfo` for **imperial** threads (the `NominalSizeDescription` argument is a guess, `1/4` or `10` from the designation) and with `FullTapDepth = true` and no thread depth; `CreateTapInfo` with full depth and no thread depth. A failure falls back as the criteria say.
  - Recreating in the same browser place: `HoleFeature.SetEndOfPart(false)`, `HoleFeature.Delete(true, true, true)`, `PartComponentDefinition.SetEndOfPartToTopOrBottom(false)`, `HoleFeature.Name` set.
  - **Units of `ThreadDepth` in `_CreateTapInfo`**: the writer passes centimetres (as the spike's `0.9` for 9 mm suggests), while the tap drill in the same call is in thread table units. If the thread depth comes out 10 times too long or short (or 2.54 times, imperial), this is why.
  - **Sign of `SetToDeviation`'s lower deviation**: the writer passes the lower deviation signed, as Core holds it (`-0.002` cm for -0.02 mm). The spike only tried a lower of 0. If Inventor expects the magnitude and applies the minus itself, a negative lower deviation shows as plus.
  - `HoleTapInfo.TapDrillDiameter` read: the interop documents it as a double in mm when Metric is true and inches when false, or Nothing; not yet read in Inventor. `TapDrillMatches` accepts a number in table units or cm, or text with mm, cm or in; anything else counts as unchanged, so a changed tap drill would then not be applied.
  - `PartFeature.HealthStatus` and `Name` on every feature of `PartComponentDefinition.Features`, logged after a recreate.
  - **A reference to a deleted hole after `Abort`**: when a recreate fails, the session aborts the inner transaction and Inventor brings back the old hole. Whether the `HoleFeature` object held from before the delete is valid again is not known.
  - `HealthStatusEnum.kUpToDateHealth` taken as the only healthy status after `PartDocument.Update`.
  - Reads: `HoleFeature.DrillPointType`, `HoleFeature.BottomTipAngle`, `HoleFeature.Extent` with `DistanceExtent.Direction` and `ThroughAllExtent.Direction` (for Flip), `Tolerance.HoleTolerance` (the ISO code), `HoleTapInfo.FullTapDepth` read typed (Extraction reads it late-bound).
  - Appearances: enumerating `PartDocument.Assets`, `Asset.AssetType`, `Color.Red`/`Green`/`Blue` read; `Assets.Add`'s third argument is named `Reserved` in the interop, so the appearance is found again by its display name, not by name.
- **Manual checklist for Inventor:** (exercised through 089 and 090; nothing in this task has a button). With the AWB Hole window from 090, on a scratch block with a sketch of hole centres:
  1. Clearance, no seat, Through All: the hole is made at every centre, into the material. Flip: the hole cuts nothing and the window shows "The hole cuts nothing..." (or the unhealthy message), and the last good hole stays.
  2. Distance with 118 deg, 90 deg and Flat drill points: the bottom changes; measure the cone angle.
  3. Counterbore and countersink: the seat sizes match the workbook row; change the size up and down several steps: no refusal.
  4. Tapped M6 (or any metric row): measure the modelled diameter; it is the workbook's tap drill, not Inventor's minor diameter. Then an imperial row: if the log has "is modelled at Inventor's minor diameter", report it (the imperial nominal size guess was wrong).
  5. Change a clearance hole to tapped and back while previewing, and on an existing hole with features after it: the hole stays in the same place in the browser and keeps its name; the features after it still compute.
  6. Dowel H7 (ISO row) and a Limits row: the diameter's tolerance shows H7 or the limits in Inventor's parameter (fx) dialog, the model stays at nominal, and every face, the bottom too, is red, blue or green by fit. Only one "AWB Press fit" (etc.) appearance is in the part after several holes. Change the RGB in the workbook, make another hole: the colour changes on all holes of that fit.
  7. Change a dowel into a clearance hole, OK: no tolerance, no colour, and the `AWB_Hole` attribute set holds the clearance hole's data (iLogic or the attribute helper). One Undo brings the dowel back.
  8. Blind tapped M6 with a thread depth shorter than the drill depth (say 9 mm thread, 12 mm drill): measure the thread depth in the model (the thread's cosmetic length, or the hole's fx dialog); it is 9 mm, not 90 or 0.9.
  9. The same with an imperial tapped row (say 0.4 in thread, 0.5 in drill): the thread depth is 0.4 in.
  10. A Limits row with a negative lower deviation (say +0 / -0.02 mm): the fx dialog shows the lower deviation as minus 0.02, and the measured limits are below nominal.
  11. A blind tapped hole changed to a shallower drill depth (and a thinner thread depth with it), in one step: no refusal; then deeper again: no refusal.
  12. Edit an AWB hole: every setting loads from its stored data. Edit a hole made by Inventor's Hole: the closest settings load, with the notes for anything guessed.

## Follow-ups

- **089 should call:** `HoleFeatureWriter.Create`/`Change` inside each inner transaction and abort it whenever `Succeeded` is false (also when `Hole` is set: an unhealthy hole is still in the model). Replace the held hole only when `Succeeded && Recreated`. After aborting a failed recreate (`Recreated` true, `Succeeded` false), do not trust the old reference: find the hole again, for example by its name in `HoleFeatures` (the recreate keeps the name) or by keeping its name before each change. Then on OK call `HoleFitWriter.Finish(doc, hole, build, log)` with the successful `HoleRules.Build` result, which applies or removes the dowel parts and writes the stored data. No Core interface was added for the session: it is add-in code with no tests, so nothing needed one.
- **090 should call** `HoleWorkbookLoader.Load(AddinServices.Settings, log)` to read the workbook, and `HoleFeatureReader.ReadSpec(hole, workbook, workbook.Settings.DefaultUnits)` for edit mode, passing `Spec` and `Notes` to `AwbHoleViewModel.ForEdit` (task 085) and using `IsInventorHole` for the "OK makes it an AWB hole" note.
- **Termination To needs a face picked**, and nothing in the window or in 089 picks one yet: `Create`/`Change` refuse To without a face ("Pick the face the hole ends at."). Plan the face pick in 089 or 090, or hide To until it is.
- **Recreating a tapped/untapped hole deletes the old feature** with its sketch and dependents kept. Features that used the old hole's faces (a fillet on its edge, a pattern of it) may fail after the change; the spike did not try this on an existing hole with dependents (checklist step 5). If it fails, refusing the change with a message is the other option the criteria allow.
- **`RemoveColour` resets only faces that carry one of our fit appearances**, so a colour a user put on a hole face by hand is kept. Spec 09 says "the colour on every face"; this reads it as "the fit colour". Confirm with Aidan.
- A changed tap drill (workbook edit) is applied by recreating the hole, when `TapDrillDiameter` can be read (see Verification).
- A thread made with the fallback (`CreateTapInfo`, Inventor's minor diameter) is recreated on every change, since the writer tries the tap drill again each time. Harmless, but slower; revisit if the fallback is ever seen in use.
