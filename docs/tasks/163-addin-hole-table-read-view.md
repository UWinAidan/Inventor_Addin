# 163: Add-in: read a drawing view's holes and origin into Core (`IHoleTableSource`)

- **Milestone:** M4 part 2
- **Feature spec:** `docs/features/04-hole-table-wizard.md` ("Add-in side", open questions 7, 16 and 17)
- **Status:** done
- **Depends on:** 155, 157, 159
- **Needs:** windows (add-in; Aidan checks in Inventor)
- **Parallel-safe with:** 161, 162

## Goal

The add-in implements task 159's `IHoleTableSource`: it lists sheets and views, and analysing a view returns its holes as `HoleTableHole`s, its ordinate origin, its scale and units, and how many holes were left out. It does this the way spike 160 proved.

Task 159 settled the interface: sheets and views are passed by a stable `Key` the add-in chooses, with `Name` for display only (`HoleTableSheetInfo`, `HoleTableViewInfo`), because names change when a view is renamed or sheets are reordered. Pick a key that survives both, for example an attribute set on the `DrawingView` and the `Sheet` written the first time the wizard sees them, and say in Verification what was chosen. The view-model catches exceptions from the source, but still report through `ComSafe` warnings where the add-in has them.

## Scope

- **`src/InventorAddin/HoleTable/HoleTableSource.cs`.** Names follow the folder's style; this is a new folder.
- **Sheets and views.** The active drawing's `Sheets`, then each sheet's `DrawingViews` (names).
- **Analysing a view.** Use the view's referenced part (`ReferencedDocumentDescriptor.ReferencedDocument`). For an assembly view, stop with a message: part views only for now (spec question 7 in task 160).
  - For each unsuppressed `HoleFeature`:
    - **End on.** Take the axis of the first side face that is a cylinder or a cone (`SurfaceType` `kCylinderSurface` or `kConeSurface`, then `Geometry.AxisVector`). The view direction is `Camera.Eye.VectorTo(Camera.Target)`, normalised. If |dot| ≤ 0.999, count the hole's centres as left out (question 16).
    - **Facts.** `HoleFeatureReader.ReadFacts` for the facts, and `HoleStoredData` for the AWB spec, as AWB Hole does. Then task 155's `HoleTableHoles.From`.
      `From` takes the feature name, the centres, the AWB Taper Tapped row (when there is one), and the tolerance as a separate `HoleTolerance?`. Build the tolerance from the facts' ISO code and deviations (task 155's follow-up).
      Then set the two values `From` does not fill (task 156's follow-up), with `with`: `ThreadClass` from the thread's `Class`, and `TaperEngagementCm` from the taper thread's engagement length (Inventor gives it in mm; convert to cm). Without them a thread line loses its class (`- 6H`) and a taper thread its depth.
    - **Centres.** For each centre in `HoleCenterPoints` (a `SketchPoint`): the sheet position is `DrawingView.ModelToSheetSpace(point.Geometry3d)`. The `Id` key comes from `point.GetReferenceKey(ref byte[] key, 0)`, then `ReferenceKeyManager.KeyToString(key)` (spike 160, C11). The 1-based index is kept for display.
  - **Origin.** If `HasOriginIndicator`, use `OriginIndicator.Intent.PointOnSheet`. Otherwise give no origin, and task 157 stops with its message.
  - **Scale.** The view's `Scale`.
  - **Units.** The drawing's length units (`UnitsOfMeasure.LengthUnits`), mapped to `HoleUnits`.
- Every COM read goes through `ComSafe`. A hole that throws is skipped with a warning in the log; it does not stop the analysis.
- Keep a map from each hole `Id` to its `HoleFeature`, its centre point, and the view's drawing curves at that centre. Tasks 164 and 165 need them. Late-bound callers must pass `DrawingCurves(Missing)`; C# can omit the argument.

Out of scope: the window (task 167), drawing anything (tasks 164 and 165).

## Acceptance criteria

- [ ] Builds on Windows with `dotnet build InventorAddin.slnx`.
- [ ] The manual checklist below passes in Inventor 2026 on Aidan's test part.

## Notes for the implementer

- Spike 160, "Recommendations" and rounds 1 and 5 (`docs/tasks/160-spike-hole-table-in-inventor.md`): every member here was used in the spike. The spike's reading code is `Read-Holes` in `spikes/160/spike160-round7.ps1` on branch `spike/160-hole-table`.
- `kConeSurface` is 5893. A taper tapped hole has no cylinder side face.
- Check member names against `Extraction/` and the interop. List any you could not confirm under Verification.

## Verification

- **Ran:** `dotnet build InventorAddin.slnx -p:DeployToInventor=false` on Windows: build succeeded, no errors (after the review fixes too). `dotnet test tests/InventorAddin.Core.Tests`: 4789 tests (21 new: `HoleTableViewRulesTests`). A full run passed 4787 and failed 2, both Numbering timing flakes (`RegisterStoreTests.RealEnvironment_ProbeFindsAnExistingFolderAndNotAMissingOne`, `SaveAsCopyCreatorTests.FailureAfterAssigning...`) that move between runs; the HoleTable, HoleReadBack and Numbering groups run together pass 1150 of 1150.
- **Keys chosen:** a GUID string in an attribute set on each `Sheet` and each `DrawingView` (names in Core `HoleTableKeys`: `AWB_HoleTableKey`, attribute `Key`). Listing reads the keys first and starts one transaction ("AWB hole table keys") only when a key is missing or duplicated (a copy carries its original's key), so a listing that changes nothing adds no undo entry. A key write that fails inside `ComSafe` is logged and that object gets no key; writes made before it are committed with the transaction. `Add(name, true)` falls back to `false` (spike C10). Names and order do not matter to the key.
- **Namespace:** `InventorAddin.HoleTables` (not `InventorAddin.HoleTable`, which clashes with `Inventor.HoleTable` in `DrawingExtractor`). The folder is `HoleTable/`.
- **Not compiled (changed under `src/InventorAddin`):** none; the new file `src/InventorAddin/HoleTable/HoleTableSource.cs` was built on Windows. It has not been run in Inventor, and nothing creates a `HoleTableSource` yet (task 167).
- **Inventor API members not confirmed:** every member compiled against the 2026 interop. Not confirmed at run time (only the spike used them late-bound): `SketchPoint.GetReferenceKey(ref byte[], 0)` followed by `PartDocument.ReferenceKeyManager.KeyToString(ref byte[])` (spike used the part's manager; I pass an empty array as the by-ref argument), `DrawingView.DrawingCurves` enumerated with the optional argument omitted, `Sheet.AttributeSets` and `DrawingView.AttributeSets` writes, and `TaperedThreadInfo.EngagementLength` as a double in mm (spike 160).
- **Manual checklist for Inventor:** (task 167's window is needed to call the source; until then, read the log `awbaddin.log`; look for `Hole table:` lines.) Open the test drawing with the part views. (1) Open the wizard: the sheets and views list and the active sheet is preselected. The drawing shows as modified only after the first listing; open the wizard a second time and no new undo entry appears. After the first listing, one Undo removes all the keys. (2) Rename a view and reorder the sheets, reopen: the same view and sheet are still found by their keys. (3) Copy a view and copy a sheet, reopen: both copies are listed (with their own keys) and can be analysed. (4) Analyse VIEW1 (end on): all 18 holes listed, tags in the expected groups, origin from the ordinate origin, scale and units matching the drawing; reference keys are non-empty and the same across two analyses. (5) Analyse VIEW2 (side view): no holes and 18 left out. (6) A view with no ordinate origin: the no-origin message. An assembly view: the message that only single part views work for now. (7) A tapped hole shows its class (`- 6H`) and a taper tapped hole its depth.

## Follow-ups

- Task 166: `GenerateTable`, `CreateTables`, `UpdateTables` and `AlignTables` return "Not yet available." and `ReadState` returns null in `HoleTableSource`; implement them there (task 162 added `ReadState`, `UpdateTables` and the state argument).
- `HoleTableSource.FindHole(sheetKey, viewKey, holeId)` returns the hole's `HoleFeature`, centre `SketchPoint` and the view's `DrawingCurve`s at that centre for tasks 164 and 165; it holds what the last `Analyze` of that view read.
- Listing sheets and views writes key attributes into the drawing, which marks it modified; if that bothers, task 167 could list lazily or keys could be kept outside the drawing.
- A taper tapped hole's AWB row is matched from Inventor's thread designation and type (as `HoleReadBack` does), not from the stored AWB spec's size.
- Review fixes: brand names are Core constants (`HoleTableKeys`); the taper row match is `HoleReadBack.TaperTappedRowFor` (used by `TapDrillCm` too); the end-on rule is `HoleTableViewRules.IsEndOn` and the units mapping `HoleTableViewRules.UnitsFor` (mil, yard and mile imperial); all tested.
- Default (to log in DECISIONS.md): a hole whose axis or side face cannot be read is counted as left out, with a warning in the log.
- Curves: `FindHole` returns only `kCircleCurve` curves, each with its centre read once. Add `kCircularArcCurve` only if task 164 or 165 needs arcs.
- S3: the tolerance built in `ReadHole` (`HoleTolerance` from the facts) could be a Core factory.
- S5: a duplicate key that arises between two listings (a sheet or view copied while the wizard is open) is only repaired on the next listing.
- Tasks 164 and 165 should call `Analyze` again before `FindHole`, since `FindHole` serves only what the last `Analyze` of that view read and its COM objects may be stale.
